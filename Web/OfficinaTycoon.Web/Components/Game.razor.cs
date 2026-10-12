using OfficinaTycoon.UnityLayer;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

        // visual effects / engagement UI state
        double _shownBalance;
        string _popId;
        DateTime _popUntil;
        bool _hit;
        string _banner;
        DateTime _bannerUntil;
        bool _showDaily;
        bool _dailyAutoShown;
        int _phaseModal = -1;
        string _scrollTarget;
        readonly Dictionary<string, int> _lastLevels = new Dictionary<string, int>();
        sealed class CrateReveal { public string Text; public bool Rare; }
        CrateReveal _crateReveal;
        string _welcome;              // inviter code from the link, shown in the welcome popup
        string _codeInput = "";
        string _inviteMessage = "";
        string _say;
        DateTime _sayUntil;
        DateTime _nextTipAt = DateTime.UtcNow.AddSeconds(4);
        int _tipIndex;

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
            ResetVisualState();
            var st = S.LastLoad != null ? S.LastLoad.Status : LoadStatus.NewGame;
            if (st == LoadStatus.CorruptStartedNew) Toast(Loc.T("toast.load_corrupt"));
            else if (st == LoadStatus.TooNewStartedNew) Toast(Loc.T("toast.load_newer"));
            else if (st == LoadStatus.RecoveredFromBackup) Toast(Loc.T("toast.load_recovered"));
            if (S.Offline.ClockAnomaly) Toast(Loc.T("offline.anomaly"));
            _showInstallHint = Js.Invoke<bool>("officina.isIos") && !Js.Invoke<bool>("officina.isStandalone") && !S.Settings.PrivacyAccepted;
            if (_showInstallHint) { S.Settings.PrivacyAccepted = true; S.Save(); }   // shown once
            MaybeAutoDaily();
            // Invite link (?ref=CODE): untrusted input, validated by SocialSystem; the param is removed from the address bar.
            string refCode = Js.Invoke<string>("officina.queryValue", "ref");
            if (!string.IsNullOrEmpty(refCode))
            {
                Js.InvokeVoid("officina.clearQuery", "ref");
                if (SocialSystem.IsValidCode(refCode) && !S.Social.ReferralApplied && SocialSystem.Normalize(refCode) != S.Social.Code)
                    _welcome = SocialSystem.Normalize(refCode);
            }
            Say(Loc.T(S.Orders.TutorialCompleted ? "mascot.hello_back" : "mascot.hello"), 5);
        }

        // ---- mascot
        void Say(string text, double seconds = 3.5) { _say = text; _sayUntil = DateTime.UtcNow.AddSeconds(seconds); }

        void NextTip()
        {
            Sfx("tap");
            var tips = Tips();
            if (tips.Count == 0) return;
            Say(tips[_tipIndex++ % tips.Count], 4);
            _nextTipAt = DateTime.UtcNow.AddSeconds(25);
        }

        List<string> Tips()
        {
            var t = new List<string>();
            if (S.Daily.Pending != null) t.Add(Loc.T("mascot.tip_daily"));
            if (S.Crates.Available > 0) t.Add(Loc.T("mascot.tip_crate"));
            if (S.Orders.ClaimableCount > 0) t.Add(Loc.T("mascot.tip_order"));
            if (HasFactoryDot()) t.Add(Loc.T("mascot.tip_machine"));
            if (Host.AdsAvailable && S.Monetization.CanOfferCrate() == AdFailReason.None) t.Add(Loc.T("mascot.tip_video"));
            if (S.Frenzy.Enabled && !S.Frenzy.Active) t.Add(Loc.T("mascot.tip_frenzy"));
            if (S.Social.Enabled && S.Social.CanClaimShare) t.Add(Loc.T("mascot.tip_invite"));
            return t;
        }

        void ResetVisualState()
        {
            _shownBalance = S.Wallet.Balance;
            _lastLevels.Clear();
            foreach (var m in S.Config.Machines) _lastLevels[m.Id] = S.Production.Level(m.Id);
            Js.InvokeVoid("officinaFx.setEnabled", !S.Settings.ReducedEffects);
        }

        /// <summary>The daily gift pops up by itself only at app start/resume and only after the tutorial (never mid-action).</summary>
        void MaybeAutoDaily()
        {
            if (_dailyAutoShown || S.Daily.Pending == null || !S.Orders.TutorialCompleted) return;
            _dailyAutoShown = true;
            _showDaily = true;
        }

        void Fx(string fn, params object[] args)
        {
            if (S == null || S.Settings.ReducedEffects) return;
            Js.InvokeVoid("officinaFx." + fn, args);
        }

        void Banner(string text) { _banner = text; _bannerUntil = DateTime.UtcNow.AddSeconds(2.6); }

        protected override void OnAfterRender(bool firstRender)
        {
            if (_scrollTarget != null) { Js.InvokeVoid("officinaFx.scrollTo", _scrollTarget); _scrollTarget = null; }
        }

        GameSession _subscribedTo;
        void Subscribe()
        {
            if (_subscribedTo == S) return;
            _subscribedTo = S;
            S.Signals.MachineLevelChanged += OnMachineLevel;
            S.Signals.MachineLevelChanged += (id, lvl) => Say(lvl == 1 ? Loc.T("mascot.new_machine")
                : (lvl % 3) switch { 0 => Loc.T("mascot.cheer0"), 1 => Loc.T("mascot.cheer1"), _ => Loc.T("mascot.cheer2") }, 2.5);
            S.Signals.UpgradePurchased += id => { Sfx("upgrade"); Toast(Loc.T("toast.upgrade")); Fx("burstAt", "[data-test='upgrade-" + id + "']", "build"); };
            S.Signals.OrderClaimed += id =>
            {
                Sfx("order"); Toast(Loc.T("toast.order_done"));
                Fx("flyCoins", _tab == Tab.Orders ? "[data-test='order-" + id + "']" : "[data-test='goal']", "[data-test='balance']", 12);
                Fx("confetti", 45);
            };
            S.Signals.PhaseChanged += p => { Sfx("unlock"); _phaseModal = p; Fx("confetti", 160); };
            S.Frenzy.Triggered += () =>
            {
                Sfx("unlock");
                Banner(Loc.T("frenzy.banner", Fmt.Mult(S.Config.Frenzy.Multiplier)));
                Say(Loc.T("mascot.frenzy"), 3);
                Fx("flash");
                Fx("burstAt", "[data-test='tap']", "frenzy");
            };
            S.Signals.Toast += key => Toast(Loc.T(key));
            S.Crates.Granted += (n, src) => { if (n > 0) Toast(Loc.T("toast.crate_earned")); };
        }

        void OnMachineLevel(string id, int lvl)
        {
            _lastLevels.TryGetValue(id, out int before);
            _lastLevels[id] = lvl;
            Sfx(lvl == 1 ? "unlock" : "buy");
            _popId = id; _popUntil = DateTime.UtcNow.AddSeconds(0.45);
            Fx("burstAt", "[data-bay='" + id + "']", "build");
            int every = S.Config.Economy.MilestoneEvery;
            if (every > 0 && lvl / every > before / every)
            {
                var def = S.Config.Machines.First(m => m.Id == id);
                Banner(Loc.T("milestone.banner", def.Name, (lvl / every) * every));
                Fx("confetti", 70);
            }
        }

        void OnHostChanged()
        {
            Subscribe();   // after import/reset a new session exists
            var now = DateTime.UtcNow;
            // Balance "rolls" towards the real value (spending snaps immediately: never show more than you have).
            double target = S.Wallet.Balance;
            if (target <= _shownBalance || Math.Abs(target - _shownBalance) <= Math.Max(1, target * 0.002)) _shownBalance = target;
            else _shownBalance += (target - _shownBalance) * 0.35;
            if (_banner != null && now >= _bannerUntil) _banner = null;
            if (now >= _nextTipAt && now >= _sayUntil)
            {
                var tips = Tips();
                if (tips.Count > 0) Say(tips[_tipIndex++ % tips.Count], 4);
                _nextTipAt = now.AddSeconds(30);
            }
            _floaters.RemoveAll(f => (now - f.Born).TotalSeconds > 1.0);
            if (_toast != null && now >= _toastUntil) _toast = null;
            if (_toast == null && _toastQueue.Count > 0) { _toast = _toastQueue.Dequeue(); _toastUntil = now.AddSeconds(2.4); }
            UpdateMockAd();
            InvokeAsync(StateHasChanged);
        }

        void OnResumed() { _showOffline = true; _offAdMessage = ""; _offerShownLogged = false; _dailyAutoShown = false; MaybeAutoDaily(); }

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
        // Touch/mouse: counted on finger-down, so fast taps and several fingers at once are never lost
        // (a "click" only arrives after the finger lifts and iOS may merge quick taps).
        DateTime _lastPointerDown = DateTime.MinValue;

        void OnTapPointer(PointerEventArgs e)
        {
            if (e.Button > 0) return;                       // ignore right/middle mouse buttons
            _lastPointerDown = DateTime.UtcNow;
            DoTap(e.ClientX, e.ClientY);
        }

        // After a finger/mouse press the browser also fires a "click" (with detail 0 or 1 depending on the browser):
        // that one was already counted. A click with no press just before it comes from the keyboard (Enter/Space).
        void OnTapClick(MouseEventArgs e)
        {
            if ((DateTime.UtcNow - _lastPointerDown).TotalMilliseconds < 1000) return;
            DoTap(0, 0);
        }

        void DoTap(double clientX, double clientY)
        {
            double got = S.Production.Tap();
            Sfx("tap");
            _hit = !_hit;
            if (clientX > 0 || clientY > 0) Fx("burst", clientX, clientY, "tap");
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

        void OnGoalClick(string id, bool done)
        {
            if (done) ClaimOrder(id);
            else SwitchTab(Tab.Orders);
        }

        void OnBayClick(string id)
        {
            SwitchTab(Tab.Factory);
            _scrollTarget = "[data-test='machine-" + id + "']";
        }

        // ---- daily gift
        void OpenDaily() { _showDaily = true; }

        string DailyText(DailyRewardDef d)
        {
            switch (d.Kind)
            {
                case DailyRewardKind.Currency: return Fmt.Cr(S.Daily.CurrencyValue(d));
                case DailyRewardKind.Crate: return Loc.T(d.Amount == 1 ? "daily.crate" : "daily.crates", d.Amount);
                default: return Loc.T("daily.parts", d.Amount);
            }
        }

        void ClaimDaily()
        {
            var def = S.Daily.Pending?.Def;
            Fx("flyCoins", "#daily-claim", "[data-test='balance']", 14);
            if (S.ClaimDaily())
            {
                Sfx("reward");
                Fx("confetti", def != null && def.Kind == DailyRewardKind.Crate ? 120 : 60);
                _showDaily = false;
            }
        }

        void DailyAd()
        {
            var r = S.Monetization.RequestDailyBonus((granted, reason) =>
            {
                if (granted) { Sfx("reward"); Fx("confetti", 40); }
                else Toast(AdReasonText(reason == AdFailReason.None ? AdFailReason.Error : reason));
            });
            if (r != AdFailReason.None) Toast(AdReasonText(r));
        }

        // ---- videos & invites
        void CrateAd()
        {
            var r = S.Monetization.RequestCrate((granted, reason) =>
            {
                if (granted) { Sfx("reward"); Toast(Loc.T("toast.crate_earned")); Fx("confetti", 50); Say(Loc.T("mascot.tip_crate"), 3); }
                else Toast(AdReasonText(reason == AdFailReason.None ? AdFailReason.Error : reason));
            });
            if (r != AdFailReason.None) Toast(AdReasonText(r));
        }

        void OrderDouble(string id)
        {
            var r = S.Monetization.RequestOrderDouble(id, (granted, reason) =>
            {
                if (granted) { S.Save(); Fx("confetti", 60); }
                else Toast(AdReasonText(reason == AdFailReason.None ? AdFailReason.Error : reason));
            });
            if (r != AdFailReason.None) Toast(AdReasonText(r));
        }

        async Task ShareInvite()
        {
            string url = Js.Invoke<string>("officina.pageUrl") + "?ref=" + S.Social.Code;
            string result = await JS.InvokeAsync<string>("officina.share", Loc.T("app.title"), Loc.T("invite.message"), url);
            if (result == "shared" || result == "copied")
            {
                _inviteMessage = Loc.T(result == "shared" ? "invite.shared" : "invite.copied");
                if (S.ClaimShareReward()) { Sfx("reward"); Toast(Loc.T("toast.crate_earned")); Fx("confetti", 50); }
            }
            else if (result == "failed") _inviteMessage = Loc.T("settings.copy_failed");
        }

        void ApplyCode(string code)
        {
            var r = S.ApplyReferral(code);
            _inviteMessage = r switch
            {
                ReferralResult.Ok => Loc.T("invite.result_ok"),
                ReferralResult.AlreadyUsed => Loc.T("invite.result_alreadyused"),
                ReferralResult.InvalidCode => Loc.T("invite.result_invalidcode"),
                ReferralResult.OwnCode => Loc.T("invite.result_owncode"),
                _ => Loc.T("invite.result_disabled")
            };
            if (r == ReferralResult.Ok) { Sfx("reward"); Fx("confetti", 90); _codeInput = ""; }
        }

        void ClaimWelcome()
        {
            Fx("flyCoins", "#welcome-claim", "[data-test='balance']", 14);
            var r = S.ApplyReferral(_welcome);
            _welcome = null;
            if (r == ReferralResult.Ok) { Sfx("reward"); Fx("confetti", 120); Say(Loc.T("mascot.welcome"), 4); }
        }

        string CrateGlow() => _crateReveal != null && _crateReveal.Rare ? "#ffd27a66" : "#78dcf533";

        void ToggleEffects()
        {
            S.Settings.ReducedEffects = !S.Settings.ReducedEffects;
            Js.InvokeVoid("officinaFx.setEnabled", !S.Settings.ReducedEffects);
            S.Save();
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
            // Rarity shown is the real one from the displayed odds (no fake "almost won" effects).
            double pct = S.Config.Crate.Entries.First(x => x.Id == r.EntryId).ProbabilityPercent;
            _crateReveal = new CrateReveal { Text = _crateResult, Rare = pct <= 5 };
            S.Save();
            _ = CelebrateCrate(_crateReveal.Rare);
        }

        async Task CelebrateCrate(bool rare)
        {
            await Task.Delay(950);   // after the lid animation
            Fx("burstAt", ".crate-art", "build");
            if (rare) Fx("confetti", 120);
        }

        void ClaimOffline()
        {
            Fx("flyCoins", "[data-test='offline-claim']", "[data-test='balance']", 16);
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
