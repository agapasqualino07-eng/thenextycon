using OfficinaTycoon.UnityLayer;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace OfficinaTycoon.Web.Components
{
    /// <summary>
    /// The whole mobile UI. It only reads state and forwards intents to the Core logic; every action is validated there.
    /// Rendering re-runs ~5 times per second (cheap DOM, a few dozen elements).
    /// </summary>
    public partial class Game
    {
        enum Tab { Factory, Upgrades, Orders, Events, Settings }

        static readonly string[] MachineColors = { "#ffaa28", "#5aa0e6", "#6ec878", "#3cbebe", "#aa78e6", "#e65a50", "#ebd250", "#78dcf5" };
        static readonly Dictionary<int, string> GearCache = new Dictionary<int, string>();

        GameSession S => Host.Session;
        IJSInProcessRuntime Js => (IJSInProcessRuntime)JS;

        Tab _tab = Tab.Factory;
        int _buyMode = 1;              // 1, 10 or 0 (= max affordable)
        string _armedKey = "";
        DateTime _armedUntil;
        string _toast;
        DateTime _toastUntil;
        readonly Queue<string> _toastQueue = new Queue<string>();
        bool _showOffline = true;
        string _offAdMessage = "";
        bool _offerShownLogged;
        bool _showInstallHint;
        string _crateResult = "";
        string _exportText = "";
        string _importText = "";
        string _importMessage = "";

        // floating "+N" texts on the tap button
        sealed class Floater { public int Id; public string Text; public int X; public DateTime Born; }
        readonly List<Floater> _floaters = new List<Floater>();
        int _floaterId;
        readonly Random _rnd = new Random();

        // dev-only simulated ad
        IRewardedAdCallbacks _adCallbacks;
        double _adSecondsLeft;
        DateTime _adEnds, _adAutoClose;

        protected override void OnInitialized()
        {
            Host.Start(devRequested: Js.Invoke<bool>("officina.queryFlag", "dev"));
            Host.MockAds.Presenter = PresentMockAd;
            Host.Changed += OnHostChanged;
            Host.Resumed += OnResumed;
            Subscribe();
            var st = S.LastLoad != null ? S.LastLoad.Status : LoadStatus.NewGame;
            if (st == LoadStatus.CorruptStartedNew) Toast(Loc.T("toast.load_corrupt"));
            else if (st == LoadStatus.TooNewStartedNew) Toast(Loc.T("toast.load_newer"));
            else if (st == LoadStatus.RecoveredFromBackup) Toast(Loc.T("toast.load_recovered"));
            if (S.Offline.ClockAnomaly) Toast(Loc.T("offline.anomaly"));
            _showInstallHint = Js.Invoke<bool>("officina.isIos") && !Js.Invoke<bool>("officina.isStandalone") && !S.Settings.PrivacyAccepted;
            if (_showInstallHint) { S.Settings.PrivacyAccepted = true; S.Save(); }   // shown once
        }

        GameSession _subscribedTo;
        void Subscribe()
        {
            if (_subscribedTo == S) return;
            _subscribedTo = S;
            S.Signals.MachineLevelChanged += (id, lvl) => Sfx(lvl == 1 ? "unlock" : "buy");
            S.Signals.UpgradePurchased += id => { Sfx("upgrade"); Toast(Loc.T("toast.upgrade")); };
            S.Signals.OrderClaimed += id => { Sfx("order"); Toast(Loc.T("toast.order_done")); };
            S.Signals.PhaseChanged += p => { Sfx("unlock"); Toast(Loc.T("toast.phase", S.Progression.Phase.Name)); };
            S.Signals.Toast += key => Toast(Loc.T(key));
            S.Crates.Granted += (n, src) => { if (n > 0) Toast(Loc.T("toast.crate_earned")); };
        }

        void OnHostChanged()
        {
            Subscribe();   // after import/reset a new session exists
            var now = DateTime.UtcNow;
            _floaters.RemoveAll(f => (now - f.Born).TotalSeconds > 1.0);
            if (_toast != null && now >= _toastUntil) _toast = null;
            if (_toast == null && _toastQueue.Count > 0) { _toast = _toastQueue.Dequeue(); _toastUntil = now.AddSeconds(2.4); }
            UpdateMockAd();
            InvokeAsync(StateHasChanged);
        }

        void OnResumed() { _showOffline = true; _offAdMessage = ""; _offerShownLogged = false; }

        public void Dispose()
        {
            Host.Changed -= OnHostChanged;
            Host.Resumed -= OnResumed;
        }

        // ------------------------------------------------------------ helpers
        void Sfx(string name) { if (S.Settings.SfxOn) Js.InvokeVoid("officina.sfx", name); }

        void Toast(string text)
        {
            if (string.IsNullOrEmpty(text) || _toastQueue.Count >= 4) return;
            _toastQueue.Enqueue(text);
        }

        bool Armed(string key) => _armedKey == key && DateTime.UtcNow < _armedUntil;
        void Arm(string key) { _armedKey = key; _armedUntil = DateTime.UtcNow.AddSeconds(4); }

        static string Pct(double v) => (Math.Clamp(v, 0, 1) * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";

        static string GearPath(int teeth)
        {
            lock (GearCache)
            {
                if (GearCache.TryGetValue(teeth, out var p)) return p;
                var sb = new StringBuilder();
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                int steps = teeth * 4;
                for (int k = 0; k < steps; k++)
                {
                    double a = 2 * Math.PI * k / steps;
                    double r = (k % 4 == 0 || k % 4 == 1) ? 48 : 38;   // tooth top / root
                    double a2 = a + (k % 4 == 1 || k % 4 == 3 ? -0.08 : 0.08);
                    sb.Append(k == 0 ? "M" : "L").Append((r * Math.Cos(a2)).ToString("0.##", inv)).Append(',').Append((r * Math.Sin(a2)).ToString("0.##", inv));
                }
                sb.Append("Z M14,0 A14,14 0 1,0 -14,0 A14,14 0 1,0 14,0 Z");   // centre hole
                p = sb.ToString();
                GearCache[teeth] = p;
                return p;
            }
        }

        static string GearStyle(int i, int lvl)
        {
            if (lvl <= 0) return "animation:none";
            double degPerSec = Math.Min(120, 18 + 10 * Math.Log2(1 + lvl));
            return "animation-duration:" + (360 / degPerSec).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }

        string BoostText()
        {
            double mult = S.Production.TempMultiplier;
            if (mult <= 1.0001) return "";
            double rem = S.Production.ActiveBoosts.Count == 0 ? 0 : S.Production.ActiveBoosts.Max(b => b.remaining);
            return Loc.T("top.boost", Fmt.Mult(mult), Fmt.Time(rem));
        }

        bool HasFactoryDot() => S.Config.Machines.Any(m => S.Production.Level(m.Id) == 0 && S.Production.MaxAffordableCount(m.Id) > 0);
        bool HasUpgradeDot() => S.Config.Upgrades.Any(u => S.Production.IsUpgradeAvailable(u.Id) && S.Wallet.CanAfford(u.Cost));
        bool HasEventsDot() => S.Experimental.Stage == EventStage.Offered || S.Experimental.Stage == EventStage.ReadyToCollect
                               || S.Surge.Stage == EventStage.Offered || S.Crates.Available > 0;

        string AdReasonText(AdFailReason r)
        {
            switch (r)
            {
                case AdFailReason.Unavailable:
                case AdFailReason.NoNetwork: return Loc.T("offline.ad_unavailable");
                case AdFailReason.Cooldown: return Loc.T("offline.ad_cooldown");
                case AdFailReason.DailyLimit: return Loc.T("offline.ad_limit");
                case AdFailReason.Disabled: return Loc.T("offline.ad_disabled");
                case AdFailReason.Busy: return Loc.T("offline.ad_loading");
                default: return Loc.T("offline.ad_failed");
            }
        }

        // ------------------------------------------------------------ actions
        void OnTap()
        {
            double got = S.Production.Tap();
            Sfx("tap");
            if (_floaters.Count < 12)
                _floaters.Add(new Floater { Id = ++_floaterId, Text = Fmt.Num(got), X = _rnd.Next(10, 85), Born = DateTime.UtcNow });
        }

        void SwitchTab(Tab t) { _tab = t; Sfx("tap"); }

        int BuyCount(MachineDef d)
        {
            int room = d.MaxLevel - S.Production.Level(d.Id);
            if (room <= 0) return 0;
            if (_buyMode == 0) return Math.Max(1, S.Production.MaxAffordableCount(d.Id));
            return Math.Min(_buyMode, room);
        }

        void BuyMachine(MachineDef d)
        {
            int n = BuyCount(d);
            if (n <= 0) return;
            if (!S.Production.TryBuy(d.Id, n)) { Sfx("error"); Toast(Loc.T("toast.not_enough")); }
        }

        void BuyUpgrade(UpgradeDef u)
        {
            if (!S.Production.TryBuyUpgrade(u.Id)) { Sfx("error"); Toast(Loc.T("toast.not_enough")); }
        }

        void Overhaul(MachineDef d)
        {
            if (S.Production.TryOverhaul(d.Id)) { Sfx("upgrade"); Toast(Loc.T("toast.overhaul")); }
            else Sfx("error");
        }

        void ClaimOrder(string id)
        {
            if (S.Orders.TryClaim(id)) S.Save();
        }

        void ExpAccept(ExperimentChoice c)
        {
            var pv = S.Experimental.GetPreview();
            double cost = c == ExperimentChoice.Prudent ? pv.CostPrudent : pv.CostExperimental;
            string key = "exp:" + c;
            if (!Armed(key)) { Arm(key); return; }      // irreversible spend: confirm with a second tap
            _armedKey = "";
            if (S.Experimental.TryAccept(c, cost)) { Sfx("event"); S.Save(); }
            else { Sfx("error"); Toast(Loc.T("toast.not_enough")); }
        }

        void ExpCollect() { if (S.Experimental.TryCollect()) { Sfx("reward"); S.Save(); } }
        void ExpDecline() { S.DeclineExperimental(); }

        void SurgeChoose(SurgeChoice c)
        {
            if (S.Surge.TryChoose(c)) { Sfx("event"); Toast(Loc.T("toast.surge")); S.Save(); }
        }

        void SurgeDismiss() { S.Surge.TryDismiss(); S.Save(); }

        void OpenCrate()
        {
            var r = S.Crates.TryOpen();
            if (r == null) { Sfx("error"); return; }
            Sfx("reward");
            switch (r.Kind)
            {
                case CrateRewardKind.Currency: _crateResult = Loc.T("crate.result_currency", Fmt.Cr(r.Currency)); break;
                case CrateRewardKind.TempBoost: _crateResult = Loc.T("crate.result_boost", Fmt.Mult(r.BoostMultiplier), Fmt.Time(r.BoostSeconds)); break;
                case CrateRewardKind.Parts: _crateResult = Loc.T("crate.result_parts", r.Parts); break;
            }
            S.Save();
        }

        void ClaimOffline()
        {
            double got = S.ClaimOffline();
            if (got > 0) { Sfx("reward"); Toast("+" + Fmt.Cr(got)); }
        }

        void OfflineAd()
        {
            _offAdMessage = Loc.T("offline.ad_loading");
            var r = S.Monetization.RequestOfflineBonus((granted, reason) =>
            {
                if (granted) { Sfx("reward"); _offAdMessage = ""; }
                else _offAdMessage = AdReasonText(reason == AdFailReason.None ? AdFailReason.Error : reason);
            });
            if (r != AdFailReason.None) _offAdMessage = AdReasonText(r);
        }

        void BoostAd()
        {
            S.Monetization.RequestBoost((granted, reason) =>
            {
                if (granted) { Sfx("reward"); Toast(Loc.T("toast.boost_ad")); }
                else if (reason != AdFailReason.None) Toast(AdReasonText(reason));
            });
        }

        void ToggleSfx() { S.Settings.SfxOn = !S.Settings.SfxOn; S.Save(); }

        async Task CopyExport()
        {
            if (string.IsNullOrEmpty(_exportText)) _exportText = Host.ExportSave();
            bool ok = await JS.InvokeAsync<bool>("officina.copyText", _exportText);
            Toast(Loc.T(ok ? "settings.copied" : "settings.copy_failed"));
        }

        void Import()
        {
            if (!Armed("import")) { Arm("import"); _importMessage = Loc.T("settings.import_confirm"); return; }
            _armedKey = "";
            if (Host.ImportSave(_importText, out var err))
            {
                _importText = ""; _importMessage = Loc.T("settings.import_ok"); _exportText = "";
                _showOffline = true;
            }
            else _importMessage = Loc.T(err == "too_new" ? "settings.import_too_new" : "settings.import_invalid");
        }

        void Reset()
        {
            if (!Armed("reset")) { Arm("reset"); return; }
            _armedKey = "";
            Host.ResetProgress();
            _crateResult = ""; _exportText = "";
            Toast(Loc.T("settings.reset_done"));
        }

        // ---- dev tools (only compiled with OfficinaDevTools=true and enabled by ?dev=1)
        void DevMoney(double amount) { if (Host.DevMode) S.Wallet.Add(amount, "dev"); }
        void DevAway(double seconds)
        {
            if (!Host.DevMode) return;
            S.Pause();
            Host.Clock.OffsetSeconds += seconds;
            S.Resume();
            OnResumed();
        }
        void DevNextPhase()
        {
            if (!Host.DevMode) return;
            int next = Math.Min(S.Config.Phases.Count - 1, S.Progression.PhaseIndex + 1);
            S.Wallet.Add(Math.Max(1, S.Config.Phases[next].TotalEarnedRequired - S.Wallet.TotalEarned + 1), "dev");
            S.Progression.Update();
        }
        void DevEvents() { if (Host.DevMode) { S.Experimental.Advance(1e6); S.Surge.Advance(1e6); } }
        void DevCrate() { if (Host.DevMode) S.Crates.Grant(1, "dev"); }
        void DevCycleAd()
        {
            if (!Host.DevMode) return;
            var all = (MockAdBehavior[])Enum.GetValues(typeof(MockAdBehavior));
            Host.MockAds.Behavior = all[(Array.IndexOf(all, Host.MockAds.Behavior) + 1) % all.Length];
        }

        // ---- simulated ad (dev)
        void PresentMockAd(IRewardedAdCallbacks cb, MockAdBehavior behavior)
        {
            _adCallbacks = cb;
            _adEnds = DateTime.UtcNow.AddSeconds(3);
            _adAutoClose = behavior == MockAdBehavior.CloseEarly ? DateTime.UtcNow.AddSeconds(1) : DateTime.MaxValue;
            _adSecondsLeft = 3;
        }

        void UpdateMockAd()
        {
            if (_adCallbacks == null) return;
            var now = DateTime.UtcNow;
            if (now >= _adAutoClose) { CloseMockAdEarly(); return; }
            _adSecondsLeft = (_adEnds - now).TotalSeconds;
            if (_adSecondsLeft <= 0)
            {
                var cb = _adCallbacks; _adCallbacks = null;
                cb.OnRewardEarned();
                cb.OnClosed();
            }
        }

        void CloseMockAdEarly()
        {
            var cb = _adCallbacks; _adCallbacks = null;
            cb?.OnClosed();
        }

        string UnlockText(MachineDef d)
        {
            if (S.Progression.PhaseIndex < d.UnlockPhase) return Loc.T("machine.req_phase", S.Config.Phases[d.UnlockPhase].Name);
            var req = S.Config.Machines.FirstOrDefault(m => m.Id == d.UnlockRequiresMachineId);
            return req != null ? Loc.T("machine.req_machine", req.Name, d.UnlockRequiresLevel) : Loc.T("machine.locked");
        }
    }
}
