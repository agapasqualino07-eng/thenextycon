using System;

namespace OfficinaTycoon
{
    /// <summary>Tracks the current progression phase (monotonic) from lifetime earnings.</summary>
    public sealed class ProgressionSystem
    {
        readonly GameConfig _cfg;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;
        readonly GameSignals _signals;

        /// <summary>(crateCount, sourceKey) — wired to CrateSystem by GameSession.</summary>
        public Action<int, string> GrantCrates = (n, s) => { };

        public int PhaseIndex { get; private set; }
        public PhaseDef Phase => _cfg.Phases[Math.Min(PhaseIndex, _cfg.Phases.Count - 1)];
        public bool IsLastPhase => PhaseIndex >= _cfg.Phases.Count - 1;

        public ProgressionSystem(GameConfig cfg, Wallet wallet, ProductionSystem production, ClaimLedger ledger, GameSignals signals)
        {
            _cfg = cfg; _wallet = wallet; _production = production; _ledger = ledger; _signals = signals;
        }

        public void Restore(int phaseIndex)
        {
            PhaseIndex = Math.Max(0, Math.Min(_cfg.Phases.Count - 1, phaseIndex));
            _production.MarkDirty();
            Update(); // catches up if thresholds were retuned downwards
        }

        /// <summary>Progress to the next phase in [0,1] (1 when final).</summary>
        public double NextPhaseProgress
        {
            get
            {
                if (IsLastPhase) return 1;
                double from = Phase.TotalEarnedRequired;
                double to = _cfg.Phases[PhaseIndex + 1].TotalEarnedRequired;
                return Num.Clamp((_wallet.TotalEarned - from) / (to - from), 0, 1);
            }
        }

        public void Update()
        {
            while (PhaseIndex < _cfg.Phases.Count - 1 && _wallet.TotalEarned >= _cfg.Phases[PhaseIndex + 1].TotalEarnedRequired)
            {
                PhaseIndex++;
                _production.MarkDirty();
                var def = _cfg.Phases[PhaseIndex];
                // Phase reward is paid at most once per phase, even if the save is restored and re-crossed.
                if (def.CratesOnEnter > 0 && _ledger.TryClaim("phase:" + def.Id)) GrantCrates(def.CratesOnEnter, "phase:" + def.Id);
                _signals.RaisePhase(PhaseIndex);
            }
        }
    }
}
