using System;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class EngagementTests
    {
        // ------------------------------------------------------------ frenzy
        [Test]
        public void Frenzy_FillsWithTapsAndTriggersBoost()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            int triggered = 0;
            r.S.Frenzy.Triggered += () => triggered++;
            for (int i = 0; i < r.Config.Frenzy.TapsToFill - 1; i++) r.S.Production.Tap();
            Assert.IsFalse(r.S.Frenzy.Active);
            Assert.Greater(r.S.Frenzy.Fill, 0.9);
            r.S.Production.Tap();
            Assert.AreEqual(1, triggered);
            Assert.IsTrue(r.S.Frenzy.Active);
            Assert.AreEqual(r.Config.Frenzy.Multiplier, r.S.Production.TempMultiplier, 1e-9);
            Assert.AreEqual(0, r.S.Frenzy.Charge);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.FrenzyTriggered));
        }

        [Test]
        public void Frenzy_DecaysWhenNotTapping_AndDoesNotRefillWhileActive()
        {
            var r = Rig.New();
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.Run(3);
            Assert.AreEqual(10 - 3 * r.Config.Frenzy.DecayPerSecond, r.S.Frenzy.Charge, 1e-6);
            r.Run(60);
            Assert.AreEqual(0, r.S.Frenzy.Charge);
            for (int i = 0; i < r.Config.Frenzy.TapsToFill; i++) r.S.Production.Tap();
            Assert.IsTrue(r.S.Frenzy.Active);
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            Assert.AreEqual(0, r.S.Frenzy.Charge, "no stacking: taps during frenzy do not charge the next one");
            r.Run(r.Config.Frenzy.Seconds + 1);
            Assert.IsFalse(r.S.Frenzy.Active);
        }

        [Test]
        public void Frenzy_BoostsTapValueToo_AndRespectsGlobalCap()
        {
            var r = Rig.New();
            double before = r.S.Production.TapValue;
            for (int i = 0; i < r.Config.Frenzy.TapsToFill; i++) r.S.Production.Tap();
            Assert.AreEqual(before * r.Config.Frenzy.Multiplier, r.S.Production.TapValue, 1e-9);
            r.S.Production.AddBoost("a", 3, 60);
            r.S.Production.AddBoost("b", 3, 60);
            Assert.AreEqual(r.Config.Economy.MaxTempMultiplier, r.S.Production.TempMultiplier, 1e-9);
        }

        [Test]
        public void Frenzy_CanBeDisabledByConfig()
        {
            var cfg = DefaultConfig.Create();
            cfg.Frenzy.Enabled = false;
            var r = Rig.New(cfg: cfg);
            for (int i = 0; i < 200; i++) r.S.Production.Tap();
            Assert.IsFalse(r.S.Frenzy.Active);
        }

        // ------------------------------------------------------------ daily gift
        [Test]
        public void Daily_AvailableOnFirstDay_ClaimOnce_ThenNextDay()
        {
            var r = Rig.New();
            Assert.IsNotNull(r.S.Daily.Pending);
            Assert.AreEqual(0, r.S.Daily.Pending.DayIndex);
            double bal = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.ClaimDaily());
            Assert.AreEqual(bal + r.Config.Daily.Days[0].MinCurrency, r.S.Wallet.Balance, 1e-6);
            Assert.IsFalse(r.S.ClaimDaily(), "only one gift per day");
            r.S.Daily.Check();
            Assert.IsNull(r.S.Daily.Pending);
            r.Clock.Advance(24 * 3600);
            r.S.Resume();
            Assert.IsNotNull(r.S.Daily.Pending);
            Assert.AreEqual(1, r.S.Daily.Pending.DayIndex);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.DailyRewardClaimed));
        }

        [Test]
        public void Daily_SkippingDaysDoesNotResetTheCalendar()
        {
            var r = Rig.New();
            r.S.ClaimDaily();
            r.Clock.Advance(5 * 24 * 3600);       // away for 5 days
            r.S.Resume();
            Assert.AreEqual(1, r.S.Daily.Pending.DayIndex, "continues from day 2, no punishment");
        }

        [Test]
        public void Daily_CycleWrapsAfterLastDay_AndPaysEachKind()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            int crates = 0, parts = 0;
            for (int d = 0; d < r.Config.Daily.Days.Count + 1; d++)
            {
                var def = r.S.Daily.Pending.Def;
                int c0 = r.S.Crates.Available, p0 = r.S.Production.Parts;
                Assert.IsTrue(r.S.ClaimDaily(), "day " + d);
                crates += r.S.Crates.Available - c0; parts += r.S.Production.Parts - p0;
                if (def.Kind == DailyRewardKind.Crate) Assert.AreEqual(c0 + def.Amount, r.S.Crates.Available);
                if (def.Kind == DailyRewardKind.Parts) Assert.AreEqual(p0 + def.Amount, r.S.Production.Parts);
                r.Clock.Advance(24 * 3600); r.S.Resume();
            }
            Assert.AreEqual(1, r.S.Daily.Pending.DayIndex, "after 8 claims the 7-day calendar wrapped");
            Assert.Greater(crates, 0); Assert.Greater(parts, 0);
        }

        [Test]
        public void Daily_CurrencyScalesWithProductionWithFloor()
        {
            var r = Rig.New();
            var def = r.Config.Daily.Days[0];
            Assert.AreEqual(def.MinCurrency, r.S.Daily.CurrencyValue(def));
            r.GiveProduction(50);
            Assert.AreEqual(Math.Floor(def.OpsSeconds * r.S.Production.BaseOutputPerSecond), r.S.Daily.CurrencyValue(def));
        }

        [Test]
        public void Daily_ClockMovedBack_NoGift_NoFarming()
        {
            var r = Rig.New();
            r.S.ClaimDaily();
            r.Clock.Advance(-3 * 24 * 3600);
            r.S.Resume();
            Assert.IsNull(r.S.Daily.Pending);
            Assert.IsTrue(r.S.Daily.ClockAnomaly);
            r.Clock.Advance(3 * 24 * 3600 + 3600);     // back to the claim day: still nothing
            r.S.Resume();
            Assert.IsNull(r.S.Daily.Pending);
        }

        [Test]
        public void Daily_RestartBeforeClaim_KeepsGift_AfterClaim_DoesNotRepeat()
        {
            var r = Rig.New();
            int id = r.S.Daily.Pending.Id;
            var r2 = r.Restart();
            Assert.AreEqual(id, r2.S.Daily.Pending.Id);
            Assert.IsTrue(r2.S.ClaimDaily());
            var r3 = r2.Restart();
            Assert.IsNull(r3.S.Daily.Pending);
            Assert.AreEqual(1, r3.S.Daily.Claims);
        }

        [Test]
        public void Daily_AdBonusDoublesOnce_AndSurvivesRestart()
        {
            var r = Rig.New();
            double bal = r.S.Wallet.Balance;
            bool granted = false;
            Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestDailyBonus((g, w) => granted = g));
            Assert.IsTrue(granted);
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.RequestDailyBonus((g, w) => { }), "bonus only once");
            var r2 = r.Restart();
            Assert.IsTrue(r2.S.Daily.Pending.BonusGranted, "earned bonus persists across a restart");
            Assert.IsTrue(r2.S.ClaimDaily());
            Assert.AreEqual(bal + 2 * r.Config.Daily.Days[0].MinCurrency, r2.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Daily_FailedOrClosedAd_NoBonus_BaseStillClaimable()
        {
            var r = Rig.New();
            r.Ads.Behavior = MockAdBehavior.CloseEarly;
            bool granted = true;
            r.S.Monetization.RequestDailyBonus((g, w) => granted = g);
            Assert.IsFalse(granted);
            Assert.IsFalse(r.S.Daily.Pending.BonusGranted);
            double bal = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.ClaimDaily());
            Assert.AreEqual(bal + r.Config.Daily.Days[0].MinCurrency, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Daily_RepeatedAdCallbacks_GrantOnce()
        {
            var r = Rig.New();
            r.Ads.AutoResolve = false;
            int grants = 0;
            r.S.Monetization.RequestDailyBonus((g, w) => { if (g) grants++; });
            r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnClosed();
            Assert.AreEqual(1, grants);
        }

        [Test]
        public void Daily_DisabledByConfig()
        {
            var cfg = DefaultConfig.Create();
            cfg.Daily.Enabled = false;
            var r = Rig.New(cfg: cfg);
            Assert.IsNull(r.S.Daily.Pending);
            Assert.IsFalse(r.S.ClaimDaily());
        }

        [Test]
        public void Validator_RejectsBrokenEngagementConfig()
        {
            var c = DefaultConfig.Create();
            c.Frenzy.TapsToFill = 0;
            c.Daily.Days.Clear();
            var e = ConfigValidator.Validate(c);
            Assert.IsTrue(e.Any(x => x.StartsWith("Frenzy")));
            Assert.IsTrue(e.Any(x => x.StartsWith("Daily")));
        }
    }
}
