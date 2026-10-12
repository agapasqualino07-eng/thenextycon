using System;

namespace OfficinaTycoon
{
    /// <summary>
    /// Connects rewarded ads to the two optional rewards: the offline bonus and the production booster.
    /// The base offline reward and all normal gameplay are independent of this class.
    /// </summary>
    public sealed class MonetizationService
    {
        public const string PlacementOffline = "offline_bonus";
        public const string PlacementBoost = "production_boost";
        public const string PlacementDaily = "daily_bonus";

        readonly GameConfig _cfg;
        readonly RewardedAdController _ads;
        readonly OfflineRewardSystem _offline;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;
        readonly IAnalyticsService _analytics;

        public int BoostSeq { get; private set; }

        DailyRewardSystem _daily;

        public MonetizationService(GameConfig cfg, RewardedAdController ads, OfflineRewardSystem offline, ProductionSystem production,
                                   ClaimLedger ledger, IAnalyticsService analytics)
        {
            _cfg = cfg; _ads = ads; _offline = offline; _production = production; _ledger = ledger; _analytics = analytics;
        }

        public void AttachDaily(DailyRewardSystem daily) { _daily = daily; }

        public AdFailReason CanOfferDailyBonus()
        {
            var p = _daily?.Pending;
            if (p == null || p.BonusGranted || _cfg.Daily.AdBonusMultiplier <= 0) return AdFailReason.Busy;
            return _ads.CanRequest();
        }

        /// <summary>Optional ad that doubles today's gift. onDone(true) only after a confirmed completion and a first-time grant.</summary>
        public AdFailReason RequestDailyBonus(Action<bool, AdFailReason> onDone)
        {
            var p = _daily?.Pending;
            if (p == null || p.BonusGranted) return AdFailReason.Busy;
            int id = p.Id;
            return _ads.Request(PlacementDaily, r =>
            {
                bool granted = r.Completed && _daily.Pending != null && _daily.Pending.Id == id && _daily.GrantBonus();
                if (granted) _analytics.Track(AnalyticsEvents.BonusGranted, AnalyticsParams.Of("type", "daily_bonus"));
                onDone?.Invoke(granted, r.Reason);
            });
        }

        public void Restore(int boostSeq) { BoostSeq = Math.Max(0, boostSeq); }

        public void NotifyOfferShown(string placement) => _analytics.Track(AnalyticsEvents.RewardedOfferShown, AnalyticsParams.Of("placement", placement));

        public AdFailReason CanOfferOfflineBonus()
        {
            var p = _offline.Pending;
            if (p == null || p.BonusGranted) return AdFailReason.Busy;
            return _ads.CanRequest();
        }

        public AdFailReason CanOfferBoost()
        {
            if (_production.BoostRemaining("ad") > _cfg.Ads.BoostRequestWhenRemainingBelow) return AdFailReason.Busy;
            return _ads.CanRequest();
        }

        /// <summary>Starts the optional offline-bonus ad. onDone(true) only after a confirmed completion AND a first-time grant.</summary>
        public AdFailReason RequestOfflineBonus(Action<bool, AdFailReason> onDone)
        {
            var pending = _offline.Pending;
            if (pending == null || pending.BonusGranted) return AdFailReason.Busy;
            int offerId = pending.Id;
            return _ads.Request(PlacementOffline, r =>
            {
                bool granted = false;
                // The offer may have been claimed while the ad was showing: only grant if it is still the same pending offer.
                if (r.Completed && _offline.Pending != null && _offline.Pending.Id == offerId)
                    granted = _offline.GrantBonus();
                if (granted) _analytics.Track(AnalyticsEvents.BonusGranted, AnalyticsParams.Of("type", "offline_bonus"));
                onDone?.Invoke(granted, r.Reason);
            });
        }

        public AdFailReason RequestBoost(Action<bool, AdFailReason> onDone)
        {
            if (CanOfferBoost() == AdFailReason.Busy && _production.BoostRemaining("ad") > _cfg.Ads.BoostRequestWhenRemainingBelow) return AdFailReason.Busy;
            return _ads.Request(PlacementBoost, r =>
            {
                bool granted = false;
                if (r.Completed && _ledger.TryClaim("ad_boost:" + BoostSeq))
                {
                    BoostSeq++;
                    _production.AddBoost("ad", _cfg.Ads.BoostMultiplier, _cfg.Ads.BoostSeconds);
                    granted = true;
                    _analytics.Track(AnalyticsEvents.BonusGranted, AnalyticsParams.Of("type", "production_boost"));
                }
                onDone?.Invoke(granted, r.Reason);
            });
        }
    }
}
