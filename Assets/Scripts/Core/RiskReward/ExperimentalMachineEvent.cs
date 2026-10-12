using System;
using System.Collections.Generic;
using System.Linq;

namespace OfficinaTycoon
{
    /// <summary>
    /// "Macchinario sperimentale": the player may invest already-earned (free) currency in a prototype.
    ///  - Prudent test: known cost, one known result (moderate return).
    ///  - Experimental test: known cost, several results with displayed probabilities (higher EV, bounded loss).
    /// Declining is free. A bad outcome never blocks progression or asks for an ad.
    /// The result is rolled when the cost is paid and persisted, so restart cannot re-roll or double-pay.
    /// </summary>
    public sealed class ExperimentalMachineEvent
    {
        public struct OutcomeRow
        {
            public string Id, Label;
            public double Percent, Reward, BoostMultiplier, BoostSeconds;
        }

        public sealed class Preview
        {
            public EventStage Stage;
            public double CostPrudent, CostExperimental;
            public double PrudentReward, PrudentDuration, ExperimentalDuration;
            public double PrudentExpectedMultiple, ExperimentalExpectedMultiple;
            public List<OutcomeRow> Outcomes = new List<OutcomeRow>();
            public bool CanAffordPrudent, CanAffordExperimental;
        }

        readonly GameConfig _cfg;
        readonly ExperimentalConfig _c;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;
        readonly IRandom _rng;
        readonly GameSignals _signals;
        readonly List<double> _percents;
        Func<int> _phase = () => 99;

        public EventStage Stage { get; private set; } = EventStage.Cooldown;
        public double Timer { get; private set; } = -1;
        public int Sequence { get; private set; }
        public ExperimentChoice Choice { get; private set; }
        public int OutcomeIndex { get; private set; } = -1;
        double _offerOps, _costP, _costE, _paid;

        public event Action Offered;
        public event Action<ExperimentChoice, double> Accepted;
        public event Action Declined;
        public event Action<ExperimentChoice, string, double> Completed; // choice, outcomeId, payout

        public ExperimentalMachineEvent(GameConfig cfg, Wallet wallet, ProductionSystem production, ClaimLedger ledger, IRandom rng, GameSignals signals)
        {
            _cfg = cfg; _c = cfg.Experimental; _wallet = wallet; _production = production; _ledger = ledger; _rng = rng; _signals = signals;
            _percents = _c.Outcomes.Select(o => o.ProbabilityPercent).ToList();
        }

        public void SetPhaseProvider(Func<int> phase) { _phase = phase; }

        public double TimerRemaining => Timer < 0 ? _c.FirstOfferDelaySeconds : Math.Max(0, Timer);

        // ------------------------------------------------------------ pricing
        double Price(double opsSeconds, double ops) => Math.Max(_c.MinCost, Math.Round(opsSeconds * ops));
        double MaxMultiple => Math.Max(_c.PrudentReturnMultiplier, _c.Outcomes.Count == 0 ? 0 : _c.Outcomes.Max(o => o.ReturnMultiplier));
        double MaxPayout(double ops) => Math.Max(_c.MaxRewardOpsSeconds * ops, _c.MinCost * MaxMultiple);
        double Payout(double cost, double multiple, double ops) => Math.Min(cost * multiple, MaxPayout(ops));

        /// <summary>Expected return per unit of cost for the experimental test (shown in debug tools and tests).</summary>
        public double ExperimentalExpectedMultiple => Probability.ExpectedValue(_percents, _c.Outcomes.Select(o => o.ReturnMultiplier).ToList());
        public double PrudentExpectedMultiple => _c.PrudentReturnMultiplier;

        public Preview GetPreview()
        {
            var p = new Preview { Stage = Stage };
            double ops = Stage == EventStage.Offered ? _offerOps : _production.BaseOutputPerSecond;
            p.CostPrudent = Stage == EventStage.Offered ? _costP : Price(_c.PrudentCostOpsSeconds, ops);
            p.CostExperimental = Stage == EventStage.Offered ? _costE : Price(_c.ExperimentalCostOpsSeconds, ops);
            p.PrudentReward = Payout(p.CostPrudent, _c.PrudentReturnMultiplier, ops);
            p.PrudentDuration = _c.PrudentDurationSeconds;
            p.ExperimentalDuration = _c.ExperimentalDurationSeconds;
            p.PrudentExpectedMultiple = PrudentExpectedMultiple;
            p.ExperimentalExpectedMultiple = ExperimentalExpectedMultiple;
            foreach (var o in _c.Outcomes)
                p.Outcomes.Add(new OutcomeRow { Id = o.Id, Label = o.Label, Percent = o.ProbabilityPercent,
                    Reward = Payout(p.CostExperimental, o.ReturnMultiplier, ops), BoostMultiplier = o.BonusBoostMultiplier, BoostSeconds = o.BonusBoostSeconds });
            p.CanAffordPrudent = _wallet.CanAfford(p.CostPrudent);
            p.CanAffordExperimental = _wallet.CanAfford(p.CostExperimental);
            return p;
        }

        /// <summary>Payout the player will receive when ReadyToCollect (for UI).</summary>
        public double PendingPayout => Stage == EventStage.ReadyToCollect || Stage == EventStage.InProgress ? PayoutFor(Choice, OutcomeIndex, _paid) : 0;

        double PayoutFor(ExperimentChoice choice, int idx, double paid)
        {
            if (choice == ExperimentChoice.Prudent) return Payout(paid, _c.PrudentReturnMultiplier, _offerOps);
            if (choice == ExperimentChoice.Experimental && idx >= 0 && idx < _c.Outcomes.Count)
                return Payout(paid, _c.Outcomes[idx].ReturnMultiplier, _offerOps);
            return 0;
        }

        public string PendingOutcomeLabel
            => Choice == ExperimentChoice.Experimental && OutcomeIndex >= 0 && OutcomeIndex < _c.Outcomes.Count ? _c.Outcomes[OutcomeIndex].Label : "";

        // ------------------------------------------------------------ time
        /// <summary>Advances the state machine by dt seconds (works for large offline steps too).</summary>
        public void Advance(double dt)
        {
            if (!(dt > 0)) return;
            switch (Stage)
            {
                case EventStage.Cooldown:
                    if (_phase() < _c.UnlockPhase) return;
                    if (Timer < 0) Timer = _c.FirstOfferDelaySeconds;
                    Timer -= dt;
                    if (Timer <= 0)
                    {
                        _offerOps = _production.BaseOutputPerSecond;
                        _costP = Price(_c.PrudentCostOpsSeconds, _offerOps);
                        _costE = Price(_c.ExperimentalCostOpsSeconds, _offerOps);
                        Sequence++;
                        Stage = EventStage.Offered;
                        Timer = 0;
                        Offered?.Invoke();
                    }
                    break;
                case EventStage.InProgress:
                    Timer -= dt;
                    if (Timer <= 0) { Timer = 0; Stage = EventStage.ReadyToCollect; }
                    break;
            }
        }

        // ------------------------------------------------------------ actions
        /// <summary>Declining is always free and has no penalty other than the next offer being delayed.</summary>
        public bool TryDecline()
        {
            if (Stage != EventStage.Offered) return false;
            Stage = EventStage.Cooldown;
            Timer = _c.DeclineCooldownSeconds;
            Declined?.Invoke();
            return true;
        }

        /// <summary>
        /// Pays the cost ONCE and rolls the result. <paramref name="shownCost"/> is the cost the UI displayed;
        /// if it does not match the logic's cost the call is rejected (UI data is never trusted).
        /// </summary>
        public bool TryAccept(ExperimentChoice choice, double shownCost)
        {
            if (Stage != EventStage.Offered) return false;
            if (choice != ExperimentChoice.Prudent && choice != ExperimentChoice.Experimental) return false;
            double cost = choice == ExperimentChoice.Prudent ? _costP : _costE;
            if (!Num.IsFinite(shownCost) || Math.Abs(shownCost - cost) > 0.5) return false;
            if (!_wallet.TrySpend(cost, "experiment:" + Sequence + ":" + choice)) return false;

            _paid = cost;
            Choice = choice;
            OutcomeIndex = choice == ExperimentChoice.Experimental ? Probability.Pick(_rng, _percents) : -1;
            Stage = EventStage.InProgress;           // set synchronously with the payment: a second tap sees InProgress and is rejected
            Timer = choice == ExperimentChoice.Prudent ? _c.PrudentDurationSeconds : _c.ExperimentalDurationSeconds;
            Accepted?.Invoke(choice, cost);
            return true;
        }

        /// <summary>Collects the result exactly once.</summary>
        public bool TryCollect()
        {
            if (Stage != EventStage.ReadyToCollect) return false;
            string key = "exp:" + Sequence;
            double payout = PayoutFor(Choice, OutcomeIndex, _paid);
            string outcomeId = Choice == ExperimentChoice.Experimental && OutcomeIndex >= 0 ? _c.Outcomes[OutcomeIndex].Id : "prudent";
            var choice = Choice;
            int idx = OutcomeIndex;
            Stage = EventStage.Cooldown;
            Timer = _c.CooldownSeconds;
            Choice = ExperimentChoice.None; OutcomeIndex = -1; _paid = 0;
            if (!_ledger.TryClaim(key)) return false;   // already paid (restored duplicate): just reset
            if (payout > 0) _wallet.Add(payout, key);
            if (choice == ExperimentChoice.Experimental && idx >= 0)
            {
                var o = _c.Outcomes[idx];
                if (o.BonusBoostMultiplier > 1 && o.BonusBoostSeconds > 0) _production.AddBoost("experiment", o.BonusBoostMultiplier, o.BonusBoostSeconds);
            }
            Completed?.Invoke(choice, outcomeId, payout);
            _signals.RaiseRiskEventCompleted("experimental");
            return true;
        }

        // ------------------------------------------------------------ persistence
        public ExperimentSave Capture() => new ExperimentSave
        {
            Stage = Stage, Timer = Timer, Sequence = Sequence, Choice = Choice, OutcomeIndex = OutcomeIndex,
            OfferBaseOps = _offerOps, CostPrudent = _costP, CostExperimental = _costE, PaidCost = _paid
        };

        public void Restore(ExperimentSave s)
        {
            Stage = EventStage.Cooldown; Timer = -1; Sequence = 0; Choice = ExperimentChoice.None; OutcomeIndex = -1;
            _offerOps = _costP = _costE = _paid = 0;
            if (s == null) return;
            Sequence = Math.Max(0, s.Sequence);
            Timer = Num.Sanitize(s.Timer, -1);
            _offerOps = Math.Max(0, Num.Sanitize(s.OfferBaseOps));
            _costP = Math.Max(0, Num.Sanitize(s.CostPrudent));
            _costE = Math.Max(0, Num.Sanitize(s.CostExperimental));
            _paid = Math.Max(0, Num.Sanitize(s.PaidCost));
            switch (s.Stage)
            {
                case EventStage.Offered:
                    if (_costP > 0 && _costE > 0) Stage = EventStage.Offered; else { Stage = EventStage.Cooldown; Timer = _c.CooldownSeconds; }
                    break;
                case EventStage.InProgress:
                case EventStage.ReadyToCollect:
                    bool valid = (s.Choice == ExperimentChoice.Prudent) ||
                                 (s.Choice == ExperimentChoice.Experimental && s.OutcomeIndex >= 0 && s.OutcomeIndex < _c.Outcomes.Count);
                    if (valid)
                    {
                        Stage = s.Stage; Choice = s.Choice; OutcomeIndex = s.OutcomeIndex;
                        if (Stage == EventStage.InProgress && Timer <= 0) Stage = EventStage.ReadyToCollect;
                    }
                    else { Stage = EventStage.Cooldown; Timer = _c.CooldownSeconds; }
                    break;
                default:
                    Stage = EventStage.Cooldown; break;
            }
        }
    }
}
