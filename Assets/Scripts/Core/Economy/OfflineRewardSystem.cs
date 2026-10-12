using System;

namespace OfficinaTycoon
{
    public sealed class OfflineOffer
    {
        public int Id;
        public double Seconds;
        public double Base;
        public double Bonus;           // declared up-front; only paid after a confirmed rewarded ad
        public bool BonusGranted;
        public bool HitCap;
    }

    /// <summary>
    /// Offline earnings. Safety properties:
    ///  - capped by OfflineMaxSeconds (config + upgrades);
    ///  - LastValidTicks only ever moves forward, so a device clock set into the past yields no reward and
    ///    cannot be used to farm repeated rewards by going back and forth;
    ///  - the offer is persisted until claimed, base and bonus are each paid at most once (ClaimLedger);
    ///  - the normal (base) reward never depends on ads.
    /// A forward clock jump is indistinguishable from real absence: it yields at most one capped offer per jump.
    /// </summary>
    public sealed class OfflineRewardSystem
    {
        readonly GameConfig _cfg;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly ClaimLedger _ledger;

        public long LastValidTicks { get; private set; }
        public int OfferSeq { get; private set; }
        public OfflineOffer Pending { get; private set; }
        public bool ClockAnomaly { get; private set; }

        public OfflineRewardSystem(GameConfig cfg, Wallet wallet, ProductionSystem production, ClaimLedger ledger)
        {
            _cfg = cfg; _wallet = wallet; _production = production; _ledger = ledger;
        }

        /// <summary>Records that the game was alive at <paramref name="now"/> (called on save/pause/autosave).</summary>
        public void Touch(DateTime now)
        {
            if (now.Ticks > LastValidTicks) LastValidTicks = now.Ticks;
        }

        /// <summary>
        /// Computes the offer for the time since LastValidTicks. <paramref name="advanceWorld"/> is told how many
        /// seconds of (capped) absence to apply to timers (events, boosts).
        /// </summary>
        public OfflineOffer CheckOffline(DateTime now, Action<double> advanceWorld)
        {
            ClockAnomaly = false;
            long ticks = now.Ticks;
            if (LastValidTicks <= 0) { LastValidTicks = ticks; return Pending; }
            if (ticks < LastValidTicks) { ClockAnomaly = true; return Pending; }   // clock moved back: no reward, no state change

            double elapsed = (ticks - LastValidTicks) / (double)TimeSpan.TicksPerSecond;
            LastValidTicks = ticks;

            // A still-unclaimed offer whose bonus was already earned stays on screen after a short absence;
            // after a new qualifying absence it is deposited (it is already paid for) and replaced.
            if (Pending != null && Pending.BonusGranted)
            {
                if (elapsed < _cfg.Offline.MinSecondsToShow || _production.BaseOutputPerSecond <= 0) return Pending;
                TryClaim();
            }

            double cap = _production.OfflineMaxSeconds;
            if (elapsed < _cfg.Offline.MinSecondsToShow && Pending == null) return null;

            double newSeconds = Math.Min(elapsed, cap);
            double ops = _production.BaseOutputPerSecond;
            if (ops <= 0) return Pending;

            double seconds = newSeconds + (Pending != null ? Pending.Seconds : 0);
            bool hit = seconds >= cap || elapsed > cap;
            seconds = Math.Min(seconds, cap);
            double eff = _production.OfflineEfficiency;
            double baseAmount = Math.Floor(seconds * ops * eff);
            if (baseAmount <= 0) return Pending;

            if (Pending == null) Pending = new OfflineOffer { Id = ++OfferSeq };
            Pending.Seconds = seconds;
            Pending.Base = baseAmount;
            Pending.Bonus = Math.Floor(baseAmount * _cfg.Offline.AdBonusFraction);
            Pending.HitCap = hit;
            advanceWorld?.Invoke(newSeconds);
            return Pending;
        }

        /// <summary>Pays the base reward (plus the bonus if an ad was confirmed). At most once per offer.</summary>
        public double TryClaim()
        {
            var p = Pending;
            if (p == null) return 0;
            Pending = null;
            if (!_ledger.TryClaim("offline:" + p.Id)) return 0;
            double total = p.Base + (p.BonusGranted ? p.Bonus : 0);
            return _wallet.Add(total, "offline:" + p.Id);
        }

        /// <summary>Marks the ad bonus as earned. Idempotent per offer.</summary>
        public bool GrantBonus()
        {
            var p = Pending;
            if (p == null || p.BonusGranted) return false;
            if (!_ledger.TryClaim("offline_bonus:" + p.Id)) return false;
            p.BonusGranted = true;
            return true;
        }

        public OfflineSave Capture()
        {
            var s = new OfflineSave { LastValidTicks = LastValidTicks, OfferSeq = OfferSeq };
            if (Pending != null)
            {
                s.HasOffer = true; s.OfferId = Pending.Id; s.Seconds = Pending.Seconds; s.Base = Pending.Base;
                s.Bonus = Pending.Bonus; s.BonusGranted = Pending.BonusGranted;
            }
            return s;
        }

        public void Restore(OfflineSave s)
        {
            Pending = null; LastValidTicks = 0; OfferSeq = 0; ClockAnomaly = false;
            if (s == null) return;
            LastValidTicks = Math.Max(0, s.LastValidTicks);
            OfferSeq = Math.Max(0, s.OfferSeq);
            if (s.HasOffer && s.Base > 0 && Num.IsFinite(s.Base))
                Pending = new OfflineOffer { Id = s.OfferId, Seconds = Math.Max(0, s.Seconds), Base = s.Base,
                                             Bonus = Math.Max(0, Num.Sanitize(s.Bonus)), BonusGranted = s.BonusGranted };
        }
    }
}
