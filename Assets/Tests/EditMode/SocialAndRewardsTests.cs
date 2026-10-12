using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class SocialAndRewardsTests
    {
        // ------------------------------------------------------------ invite code
        [Test]
        public void Code_IsGeneratedValidAndStableAcrossRestarts()
        {
            var r = Rig.New();
            string code = r.S.Social.Code;
            Assert.IsTrue(SocialSystem.IsValidCode(code), code);
            var r2 = r.Restart();
            Assert.AreEqual(code, r2.S.Social.Code);
        }

        [Test]
        public void Referral_ValidCode_GivesWelcomeGiftOnce()
        {
            var r = Rig.New();
            double bal = r.S.Wallet.Balance;
            Assert.AreEqual(ReferralResult.Ok, r.S.ApplyReferral("abcd23"));     // case-insensitive
            Assert.AreEqual(bal + r.Config.Social.WelcomeMin, r.S.Wallet.Balance, 1e-6);
            Assert.AreEqual(r.Config.Social.WelcomeCrates, r.S.Crates.Available);
            Assert.AreEqual(ReferralResult.AlreadyUsed, r.S.ApplyReferral("XYZW23"));
            var r2 = r.Restart();
            Assert.AreEqual(ReferralResult.AlreadyUsed, r2.S.ApplyReferral("XYZW45"), "one welcome gift per device, also after restart");
            Assert.AreEqual(1, r.Analytics.Count(AnalyticsEvents.ReferralWelcomeClaimed));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABC")]
        [TestCase("ABCDEFG")]
        [TestCase("ABCD0O")]               // 0 and O are not in the alphabet
        [TestCase("<scrip")]
        [TestCase("AB CD2")]
        public void Referral_InvalidCodes_Rejected_NoReward(string code)
        {
            var r = Rig.New();
            Assert.AreEqual(ReferralResult.InvalidCode, r.S.ApplyReferral(code));
            Assert.AreEqual(0, r.S.Wallet.Balance);
            Assert.AreEqual(ReferralResult.Ok, r.S.ApplyReferral("ABCD23"), "a later valid code still works");
        }

        [Test]
        public void Referral_OwnCodeRejected()
        {
            var r = Rig.New();
            Assert.AreEqual(ReferralResult.OwnCode, r.S.ApplyReferral(r.S.Social.Code));
            Assert.AreEqual(0, r.S.Wallet.Balance);
        }

        [Test]
        public void Share_RewardOncePerDay_ClockBackSafe()
        {
            var r = Rig.New();
            Assert.IsTrue(r.S.Social.CanClaimShare);
            Assert.IsTrue(r.S.ClaimShareReward());
            Assert.AreEqual(r.Config.Social.ShareCrates, r.S.Crates.Available);
            Assert.IsFalse(r.S.ClaimShareReward());
            r.Clock.Advance(-2 * 24 * 3600);
            Assert.IsFalse(r.S.ClaimShareReward(), "moving the clock back gives nothing");
            r.Clock.Advance(3 * 24 * 3600);
            Assert.IsTrue(r.S.ClaimShareReward());
            var r2 = r.Restart();
            Assert.IsFalse(r2.S.ClaimShareReward(), "state persisted");
            Assert.AreEqual(2, r2.S.Social.Shares);
        }

        [Test]
        public void Social_RestoreSanitisesBadCodes()
        {
            var r = Rig.New(start: false);
            r.S.Social.Restore(new SocialSave { Code = "<b>x</b>", ReferredBy = "!!", Shares = -4 });
            Assert.AreEqual("", r.S.Social.Code);
            r.S.Social.EnsureCode();
            Assert.IsTrue(SocialSystem.IsValidCode(r.S.Social.Code));
            Assert.AreEqual(0, r.S.Social.Shares);
        }

        // ------------------------------------------------------------ video for a free crate
        [Test]
        public void CrateAd_GrantsCrateOnlyOnCompletion_CappedPerDay_ResetsNextDay()
        {
            var r = Rig.New();
            r.GiveProduction(10);
            int cap = r.Config.Ads.CrateAdsPerDay;
            r.Ads.Behavior = MockAdBehavior.CloseEarly;
            r.S.Monetization.RequestCrate((g, w) => { });
            Assert.AreEqual(0, r.S.Crates.Available, "closed early: no crate");
            r.Ads.Behavior = MockAdBehavior.Complete;
            for (int i = 0; i < cap; i++)
            {
                r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
                Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestCrate((g, w) => Assert.IsTrue(g)), "video " + i);
            }
            Assert.AreEqual(cap, r.S.Crates.Available);
            r.Clock.Advance(r.Config.Ads.MinSecondsBetweenAds + 1);
            Assert.AreEqual(AdFailReason.DailyLimit, r.S.Monetization.CanOfferCrate());
            Assert.AreEqual(0, r.S.Monetization.CrateAdsLeftToday);
            var r2 = r.Restart();
            Assert.AreEqual(AdFailReason.DailyLimit, r2.S.Monetization.CanOfferCrate(), "cap persisted");
            r2.Clock.Advance(24 * 3600);
            Assert.AreEqual(cap, r2.S.Monetization.CrateAdsLeftToday);
        }

        [Test]
        public void CrateAd_RepeatedCallbacks_GrantOneCrate()
        {
            var r = Rig.New();
            r.Ads.AutoResolve = false;
            r.S.Monetization.RequestCrate((g, w) => { });
            r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnRewardEarned(); r.Ads.Last.OnClosed();
            Assert.AreEqual(1, r.S.Crates.Available);
        }

        [Test]
        public void CrateAd_NotOfferedWhenStorageFull()
        {
            var r = Rig.New();
            r.S.Crates.Grant(r.Config.Crate.MaxStored, "t");
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.CanOfferCrate());
        }

        // ------------------------------------------------------------ double order reward with a video
        static Rig CompletedTutorialOrder()
        {
            var r = Rig.New();
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            Assert.IsTrue(r.S.Orders.IsCompleted("t1"));
            return r;
        }

        [Test]
        public void OrderDouble_PaysTwiceCurrencyOnce()
        {
            var r = CompletedTutorialOrder();
            double bal = r.S.Wallet.Balance;
            bool granted = false;
            Assert.AreEqual(AdFailReason.None, r.S.Monetization.RequestOrderDouble("t1", (g, w) => granted = g));
            Assert.IsTrue(granted);
            Assert.AreEqual(bal + 15 * r.Config.Ads.OrderAdMultiplier, r.S.Wallet.Balance, 1e-6);
            Assert.IsFalse(r.S.Orders.TryClaim("t1"));
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.CanOfferOrderDouble("t1"));
        }

        [Test]
        public void OrderDouble_FailedVideo_OrderStillClaimableNormally()
        {
            var r = CompletedTutorialOrder();
            r.Ads.Behavior = MockAdBehavior.Fail;
            bool granted = true;
            r.S.Monetization.RequestOrderDouble("t1", (g, w) => granted = g);
            Assert.IsFalse(granted);
            double bal = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            Assert.AreEqual(bal + 15, r.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void OrderDouble_ClaimedDuringVideo_NoSecondPayment()
        {
            var r = CompletedTutorialOrder();
            r.Ads.AutoResolve = false;
            r.S.Monetization.RequestOrderDouble("t1", (g, w) => { });
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            double bal = r.S.Wallet.Balance;
            r.Ads.Last.OnRewardEarned();
            Assert.AreEqual(bal, r.S.Wallet.Balance, "the late video does not pay the order again");
        }

        [Test]
        public void OrderDouble_NotOfferedForIncompleteOrUnknownOrders()
        {
            var r = Rig.New();
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.CanOfferOrderDouble("t1"));
            Assert.AreEqual(AdFailReason.Busy, r.S.Monetization.CanOfferOrderDouble("ghost"));
        }

        // ------------------------------------------------------------ compatibility with older saves
        [Test]
        public void SaveFromPreviousVersion_LoadsWithProgressAndNewFeatures()
        {
            // Build a save, then strip everything added after 0.2.0 (Daily, Social, crate-ad counters, ReducedEffects).
            var a = Rig.New();
            a.GiveProduction(12);
            a.S.Production.TryBuyUpgrade("up_bench");
            var tree = (Dictionary<string, object>)MiniJson.Parse(JsonMapper.ToJson(a.S.Capture()));
            tree.Remove("Daily"); tree.Remove("Social");
            var ads = (Dictionary<string, object>)tree["Ads"];
            ads.Remove("CrateAdDay"); ads.Remove("CrateAdCount"); ads.Remove("CrateAdSeq");
            ((Dictionary<string, object>)tree["Settings"]).Remove("ReducedEffects");
            string payload = MiniJson.Serialize(tree);
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = MiniJson.Serialize(new Dictionary<string, object> { ["v"] = 1L, ["sum"] = SaveManager.Checksum(payload), ["payload"] = payload });
            var s = new GameSession(DefaultConfig.Create(), a.Clock, st, new MockRewardedAdService());
            s.Start();
            Assert.AreEqual(LoadStatus.Loaded, s.LastLoad.Status);
            Assert.AreEqual(12, s.Production.Level("drill"), "old progress kept");
            Assert.IsTrue(s.Production.HasUpgrade("up_bench"));
            Assert.IsNotNull(s.Daily.Pending, "new daily gift available");
            Assert.IsTrue(SocialSystem.IsValidCode(s.Social.Code), "invite code created");
            Assert.IsFalse(s.Settings.ReducedEffects);
        }
    }
}
