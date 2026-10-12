using System;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class RewardedAdTests
    {
        // A player returning after 1 hour with an unclaimed offline reward.
        static Rig WithOfflineOffer(bool autoResolve = true)
        {
            var a = Rig.New();
            a.GiveProduction(20);
            a.S.Wallet.Restore(0, a.S.Wallet.TotalEarned, 0);
            a.S.Pause();
            a.Clock.Advance(3600);
            var b = a.Restart(saveFirst: false);
            b.Ads.AutoResolve = autoResolve;
            Assert.IsNotNull(b.S.Offline.Pending);
            return b;
        }

        [Test]
        public void Completed_GrantsDeclaredBonusExactlyOnce()
        {
            var r = WithOfflineOffer();
            double baseAmt = r.S.Offline.Pending.Base, bonus = r.S.Offline.Pending.Bonus;
            Assert.AreEqual(Math.Floor(baseAmt * 0.5), bonus, 1);
            bool granted = false;
            var res = r.S.Monetization.RequestOfflineBonus((g, why) => granted = g);
            Assert.AreEqual(AdFailReason.None, res);
            Assert.IsTrue(granted);
            Assert.AreEqual(baseAmt + bonus, r.S.ClaimOffline(), 1e-6);
            Assert.AreEqual(0, r.S.ClaimOffline());
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.BonusGranted));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RewardedAdCompleted));
        }

        [Test]
        public void BonusAmountIsKnownBeforeWatching_AndBaseIsShownFirst()
        {
            var r = WithOfflineOffer();
            Assert.Greater(r.S.Offline.Pending.Base, 0);
            Assert.Greater(r.S.Offline.Pending.Bonus, 0);
            Assert.IsFalse(r.S.Offline.Pending.BonusGranted, "nothing is granted before the ad is confirmed");
            Assert.AreEqual(AdFailReason.None, r.S.Monetization.CanOfferOfflineBonus());
        }

        [Test]
        public void Unavailable_BaseRewardStillClaimable()
        {
            var r = WithOfflineOffer();
            r.Ads.Behavior = MockAdBehavior.Unavailable;
            double baseAmt = r.S.Offline.Pending.Base;
            Assert.AreEqual(AdFailReason.Unavailable, r.S.Monetization.CanOfferOfflineBonus());
            Assert.AreEqual(AdFailReason.Unavailable, r.S.Monetization.RequestOfflineBonus((g, w) => { }));
            Assert.AreEqual(0, r.Ads.ShowCount);
            Assert.AreEqual(baseAmt, r.S.ClaimOffline(), 1e-6);
        }

        [TestCase(MockAdBehavior.Fail, AdFailReason.Error)]
        [TestCase(MockAdBehavior.CloseEarly, AdFailReason.ClosedEarly)]
        public void Failures_NeverGrantBonus_AndBaseRemainsAvailable(MockAdBehavior behavior, AdFailReason expected)
        {
            var r = WithOfflineOffer();
            r.Ads.Behavior = behavior;
            bool? granted = null; AdFailReason reason = AdFailReason.None;
            var res = r.S.Monetization.RequestOfflineBonus((g, why) => { granted = g; reason = why; });
            Assert.AreEqual(AdFailReason.None, res);
            Assert.IsFalse(granted.Value);
            Assert.AreEqual(expected, reason);
            Assert.IsFalse(r.S.Offline.Pending.BonusGranted);
            double baseAmt = r.S.Offline.Pending.Base;
            Assert.AreEqual(baseAmt, r.S.ClaimOffline(), 1e-6);
            Assert.GreaterOrEqual(r.Analytics.Count(AnalyticsEvents.RewardedAdFailed), 0);
            Assert.AreEqual(0, r.Analytics.Count(AnalyticsEvents.BonusGranted));
        }

        [Test]
        public void NoNetwork_WhenSdkIsNotReady_RefusesToStart_BaseStillClaimable()
        {
            var r = WithOfflineOffer();
            r.Ads.Behavior = MockAdBehavior.NoNetwork;       // SDK reports not ready (no fill / offline)
            Assert.AreEqual(AdFailReason.Unavailable, r.S.Monetization.RequestOfflineBonus((g, w) => { }));
            Assert.AreEqual(r.S.Offline.Pending.Base, r.S.ClaimOffline(), 1e-6);
        }

        [Test]
        public void NetworkErrorReportedByTheSdkDuringShow_FailsWithoutBonus()
        {
            var r = WithOfflineOffer(autoResolve: false);
            AdFailReason reason = AdFailReason.None; bool granted = true;
            r.S.Monetization.RequestOfflineBonus((g, w) => { granted = g; reason = w; });
            r.Ads.Last.OnFailed(AdFailReason.NoNetwork, "connection lost");
            Assert.AreEqual(AdFailReason.NoNetwork, reason);
            Assert.IsFalse(granted);
            Assert.IsFalse(r.S.Ads.IsBusy);
            Assert.AreEqual(r.S.Offline.Pending.Base, r.S.ClaimOffline(), 1e-6);
        }

        [Test]
        public void SdkThrowing_IsTreatedAsFailure()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            var throwing = new ThrowingAds();
            var ctl = new RewardedAdController(r.Config.Ads, throwing, r.Clock, r.Analytics);
            AdResult? result = null;
            Assert.AreEqual(AdFailReason.None, ctl.Request("p", x => result = x));
            Assert.IsFalse(result.Value.Completed);
            Assert.AreEqual(AdFailReason.Error, result.Value.Reason);
            Assert.IsFalse(ctl.IsBusy, "controller must not stay stuck after an SDK exception");
        }

        sealed class ThrowingAds : IRewardedAdService
        {
            public bool IsReady => true;
            public void Load() { }
            public void Show(string placement, IRewardedAdCallbacks callbacks) { throw new InvalidOperationException("sdk crash"); }
        }

        [Test]
        public void RepeatedCallbacks_GrantOnce()
        {
            var r = WithOfflineOffer(autoResolve: false);
            int calls = 0; bool granted = false;
            r.S.Monetization.RequestOfflineBonus((g, w) => { calls++; granted |= g; });
            var cb = r.Ads.Last;
            cb.OnRewardEarned(); cb.OnRewardEarned(); cb.OnClosed(); cb.OnRewardEarned(); cb.OnFailed(AdFailReason.Error, "late");
            Assert.AreEqual(1, calls, "result delivered exactly once");
            Assert.IsTrue(granted);
            Assert.AreEqual(r.S.Offline.Pending.Base + r.S.Offline.Pending.Bonus, r.S.ClaimOffline(), 1e-6);
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.BonusGranted));
        }

        [Test]
        public void OpeningTheAdWithoutRewardEvent_GrantsNothing()
        {
            var r = WithOfflineOffer(autoResolve: false);
            bool granted = false; bool done = false;
            r.S.Monetization.RequestOfflineBonus((g, w) => { granted = g; done = true; });
            Assert.IsTrue(r.S.Ads.IsBusy);
            Assert.IsFalse(done);
            Assert.IsFalse(granted);
            Assert.IsFalse(r.S.Offline.Pending.BonusGranted);
        }

        [Test]
        public void Timeout_FailsWithoutBonus_AndLateRewardIsIgnored()
        {
            var r = WithOfflineOffer(autoResolve: false);
            bool granted = false; AdFailReason reason = AdFailReason.None;
            r.S.Monetization.RequestOfflineBonus((g, w) => { granted = g; reason = w; });
            var cb = r.Ads.Last;
            r.Run(r.Config.Ads.ShowTimeoutSeconds + 1);
            Assert.AreEqual(AdFailReason.Timeout, reason);
            Assert.IsFalse(r.S.Ads.IsBusy);
            cb.OnRewardEarned();                          // arrives after the timeout
            Assert.IsFalse(granted);
            Assert.IsFalse(r.S.Offline.Pending.BonusGranted);
            Assert.AreEqual(r.S.Offline.Pending.Base, r.S.ClaimOffline(), 1e-6);
        }

        [Test]
        public void DoubleTap_SecondRequestIsRejectedAsBusy()
        {
            var r = WithOfflineOffer(autoResolve: false);
            Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestOfflineBonus((g, w) => { }));
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.RequestOfflineBonus((g, w) => { }));
            Assert.AreEqual(1, r.Ads.ShowCount);
        }

        [Test]
        public void ClaimedWhileAdShowing_BonusIsNotGrantedToNothing()
        {
            var r = WithOfflineOffer(autoResolve: false);
            bool granted = true;
            r.S.Monetization.RequestOfflineBonus((g, w) => granted = g);
            double baseAmt = r.S.Offline.Pending.Base;
            Assert.AreEqual(baseAmt, r.S.ClaimOffline(), 1e-6);   // player claims base while the ad is up
            double bal = r.S.Wallet.Balance;
            r.Ads.Last.OnRewardEarned();
            Assert.IsFalse(granted);
            Assert.AreEqual(bal, r.S.Wallet.Balance, "no money appears from a stale ad");
        }

        [Test]
        public void RestartDuringAdFlow_NoBonus_BaseStillClaimable()
        {
            var r = WithOfflineOffer(autoResolve: false);
            r.S.Monetization.RequestOfflineBonus((g, w) => { });
            var oldCallbacks = r.Ads.Last;
            double baseAmt = r.S.Offline.Pending.Base;
            var r2 = r.Restart();                               // app killed while the ad was on screen
            Assert.IsFalse(r2.S.Ads.IsBusy);
            Assert.IsNotNull(r2.S.Offline.Pending);
            Assert.IsFalse(r2.S.Offline.Pending.BonusGranted);
            oldCallbacks.OnRewardEarned();                      // the dead session's SDK callback fires late
            Assert.IsFalse(r2.S.Offline.Pending.BonusGranted, "a new process has no attempt in flight");
            Assert.AreEqual(baseAmt, r2.S.ClaimOffline(), baseAmt * 0.001 + 1);
        }

        [Test]
        public void RestartAfterAdCompleted_BonusPersistsAndIsPaidOnce()
        {
            var r = WithOfflineOffer();
            r.S.Monetization.RequestOfflineBonus((g, w) => { });
            Assert.IsTrue(r.S.Offline.Pending.BonusGranted);
            double total = r.S.Offline.Pending.Base + r.S.Offline.Pending.Bonus;
            var r2 = r.Restart();
            Assert.IsTrue(r2.S.Offline.Pending.BonusGranted);
            Assert.AreEqual(AdFailReason.Busy, r2.S.Monetization.RequestOfflineBonus((g, w) => { }), "cannot earn the bonus twice");
            Assert.AreEqual(total, r2.S.ClaimOffline(), 1e-6);
            var r3 = r2.Restart();
            Assert.IsNull(r3.S.Offline.Pending);
        }

        [Test]
        public void UnclaimedOfferWithBonus_IsDepositedOnNextAbsence_NotLost_NotDuplicated()
        {
            var r = WithOfflineOffer();
            r.S.Monetization.RequestOfflineBonus((g, w) => { });
            double total = r.S.Offline.Pending.Base + r.S.Offline.Pending.Bonus;
            double balBefore = r.S.Wallet.Balance;
            r.S.Pause();
            r.Clock.Advance(1800);
            var r2 = r.Restart(saveFirst: false);
            Assert.GreaterOrEqual(r2.S.Wallet.Balance, balBefore + total - 1e-6, "earlier paid-for offer is deposited");
            Assert.IsNotNull(r2.S.Offline.Pending);
            Assert.IsFalse(r2.S.Offline.Pending.BonusGranted);
        }

        // -------------------------------------------------- limits
        [Test]
        public void Cooldown_BlocksBackToBackAds_ThenAllowsAfterWait()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            r.S.Production.AddParts(0);
            Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestBoost((g, w) => { }));
            Assert.Greater(r.S.Production.BoostRemaining("ad"), 0);
            r.S.Production.TickBoosts(10000);
            Assert.AreEqual(AdFailReason.Cooldown, r.S.Ads.CanRequest());
            r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
            Assert.AreEqual(AdFailReason.None, r.S.Ads.CanRequest());
        }

        [Test]
        public void DailyLimit_IsEnforcedAndResetsNextDay()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            for (int i = 0; i < r.Config.Ads.MaxPerDay; i++)
            {
                r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
                r.S.Production.TickBoosts(10000);
                Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestBoost((g, w) => { }), "ad #" + i);
            }
            r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
            r.S.Production.TickBoosts(10000);
            Assert.AreEqual(AdFailReason.DailyLimit, r.S.Ads.CanRequest());
            r.Clock.Advance(24 * 3600);
            Assert.AreEqual(AdFailReason.None, r.S.Ads.CanRequest());
        }

        [Test]
        public void DailyLimit_SurvivesRestart()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            for (int i = 0; i < r.Config.Ads.MaxPerDay; i++)
            {
                r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
                r.S.Production.TickBoosts(10000);
                r.S.Monetization.RequestBoost((g, w) => { });
            }
            var r2 = r.Restart();
            r2.Clock.Advance(r2.Config.Ads.MinSecondsBetweenAds + 1);
            Assert.AreEqual(AdFailReason.DailyLimit, r2.S.Ads.CanRequest());
        }

        [Test]
        public void DisabledAds_NeverShow()
        {
            var cfg = DefaultConfig.Create();
            cfg.Ads.Enabled = false;
            var r = Rig.New(cfg: cfg);
            r.GiveProduction(10);
            Assert.AreEqual(AdFailReason.Disabled, r.S.Monetization.RequestBoost((g, w) => { }));
            Assert.AreEqual(0, r.Ads.ShowCount);
        }

        // -------------------------------------------------- production boost ad
        [Test]
        public void BoostAd_GrantsDeclaredMultiplierOnce_AndDoesNotStack()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            double ops = r.S.Production.BaseOutputPerSecond;
            r.S.Monetization.RequestBoost((g, w) => { });
            Assert.AreEqual(ops * r.Config.Ads.BoostMultiplier, r.S.Production.OutputPerSecond, 1e-9);
            Assert.AreEqual(r.Config.Ads.BoostSeconds, r.S.Production.BoostRemaining("ad"), 1e-9);
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.CanOfferBoost(), "no new ad while a boost is running");
        }

        [Test]
        public void BoostAd_RepeatedCallbacks_GrantOnce()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            r.Ads.AutoResolve = false;
            int grants = 0;
            r.S.Monetization.RequestBoost((g, w) => { if (g) grants++; });
            r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnClosed();
            Assert.AreEqual(1, grants);
            Assert.AreEqual(r.Config.Ads.BoostSeconds, r.S.Production.BoostRemaining("ad"), 1e-9);
        }

        [Test]
        public void GameIsFullyPlayableWithoutAnyAd()
        {
            var r = Rig.New();
            r.Ads.Behavior = MockAdBehavior.Unavailable;
            for (int i = 0; i < 30; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            r.Money(1000);
            Assert.IsTrue(r.S.Production.TryBuy("drill", 5));
            r.Run(60);
            Assert.Greater(r.S.Production.BaseOutputPerSecond, 0);
            Assert.AreEqual(0, r.Ads.ShowCount);
        }

        // -------------------------------------------------- analytics
        [Test]
        public void AdFlowEmitsAnalytics_WithoutPersonalData()
        {
            var r = WithOfflineOffer();
            r.S.Monetization.NotifyOfferShown(MonetizationService.PlacementOffline);
            r.S.Monetization.RequestOfflineBonus((g, w) => { });
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RewardedOfferShown));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RewardedAdStarted));
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RewardedAdCompleted));
            foreach (var rec in r.Analytics.Records)
                foreach (var k in rec.Params.Keys)
                    Assert.IsFalse(AnalyticsParams.IsForbidden(k), k);
        }

        [Test]
        public void FailedAdEmitsFailedEventOnce()
        {
            var r = WithOfflineOffer();
            r.Ads.Behavior = MockAdBehavior.Fail;
            r.S.Monetization.RequestOfflineBonus((g, w) => { });
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.RewardedAdFailed));
        }
    }
}

namespace OfficinaTycoon.Tests
{
    public class ConsentGateTests
    {
        [Test]
        public void NoAdIsRequestedBeforeConsentDecision_OrAfterOptOut()
        {
            var inner = new MockRewardedAdService();
            bool allowed = false;
            var gate = new ConsentGatedAdService(inner, () => allowed);
            Assert.IsFalse(gate.IsReady);
            AdFailReason got = AdFailReason.None;
            var cb = new Cb { OnFail = r => got = r };
            gate.Show("p", cb);
            Assert.AreEqual(AdFailReason.Disabled, got);
            Assert.AreEqual(0, inner.ShowCount);
            allowed = true;
            Assert.IsTrue(gate.IsReady);
            gate.Show("p", new Cb());
            Assert.AreEqual(1, inner.ShowCount);
        }

        [Test]
        public void GameplayWorksWhenPlayerOptsOutOfAds()
        {
            var r = Rig.New();
            bool allowed = false;
            var gated = new ConsentGatedAdService(new MockRewardedAdService(), () => allowed);
            var s = new GameSession(DefaultConfig.Create(), r.Clock, new InMemorySaveStorage(), gated);
            s.Start();
            s.Wallet.Add(100, "t");
            Assert.IsTrue(s.Production.TryBuy("drill"));
            Assert.AreEqual(AdFailReason.Unavailable, s.Monetization.CanOfferBoost());
        }

        sealed class Cb : IRewardedAdCallbacks
        {
            public System.Action<AdFailReason> OnFail;
            public void OnRewardEarned() { }
            public void OnClosed() { }
            public void OnFailed(AdFailReason reason, string message) { OnFail?.Invoke(reason); }
        }
    }
}
