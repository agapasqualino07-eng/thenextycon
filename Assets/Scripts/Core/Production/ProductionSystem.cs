using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    /// <summary>
    /// Centralised production: one Tick() drives every machine (no per-machine Update loops).
    /// Owns machine levels, permanent upgrades, overhauls, spare parts and temporary boosts.
    /// Output formula per machine:
    ///   BaseOutput * Level * Milestone * Upgrades(machine) * Overhaul * Synergy * Global * TempBoost
    /// </summary>
    public sealed class ProductionSystem
    {
        sealed class Machine
        {
            public MachineDef Def;
            public int Level;
            public int Overhaul;
            public double Produced;
        }

        sealed class Boost { public string Source; public double Multiplier; public double Remaining; }

        readonly GameConfig _cfg;
        readonly Wallet _wallet;
        readonly GameSignals _signals;
        readonly List<Machine> _machines = new List<Machine>();
        readonly Dictionary<string, Machine> _byId = new Dictionary<string, Machine>();
        readonly HashSet<string> _upgrades = new HashSet<string>();
        readonly List<Boost> _boosts = new List<Boost>();
        readonly Dictionary<string, UpgradeDef> _upgradeDefs;

        Func<int> _phase = () => 0;
        bool _dirty = true;
        double _baseOps;       // without temp boosts
        double _tapMult = 1;
        double _globalMult = 1;
        double _offlineEffAdd, _offlineCapAddHours;
        readonly Dictionary<string, double> _machineMult = new Dictionary<string, double>();

        public int Parts { get; private set; }
        public long TapCount { get; private set; }

        public ProductionSystem(GameConfig cfg, Wallet wallet, GameSignals signals)
        {
            _cfg = cfg; _wallet = wallet; _signals = signals;
            foreach (var d in cfg.Machines)
            {
                var m = new Machine { Def = d };
                _machines.Add(m); _byId[d.Id] = m;
            }
            _upgradeDefs = cfg.Upgrades.ToDictionary(u => u.Id);
        }

        /// <summary>Injects the current-phase provider (set by GameSession once ProgressionSystem exists).</summary>
        public void SetPhaseProvider(Func<int> phase) { _phase = phase; _dirty = true; }

        // ----------------------------------------------------------------- queries
        public IReadOnlyList<MachineDef> MachineDefs => _cfg.Machines;
        public bool HasMachine(string id) => _byId.ContainsKey(id);
        public int Level(string id) => _byId.TryGetValue(id, out var m) ? m.Level : 0;
        public int OverhaulLevel(string id) => _byId.TryGetValue(id, out var m) ? m.Overhaul : 0;
        public double Produced(string id) => _byId.TryGetValue(id, out var m) ? m.Produced : 0;
        public bool HasUpgrade(string id) => _upgrades.Contains(id);

        public bool IsUnlocked(string id)
        {
            if (!_byId.TryGetValue(id, out var m)) return false;
            if (m.Level > 0) return true;
            var d = m.Def;
            if (_phase() < d.UnlockPhase) return false;
            if (!string.IsNullOrEmpty(d.UnlockRequiresMachineId) && Level(d.UnlockRequiresMachineId) < d.UnlockRequiresLevel) return false;
            return true;
        }

        public double NextCost(string id, int count = 1)
        {
            if (!_byId.TryGetValue(id, out var m)) return double.PositiveInfinity;
            count = Math.Min(count, m.Def.MaxLevel - m.Level);
            if (count <= 0) return double.PositiveInfinity;
            return Formulas.BulkCost(m.Def.BaseCost, m.Def.CostGrowth, m.Level, count);
        }

        public int MaxAffordableCount(string id)
        {
            if (!_byId.TryGetValue(id, out var m) || !IsUnlocked(id)) return 0;
            return Formulas.MaxAffordable(m.Def.BaseCost, m.Def.CostGrowth, m.Level, _wallet.Balance, m.Def.MaxLevel - m.Level);
        }

        /// <summary>Production per second including temporary boosts.</summary>
        public double OutputPerSecond { get { Recompute(); return _baseOps * TempMultiplier; } }
        /// <summary>Production per second excluding temporary boosts (used for offline pay and event pricing).</summary>
        public double BaseOutputPerSecond { get { Recompute(); return _baseOps; } }

        public double MachineOutput(string id)
        {
            Recompute();
            if (!_byId.TryGetValue(id, out var m)) return 0;
            return MachineBase(m) * TempMultiplier;
        }

        public double TapValue
        {
            get
            {
                Recompute();
                return (_cfg.Economy.TapBase + _cfg.Economy.TapOpsFraction * _baseOps) * _tapMult * TempMultiplier;
            }
        }

        public double OfflineEfficiency => Math.Min(_cfg.Offline.MaxEfficiency, _cfg.Offline.Efficiency + OfflineEffAdd());
        public double OfflineMaxSeconds => Math.Min(_cfg.Offline.MaxSecondsCeiling, _cfg.Offline.MaxSeconds + OfflineCapAddHours() * 3600.0);
        double OfflineEffAdd() { Recompute(); return _offlineEffAdd; }
        double OfflineCapAddHours() { Recompute(); return _offlineCapAddHours; }

        public double TempMultiplier
        {
            get
            {
                double p = 1;
                foreach (var b in _boosts) p *= b.Multiplier;
                return Math.Min(p, _cfg.Economy.MaxTempMultiplier);
            }
        }

        // ----------------------------------------------------------------- actions
        /// <summary>Taps the bench: adds TapValue. Returns the amount credited.</summary>
        public double Tap()
        {
            double v = TapValue;
            double applied = _wallet.Add(v, "tap");
            TapCount++;
            if (applied > 0) _signals.RaiseEarned(applied, "");
            _signals.RaiseTapped();
            return applied;
        }

        /// <summary>Buys <paramref name="count"/> levels atomically (all or nothing).</summary>
        public bool TryBuy(string id, int count = 1)
        {
            if (count <= 0 || !_byId.TryGetValue(id, out var m)) return false;
            if (!IsUnlocked(id) || m.Level + count > m.Def.MaxLevel) return false;
            double cost = Formulas.BulkCost(m.Def.BaseCost, m.Def.CostGrowth, m.Level, count);
            if (!_wallet.TrySpend(cost, "buy:" + id + "x" + count)) return false;
            m.Level += count;
            _dirty = true;
            _signals.RaiseMachineLevel(id, m.Level);
            return true;
        }

        public bool TryBuyMax(string id)
        {
            int n = MaxAffordableCount(id);
            return n > 0 && TryBuy(id, n);
        }

        public bool IsUpgradeAvailable(string id)
        {
            if (!_upgradeDefs.TryGetValue(id, out var u) || _upgrades.Contains(id)) return false;
            if (_phase() < u.UnlockPhase) return false;
            if (!string.IsNullOrEmpty(u.RequiresMachineId) && Level(u.RequiresMachineId) < u.RequiresMachineLevel) return false;
            return true;
        }

        public bool TryBuyUpgrade(string id)
        {
            if (!IsUpgradeAvailable(id)) return false;
            var u = _upgradeDefs[id];
            if (!_wallet.TrySpend(u.Cost, "upgrade:" + id)) return false;
            _upgrades.Add(id);
            _dirty = true;
            _signals.RaiseUpgrade(id);
            return true;
        }

        public int OverhaulPartsCost(string id)
        {
            int ol = OverhaulLevel(id);
            if (ol >= _cfg.Economy.OverhaulMaxLevel) return int.MaxValue;
            return (int)Math.Ceiling(_cfg.Economy.OverhaulPartsBase * Num.SafePow(_cfg.Economy.OverhaulPartsGrowth, ol));
        }

        public bool TryOverhaul(string id)
        {
            if (!_byId.TryGetValue(id, out var m) || m.Level <= 0) return false;
            int cost = OverhaulPartsCost(id);
            if (cost == int.MaxValue || Parts < cost) return false;
            Parts -= cost;
            m.Overhaul++;
            _dirty = true;
            return true;
        }

        public void AddParts(int n) { if (n > 0) Parts = (int)Math.Min(int.MaxValue / 2, (long)Parts + n); }

        // ----------------------------------------------------------------- boosts
        /// <summary>
        /// Adds a temporary multiplier. The same source never stacks with itself: re-adding takes the
        /// stronger multiplier and the longer remaining time. The product of all sources is capped.
        /// </summary>
        public void AddBoost(string source, double multiplier, double seconds)
        {
            if (!(multiplier > 1) || !(seconds > 0) || !Num.IsFinite(multiplier) || !Num.IsFinite(seconds)) return;
            var existing = _boosts.FirstOrDefault(b => b.Source == source);
            if (existing != null)
            {
                existing.Multiplier = Math.Max(existing.Multiplier, multiplier);
                existing.Remaining = Math.Max(existing.Remaining, seconds);
            }
            else _boosts.Add(new Boost { Source = source, Multiplier = multiplier, Remaining = seconds });
        }

        public double BoostRemaining(string source)
        {
            var b = _boosts.FirstOrDefault(x => x.Source == source);
            return b == null ? 0 : b.Remaining;
        }

        public IReadOnlyList<(string source, double multiplier, double remaining)> ActiveBoosts
            => _boosts.Select(b => (b.Source, b.Multiplier, b.Remaining)).ToList();

        // ----------------------------------------------------------------- tick
        /// <summary>Advances production by dt seconds of play time.</summary>
        public void Tick(double dt)
        {
            if (!(dt > 0) || !Num.IsFinite(dt)) return;
            Recompute();
            double temp = TempMultiplier; // boost expiry inside the step is ignored (steps are ~frame sized)
            foreach (var m in _machines)
            {
                if (m.Level <= 0) continue;
                double amount = MachineBase(m) * temp * dt;
                if (amount <= 0) continue;
                double applied = _wallet.Add(amount, "prod:" + m.Def.Id);
                if (applied > 0)
                {
                    m.Produced += applied;
                    _signals.RaiseEarned(applied, m.Def.Id);
                }
            }
            TickBoosts(dt);
        }

        /// <summary>Reduces boost timers without paying anything (used for offline time).</summary>
        public void TickBoosts(double dt)
        {
            for (int i = _boosts.Count - 1; i >= 0; i--)
            {
                _boosts[i].Remaining -= dt;
                if (_boosts[i].Remaining <= 0) _boosts.RemoveAt(i);
            }
        }

        // ----------------------------------------------------------------- maths
        void Recompute()
        {
            if (!_dirty) return;
            _dirty = false;
            _tapMult = 1; _globalMult = 1; _offlineEffAdd = 0; _offlineCapAddHours = 0;
            _machineMult.Clear();
            foreach (var id in _upgrades)
            {
                var u = _upgradeDefs[id];
                switch (u.Effect)
                {
                    case UpgradeEffect.TapMultiplier: _tapMult *= u.Value; break;
                    case UpgradeEffect.GlobalMultiplier: _globalMult *= u.Value; break;
                    case UpgradeEffect.OfflineEfficiencyAdd: _offlineEffAdd += u.Value; break;
                    case UpgradeEffect.OfflineCapHoursAdd: _offlineCapAddHours += u.Value; break;
                    case UpgradeEffect.MachineMultiplier:
                        _machineMult.TryGetValue(u.TargetMachineId, out double cur);
                        _machineMult[u.TargetMachineId] = (cur == 0 ? 1 : cur) * u.Value;
                        break;
                }
            }
            double ops = 0;
            foreach (var m in _machines) ops += MachineBase(m);
            _baseOps = Num.Sanitize(ops);
        }

        // Output of one machine without temp boosts. Uses cached upgrade multipliers (valid after Recompute starts).
        double MachineBase(Machine m)
        {
            if (m.Level <= 0) return 0;
            var e = _cfg.Economy;
            double milestone = e.MilestoneEvery > 0 ? Num.SafePow(e.MilestoneMultiplier, m.Level / e.MilestoneEvery) : 1;
            _machineMult.TryGetValue(m.Def.Id, out double up);
            if (up == 0) up = 1;
            double overhaul = 1 + e.OverhaulBonus * m.Overhaul;
            double synergy = 1;
            int phase = _phase();
            foreach (var s in _cfg.Synergies)
                if (s.TargetMachineId == m.Def.Id && phase >= s.UnlockPhase)
                    synergy += s.BonusPerSourceLevel * Level(s.SourceMachineId);
            double v = m.Def.BaseOutput * m.Level * milestone * up * overhaul * synergy * _globalMult;
            return Num.Sanitize(v);
        }

        /// <summary>Call when something outside the system changes inputs (phase change).</summary>
        public void MarkDirty() { _dirty = true; }

        // ----------------------------------------------------------------- persistence
        public ProductionSave Capture()
        {
            var s = new ProductionSave { Parts = Parts, TapCount = TapCount };
            foreach (var m in _machines)
                if (m.Level > 0 || m.Produced > 0)
                    s.Machines.Add(new MachineSave { Id = m.Def.Id, Level = m.Level, Overhaul = m.Overhaul, Produced = Num.Sanitize(m.Produced) });
            s.Upgrades.AddRange(_upgrades);
            foreach (var b in _boosts) s.Boosts.Add(new BoostSave { Source = b.Source, Multiplier = b.Multiplier, Remaining = b.Remaining });
            return s;
        }

        public void Restore(ProductionSave s)
        {
            foreach (var m in _machines) { m.Level = 0; m.Overhaul = 0; m.Produced = 0; }
            _upgrades.Clear(); _boosts.Clear();
            if (s == null) { _dirty = true; return; }
            foreach (var ms in s.Machines)
            {
                if (!_byId.TryGetValue(ms.Id ?? "", out var m)) continue; // unknown id (removed content): ignore safely
                m.Level = Math.Max(0, Math.Min(m.Def.MaxLevel, ms.Level));
                m.Overhaul = Math.Max(0, Math.Min(_cfg.Economy.OverhaulMaxLevel, ms.Overhaul));
                m.Produced = Math.Max(0, Num.Sanitize(ms.Produced));
            }
            foreach (var u in s.Upgrades) if (_upgradeDefs.ContainsKey(u ?? "")) _upgrades.Add(u);
            foreach (var b in s.Boosts)
                if (b != null && b.Multiplier > 1 && b.Remaining > 0 && Num.IsFinite(b.Multiplier) && Num.IsFinite(b.Remaining))
                    _boosts.Add(new Boost { Source = b.Source ?? "", Multiplier = b.Multiplier, Remaining = b.Remaining });
            Parts = Math.Max(0, s.Parts);
            TapCount = Math.Max(0, s.TapCount);
            _dirty = true;
        }
    }
}
