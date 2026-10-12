using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    /// <summary>
    /// Configurable industrial orders. Up to MaxActive orders are active at once, taken in config order
    /// among those whose phase is reached and whose prerequisite order was claimed. Progress is driven by
    /// signals (earned/taps/levels/upgrades/events) and by polling for absolute goals (level, output/s).
    /// Orders are completed via the normal production path; they never require the risk/reward module
    /// (except explicitly marked CompleteEvent orders, which are optional extras).
    /// </summary>
    public sealed class OrderSystem
    {
        public const int MaxActive = 4;

        public sealed class OrderView
        {
            public OrderDef Def;
            public OrderStatus Status;
            public double Progress;       // clamped to Target
            public double RemainingSeconds; // 0 when no deadline
            public double Fraction => Def.Target <= 0 ? 1 : Num.Clamp(Progress / Def.Target, 0, 1);
            public double RewardCurrency;
        }

        readonly GameConfig _cfg;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly ProgressionSystem _progression;
        readonly ClaimLedger _ledger;
        readonly GameSignals _signals;
        readonly Dictionary<string, OrderStateSave> _states = new Dictionary<string, OrderStateSave>();
        readonly Dictionary<string, OrderDef> _defs;

        /// <summary>Wired by GameSession to CrateSystem.</summary>
        public Action<int, string> GrantCrates = (n, s) => { };

        public bool TutorialCompleted { get; private set; }
        public int EventsCompleted { get; private set; }

        public OrderSystem(GameConfig cfg, Wallet wallet, ProductionSystem production, ProgressionSystem progression,
                           ClaimLedger ledger, GameSignals signals)
        {
            _cfg = cfg; _wallet = wallet; _production = production; _progression = progression; _ledger = ledger; _signals = signals;
            _defs = cfg.Orders.ToDictionary(o => o.Id);
            signals.Earned += OnEarned;
            signals.Tapped += OnTapped;
            signals.RiskEventCompleted += OnEventCompleted;
        }

        // ---------------------------------------------------------------- queries
        public IEnumerable<OrderView> Views()
        {
            foreach (var kv in _states)
            {
                if (kv.Value.Status == OrderStatus.Claimed) continue;
                yield return ToView(kv.Value);
            }
        }

        public OrderView Get(string id) => _states.TryGetValue(id, out var s) ? ToView(s) : null;

        public int ClaimableCount => _states.Values.Count(s => s.Status == OrderStatus.Completed);
        public bool IsCompleted(string id) => _states.TryGetValue(id, out var s) && s.Status == OrderStatus.Completed;

        OrderView ToView(OrderStateSave s)
        {
            var d = _defs[s.Id];
            return new OrderView
            {
                Def = d, Status = s.Status, Progress = Math.Min(s.Progress, d.Target),
                RemainingSeconds = d.DeadlineSeconds > 0 && s.Status == OrderStatus.Active ? Math.Max(0, s.Remaining) : 0,
                RewardCurrency = RewardFor(d)
            };
        }

        public double RewardFor(OrderDef d)
            => Math.Max(d.RewardMin, d.RewardOpsSeconds * _production.BaseOutputPerSecond);

        // ---------------------------------------------------------------- updates
        public void Update(double dt)
        {
            Activate();
            foreach (var s in _states.Values.ToList())
            {
                if (s.Status != OrderStatus.Active) continue;
                var d = _defs[s.Id];
                PollAbsolute(s, d);
                if (s.Progress >= d.Target) { s.Status = OrderStatus.Completed; continue; }
                if (d.DeadlineSeconds > 0 && dt > 0)
                {
                    s.Remaining -= dt;
                    if (s.Remaining <= 0)
                    {
                        // Expired: restart without penalty.
                        s.Remaining = d.DeadlineSeconds;
                        s.Progress = 0;
                        s.Baseline = BaselineFor(d);
                        _signals.RaiseToast("toast.order_expired");
                    }
                }
            }
        }

        void Activate()
        {
            int active = _states.Values.Count(s => s.Status != OrderStatus.Claimed);
            if (active >= MaxActive) return;
            foreach (var d in _cfg.Orders)
            {
                if (active >= MaxActive) break;
                if (_states.ContainsKey(d.Id)) continue;
                if (d.UnlockPhase > _progression.PhaseIndex) continue;
                if (!string.IsNullOrEmpty(d.PrerequisiteOrderId) &&
                    !(_states.TryGetValue(d.PrerequisiteOrderId, out var pre) && pre.Status == OrderStatus.Claimed)) continue;
                _states[d.Id] = new OrderStateSave
                {
                    Id = d.Id, Status = OrderStatus.Active, Progress = 0, Remaining = d.DeadlineSeconds, Baseline = BaselineFor(d)
                };
                active++;
            }
        }

        double BaselineFor(OrderDef d)
        {
            switch (d.Type)
            {
                case OrderType.ProduceWithMachine: return _production.Produced(d.TargetId);
                default: return 0;
            }
        }

        void PollAbsolute(OrderStateSave s, OrderDef d)
        {
            switch (d.Type)
            {
                case OrderType.MachineLevel: s.Progress = Math.Max(s.Progress, _production.Level(d.TargetId)); break;
                case OrderType.OutputPerSecond: s.Progress = Math.Max(s.Progress, _production.BaseOutputPerSecond); break;
                case OrderType.BuyUpgrade: if (_production.HasUpgrade(d.TargetId)) s.Progress = d.Target; break;
                case OrderType.ProduceWithMachine: s.Progress = Math.Max(s.Progress, _production.Produced(d.TargetId) - s.Baseline); break;
            }
        }

        void OnEarned(double amount, string machineId)
        {
            foreach (var s in _states.Values)
            {
                if (s.Status != OrderStatus.Active) continue;
                if (_defs[s.Id].Type == OrderType.EarnCurrency) s.Progress += amount;
            }
        }

        void OnTapped()
        {
            foreach (var s in _states.Values)
                if (s.Status == OrderStatus.Active && _defs[s.Id].Type == OrderType.TapCount) s.Progress += 1;
        }

        void OnEventCompleted(string kind)
        {
            EventsCompleted++;
            foreach (var s in _states.Values)
            {
                if (s.Status != OrderStatus.Active) continue;
                var d = _defs[s.Id];
                if (d.Type == OrderType.CompleteEvent && (d.TargetId == "any" || d.TargetId == "" || d.TargetId == kind)) s.Progress += 1;
            }
        }

        // ---------------------------------------------------------------- claim
        /// <summary>Pays the order reward exactly once.</summary>
        public bool TryClaim(string id) => TryClaim(id, 1.0);

        /// <summary>Pays the order reward exactly once; currencyMultiplier &gt; 1 only after a confirmed rewarded ad.</summary>
        public bool TryClaim(string id, double currencyMultiplier)
        {
            if (!_states.TryGetValue(id, out var s) || s.Status != OrderStatus.Completed) return false;
            if (!_ledger.TryClaim("order:" + id)) { s.Status = OrderStatus.Claimed; return false; }
            var d = _defs[id];
            _wallet.Add(RewardFor(d) * Math.Max(1, Num.Sanitize(currencyMultiplier, 1)), "order:" + id);
            if (d.RewardParts > 0) _production.AddParts(d.RewardParts);
            if (d.RewardCrates > 0) GrantCrates(d.RewardCrates, "order:" + id);
            s.Status = OrderStatus.Claimed;
            if (!TutorialCompleted && _cfg.Orders.Where(o => o.IsTutorial).All(o => _states.TryGetValue(o.Id, out var st) && st.Status == OrderStatus.Claimed))
                TutorialCompleted = true;
            _signals.RaiseOrderClaimed(id);
            Activate();
            return true;
        }

        // ---------------------------------------------------------------- persistence
        public OrdersSave Capture()
        {
            var o = new OrdersSave { TutorialCompleted = TutorialCompleted, EventsCompleted = EventsCompleted };
            foreach (var s in _states.Values)
                o.States.Add(new OrderStateSave { Id = s.Id, Status = s.Status, Progress = Num.Sanitize(s.Progress), Baseline = Num.Sanitize(s.Baseline), Remaining = Num.Sanitize(s.Remaining) });
            return o;
        }

        public void Restore(OrdersSave o)
        {
            _states.Clear();
            if (o == null) return;
            foreach (var s in o.States)
            {
                if (s == null || !_defs.ContainsKey(s.Id ?? "")) continue;
                _states[s.Id] = new OrderStateSave
                {
                    Id = s.Id, Status = s.Status, Progress = Math.Max(0, Num.Sanitize(s.Progress)),
                    Baseline = Math.Max(0, Num.Sanitize(s.Baseline)), Remaining = Math.Max(0, Num.Sanitize(s.Remaining))
                };
            }
            TutorialCompleted = o.TutorialCompleted;
            EventsCompleted = Math.Max(0, o.EventsCompleted);
        }
    }
}
