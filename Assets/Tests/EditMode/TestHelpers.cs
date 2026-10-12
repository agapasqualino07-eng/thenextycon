using System;
using System.Linq;

namespace OfficinaTycoon.Tests
{
    /// <summary>Shared builders for tests. Everything is deterministic: ManualClock + injected RNG.</summary>
    public sealed class Rig
    {
        public GameSession S;
        public ManualClock Clock;
        public MockRewardedAdService Ads;
        public InMemorySaveStorage Storage;
        public DebugAnalytics Analytics;
        public GameConfig Config;
        public IRandom Rng;

        public static Rig New(IRandom rng = null, GameConfig cfg = null, bool start = true)
        {
            var r = new Rig
            {
                Clock = new ManualClock(),
                Ads = new MockRewardedAdService(),
                Storage = new InMemorySaveStorage(),
                Analytics = new DebugAnalytics(),
                Config = cfg ?? DefaultConfig.Create(),
                Rng = rng ?? new XorShiftRandom(42)
            };
            r.S = new GameSession(r.Config, r.Clock, r.Storage, r.Ads, r.Analytics, r.Rng);
            if (start) r.S.Start();
            return r;
        }

        /// <summary>Simulates a process restart: persists (unless saveFirst=false, i.e. the process was killed), then builds a fresh GameSession over the same storage.</summary>
        public Rig Restart(IRandom rng = null, bool saveFirst = true)
        {
            if (saveFirst) S.Pause();
            var n = new Rig
            {
                Clock = Clock, Ads = new MockRewardedAdService(), Storage = Storage, Analytics = new DebugAnalytics(),
                Config = DefaultConfig.Create(), Rng = rng ?? Rng
            };
            n.S = new GameSession(n.Config, n.Clock, n.Storage, n.Ads, n.Analytics, n.Rng);
            n.S.Start();
            return n;
        }

        public void Money(double amount) => S.Wallet.Add(amount, "test");

        public void Run(double seconds, double step = 1.0)
        {
            double t = 0;
            while (t < seconds - 1e-9)
            {
                double dt = Math.Min(step, seconds - t);
                Clock.Advance(dt);
                S.Update(dt);
                t += dt;
            }
        }

        /// <summary>Gives the player a few machines so base production is non-zero.</summary>
        public void GiveProduction(int drillLevels = 10)
        {
            Money(1e6);
            if (!S.Production.TryBuy("drill", drillLevels)) throw new Exception("setup failed");
        }

        /// <summary>Forces the session into a given phase by crediting lifetime earnings.</summary>
        public void ReachPhase(int phase)
        {
            S.Wallet.Add(Math.Max(0, Config.Phases[phase].TotalEarnedRequired - S.Wallet.TotalEarned + 1), "test:phase");
            S.Progression.Update();
            if (S.Progression.PhaseIndex < phase) throw new Exception("phase not reached");
        }
    }
}
