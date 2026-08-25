using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class YQUITheme
{
    public static readonly Color Pearl =
        new Color(0.955f, 0.975f, 1f, 1f);

    public static readonly Color StreamBlue =
        new Color(0.34f, 0.84f, 1f, 0.82f);

    public static readonly Color Panel =
        new Color(0.018f, 0.105f, 0.190f, 0.70f);

    public static readonly Color PanelSolid =
        new Color(0.025f, 0.125f, 0.220f, 0.82f);

    public static readonly Color PanelSoft =
        new Color(0.045f, 0.180f, 0.285f, 0.58f);

    public static readonly Color Ink =
        Pearl;

    public static readonly Color Muted =
        new Color(0.70f, 0.87f, 0.96f, 0.96f);

    public static readonly Color Gold =
        new Color(0.61f, 0.91f, 1f, 1f);

    public static readonly Color GoldDim =
        new Color(0.38f, 0.87f, 1f, 0.50f);

    public static readonly Color Button =
        new Color(0.035f, 0.205f, 0.345f, 0.72f);

    public static readonly Color ButtonSelected =
        new Color(0.10f, 0.42f, 0.61f, 0.88f);

    public static readonly Color Dim =
        new Color(0.005f, 0.055f, 0.125f, 0.44f);

    public static void ApplyCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return;

        // note: All runtime-created UI uses one reference resolution so panels scale consistently.
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight =
            0.5f;
    }

    public static void ApplyPanel(
        Image image,
        bool framed = true)
    {
        if (image == null)
            return;

        image.color =
            Panel;
        image.raycastTarget =
            true;

        if (framed)
        {
            AddFrame(image.gameObject);
            AddDataStreamDecoration(image.gameObject);
        }
    }

    public static void ApplySoftPanel(
        Image image)
    {
        if (image == null)
            return;

        image.color =
            PanelSoft;
        image.raycastTarget =
            true;
        AddDataStreamDecoration(image.gameObject);
    }

    public static void AddFrame(
        GameObject go)
    {
        if (go == null ||
            go.transform.Find("__YQ_PearlFrame") != null)
        {
            return;
        }

        /*
         * Unity's Outline duplicates the complete source graphic four times.
         * On translucent glass panels that compounds alpha and turns a quiet
         * surface into an opaque cyan slab. Four child rails provide the same
         * edge definition with five fewer overdrawn panel quads.
         */
        Outline legacyOutline = go.GetComponent<Outline>();
        if (legacyOutline != null)
            legacyOutline.enabled = false;

        GameObject frame = new GameObject(
            "__YQ_PearlFrame",
            typeof(RectTransform));
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.SetParent(go.transform, false);
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        // note: One-pixel pearlescent rails keep glass surfaces crisp without multiplying their fill opacity.
        CreateChromeLine(frameRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), Vector2.zero, GoldDim);
        CreateChromeLine(frameRect, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1f), new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.24f));
        CreateChromeLine(frameRect, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(1f, 0f), new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.24f));
        CreateChromeLine(frameRect, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-1f, 0f), Vector2.zero, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.24f));
    }

    public static void ApplyText(
        TMP_Text text,
        Color? color = null)
    {
        if (text == null)
            return;

        text.color =
            color ?? Ink;
        // note: Use the current TMP wrapping API so the polish pass does not add compiler warnings.
        text.overflowMode =
            TextOverflowModes.Ellipsis;
        text.textWrappingMode =
            TextWrappingModes.Normal;
        text.fontSizeMin =
            Mathf.Max(10f, text.fontSize * 0.72f);
        text.fontSizeMax =
            Mathf.Max(text.fontSize, text.fontSizeMax);
    }

    public static void ApplyButton(
        Button button,
        bool selected = false)
    {
        if (button == null)
            return;

        Image image =
            button.GetComponent<Image>();

        if (image != null)
        {
            image.color =
                selected ? ButtonSelected : Button;
        }

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            selected ? ButtonSelected : Button;
        colors.highlightedColor =
            new Color(0.15f, 0.50f, 0.68f, 0.92f);
        colors.pressedColor =
            new Color(0.025f, 0.16f, 0.28f, 0.90f);
        colors.selectedColor =
            ButtonSelected;
        colors.disabledColor =
            new Color(0.025f, 0.08f, 0.13f, 0.46f);
        colors.colorMultiplier =
            1f;
        colors.fadeDuration =
            0.08f;
        button.colors =
            colors;

        AddFrame(
            button.gameObject);
        AddDataStreamDecoration(
            button.gameObject);

        TMP_Text label =
            button.GetComponentInChildren<TMP_Text>(
                true);

        ApplyText(
            label,
            selected ? Gold : Ink);
    }

    private static void AddDataStreamDecoration(GameObject panel)
    {
        if (panel == null ||
            panel.transform.Find("__YQ_DataStreamChrome") != null)
        {
            return;
        }

        // note: Static rails and a restrained corner return suggest a live data surface without Update loops or per-frame allocations.
        GameObject chrome = new GameObject(
            "__YQ_DataStreamChrome",
            typeof(RectTransform));
        RectTransform chromeRect = chrome.GetComponent<RectTransform>();
        chromeRect.SetParent(panel.transform, false);
        chromeRect.anchorMin = Vector2.zero;
        chromeRect.anchorMax = Vector2.one;
        chromeRect.offsetMin = Vector2.zero;
        chromeRect.offsetMax = Vector2.zero;
        chromeRect.SetAsFirstSibling();

        CreateChromeLine(
            chromeRect,
            "Pearl_TopRail",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -1.5f),
            new Vector2(0f, 1.5f),
            StreamBlue);
        CreateChromeLine(
            chromeRect,
            "Pearl_LeftRail",
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1.5f, 0f),
            new Color(StreamBlue.r, StreamBlue.g, StreamBlue.b, 0.36f));

        // note: A short lower-right return replaces the former telemetry ruler, keeping the data-stream language without a debug-overlay silhouette.
        CreateChromeLine(
            chromeRect,
            "Pearl_LowerReturn",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-34f, 0f),
            new Vector2(0f, 1f),
            new Color(0.68f, 0.93f, 1f, 0.30f));
    }

    private static void CreateChromeLine(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color)
    {
        GameObject line = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Image image = line.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
