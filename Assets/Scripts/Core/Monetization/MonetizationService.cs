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
        public const string PlacementCrate = "free_crate";
        public const string PlacementOrder = "order_double";

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

        FreePartsCrate _crates;
        OrderSystem _orders;
        IClock _clock;
        public string CrateAdDay { get; private set; } = "";
        public int CrateAdCount { get; private set; }
        public int CrateAdSeq { get; private set; }

        public void AttachRewards(FreePartsCrate crates, OrderSystem orders, IClock clock) { _crates = crates; _orders = orders; _clock = clock; }

        string Today => _clock.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        public int CrateAdsLeftToday => Math.Max(0, _cfg.Ads.CrateAdsPerDay - (CrateAdDay == Today ? CrateAdCount : 0));

        public AdFailReason CanOfferCrate()
        {
            if (_crates == null || CrateAdsLeftToday <= 0) return AdFailReason.DailyLimit;
            if (_crates.Available >= _crates.MaxStored) return AdFailReason.Busy;
            return _ads.CanRequest();
        }

        /// <summary>"Watch a video for a free crate" (capped per day). The crate is granted only after a confirmed completion.</summary>
        public AdFailReason RequestCrate(Action<bool, AdFailReason> onDone)
        {
            var can = CanOfferCrate();
            if (can != AdFailReason.None) return can;
            return _ads.Request(PlacementCrate, r =>
            {
                bool granted = false;
                if (r.Completed && CrateAdsLeftToday > 0 && _ledger.TryClaim("ad_crate:" + CrateAdSeq))
                {
                    CrateAdSeq++;
                    if (CrateAdDay != Today) { CrateAdDay = Today; CrateAdCount = 0; }
                    CrateAdCount++;
                    _crates.Grant(1, "ad_crate");
                    granted = true;
                    _analytics.Track(AnalyticsEvents.BonusGranted, AnalyticsParams.Of("type", "free_crate"));
                }
                onDone?.Invoke(granted, r.Reason);
            });
        }

        public AdFailReason CanOfferOrderDouble(string orderId)
        {
            if (_orders == null || !_orders.IsCompleted(orderId)) return AdFailReason.Busy;
            return _ads.CanRequest();
        }

        /// <summary>Claims a completed order with doubled currency after a confirmed video. A failed video leaves the order claimable as usual.</summary>
        public AdFailReason RequestOrderDouble(string orderId, Action<bool, AdFailReason> onDone)
        {
            var can = CanOfferOrderDouble(orderId);
            if (can != AdFailReason.None) return can;
            return _ads.Request(PlacementOrder, r =>
            {
                bool granted = r.Completed && _orders.IsCompleted(orderId) && _orders.TryClaim(orderId, _cfg.Ads.OrderAdMultiplier);
                if (granted) _analytics.Track(AnalyticsEvents.BonusGranted, AnalyticsParams.Of("type", "order_double"));
                onDone?.Invoke(granted, r.Reason);
            });
        }

        public void CaptureInto(AdSave s) { s.CrateAdDay = CrateAdDay; s.CrateAdCount = CrateAdCount; s.CrateAdSeq = CrateAdSeq; }
        public void RestoreFrom(AdSave s)
        {
            CrateAdDay = s?.CrateAdDay ?? ""; CrateAdCount = Math.Max(0, s?.CrateAdCount ?? 0); CrateAdSeq = Math.Max(0, s?.CrateAdSeq ?? 0);
        }

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
