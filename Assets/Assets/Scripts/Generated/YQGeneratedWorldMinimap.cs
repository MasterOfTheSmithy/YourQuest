using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQGeneratedWorldMinimap : MonoBehaviour
{
    public static YQGeneratedWorldMinimap Instance
    {
        get;
        private set;
    }

    private const string RootObjectName =
        "YQ_GENERATED_WORLD_MINIMAP";

    private const string FogSavePrefix =
        "generated_map:fog:v1:";

    private WorldState _loadedWorldState;

    /*
     * Persistent discovery grid.
     *
     * 64x64 over a 1024m world gives ~16m discovery cells.
     * That's accurate enough for traversal while keeping the
     * persistent save footprint reasonable.
     */
    private const int DiscoveryResolution =
        64;

    /*
     * Visual fog texture is higher resolution than the persistent
     * discovery grid so the UI remains reasonably clean.
     */
    private const int FogTextureResolution =
        256;

    private const int FogPixelsPerCell =
        FogTextureResolution /
        DiscoveryResolution;

    private const int MapRenderResolution =
        384;

    private const float MapWorldRadius =
        82f;

    private const float RevealRadius =
        34f;

    private const float RevealInterval =
        0.16f;

    private const float SaveInterval =
        3f;

    private const float PlayerResolveInterval =
        1f;

    private const float MapCameraHeight =
        240f;

    private const float UiMapSize =
        300f;

    private Camera _mapCamera;

    private Canvas _canvas;

    private RenderTexture _mapRenderTexture;

    private Texture2D _fogTexture;

    private RawImage _mapImage;

    private RawImage _fogImage;

    private RectTransform _playerMarker;

    private RectTransform _questMarker;

    private TextMeshProUGUI _questDistanceText;

    private Transform _player;

    private YQActiveQuestWorldHighlight _questHighlight;

    private bool[,] _discovered =
        new bool[
            DiscoveryResolution,
            DiscoveryResolution];

    private bool _discoveryDirty;

    private float _nextRevealTime;

    private float _nextSaveTime;

    private float _nextPlayerResolveTime;

    private float _nextQuestMarkerRefreshTime;

    private float _nextQuestHighlightResolveTime;

    private static readonly Color32 FoggedColor =
        new Color32(
            0,
            0,
            0,
            242);

    private static readonly Color32 RevealedColor =
        new Color32(
            0,
            0,
            0,
            0);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
        {
            // note: The minimap render texture, fog texture, camera, and canvas are gameplay allocations and do not exist during the title phase.
            return;
        }

        if (FindAnyObjectByType<
                YQGeneratedWorldMinimap>() != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                RootObjectName);

        DontDestroyOnLoad(
            root);

        root.AddComponent<
            YQGeneratedWorldMinimap>();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject);

            return;
        }

        Instance =
            this;

        DontDestroyOnLoad(
            gameObject);

        BuildMapCamera();

        BuildFogTexture();

        SyncDiscoveryWithActiveWorldState(
    true);

        BuildUi();

        if (_canvas != null)
        {
            // note: Keep the newly created HUD hidden until the authoritative gameplay presentation has actually been released.
            _canvas.enabled =
                false;
        }

        _nextRevealTime =
            Time.unscaledTime +
            0.1f;

        _nextSaveTime =
            Time.unscaledTime +
            SaveInterval;

        _nextPlayerResolveTime =
            0f;
    }

    private void Update()
    {
        bool gameplayMapVisible =
            YourQuestTutorialAutoBootstrap
                .GameplayPresentationReleased && !RuntimeModalUiBlocker.IsBlocked;

        if (_canvas != null &&
            _canvas.enabled != gameplayMapVisible)
        {
            // note: Service readiness starts world construction; only the presentation-release gate means the player has entered gameplay.
            _canvas.enabled =
                gameplayMapVisible;
        }

        if (_mapCamera != null &&
            _mapCamera.enabled != gameplayMapVisible)
        {
            // note: URP schedules enabled cameras safely inside its render loop; calling Camera.Render from LateUpdate corrupted RenderGraph light jobs.
            _mapCamera.enabled = gameplayMapVisible;
        }

        if (!gameplayMapVisible)
            return;

        SyncDiscoveryWithActiveWorldState(
        false);

        ResolvePlayer();

        if (_player == null)
            return;

        if (Time.unscaledTime >=
            _nextRevealTime)
        {
            _nextRevealTime =
                Time.unscaledTime +
                RevealInterval;

            RevealAroundPlayer();
        }

        if (_discoveryDirty &&
            Time.unscaledTime >=
            _nextSaveTime)
        {
            _nextSaveTime =
                Time.unscaledTime +
                SaveInterval;

            // note: Active profiles already own the changed WorldState and publish it as a paired revision on Save or Quit; a recurring standalone mirror write stalls streaming frames.
            if (YQProfileSaveSystem.Instance == null)
                SaveDiscovery();
        }
    }

    private void LateUpdate()
    {
        if (_player == null ||
            _mapCamera == null ||
            !YourQuestTutorialAutoBootstrap
                .GameplayPresentationReleased)
        {
            return;
        }

        Vector3 playerPosition =
            _player.position;

        /*
         * Camera remains north-up.
         *
         * +Z is north on the minimap.
         */
        _mapCamera.transform.position =
            new Vector3(
                playerPosition.x,
                playerPosition.y + MapCameraHeight,
                playerPosition.z);

        _mapCamera.transform.rotation =
            Quaternion.Euler(
                90f,
                0f,
                0f);

        UpdateFogUv(
            playerPosition);

        UpdatePlayerMarker();

        UpdateQuestMarker(
            playerPosition);

    }

    private void OnApplicationQuit()
    {
        // note: The profile owner commits this same in-memory discovery state with the player document during shutdown.
        if (_discoveryDirty && YQProfileSaveSystem.Instance == null)
            SaveDiscovery();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_mapCamera != null)
        {
            Destroy(
                _mapCamera.gameObject);
        }

        if (_mapRenderTexture != null)
        {
            _mapRenderTexture.Release();

            Destroy(
                _mapRenderTexture);
        }

        if (_fogTexture != null)
        {
            Destroy(
                _fogTexture);
        }
    }

    private void BuildMapCamera()
    {
        GameObject cameraObject =
            new GameObject(
                "YQ_MinimapCamera");

        cameraObject.transform.SetParent(
            transform,
            false);

        _mapCamera =
            cameraObject.AddComponent<
                Camera>();

        _mapCamera.orthographic =
            true;

        _mapCamera.orthographicSize =
            MapWorldRadius;

        _mapCamera.transform.rotation =
            Quaternion.Euler(
                90f,
                0f,
                0f);

        _mapCamera.transform.position =
            new Vector3(
                0f,
                MapCameraHeight,
                0f);

        _mapCamera.nearClipPlane =
            0.5f;

        _mapCamera.farClipPlane =
            600f;

        _mapCamera.clearFlags =
            CameraClearFlags.SolidColor;

        _mapCamera.backgroundColor =
            new Color(
                0.025f,
                0.03f,
                0.035f,
                1f);

        _mapCamera.allowHDR =
            false;

        _mapCamera.allowMSAA =
            false;

        _mapCamera.useOcclusionCulling =
            false;

        _mapCamera.depth =
            -100f;

        // note: The camera starts disabled and Update publishes it only after gameplay release, keeping Goddess loading free of minimap rendering.
        _mapCamera.enabled =
            false;

        _mapRenderTexture =
            new RenderTexture(
                MapRenderResolution,
                MapRenderResolution,
                16,
                RenderTextureFormat.ARGB32);

        _mapRenderTexture.name =
            "YQ_GeneratedWorld_Minimap_RT";

        _mapRenderTexture.filterMode =
            FilterMode.Bilinear;

        _mapRenderTexture.wrapMode =
            TextureWrapMode.Clamp;

        _mapRenderTexture.Create();

        _mapCamera.targetTexture =
            _mapRenderTexture;
    }

    private void BuildFogTexture()
    {
        _fogTexture =
            new Texture2D(
                FogTextureResolution,
                FogTextureResolution,
                TextureFormat.RGBA32,
                false,
                false);

        _fogTexture.name =
            "YQ_GeneratedWorld_Fog";

        _fogTexture.filterMode =
            FilterMode.Bilinear;

        _fogTexture.wrapMode =
            TextureWrapMode.Clamp;
    }

    private void BuildUi()
    {
        // note: The live north-up map keeps existing discovery/quest authorities and shares the resource HUD's blue/white frame.
        var go=new GameObject("YQ_MinimapCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        go.transform.SetParent(transform,false);
        _canvas=go.GetComponent<Canvas>();
        _canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder=3100;
        YQUITheme.ApplyCanvasScaler(go.GetComponent<CanvasScaler>());
        RectTransform frame=CreateUiObject(go.transform,"MinimapFrame");
        // note: Navigation occupies the lower-left corner so the floating identity/resource rails retain the reference's upper-left placement.
        frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(0,0);
        frame.anchoredPosition=new Vector2(30,28);
        frame.sizeDelta=new Vector2(UiMapSize+34,UiMapSize+80);
        var background=frame.gameObject.AddComponent<Image>();
        background.raycastTarget=false;
        YQBlueglassStyle.Panel(background);
        var title=CreateMapLabel(frame,"MapTitle","LOCAL MAP  /  NORTH",new Vector2(0,UiMapSize*.5f+28),new Vector2(UiMapSize,28),20,YQUITheme.Pearl,FontStyles.Bold);
        title.characterSpacing=2;
        RectTransform border=CreateUiObject(frame,"MapBorder");
        border.anchorMin=border.anchorMax=border.pivot=new Vector2(.5f,.5f);
        border.anchoredPosition=new Vector2(0,-2);
        border.sizeDelta=new Vector2(UiMapSize+4,UiMapSize+4);
        var borderImage=border.gameObject.AddComponent<Image>();
        borderImage.raycastTarget=false;
        borderImage.color=YQUITheme.GoldDim;
        RectTransform map=CreateUiObject(border,"Map");
        StretchToParent(map);
        map.offsetMin=new Vector2(2,2);
        map.offsetMax=new Vector2(-2,-2);
        map.gameObject.AddComponent<RectMask2D>();
        _mapImage=map.gameObject.AddComponent<RawImage>();
        _mapImage.texture=_mapRenderTexture;
        _mapImage.raycastTarget=false;
        RectTransform fog=CreateUiObject(map,"FogOfWar");
        fog.anchorMin=fog.anchorMax=fog.pivot=new Vector2(.5f,.5f);
        _fogImage=fog.gameObject.AddComponent<RawImage>();
        _fogImage.texture=_fogTexture;
        _fogImage.raycastTarget=false;
        CreateCardinalLabel(frame,"N",new Vector2(0,UiMapSize*.5f-18));
        CreateCardinalLabel(frame,"S",new Vector2(0,-UiMapSize*.5f+14));
        CreateCardinalLabel(frame,"W",new Vector2(-UiMapSize*.5f+15,-2));
        CreateCardinalLabel(frame,"E",new Vector2(UiMapSize*.5f-15,-2));
        _playerMarker=CreateUiObject(map,"PlayerMarker");
        _playerMarker.anchorMin=_playerMarker.anchorMax=_playerMarker.pivot=new Vector2(.5f,.5f);
        _playerMarker.sizeDelta=new Vector2(30,30);
        var playerText=_playerMarker.gameObject.AddComponent<TextMeshProUGUI>();
        playerText.text="▲";
        playerText.fontSize=26;
        playerText.alignment=TextAlignmentOptions.Center;
        playerText.color=YQUITheme.Pearl;
        playerText.raycastTarget=false;
        _questMarker=CreateUiObject(map,"ActiveQuestMarker");
        _questMarker.anchorMin=_questMarker.anchorMax=_questMarker.pivot=new Vector2(.5f,.5f);
        // note: Native geometry keeps the quest diamond visible when the approved UI font has no diamond glyph.
        _questMarker.sizeDelta=new Vector2(16,16);
        _questMarker.localRotation=Quaternion.Euler(0,0,45f);
        var questOutline=_questMarker.gameObject.AddComponent<Image>();
        questOutline.color=YQUITheme.Pearl;
        questOutline.raycastTarget=false;
        var questCenter=CreateUiObject(_questMarker,"QuestDiamondCenter");
        questCenter.anchorMin=questCenter.anchorMax=questCenter.pivot=new Vector2(.5f,.5f);
        questCenter.sizeDelta=new Vector2(10,10);
        var questFill=questCenter.gameObject.AddComponent<Image>();
        questFill.color=YQUITheme.StreamBlue;
        questFill.raycastTarget=false;
        _questMarker.gameObject.SetActive(false);
        _questDistanceText=CreateMapLabel(frame,"QuestDistance",string.Empty,new Vector2(0,-UiMapSize*.5f-27),new Vector2(UiMapSize,28),19,YQUITheme.Muted,FontStyles.Normal);
    }

    private static RectTransform CreateUiObject(
        Transform parent,
        string objectName)
    {
        GameObject go =
            new GameObject(
                objectName,
                typeof(RectTransform));

        go.transform.SetParent(
            parent,
            false);

        return
            go.GetComponent<
                RectTransform>();
    }

    private static void StretchToParent(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static void CreateCardinalLabel(
        Transform parent,
        string text,
        Vector2 position)
    {
        RectTransform rect =
            CreateUiObject(
                parent,
                "Direction_" +
                text);

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            new Vector2(
                28f,
                28f);

        TextMeshProUGUI label =
            rect.gameObject.AddComponent<
                TextMeshProUGUI>();

        label.text =
            text;

        label.fontSize =
            20f;

        label.alignment =
            TextAlignmentOptions.Center;

        label.fontStyle =
            FontStyles.Bold;

        label.color = YQUITheme.Pearl;

        label.raycastTarget =
            false;

        Outline outline =
            rect.gameObject.AddComponent<
                Outline>();

        outline.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.9f);

        outline.effectDistance =
            new Vector2(
                1f,
                -1f);
    }

    private static TextMeshProUGUI CreateMapLabel(
        Transform parent,
        string objectName,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style)
    {
        RectTransform rect =
            CreateUiObject(
                parent,
                objectName);

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchorMax =
            rect.anchorMin;

        rect.pivot =
            rect.anchorMin;

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            size;

        TextMeshProUGUI label =
            rect.gameObject.AddComponent<TextMeshProUGUI>();

        label.text =
            value;

        label.fontSize =
            fontSize;

        label.fontStyle =
            style;

        label.alignment =
            TextAlignmentOptions.Center;

        label.color =
            color;

        label.raycastTarget =
            false;

        return label;
    }

    private void ResolvePlayer()
    {
        if (_player != null)
            return;

        if (Time.unscaledTime <
            _nextPlayerResolveTime)
        {
            return;
        }

        _nextPlayerResolveTime =
            Time.unscaledTime +
            PlayerResolveInterval;

        GameObject playerObject =
            null;

        try
        {
            playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player");
        }
        catch
        {
        }

        if (playerObject != null)
        {
            _player =
                playerObject.transform;

            return;
        }

        Camera mainCamera =
            Camera.main;

        if (mainCamera != null &&
            mainCamera.transform.parent != null)
        {
            _player =
                mainCamera.transform.parent;
        }
    }

    private void RevealAroundPlayer()
    {
        if (_player == null)
            return;

        Vector3 position =
            _player.position;

        WorldToDiscoveryCell(
            position,
            out int centerX,
            out int centerY);

        float cellWorldSize =
            YQGeneratedWorldTerrain.WorldSize /
            DiscoveryResolution;

        int radiusCells =
            Mathf.CeilToInt(
                RevealRadius /
                cellWorldSize);

        bool changed =
            false;

        for (int y =
                 centerY -
                 radiusCells;
             y <=
                 centerY +
                 radiusCells;
             y++)
        {
            if (y < 0 ||
                y >= DiscoveryResolution)
            {
                continue;
            }

            for (int x =
                     centerX -
                     radiusCells;
                 x <=
                     centerX +
                     radiusCells;
                 x++)
            {
                if (x < 0 ||
                    x >= DiscoveryResolution)
                {
                    continue;
                }

                float dx =
                    x -
                    centerX;

                float dy =
                    y -
                    centerY;

                if (dx * dx +
                    dy * dy >
                    radiusCells *
                    radiusCells)
                {
                    continue;
                }

                if (_discovered[x, y])
                    continue;

                _discovered[x, y] =
                    true;

                SetFogCellRevealed(
                    x,
                    y);

                PersistDiscoveryCell(
                    x,
                    y);

                changed =
                    true;
            }
        }

        if (!changed)
            return;

        _fogTexture.Apply(
            false,
            false);

        _discoveryDirty =
            true;
    }

    private void WorldToDiscoveryCell(
        Vector3 worldPosition,
        out int cellX,
        out int cellY)
    {
        float half =
            YQGeneratedWorldTerrain.WorldSize *
            0.5f;

        float normalizedX =
            Mathf.InverseLerp(
                -half,
                half,
                worldPosition.x);

        float normalizedY =
            Mathf.InverseLerp(
                -half,
                half,
                worldPosition.z);

        cellX =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    normalizedX *
                    DiscoveryResolution),
                0,
                DiscoveryResolution - 1);

        cellY =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    normalizedY *
                    DiscoveryResolution),
                0,
                DiscoveryResolution - 1);
    }

    private void UpdateFogUv(Vector3 playerPosition)
    {
        if (_fogImage==null) return;
        // note: Position the original finite discovery atlas in map space. Clamping UVs beyond the origin used to smear one edge across the entire live map.
        float pixelsPerMetre=UiMapSize/(MapWorldRadius*2f);
        _fogImage.uvRect=new Rect(0,0,1,1);
        _fogImage.rectTransform.sizeDelta=Vector2.one*(YQGeneratedWorldTerrain.WorldSize*pixelsPerMetre);
        _fogImage.rectTransform.anchoredPosition=new Vector2(-playerPosition.x,-playerPosition.z)*pixelsPerMetre;
    }

    private void UpdatePlayerMarker()
    {
        if (_player == null ||
            _playerMarker == null)
        {
            return;
        }

        /*
         * Map itself stays north-up.
         * Rotate the arrow to show the player's facing direction.
         */
        float yaw =
            _player.eulerAngles.y;

        _playerMarker.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -yaw);
    }

    private void UpdateQuestMarker(
        Vector3 playerPosition)
    {
        if (_questMarker == null ||
            _questDistanceText == null ||
            Time.unscaledTime <
                _nextQuestMarkerRefreshTime)
        {
            return;
        }

        _nextQuestMarkerRefreshTime =
            Time.unscaledTime +
            0.2f;

        if (_questHighlight == null &&
            Time.unscaledTime >=
                _nextQuestHighlightResolveTime)
        {
            _nextQuestHighlightResolveTime =
                Time.unscaledTime +
                PlayerResolveInterval;

            _questHighlight =
                FindAnyObjectByType<
                    YQActiveQuestWorldHighlight>();
        }

        Vector3 targetPosition =
            Vector3.zero;

        bool hasTarget =
            _questHighlight != null &&
            _questHighlight.TryGetCurrentTargetPosition(
                out targetPosition);

        if (!hasTarget)
        {
            if (_questMarker.gameObject.activeSelf)
                _questMarker.gameObject.SetActive(false);

            if (_questDistanceText.text.Length > 0)
                _questDistanceText.text = string.Empty;

            return;
        }

        Vector2 worldOffset =
            new Vector2(
                targetPosition.x -
                    playerPosition.x,
                targetPosition.z -
                    playerPosition.z);

        float distance =
            worldOffset.magnitude;

        // note: Use the same exact world-to-pixel scale as the camera; inset only the final off-map marker.
        float pixelsPerMetre=UiMapSize/(MapWorldRadius*2f);

        Vector2 markerPosition =
            worldOffset *
            pixelsPerMetre;

        float maximumAxis =
            Mathf.Max(
                Mathf.Abs(markerPosition.x),
                Mathf.Abs(markerPosition.y));

        float markerLimit =
            UiMapSize *
                0.5f -
            18f;

        bool beyondMap =
            maximumAxis >
            markerLimit;

        if (beyondMap)
        {
            markerPosition *=
                markerLimit /
                Mathf.Max(
                    0.001f,
                    maximumAxis);
        }

        _questMarker.anchoredPosition =
            markerPosition;

        if (!_questMarker.gameObject.activeSelf)
            _questMarker.gameObject.SetActive(true);

        string distanceText =
            "Target  " +
            Mathf.RoundToInt(distance) +
            " m" +
            (beyondMap
                ? "  ·  OFF MAP"
                : string.Empty);

        if (_questDistanceText.text != distanceText)
            _questDistanceText.text = distanceText;

        // note: Quest-map UI reuses the world highlight's cached target and refreshes at 5 Hz, avoiding duplicate scans and per-frame text allocation.
    }

    private void SyncDiscoveryWithActiveWorldState(
    bool force)
    {
        WorldStateManager manager =
            WorldStateManager.Instance;

        WorldState world =
            manager != null
                ? manager.State
                : null;

        if (!force &&
            ReferenceEquals(
                world,
                _loadedWorldState))
        {
            return;
        }

        /*
         * If the previous active save had unsaved newly explored cells,
         * commit them before switching to another WorldState object.
         */
        if (_loadedWorldState != null &&
            _discoveryDirty)
        {
            WorldStateManager currentManager =
                WorldStateManager.Instance;

            if (currentManager != null &&
                ReferenceEquals(
                    currentManager.State,
                    _loadedWorldState))
            {
                currentManager.Save();
            }
        }

        _loadedWorldState =
            world;

        _discoveryDirty =
            false;

        ClearDiscoveryGrid();

        LoadDiscoveryFromWorldState(
            world);

        RebuildFogTexture();
    }

    private void ClearDiscoveryGrid()
    {
        for (int y = 0;
             y < DiscoveryResolution;
             y++)
        {
            for (int x = 0;
                 x < DiscoveryResolution;
                 x++)
            {
                _discovered[x, y] =
                    false;
            }
        }
    }

    private void LoadDiscoveryFromWorldState(
        WorldState world)
    {
        if (world == null)
            return;

        world.EnsureCollections();

        if (world.globalFlags == null)
            return;

        for (int y = 0;
             y < DiscoveryResolution;
             y++)
        {
            for (int x = 0;
                 x < DiscoveryResolution;
                 x++)
            {
                string key =
                    FogCellKey(
                        x,
                        y);

                if (world.globalFlags.TryGetValue(
                        key,
                        out float value) &&
                    value > 0.5f)
                {
                    _discovered[x, y] =
                        true;
                }
            }
        }
    }

    private void PersistDiscoveryCell(
        int x,
        int y)
    {
        WorldStateManager manager =
            WorldStateManager.Instance;

        WorldState world =
            manager != null
                ? manager.State
                : null;

        if (world == null)
            return;

        world.EnsureCollections();

        if (world.globalFlags == null)
        {
            world.globalFlags =
                new System.Collections.Generic
                    .Dictionary<string, float>();
        }

        world.globalFlags[
            FogCellKey(
                x,
                y)] =
            1f;
    }

    private void SaveDiscovery()
    {
        WorldStateManager manager =
            WorldStateManager.Instance;

        if (manager == null ||
            manager.State == null ||
            !ReferenceEquals(
                manager.State,
                _loadedWorldState))
        {
            return;
        }

        manager.Save();

        _discoveryDirty =
            false;
    }

    private static string FogCellKey(
        int x,
        int y)
    {
        return
            FogSavePrefix +
            x +
            ":" +
            y;
    }

    private void RebuildFogTexture()
    {
        Color32[] pixels =
            new Color32[
                FogTextureResolution *
                FogTextureResolution];

        for (int i = 0;
             i < pixels.Length;
             i++)
        {
            pixels[i] =
                FoggedColor;
        }

        _fogTexture.SetPixels32(
            pixels);

        for (int y = 0;
             y < DiscoveryResolution;
             y++)
        {
            for (int x = 0;
                 x < DiscoveryResolution;
                 x++)
            {
                if (_discovered[x, y])
                {
                    SetFogCellRevealed(
                        x,
                        y);
                }
            }
        }

        _fogTexture.Apply(
            false,
            false);
    }

    private void SetFogCellRevealed(
        int cellX,
        int cellY)
    {
        int startX =
            cellX *
            FogPixelsPerCell;

        int startY =
            cellY *
            FogPixelsPerCell;

        Color32[] block =
            new Color32[
                FogPixelsPerCell *
                FogPixelsPerCell];

        for (int i = 0;
             i < block.Length;
             i++)
        {
            block[i] =
                RevealedColor;
        }

        _fogTexture.SetPixels32(
            startX,
            startY,
            FogPixelsPerCell,
            FogPixelsPerCell,
            block);
    }
}
