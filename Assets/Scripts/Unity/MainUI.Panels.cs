using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OfficinaTycoon.UnityLayer
{
    public sealed partial class MainUI
    {
        // =================================================================== FACTORY (machines)
        sealed class MachineRowUi
        {
            public MachineDef Def; public int Index;
            public GameObject Root; public Image Gear; public Text Name, Info; public Button Buy;
        }

        readonly List<MachineRowUi> _machineRows = new List<MachineRowUi>();
        readonly List<Button> _modeButtons = new List<Button>();
        int _buyMode = 1;   // 1, 10 or 0 (= max affordable)
        Button _boostAdButton;
        Text _boostAdNote;

        void BuildFactoryPanel()
        {
            var panel = NewPanel(Tab.Factory);
            var content = UiKit.ScrollView(panel, "Scroll", out _);

            var head = UiKit.Rect("BuyMode", content);
            UiKit.Size(head.gameObject, prefH: 84, minH: 84);
            UiKit.HGroup(head.gameObject, 10);
            var lbl = UiKit.Label(head, Loc.T("machine.buy", ""), 30, UiKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(lbl.gameObject, prefW: 200);
            int[] modes = { 1, 10, 0 };
            foreach (int m in modes)
            {
                int mode = m;
                var b = UiKit.MakeButton(head, mode == 0 ? Loc.T("machine.max") : Loc.T("machine.buy_x", mode), UiKit.PanelLight, () => { _buyMode = mode; Sound(Sfx.Tap); RefreshFactory(); }, 32, "Mode" + mode);
                UiKit.Size(b.gameObject, prefW: 190);
                _modeButtons.Add(b);
            }

            // Optional rewarded boost
            var card = Card(content, "BoostAd", UiKit.Panel, 6);
            _boostAdButton = CardButton(card, "", UiKit.Good, OnBoostAdPressed, 96, 30);
            _boostAdNote = CardText(card, Loc.T("boost.ad_note"), 26, UiKit.TextDim);

            int i = 0;
            foreach (var def in _s.Config.Machines)
            {
                var row = new MachineRowUi { Def = def, Index = i };
                var box = UiKit.Box(content, "Row_" + def.Id, UiKit.Panel);
                UiKit.Size(box.gameObject, prefH: 190, minH: 190);
                row.Root = box.gameObject;
                row.Gear = UiKit.Box(box.transform, "Gear", MachineColors[i % MachineColors.Length], rounded: false);
                row.Gear.sprite = SpriteFactory.Gear(6 + i); row.Gear.preserveAspect = true; row.Gear.raycastTarget = false;
                UiKit.Anchor(row.Gear.rectTransform, 0.01f, 0.1f, 0.17f, 0.9f, 8, 0, 0, 0);
                row.Name = UiKit.Label(box.transform, def.Name, 40, UiKit.TextMain, TextAnchor.LowerLeft, FontStyle.Bold, true);
                UiKit.Anchor(row.Name.rectTransform, 0.19f, 0.5f, 0.64f, 0.96f);
                row.Info = UiKit.Label(box.transform, "", 28, UiKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, true);
                UiKit.Anchor(row.Info.rectTransform, 0.19f, 0.04f, 0.64f, 0.5f);
                string id = def.Id;
                row.Buy = UiKit.MakeButton(box.transform, "", UiKit.Good, () => OnBuyMachine(id), 30, "Buy_" + id);
                UiKit.Anchor(row.Buy.GetComponent<RectTransform>(), 0.655f, 0.14f, 0.985f, 0.86f);
                _machineRows.Add(row);
                i++;
            }
        }

        int BuyCount(MachineDef d)
        {
            int lvl = _s.Production.Level(d.Id), room = d.MaxLevel - lvl;
            if (room <= 0) return 0;
            if (_buyMode == 0) return Math.Max(1, _s.Production.MaxAffordableCount(d.Id));
            return Math.Min(_buyMode, room);
        }

        void OnBuyMachine(string id)
        {
            var def = _s.Config.Machines.First(m => m.Id == id);
            int n = BuyCount(def);
            if (n <= 0) return;
            if (!_s.Production.TryBuy(id, n)) { Sound(Sfx.Error); Toast(Loc.T("toast.not_enough")); return; }
            RefreshTop(); RefreshFactory();
        }

        void RefreshFactory()
        {
            for (int k = 0; k < _modeButtons.Count; k++)
            {
                int mode = k == 0 ? 1 : (k == 1 ? 10 : 0);
                var c = _buyMode == mode ? UiKit.AccentDark : UiKit.PanelLight;
                var cb = _modeButtons[k].colors; cb.normalColor = c; cb.selectedColor = c; _modeButtons[k].colors = cb;
            }
            RefreshBoostAd();
            foreach (var r in _machineRows)
            {
                var d = r.Def;
                int lvl = _s.Production.Level(d.Id);
                bool unlocked = _s.Production.IsUnlocked(d.Id);
                var gc = r.Gear.color; gc.a = lvl > 0 ? 1f : (unlocked ? 0.5f : 0.18f); r.Gear.color = gc;
                if (!unlocked)
                {
                    Set(r.Name, "???");
                    Set(r.Info, UnlockText(d));
                    r.Buy.gameObject.SetActive(false);
                    continue;
                }
                r.Buy.gameObject.SetActive(true);
                Set(r.Name, d.Name);
                if (lvl >= d.MaxLevel)
                {
                    Set(r.Info, Loc.T("machine.maxed"));
                    r.Buy.interactable = false; UiKit.SetButtonLabel(r.Buy, Loc.T("machine.maxed"));
                    continue;
                }
                string info = Loc.T("machine.level", lvl) + "  " + Loc.T("machine.output", Fmt.Num(_s.Production.MachineOutput(d.Id))) + "\n" + Loc.T("machine.produces", d.Good);
                Set(r.Info, info);
                int n = BuyCount(d);
                double cost = _s.Production.NextCost(d.Id, n);
                UiKit.SetButtonLabel(r.Buy, Loc.T("machine.buy", Loc.T("machine.buy_x", n)) + "\n" + Fmt.Cr(cost));
                r.Buy.interactable = _s.Wallet.CanAfford(cost);
            }
        }

        string UnlockText(MachineDef d)
        {
            if (_s.Progression.PhaseIndex < d.UnlockPhase) return Loc.T("machine.req_phase", _s.Config.Phases[d.UnlockPhase].Name);
            var req = _s.Config.Machines.FirstOrDefault(m => m.Id == d.UnlockRequiresMachineId);
            return req != null ? Loc.T("machine.req_machine", req.Name, d.UnlockRequiresLevel) : Loc.T("machine.locked");
        }

        // ---- optional rewarded boost
        void OnBoostAdPressed()
        {
            _s.Monetization.RequestBoost((granted, reason) =>
            {
                if (granted) { Sound(Sfx.Reward); Toast(Loc.T("toast.boost_ad")); }
                else if (reason != AdFailReason.None) Toast(AdReasonText(reason));
            });
            RefreshBoostAd();
        }

        void RefreshBoostAd()
        {
            double rem = _s.Production.BoostRemaining("ad");
            var can = _s.Monetization.CanOfferBoost();
            string label = Loc.T("boost.ad", Fmt.Mult(_s.Config.Ads.BoostMultiplier), Fmt.Time(_s.Config.Ads.BoostSeconds));
            if (rem > 0) label = Loc.T("boost.ad_active") + " " + Fmt.Time(rem);
            UiKit.SetButtonLabel(_boostAdButton, label);
            _boostAdButton.interactable = can == AdFailReason.None;
            Set(_boostAdNote, can == AdFailReason.None || can == AdFailReason.Busy ? Loc.T("boost.ad_note") : AdReasonText(can));
        }

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

        // =================================================================== UPGRADES (+ overhaul)
        sealed class UpgradeRowUi { public UpgradeDef Def; public Text Name, Info; public Button Buy; }
        sealed class OverhaulRowUi { public MachineDef Def; public GameObject Root; public Text Name; public Button Buy; }
        readonly List<UpgradeRowUi> _upgradeRows = new List<UpgradeRowUi>();
        readonly List<OverhaulRowUi> _overhaulRows = new List<OverhaulRowUi>();
        Text _overhaulHeader;

        void BuildUpgradesPanel()
        {
            var panel = NewPanel(Tab.Upgrades);
            var content = UiKit.ScrollView(panel, "Scroll", out _);
            var h = UiKit.Label(content, Loc.T("upg.header"), 40, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(h.gameObject, prefH: 64, minH: 64);
            foreach (var def in _s.Config.Upgrades)
            {
                var box = UiKit.Box(content, "Upg_" + def.Id, UiKit.Panel);
                UiKit.Size(box.gameObject, prefH: 170, minH: 170);
                var row = new UpgradeRowUi { Def = def };
                row.Name = UiKit.Label(box.transform, def.Name, 36, UiKit.TextMain, TextAnchor.LowerLeft, FontStyle.Bold, true);
                UiKit.Anchor(row.Name.rectTransform, 0.03f, 0.55f, 0.64f, 0.96f);
                row.Info = UiKit.Label(box.transform, def.Description, 27, UiKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, true);
                UiKit.Anchor(row.Info.rectTransform, 0.03f, 0.04f, 0.64f, 0.55f);
                string id = def.Id;
                row.Buy = UiKit.MakeButton(box.transform, "", UiKit.Good, () => OnBuyUpgrade(id), 30, "BuyUpg_" + id);
                UiKit.Anchor(row.Buy.GetComponent<RectTransform>(), 0.655f, 0.16f, 0.985f, 0.84f);
                _upgradeRows.Add(row);
            }

            _overhaulHeader = UiKit.Label(content, "", 36, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(_overhaulHeader.gameObject, prefH: 70, minH: 70);
            var hint = UiKit.Label(content, Loc.T("overhaul.hint", Fmt.Percent(_s.Config.Economy.OverhaulBonus * 100)), 26, UiKit.TextDim, TextAnchor.UpperLeft);
            foreach (var def in _s.Config.Machines)
            {
                var box = UiKit.Box(content, "Ovh_" + def.Id, UiKit.Panel);
                UiKit.Size(box.gameObject, prefH: 120, minH: 120);
                var row = new OverhaulRowUi { Def = def, Root = box.gameObject };
                row.Name = UiKit.Label(box.transform, "", 30, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
                UiKit.Anchor(row.Name.rectTransform, 0.03f, 0.05f, 0.62f, 0.95f);
                string id = def.Id;
                row.Buy = UiKit.MakeButton(box.transform, "", UiKit.Steel, () => OnOverhaul(id), 28, "Ovh_" + id);
                UiKit.Anchor(row.Buy.GetComponent<RectTransform>(), 0.64f, 0.14f, 0.985f, 0.86f);
                _overhaulRows.Add(row);
            }
        }

        void OnBuyUpgrade(string id)
        {
            if (!_s.Production.TryBuyUpgrade(id)) { Sound(Sfx.Error); Toast(Loc.T("toast.not_enough")); return; }
            RefreshTop(); RefreshUpgrades();
        }

        void OnOverhaul(string id)
        {
            if (!_s.Production.TryOverhaul(id)) { Sound(Sfx.Error); return; }
            Sound(Sfx.Upgrade); Toast(Loc.T("toast.overhaul"));
            RefreshUpgrades();
        }

        void RefreshUpgrades()
        {
            foreach (var r in _upgradeRows)
            {
                var d = r.Def;
                bool owned = _s.Production.HasUpgrade(d.Id);
                bool avail = _s.Production.IsUpgradeAvailable(d.Id);
                if (owned)
                {
                    Set(r.Info, d.Description);
                    UiKit.SetButtonLabel(r.Buy, Loc.T("upg.owned")); r.Buy.interactable = false;
                }
                else if (avail)
                {
                    Set(r.Info, d.Description);
                    UiKit.SetButtonLabel(r.Buy, Loc.T("upg.buy") + "\n" + Fmt.Cr(d.Cost));
                    r.Buy.interactable = _s.Wallet.CanAfford(d.Cost);
                }
                else
                {
                    string why;
                    if (_s.Progression.PhaseIndex < d.UnlockPhase) why = Loc.T("upg.locked_phase", _s.Config.Phases[d.UnlockPhase].Name);
                    else { var m = _s.Config.Machines.FirstOrDefault(x => x.Id == d.RequiresMachineId); why = Loc.T("upg.locked_machine", m != null ? m.Name : "?", d.RequiresMachineLevel); }
                    Set(r.Info, why);
                    UiKit.SetButtonLabel(r.Buy, Fmt.Cr(d.Cost)); r.Buy.interactable = false;
                }
            }
            Set(_overhaulHeader, Loc.T("overhaul.header", _s.Production.Parts));
            foreach (var r in _overhaulRows)
            {
                int lvl = _s.Production.Level(r.Def.Id);
                r.Root.SetActive(lvl > 0);
                if (lvl <= 0) continue;
                int ol = _s.Production.OverhaulLevel(r.Def.Id);
                Set(r.Name, Loc.T("overhaul.row", r.Def.Name, ol, _s.Config.Economy.OverhaulMaxLevel));
                int cost = _s.Production.OverhaulPartsCost(r.Def.Id);
                if (cost == int.MaxValue) { UiKit.SetButtonLabel(r.Buy, Loc.T("overhaul.max")); r.Buy.interactable = false; }
                else { UiKit.SetButtonLabel(r.Buy, Loc.T("overhaul.buy", cost)); r.Buy.interactable = _s.Production.Parts >= cost; }
            }
        }

        // =================================================================== ORDERS
        sealed class OrderRowUi { public string Id; public Image Fill; public Text Progress, Timer; public Button Claim; }
        readonly List<OrderRowUi> _orderRows = new List<OrderRowUi>();
        RectTransform _ordersContent;
        string _ordersSignature = "";
        Text _ordersEmpty;

        void BuildOrdersPanel()
        {
            var panel = NewPanel(Tab.Orders);
            _ordersContent = UiKit.ScrollView(panel, "Scroll", out _);
            RebuildOrders(new List<OrderSystem.OrderView>());
        }

        void RebuildOrders(List<OrderSystem.OrderView> views)
        {
            for (int i = _ordersContent.childCount - 1; i >= 0; i--) Destroy(_ordersContent.GetChild(i).gameObject);
            _orderRows.Clear();
            var h = UiKit.Label(_ordersContent, Loc.T("orders.header"), 40, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(h.gameObject, prefH: 64, minH: 64);
            if (views.Count == 0)
            {
                _ordersEmpty = UiKit.Label(_ordersContent, Loc.T("orders.empty"), 32, UiKit.TextDim, TextAnchor.UpperLeft);
                return;
            }
            foreach (var v in views)
            {
                var box = UiKit.Box(_ordersContent, "Order_" + v.Def.Id, UiKit.Panel);
                UiKit.Size(box.gameObject, prefH: 250, minH: 250);
                var title = UiKit.Label(box.transform, v.Def.Title, 38, UiKit.TextMain, TextAnchor.LowerLeft, FontStyle.Bold, true);
                UiKit.Anchor(title.rectTransform, 0.03f, 0.72f, 0.66f, 0.97f);
                var desc = UiKit.Label(box.transform, v.Def.Description, 27, UiKit.TextDim, TextAnchor.UpperLeft, FontStyle.Normal, true);
                UiKit.Anchor(desc.rectTransform, 0.03f, 0.47f, 0.66f, 0.72f);
                var back = UiKit.ProgressBar(box.transform, UiKit.Good, out var fill);
                UiKit.Anchor(back.rectTransform, 0.03f, 0.30f, 0.66f, 0.42f);
                var prog = UiKit.Label(box.transform, "", 26, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Normal, true);
                UiKit.Anchor(prog.rectTransform, 0.03f, 0.03f, 0.40f, 0.28f);
                var timer = UiKit.Label(box.transform, "", 26, UiKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
                UiKit.Anchor(timer.rectTransform, 0.40f, 0.03f, 0.66f, 0.28f);
                string reward = Loc.T("orders.reward", Fmt.Cr(v.RewardCurrency));
                if (v.Def.RewardCrates > 0) reward += Loc.T("orders.reward_crate", v.Def.RewardCrates);
                if (v.Def.RewardParts > 0) reward += Loc.T("orders.reward_parts", v.Def.RewardParts);
                var rw = UiKit.Label(box.transform, reward, 26, UiKit.Accent, TextAnchor.UpperRight, FontStyle.Bold, true);
                UiKit.Anchor(rw.rectTransform, 0.67f, 0.66f, 0.98f, 0.97f);
                string id = v.Def.Id;
                var claim = UiKit.MakeButton(box.transform, Loc.T("orders.claim"), UiKit.Good, () => OnClaimOrder(id), 34, "Claim_" + id);
                UiKit.Anchor(claim.GetComponent<RectTransform>(), 0.68f, 0.08f, 0.98f, 0.60f);
                _orderRows.Add(new OrderRowUi { Id = id, Fill = fill, Progress = prog, Timer = timer, Claim = claim });
            }
        }

        void OnClaimOrder(string id)
        {
            if (_s.Orders.TryClaim(id)) { _s.Save(); RefreshTop(); }
            _ordersSignature = "";   // force rebuild
            RefreshOrders();
        }

        void RefreshOrders()
        {
            var views = _s.Orders.Views().ToList();
            string sig = string.Join("|", views.Select(v => v.Def.Id + ":" + v.Status));
            if (sig != _ordersSignature)
            {
                _ordersSignature = sig;
                RebuildOrders(views);
            }
            for (int i = 0; i < _orderRows.Count && i < views.Count; i++)
            {
                var v = views[i]; var r = _orderRows[i];
                UiKit.SetProgress(r.Fill, (float)v.Fraction);
                Set(r.Progress, v.Status == OrderStatus.Completed ? Loc.T("orders.claim") : Loc.T("orders.progress", Fmt.Num(v.Progress), Fmt.Num(v.Def.Target)));
                Set(r.Timer, v.RemainingSeconds > 0 ? Loc.T("orders.deadline", Fmt.Time(v.RemainingSeconds)) : "");
                r.Claim.interactable = v.Status == OrderStatus.Completed;
                UiKit.SetButtonLabel(r.Claim, v.Status == OrderStatus.Completed ? Loc.T("orders.claim") : Loc.T("orders.in_progress"));
            }
        }

        // =================================================================== SETTINGS
        Button _sfxButton, _musicButton, _resetButton, _devToggle;
        Text _analyticsLog;
        RectTransform _devSection;

        void BuildSettingsPanel()
        {
            var panel = NewPanel(Tab.Settings);
            var content = UiKit.ScrollView(panel, "Scroll", out _);
            var h = UiKit.Label(content, Loc.T("settings.header"), 40, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(h.gameObject, prefH: 64, minH: 64);

            _sfxButton = SettingsButton(content, "", () =>
            {
                _s.Settings.SfxOn = !_s.Settings.SfxOn; _audio.SfxOn = _s.Settings.SfxOn; _s.Save(); Sound(Sfx.Tap); RefreshSettings();
            });
            _musicButton = SettingsButton(content, "", () =>
            {
                _s.Settings.MusicOn = !_s.Settings.MusicOn; _audio.MusicOn = _s.Settings.MusicOn; _s.Save(); RefreshSettings();
            });
            SettingsButton(content, Loc.T("settings.privacy"), () => ShowPrivacy(null));
            _resetButton = SettingsButton(content, Loc.T("settings.reset"), OnResetPressed, UiKit.Bad);

            var ver = UiKit.Label(content, Loc.T("app.version", Application.version) + "\n" + Loc.T("settings.mock_note"), 26, UiKit.TextDim, TextAnchor.UpperLeft);

            _devSection = UiKit.Rect("Dev", content);
            UiKit.VGroup(_devSection.gameObject, 10, 0);
            bool dev = Debug.isDebugBuild || Application.isEditor;
            _devSection.gameObject.SetActive(dev);
            if (!dev) return;
            var dh = UiKit.Label(_devSection, Loc.T("settings.dev"), 32, UiKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(dh.gameObject, prefH: 56, minH: 56);
            SettingsButton(_devSection, "+1K Cr", () => { _s.Wallet.Add(1e3, "dev"); }, UiKit.Steel);
            SettingsButton(_devSection, "+1M Cr", () => { _s.Wallet.Add(1e6, "dev"); }, UiKit.Steel);
            SettingsButton(_devSection, "+1B Cr", () => { _s.Wallet.Add(1e9, "dev"); }, UiKit.Steel);
            SettingsButton(_devSection, "Simula 1 ora offline", () => SimulateAway(3600), UiKit.Steel);
            SettingsButton(_devSection, "Simula 10 ore offline (limite)", () => SimulateAway(36000), UiKit.Steel);
            SettingsButton(_devSection, "Simula orologio indietro di 1 ora", () => SimulateAway(-3600), UiKit.Steel);
            SettingsButton(_devSection, "Anticipa eventi (azzera attese)", () => { _s.Experimental.Advance(1e6); _s.Surge.Advance(1e6); }, UiKit.Steel);
            SettingsButton(_devSection, "Vai alla fase successiva", () =>
            {
                int next = Math.Min(_s.Config.Phases.Count - 1, _s.Progression.PhaseIndex + 1);
                _s.Wallet.Add(Math.Max(1, _s.Config.Phases[next].TotalEarnedRequired - _s.Wallet.TotalEarned + 1), "dev"); _s.Progression.Update();
            }, UiKit.Steel);
            SettingsButton(_devSection, "+1 cassa gratuita", () => _s.Crates.Grant(1, "dev"), UiKit.Steel);
            _devToggle = SettingsButton(_devSection, "", () =>
            {
                if (_mockAds == null) return;
                var all = (MockAdBehavior[])Enum.GetValues(typeof(MockAdBehavior));
                _mockAds.Behavior = all[(Array.IndexOf(all, _mockAds.Behavior) + 1) % all.Length];
                RefreshSettings();
            }, UiKit.Steel);
            SettingsButton(_devSection, "Mostra/nascondi log analytics", () => { _s.Settings.DevShowAnalytics = !_s.Settings.DevShowAnalytics; RefreshSettings(); }, UiKit.Steel);
            _analyticsLog = UiKit.Label(_devSection, "", 22, UiKit.TextDim, TextAnchor.UpperLeft);
        }

        Button SettingsButton(Transform parent, string label, Action onClick, Color? color = null)
        {
            var b = UiKit.MakeButton(parent, label, color ?? UiKit.PanelLight, onClick, 32);
            UiKit.Size(b.gameObject, prefH: 96, minH: 96);
            return b;
        }

        void SimulateAway(double seconds)
        {
            // Dev only: pretend the app was closed for `seconds` by shifting the dev clock, then run the normal resume path.
            _s.Pause();
            _devClock.OffsetSeconds += seconds;
            _s.Resume();
            ShowOfflineIfPending();
        }

        void RefreshSettings()
        {
            UiKit.SetButtonLabel(_sfxButton, Loc.T("settings.sfx") + ": " + Loc.T(_s.Settings.SfxOn ? "settings.on" : "settings.off"));
            UiKit.SetButtonLabel(_musicButton, Loc.T("settings.music") + ": " + Loc.T(_s.Settings.MusicOn ? "settings.on" : "settings.off"));
            bool armed = Armed("reset");
            UiKit.SetButtonLabel(_resetButton, armed ? Loc.T("settings.reset_confirm") : Loc.T("settings.reset"));
            if (_devToggle != null && _mockAds != null) UiKit.SetButtonLabel(_devToggle, "Annuncio mock: " + _mockAds.Behavior);
            if (_analyticsLog != null)
            {
                var da = _s.Analytics as DebugAnalytics;
                _analyticsLog.gameObject.SetActive(_s.Settings.DevShowAnalytics && da != null);
                if (da != null && _s.Settings.DevShowAnalytics)
                    Set(_analyticsLog, string.Join("\n", da.Records.Reverse().Take(14).Select(r => r.ToString())));
            }
        }

        void OnResetPressed()
        {
            if (!Armed("reset")) { Arm("reset"); RefreshSettings(); return; }
            _armedKey = "";
            _onReset?.Invoke();          // owner (bootstrap) wipes the save and restarts the session
        }

        // =================================================================== POPUPS
        GameObject _offlinePopup, _privacyPopup, _adPopup;
        Text _offAway, _offBase, _offStatus;
        Button _offClaim, _offAd;
        string _offAdMessage = "";
        bool _offerShownLogged;

        RectTransform Modal(string name, float heightFraction, out GameObject root)
        {
            var dim = UiKit.Box(_root, name, new Color(0, 0, 0, 0.78f), rounded: false);
            UiKit.Stretch(dim.rectTransform);
            root = dim.gameObject;
            var box = UiKit.Box(dim.transform, "Box", UiKit.Panel);
            float half = heightFraction / 2f;
            UiKit.Anchor(box.rectTransform, 0.06f, 0.5f - half, 0.94f, 0.5f + half);
            var content = UiKit.Rect("Content", box.transform);
            UiKit.Stretch(content, 28, 28, 28, 28);
            UiKit.VGroup(content.gameObject, 16, 0);
            return content;
        }

        void ShowStartupPopups()
        {
            var st = _s.LastLoad != null ? _s.LastLoad.Status : LoadStatus.NewGame;
            if (st == LoadStatus.CorruptStartedNew) Toast(Loc.T("toast.load_corrupt"));
            else if (st == LoadStatus.TooNewStartedNew) Toast(Loc.T("toast.load_newer"));
            else if (st == LoadStatus.RecoveredFromBackup) Toast(Loc.T("toast.load_recovered"));
            if (!_s.Settings.ConsentAsked) ShowPrivacy(ShowOfflineIfPending);
            else ShowOfflineIfPending();
        }

        public void OnAppResumed()
        {
            if (_s == null) return;
            ShowOfflineIfPending();
            RefreshAll();
        }

        void ShowPrivacy(Action onDone)
        {
            if (_privacyPopup != null) return;
            var c = Modal("PrivacyModal", 0.62f, out _privacyPopup);
            var title = UiKit.Label(c, Loc.T("privacy.title"), 46, UiKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(title.gameObject, prefH: 70, minH: 70);
            UiKit.Label(c, Loc.T("privacy.body"), 30, UiKit.TextMain, TextAnchor.UpperLeft);
            void Choose(bool personalized, bool optOut)
            {
                _s.Settings.ConsentAsked = true; _s.Settings.PrivacyAccepted = true;
                _s.Settings.PersonalizedAds = personalized; _s.Settings.AdsOptOut = optOut;
                _s.Save();
                Destroy(_privacyPopup); _privacyPopup = null;
                onDone?.Invoke();
            }
            CardButton(c, Loc.T("privacy.personalized"), UiKit.Good, () => Choose(true, false), 96, 30);
            CardButton(c, Loc.T("privacy.basic"), UiKit.Steel, () => Choose(false, false), 96, 30);
            CardButton(c, Loc.T("privacy.none"), UiKit.PanelLight, () => Choose(false, true), 96, 30);
            UiKit.Label(c, Loc.T("privacy.later"), 24, UiKit.TextDim, TextAnchor.UpperLeft);
        }

        void ShowOfflineIfPending()
        {
            if (_privacyPopup != null || _offlinePopup != null || _adPopup != null) return;
            var p = _s.Offline.Pending;
            if (p == null)
            {
                if (_s.Offline.ClockAnomaly) Toast(Loc.T("offline.anomaly"));
                return;
            }
            var c = Modal("OfflineModal", 0.58f, out _offlinePopup);
            var title = UiKit.Label(c, Loc.T("offline.title"), 52, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Size(title.gameObject, prefH: 80, minH: 80);
            _offAway = UiKit.Label(c, "", 32, UiKit.TextDim, TextAnchor.MiddleCenter);
            _offBase = UiKit.Label(c, "", 44, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Size(_offBase.gameObject, prefH: 70, minH: 70);
            _offClaim = CardButton(c, "", UiKit.Good, OnClaimOffline, 110, 38);
            _offAd = CardButton(c, "", UiKit.Steel, OnOfflineAd, 100, 30);
            _offStatus = UiKit.Label(c, "", 26, UiKit.TextDim, TextAnchor.UpperCenter);
            _offAdMessage = ""; _offerShownLogged = false;
            RefreshOfflinePopup();
        }

        void RefreshPopups()
        {
            if (_offlinePopup != null) RefreshOfflinePopup();
            if (_adPopup != null) UpdateMockAd();
        }

        void RefreshOfflinePopup()
        {
            var p = _s.Offline.Pending;
            if (p == null) { CloseOffline(); return; }
            string away = Loc.T("offline.away", Fmt.Time(p.Seconds));
            if (p.HitCap) away += "\n" + Loc.T("offline.cap", Fmt.Time(_s.Production.OfflineMaxSeconds));
            Set(_offAway, away);
            Set(_offBase, Loc.T("offline.base", Fmt.Cr(p.Base)));
            UiKit.SetButtonLabel(_offClaim, p.BonusGranted ? Loc.T("offline.claim_bonus", Fmt.Cr(p.Base + p.Bonus)) : Loc.T("offline.claim", Fmt.Cr(p.Base)));
            _offClaim.interactable = true;   // the base reward never depends on ads
            if (p.BonusGranted)
            {
                _offAd.gameObject.SetActive(false);
                Set(_offStatus, Loc.T("offline.ad_done"));
                return;
            }
            _offAd.gameObject.SetActive(true);
            var can = _s.Monetization.CanOfferOfflineBonus();
            UiKit.SetButtonLabel(_offAd, Loc.T("offline.ad", Fmt.Cr(p.Bonus)));
            _offAd.interactable = can == AdFailReason.None;
            if (can == AdFailReason.None && !_offerShownLogged)
            {
                _offerShownLogged = true;
                _s.Monetization.NotifyOfferShown(MonetizationService.PlacementOffline);
            }
            string status = !string.IsNullOrEmpty(_offAdMessage) ? _offAdMessage
                          : (can == AdFailReason.None ? Loc.T("offline.ad_note") : AdReasonText(can));
            Set(_offStatus, status);
        }

        void OnClaimOffline()
        {
            double got = _s.ClaimOffline();
            if (got > 0) { Sound(Sfx.Reward); Toast("+" + Fmt.Cr(got)); }
            CloseOffline();
        }

        void OnOfflineAd()
        {
            _offAdMessage = Loc.T("offline.ad_loading");
            var r = _s.Monetization.RequestOfflineBonus((granted, reason) =>
            {
                if (granted) { Sound(Sfx.Reward); _offAdMessage = ""; }
                else _offAdMessage = AdReasonText(reason == AdFailReason.None ? AdFailReason.Error : reason);
            });
            if (r != AdFailReason.None) _offAdMessage = AdReasonText(r);
            RefreshOfflinePopup();
        }

        void CloseOffline()
        {
            if (_offlinePopup != null) Destroy(_offlinePopup);
            _offlinePopup = null;
        }

        // ---- mock ad overlay (development only)
        IRewardedAdCallbacks _adCallbacks;
        Text _adText;
        float _adEnds, _adAutoClose;
        const float MockAdSeconds = 3f;

        void PresentMockAd(IRewardedAdCallbacks cb, MockAdBehavior behavior)
        {
            _adCallbacks = cb;
            var c = Modal("MockAd", 0.45f, out _adPopup);
            var t = UiKit.Label(c, Loc.T("ad.mock.title"), 44, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Size(t.gameObject, prefH: 80, minH: 80);
            _adText = UiKit.Label(c, "", 32, UiKit.TextMain, TextAnchor.MiddleCenter);
            CardButton(c, Loc.T("ad.mock.close"), UiKit.PanelLight, CloseMockAdEarly, 100, 30);
            _adEnds = Time.unscaledTime + MockAdSeconds;
            _adAutoClose = behavior == MockAdBehavior.CloseEarly ? Time.unscaledTime + 1f : float.MaxValue;
        }

        void UpdateMockAd()
        {
            if (_adCallbacks == null) return;
            if (Time.unscaledTime >= _adAutoClose) { CloseMockAdEarly(); return; }
            float left = _adEnds - Time.unscaledTime;
            if (left <= 0)
            {
                var cb = _adCallbacks; _adCallbacks = null;
                Destroy(_adPopup); _adPopup = null;
                cb.OnRewardEarned();    // confirmed completion
                cb.OnClosed();
                return;
            }
            Set(_adText, Loc.T("ad.mock.body", Mathf.CeilToInt(left) + "s"));
        }

        void CloseMockAdEarly()
        {
            var cb = _adCallbacks; _adCallbacks = null;
            if (_adPopup != null) Destroy(_adPopup);
            _adPopup = null;
            cb?.OnClosed();             // closed WITHOUT reward
        }
    }
}
