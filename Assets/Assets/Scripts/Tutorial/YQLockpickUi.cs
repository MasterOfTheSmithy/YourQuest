using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class YQLockpickUi : MonoBehaviour
{
    private const string LockpickPrefabPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/LockpickA.prefab";
    private const string LockPrefabPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/Lock1.prefab";
    private const float MinAngle = -78f;
    private const float MaxAngle = 78f;

    private static readonly object ModalToken = new object();
    private static YQLockpickUi s_instance;
    private static Sprite s_lockDiscSprite;
    private static Sprite s_lockRingSprite;

    private Canvas _canvas;
    private RectTransform _panel;
    private RectTransform _pickLine;
    private RectTransform _sweetLine;
    private RectTransform _tensionFill;
    private RectTransform _progressFill;
    private RectTransform _stressFill;
    private TMP_Text _titleText;
    private TMP_Text _hintText;
    private TMP_Text _statusText;
    private GameObject _worldPreview;
    private GameObject _previewLock;
    private GameObject _previewPick;
    private Vector3 _previewPickBaseLocalPosition;
    private Quaternion _previewPickBaseLocalRotation = Quaternion.identity;
    private Quaternion _previewLockBaseLocalRotation = Quaternion.identity;

    private YQLockpickableDoor _door;
    private YQLockpickableLoot _loot;
    private GameObject _player;
    private Vector3 _targetPosition;
    private bool _open;
    private bool _wasTensioning;
    private float _difficulty;
    private float _pickAngle;
    private float _sweetSpotAngle;
    private float _tension;
    private float _progress;
    private float _stress;
    private float _nextTensionSoundTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        EnsureInstance();
    }

    public static bool TryBegin(YQLockpickableDoor door, GameObject player)
    {
        if (door == null)
            return false;

        YQLockpickUi ui = EnsureInstance();
        if (ui == null || (RuntimeModalUiBlocker.IsAnyModalOpen && !ui._open))
            return false;

        ui.BeginDoor(door, player);
        return true;
    }

    public static bool TryBegin(YQLockpickableLoot loot, GameObject player)
    {
        if (loot == null)
            return false;

        YQLockpickUi ui = EnsureInstance();
        if (ui == null || (RuntimeModalUiBlocker.IsAnyModalOpen && !ui._open))
            return false;

        ui.BeginLoot(loot, player);
        return true;
    }

    private static YQLockpickUi EnsureInstance()
    {
        if (s_instance != null)
            return s_instance;

        s_instance = FindFirstObjectByType<YQLockpickUi>(FindObjectsInactive.Include);
        if (s_instance != null)
            return s_instance;

        GameObject go = new GameObject("YQLockpickUi");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<YQLockpickUi>();
        return s_instance;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        HideUi();
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
        RuntimeModalUiBlocker.Release(ModalToken);
        HideWorldPreview();
    }

    private void Update()
    {
        if (!_open)
            return;

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            Cancel();
            return;
        }

        float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.05f);
        float turn = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
                turn -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
                turn += 1f;
        }

        if (Mathf.Abs(turn) > 0.01f)
            _pickAngle += turn * 118f * dt;
        if (mouse != null)
            _pickAngle += mouse.delta.ReadValue().x * 0.08f;
        _pickAngle = Mathf.Clamp(_pickAngle, MinAngle, MaxAngle);

        bool tensioning = (kb != null && (kb.eKey.isPressed || kb.spaceKey.isPressed)) || (mouse != null && mouse.leftButton.isPressed);
        if (tensioning && !_wasTensioning)
            YQRuntimeAudioFeedback.PlayLockpickClick(_targetPosition);
        _wasTensioning = tensioning;

        _tension = Mathf.MoveTowards(_tension, tensioning ? 1f : 0f, (tensioning ? 2.4f : 3.8f) * dt);
        float window = SuccessWindow;
        float offset = Mathf.Abs(Mathf.DeltaAngle(_pickAngle, _sweetSpotAngle));
        if (tensioning)
        {
            if (offset <= window)
            {
                _progress = Mathf.Clamp01(_progress + dt * Mathf.Lerp(1.22f, 0.74f, _difficulty) * Mathf.Lerp(0.45f, 1f, _tension));
                _stress = Mathf.Max(0f, _stress - dt * 0.32f);
            }
            else
            {
                float pressure = Mathf.InverseLerp(window, MaxAngle, offset);
                _stress = Mathf.Clamp01(_stress + dt * Mathf.Lerp(0.58f, 1.45f, _difficulty) * Mathf.Lerp(0.45f, 1f, pressure));
                if (Time.unscaledTime >= _nextTensionSoundTime)
                {
                    _nextTensionSoundTime = Time.unscaledTime + 0.38f;
                    YQRuntimeAudioFeedback.PlayLockpickTension(_targetPosition);
                }
            }
        }
        else
        {
            _stress = Mathf.Max(0f, _stress - dt * 0.42f);
        }

        UpdateVisuals(tensioning, offset, window);

        if (_progress >= 1f)
            Complete(true);
        else if (_stress >= 1f)
            Complete(false);
    }

    private void BeginDoor(YQLockpickableDoor door, GameObject player)
    {
        if (_open)
            return;

        _door = door;
        _loot = null;
        _player = player;
        BeginCommon(door.displayName, door.regionId, door.lockDifficulty, door.transform);
    }

    private void BeginLoot(YQLockpickableLoot loot, GameObject player)
    {
        if (_open)
            return;

        _door = null;
        _loot = loot;
        _player = player;
        BeginCommon(loot.displayName, loot.regionId, loot.lockDifficulty, loot.transform);
    }

    private void BeginCommon(string displayName, string regionId, float difficulty, Transform target)
    {
        _difficulty = Mathf.Clamp01(difficulty);
        _sweetSpotAngle = ComputeSweetSpot(displayName, regionId, _difficulty);
        _pickAngle = Mathf.Clamp(_sweetSpotAngle + ComputeStartingOffset(displayName, regionId, _difficulty), MinAngle, MaxAngle);
        _tension = 0f;
        _progress = 0f;
        _stress = 0f;
        _wasTensioning = false;
        _nextTensionSoundTime = 0f;
        _targetPosition = ResolveTargetPosition(target);

        if (_titleText != null)
            _titleText.text = string.IsNullOrWhiteSpace(displayName) ? "LOCK" : displayName.ToUpperInvariant();
        if (_hintText != null)
            _hintText.text = "Find the set point with A/D or mouse. Hold E or Space to turn the lock.";
        if (_statusText != null)
            _statusText.text = "Listening for the pins...";

        _open = true;
        if (_canvas != null)
            _canvas.gameObject.SetActive(true);
        RuntimeModalUiBlocker.Acquire(ModalToken);
        ShowWorldPreview(target);
        YQRuntimeAudioFeedback.PlayLockpickStart(_targetPosition);
        RecordAttemptStarted(displayName);
        UpdateVisuals(false, Mathf.Abs(Mathf.DeltaAngle(_pickAngle, _sweetSpotAngle)), SuccessWindow);
    }

    private void Complete(bool success)
    {
        YQLockpickableDoor door = _door;
        YQLockpickableLoot loot = _loot;
        GameObject player = _player;
        CloseWithoutOutcome();

        if (!success)
            YQRuntimeAudioFeedback.PlayLockpickBreak(_targetPosition);

        if (door != null)
            door.CompleteLockpickFromUi(player, success);
        else if (loot != null)
            loot.CompleteLockpickFromUi(player, success);
    }

    private void Cancel()
    {
        string targetName = _door != null ? _door.displayName : _loot != null ? _loot.displayName : "lock";
        CloseWithoutOutcome();
        GeneratedRpgContentService.Instance?.SetInventoryMessage("Stopped picking " + targetName + ".");
    }

    private void CloseWithoutOutcome()
    {
        _open = false;
        _door = null;
        _loot = null;
        _player = null;
        RuntimeModalUiBlocker.Release(ModalToken);
        HideWorldPreview();
        HideUi();
    }

    private void HideUi()
    {
        if (_canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void UpdateVisuals(bool tensioning, float offset, float window)
    {
        if (_pickLine != null)
            _pickLine.localRotation = Quaternion.Euler(0f, 0f, _pickAngle);
        if (_sweetLine != null)
            _sweetLine.localRotation = Quaternion.Euler(0f, 0f, _sweetSpotAngle);
        UpdateWorldPreviewPose(tensioning);

        SetFill(_tensionFill, _tension);
        SetFill(_progressFill, _progress);
        SetFill(_stressFill, _stress);

        if (_panel != null)
        {
            float shake = Mathf.Clamp01(_stress - 0.68f) * (tensioning ? 5f : 1f);
            _panel.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 72f) * shake, Mathf.Cos(Time.unscaledTime * 61f) * shake);
        }

        if (_statusText == null)
            return;

        if (!tensioning)
            _statusText.text = "Angle the pick until the lock settles.";
        else if (offset <= window)
            _statusText.text = "Pins are binding. Hold steady.";
        else if (_stress > 0.72f)
            _statusText.text = "Too much pressure. Ease off.";
        else
            _statusText.text = "The pick is scraping past the set point.";
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("YQLockpickCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5600;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform backdrop = CreatePanel(canvasGo.transform, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.012f, 0.014f, 0.62f));
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;

        _panel = CreatePanel(canvasGo.transform, "LockpickPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(640f, 500f), Vector2.zero, new Color(0.055f, 0.058f, 0.065f, 0.92f));
        AddFrame(_panel, new Color(0.78f, 0.68f, 0.44f, 0.46f));

        _titleText = CreateText(_panel, "Title", 24f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, -28f), new Vector2(560f, 34f));
        _hintText = CreateText(_panel, "Hint", 15f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0f, -66f), new Vector2(560f, 46f));
        _hintText.textWrappingMode = TextWrappingModes.Normal;

        RectTransform lockRoot = CreatePanel(_panel, "LockFaceRoot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(312f, 312f), new Vector2(0f, 26f), new Color(0f, 0f, 0f, 0f));
        Image disc = CreateImage(lockRoot, "LockDisc", new Vector2(248f, 248f), Vector2.zero, GetLockDiscSprite(), new Color(0.18f, 0.17f, 0.15f, 0.58f));
        disc.type = Image.Type.Simple;
        Image ring = CreateImage(lockRoot, "LockRing", new Vector2(292f, 292f), Vector2.zero, GetLockRingSprite(), new Color(0.70f, 0.60f, 0.38f, 0.52f));
        ring.type = Image.Type.Simple;

        _sweetLine = CreateLine(lockRoot, "SetPoint", new Color(0.48f, 0.96f, 0.70f, 0.34f), 116f, 8f);
        _pickLine = CreateLine(lockRoot, "PickReadout", new Color(0.86f, 0.82f, 0.70f, 0.74f), 136f, 5f);

        CreatePinStack(lockRoot);

        _statusText = CreateText(_panel, "Status", 16f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, -266f), new Vector2(560f, 28f));

        CreateBar(_panel, "TensionBar", "TENSION", new Vector2(-210f, -196f), new Color(0.85f, 0.70f, 0.38f, 1f), out _tensionFill);
        CreateBar(_panel, "ProgressBar", "SET", new Vector2(0f, -196f), new Color(0.40f, 0.84f, 0.58f, 1f), out _progressFill);
        CreateBar(_panel, "StressBar", "STRESS", new Vector2(210f, -196f), new Color(0.88f, 0.34f, 0.28f, 1f), out _stressFill);
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
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
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = dimensions;

        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color32(232, 230, 214, 255);
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 size, Vector2 anchoredPosition, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    private static RectTransform CreateLine(Transform parent, string name, Color color, float length, float thickness)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(length, thickness);
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static void CreateBar(Transform parent, string name, string label, Vector2 position, Color color, out RectTransform fill)
    {
        RectTransform root = CreatePanel(parent, name, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(172f, 42f), position, new Color(0.02f, 0.024f, 0.03f, 0.86f));
        AddFrame(root, new Color(0.78f, 0.68f, 0.44f, 0.18f));

        TMP_Text title = CreateText(root, name + "_Label", 10f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0f, -4f), new Vector2(150f, 14f));
        title.text = label;

        RectTransform track = CreatePanel(root, name + "_Track", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(132f, 8f), new Vector2(0f, 10f), new Color(0.10f, 0.10f, 0.10f, 1f));
        fill = CreatePanel(track, name + "_Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), Vector2.zero, color);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = new Vector2(-132f, 0f);
    }

    private static void CreatePinStack(Transform parent)
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject go = new GameObject("Pin_" + i, typeof(RectTransform), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(12f, 34f + i * 3f);
            rt.anchoredPosition = new Vector2(-48f + i * 24f, 72f);
            go.GetComponent<Image>().color = new Color(0.74f, 0.66f, 0.48f, 0.72f);
        }
    }

    private static void AddFrame(RectTransform rt, Color color)
    {
        Outline outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static void SetFill(RectTransform fill, float value)
    {
        if (fill == null)
            return;
        fill.offsetMax = new Vector2(-132f * (1f - Mathf.Clamp01(value)), 0f);
    }

    private float SuccessWindow => Mathf.Lerp(45f, 12f, _difficulty);

    private static float ComputeSweetSpot(string displayName, string regionId, float difficulty)
    {
        int hash = ComputeStableHash((displayName ?? string.Empty) + "|" + (regionId ?? string.Empty) + "|" + Mathf.RoundToInt(difficulty * 100f));
        float t = Mathf.Abs(hash % 10000) / 9999f;
        return Mathf.Lerp(-58f, 58f, t);
    }

    private static float ComputeStartingOffset(string displayName, string regionId, float difficulty)
    {
        int hash = ComputeStableHash((regionId ?? string.Empty) + "|" + (displayName ?? string.Empty));
        float direction = (hash & 1) == 0 ? -1f : 1f;
        return direction * Mathf.Lerp(34f, 66f, difficulty);
    }

    private static int ComputeStableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }

            return hash == int.MinValue ? 0 : Mathf.Abs(hash);
        }
    }

    private static void RecordAttemptStarted(string displayName)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        if (state == null)
            return;

        state.EnsureCollections();
        state.IncCounter("lockpick:attempt", 1f);
        state.AddLedgerLine("The player started picking " + (string.IsNullOrWhiteSpace(displayName) ? "a lock" : displayName) + ".");
    }

    private static Vector3 ResolveTargetPosition(Transform target)
    {
        if (target == null)
            return Vector3.zero;
        if (TryGetBounds(target.gameObject, out Bounds bounds))
            return bounds.center;
        return target.position + Vector3.up * 0.75f;
    }

    private void ShowWorldPreview(Transform target)
    {
        HideWorldPreview();
        if (target == null)
            return;

#if UNITY_EDITOR
        GameObject root = new GameObject("YQ_LockpickToolPreview");
        _worldPreview = root;
        Bounds bounds;
        Vector3 basePosition = TryGetBounds(target.gameObject, out bounds) ? bounds.center + Vector3.up * Mathf.Max(0.25f, bounds.extents.y * 0.45f) : target.position + Vector3.up * 1.1f;
        Camera camera = Camera.main;
        Vector3 cameraForward = camera != null ? camera.transform.forward : Vector3.forward;
        root.transform.position = basePosition - cameraForward * 0.74f + Vector3.up * 0.12f;
        root.transform.rotation = camera != null ? Quaternion.LookRotation(-cameraForward, Vector3.up) : Quaternion.identity;

        _previewLock = InstantiatePreviewPrefab(root.transform, "PreviewLock", LockPrefabPath, new Vector3(-0.18f, 0f, 0f), new Vector3(0f, 0f, 0f), 1.05f, 1.05f);
        _previewPick = InstantiatePreviewPrefab(root.transform, "PreviewPick", LockpickPrefabPath, new Vector3(0.42f, -0.14f, -0.04f), new Vector3(0f, 0f, -28f), 0.95f, 0.42f);
        if (_previewLock != null)
            _previewLockBaseLocalRotation = _previewLock.transform.localRotation;
        if (_previewPick != null)
        {
            _previewPickBaseLocalPosition = _previewPick.transform.localPosition;
            _previewPickBaseLocalRotation = _previewPick.transform.localRotation;
        }
#endif
    }

    private void HideWorldPreview()
    {
        if (_worldPreview == null)
            return;

        Destroy(_worldPreview);
        _worldPreview = null;
        _previewLock = null;
        _previewPick = null;
    }

    private void UpdateWorldPreviewPose(bool tensioning)
    {
        if (_previewPick != null)
        {
            float jitter = _stress > 0.68f ? Mathf.Sin(Time.unscaledTime * 88f) * Mathf.Lerp(0f, 4.5f, _stress) : 0f;
            _previewPick.transform.localPosition = _previewPickBaseLocalPosition + new Vector3(0f, _tension * 0.026f, -_stress * 0.036f);
            _previewPick.transform.localRotation = _previewPickBaseLocalRotation * Quaternion.Euler(0f, 0f, _pickAngle * 0.62f + jitter);
        }

        if (_previewLock != null)
        {
            float bind = tensioning ? Mathf.Lerp(0f, 9f, _progress) : 0f;
            float rattle = _stress > 0.65f ? Mathf.Sin(Time.unscaledTime * 64f) * Mathf.Lerp(0f, 2.5f, _stress) : 0f;
            _previewLock.transform.localRotation = _previewLockBaseLocalRotation * Quaternion.Euler(0f, 0f, bind + rattle);
        }
    }

#if UNITY_EDITOR
    private static GameObject InstantiatePreviewPrefab(Transform parent, string name, string path, Vector3 localPosition, Vector3 localEuler, float maxFootprint, float maxHeight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return null;

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        DisablePreviewComponents(instance);
        FitPreview(instance, parent.TransformPoint(localPosition), maxFootprint, maxHeight);
        YQRuntimeUrpMaterialRepair.RepairHierarchy(instance);
        return instance;
    }

    private static void DisablePreviewComponents(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].isKinematic = true;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }
    }

    private static void FitPreview(GameObject root, Vector3 anchor, float maxFootprint, float maxHeight)
    {
        if (!TryGetBounds(root, out Bounds bounds))
            return;

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float scale = 1f;
        if (footprint > 0.01f && maxFootprint > 0.01f && footprint > maxFootprint)
            scale = Mathf.Min(scale, maxFootprint / footprint);
        if (bounds.size.y > 0.01f && maxHeight > 0.01f && bounds.size.y > maxHeight)
            scale = Mathf.Min(scale, maxHeight / bounds.size.y);
        if (scale < 0.999f)
        {
            root.transform.localScale *= scale;
            if (!TryGetBounds(root, out bounds))
                return;
        }

        root.transform.position += anchor - bounds.center;
    }
#endif

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        bounds = default;
        bool initialized = false;
        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static Sprite GetLockDiscSprite()
    {
        if (s_lockDiscSprite == null)
            s_lockDiscSprite = CreateCircleSprite(160, 0.98f, 0f, new Color32(60, 55, 48, 255), new Color32(28, 28, 28, 255));
        return s_lockDiscSprite;
    }

    private static Sprite GetLockRingSprite()
    {
        if (s_lockRingSprite == null)
            s_lockRingSprite = CreateCircleSprite(160, 0.98f, 0.80f, new Color32(181, 150, 89, 255), new Color32(0, 0, 0, 0));
        return s_lockRingSprite;
    }

    private static Sprite CreateCircleSprite(int size, float outerRadius01, float innerRadius01, Color32 fill, Color32 inner)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        Color32 clear = new Color32(0, 0, 0, 0);
        float center = (size - 1) * 0.5f;
        float outer = center * Mathf.Clamp01(outerRadius01);
        float innerRadius = center * Mathf.Clamp01(innerRadius01);

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = dist <= outer ? (dist <= innerRadius ? inner : fill) : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
