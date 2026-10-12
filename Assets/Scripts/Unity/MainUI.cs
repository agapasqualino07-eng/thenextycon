using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// The whole mobile UI, built from code (portrait, 1080x1920 reference, scales with the screen).
    /// It only READS game state and forwards player intents to the logic; it never edits balances itself and every
    /// value it displays is re-validated by the logic when acted upon.
    /// Split in partial files: MainUI (frame, top bar, navigation, popups), MainUI.Panels, MainUI.Events.
    /// </summary>
    public sealed partial class MainUI : MonoBehaviour
    {
        enum Tab { Factory, Upgrades, Orders, Events, Settings }

        GameSession _s;
        AudioService _audio;
        UnityMockAdService _mockAds;
        DevClock _devClock;
        Action _onReset;

        Canvas _canvas;
        RectTransform _root, _safe;
        Rect _lastSafe;
        float _refreshTimer;

        // top bar
        Text _balanceText, _opsText, _boostText, _phaseText;
        Image _phaseFill;
        // tap + gears
        Button _tapButton;
        Text _tapLabel;
        RectTransform _floatLayer;
        readonly List<GearTile> _gears = new List<GearTile>();
        // navigation / panels
        readonly Dictionary<Tab, RectTransform> _panels = new Dictionary<Tab, RectTransform>();
        readonly Dictionary<Tab, Button> _navButtons = new Dictionary<Tab, Button>();
        readonly Dictionary<Tab, Image> _navDots = new Dictionary<Tab, Image>();
        Tab _tab = Tab.Factory;
        // toasts
        Text _toastText;
        Image _toastBox;
        readonly Queue<string> _toasts = new Queue<string>();
        float _toastUntil;
        // inline "tap again to confirm" state
        string _armedKey = "";
        float _armedUntil;

        static readonly Color[] MachineColors =
        {
            new Color32(255, 170, 40, 255), new Color32(90, 160, 230, 255), new Color32(110, 200, 120, 255), new Color32(60, 190, 190, 255),
            new Color32(170, 120, 230, 255), new Color32(230, 90, 80, 255), new Color32(235, 210, 80, 255), new Color32(120, 220, 245, 255)
        };

        sealed class GearTile
        {
            public string MachineId;
            public Image Gear;
            public Text Level;
            public Spin Spinner;
            public Pulse Pulse;
            public float Dir = 1f;
        }

        // ------------------------------------------------------------------ setup
        public void Setup(GameSession session, AudioService audio, UnityMockAdService mockAds, DevClock devClock, Action onReset)
        {
            _s = session; _audio = audio; _mockAds = mockAds; _devClock = devClock; _onReset = onReset;
            _audio.SfxOn = _s.Settings.SfxOn; _audio.MusicOn = _s.Settings.MusicOn;
            if (_mockAds != null) _mockAds.Presenter = PresentMockAd;
            BuildCanvas();
            BuildTopBar();
            BuildGearStrip();
            BuildTapButton();
            BuildContentArea();
            BuildNav();
            BuildToast();
            BuildFactoryPanel();
            BuildUpgradesPanel();
            BuildOrdersPanel();
            BuildEventsPanel();
            BuildSettingsPanel();
            _s.Signals.MachineLevelChanged += OnMachineLevel;
            _s.Signals.UpgradePurchased += id => { Sound(Sfx.Upgrade); PopObject(_navButtons[Tab.Upgrades].gameObject); Toast(Loc.T("toast.upgrade")); };
            _s.Signals.OrderClaimed += id => { Sound(Sfx.OrderDone); Toast(Loc.T("toast.order_done")); };
            _s.Signals.PhaseChanged += p => { Sound(Sfx.Unlock); Toast(Loc.T("toast.phase", _s.Progression.Phase.Name)); };
            _s.Signals.Toast += key => Toast(Loc.T(key));
            _s.Crates.Granted += (n, src) => { if (n > 0) Toast(Loc.T("toast.crate_earned")); };
            SwitchTab(Tab.Factory);
            RefreshAll();
            ShowStartupPopups();
        }

        void BuildCanvas()
        {
            if (Compat.FindFirst<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)go.transform;
            var bg = UiKit.Box(_root, "Background", UiKit.Bg, rounded: false);
            UiKit.Stretch(bg.rectTransform);
            _safe = UiKit.Rect("SafeArea", _root);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == _lastSafe || Screen.width == 0 || Screen.height == 0) return;
            _lastSafe = sa;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            _safe.offsetMin = Vector2.zero; _safe.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ top bar
        void BuildTopBar()
        {
            var bar = UiKit.Box(_safe, "TopBar", UiKit.Panel);
            UiKit.Anchor(bar.rectTransform, 0, 0.885f, 1, 1, 12, 6, 12, 12);
            _balanceText = UiKit.Label(bar.transform, "0 Cr", 78, UiKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Anchor(_balanceText.rectTransform, 0, 0.5f, 0.68f, 1, 26, 0, 0, 6);
            _opsText = UiKit.Label(bar.transform, "+0/s", 46, UiKit.TextMain, TextAnchor.MiddleRight, FontStyle.Bold, true);
            UiKit.Anchor(_opsText.rectTransform, 0.62f, 0.5f, 1, 1, 0, 0, 26, 6);
            _boostText = UiKit.Label(bar.transform, "", 30, UiKit.Good, TextAnchor.MiddleRight, FontStyle.Bold, true);
            UiKit.Anchor(_boostText.rectTransform, 0.5f, 0.27f, 1, 0.52f, 0, 0, 26, 0);
            _phaseText = UiKit.Label(bar.transform, "", 28, UiKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal, true);
            UiKit.Anchor(_phaseText.rectTransform, 0, 0.27f, 0.6f, 0.52f, 26, 0, 0, 0);
            var back = UiKit.ProgressBar(bar.transform, UiKit.Steel, out _phaseFill);
            UiKit.Anchor(back.rectTransform, 0, 0, 1, 0.2f, 26, 14, 26, 6);
        }

        // ------------------------------------------------------------------ factory strip (gears)
        void BuildGearStrip()
        {
            var strip = UiKit.Box(_safe, "FactoryStrip", new Color32(30, 37, 47, 255));
            UiKit.Anchor(strip.rectTransform, 0, 0.745f, 1, 0.88f, 12, 4, 12, 4);
            var row = UiKit.Rect("Row", strip.transform);
            UiKit.Stretch(row, 8, 8, 8, 8);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            int i = 0;
            foreach (var m in _s.Config.Machines)
            {
                var tile = UiKit.Rect("Tile_" + m.Id, row);
                var gearRect = UiKit.Rect("Gear", tile);
                UiKit.Anchor(gearRect, 0.05f, 0.3f, 0.95f, 1f);
                var img = gearRect.gameObject.AddComponent<Image>();
                img.sprite = SpriteFactory.Gear(6 + i);
                img.preserveAspect = true;
                img.color = MachineColors[i % MachineColors.Length];
                img.raycastTarget = false;
                var spin = gearRect.gameObject.AddComponent<Spin>();
                var pulse = tile.gameObject.AddComponent<Pulse>();
                var lvl = UiKit.Label(tile, "", 26, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold, true);
                UiKit.Anchor(lvl.rectTransform, 0, 0, 1, 0.28f);
                _gears.Add(new GearTile { MachineId = m.Id, Gear = img, Level = lvl, Spinner = spin, Pulse = pulse, Dir = i % 2 == 0 ? 1f : -1f });
                i++;
            }
        }

        // ------------------------------------------------------------------ tap button
        void BuildTapButton()
        {
            _tapButton = UiKit.MakeButton(_safe, Loc.T("tap.button"), UiKit.Accent, OnTapPressed, 44, "TapButton");
            UiKit.Anchor(_tapButton.GetComponent<RectTransform>(), 0, 0.668f, 1, 0.745f, 24, 2, 24, 2);
            _tapLabel = UiKit.ButtonText(_tapButton);
            _tapLabel.color = new Color32(30, 25, 10, 255);
            _floatLayer = UiKit.Rect("FloatLayer", _safe);
            UiKit.Stretch(_floatLayer);
        }

        void OnTapPressed()
        {
            double got = _s.Production.Tap();
            Sound(Sfx.Tap);
            var r = (RectTransform)_tapButton.transform;
            var t = UiKit.Label(_floatLayer, "+" + Fmt.Num(got), 44, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.gameObject.AddComponent<FloatText>();
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(300, 70);
            Vector3 world = r.TransformPoint(new Vector3(UnityEngine.Random.Range(-250f, 250f), 40f, 0));
            rt.position = world;
        }

        // ------------------------------------------------------------------ content area + navigation
        RectTransform _contentArea;

        void BuildContentArea()
        {
            _contentArea = UiKit.Rect("Content", _safe);
            UiKit.Anchor(_contentArea, 0, 0.082f, 1, 0.668f, 8, 4, 8, 4);
        }

        RectTransform NewPanel(Tab tab)
        {
            var p = UiKit.Rect("Panel_" + tab, _contentArea);
            UiKit.Stretch(p);
            _panels[tab] = p;
            return p;
        }

        void BuildNav()
        {
            var nav = UiKit.Box(_safe, "Nav", UiKit.Panel, rounded: false);
            UiKit.Anchor(nav.rectTransform, 0, 0, 1, 0.082f);
            var h = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4; h.padding = new RectOffset(6, 6, 6, 6);
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            AddNav(nav.transform, Tab.Factory, "nav.factory");
            AddNav(nav.transform, Tab.Upgrades, "nav.upgrades");
            AddNav(nav.transform, Tab.Orders, "nav.orders");
            AddNav(nav.transform, Tab.Events, "nav.events");
            AddNav(nav.transform, Tab.Settings, "nav.settings");
        }

        void AddNav(Transform parent, Tab tab, string key)
        {
            var b = UiKit.MakeButton(parent, Loc.T(key), UiKit.PanelLight, () => { Sound(Sfx.Tap); SwitchTab(tab); }, 28, "Nav_" + tab);
            _navButtons[tab] = b;
            var dot = UiKit.Box(b.transform, "Dot", UiKit.Bad);
            UiKit.Anchor(dot.rectTransform, 0.68f, 0.62f, 0.92f, 0.92f);
            dot.raycastTarget = false;
            dot.gameObject.SetActive(false);
            _navDots[tab] = dot;
        }

        void SwitchTab(Tab tab)
        {
            _tab = tab;
            foreach (var kv in _panels) kv.Value.gameObject.SetActive(kv.Key == tab);
            foreach (var kv in _navButtons)
            {
                var cb = kv.Value.colors;
                var c = kv.Key == tab ? UiKit.AccentDark : UiKit.PanelLight;
                cb.normalColor = c; cb.selectedColor = c;
                cb.highlightedColor = Color.Lerp(c, Color.white, 0.12f);
                cb.pressedColor = Color.Lerp(c, Color.black, 0.25f);
                kv.Value.colors = cb;
            }
            RefreshActivePanel();
        }

        // ------------------------------------------------------------------ toast
        void BuildToast()
        {
            _toastBox = UiKit.Box(_safe, "Toast", new Color(0.05f, 0.07f, 0.1f, 0.94f));
            UiKit.Anchor(_toastBox.rectTransform, 0.06f, 0.09f, 0.94f, 0.15f);
            _toastText = UiKit.Label(_toastBox.transform, "", 34, UiKit.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Stretch(_toastText.rectTransform, 16, 6, 16, 6);
            _toastBox.raycastTarget = false;
            _toastBox.gameObject.SetActive(false);
        }

        public void Toast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_toasts.Count < 4) _toasts.Enqueue(text);
        }

        void UpdateToast()
        {
            if (_toastBox.gameObject.activeSelf && Time.unscaledTime >= _toastUntil) _toastBox.gameObject.SetActive(false);
            if (!_toastBox.gameObject.activeSelf && _toasts.Count > 0)
            {
                _toastText.text = _toasts.Dequeue();
                _toastBox.gameObject.SetActive(true);
                _toastUntil = Time.unscaledTime + 2.4f;
            }
        }

        // ------------------------------------------------------------------ refresh
        void Update()
        {
            if (_s == null) return;
            ApplySafeArea();
            UpdateToast();
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0) return;
            _refreshTimer = 0.2f;
            RefreshAll();
        }

        void RefreshAll()
        {
            RefreshTop();
            RefreshGears();
            RefreshActivePanel();
            RefreshDots();
            RefreshPopups();
        }

        static void Set(Text t, string s) { if (t.text != s) t.text = s; }

        void RefreshTop()
        {
            Set(_balanceText, Fmt.Cr(_s.Wallet.Balance));
            Set(_opsText, Loc.T("top.ops", Fmt.Num(_s.Production.OutputPerSecond)));
            double mult = _s.Production.TempMultiplier;
            if (mult > 1.0001)
            {
                double rem = _s.Production.ActiveBoosts.Count == 0 ? 0 : _s.Production.ActiveBoosts.Max(b => b.remaining);
                Set(_boostText, Loc.T("top.boost", Fmt.Mult(mult), Fmt.Time(rem)));
            }
            else Set(_boostText, "");
            Set(_phaseText, Loc.T("top.phase", _s.Progression.Phase.Name));
            UiKit.SetProgress(_phaseFill, (float)_s.Progression.NextPhaseProgress);
            Set(_tapLabel, Loc.T("tap.button") + "  " + Loc.T("tap.value", Fmt.Num(_s.Production.TapValue)));
        }

        void RefreshGears()
        {
            foreach (var g in _gears)
            {
                int lvl = _s.Production.Level(g.MachineId);
                bool unlocked = _s.Production.IsUnlocked(g.MachineId);
                var c = g.Gear.color;
                c.a = lvl > 0 ? 1f : (unlocked ? 0.45f : 0.15f);
                g.Gear.color = c;
                g.Spinner.DegreesPerSecond = lvl > 0 ? Mathf.Min(120f, 18f + 10f * Mathf.Log(1 + lvl, 2f)) * g.Dir : 0f;
                Set(g.Level, lvl > 0 ? lvl.ToString() : (unlocked ? "+" : ""));
            }
        }

        void RefreshDots()
        {
            _navDots[Tab.Orders].gameObject.SetActive(_s.Orders.ClaimableCount > 0);
            bool ev = _s.Experimental.Stage == EventStage.Offered || _s.Experimental.Stage == EventStage.ReadyToCollect
                      || _s.Surge.Stage == EventStage.Offered || _s.Crates.Available > 0;
            _navDots[Tab.Events].gameObject.SetActive(ev);
            _navDots[Tab.Upgrades].gameObject.SetActive(_s.Config.Upgrades.Any(u => _s.Production.IsUpgradeAvailable(u.Id) && _s.Wallet.CanAfford(u.Cost)));
            _navDots[Tab.Factory].gameObject.SetActive(_s.Config.Machines.Any(m => _s.Production.MaxAffordableCount(m.Id) > 0 && _s.Production.Level(m.Id) == 0));
        }

        void RefreshActivePanel()
        {
            switch (_tab)
            {
                case Tab.Factory: RefreshFactory(); break;
                case Tab.Upgrades: RefreshUpgrades(); break;
                case Tab.Orders: RefreshOrders(); break;
                case Tab.Events: RefreshEvents(); break;
                case Tab.Settings: RefreshSettings(); break;
            }
        }

        // ------------------------------------------------------------------ helpers
        void Sound(Sfx s) { if (_audio != null) _audio.Play(s); }
        static void PopObject(GameObject go) { var p = go.GetComponent<Pulse>() ?? go.AddComponent<Pulse>(); p.Play(); }

        void OnMachineLevel(string id, int level)
        {
            Sound(level == 1 ? Sfx.Unlock : Sfx.Buy);
            var g = _gears.FirstOrDefault(x => x.MachineId == id);
            if (g != null) g.Pulse.Play();
        }

        bool Armed(string key) => _armedKey == key && Time.unscaledTime < _armedUntil;
        void Arm(string key) { _armedKey = key; _armedUntil = Time.unscaledTime + 4f; }

        /// <summary>Fixed-height card with a title; returns the card root (VerticalLayoutGroup child).</summary>
        RectTransform Card(Transform parent, string name, Color color, float spacing = 10)
        {
            var box = UiKit.Box(parent, name, color);
            UiKit.VGroup(box.gameObject, spacing, 18);
            return box.rectTransform;
        }

        Text CardText(Transform card, string text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = UiKit.Label(card, text, size, color, anchor, style);
            return t;
        }

        Button CardButton(Transform card, string label, Color color, Action onClick, float height = 100, int size = 34)
        {
            var b = UiKit.MakeButton(card, label, color, onClick, size);
            UiKit.Size(b.gameObject, prefH: height, minH: height);
            return b;
        }
    }
}
