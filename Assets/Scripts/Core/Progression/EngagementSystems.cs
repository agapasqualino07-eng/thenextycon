using System;

namespace OfficinaTycoon
{
    /// <summary>
    /// "Frenesia": each tap adds charge, charge decays over time; a full meter triggers a short production boost
    /// (taps benefit too). Rewards active play without punishing anyone who does not tap. Not persisted (session-only),
    /// the resulting boost is persisted by ProductionSystem like every other boost.
    /// </summary>
    public sealed class FrenzySystem
    {
        public const string BoostSource = "frenzy";
        readonly FrenzyConfig _c;
        readonly ProductionSystem _production;

        public double Charge { get; private set; }
        public double Fill => _c.TapsToFill <= 0 ? 0 : Math.Min(1, Charge / _c.TapsToFill);
        public bool Active => _production.BoostRemaining(BoostSource) > 0;
        public double ActiveRemaining => _production.BoostRemaining(BoostSource);
        public bool Enabled => _c.Enabled;
        public event Action Triggered;

        public FrenzySystem(GameConfig cfg, ProductionSystem production, GameSignals signals)
        {
            _c = cfg.Frenzy; _production = production;
            signals.Tapped += OnTap;
        }

        void OnTap()
        {
            if (!_c.Enabled || Active) return;
            Charge += 1;
            if (Charge >= _c.TapsToFill)
            {
                Charge = 0;
                _production.AddBoost(BoostSource, _c.Multiplier, _c.Seconds);
                Triggered?.Invoke();
            }
        }

        public void Tick(double dt)
        {
            if (Charge > 0 && dt > 0) Charge = Math.Max(0, Charge - _c.DecayPerSecond * dt);
        }
    }

    public sealed class DailyOffer
    {
        public int Id;              // = claims so far + 1 (unique, monotonic)
        public int DayIndex;        // 0..Days-1
        public DailyRewardDef Def;
        public bool BonusGranted;
    }

    /// <summary>
    /// Daily gift. Available once per UTC day. The calendar position advances on each claim and is never reset by
    /// missed days. LastDay only moves forward: setting the device clock back yields nothing until real time passes it.
    /// The base gift never depends on ads; an optional rewarded ad gives the same gift again (declared up-front).
    /// </summary>
    public sealed class DailyRewardSystem
    {
        static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        readonly DailyConfig _c;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly FreePartsCrate _crates;
        readonly ClaimLedger _ledger;
        readonly IClock _clock;

        public int LastDay { get; private set; } = -1;
        public int Index { get; private set; }
        public int Claims { get; private set; }
        public bool ClockAnomaly { get; private set; }
        public DailyOffer Pending { get; private set; }
        bool _bonusGranted;

        public DailyRewardSystem(GameConfig cfg, Wallet wallet, ProductionSystem production, FreePartsCrate crates, ClaimLedger ledger, IClock clock)
        {
            _c = cfg.Daily; _wallet = wallet; _production = production; _crates = crates; _ledger = ledger; _clock = clock;
        }

        public int DayCount => _c.Days.Count;
        public DailyRewardDef DefAt(int index) => _c.Days[((index % DayCount) + DayCount) % DayCount];
        int Today => (int)Math.Floor((_clock.UtcNow - Epoch).TotalDays);

        /// <summary>Creates the pending gift if a new day has started. Call on start/resume and periodically.</summary>
        public DailyOffer Check()
        {
            ClockAnomaly = false;
            if (!_c.Enabled || DayCount == 0) return null;
            int today = Today;
            if (today < LastDay) { ClockAnomaly = true; return Pending; }
            if (Pending == null && today > LastDay)
                Pending = new DailyOffer { Id = Claims + 1, DayIndex = Index % DayCount, Def = DefAt(Index), BonusGranted = _bonusGranted };
            return Pending;
        }

        /// <summary>Currency value of a gift right now (used both to display and to pay).</summary>
        public double CurrencyValue(DailyRewardDef d) => d.Kind != DailyRewardKind.Currency ? 0
            : Math.Floor(Math.Max(d.MinCurrency, d.OpsSeconds * _production.BaseOutputPerSecond));

        /// <summary>Pays the gift (twice if the ad bonus was earned). At most once per gift id.</summary>
        public bool TryClaim()
        {
            var p = Pending;
            if (p == null) return false;
            Pending = null;
            if (!_ledger.TryClaim("daily:" + p.Id)) return false;
            int times = 1 + (p.BonusGranted ? (int)Math.Round(Math.Max(0, _c.AdBonusMultiplier)) : 0);
            for (int i = 0; i < times; i++) Pay(p.Def, "daily:" + p.Id);
            LastDay = Math.Max(LastDay, Today);
            Index = (Index + 1) % DayCount;
            Claims++;
            _bonusGranted = false;
            return true;
        }

        void Pay(DailyRewardDef d, string reason)
        {
            switch (d.Kind)
            {
                case DailyRewardKind.Currency: _wallet.Add(CurrencyValue(d), reason); break;
                case DailyRewardKind.Crate: _crates.Grant(d.Amount, reason); break;
                case DailyRewardKind.Parts: _production.AddParts(d.Amount); break;
            }
        }

        /// <summary>Marks the optional ad bonus as earned for the pending gift. Idempotent.</summary>
        public bool GrantBonus()
        {
            var p = Pending;
            if (p == null || p.BonusGranted || _c.AdBonusMultiplier <= 0) return false;
            if (!_ledger.TryClaim("daily_bonus:" + p.Id)) return false;
            p.BonusGranted = true;
            _bonusGranted = true;
            return true;
        }

        public DailySave Capture() => new DailySave { LastDay = LastDay, Index = Index, Claims = Claims, BonusGranted = Pending != null && Pending.BonusGranted };

        public void Restore(DailySave s)
        {
            Pending = null; LastDay = -1; Index = 0; Claims = 0; _bonusGranted = false;
            if (s == null) return;
            LastDay = Math.Max(-1, s.LastDay);
            Index = DayCount == 0 ? 0 : ((s.Index % DayCount) + DayCount) % DayCount;
            Claims = Math.Max(0, s.Claims);
            _bonusGranted = s.BonusGranted;
        }
    }
}
