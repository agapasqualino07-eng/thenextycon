using System;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class EconomyTests
    {
        [Test]
        public void Wallet_InsufficientFunds_RejectsAndKeepsBalance()
        {
            var w = new Wallet(1e24);
            w.Add(10, "t");
            Assert.IsFalse(w.TrySpend(11, "x"));
            Assert.AreEqual(10, w.Balance);
        }

        [Test]
        public void Wallet_ValidSpend_DebitsOnce()
        {
            var w = new Wallet(1e24);
            w.Add(100, "t");
            Assert.IsTrue(w.TrySpend(40, "x"));
            Assert.AreEqual(60, w.Balance, 1e-9);
            Assert.AreEqual(40, w.TotalSpent, 1e-9);
        }

        [Test]
        public void Wallet_InvalidAmounts_Rejected_NeverNegative()
        {
            var w = new Wallet(1e24);
            w.Add(5, "t");
            Assert.IsFalse(w.TrySpend(-1, "neg"));
            Assert.IsFalse(w.TrySpend(double.NaN, "nan"));
            Assert.IsFalse(w.TrySpend(double.PositiveInfinity, "inf"));
            Assert.AreEqual(0, w.Add(-5, "neg"));
            Assert.AreEqual(0, w.Add(double.NaN, "nan"));
            Assert.AreEqual(5, w.Balance);
            Assert.IsTrue(w.TrySpend(5, "all"));
            Assert.AreEqual(0, w.Balance);
            Assert.IsFalse(w.TrySpend(0.0001, "more"));
            Assert.GreaterOrEqual(w.Balance, 0);
        }

        [Test]
        public void Wallet_Overflow_ClampedAtMaxBalance()
        {
            var w = new Wallet(1000);
            Assert.AreEqual(1000, w.Add(5000, "big"));
            Assert.AreEqual(1000, w.Balance);
            Assert.AreEqual(0, w.Add(1, "more"));
            Assert.IsTrue(Num.IsFinite(w.TotalEarned));
        }

        [Test]
        public void Wallet_LogsTransactions_IncludingRejected()
        {
            var w = new Wallet(1e24);
            w.Add(10, "gain");
            w.TrySpend(100, "too much");
            Assert.AreEqual(2, w.Log.Count);
            Assert.IsTrue(w.Log[0].Applied);
            Assert.IsFalse(w.Log[1].Applied);
        }

        [Test]
        public void Wallet_LogIsBounded()
        {
            var w = new Wallet(1e24);
            for (int i = 0; i < 1000; i++) w.Add(1, "t");
            Assert.LessOrEqual(w.Log.Count, 200);
        }

        [Test]
        public void Purchase_ValidBuy_DebitsExactCostAndRaisesLevel()
        {
            var r = Rig.New();
            r.Money(100);
            double before = r.S.Wallet.Balance;
            double cost = r.S.Production.NextCost("drill");
            Assert.IsTrue(r.S.Production.TryBuy("drill"));
            Assert.AreEqual(1, r.S.Production.Level("drill"));
            Assert.AreEqual(before - cost, r.S.Wallet.Balance, 1e-9);
        }

        [Test]
        public void Purchase_InsufficientBalance_DoesNothing()
        {
            var r = Rig.New();
            Assert.IsFalse(r.S.Production.TryBuy("drill"));
            Assert.AreEqual(0, r.S.Production.Level("drill"));
        }

        [Test]
        public void Purchase_RepeatedTapsWithFundsForOne_BuyOnlyOnce()
        {
            var r = Rig.New();
            double cost = r.Config.Machines[0].BaseCost;
            r.Money(cost);                              // exactly one drill
            int ok = 0;
            for (int i = 0; i < 5; i++) if (r.S.Production.TryBuy("drill")) ok++;
            Assert.AreEqual(1, ok);
            Assert.AreEqual(1, r.S.Production.Level("drill"));
            Assert.GreaterOrEqual(r.S.Wallet.Balance, 0);
        }

        [Test]
        public void Purchase_BulkIsAtomic_AllOrNothing()
        {
            var r = Rig.New();
            r.Money(r.S.Production.NextCost("drill", 3) - 1);   // 1 short for 3 levels
            Assert.IsFalse(r.S.Production.TryBuy("drill", 3));
            Assert.AreEqual(0, r.S.Production.Level("drill"));
            Assert.AreEqual(r.S.Production.NextCost("drill", 3) - 1, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Purchase_LockedMachine_Rejected()
        {
            var r = Rig.New();
            r.Money(1e12);
            Assert.IsFalse(r.S.Production.IsUnlocked("lathe"));
            Assert.IsFalse(r.S.Production.TryBuy("lathe"));
        }

        [Test]
        public void Purchase_UnknownId_Rejected()
        {
            var r = Rig.New();
            r.Money(1e12);
            Assert.IsFalse(r.S.Production.TryBuy("nope"));
            Assert.IsFalse(r.S.Production.TryBuyUpgrade("nope"));
        }

        [Test]
        public void Purchase_RespectsMaxLevel()
        {
            var cfg = DefaultConfig.Create();
            cfg.Machines[0].MaxLevel = 3;
            var r = Rig.New(cfg: cfg);
            r.Money(1e9);
            Assert.IsTrue(r.S.Production.TryBuy("drill", 3));
            Assert.IsFalse(r.S.Production.TryBuy("drill"));
            Assert.AreEqual(3, r.S.Production.Level("drill"));
        }

        [Test]
        public void Formula_LevelCost_MatchesGeometricGrowth()
        {
            Assert.AreEqual(15 * Math.Pow(1.15, 3), Formulas.LevelCost(15, 1.15, 3), 1e-9);
            Assert.AreEqual(15, Formulas.LevelCost(15, 1.15, 0), 1e-12);
        }

        [Test]
        public void Formula_BulkCost_EqualsSumOfIndividualCosts()
        {
            for (int start = 0; start < 40; start += 7)
                for (int count = 1; count < 30; count += 4)
                {
                    double sum = 0;
                    for (int i = 0; i < count; i++) sum += Formulas.LevelCost(15, 1.15, start + i);
                    Assert.AreEqual(sum, Formulas.BulkCost(15, 1.15, start, count), sum * 1e-9);
                }
        }

        [Test]
        public void Formula_MaxAffordable_MatchesBruteForce()
        {
            var rnd = new Random(7);
            for (int k = 0; k < 300; k++)
            {
                int lvl = rnd.Next(0, 60);
                double g = 1.05 + rnd.NextDouble() * 0.2;
                double budget = Math.Pow(10, rnd.NextDouble() * 12);
                int expected = 0;
                while (expected < 500 && Formulas.BulkCost(10, g, lvl, expected + 1) <= budget) expected++;
                Assert.AreEqual(expected, Formulas.MaxAffordable(10, g, lvl, budget, 500), "lvl=" + lvl + " g=" + g + " budget=" + budget);
            }
        }

        [Test]
        public void Production_OutputIsLevelTimesBaseOutput()
        {
            var r = Rig.New();
            r.Money(1e6);
            r.S.Production.TryBuy("drill", 4);
            Assert.AreEqual(0.5 * 4, r.S.Production.BaseOutputPerSecond, 1e-9);
        }

        [Test]
        public void Production_TickPaysOutputTimesDt()
        {
            var r = Rig.New();
            r.Money(1e6);
            r.S.Production.TryBuy("drill", 4);
            double before = r.S.Wallet.Balance;
            r.S.Production.Tick(10);
            Assert.AreEqual(before + 20, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Production_MilestoneDoublesOutputEvery25Levels()
        {
            var r = Rig.New();
            r.Money(1e15);
            r.S.Production.TryBuy("drill", 24);
            double at24 = r.S.Production.BaseOutputPerSecond;
            r.S.Production.TryBuy("drill", 1);
            Assert.AreEqual(at24 / 24 * 25 * 2, r.S.Production.BaseOutputPerSecond, 1e-9);
        }

        [Test]
        public void Production_UpgradeMultipliesMachineOutput()
        {
            var r = Rig.New();
            r.Money(1e9);
            r.S.Production.TryBuy("drill", 10);
            double before = r.S.Production.BaseOutputPerSecond;
            Assert.IsTrue(r.S.Production.TryBuyUpgrade("up_drill"));
            Assert.AreEqual(before * 2, r.S.Production.BaseOutputPerSecond, 1e-9);
            Assert.IsFalse(r.S.Production.TryBuyUpgrade("up_drill"), "an upgrade can only be bought once");
        }

        [Test]
        public void Production_UpgradeRequirementEnforced()
        {
            var r = Rig.New();
            r.Money(1e9);
            r.S.Production.TryBuy("drill", 9);
            Assert.IsFalse(r.S.Production.TryBuyUpgrade("up_drill"));
            r.S.Production.TryBuy("drill", 1);
            Assert.IsTrue(r.S.Production.TryBuyUpgrade("up_drill"));
        }

        [Test]
        public void Production_TapValue_UsesBaseAndMultiplier()
        {
            var r = Rig.New();
            Assert.AreEqual(1.0, r.S.Production.TapValue, 1e-9);
            r.Money(1000);
            r.S.Production.TryBuyUpgrade("up_bench");
            Assert.AreEqual(2.0, r.S.Production.TapValue, 1e-9);
            double b = r.S.Wallet.Balance;
            r.S.Production.Tap();
            Assert.AreEqual(b + 2.0, r.S.Wallet.Balance, 1e-9);
        }

        [Test]
        public void Boosts_DoNotStackWithSameSource_AndAreCapped()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            double baseOps = r.S.Production.BaseOutputPerSecond;
            r.S.Production.AddBoost("a", 2, 60);
            r.S.Production.AddBoost("a", 2, 60);
            Assert.AreEqual(baseOps * 2, r.S.Production.OutputPerSecond, 1e-9);
            r.S.Production.AddBoost("b", 3, 60);
            r.S.Production.AddBoost("c", 5, 60);
            Assert.AreEqual(baseOps * r.Config.Economy.MaxTempMultiplier, r.S.Production.OutputPerSecond, 1e-9, "product must be capped");
        }

        [Test]
        public void Boosts_ExpireAfterDuration()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            r.S.Production.AddBoost("a", 2, 30);
            r.Run(31);
            Assert.AreEqual(0, r.S.Production.BoostRemaining("a"));
            Assert.AreEqual(1.0, r.S.Production.TempMultiplier);
        }

        [Test]
        public void Boosts_InvalidInputIgnored()
        {
            var r = Rig.New();
            r.S.Production.AddBoost("a", 0.5, 10);
            r.S.Production.AddBoost("a", double.NaN, 10);
            r.S.Production.AddBoost("a", 2, -1);
            Assert.AreEqual(1.0, r.S.Production.TempMultiplier);
        }

        [Test]
        public void NoOverflow_HugeLevelsStayFinite()
        {
            Assert.IsTrue(Num.IsFinite(Formulas.LevelCost(15, 1.5, 100000)));
            Assert.IsTrue(Num.IsFinite(Formulas.BulkCost(15, 1.2, 50000, 50000)));
            Assert.AreEqual(0, Formulas.MaxAffordable(15, 1.2, 50000, 1e24, 100));
            var r = Rig.New();
            r.Money(1e24);
            Assert.IsFalse(r.S.Production.TryBuy("drill", 400), "cost exceeds even the maximum balance");
            Assert.IsTrue(Num.IsFinite(r.S.Production.NextCost("drill", 400)));
        }

        [Test]
        public void Validator_RejectsConfigWhoseCostsOverflow()
        {
            var cfg = DefaultConfig.Create();
            cfg.Machines[0].MaxLevel = 100000;
            Assert.IsTrue(ConfigValidator.Validate(cfg).Any(e => e.Contains("overflows")));
        }

        [Test]
        public void NoOverflow_BalanceNeverExceedsCapWhileProducing()
        {
            var cfg = DefaultConfig.Create();
            cfg.Economy.MaxBalance = 1e6;
            var r = Rig.New(cfg: cfg);
            r.Money(1e6);
            r.S.Production.TryBuy("drill", 10);
            r.Run(600);
            Assert.LessOrEqual(r.S.Wallet.Balance, 1e6);
            Assert.IsTrue(Num.IsFinite(r.S.Wallet.TotalEarned));
        }

        [Test]
        public void Overhaul_SpendsPartsAndBoostsMachine()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            double before = r.S.Production.BaseOutputPerSecond;
            Assert.IsFalse(r.S.Production.TryOverhaul("drill"), "needs parts");
            r.S.Production.AddParts(5);
            Assert.IsTrue(r.S.Production.TryOverhaul("drill"));
            Assert.AreEqual(0, r.S.Production.Parts);
            Assert.AreEqual(before * (1 + r.Config.Economy.OverhaulBonus), r.S.Production.BaseOutputPerSecond, 1e-9);
        }

        [Test]
        public void Synergy_OnlyActiveInAdvancedPhase()
        {
            var r = Rig.New();
            r.Money(1e18);
            r.ReachPhase(2);
            Assert.IsTrue(r.S.Production.TryBuy("drill", 5));
            Assert.IsTrue(r.S.Production.TryBuy("lathe", 5));
            Assert.IsTrue(r.S.Production.TryBuy("mill", 5));
            Assert.IsTrue(r.S.Production.TryBuy("cnc", 5));
            Assert.IsTrue(r.S.Production.TryBuy("press", 1));
            double pressOut = r.S.Production.MachineOutput("press");
            Assert.AreEqual(2048 * 1 * (1 + 0.01 * 5), pressOut, 1e-6);
        }
    }
}
