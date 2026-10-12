using System;
using UnityEngine;
using UnityEngine.UI;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// Development ad service: shows an on-screen "fake ad" with a countdown. The reward is only reported when the
    /// countdown finishes; closing early reports a close WITHOUT reward. Behaviour can be forced from the dev menu.
    /// No SDK, no network, no revenue.
    /// </summary>
    public sealed class UnityMockAdService : IRewardedAdService
    {
        public MockAdBehavior Behavior = MockAdBehavior.Complete;
        public Action<IRewardedAdCallbacks, MockAdBehavior> Presenter;   // supplied by the UI

        public bool IsReady => Behavior != MockAdBehavior.Unavailable && Behavior != MockAdBehavior.NoNetwork;
        public void Load() { }

        public void Show(string placement, IRewardedAdCallbacks callbacks)
        {
            switch (Behavior)
            {
                case MockAdBehavior.Fail: callbacks.OnFailed(AdFailReason.Error, "mock error"); return;
                case MockAdBehavior.Unavailable: callbacks.OnFailed(AdFailReason.Unavailable, "mock unavailable"); return;
                case MockAdBehavior.NoNetwork: callbacks.OnFailed(AdFailReason.NoNetwork, "mock offline"); return;
                case MockAdBehavior.Hang: return;    // never answers: exercises the timeout path
            }
            if (Presenter == null) { callbacks.OnFailed(AdFailReason.Error, "no presenter"); return; }
            Presenter(callbacks, Behavior);
        }
    }

    /// <summary>Ad unit identifiers. Test IDs are Google's public sample IDs; production IDs MUST be configured by the owner.</summary>
    public static class AdUnitIds
    {
        // Official AdMob sample rewarded ad units (safe for development, they never pay out).
        public const string AndroidTest = "ca-app-pub-3940256099942544/5224354917";
        public const string IosTest = "ca-app-pub-3940256099942544/1712485313";
        // TODO(owner): production ad unit ids from your AdMob account. Left empty on purpose.
        public const string AndroidProduction = "";
        public const string IosProduction = "";

        public static string Current()
        {
#if UNITY_ANDROID
            return Debug.isDebugBuild || string.IsNullOrEmpty(AndroidProduction) ? AndroidTest : AndroidProduction;
#elif UNITY_IOS
            return Debug.isDebugBuild || string.IsNullOrEmpty(IosProduction) ? IosTest : IosProduction;
#else
            return AndroidTest;
#endif
        }
    }

#if OFFICINA_ADMOB
    // ---------------------------------------------------------------------------------------------------
    // UNVERIFIED ADAPTER. It was written against the Google Mobile Ads Unity plugin (v8+ API) but has NOT been
    // compiled or run: the Unity Editor and the plugin were not available when it was authored.
    // To enable: import the plugin, add scripting define OFFICINA_ADMOB, set the AdMob App ID in
    // Assets/GoogleMobileAds/Settings, then test on a device with the test ad units above.
    // ---------------------------------------------------------------------------------------------------
    public sealed class AdMobRewardedAdService : IRewardedAdService
    {
        GoogleMobileAds.Api.RewardedAd _ad;
        bool _loading;
        bool _initialised;

        public AdMobRewardedAdService()
        {
            GoogleMobileAds.Api.MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // Exclude gambling-related categories where the network supports it (configure also in the AdMob console).
            var cfg = new GoogleMobileAds.Api.RequestConfiguration
            {
                TagForChildDirectedTreatment = GoogleMobileAds.Api.TagForChildDirectedTreatment.Unspecified
            };
            GoogleMobileAds.Api.MobileAds.SetRequestConfiguration(cfg);
            GoogleMobileAds.Api.MobileAds.Initialize(status => { _initialised = true; Load(); });
        }

        public bool IsReady => _initialised && _ad != null && _ad.CanShowAd();

        public void Load()
        {
            if (!_initialised || _loading || (_ad != null && _ad.CanShowAd())) return;
            _loading = true;
            GoogleMobileAds.Api.RewardedAd.Load(AdUnitIds.Current(), new GoogleMobileAds.Api.AdRequest(), (ad, error) =>
            {
                _loading = false;
                if (error != null || ad == null) { _ad = null; return; }
                _ad = ad;
            });
        }

        public void Show(string placement, IRewardedAdCallbacks callbacks)
        {
            if (!IsReady) { callbacks.OnFailed(AdFailReason.Unavailable, "not loaded"); Load(); return; }
            var ad = _ad;
            _ad = null;
            ad.OnAdFullScreenContentClosed += () => { callbacks.OnClosed(); Load(); };
            ad.OnAdFullScreenContentFailed += err => { callbacks.OnFailed(AdFailReason.Error, err != null ? err.GetMessage() : "show failed"); Load(); };
            ad.Show(reward => callbacks.OnRewardEarned());
        }
    }
#endif
}
