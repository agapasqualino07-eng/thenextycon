using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>Names of all analytics events (see Docs/ANALYTICS.md).</summary>
    public static class AnalyticsEvents
    {
        public const string SessionStarted = "game_session_started";
        public const string SessionEnded = "game_session_ended";
        public const string TutorialCompleted = "tutorial_completed";
        public const string MachinePurchased = "machine_purchased";
        public const string UpgradePurchased = "upgrade_purchased";
        public const string OrderCompleted = "order_completed";
        public const string OfflineRewardClaimed = "offline_reward_claimed";
        public const string RiskEventShown = "risk_event_shown";
        public const string RiskEventAccepted = "risk_event_accepted";
        public const string RiskEventDeclined = "risk_event_declined";
        public const string RiskEventCompleted = "risk_event_completed";
        public const string FreeCrateOpened = "free_crate_opened";
        public const string RewardedOfferShown = "rewarded_offer_shown";
        public const string RewardedAdStarted = "rewarded_ad_started";
        public const string RewardedAdCompleted = "rewarded_ad_completed";
        public const string RewardedAdFailed = "rewarded_ad_failed";
        public const string BonusGranted = "bonus_granted";
        public const string DailyRewardClaimed = "daily_reward_claimed";
        public const string FrenzyTriggered = "frenzy_triggered";
        public const string InviteShared = "invite_shared";
        public const string ReferralWelcomeClaimed = "referral_welcome_claimed";
    }

    /// <summary>
    /// Abstract analytics sink. No SDK is bundled: plug an implementation in later.
    /// Parameters must never contain personal data or direct identifiers.
    /// </summary>
    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }

    public sealed class NullAnalytics : IAnalyticsService
    {
        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null) { }
    }

    public static class AnalyticsParams
    {
        static readonly string[] Forbidden = { "email", "name", "user_id", "userid", "device_id", "deviceid", "advertising_id", "adid", "idfa", "gaid", "phone", "ip" };

        /// <summary>Builds a parameter dictionary from alternating key,value pairs; forbidden (PII-like) keys are dropped.</summary>
        public static Dictionary<string, object> Of(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                string k = kv[i] as string;
                if (string.IsNullOrEmpty(k) || IsForbidden(k)) continue;
                d[k] = kv[i + 1];
            }
            return d;
        }

        public static bool IsForbidden(string key)
        {
            string k = key.ToLowerInvariant();
            foreach (var f in Forbidden) if (k == f) return true;
            return false;
        }
    }

    public struct AnalyticsRecord
    {
        public long Index;
        public string Name;
        public Dictionary<string, object> Params;
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(Name);
            if (Params != null) foreach (var kv in Params) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            return sb.ToString();
        }
    }

    /// <summary>In-memory sink for the local debug mode and tests. Also forwards to an optional log callback.</summary>
    public sealed class DebugAnalytics : IAnalyticsService
    {
        const int Capacity = 500;
        readonly List<AnalyticsRecord> _records = new List<AnalyticsRecord>();
        long _index;
        public Action<string> Log;
        public IReadOnlyList<AnalyticsRecord> Records => _records;

        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            var p = new Dictionary<string, object>();
            if (parameters != null)
                foreach (var kv in parameters)
                    if (!AnalyticsParams.IsForbidden(kv.Key)) p[kv.Key] = kv.Value;
            var rec = new AnalyticsRecord { Index = ++_index, Name = eventName, Params = p };
            if (_records.Count >= Capacity) _records.RemoveAt(0);
            _records.Add(rec);
            Log?.Invoke("[analytics] " + rec);
        }

        public int Count(string name) { int n = 0; foreach (var r in _records) if (r.Name == name) n++; return n; }
        public void Clear() => _records.Clear();
    }
}
