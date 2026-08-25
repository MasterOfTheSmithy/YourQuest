// Assets/Assets/Scripts/Tutorial/YourQuestPauseMenuUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YourQuestPauseMenuUI : MonoBehaviour
{
    public static bool IsOpenNow { get; private set; }

    private readonly object _modalToken = new object();

    private Canvas _canvas;
    private TMP_Text _settingsText;
    private bool _open;

    private void Awake()
    {
        BuildUi();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (_open)
            RuntimeModalUiBlocker.Release(_modalToken);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (!_open)
        {
            if (!RuntimeModalUiBlocker.IsAnyModalOpen && kb.escapeKey.wasPressedThisFrame)
                SetOpen(true);
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
            SetOpen(false);

        RenderSettings();
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
            RenderSettings();
        }
        else
        {
            RuntimeModalUiBlocker.Release(_modalToken);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Resume() => SetOpen(false);

    private void SaveNow()
    {
        YQProfileSaveSystem.Instance?.SaveActiveProfile();
        PlayerStateManager.Instance?.Save();
        WorldStateManager.Instance?.Save();
        RenderSettings();
    }

    private void OpenProfiles()
    {
        YQProfileMenuUI menu = FindFirstObjectByType<YQProfileMenuUI>();
        if (menu != null)
            menu.OpenFromPause();
    }

    private void ToggleCameraMode()
    {
        YQInvestorPlayerMotor motor = FindFirstObjectByType<YQInvestorPlayerMotor>();
        if (motor == null)
            return;

        motor.ToggleCameraMode();
        RenderSettings();
    }

    private void AdjustSensitivity(float delta)
    {
        YQInvestorPlayerMotor motor = FindFirstObjectByType<YQInvestorPlayerMotor>();
        if (motor == null)
            return;

        motor.sensitivityX = Mathf.Clamp(motor.sensitivityX + delta, 0.03f, 0.45f);
        motor.sensitivityY = Mathf.Clamp(motor.sensitivityY + delta, 0.03f, 0.45f);
        RenderSettings();
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RenderSettings()
    {
        if (_settingsText == null)
            return;

        YQInvestorPlayerMotor motor = FindFirstObjectByType<YQInvestorPlayerMotor>();
        string cameraMode = motor != null && motor.firstPerson ? "First Person" : "Third Person";
        float sensitivity = motor != null ? motor.sensitivityX : 0f;
        string activeProfile = YQProfileSaveSystem.Instance != null ? YQProfileSaveSystem.Instance.ActiveProfileId : "<none>";

        _settingsText.text =
            "Settings\n\n" +
            "Camera Mode  " + cameraMode + "\n" +
            "Look Sensitivity  " + sensitivity.ToString("0.00") + "\n" +
            "Active Profile  " + activeProfile + "\n\n" +
            "Save writes player and world state into the active profile.";
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YourQuestPauseMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5300;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        YQUITheme.ApplyCanvasScaler(scaler);

        RectTransform dim = CreatePanel(canvasGo.transform, "Dim", new Vector2(0.5f, 0.5f), new Vector2(1920f, 1080f), Vector2.zero, YQUITheme.Dim);
        RectTransform panel = CreatePanel(dim, "Panel", new Vector2(0.5f, 0.5f), new Vector2(720f, 540f), Vector2.zero, YQUITheme.PanelSolid);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.68f, 0.61f, 0.42f, 0.4f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateText(panel, "Title", 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(24f, -20f), new Vector2(400f, 36f));
        title.color = YQUITheme.Gold;
        title.text = "Paused";

        _settingsText = CreateText(panel, "SettingsText", 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(24f, -74f), new Vector2(380f, 260f));
        _settingsText.textWrappingMode = TextWrappingModes.Normal;

        CreateButton(panel, "Resume", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(220f, 52f), "Resume").onClick.AddListener(Resume);
        CreateButton(panel, "Save", new Vector2(1f, 1f), new Vector2(-24f, -90f), new Vector2(220f, 52f), "Save State").onClick.AddListener(SaveNow);
        CreateButton(panel, "Profiles", new Vector2(1f, 1f), new Vector2(-24f, -156f), new Vector2(220f, 52f), "Profiles").onClick.AddListener(OpenProfiles);
        CreateButton(panel, "CameraMode", new Vector2(1f, 1f), new Vector2(-24f, -222f), new Vector2(220f, 52f), "Toggle Camera").onClick.AddListener(ToggleCameraMode);
        CreateButton(panel, "SensDown", new Vector2(1f, 1f), new Vector2(-24f, -288f), new Vector2(104f, 52f), "Sens -").onClick.AddListener(() => AdjustSensitivity(-0.01f));
        CreateButton(panel, "SensUp", new Vector2(1f, 1f), new Vector2(-140f, -288f), new Vector2(104f, 52f), "Sens +").onClick.AddListener(() => AdjustSensitivity(0.01f));
        CreateButton(panel, "Quit", new Vector2(1f, 1f), new Vector2(-24f, -354f), new Vector2(220f, 52f), "Quit").onClick.AddListener(Quit);
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
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

    private static TMP_Text CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = dimensions;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string label)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
        Button button = go.GetComponent<Button>();
        YQUITheme.ApplyButton(button);

        TMP_Text text = CreateText(go.transform, "Label", 18f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, size);
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.text = label;
        YQUITheme.ApplyText(text, YQUITheme.Ink);
        return button;
    }
}
