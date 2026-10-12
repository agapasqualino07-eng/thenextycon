using System;
using System.Linq;

namespace OfficinaTycoon
{
    public enum ReferralResult { Ok, AlreadyUsed, InvalidCode, OwnCode, Disabled }

    /// <summary>
    /// Invite a friend, offline-first:
    ///  - every player gets a random 6-character code (no personal data);
    ///  - a friend who opens an invite link (code in the URL) receives a one-time welcome gift on their device;
    ///  - the inviter receives a small reward for sharing, at most once per (UTC) day.
    /// Without a server the game cannot know whether the friend really started playing, so there is no
    /// "friend joined" reward here (that needs server-side verification to prevent self-invites).
    /// </summary>
    public sealed class SocialSystem
    {
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O/1/I to avoid typos
        public const int CodeLength = 6;
        static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        readonly SocialConfig _c;
        readonly Wallet _wallet;
        readonly ProductionSystem _production;
        readonly FreePartsCrate _crates;
        readonly ClaimLedger _ledger;
        readonly IClock _clock;
        readonly IRandom _rng;

        public string Code { get; private set; } = "";
        public bool ReferralApplied { get; private set; }
        public string ReferredBy { get; private set; } = "";
        public int LastShareDay { get; private set; } = -1;
        public int Shares { get; private set; }
        public bool Enabled => _c.Enabled;

        public SocialSystem(GameConfig cfg, Wallet wallet, ProductionSystem production, FreePartsCrate crates, ClaimLedger ledger, IClock clock, IRandom rng)
        {
            _c = cfg.Social; _wallet = wallet; _production = production; _crates = crates; _ledger = ledger; _clock = clock; _rng = rng;
        }

        int Today => (int)Math.Floor((_clock.UtcNow - Epoch).TotalDays);

        public static string Normalize(string code) => (code ?? "").Trim().ToUpperInvariant();
        public static bool IsValidCode(string code)
        {
            code = Normalize(code);
            return code.Length == CodeLength && code.All(ch => Alphabet.IndexOf(ch) >= 0);
        }

        public void EnsureCode()
        {
            if (IsValidCode(Code)) return;
            var chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) chars[i] = Alphabet[(int)(_rng.NextDouble() * Alphabet.Length) % Alphabet.Length];
            Code = new string(chars);
        }

        public double WelcomeCurrency => Math.Floor(Math.Max(_c.WelcomeMin, _c.WelcomeOpsSeconds * _production.BaseOutputPerSecond));

        /// <summary>Applies a friend's invite code once per device. Untrusted input: only the strict code format is accepted.</summary>
        public ReferralResult ApplyReferral(string code)
        {
            if (!_c.Enabled) return ReferralResult.Disabled;
            if (ReferralApplied) return ReferralResult.AlreadyUsed;
            code = Normalize(code);
            if (!IsValidCode(code)) return ReferralResult.InvalidCode;
            if (code == Code) return ReferralResult.OwnCode;
            if (!_ledger.TryClaim("welcome")) { ReferralApplied = true; return ReferralResult.AlreadyUsed; }
            ReferralApplied = true;
            ReferredBy = code;
            _wallet.Add(WelcomeCurrency, "welcome");
            if (_c.WelcomeCrates > 0) _crates.Grant(_c.WelcomeCrates, "welcome");
            return ReferralResult.Ok;
        }

        public bool CanClaimShare => _c.Enabled && Today > LastShareDay;

        /// <summary>Reward for sharing the invite (call only after the share sheet reports success). Once per day; clock-back safe.</summary>
        public bool ClaimShare()
        {
            if (!CanClaimShare) return false;
            int today = Today;
            if (!_ledger.TryClaim("share:" + today)) return false;
            LastShareDay = today;
            Shares++;
            if (_c.ShareCrates > 0) _crates.Grant(_c.ShareCrates, "share");
            return true;
        }

        public SocialSave Capture() => new SocialSave { Code = Code, ReferralApplied = ReferralApplied, ReferredBy = ReferredBy, LastShareDay = LastShareDay, Shares = Shares };

        public void Restore(SocialSave s)
        {
            Code = ""; ReferralApplied = false; ReferredBy = ""; LastShareDay = -1; Shares = 0;
            if (s == null) return;
            Code = IsValidCode(s.Code) ? Normalize(s.Code) : "";
            ReferralApplied = s.ReferralApplied;
            ReferredBy = IsValidCode(s.ReferredBy) ? Normalize(s.ReferredBy) : "";
            LastShareDay = Math.Max(-1, s.LastShareDay);
            Shares = Math.Max(0, s.Shares);
        }
    }
}
