// STUBS of the uGUI subset used by the project, for the offline partial compile check only (see csproj comment).
// They are written from knowledge of the uGUI API and are NOT shipped; the real package com.unity.ugui is used in Unity.
using UnityEngine.Events;

namespace UnityEngine.EventSystems
{
    public class EventSystem : MonoBehaviour { }
    public class StandaloneInputModule : MonoBehaviour { }
    public class PointerEventData { }
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
}

namespace UnityEngine.UI
{
    public abstract class Graphic : MonoBehaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => (RectTransform)transform;
    }

    public class Image : Graphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
    }

    public class Text : Graphic
    {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public FontStyle fontStyle { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
    }

    public abstract class Selectable : MonoBehaviour
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public bool interactable { get; set; }
        public ColorBlock colors { get; set; }
        public Transition transition { get; set; }
        public Graphic targetGraphic { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
    }

    public class ScrollRect : MonoBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform viewport { get; set; }
        public RectTransform content { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
    }

    public class RectMask2D : MonoBehaviour { }
    public class GraphicRaycaster : MonoBehaviour { }

    public class LayoutElement : MonoBehaviour
    {
        public float minHeight { get; set; }
        public float preferredHeight { get; set; }
        public float preferredWidth { get; set; }
        public float flexibleWidth { get; set; }
    }

    public abstract class LayoutGroup : MonoBehaviour
    {
        public RectOffset padding { get; set; }
    }

    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
    }

    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class ContentSizeFitter : MonoBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode verticalFit { get; set; }
    }

    public class CanvasScaler : MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }
}
