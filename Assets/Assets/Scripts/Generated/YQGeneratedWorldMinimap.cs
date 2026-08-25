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
        248f;

    private Camera _mapCamera;

    private Canvas _canvas;

    private RenderTexture _mapRenderTexture;

    private Texture2D _fogTexture;

    private RawImage _mapImage;

    private RawImage _fogImage;

    private RectTransform _playerMarker;

    private Transform _player;

    private bool[,] _discovered =
        new bool[
            DiscoveryResolution,
            DiscoveryResolution];

    private bool _discoveryDirty;

    private float _nextRevealTime;

    private float _nextSaveTime;

    private float _nextPlayerResolveTime;

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
                .GameplayPresentationReleased;

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
            SaveDiscovery();

            _nextSaveTime =
                Time.unscaledTime +
                SaveInterval;
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
                MapCameraHeight,
                playerPosition.z);

        _mapCamera.transform.rotation =
            Quaternion.Euler(
                90f,
                0f,
                0f);

        UpdateFogUv(
            playerPosition);

        UpdatePlayerMarker();

    }

    private void OnApplicationQuit()
    {
        if (_discoveryDirty)
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
        GameObject canvasObject =
            new GameObject(
                "YQ_MinimapCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(
            transform,
            false);

        Canvas canvas =
            canvasObject.GetComponent<
                Canvas>();

        _canvas =
            canvas;

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        /*
         * Normal HUD layer.
         * Modal/title/menu interfaces remain above this.
         */
        canvas.sortingOrder =
            3100;

        CanvasScaler scaler =
            canvasObject.GetComponent<
                CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode
                .ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.matchWidthOrHeight =
            0.5f;

        RectTransform frame =
            CreateUiObject(
                canvasObject.transform,
                "MinimapFrame");

        frame.anchorMin =
    new Vector2(
        0f,
        1f);

        frame.anchorMax =
            new Vector2(
                0f,
                1f);

        frame.pivot =
            new Vector2(
                0f,
                1f);

        frame.anchoredPosition =
            new Vector2(
                22f,
                -22f);

        frame.sizeDelta =
            new Vector2(
                UiMapSize + 18f,
                UiMapSize + 18f);

        Image frameBackground =
            frame.gameObject.AddComponent<
                Image>();

        frameBackground.color =
            new Color(
                0.02f,
                0.025f,
                0.03f,
                0.92f);

        Outline frameOutline =
            frame.gameObject.AddComponent<
                Outline>();

        frameOutline.effectColor =
            new Color(
                0.78f,
                0.72f,
                0.54f,
                0.9f);

        frameOutline.effectDistance =
            new Vector2(
                2f,
                -2f);

        RectTransform mapRect =
            CreateUiObject(
                frame,
                "Map");

        mapRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        mapRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        mapRect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        mapRect.anchoredPosition =
            Vector2.zero;

        mapRect.sizeDelta =
            new Vector2(
                UiMapSize,
                UiMapSize);

        _mapImage =
            mapRect.gameObject.AddComponent<
                RawImage>();

        _mapImage.texture =
            _mapRenderTexture;

        _mapImage.color =
            Color.white;

        RectTransform fogRect =
            CreateUiObject(
                mapRect,
                "FogOfWar");

        StretchToParent(
            fogRect);

        _fogImage =
            fogRect.gameObject.AddComponent<
                RawImage>();

        _fogImage.texture =
            _fogTexture;

        _fogImage.color =
            Color.white;

        _fogImage.raycastTarget =
            false;

        CreateCardinalLabel(
            frame,
            "N",
            new Vector2(
                0f,
                UiMapSize *
                    0.5f -
                10f));

        CreateCardinalLabel(
            frame,
            "S",
            new Vector2(
                0f,
                -UiMapSize *
                    0.5f +
                10f));

        CreateCardinalLabel(
            frame,
            "W",
            new Vector2(
                -UiMapSize *
                    0.5f +
                10f,
                0f));

        CreateCardinalLabel(
            frame,
            "E",
            new Vector2(
                UiMapSize *
                    0.5f -
                10f,
                0f));

        RectTransform marker =
            CreateUiObject(
                frame,
                "PlayerMarker");

        marker.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        marker.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        marker.pivot =
            new Vector2(
                0.5f,
                0.5f);

        marker.anchoredPosition =
            Vector2.zero;

        marker.sizeDelta =
            new Vector2(
                28f,
                28f);

        TextMeshProUGUI markerText =
            marker.gameObject.AddComponent<
                TextMeshProUGUI>();

        markerText.text =
            "▲";

        markerText.fontSize =
            23f;

        markerText.alignment =
            TextAlignmentOptions.Center;

        markerText.color =
            new Color(
                1f,
                0.88f,
                0.35f,
                1f);

        markerText.raycastTarget =
            false;

        _playerMarker =
            marker;
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
            17f;

        label.alignment =
            TextAlignmentOptions.Center;

        label.fontStyle =
            FontStyles.Bold;

        label.color =
            Color.white;

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

    private void UpdateFogUv(
        Vector3 playerPosition)
    {
        if (_fogImage == null)
            return;

        float worldSize =
            YQGeneratedWorldTerrain.WorldSize;

        float halfWorld =
            worldSize *
            0.5f;

        float viewDiameter =
            MapWorldRadius *
            2f;

        float uvSize =
            viewDiameter /
            worldSize;

        float normalizedCenterX =
            (playerPosition.x +
                halfWorld) /
            worldSize;

        float normalizedCenterY =
            (playerPosition.z +
                halfWorld) /
            worldSize;

        Rect uv =
            new Rect(
                normalizedCenterX -
                    uvSize *
                    0.5f,
                normalizedCenterY -
                    uvSize *
                    0.5f,
                uvSize,
                uvSize);

        _fogImage.uvRect =
            uv;
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
