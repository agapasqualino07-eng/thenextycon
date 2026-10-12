using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>
    /// Composition root of the game logic (a plain object, not a singleton). It builds all systems from one
    /// GameConfig, wires them with explicit dependencies, drives the single update loop, and owns
    /// load/save. The Unity layer only calls Update/Pause/Resume and reads state for the UI.
    /// </summary>
    public sealed class GameSession
    {
        public const double MaxFrameSeconds = 5.0;

        public readonly GameConfig Config;
        public readonly IClock Clock;
        public readonly GameSignals Signals = new GameSignals();
        public readonly IAnalyticsService Analytics;
        public readonly ClaimLedger Ledger = new ClaimLedger();
        public readonly XorShiftRandom Rng;
        public readonly Wallet Wallet;
        public readonly ProductionSystem Production;
        public readonly ProgressionSystem Progression;
        public readonly OrderSystem Orders;
        public readonly ExperimentalMachineEvent Experimental;
        public readonly ProductionSurgeEvent Surge;
        public readonly FreePartsCrate Crates;
        public readonly OfflineRewardSystem Offline;
        public readonly RewardedAdController Ads;
        public readonly MonetizationService Monetization;
        public readonly SaveManager Saves;
        public readonly FrenzySystem Frenzy;
        public readonly DailyRewardSystem Daily;

        public SettingsSave Settings = new SettingsSave();
        public StatsSave Stats = new StatsSave();
        public LoadResult LastLoad { get; private set; }
        public bool Started { get; private set; }

        double _autosave;
        double _dailyCheck;
        double _sessionSeconds;
        bool _tutorialReported;
        readonly IRandom _externalRng;

        /// <param name="rng">Optional injected RNG (tests). If null a persisted xorshift RNG is used.</param>
        public GameSession(GameConfig config, IClock clock, ISaveStorage storage, IRewardedAdService adService,
                           IAnalyticsService analytics = null, IRandom rng = null, SaveMigrator migrator = null)
        {
            var errors = ConfigValidator.Validate(config);
            if (errors.Count > 0) throw new ArgumentException("Invalid GameConfig:\n - " + string.Join("\n - ", errors));
            Config = config;
            Clock = clock;
            Analytics = analytics ?? new NullAnalytics();
            Rng = new XorShiftRandom((ulong)clock.UtcNow.Ticks ^ 0xA5A5A5A5UL);
            _externalRng = rng;
            IRandom r = rng ?? Rng;

            Wallet = new Wallet(config.Economy.MaxBalance, config.Economy.StartingBalance);
            Production = new ProductionSystem(config, Wallet, Signals);
            Progression = new ProgressionSystem(config, Wallet, Production, Ledger, Signals);
            Production.SetPhaseProvider(() => Progression.PhaseIndex);
            Orders = new OrderSystem(config, Wallet, Production, Progression, Ledger, Signals);
            Experimental = new ExperimentalMachineEvent(config, Wallet, Production, Ledger, r, Signals);
            Surge = new ProductionSurgeEvent(config, Production, Ledger, r, Signals);
            Crates = new FreePartsCrate(config, Wallet, Production, Ledger, r, Signals);
            Experimental.SetPhaseProvider(() => Progression.PhaseIndex);
            Surge.SetPhaseProvider(() => Progression.PhaseIndex);
            Progression.GrantCrates = (n, src) => Crates.Grant(n, src);
            Orders.GrantCrates = (n, src) => Crates.Grant(n, src);
            Offline = new OfflineRewardSystem(config, Wallet, Production, Ledger);
            Ads = new RewardedAdController(config.Ads, adService, clock, Analytics);
            Monetization = new MonetizationService(config, Ads, Offline, Production, Ledger, Analytics);
            Saves = new SaveManager(storage, migrator);
            Frenzy = new FrenzySystem(config, Production, Signals);
            Daily = new DailyRewardSystem(config, Wallet, Production, Crates, Ledger, clock);
            Monetization.AttachDaily(Daily);
            WireAnalytics();
        }

        // ------------------------------------------------------------------ lifecycle
        /// <summary>Loads (or creates) the save, applies offline time and begins the session.</summary>
        public void Start()
        {
            LastLoad = Saves.Load();
            Apply(LastLoad.Data);
            var now = Clock.UtcNow;
            if (Stats.InstallTicks == 0) Stats.InstallTicks = now.Ticks;
            Stats.Sessions++;
            Offline.CheckOffline(now, AdvanceWorld);
            Daily.Check();
            Progression.Update();
            Orders.Update(0);
            Started = true;
            _sessionSeconds = 0;
            Analytics.Track(AnalyticsEvents.SessionStarted, AnalyticsParams.Of(
                "session_index", Stats.Sessions, "days_since_install", (int)((now.Ticks - Stats.InstallTicks) / TimeSpan.TicksPerDay),
                "phase", Progression.PhaseIndex));
            Save();
        }

        /// <summary>Advances the game by dt real seconds. Called once per frame by the host.</summary>
        public void Update(double dt)
        {
            if (!Started || !(dt > 0) || !Num.IsFinite(dt)) return;
            dt = Math.Min(dt, MaxFrameSeconds);   // a long frame (debugger, hitch) is not an offline period
            Production.Tick(dt);
            Progression.Update();
            Orders.Update(dt);
            Experimental.Advance(dt);
            Surge.Advance(dt);
            Ads.Tick(dt);
            Frenzy.Tick(dt);
            _dailyCheck += dt;
            if (_dailyCheck >= 30) { _dailyCheck = 0; Daily.Check(); }
            Stats.SecondsPlayed += dt;
            _sessionSeconds += dt;
            ReportTutorial();
            _autosave += dt;
            if (_autosave >= Config.Economy.AutosaveSeconds) Save();
        }

        /// <summary>App goes to background / is closing: persist and stamp the time used for offline pay.</summary>
        public void Pause()
        {
            if (!Started) return;
            Save();
        }

        /// <summary>App returns to foreground: compute offline reward for the time away.</summary>
        public void Resume()
        {
            if (!Started) return;
            Offline.CheckOffline(Clock.UtcNow, AdvanceWorld);
            Daily.Check();
            Progression.Update();
            Orders.Update(0);
        }

        public void End()
        {
            if (!Started) return;
            Analytics.Track(AnalyticsEvents.SessionEnded, AnalyticsParams.Of("duration_s", (int)_sessionSeconds, "phase", Progression.PhaseIndex));
            Save();
            Started = false;
        }

        void AdvanceWorld(double seconds)
        {
            Production.TickBoosts(seconds);
            Experimental.Advance(seconds);
            Surge.Advance(seconds);
        }

        // ------------------------------------------------------------------ persistence
        public SaveData Capture()
        {
            Offline.Touch(Clock.UtcNow);
            return new SaveData
            {
                SavedAtTicks = Clock.UtcNow.Ticks,
                RngState = Rng.State,
                Wallet = new WalletSave { Balance = Wallet.Balance, TotalEarned = Wallet.TotalEarned, TotalSpent = Wallet.TotalSpent },
                Production = Production.Capture(),
                PhaseIndex = Progression.PhaseIndex,
                Orders = Orders.Capture(),
                Experiment = Experimental.Capture(),
                Surge = Surge.Capture(),
                Crate = Crates.Capture(),
                Offline = Offline.Capture(),
                Ads = Ads.Capture(Monetization.BoostSeq),
                Daily = Daily.Capture(),
                Claimed = Ledger.Snapshot(),
                Settings = Settings,
                Stats = Stats
            };
        }

        public bool Save()
        {
            _autosave = 0;
            return Saves.Save(Capture());
        }

        public void Apply(SaveData d)
        {
            Wallet.Restore(d.Wallet.Balance, d.Wallet.TotalEarned, d.Wallet.TotalSpent);
            Ledger.Restore(d.Claimed);
            Production.Restore(d.Production);
            Progression.Restore(d.PhaseIndex);
            Orders.Restore(d.Orders);
            Experimental.Restore(d.Experiment);
            Surge.Restore(d.Surge);
            Crates.Restore(d.Crate);
            Offline.Restore(d.Offline);
            Ads.Restore(d.Ads);
            Monetization.Restore(d.Ads.BoostSeq);
            Daily.Restore(d.Daily);
            Settings = d.Settings ?? new SettingsSave();
            Stats = d.Stats ?? new StatsSave();
            if (_externalRng == null && d.RngState != 0) Rng.State = d.RngState;
            _tutorialReported = Orders.TutorialCompleted;
        }

        // ------------------------------------------------------------------ analytics wiring
        void ReportTutorial()
        {
            if (_tutorialReported || !Orders.TutorialCompleted) return;
            _tutorialReported = true;
            Analytics.Track(AnalyticsEvents.TutorialCompleted, AnalyticsParams.Of("seconds_played", (int)Stats.SecondsPlayed));
        }

        void WireAnalytics()
        {
            Signals.MachineLevelChanged += (id, lvl) => Analytics.Track(AnalyticsEvents.MachinePurchased, AnalyticsParams.Of("machine_id", id, "level", lvl));
            Signals.UpgradePurchased += id => Analytics.Track(AnalyticsEvents.UpgradePurchased, AnalyticsParams.Of("upgrade_id", id));
            Signals.OrderClaimed += id => Analytics.Track(AnalyticsEvents.OrderCompleted, AnalyticsParams.Of("order_id", id));
            Signals.CrateOpened += id => Analytics.Track(AnalyticsEvents.FreeCrateOpened, AnalyticsParams.Of("entry_id", id));
            Frenzy.Triggered += () => Analytics.Track(AnalyticsEvents.FrenzyTriggered);

            Experimental.Offered += () => { Stats.RiskShown++; Analytics.Track(AnalyticsEvents.RiskEventShown, AnalyticsParams.Of("type", "experimental")); };
            Experimental.Accepted += (c, cost) => Analytics.Track(AnalyticsEvents.RiskEventAccepted, AnalyticsParams.Of("type", "experimental", "choice", c.ToString()));
            Experimental.Declined += () => { Stats.RiskDeclined++; Analytics.Track(AnalyticsEvents.RiskEventDeclined, AnalyticsParams.Of("type", "experimental")); };
            Experimental.Completed += (c, outcome, payout) => Analytics.Track(AnalyticsEvents.RiskEventCompleted, AnalyticsParams.Of("type", "experimental", "choice", c.ToString(), "outcome", outcome));

            Surge.Offered += () => { Stats.RiskShown++; Analytics.Track(AnalyticsEvents.RiskEventShown, AnalyticsParams.Of("type", "surge")); };
            Surge.Expired += () => { Stats.RiskDeclined++; Analytics.Track(AnalyticsEvents.RiskEventDeclined, AnalyticsParams.Of("type", "surge", "reason", "expired")); };
            Surge.Resolved += (c, outcome, m, s) =>
            {
                Analytics.Track(AnalyticsEvents.RiskEventAccepted, AnalyticsParams.Of("type", "surge", "choice", c.ToString()));
                Analytics.Track(AnalyticsEvents.RiskEventCompleted, AnalyticsParams.Of("type", "surge", "choice", c.ToString(), "outcome", outcome));
            };
        }

        // ------------------------------------------------------------------ player-facing helpers
        /// <summary>Claims the offline reward (base plus granted bonus). Always available regardless of ads.</summary>
        public double ClaimOffline()
        {
            var p = Offline.Pending;
            if (p == null) return 0;
            double secs = p.Seconds; bool bonus = p.BonusGranted;
            double got = Offline.TryClaim();
            if (got > 0) Analytics.Track(AnalyticsEvents.OfflineRewardClaimed, AnalyticsParams.Of("seconds", (int)secs, "with_bonus", bonus));
            Save();
            return got;
        }

        public void DeclineExperimental() { Experimental.TryDecline(); Save(); }

        /// <summary>Claims today's gift (doubled if the optional ad bonus was earned). Always available without ads.</summary>
        public bool ClaimDaily()
        {
            var p = Daily.Pending;
            if (p == null) return false;
            int day = p.DayIndex; bool bonus = p.BonusGranted;
            bool ok = Daily.TryClaim();
            if (ok) Analytics.Track(AnalyticsEvents.DailyRewardClaimed, AnalyticsParams.Of("day", day + 1, "with_bonus", bonus));
            Save();
            return ok;
        }
    }
}
