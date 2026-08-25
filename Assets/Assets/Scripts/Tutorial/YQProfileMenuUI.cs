// Assets/Assets/Scripts/Tutorial/YQProfileMenuUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQProfileMenuUI : MonoBehaviour
{
    public static bool IsOpenNow { get; private set; }

    private readonly object _modalToken = new object();

    private Canvas _canvas;
    private RectTransform _listContent;
    private TMP_InputField _nameInput;
    private TMP_Text _statusText;
    private bool _open;
    private float _nextRefreshTime;

    private void Awake()
    {
        BuildUi();
        SetOpen(false);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && !_open && !RuntimeModalUiBlocker.IsAnyModalOpen && kb.f10Key.wasPressedThisFrame)
            SetOpen(true);
        else if (kb != null && _open && (kb.escapeKey.wasPressedThisFrame || kb.f10Key.wasPressedThisFrame))
            SetOpen(false);

        if (_open && Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + 0.25f;
            RenderProfiles();
        }
    }

    public void OpenFromPause()
    {
        SetOpen(true);
    }

    public void ForceCloseFromBootstrap()
    {
        _open = false;
        IsOpenNow = false;
        if (_canvas != null)
            _canvas.enabled = false;
        RuntimeModalUiBlocker.Release(_modalToken);
    }

    private void SetOpen(bool value)
    {
        if (_open == value && _canvas != null && _canvas.enabled == value)
            return;

        _open = value;
        IsOpenNow = value;
        if (_canvas != null)
            _canvas.enabled = value;

        if (value)
        {
            RuntimeModalUiBlocker.Acquire(_modalToken);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RenderProfiles();
        }
        else
        {
            RuntimeModalUiBlocker.Release(_modalToken);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void CreateProfile()
    {
        if (YQProfileSaveSystem.Instance == null)
            return;

        string name = _nameInput != null ? _nameInput.text : string.Empty;
        string id = YQProfileSaveSystem.Instance.CreateNewProfile(name);
        _statusText.text = "Created profile " + id;
        if (_nameInput != null)
            _nameInput.text = string.Empty;
        RenderProfiles();
    }

    private void SaveActive()
    {
        bool ok = YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.SaveActiveProfile();
        _statusText.text = ok ? "Saved active profile." : "Save failed.";
        RenderProfiles();
    }

    private void RenderProfiles()
    {
        if (_listContent == null)
            return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        if (system == null)
        {
            _statusText.text = "Profile system unavailable.";
            return;
        }

        for (int i = 0; i < system.Profiles.Count; i++)
        {
            YQProfileSaveSystem.ProfileEntry entry = system.Profiles[i];
            if (entry == null)
                continue;

            GameObject row = new GameObject("ProfileRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_listContent, false);
            // note: Active profile rows use the same neon sky accent as the startup screen.
            row.GetComponent<Image>().color = string.Equals(entry.profileId, system.ActiveProfileId) ? YQUITheme.ButtonSelected : YQUITheme.Button;

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;

            TMP_Text label = CreateText(row.transform, string.IsNullOrWhiteSpace(entry.displayName) ? entry.profileId : entry.displayName, 18f, new Vector2(300f, 34f));
            label.alignment = TextAlignmentOptions.MidlineLeft;

            CreateButton(row.transform, "Save", 96f, () =>
            {
                bool ok = system.SaveProfile(entry.profileId);
                _statusText.text = ok ? "Saved " + entry.displayName : "Save failed.";
                RenderProfiles();
            });

            CreateButton(row.transform, "Load", 96f, () =>
            {
                bool ok = system.LoadProfile(entry.profileId);
                _statusText.text = ok ? "Loaded " + entry.displayName : "Load failed.";
                RenderProfiles();
            });

            CreateButton(row.transform, "Delete", 96f, () =>
            {
                bool ok = system.DeleteProfile(entry.profileId);
                _statusText.text = ok ? "Deleted " + entry.displayName : "Delete failed.";
                RenderProfiles();
            });
        }
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YQProfileMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5350;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        YQUITheme.ApplyCanvasScaler(scaler);

        RectTransform dim = CreatePanel(canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), Vector2.zero, YQUITheme.Dim);
        RectTransform panel = CreatePanel(dim, new Vector2(0.5f, 0.5f), new Vector2(860f, 620f), Vector2.zero, YQUITheme.PanelSolid);
        YQUITheme.AddFrame(panel.gameObject);

        TMP_Text title = CreateText(panel, "Profiles", 28f, new Vector2(300f, 36f));
        title.rectTransform.anchoredPosition = new Vector2(18f, -16f);
        title.alignment = TextAlignmentOptions.TopLeft;

        _nameInput = CreateInput(panel, new Vector2(18f, -64f), new Vector2(320f, 42f), "New character name");
        CreateButton(panel, "Create New", new Vector2(354f, -64f), 140f, CreateProfile);
        CreateButton(panel, "Save Active", new Vector2(504f, -64f), 140f, SaveActive);

        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(panel, false);
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(18f, 18f);
        scrollRt.offsetMax = new Vector2(-18f, -122f);
        scrollGo.GetComponent<Image>().color = new Color(0.010f, 0.050f, 0.095f, 0.54f);

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(scrollRt, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewportGo.GetComponent<Image>().color = new Color(0.72f, 0.95f, 1f, 0.03f);
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _listContent = contentGo.GetComponent<RectTransform>();
        _listContent.SetParent(viewport, false);
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.offsetMin = new Vector2(8f, 8f);
        _listContent.offsetMax = new Vector2(-8f, -8f);
        VerticalLayoutGroup v = contentGo.GetComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = _listContent;
        scroll.horizontal = false;

        _statusText = CreateText(panel, string.Empty, 16f, new Vector2(820f, 34f));
        _statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
        _statusText.rectTransform.anchorMax = new Vector2(0f, 0f);
        _statusText.rectTransform.pivot = new Vector2(0f, 0f);
        _statusText.rectTransform.anchoredPosition = new Vector2(18f, 18f);
        _statusText.alignment = TextAlignmentOptions.BottomLeft;
    }

    private static RectTransform CreatePanel(Transform parent, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text CreateText(Transform parent, string value, float size, Vector2 dimensions)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = dimensions;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.text = value;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static TMP_InputField CreateInput(Transform parent, Vector2 anchoredPosition, Vector2 size, string placeholderText)
    {
        GameObject go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        // note: Inputs use a darker neon-glass fill so white player text stays readable.
        go.GetComponent<Image>().color = new Color(0.025f, 0.090f, 0.145f, 0.98f);

        GameObject viewportGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(go.transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(10f, 8f);
        viewport.offsetMax = new Vector2(-10f, -8f);

        TextMeshProUGUI placeholder = CreateText(viewport, placeholderText, 16f, size) as TextMeshProUGUI;
        placeholder.color = YQUITheme.Muted;
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = Vector2.zero;
        placeholder.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI text = CreateText(viewport, string.Empty, 16f, size) as TextMeshProUGUI;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Left;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private static Button CreateButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction action)
    {
        return CreateButton(parent, label, Vector2.zero, width, action);
    }

    private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, float width, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(width, 42f);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(go.transform, label, 16f, rt.sizeDelta);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        // note: Apply button colors after the label exists so text and frame match the shared theme.
        YQUITheme.ApplyButton(button);
        return button;
    }
}
