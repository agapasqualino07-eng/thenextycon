using System;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class ProgressionAndConfigTests
    {
        // ---------------------------------------------------------------- orders
        [Test]
        public void Tutorial_FirstMinuteFlow_TapBuyProduce()
        {
            var r = Rig.New();
            Assert.AreEqual("t1", r.S.Orders.Views().Single().Def.Id, "only the first tutorial step is shown at the start");
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            Assert.IsTrue(r.S.Production.TryBuy("drill"), "the tutorial reward pays for the first machine");
            r.S.Orders.Update(0);
            Assert.AreEqual(OrderStatus.Completed, r.S.Orders.Get("t2").Status);
            Assert.IsTrue(r.S.Orders.TryClaim("t2"));
            r.S.Orders.Update(0);
            Assert.IsFalse(r.S.Orders.TutorialCompleted);
        }

        [Test]
        public void Tutorial_CompletionEmitsAnalyticsOnce()
        {
            var r = Rig.New();
            r.Money(1000);
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0); r.S.Orders.TryClaim("t1");
            r.S.Production.TryBuy("drill", 2);
            r.S.Orders.Update(0); r.S.Orders.TryClaim("t2");
            r.Run(1);
            r.S.Orders.Update(0); Assert.IsTrue(r.S.Orders.TryClaim("t3"));
            r.Run(1);
            Assert.IsTrue(r.S.Orders.TutorialCompleted);
            r.Run(3);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.TutorialCompleted));
        }

        [Test]
        public void Orders_RewardScalesWithProductionButHasFloor()
        {
            var r = Rig.New();
            var o = r.Config.Orders.First(x => x.Id == "a1");
            Assert.AreEqual(o.RewardMin, r.S.Orders.RewardFor(o), 1e-9);
            r.GiveProduction(20);
            Assert.AreEqual(o.RewardOpsSeconds * r.S.Production.BaseOutputPerSecond, r.S.Orders.RewardFor(o), 1e-9);
        }

        [Test]
        public void Orders_ClaimRequiresCompletion_AndPaysOnce()
        {
            var r = Rig.New();
            Assert.IsFalse(r.S.Orders.TryClaim("t1"));
            Assert.IsFalse(r.S.Orders.TryClaim("ghost"));
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            double b = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            Assert.IsFalse(r.S.Orders.TryClaim("t1"));
            Assert.AreEqual(b + 15, r.S.Wallet.Balance, 1e-9);
        }

        [Test]
        public void Orders_ActiveCountIsLimited()
        {
            var r = Rig.New();
            r.Money(1e9);
            r.ReachPhase(1);
            r.S.Orders.Update(0);
            Assert.LessOrEqual(r.S.Orders.Views().Count(), OrderSystem.MaxActive);
        }

        [Test]
        public void Orders_DeadlineExpiryResetsWithoutPenalty()
        {
            var cfg = DefaultConfig.Create();
            var o = cfg.Orders.First(x => x.Id == "t1");
            o.DeadlineSeconds = 10;
            var r = Rig.New(cfg: cfg);
            for (int i = 0; i < 5; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            Assert.AreEqual(5, r.S.Orders.Get("t1").Progress);
            double bal = r.S.Wallet.Balance;
            r.S.Orders.Update(11);
            Assert.AreEqual(0, r.S.Orders.Get("t1").Progress);
            Assert.AreEqual(OrderStatus.Active, r.S.Orders.Get("t1").Status);
            Assert.AreEqual(10, r.S.Orders.Get("t1").RemainingSeconds, 1e-9);
            Assert.AreEqual(bal, r.S.Wallet.Balance);
        }

        [Test]
        public void Orders_StateSurvivesRestart()
        {
            var r = Rig.New();
            for (int i = 0; i < 6; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            var r2 = r.Restart();
            Assert.AreEqual(6, r2.S.Orders.Get("t1").Progress);
        }

        [Test]
        public void Orders_OrdersCanBeCompletedWithoutTheRiskModule()
        {
            var c = DefaultConfig.Create();
            foreach (var phase in Enumerable.Range(0, c.Phases.Count))
            {
                var inPhase = c.Orders.Where(o => o.UnlockPhase == phase).ToList();
                if (inPhase.Count == 0) continue;
                int nonEvent = inPhase.Count(o => o.Type != OrderType.CompleteEvent);
                Assert.GreaterOrEqual(nonEvent, 2, "phase " + phase + " must offer an alternate path through normal production");
            }
            // Every CompleteEvent order has an alternative that unlocks the same next content: nothing depends on it.
            var ids = c.Orders.Where(o => o.Type == OrderType.CompleteEvent).Select(o => o.Id).ToList();
            Assert.IsFalse(c.Orders.Any(o => ids.Contains(o.PrerequisiteOrderId)), "no order is gated behind a risk event");
        }

        [Test]
        public void Orders_ProgressForEachTypeIsTracked()
        {
            var r = Rig.New();
            r.S.Wallet.Restore(1e9, 0, 0);   // balance without lifetime earnings, so the phase does not jump
            r.S.Production.TryBuy("drill", 10);
            r.S.Production.TryBuy("lathe", 3);
            Assert.IsTrue(r.S.Production.TryBuyUpgrade("up_drill"));
            r.Run(2);
            // finish tutorial chain so that phase-0 orders activate
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            foreach (var id in new[] { "t1", "t2", "t3" })
            {
                r.S.Orders.Update(0);
                Assert.IsTrue(r.S.Orders.TryClaim(id), id);
            }
            r.S.Orders.Update(0);
            Assert.AreEqual(OrderStatus.Completed, r.S.Orders.Get("a2").Status, "MachineLevel");
            Assert.AreEqual(OrderStatus.Completed, r.S.Orders.Get("a3").Status, "BuyUpgrade");
        }

        // ---------------------------------------------------------------- phases
        [Test]
        public void Phases_AdvanceWithLifetimeEarningsAndNeverGoBack()
        {
            var r = Rig.New();
            Assert.AreEqual(0, r.S.Progression.PhaseIndex);
            r.ReachPhase(1);
            Assert.AreEqual(1, r.S.Progression.PhaseIndex);
            r.S.Wallet.TrySpend(r.S.Wallet.Balance, "spend all");
            r.S.Progression.Update();
            Assert.AreEqual(1, r.S.Progression.PhaseIndex);
        }

        [Test]
        public void Phases_ThreeDistinctStagesExist()
        {
            var c = DefaultConfig.Create();
            Assert.GreaterOrEqual(c.Phases.Count, 3);
            Assert.IsTrue(c.Machines.Any(m => m.UnlockPhase == 0));
            Assert.IsTrue(c.Machines.Any(m => m.UnlockPhase == 1));
            Assert.IsTrue(c.Machines.Any(m => m.UnlockPhase == 2));
            Assert.IsTrue(c.Synergies.Any(), "advanced phase has synergies");
        }

        [Test]
        public void Phases_UnlockMachinesProgressively()
        {
            var r = Rig.New();
            r.Money(1e18);
            r.S.Production.TryBuy("drill", 5); r.S.Production.TryBuy("lathe", 5); r.S.Production.TryBuy("mill", 5);
            Assert.IsFalse(r.S.Production.IsUnlocked("cnc"), "needs phase 1");
            r.ReachPhase(1);
            Assert.IsTrue(r.S.Production.IsUnlocked("cnc"));
        }

        // ---------------------------------------------------------------- config
        [Test]
        public void Config_DefaultIsValid()
        {
            var e = ConfigValidator.Validate(DefaultConfig.Create());
            Assert.IsEmpty(e, string.Join("\n", e));
        }

        [Test]
        public void Config_RoundTripsThroughJson_AndStillValid()
        {
            var c = DefaultConfig.Create();
            var back = JsonMapper.FromJson<GameConfig>(JsonMapper.ToJson(c));
            Assert.AreEqual(c.Machines.Count, back.Machines.Count);
            Assert.AreEqual(c.Machines[3].BaseCost, back.Machines[3].BaseCost);
            Assert.AreEqual(c.Experimental.Outcomes[2].ReturnMultiplier, back.Experimental.Outcomes[2].ReturnMultiplier);
            Assert.AreEqual(c.Orders.Count, back.Orders.Count);
            Assert.AreEqual(c.Crate.Entries[4].Kind, back.Crate.Entries[4].Kind);
            Assert.IsEmpty(ConfigValidator.Validate(back));
        }

        [Test]
        public void Config_PartialJsonOverridesOnlyWhatItSpecifies()
        {
            var c = JsonMapper.FromJson<GameConfig>("{\"Economy\":{\"TapBase\":5},\"Offline\":{\"MaxSeconds\":3600}}");
            Assert.AreEqual(5, c.Economy.TapBase);
            Assert.AreEqual(3600, c.Offline.MaxSeconds);
            Assert.AreEqual(0.5, c.Offline.Efficiency, "untouched field keeps its default");
            Assert.AreEqual(0, c.Machines.Count, "lists not present stay at the class default (empty): full config must be provided for content");
        }

        [Test]
        public void Config_ChangingJsonCostsChangesGameplay_WithoutCodeChanges()
        {
            var c = DefaultConfig.Create();
            c.Machines[0].BaseCost = 100;
            var r = Rig.New(cfg: JsonMapper.FromJson<GameConfig>(JsonMapper.ToJson(c)));
            r.Money(99);
            Assert.IsFalse(r.S.Production.TryBuy("drill"));
            r.Money(1);
            Assert.IsTrue(r.S.Production.TryBuy("drill"));
        }

        [Test]
        public void Validator_FlagsStructuralProblems()
        {
            var c = DefaultConfig.Create();
            c.Machines[1].CostGrowth = 1.0;
            c.Machines[2].UnlockRequiresMachineId = "ghost";
            c.Machines.Add(new MachineDef { Id = "drill", BaseCost = 1, CostGrowth = 1.1, BaseOutput = 1 });
            c.Orders[1].PrerequisiteOrderId = "t3";   // t1 -> t2 -> t3 -> t2 cycle (t3 requires t2)
            c.Upgrades[1].TargetMachineId = "ghost";
            var e = ConfigValidator.Validate(c);
            Assert.IsTrue(e.Any(x => x.Contains("CostGrowth")));
            Assert.IsTrue(e.Any(x => x.Contains("unknown prerequisite machine")));
            Assert.IsTrue(e.Any(x => x.Contains("duplicate machine id")));
            Assert.IsTrue(e.Any(x => x.Contains("cycle")));
            Assert.IsTrue(e.Any(x => x.Contains("unknown target machine")));
        }

        [Test]
        public void Session_RefusesToStartWithInvalidConfig_WithReadableMessage()
        {
            var c = DefaultConfig.Create();
            c.Crate.Entries[0].ProbabilityPercent = 50;
            var ex = Assert.Throws<ArgumentException>(() => Rig.New(cfg: c));
            StringAssert.Contains("Crate.Entries", ex.Message);
        }

        [Test]
        public void Config_NoHardcodedNumbersNeededForPhaseUnlockOfEvents()
        {
            var c = DefaultConfig.Create();
            c.Experimental.UnlockPhase = 99;
            Assert.IsTrue(ConfigValidator.Validate(c).Count == 0, "event unlock phase is data; an out-of-range value just means never");
            var r = Rig.New(cfg: c);
            r.GiveProduction(10);
            r.S.Experimental.Advance(1e7);
            Assert.AreEqual(EventStage.Cooldown, r.S.Experimental.Stage);
        }

        // ---------------------------------------------------------------- session & analytics
        [Test]
        public void Session_LifecycleEmitsStartAndEnd()
        {
            var r = Rig.New();
            r.Run(10);
            r.S.End();
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.SessionStarted));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.SessionEnded));
            var ended = r.Analytics.Records.First(x => x.Name == AnalyticsEvents.SessionEnded);
            Assert.AreEqual(10, (int)ended.Params["duration_s"]);
            Assert.IsFalse(r.S.Started);
            r.S.Update(100);                               // updates after End are ignored
            Assert.AreEqual(0, r.S.Wallet.Balance);
        }

        [Test]
        public void Session_CountsSessionsAcrossRestarts()
        {
            var r = Rig.New();
            var r2 = r.Restart();
            var r3 = r2.Restart();
            Assert.AreEqual(3, r3.S.Stats.Sessions);
        }

        [Test]
        public void Analytics_PurchasesAndOrdersAreTracked()
        {
            var r = Rig.New();
            r.Money(1e6);
            r.S.Production.TryBuy("drill");
            r.S.Production.TryBuyUpgrade("up_bench");
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            r.S.Orders.TryClaim("t1");
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.MachinePurchased));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.UpgradePurchased));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.OrderCompleted));
            var rec = r.Analytics.Records.First(x => x.Name == AnalyticsEvents.MachinePurchased);
            Assert.AreEqual("drill", rec.Params["machine_id"]);
        }

        [Test]
        public void Analytics_OfflineClaimTracked()
        {
            var a = Rig.New();
            a.GiveProduction(20);
            a.S.Pause();
            a.Clock.Advance(3600);
            var b = a.Restart(saveFirst: false);
            b.S.ClaimOffline();
            Assert.AreEqual(1, b.Analytics.Count(AnalyticsEvents.OfflineRewardClaimed));
        }

        [Test]
        public void Analytics_RiskEventsTracked()
        {
            var r = Rig.New(new FixedRandom(0.5));
            r.GiveProduction(20);
            r.ReachPhase(1);
            r.Money(1e7);
            r.S.Experimental.Advance(10000);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RiskEventShown));
            var pv = r.S.Experimental.GetPreview();
            r.S.Experimental.TryAccept(ExperimentChoice.Experimental, pv.CostExperimental);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RiskEventAccepted));
            r.S.Experimental.Advance(1000);
            r.S.Experimental.TryCollect();
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RiskEventCompleted));
        }

        [Test]
        public void Analytics_ForbiddenKeysAreDroppedFromParams()
        {
            var p = AnalyticsParams.Of("email", "a@b.c", "device_id", "x", "level", 3);
            Assert.AreEqual(1, p.Count);
            var a = new DebugAnalytics();
            a.Track("x", new System.Collections.Generic.Dictionary<string, object> { ["user_id"] = "u", ["ok"] = 1 });
            Assert.IsFalse(a.Records[0].Params.ContainsKey("user_id"));
            Assert.IsTrue(a.Records[0].Params.ContainsKey("ok"));
        }

        [Test]
        public void Analytics_DebugBufferIsBounded()
        {
            var a = new DebugAnalytics();
            for (int i = 0; i < 2000; i++) a.Track("e");
            Assert.LessOrEqual(a.Records.Count, 500);
        }

        // ---------------------------------------------------------------- performance (logic only)
        [Test]
        public void Performance_OneHourAt60FpsRunsFastAndStaysFinite()
        {
            var r = Rig.New();
            r.Money(1e12);
            r.S.Production.TryBuy("drill", 20); r.S.Production.TryBuy("lathe", 20); r.S.Production.TryBuy("mill", 20);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 60 * 3600; i++) { r.Clock.Advance(1.0 / 60); r.S.Update(1.0 / 60); }
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 15000, "216k ticks should be far under the budget (this is a regression tripwire, not a device benchmark)");
            Assert.IsTrue(Num.IsFinite(r.S.Wallet.Balance));
        }

        [Test]
        public void Startup_NewSessionStartsQuickly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++) Rig.New();
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds / 100.0, 50, "avg session creation+start under 50 ms on a dev machine");
        }
    }
}
