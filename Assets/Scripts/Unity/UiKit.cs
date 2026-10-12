using System;
using UnityEngine;
using UnityEngine.UI;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>Small uGUI builder used to construct the whole UI from code (no scene/prefab dependencies).</summary>
    public static class UiKit
    {
        // Industrial palette (contrast checked against white text on the dark backgrounds).
        public static readonly Color Bg = new Color32(22, 27, 34, 255);
        public static readonly Color Panel = new Color32(38, 46, 58, 255);
        public static readonly Color PanelLight = new Color32(52, 63, 78, 255);
        public static readonly Color Accent = new Color32(255, 170, 40, 255);     // safety orange
        public static readonly Color AccentDark = new Color32(200, 120, 10, 255);
        public static readonly Color Good = new Color32(70, 170, 100, 255);
        public static readonly Color Bad = new Color32(200, 70, 60, 255);
        public static readonly Color Disabled = new Color32(70, 76, 86, 255);
        public static readonly Color TextMain = new Color32(240, 243, 247, 255);
        public static readonly Color TextDim = new Color32(165, 175, 190, 255);
        public static readonly Color Steel = new Color32(120, 150, 180, 255);

        static Font _font;
        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    // Unity 2022.2+/6: LegacyRuntime.ttf replaces Arial.ttf. Fall back for older editors.
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                }
                return _font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform r, float xMin, float yMin, float xMax, float yMax, float padL = 0, float padB = 0, float padR = 0, float padT = 0)
        {
            r.anchorMin = new Vector2(xMin, yMin); r.anchorMax = new Vector2(xMax, yMax);
            r.offsetMin = new Vector2(padL, padB); r.offsetMax = new Vector2(-padR, -padT);
        }

        public static Image Box(Transform parent, string name, Color color, bool rounded = true)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = SpriteFactory.RoundedRect(); img.type = Image.Type.Sliced; }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
                                 FontStyle style = FontStyle.Normal, bool bestFit = false)
        {
            var r = Rect("Label", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            if (bestFit) { t.resizeTextForBestFit = true; t.resizeTextMinSize = Mathf.Max(14, size / 2); t.resizeTextMaxSize = size; }
            return t;
        }

        /// <summary>Button with touch feedback (colour tint + press scale) and a best-fit label.</summary>
        public static Button MakeButton(Transform parent, string label, Color color, Action onClick, int fontSize = 34, string name = "Button")
        {
            var img = Box(parent, name, Color.white);
            img.name = name;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = color;
            cb.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            cb.pressedColor = Color.Lerp(color, Color.black, 0.25f);
            cb.selectedColor = color;
            cb.disabledColor = Disabled;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.transition = Selectable.Transition.ColorTint;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = Label(img.transform, label, fontSize, TextMain, TextAnchor.MiddleCenter, FontStyle.Bold, bestFit: true);
            Stretch(t.rectTransform, 8, 4, 8, 4);
            btn.gameObject.AddComponent<PressScale>();
            return btn;
        }

        public static Text ButtonText(Button b) => b.GetComponentInChildren<Text>();
        public static void SetButtonLabel(Button b, string label) { var t = ButtonText(b); if (t != null && t.text != label) t.text = label; }

        public static LayoutElement Size(GameObject go, float minH = -1, float prefH = -1, float prefW = -1, float flexW = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (minH >= 0) le.minHeight = minH;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        public static VerticalLayoutGroup VGroup(GameObject go, float spacing = 12, int pad = 0, bool controlChildW = true)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childControlWidth = controlChildW; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HGroup(GameObject go, float spacing = 10, int pad = 0)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            return h;
        }

        /// <summary>Vertical scroll view; returns the content rect (a VerticalLayoutGroup with a ContentSizeFitter).</summary>
        public static RectTransform ScrollView(Transform parent, string name, out ScrollRect scroll)
        {
            var root = Rect(name, parent);
            Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.0f);
            scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vimg = viewport.gameObject.AddComponent<Image>(); vimg.color = new Color(0, 0, 0, 0.01f);
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
            VGroup(content.gameObject, 14, 14);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40;
            return content;
        }

        public static Image ProgressBar(Transform parent, Color fillColor, out Image fill)
        {
            var back = Box(parent, "ProgressBack", new Color32(20, 24, 30, 255));
            fill = Box(back.transform, "ProgressFill", fillColor);
            fill.type = Image.Type.Sliced;
            Anchor(fill.rectTransform, 0, 0, 1, 1);
            SetProgress(fill, 0);
            return back;
        }

        public static void SetProgress(Image fill, float v)
        {
            v = Mathf.Clamp01(v);
            fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1);
            fill.rectTransform.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Touch feedback: scales the control down slightly while pressed.</summary>
    public sealed class PressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        Vector3 _base = Vector3.one;
        void Awake() { _base = transform.localScale; }
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { transform.localScale = _base * 0.96f; }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { transform.localScale = _base; }
        void OnDisable() { transform.localScale = _base; }
    }

    /// <summary>Brief scale "pop" used when something is purchased or claimed.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        float _t = 1;
        public void Play() { _t = 0; }
        void Update()
        {
            if (_t >= 1) return;
            _t += Time.unscaledDeltaTime / 0.25f;
            float k = _t >= 1 ? 1 : 1 + 0.18f * Mathf.Sin(_t * Mathf.PI);
            transform.localScale = new Vector3(k, k, 1);
        }
    }

    /// <summary>Rotates a UI element continuously (speed set by the owner) — used for the gears.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float DegreesPerSecond;
        void Update() { if (DegreesPerSecond != 0f) transform.Rotate(0, 0, -DegreesPerSecond * Time.unscaledDeltaTime); }
    }

    /// <summary>"+12 Cr" text that floats up and fades, then destroys itself.</summary>
    public sealed class FloatText : MonoBehaviour
    {
        float _t;
        Text _text;
        Color _color;
        void Awake() { _text = GetComponent<Text>(); _color = _text.color; }
        void Update()
        {
            _t += Time.unscaledDeltaTime;
            var p = transform.localPosition; p.y += 140f * Time.unscaledDeltaTime; transform.localPosition = p;
            _text.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(1f - _t / 0.9f));
            if (_t >= 0.9f) Destroy(gameObject);
        }
    }
}
