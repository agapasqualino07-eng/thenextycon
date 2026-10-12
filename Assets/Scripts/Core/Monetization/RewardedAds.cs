using System;

namespace OfficinaTycoon
{
    public enum AdFailReason { None, Disabled, Unavailable, NoNetwork, Error, ClosedEarly, Timeout, Cooldown, DailyLimit, Busy }

    public struct AdResult
    {
        public bool Completed;
        public AdFailReason Reason;
        public string Placement;
    }

    /// <summary>Callbacks an ad SDK adapter invokes. They may be invoked several times or late; the controller filters.</summary>
    public interface IRewardedAdCallbacks
    {
        void OnRewardEarned();
        void OnClosed();
        void OnFailed(AdFailReason reason, string message);
    }

    /// <summary>Abstraction over the ad network (AdMob adapter in the Unity layer, or the mock).</summary>
    public interface IRewardedAdService
    {
        bool IsReady { get; }
        void Load();
        void Show(string placement, IRewardedAdCallbacks callbacks);
    }

    /// <summary>
    /// Wraps the ad service with the rules that make the reward flow robust:
    ///  - one ad in flight at a time (double taps -> Busy),
    ///  - reward only on a confirmed OnRewardEarned for the CURRENT attempt (never on open),
    ///  - the result is delivered exactly once (repeated/late callbacks are ignored),
    ///  - timeout, cooldown between ads and a daily cap,
    ///  - nothing in flight survives a restart: a new controller has no attempt, so no reward can arrive.
    /// </summary>
    public sealed class RewardedAdController
    {
        sealed class Attempt : IRewardedAdCallbacks
        {
            public RewardedAdController Owner;
            public string Placement;
            public Action<AdResult> Done;
            public bool Earned, Resolved;
            public double Age;

            public void OnRewardEarned() { if (Resolved) return; Earned = true; Owner.Resolve(this, true, AdFailReason.None); }
            public void OnClosed() { if (Resolved) return; Owner.Resolve(this, Earned, Earned ? AdFailReason.None : AdFailReason.ClosedEarly); }
            public void OnFailed(AdFailReason reason, string message)
            {
                if (Resolved) return;
                Owner._analytics.Track(AnalyticsEvents.RewardedAdFailed, AnalyticsParams.Of("placement", Placement, "reason", reason.ToString()));
                Owner.Resolve(this, false, reason == AdFailReason.None ? AdFailReason.Error : reason, track: false);
            }
        }

        readonly AdsConfig _cfg;
        readonly IRewardedAdService _service;
        readonly IClock _clock;
        readonly IAnalyticsService _analytics;
        Attempt _current;

        public string DayKey { get; private set; } = "";
        public int CountToday { get; private set; }
        public long LastAdTicks { get; private set; }

        public RewardedAdController(AdsConfig cfg, IRewardedAdService service, IClock clock, IAnalyticsService analytics)
        {
            _cfg = cfg; _service = service; _clock = clock; _analytics = analytics;
        }

        public bool IsBusy => _current != null;

        string TodayKey() => _clock.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whether an ad could be started right now (used to enable/disable the button). None = yes.</summary>
        public AdFailReason CanRequest()
        {
            if (!_cfg.Enabled) return AdFailReason.Disabled;
            if (_current != null) return AdFailReason.Busy;
            int today = DayKey == TodayKey() ? CountToday : 0;
            if (today >= _cfg.MaxPerDay) return AdFailReason.DailyLimit;
            if (LastAdTicks > 0)
            {
                double since = (_clock.UtcNow.Ticks - LastAdTicks) / (double)TimeSpan.TicksPerSecond;
                if (since < _cfg.MinSecondsBetweenAds) return AdFailReason.Cooldown;   // also true when the clock went backwards
            }
            if (!_service.IsReady) return AdFailReason.Unavailable;
            return AdFailReason.None;
        }

        /// <summary>Starts an ad. Returns None if started (result arrives via onDone exactly once), else the reason it was refused.</summary>
        public AdFailReason Request(string placement, Action<AdResult> onDone)
        {
            var can = CanRequest();
            if (can != AdFailReason.None) return can;
            var a = new Attempt { Owner = this, Placement = placement, Done = onDone };
            _current = a;
            _analytics.Track(AnalyticsEvents.RewardedAdStarted, AnalyticsParams.Of("placement", placement));
            try { _service.Show(placement, a); }
            catch (Exception ex)
            {
                // SDK threw synchronously: treat as failure, never as a reward.
                a.OnFailed(AdFailReason.Error, ex.Message);
            }
            return AdFailReason.None;
        }

        /// <summary>Advances the timeout of the attempt in flight.</summary>
        public void Tick(double dt)
        {
            var a = _current;
            if (a == null) return;
            a.Age += dt;
            if (a.Age >= _cfg.ShowTimeoutSeconds) Resolve(a, false, AdFailReason.Timeout);
        }

        void Resolve(Attempt a, bool completed, AdFailReason reason, bool track = true)
        {
            if (a.Resolved) return;
            a.Resolved = true;
            if (ReferenceEquals(_current, a)) _current = null;
            if (completed)
            {
                string today = TodayKey();
                if (DayKey != today) { DayKey = today; CountToday = 0; }
                CountToday++;
                LastAdTicks = _clock.UtcNow.Ticks;
                _analytics.Track(AnalyticsEvents.RewardedAdCompleted, AnalyticsParams.Of("placement", a.Placement));
            }
            else if (track)
            {
                _analytics.Track(AnalyticsEvents.RewardedAdFailed, AnalyticsParams.Of("placement", a.Placement, "reason", reason.ToString()));
            }
            a.Done?.Invoke(new AdResult { Completed = completed, Reason = reason, Placement = a.Placement });
        }

        public AdSave Capture(int boostSeq) => new AdSave { DayKey = DayKey, CountToday = CountToday, LastAdTicks = LastAdTicks, BoostSeq = boostSeq };

        public void Restore(AdSave s)
        {
            _current = null;
            if (s == null) return;
            DayKey = s.DayKey ?? "";
            CountToday = Math.Max(0, s.CountToday);
            LastAdTicks = Math.Max(0, s.LastAdTicks);
        }
    }

    public enum MockAdBehavior { Complete, Fail, CloseEarly, Unavailable, NoNetwork, Hang }

    /// <summary>
    /// Development/test ad service: no SDK, no network. In AutoResolve mode Show() answers immediately
    /// according to Behavior; otherwise tests drive the callbacks manually (including repeated ones).
    /// </summary>
    public sealed class MockRewardedAdService : IRewardedAdService
    {
        public MockAdBehavior Behavior = MockAdBehavior.Complete;
        public bool AutoResolve = true;
        public int ShowCount { get; private set; }
        public IRewardedAdCallbacks Last { get; private set; }

        public bool IsReady => Behavior != MockAdBehavior.Unavailable && Behavior != MockAdBehavior.NoNetwork;
        public void Load() { }

        public void Show(string placement, IRewardedAdCallbacks callbacks)
        {
            ShowCount++;
            Last = callbacks;
            if (!AutoResolve) return;
            switch (Behavior)
            {
                case MockAdBehavior.Complete: callbacks.OnRewardEarned(); callbacks.OnClosed(); break;
                case MockAdBehavior.Fail: callbacks.OnFailed(AdFailReason.Error, "mock error"); break;
                case MockAdBehavior.CloseEarly: callbacks.OnClosed(); break;
                case MockAdBehavior.Unavailable: callbacks.OnFailed(AdFailReason.Unavailable, "mock unavailable"); break;
                case MockAdBehavior.NoNetwork: callbacks.OnFailed(AdFailReason.NoNetwork, "mock offline"); break;
                case MockAdBehavior.Hang: break;
            }
        }
    }
}

namespace OfficinaTycoon
{
    /// <summary>
    /// Wraps an ad service so that NO ad is requested until the player has seen the privacy/consent screen,
    /// and never if they opted out. Real network consent (e.g. Google UMP) must still be implemented
    /// in the platform adapter; this only guarantees the game never asks an SDK for an ad before consent state is known.
    /// </summary>
    public sealed class ConsentGatedAdService : IRewardedAdService
    {
        readonly IRewardedAdService _inner;
        readonly System.Func<bool> _allowed;

        public ConsentGatedAdService(IRewardedAdService inner, System.Func<bool> allowed) { _inner = inner; _allowed = allowed; }

        public bool IsReady => _allowed() && _inner.IsReady;
        public void Load() { if (_allowed()) _inner.Load(); }

        public void Show(string placement, IRewardedAdCallbacks callbacks)
        {
            if (!_allowed()) { callbacks.OnFailed(AdFailReason.Disabled, "consent not given"); return; }
            _inner.Show(placement, callbacks);
        }
    }
}
