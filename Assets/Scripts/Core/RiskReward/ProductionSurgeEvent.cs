using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    public enum SurgeChoice { None, Guaranteed, Experiment }

    /// <summary>
    /// "Produzione fuori scala": a short-lived offer (OfferWindow) to boost production. The player picks a
    /// guaranteed boost or an experiment with declared outcomes. Both are free. If ignored, the offer simply
    /// expires (no penalty). Cooldown prevents frequent offers; boosts are capped by Economy.MaxTempMultiplier.
    /// The active boost lives in ProductionSystem and is persisted there (restart-safe).
    /// </summary>
    public sealed class ProductionSurgeEvent
    {
        public struct OutcomeRow { public string Id, Label; public double Percent, Multiplier, Seconds; }

        public sealed class Preview
        {
            public double GuaranteedMultiplier, GuaranteedSeconds, GuaranteedValue;
            public double ExperimentExpectedValue;
            public List<OutcomeRow> Outcomes = new List<OutcomeRow>();
        }

        readonly SurgeConfig _c;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;
        readonly IRandom _rng;
        readonly GameSignals _signals;
        readonly List<double> _percents;
        Func<int> _phase = () => 99;

        public EventStage Stage { get; private set; } = EventStage.Cooldown; // Cooldown | Offered
        public double Timer { get; private set; } = -1;
        public int Sequence { get; private set; }

        public event Action Offered;
        public event Action Expired;
        public event Action<SurgeChoice, string, double, double> Resolved; // choice, outcomeId, multiplier, seconds

        public ProductionSurgeEvent(GameConfig cfg, ProductionSystem production, ClaimLedger ledger, IRandom rng, GameSignals signals)
        {
            _c = cfg.Surge; _production = production; _ledger = ledger; _rng = rng; _signals = signals;
            _percents = _c.Outcomes.Select(o => o.ProbabilityPercent).ToList();
        }

        public void SetPhaseProvider(Func<int> phase) { _phase = phase; }

        /// <summary>Boost "value" = (multiplier - 1) * seconds, i.e. extra production-seconds. Used to compare options.</summary>
        public static double BoostValue(double multiplier, double seconds) => (multiplier - 1.0) * seconds;

        public double ExperimentExpectedValue
            => Probability.ExpectedValue(_percents, _c.Outcomes.Select(o => BoostValue(o.BoostMultiplier, o.BoostSeconds)).ToList());
        public double GuaranteedValue => BoostValue(_c.GuaranteedMultiplier, _c.GuaranteedSeconds);

        public Preview GetPreview()
        {
            var p = new Preview
            {
                GuaranteedMultiplier = _c.GuaranteedMultiplier, GuaranteedSeconds = _c.GuaranteedSeconds,
                GuaranteedValue = GuaranteedValue, ExperimentExpectedValue = ExperimentExpectedValue
            };
            foreach (var o in _c.Outcomes)
                p.Outcomes.Add(new OutcomeRow { Id = o.Id, Label = o.Label, Percent = o.ProbabilityPercent, Multiplier = o.BoostMultiplier, Seconds = o.BoostSeconds });
            return p;
        }

        public double TimerRemaining => Timer < 0 ? _c.FirstOfferDelaySeconds : Math.Max(0, Timer);

        public void Advance(double dt)
        {
            if (!(dt > 0)) return;
            if (Stage == EventStage.Cooldown)
            {
                if (_phase() < _c.UnlockPhase) return;
                if (Timer < 0) Timer = _c.FirstOfferDelaySeconds;
                Timer -= dt;
                if (Timer <= 0)
                {
                    Sequence++;
                    Stage = EventStage.Offered;
                    Timer = _c.OfferWindowSeconds;
                    Offered?.Invoke();
                }
            }
            else if (Stage == EventStage.Offered)
            {
                Timer -= dt;
                if (Timer <= 0)
                {
                    Stage = EventStage.Cooldown;
                    Timer = _c.CooldownSeconds;
                    Expired?.Invoke();
                }
            }
        }

        /// <summary>Ignoring is free: the offer is dismissed and the cooldown starts.</summary>
        public bool TryDismiss()
        {
            if (Stage != EventStage.Offered) return false;
            Stage = EventStage.Cooldown;
            Timer = _c.CooldownSeconds;
            return true;
        }

        public bool TryChoose(SurgeChoice choice)
        {
            if (Stage != EventStage.Offered) return false;
            if (choice != SurgeChoice.Guaranteed && choice != SurgeChoice.Experiment) return false;
            if (!_ledger.TryClaim("surge:" + Sequence)) return false;
            // State change first: a duplicate tap/callback can no longer pass the Stage check.
            Stage = EventStage.Cooldown;
            Timer = _c.CooldownSeconds;

            string id = "guaranteed";
            double mult = _c.GuaranteedMultiplier, secs = _c.GuaranteedSeconds;
            if (choice == SurgeChoice.Experiment)
            {
                int idx = Probability.Pick(_rng, _percents);
                var o = _c.Outcomes[idx];
                id = o.Id; mult = o.BoostMultiplier; secs = o.BoostSeconds;
            }
            _production.AddBoost("surge", mult, secs);
            Resolved?.Invoke(choice, id, mult, secs);
            _signals.RaiseRiskEventCompleted("surge");
            return true;
        }

        public SurgeSave Capture() => new SurgeSave { Stage = Stage, Timer = Timer, Sequence = Sequence };

        public void Restore(SurgeSave s)
        {
            Stage = EventStage.Cooldown; Timer = -1; Sequence = 0;
            if (s == null) return;
            Sequence = Math.Max(0, s.Sequence);
            Timer = Num.Sanitize(s.Timer, -1);
            if (s.Stage == EventStage.Offered && Timer > 0) Stage = EventStage.Offered;
            else if (s.Stage == EventStage.Offered) Timer = _c.CooldownSeconds;
        }
    }
}
