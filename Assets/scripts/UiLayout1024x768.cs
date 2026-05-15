using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Anchors HUD and menu text for a 1024×768 reference resolution.
/// </summary>
public static class UiLayout1024x768
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1024f, 768f);

    const float SafeMarginX = 40f;
    const float SafeMarginY = 32f;
    const int HudFontSize = 24;
    const int HudSmallFontSize = 22;
    const int TitleFontSize = 72;
    const int SubtitleFontSize = 28;
    const int AnnouncementFontSize = 72;

    public static void ConfigureCanvas(Canvas canvas)
    {
        if (canvas == null) return;

        RectTransform canvasRt = canvas.GetComponent<RectTransform>();
        if (canvasRt != null)
        {
            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.offsetMin = Vector2.zero;
            canvasRt.offsetMax = Vector2.zero;
            canvasRt.localScale = Vector3.one;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
    }

    public static void ApplyTopLeft(TextMeshProUGUI label, float insetFromTop, float minWidth = 380f)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.TopLeft, HudFontSize);
        float width = FitWidth(label, minWidth, 420f, HudFontSize);
        PlaceCornerRect(label.rectTransform, 0f, 1f, SafeMarginX, insetFromTop, width, 40f);
    }

    public static void ApplyTopRight(TextMeshProUGUI label, float insetFromTop, float minWidth = 280f)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.TopRight, HudFontSize);
        float width = FitWidth(label, minWidth, 400f, HudFontSize);
        PlaceCornerRect(label.rectTransform, 1f, 1f, SafeMarginX, insetFromTop, width, 40f, fromRight: true);
    }

    public static void ApplyTopCenter(TextMeshProUGUI label, float insetFromTop, float minWidth = 520f, int fontSize = 28)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.Top, fontSize);
        float width = FitWidth(label, minWidth, 640f, fontSize);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(width, 44f);
        rt.anchoredPosition = new Vector2(0f, -insetFromTop);
        NormalizeRect(rt);
    }

    public static void ApplyBottomCenter(TextMeshProUGUI label, float insetFromBottom, float minWidth = 520f, int fontSize = HudFontSize)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.Bottom, fontSize);
        float width = FitWidth(label, minWidth, 620f, fontSize);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(width, 56f);
        rt.anchoredPosition = new Vector2(0f, insetFromBottom);
        NormalizeRect(rt);
    }

    public static void ApplyBottomRight(TextMeshProUGUI label, float insetFromBottom, float minWidth = 360f, float height = 64f)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.BottomRight, HudSmallFontSize);
        float width = FitWidth(label, minWidth, 480f, HudSmallFontSize);
        PlaceCornerRect(label.rectTransform, 1f, 0f, SafeMarginX, insetFromBottom, width, height, fromRight: true, fromTop: false);
    }

    public static void ApplyBottomLeft(TextMeshProUGUI label, float insetFromBottom, float minWidth = 260f)
    {
        if (label == null) return;

        Style(label, TextAlignmentOptions.BottomLeft, HudSmallFontSize);
        float width = FitWidth(label, minWidth, 320f, HudSmallFontSize);
        PlaceCornerRect(label.rectTransform, 0f, 0f, SafeMarginX, insetFromBottom, width, 44f, fromRight: false, fromTop: false);
    }

    public static void ApplyScreenCenter(TextMeshProUGUI label, float width = 900f, int fontSize = AnnouncementFontSize)
    {
        if (label == null) return;

        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 140f);
        rt.anchoredPosition = Vector2.zero;
        Style(label, TextAlignmentOptions.Center, fontSize);
    }

    public static void ApplyMenuTitle(TextMeshProUGUI label)
    {
        if (label == null) return;
        ApplyScreenCenter(label, 900f, TitleFontSize);
    }

    public static void ApplyMenuSubtitle(TextMeshProUGUI label, float belowTitleOffsetY)
    {
        if (label == null) return;

        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 48f);
        rt.anchoredPosition = new Vector2(0f, belowTitleOffsetY);
        Style(label, TextAlignmentOptions.Center, SubtitleFontSize);
    }

    public static void ApplyMenuPrompt(TextMeshProUGUI label)
    {
        if (label == null) return;
        ApplyBottomCenter(label, SafeMarginY + 8f, 680f, SubtitleFontSize);
    }

    static void PlaceCornerRect(
        RectTransform rt,
        float anchorX,
        float anchorY,
        float marginX,
        float marginY,
        float width,
        float height,
        bool fromRight = false,
        bool fromTop = true)
    {
        rt.anchorMin = new Vector2(anchorX, anchorY);
        rt.anchorMax = new Vector2(anchorX, anchorY);

        if (fromRight && fromTop)
        {
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-marginX, -marginY);
        }
        else if (fromRight && !fromTop)
        {
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-marginX, marginY);
        }
        else if (!fromRight && fromTop)
        {
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(marginX, -marginY);
        }
        else
        {
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(marginX, marginY);
        }

        rt.sizeDelta = new Vector2(width, height);
        NormalizeRect(rt);
    }

    static void NormalizeRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    static float FitWidth(TextMeshProUGUI label, float minWidth, float maxWidth, int fontSize)
    {
        if (label == null) return minWidth;

        string text = label.text;
        if (string.IsNullOrEmpty(text))
            text = " ";

        label.fontSize = fontSize;
        label.ForceMeshUpdate();
        float preferred = label.GetPreferredValues(text, 0f, 0f).x + 40f;
        return Mathf.Clamp(preferred, minWidth, maxWidth);
    }

    static void Style(TextMeshProUGUI label, TextAlignmentOptions alignment, int fontSize)
    {
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableAutoSizing = false;
        label.alignment = alignment;
        label.fontSize = fontSize;
        label.raycastTarget = false;
        label.margin = new Vector4(4f, 2f, 4f, 2f);
    }
}
