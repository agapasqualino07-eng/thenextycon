using System;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class RiskRewardTests
    {
        // Prepares a session in phase 1 with production and an experimental offer ready.
        static Rig OfferReady(IRandom rng, double money = 1e7)
        {
            var r = Rig.New(rng);
            r.GiveProduction(20);
            r.ReachPhase(1);
            r.Money(money);
            r.S.Experimental.Advance(10000);
            Assert.AreEqual(EventStage.Offered, r.S.Experimental.Stage);
            return r;
        }

        // ---------------------------------------------------------------- probabilities
        [Test]
        public void Config_AllProbabilityTablesSumTo100()
        {
            var c = DefaultConfig.Create();
            Assert.AreEqual(100.0, c.Experimental.Outcomes.Sum(o => o.ProbabilityPercent), 1e-9);
            Assert.AreEqual(100.0, c.Surge.Outcomes.Sum(o => o.ProbabilityPercent), 1e-9);
            Assert.AreEqual(100.0, c.Crate.Entries.Sum(o => o.ProbabilityPercent), 1e-9);
            Assert.IsEmpty(ConfigValidator.Validate(c));
        }

        [Test]
        public void Validator_RejectsBadProbabilityTables()
        {
            var c = DefaultConfig.Create();
            c.Experimental.Outcomes[0].ProbabilityPercent = 31;       // sums to 101
            c.Crate.Entries[0].ProbabilityPercent = -5;
            var e = ConfigValidator.Validate(c);
            Assert.IsTrue(e.Any(x => x.Contains("Experimental.Outcomes")));
            Assert.IsTrue(e.Any(x => x.Contains("Crate.Entries")));
        }

        [Test]
        public void Probability_PickIsDeterministicAndRespectsBoundaries()
        {
            var p = new double[] { 30, 38, 24, 8 };
            Assert.AreEqual(0, Probability.Pick(new FixedRandom(0.0), p));
            Assert.AreEqual(0, Probability.Pick(new FixedRandom(0.2999), p));
            Assert.AreEqual(1, Probability.Pick(new FixedRandom(0.30), p));
            Assert.AreEqual(2, Probability.Pick(new FixedRandom(0.68), p));
            Assert.AreEqual(3, Probability.Pick(new FixedRandom(0.92), p));
            Assert.AreEqual(3, Probability.Pick(new FixedRandom(0.99999999), p));
        }

        [Test]
        public void Probability_EmpiricalFrequenciesMatchDisplayedPercentages()
        {
            var cfg = DefaultConfig.Create();
            var pct = cfg.Experimental.Outcomes.Select(o => o.ProbabilityPercent).ToList();
            var rng = new XorShiftRandom(2024);
            var counts = new int[pct.Count];
            int n = 200000;
            for (int i = 0; i < n; i++) counts[Probability.Pick(rng, pct)]++;
            for (int i = 0; i < pct.Count; i++)
                Assert.AreEqual(pct[i], counts[i] * 100.0 / n, 0.5, "outcome " + i);
        }

        [Test]
        public void ExpectedValue_IsComputedFromTheSameConfigThatDrivesRolls()
        {
            var r = Rig.New();
            Assert.AreEqual(1.349, r.S.Experimental.ExperimentalExpectedMultiple, 1e-9);
            Assert.AreEqual(1.2, r.S.Experimental.PrudentExpectedMultiple, 1e-9);
            Assert.Greater(r.S.Experimental.ExperimentalExpectedMultiple, r.S.Experimental.PrudentExpectedMultiple);
            Assert.AreEqual(90, r.S.Surge.GuaranteedValue, 1e-9);
            Assert.AreEqual(103.5, r.S.Surge.ExperimentExpectedValue, 1e-9);
        }

        [Test]
        public void RiskMechanics_EVsAreModestlyAboveSafeOptions_NotExtreme()
        {
            var r = Rig.New();
            Assert.Less(r.S.Experimental.ExperimentalExpectedMultiple, 1.6);
            Assert.Less(r.S.Surge.ExperimentExpectedValue, r.S.Surge.GuaranteedValue * 1.5);
        }

        // ---------------------------------------------------------------- experimental machine
        [Test]
        public void Experimental_NotOfferedBeforeUnlockPhase()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            r.S.Experimental.Advance(1e6);
            Assert.AreEqual(EventStage.Cooldown, r.S.Experimental.Stage);
        }

        [Test]
        public void Experimental_PreviewShowsCostsOutcomesAndProbabilitiesBeforeConfirmation()
        {
            var r = OfferReady(new FixedRandom(0.5));
            var p = r.S.Experimental.GetPreview();
            Assert.Greater(p.CostPrudent, 0);
            Assert.Greater(p.CostExperimental, 0);
            Assert.AreEqual(4, p.Outcomes.Count);
            Assert.AreEqual(100.0, p.Outcomes.Sum(o => o.Percent), 1e-9);
            Assert.Greater(p.PrudentDuration, 0);
            Assert.Greater(p.ExperimentalDuration, 0);
            Assert.IsTrue(p.Outcomes.All(o => o.Reward >= 0));
            Assert.AreEqual(p.CostPrudent * 1.2, p.PrudentReward, 1e-6);
        }

        [Test]
        public void Experimental_CostIsChargedExactlyOnce()
        {
            var r = OfferReady(new FixedRandom(0.5));
            var pv = r.S.Experimental.GetPreview();
            double before = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental));
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental), "double tap");
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.Prudent, pv.CostPrudent), "other option after accepting");
            Assert.AreEqual(before - pv.CostExperimental, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Experimental_InsufficientFunds_RejectedWithoutSideEffects()
        {
            var r = OfferReady(new FixedRandom(0.5), money: 0);
            r.S.Wallet.Restore(0, r.S.Wallet.TotalEarned, 0);
            var pv = r.S.Experimental.GetPreview();
            Assert.IsFalse(pv.CanAffordExperimental);
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental));
            Assert.AreEqual(EventStage.Offered, r.S.Experimental.Stage);
            Assert.AreEqual(0, r.S.Wallet.Balance);
        }

        [Test]
        public void Experimental_UiValueIsNeverTrusted()
        {
            var r = OfferReady(new FixedRandom(0.5));
            var pv = r.S.Experimental.GetPreview();
            double bal = r.S.Wallet.Balance;
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental / 2), "stale or forged cost");
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.None, 0));
            Assert.IsFalse(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, double.NaN));
            Assert.AreEqual(bal, r.S.Wallet.Balance);
            Assert.AreEqual(EventStage.Offered, r.S.Experimental.Stage);
        }

        [Test]
        public void Experimental_DeclineIsFreeAndHasNoPenalty()
        {
            var r = OfferReady(new FixedRandom(0.5));
            double bal = r.S.Wallet.Balance;
            double ops = r.S.Production.BaseOutputPerSecond;
            Assert.IsTrue(r.S.Experimental.TryDecline());
            Assert.AreEqual(bal, r.S.Wallet.Balance);
            Assert.AreEqual(ops, r.S.Production.BaseOutputPerSecond);
            Assert.AreEqual(EventStage.Cooldown, r.S.Experimental.Stage);
            Assert.IsFalse(r.S.Experimental.TryDecline());
            r.S.Experimental.Advance(r.Config.Experimental.DeclineCooldownSeconds + 1);
            Assert.AreEqual(EventStage.Offered, r.S.Experimental.Stage, "a new offer returns later");
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RiskEventDeclined));
        }

        [TestCase(0.0, "fail", 0.30)]
        [TestCase(0.5, "std", 1.25)]
        [TestCase(0.7, "good", 2.00)]
        [TestCase(0.95, "great", 3.80)]
        public void Experimental_DeterministicOutcomeAndPayout(double roll, string expectedId, double mult)
        {
            var r = OfferReady(new FixedRandom(roll));
            var pv = r.S.Experimental.GetPreview();
            string outcome = null; double paid = 0;
            r.S.Experimental.Completed += (c, id, payout) => { outcome = id; paid = payout; };
            double before = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental));
            Assert.AreEqual(EventStage.InProgress, r.S.Experimental.Stage);
            Assert.IsFalse(r.S.Experimental.TryCollect(), "result not ready yet");
            r.S.Experimental.Advance(r.Config.Experimental.ExperimentalDurationSeconds + 1);
            Assert.AreEqual(EventStage.ReadyToCollect, r.S.Experimental.Stage);
            Assert.IsTrue(r.S.Experimental.TryCollect());
            Assert.AreEqual(expectedId, outcome);
            Assert.AreEqual(pv.CostExperimental * mult, paid, 1e-6);
            Assert.AreEqual(before - pv.CostExperimental + paid, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Experimental_ResultIsAssignedOnlyOnce()
        {
            var r = OfferReady(new FixedRandom(0.5));
            var pv = r.S.Experimental.GetPreview();
            r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental);
            r.S.Experimental.Advance(1000);
            Assert.IsTrue(r.S.Experimental.TryCollect());
            double bal = r.S.Wallet.Balance;
            Assert.IsFalse(r.S.Experimental.TryCollect());
            Assert.AreEqual(bal, r.S.Wallet.Balance);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RiskEventCompleted));
        }

        [Test]
        public void Experimental_PrudentChoiceIsCertain()
        {
            foreach (double roll in new[] { 0.0, 0.5, 0.99 })
            {
                var r = OfferReady(new FixedRandom(roll));
                var pv = r.S.Experimental.GetPreview();
                double before = r.S.Wallet.Balance;
                r.S.Experimental.TryAccept(ExperimentChoice.Prudent, pv.CostPrudent);
                r.S.Experimental.Advance(100);
                r.S.Experimental.TryCollect();
                Assert.AreEqual(before + pv.CostPrudent * 0.2, r.S.Wallet.Balance, 1e-6);
            }
        }

        [Test]
        public void Experimental_WorstCaseLossIsBoundedAndPayoutIsCapped()
        {
            var r = OfferReady(new FixedRandom(0.0));
            var pv = r.S.Experimental.GetPreview();
            double before = r.S.Wallet.Balance;
            r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental);
            r.S.Experimental.Advance(1000);
            r.S.Experimental.TryCollect();
            double loss = before - r.S.Wallet.Balance;
            Assert.AreEqual(pv.CostExperimental * 0.7, loss, 1e-6, "loss limited to 70% of the displayed cost");
            double maxPayout = r.Config.Experimental.MaxRewardOpsSeconds * r.S.Production.BaseOutputPerSecond;
            foreach (var o in pv.Outcomes) Assert.LessOrEqual(o.Reward, Math.Max(maxPayout, r.Config.Experimental.MinCost * 5));
        }

        [Test]
        public void Experimental_BadOutcomeNeverBlocksProgressOrDemandsAds()
        {
            var r = OfferReady(new FixedRandom(0.0), money: 0);
            var pv = r.S.Experimental.GetPreview();
            r.S.Wallet.Restore(pv.CostExperimental, r.S.Wallet.TotalEarned, 0);   // exactly the cost: the worst case
            r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental);
            r.S.Experimental.Advance(1000);
            r.S.Experimental.TryCollect();
            // Normal progression is still fully available, no ad is requested.
            Assert.AreEqual(0, r.Ads.ShowCount);
            double ops = r.S.Production.BaseOutputPerSecond;
            r.Run(60);
            Assert.Greater(r.S.Wallet.Balance, 0);
            Assert.AreEqual(ops, r.S.Production.BaseOutputPerSecond);
            r.Money(1e6);
            Assert.IsTrue(r.S.Production.TryBuy("drill"));
            Assert.AreEqual(0, r.Analytics.Count(AnalyticsEvents.RewardedAdStarted));
        }

        [Test]
        public void Experimental_RestartDuringEvent_KeepsCostPaidOnce_AndSameOutcome()
        {
            var r = OfferReady(new FixedRandom(0.95));
            var pv = r.S.Experimental.GetPreview();
            r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental);
            double balAfterPay = r.S.Wallet.Balance;
            var r2 = r.Restart(new FixedRandom(0.0));      // a different RNG: outcome must come from the SAVED roll
            Assert.AreEqual(EventStage.InProgress, r2.S.Experimental.Stage);
            Assert.AreEqual(balAfterPay, r2.S.Wallet.Balance, 1e-6, "no second charge on restart");
            r2.S.Experimental.Advance(1000);
            string outcome = null;
            r2.S.Experimental.Completed += (c, id, p) => outcome = id;
            Assert.IsTrue(r2.S.Experimental.TryCollect());
            Assert.AreEqual("great", outcome);
            var r3 = r2.Restart();
            Assert.AreEqual(EventStage.Cooldown, r3.S.Experimental.Stage);
            Assert.IsFalse(r3.S.Experimental.TryCollect(), "cannot collect again after restart");
        }

        [Test]
        public void Experimental_RestartWhileResultWaiting_StillCollectableOnce()
        {
            var r = OfferReady(new FixedRandom(0.5));
            var pv = r.S.Experimental.GetPreview();
            r.S.Experimental.TryAccept(ExperimentChoice.Prudent, pv.CostPrudent);
            r.S.Experimental.Advance(1000);
            var r2 = r.Restart();
            Assert.AreEqual(EventStage.ReadyToCollect, r2.S.Experimental.Stage);
            Assert.IsTrue(r2.S.Experimental.TryCollect());
            Assert.IsFalse(r2.S.Experimental.TryCollect());
        }

        [Test]
        public void Experimental_RestoreFromInvalidState_FallsBackToCooldown()
        {
            var r = Rig.New(start: false);
            r.S.Experimental.Restore(new ExperimentSave { Stage = EventStage.InProgress, Choice = ExperimentChoice.Experimental, OutcomeIndex = 99, Timer = 5 });
            Assert.AreEqual(EventStage.Cooldown, r.S.Experimental.Stage);
            r.S.Experimental.Restore(new ExperimentSave { Stage = EventStage.Offered });
            Assert.AreEqual(EventStage.Cooldown, r.S.Experimental.Stage);
        }

        [Test]
        public void Experimental_CompletingCountsForCompleteEventOrders()
        {
            var r = OfferReady(new FixedRandom(0.5));
            r.S.Orders.Update(0);
            var pv = r.S.Experimental.GetPreview();
            r.S.Experimental.TryAccept(ExperimentChoice.Prudent, pv.CostPrudent);
            r.S.Experimental.Advance(100);
            r.S.Experimental.TryCollect();
            Assert.AreEqual(1, r.S.Orders.EventsCompleted);
        }

        // ---------------------------------------------------------------- production surge
        static Rig SurgeReady(IRandom rng)
        {
            var r = Rig.New(rng);
            r.GiveProduction(20);
            r.ReachPhase(1);
            r.S.Surge.Advance(10000);
            Assert.AreEqual(EventStage.Offered, r.S.Surge.Stage);
            return r;
        }

        [Test]
        public void Surge_GuaranteedBonusAppliesDeclaredBoost()
        {
            var r = SurgeReady(new FixedRandom(0.99));
            double ops = r.S.Production.BaseOutputPerSecond;
            Assert.IsTrue(r.S.Surge.TryChoose(SurgeChoice.Guaranteed));
            Assert.AreEqual(ops * 2, r.S.Production.OutputPerSecond, 1e-9);
            Assert.AreEqual(90, r.S.Production.BoostRemaining("surge"), 1e-9);
        }

        [TestCase(0.0, 1.5, 60)]
        [TestCase(0.5, 2.0, 90)]
        [TestCase(0.8, 3.0, 90)]
        [TestCase(0.99, 5.0, 120)]
        public void Surge_ExperimentOutcomesAreDeterministic(double roll, double mult, double secs)
        {
            var r = SurgeReady(new FixedRandom(roll));
            Assert.IsTrue(r.S.Surge.TryChoose(SurgeChoice.Experiment));
            Assert.AreEqual(secs, r.S.Production.BoostRemaining("surge"), 1e-9);
            Assert.AreEqual(Math.Min(mult, r.Config.Economy.MaxTempMultiplier), r.S.Production.TempMultiplier, 1e-9);
        }

        [Test]
        public void Surge_ChoosingTwiceAppliesOnce_AndStartsCooldown()
        {
            var r = SurgeReady(new FixedRandom(0.5));
            Assert.IsTrue(r.S.Surge.TryChoose(SurgeChoice.Guaranteed));
            Assert.IsFalse(r.S.Surge.TryChoose(SurgeChoice.Guaranteed));
            Assert.IsFalse(r.S.Surge.TryChoose(SurgeChoice.Experiment));
            Assert.AreEqual(EventStage.Cooldown, r.S.Surge.Stage);
            Assert.AreEqual(2.0, r.S.Production.TempMultiplier, 1e-9);
        }

        [Test]
        public void Surge_IgnoredOfferExpiresWithoutPenalty_AndHonoursCooldown()
        {
            var r = SurgeReady(new FixedRandom(0.5));
            double bal = r.S.Wallet.Balance;
            r.S.Surge.Advance(r.Config.Surge.OfferWindowSeconds + 1);
            Assert.AreEqual(EventStage.Cooldown, r.S.Surge.Stage);
            Assert.AreEqual(bal, r.S.Wallet.Balance);
            Assert.IsFalse(r.S.Surge.TryChoose(SurgeChoice.Guaranteed));
            r.S.Surge.Advance(r.Config.Surge.CooldownSeconds - 1);
            Assert.AreEqual(EventStage.Cooldown, r.S.Surge.Stage, "cooldown prevents frequent offers");
            r.S.Surge.Advance(2);
            Assert.AreEqual(EventStage.Offered, r.S.Surge.Stage);
        }

        [Test]
        public void Surge_DismissIsFree()
        {
            var r = SurgeReady(new FixedRandom(0.5));
            Assert.IsTrue(r.S.Surge.TryDismiss());
            Assert.AreEqual(1.0, r.S.Production.TempMultiplier);
            Assert.IsFalse(r.S.Surge.TryDismiss());
        }

        [Test]
        public void Surge_RestartKeepsBoostAndOfferState()
        {
            var r = SurgeReady(new FixedRandom(0.5));
            r.S.Surge.TryChoose(SurgeChoice.Guaranteed);
            r.Run(30);
            var r2 = r.Restart();
            Assert.AreEqual(60, r2.S.Production.BoostRemaining("surge"), 1.5);
            Assert.AreEqual(EventStage.Cooldown, r2.S.Surge.Stage);
            Assert.IsFalse(r2.S.Surge.TryChoose(SurgeChoice.Guaranteed));
        }

        [Test]
        public void Surge_BoostNeverExceedsGlobalCap_EvenWithOtherBoosts()
        {
            var r = SurgeReady(new FixedRandom(0.99));
            r.S.Production.AddBoost("ad", 2, 300);
            r.S.Production.AddBoost("crate", 2, 300);
            r.S.Surge.TryChoose(SurgeChoice.Experiment);       // x5
            Assert.AreEqual(r.Config.Economy.MaxTempMultiplier, r.S.Production.TempMultiplier);
        }

        [Test]
        public void Surge_NormalProductionStaysRelevant()
        {
            var r = SurgeReady(new FixedRandom(0.99));
            r.S.Surge.TryChoose(SurgeChoice.Experiment);
            // Even at the best outcome the boost is a bounded multiplier of production, never a flat jackpot:
            Assert.AreEqual(Math.Min(5.0, r.Config.Economy.MaxTempMultiplier) * r.S.Production.BaseOutputPerSecond, r.S.Production.OutputPerSecond, 1e-9);
            var r0 = Rig.New();
            Assert.AreEqual(0, r0.S.Production.OutputPerSecond, "without production a boost yields nothing");
        }

        // ---------------------------------------------------------------- free parts crate
        [Test]
        public void Crate_CannotBeObtainedWithoutGameplay_AndOpenWithNoneFails()
        {
            var r = Rig.New();
            Assert.AreEqual(0, r.S.Crates.Available);
            Assert.IsNull(r.S.Crates.TryOpen());
        }

        [Test]
        public void Crate_HasNoPurchaseApi()
        {
            var methods = typeof(FreePartsCrate).GetMethods().Select(m => m.Name.ToLowerInvariant());
            Assert.IsFalse(methods.Any(n => n.Contains("buy") || n.Contains("purchase") || n.Contains("pay")));
        }

        [Test]
        public void Crate_EarnedFromOrdersAndPhaseMilestones()
        {
            var r = Rig.New();
            r.Money(1e9);
            r.ReachPhase(1);
            Assert.AreEqual(1, r.S.Crates.Available, "entering phase 1 grants one crate");
            r.ReachPhase(1);
            r.S.Progression.Update();
            Assert.AreEqual(1, r.S.Crates.Available, "granted only once");
        }

        [Test]
        public void Crate_PhaseRewardNotRepeatedAfterRestore()
        {
            var r = Rig.New();
            r.ReachPhase(1);
            var r2 = r.Restart();
            r2.S.Progression.Update();
            Assert.AreEqual(1, r2.S.Crates.Available);
            r2.S.Crates.TryOpen();
            var r3 = r2.Restart();
            r3.S.Progression.Update();
            Assert.AreEqual(0, r3.S.Crates.Available, "opened crate must not come back");
        }

        [Test]
        public void Crate_RowsExposeProbabilitiesBeforeOpening()
        {
            var r = Rig.New();
            var rows = r.S.Crates.Rows();
            Assert.AreEqual(5, rows.Count);
            Assert.AreEqual(100.0, rows.Sum(x => x.Percent), 1e-9);
        }

        [TestCase(0.0, "cur_s")]
        [TestCase(0.5, "cur_l")]
        [TestCase(0.7, "boost")]
        [TestCase(0.9, "parts")]
        [TestCase(0.999, "alloy")]
        public void Crate_DeterministicRewards(double roll, string expectedId)
        {
            var r = Rig.New(new FixedRandom(roll));
            r.GiveProduction(20);
            r.S.Crates.Grant(1, "test");
            double bal = r.S.Wallet.Balance; int parts = r.S.Production.Parts;
            var res = r.S.Crates.TryOpen();
            Assert.AreEqual(expectedId, res.EntryId);
            switch (res.Kind)
            {
                case CrateRewardKind.Currency: Assert.AreEqual(bal + res.Currency, r.S.Wallet.Balance, 1e-6); Assert.Greater(res.Currency, 0); break;
                case CrateRewardKind.Parts: Assert.AreEqual(parts + res.Parts, r.S.Production.Parts); break;
                case CrateRewardKind.TempBoost: Assert.Greater(r.S.Production.BoostRemaining("crate"), 0); break;
            }
        }

        [Test]
        public void Crate_OpenConsumesExactlyOneAndNeverTwice()
        {
            var r = Rig.New(new FixedRandom(0.5));
            r.GiveProduction(10);
            r.S.Crates.Grant(1, "test");
            Assert.IsNotNull(r.S.Crates.TryOpen());
            Assert.IsNull(r.S.Crates.TryOpen());
            Assert.AreEqual(1, r.S.Crates.TotalOpened);
        }

        [Test]
        public void Crate_RestartDoesNotDuplicateOrReRoll()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            r.S.Crates.Grant(2, "test");
            r.S.Crates.TryOpen();
            var r2 = r.Restart();
            Assert.AreEqual(1, r2.S.Crates.Available);
            Assert.AreEqual(1, r2.S.Crates.TotalOpened);
        }

        [Test]
        public void Crate_StorageIsLimited_OverflowBecomesCurrency()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            double bal = r.S.Wallet.Balance;
            r.S.Crates.Grant(r.Config.Crate.MaxStored + 3, "test");
            Assert.AreEqual(r.Config.Crate.MaxStored, r.S.Crates.Available);
            Assert.Greater(r.S.Wallet.Balance, bal, "overflow compensated with currency, never silently lost");
        }

        [Test]
        public void Crate_CurrencyRewardIsCappedAgainstInflation()
        {
            var cfg = DefaultConfig.Create();
            cfg.Crate.Entries.Clear();
            cfg.Crate.Entries.Add(new CrateEntryDef { Id = "huge", Label = "huge", Kind = CrateRewardKind.Currency, ProbabilityPercent = 100, OpsSeconds = 1e9 });
            var r = Rig.New(new FixedRandom(0.1), cfg);
            r.GiveProduction(20);
            r.S.Crates.Grant(1, "t");
            double ops = r.S.Production.BaseOutputPerSecond;
            var res = r.S.Crates.TryOpen();
            Assert.AreEqual(cfg.Crate.MaxCurrencyOpsSeconds * ops, res.Currency, 1e-6);
        }

        [Test]
        public void Crate_RewardsHaveNoExternalValue()
        {
            // Every reward kind is an in-game quantity (currency, boost, parts); nothing else exists.
            var kinds = Enum.GetValues(typeof(CrateRewardKind)).Cast<CrateRewardKind>().ToList();
            CollectionAssert.AreEquivalent(new[] { CrateRewardKind.Currency, CrateRewardKind.TempBoost, CrateRewardKind.Parts }, kinds);
        }

        [Test]
        public void Crate_ProgressionDoesNotDependOnCrates()
        {
            var r = Rig.New();
            r.Money(1e9);
            Assert.AreEqual(0, r.S.Crates.Available);
            Assert.IsTrue(r.S.Production.TryBuy("drill", 5));
            Assert.IsTrue(r.S.Production.TryBuy("lathe", 5));
            Assert.Greater(r.S.Production.BaseOutputPerSecond, 0);
        }

        [Test]
        public void Analytics_CrateOpenedEventEmitted()
        {
            var r = Rig.New(new FixedRandom(0.1));
            r.GiveProduction(10);
            r.S.Crates.Grant(1, "t");
            r.S.Crates.TryOpen();
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.FreeCrateOpened));
        }
    }
}
