#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class YQTitleEnvironmentSceneBuilder
{
    private const string SceneFolder = "Assets/Assets/Scenes";
    private const string ScenePath = SceneFolder + "/YourQuest_TitleEnvironment.unity";
    private const string LightingPath = SceneFolder + "/YourQuest_TitleEnvironmentLighting.asset";
    private const string PostProfilePath = SceneFolder + "/YourQuest_TitlePostProcessing.asset";
    private const string MaterialPath = SceneFolder + "/YourQuest_TitleGround.mat";
    // note: These paths are retained only for the archived scene-wordmark recipe; recipe 9 no longer instantiates that camera-space geometry.
    private const string AdapterFolder = "Assets/Assets/GeneratedAssets/TitleScreen/Materials";
    private const string LogoTextMaterialPath = AdapterFolder + "/YQ_TitleLogo_Pearl.mat";
    private const string LogoDepthMaterialPath = AdapterFolder + "/YQ_TitleLogo_Depth.mat";
    private const string LogoBackingMaterialPath = AdapterFolder + "/YQ_TitleLogo_Obsidian.mat";
    private const string LogoTrimMaterialPath = AdapterFolder + "/YQ_TitleLogo_Trim.mat";
    private const string AmbientHumPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Generic/Humming & Pulsing/Humming_Loop_4_S.wav";
    private const string AmbientWindPath = "Assets/Magic Pig Games (Infinity PBR)/Characters/Dragons/Sound Effects/Wind_Loop.wav";
    private const string UiHoverPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Menu Click_1.wav";
    private const string UiConfirmPath = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/Audio & Mixer/Menu Click_2.wav";
    private const string ThresholdAudioPath = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/HolyLight/Warmup Short/HolyLight_Warmup_Short_1_S.wav";
    private const int TitleLayer = 31;
    private const int SceneRecipeVersion = 9;

    private static readonly (string path, Vector3 position, Vector3 euler, Vector3 scale)[] StagePieces =
    {
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_AngelStatue_02.prefab", new Vector3(5.5f, 0f, 6.5f), new Vector3(0f, -25f, 0f), new Vector3(1.25f, 1.25f, 1.25f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Ruin_01.prefab", new Vector3(2f, 0f, 8.5f), new Vector3(0f, 8f, 0f), Vector3.one),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Ruin_04.prefab", new Vector3(9f, 0f, 8f), new Vector3(0f, 188f, 0f), Vector3.one),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Stair_02.prefab", new Vector3(5.3f, -0.15f, 2.2f), new Vector3(0f, -2f, 0f), new Vector3(1.3f, 1f, 1.3f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Rock_09.prefab", new Vector3(5f, -1.2f, 8f), new Vector3(0f, 24f, 0f), new Vector3(2.8f, 0.8f, 2.4f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Rock_06.prefab", new Vector3(-1.8f, -0.5f, 8f), new Vector3(0f, -18f, 0f), new Vector3(1.7f, 1.2f, 1.5f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Rock_04.prefab", new Vector3(12f, -0.6f, 7f), new Vector3(0f, 72f, 0f), new Vector3(1.8f, 1.1f, 1.6f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_MountainTree_01.prefab", new Vector3(-2.5f, 0f, 11f), new Vector3(0f, 18f, 0f), new Vector3(1.15f, 1.15f, 1.15f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_MountainTree_03.prefab", new Vector3(13.5f, 0f, 12f), new Vector3(0f, -35f, 0f), new Vector3(0.9f, 0.9f, 0.9f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Fern_01.prefab", new Vector3(1.2f, 0f, 3.8f), new Vector3(0f, 30f, 0f), new Vector3(1.4f, 1.4f, 1.4f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_HeatherFlower_03.prefab", new Vector3(9.8f, 0f, 3.8f), new Vector3(0f, -12f, 0f), new Vector3(1.35f, 1.35f, 1.35f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_Sword_01.prefab", new Vector3(5.5f, 0.15f, 2.5f), new Vector3(-8f, 8f, 0f), new Vector3(1.1f, 1.1f, 1.1f))
    };

    private static readonly (string path, Vector3 position, Vector3 euler, Vector3 scale)[] DynamicAccoutrement =
    {
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/PS_Dust.prefab", new Vector3(5.5f, 0.30f, 6.5f), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/PS_Leaves.prefab", new Vector3(5.5f, 4.1f, 6.5f), new Vector3(0f, -25f, 0f), new Vector3(0.78f, 0.78f, 0.78f)),
        ("Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Star Sparkles Float.prefab", new Vector3(5.5f, 4.8f, 6.5f), Vector3.zero, new Vector3(1.3f, 1.3f, 1.3f)),
        ("Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Wisps Circular.prefab", new Vector3(5.5f, 0.25f, 6.5f), Vector3.zero, new Vector3(1.8f, 1.8f, 1.8f)),
        ("Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_BirdAnimation.prefab", new Vector3(8.2f, 6.6f, 10.4f), new Vector3(0f, -118f, 0f), new Vector3(0.72f, 0.72f, 0.72f))
    };

    [InitializeOnLoadMethod]
    private static void EnsureRequestedSceneExists()
    {
        // note: The explicitly requested production scene is materialized once after compilation; future rebuilds remain a deliberate menu action.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            IsCurrentRecipe())
        {
            EditorApplication.delayCall += EnsureIncludedInBuild;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: A script reload during play queues the requested scene for the first safe Edit Mode frame instead of silently abandoning it.
            EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
            EditorApplication.playModeStateChanged += BuildAfterPlayModeExit;
            return;
        }

        EditorApplication.delayCall += BuildMissingScene;
    }

    private static void BuildAfterPlayModeExit(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
        EditorApplication.delayCall += BuildMissingScene;
    }

    [MenuItem("YourQuest/Production/Rebuild + Bake 3D Title Screen")]
    public static void RebuildAndBake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[YQTitleEnvironmentSceneBuilder] Exit Play Mode before rebuilding the title scene.");
            return;
        }

        BuildScene(true);
    }

    [MenuItem("YourQuest/Production/Open 3D Title Screen")]
    public static void OpenTitleScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            BuildScene(false);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void BuildMissingScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            IsCurrentRecipe())
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
            EditorApplication.playModeStateChanged += BuildAfterPlayModeExit;
            return;
        }

        SceneSetup[] currentSetup = EditorSceneManager.GetSceneManagerSetup();
        for (int index = 0; index < currentSetup.Length; index++)
        {
            Scene openScene = SceneManager.GetSceneByPath(currentSetup[index].path);
            if (openScene.IsValid() && openScene.isDirty)
            {
                // note: Automatic production tooling never discards or prompts over unrelated unsaved scene work.
                Debug.LogWarning(
                    "[YQTitleEnvironmentSceneBuilder] Title scene build deferred because an open scene has unsaved changes. Save it, then use YourQuest > Production > Rebuild + Bake 3D Title Screen.");
                return;
            }
        }

        BuildScene(true);
    }

    private static void BuildScene(bool bakeLighting)
    {
        if (!Directory.Exists(SceneFolder))
            Directory.CreateDirectory(SceneFolder);

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "YourQuest_TitleEnvironment";

        ConfigureEnvironment();
        Transform environmentRoot = new GameObject("01__BakedGoddessShrine").transform;
        environmentRoot.gameObject.layer = TitleLayer;
        BuildGround(environmentRoot);
        Transform goddessRoot = BuildStagePieces(environmentRoot);
        MarkStatic(environmentRoot.gameObject);
        BuildDynamicAccoutrement();
        Transform target = BuildCameraTarget();
        BuildGoddessPortraitPose(
            goddessRoot,
            out Transform portraitCameraAnchor,
            out Transform portraitLookTarget);
        Camera camera = BuildCamera(target);
        BuildPostProcessing(camera);
        Light goddessKeyLight = BuildLighting();
        BuildLightProbes();
        BuildReflectionProbe();
        BuildTitleAudio(
            out AudioSource uiAudioSource,
            out AudioClip uiHoverClip,
            out AudioClip uiConfirmClip,
            out AudioClip thresholdClip);

        YQTitleEnvironmentScene controller =
            new GameObject("00__TitleEnvironmentController")
                .AddComponent<YQTitleEnvironmentScene>();
        controller.gameObject.layer = TitleLayer;
        controller.Configure(
            camera,
            target,
            goddessRoot,
            portraitCameraAnchor,
            portraitLookTarget,
            goddessKeyLight,
            uiAudioSource,
            uiHoverClip,
            uiConfirmClip,
            thresholdClip,
            SceneRecipeVersion);

        LightingSettings lighting = ConfigureLightingSettings();
        Lightmapping.lightingSettings = lighting;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureIncludedInBuild();

        bool baked = false;
        if (bakeLighting)
        {
            // note: The title stage is intentionally small, making a synchronous production bake bounded and avoiding a half-saved asynchronous scene.
            baked = Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.RestoreSceneManagerSetup(previousSetup);

        Debug.Log(
            "[YQTitleEnvironmentSceneBuilder] 3D TITLE SCREEN READY\n" +
            "Scene: " + ScenePath + "\n" +
            "Environment pieces: " + StagePieces.Length + "\n" +
            "Dynamic accoutrement: " + DynamicAccoutrement.Length + "\n" +
            "Baked lighting: " + (bakeLighting ? (baked ? "ready" : "bake failed; mixed key-light fallback active") : "not requested"));
    }

    private static bool IsCurrentRecipe()
    {
        if (!File.Exists(ScenePath))
            return false;

        // note: The serialized recipe marker makes project upgrades deterministic without rebuilding this baked scene on every editor launch.
        string sceneText = File.ReadAllText(ScenePath);
        return sceneText.Contains("sceneRecipeVersion: " + SceneRecipeVersion);
    }

    private static void ConfigureEnvironment()
    {
        // note: The restrained dusk palette lets warm shrine lighting read clearly behind pale menu typography.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f, 0.16f, 0.25f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.065f, 0.085f, 0.12f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.018f, 0.025f, 0.035f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.055f, 0.085f, 0.13f, 1f);
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.reflectionIntensity = 0.55f;
    }

    private static void BuildGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ShrineGround_ShadowReceiver";
        ground.layer = TitleLayer;
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(5.5f, -0.08f, 7f);
        ground.transform.localScale = new Vector3(3.2f, 1f, 2.4f);
        ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateGroundMaterial();
    }

    private static Material GetOrCreateGroundMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader)
        {
            name = "YourQuest_TitleGround",
            color = new Color(0.055f, 0.075f, 0.072f, 1f)
        };
        material.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static Transform BuildStagePieces(Transform parent)
    {
        List<string> missing = new List<string>();
        Transform goddessRoot = null;
        for (int index = 0; index < StagePieces.Length; index++)
        {
            var piece = StagePieces[index];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(piece.path);
            if (prefab == null)
            {
                missing.Add(piece.path);
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                continue;

            instance.name = "Title_" + prefab.name;
            instance.transform.SetParent(parent, false);
            instance.transform.position = piece.position;
            instance.transform.rotation = Quaternion.Euler(piece.euler);
            instance.transform.localScale = piece.scale;
            SetLayerRecursively(instance, TitleLayer);
            RepairTitleMaterials(instance);
            if (piece.path.IndexOf(
                    "SM_AngelStatue_02.prefab",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // note: Persist the complete combined goddess mesh so portrait framing includes every authored material slot and ornament.
                goddessRoot = instance.transform;
            }
        }

        if (missing.Count > 0)
            Debug.LogWarning("[YQTitleEnvironmentSceneBuilder] Missing " + missing.Count + " optional stage prefabs.");

        return goddessRoot;
    }

    private static void BuildDynamicAccoutrement()
    {
        Transform root = new GameObject("02__GoddessAtmosphere").transform;
        root.gameObject.layer = TitleLayer;
        List<string> missing = new List<string>();

        for (int index = 0; index < DynamicAccoutrement.Length; index++)
        {
            var piece = DynamicAccoutrement[index];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(piece.path);
            if (prefab == null)
            {
                missing.Add(piece.path);
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                continue;

            instance.name = "TitleAtmosphere_" + prefab.name;
            instance.transform.SetParent(root, false);
            instance.transform.position = piece.position;
            instance.transform.rotation = Quaternion.Euler(piece.euler);
            instance.transform.localScale = piece.scale;
            SetLayerRecursively(instance, TitleLayer);
            RepairTitleMaterials(instance);

            ParticleSystem[] particles =
                instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int particleIndex = 0;
                 particleIndex < particles.Length;
                 particleIndex++)
            {
                // note: Title presentation runs while gameplay time and ordinary listeners are paused.
                ParticleSystem.MainModule main = particles[particleIndex].main;
                main.useUnscaledTime = true;
            }
        }

        if (missing.Count > 0)
            Debug.LogWarning("[YQTitleEnvironmentSceneBuilder] Missing " + missing.Count + " optional title atmosphere prefabs.");
    }

    private static void RepairTitleMaterials(GameObject instance)
    {
        if (instance == null)
            return;

        EnsureAssetFolder(AdapterFolder);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material source = materials[slot];
                if (IsSupportedUrpMaterial(source))
                    continue;

                Material adapter = GetOrCreateTitleAdapter(source, renderer);
                if (adapter == null)
                    continue;

                materials[slot] = adapter;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    private static bool IsSupportedUrpMaterial(Material material)
    {
        if (material == null || material.shader == null || !material.shader.isSupported)
            return false;

        string shaderName = material.shader.name ?? string.Empty;
        return shaderName.StartsWith(
            "Universal Render Pipeline/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static Material GetOrCreateTitleAdapter(
        Material source,
        Renderer renderer)
    {
        string sourceGuid = "missing";
        long sourceLocalId = 0L;
        if (source != null)
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                source,
                out sourceGuid,
                out sourceLocalId);

        string rendererHint = SanitizeAssetName(
            renderer != null ? renderer.name : "renderer");
        string sourceHint = SanitizeAssetName(
            source != null ? source.name : "missing_material");
        string adapterPath = AdapterFolder + "/" +
            sourceHint + "_" + sourceGuid.Substring(0, Mathf.Min(8, sourceGuid.Length)) +
            "_" + sourceLocalId + "_" + rendererHint + ".mat";

        Material persisted = AssetDatabase.LoadAssetAtPath<Material>(adapterPath);
        if (persisted != null)
            return persisted;

        Material converted =
            YQRuntimeUrpMaterialRepair.CreateEditorUrpLitMaterial(
                source,
                renderer);
        if (converted == null)
            return null;

        converted.name = Path.GetFileNameWithoutExtension(adapterPath);
        AssetDatabase.CreateAsset(converted, adapterPath);
        return converted;
    }

    private static string SanitizeAssetName(string value)
    {
        string clean = string.IsNullOrWhiteSpace(value)
            ? "material"
            : value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int index = 0; index < invalid.Length; index++)
            clean = clean.Replace(invalid[index], '_');
        return clean.Length <= 72 ? clean : clean.Substring(0, 72);
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }

    private static Transform BuildCameraTarget()
    {
        GameObject target = new GameObject("TitleCameraLookTarget");
        target.layer = TitleLayer;
        target.transform.position = new Vector3(5.5f, 3.2f, 7.2f);
        return target.transform;
    }

    private static void BuildGoddessPortraitPose(
        Transform goddessRoot,
        out Transform cameraAnchor,
        out Transform lookTarget)
    {
        GameObject lookObject = new GameObject("GoddessPortraitLookTarget");
        lookObject.layer = TitleLayer;
        lookTarget = lookObject.transform;
        lookTarget.position = new Vector3(5.5f, 5.35f, 6.5f);

        GameObject anchorObject = new GameObject("GoddessPortraitCameraAnchor");
        anchorObject.layer = TitleLayer;
        cameraAnchor = anchorObject.transform;
        Vector3 authoredFront = goddessRoot != null
            ? -goddessRoot.forward
            : new Vector3(0f, 0f, -1f);
        authoredFront.y = 0f;
        if (authoredFront.sqrMagnitude < 0.01f)
            authoredFront = new Vector3(0f, 0f, -1f);
        authoredFront.Normalize();
        cameraAnchor.position = lookTarget.position + authoredFront * 7.1f + Vector3.up * 0.20f;
        cameraAnchor.rotation = Quaternion.LookRotation(
            lookTarget.position - cameraAnchor.position,
            Vector3.up);
        // note: This explicit baked-scene portrait contract frames the statue's face and upper body without rotating static GI geometry at runtime.
    }

    private static Camera BuildCamera(Transform target)
    {
        GameObject cameraObject = new GameObject("Title Environment Camera");
        cameraObject.layer = TitleLayer;
        cameraObject.transform.position = new Vector3(-10.5f, 5.8f, -9.5f);
        cameraObject.transform.rotation = Quaternion.LookRotation(
            target.position - cameraObject.transform.position,
            Vector3.up);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.022f, 0.045f, 0.080f, 1f);
        camera.cullingMask = 1 << TitleLayer;
        camera.depth = 100f;
        camera.fieldOfView = 43f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 180f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        // note: The additive title stage owns its listener because the playable player/camera does not exist during production startup.
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static Transform BuildTitleWordmark(Camera camera)
    {
        if (camera == null || TMP_Settings.defaultFontAsset == null)
            return null;

        EnsureAssetFolder(AdapterFolder);
        Transform root = new GameObject("04__YourQuest_3DWordmark").transform;
        root.gameObject.layer = TitleLayer;
        root.SetParent(camera.transform, false);
        root.localPosition = new Vector3(-1.84f, 0.93f, 4.30f);
        root.localRotation = Quaternion.Euler(-0.8f, 3.2f, -0.25f);
        root.localScale = Vector3.one;

        Material backing = GetOrCreateLogoLitMaterial(
            LogoBackingMaterialPath,
            new Color(0.006f, 0.014f, 0.024f, 1f),
            0.42f,
            0.48f);
        Material trim = GetOrCreateLogoLitMaterial(
            LogoTrimMaterialPath,
            new Color(0.42f, 0.66f, 0.76f, 1f),
            0.58f,
            0.70f);
        Material pearlText = GetOrCreateLogoTextMaterial(
            LogoTextMaterialPath,
            new Color(0.91f, 0.97f, 1f, 1f),
            new Color(0.24f, 0.53f, 0.68f, 1f),
            0.10f);
        Material depthText = GetOrCreateLogoTextMaterial(
            LogoDepthMaterialPath,
            new Color(0.045f, 0.12f, 0.17f, 1f),
            new Color(0.01f, 0.03f, 0.05f, 1f),
            0.16f);

        // note: Layered beveled solids give the wordmark real silhouette depth and scene-light response instead of presenting another rectangular UI graphic.
        CreateLogoSolid(
            root,
            "ObsidianDepthPlate",
            new Vector3(0f, 0f, 0.05f),
            new Vector3(17.7f, 3.15f, 0.22f),
            Quaternion.identity,
            backing);
        CreateLogoSolid(
            root,
            "UpperPearlRail",
            new Vector3(0f, 1.61f, -0.07f),
            new Vector3(17.9f, 0.09f, 0.10f),
            Quaternion.identity,
            trim);
        CreateLogoSolid(
            root,
            "LowerPearlRail",
            new Vector3(0f, -1.61f, -0.07f),
            new Vector3(17.9f, 0.09f, 0.10f),
            Quaternion.identity,
            trim);
        CreateLogoSolid(
            root,
            "LeftCutGem",
            new Vector3(-8.73f, 0f, -0.08f),
            new Vector3(0.26f, 0.26f, 0.11f),
            Quaternion.Euler(0f, 0f, 45f),
            trim);
        CreateLogoSolid(
            root,
            "RightCutGem",
            new Vector3(8.73f, 0f, -0.08f),
            new Vector3(0.26f, 0.26f, 0.11f),
            Quaternion.Euler(0f, 0f, 45f),
            trim);

        CreateLogoText(
            root,
            "YourQuest_Depth",
            depthText,
            new Vector3(0f, 0f, -0.11f));

        TextMeshPro face = CreateLogoText(
            root,
            "YourQuest_PearlFace",
            pearlText,
            new Vector3(0f, 0f, -0.145f));

        Bounds textBounds = face.textBounds;
        float textWidth = Mathf.Max(1f, textBounds.size.x);
        float textHeight = Mathf.Max(1f, textBounds.size.y);
        float plateWidth = textWidth + 1.15f;
        float plateHeight = textHeight + 0.80f;
        // note: TMP glyph metrics vary with font generation; fit the actual mesh to a fixed world-space width instead of guessing a transform scale.
        root.localScale = Vector3.one * (1.92f / textWidth);
        root.Find("ObsidianDepthPlate").localScale =
            new Vector3(plateWidth, plateHeight, 0.22f);
        root.Find("UpperPearlRail").localPosition =
            new Vector3(0f, plateHeight * 0.5f, -0.07f);
        root.Find("UpperPearlRail").localScale =
            new Vector3(plateWidth + 0.20f, 0.07f, 0.10f);
        root.Find("LowerPearlRail").localPosition =
            new Vector3(0f, -plateHeight * 0.5f, -0.07f);
        root.Find("LowerPearlRail").localScale =
            new Vector3(plateWidth + 0.20f, 0.07f, 0.10f);
        root.Find("LeftCutGem").localPosition =
            new Vector3(-plateWidth * 0.5f, 0f, -0.08f);
        root.Find("RightCutGem").localPosition =
            new Vector3(plateWidth * 0.5f, 0f, -0.08f);

        CreateLogoLight(
            camera.transform,
            "Wordmark_Pearl_Key",
            new Color(0.63f, 0.88f, 1f),
            1.45f,
            new Vector3(-2.7f, 2.1f, 2.1f),
            root.position,
            7f,
            52f);
        CreateLogoLight(
            camera.transform,
            "Wordmark_Warm_Rim",
            new Color(1f, 0.57f, 0.30f),
            0.62f,
            new Vector3(-0.2f, 0.15f, 3.0f),
            root.position,
            5f,
            64f);

        return root;
    }

    private static TextMeshPro CreateLogoText(
        Transform parent,
        string name,
        Material material,
        Vector3 localPosition)
    {
        GameObject textObject = new GameObject(name, typeof(TextMeshPro));
        textObject.layer = TitleLayer;
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.identity;
        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSharedMaterial = material;
        text.text = "YourQuest";
        text.fontSize = 36f;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 0.8f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.sizeDelta = new Vector2(60f, 8f);
        text.ForceMeshUpdate();
        return text;
    }

    private static void CreateLogoSolid(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
        solid.name = name;
        solid.layer = TitleLayer;
        solid.transform.SetParent(parent, false);
        solid.transform.localPosition = localPosition;
        solid.transform.localRotation = localRotation;
        solid.transform.localScale = localScale;
        solid.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = solid.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static Material GetOrCreateLogoLitMaterial(
        string path,
        Color color,
        float metallic,
        float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.name = Path.GetFileNameWithoutExtension(path);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateLogoTextMaterial(
        string path,
        Color faceColor,
        Color outlineColor,
        float outlineWidth)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        Material source = font != null ? font.material : null;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = source != null
                ? new Material(source)
                : new Material(Shader.Find("TextMeshPro/Distance Field"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.name = Path.GetFileNameWithoutExtension(path);
        if (font != null && font.atlasTexture != null &&
            material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", font.atlasTexture);
        }
        if (material.HasProperty("_FaceColor"))
            material.SetColor("_FaceColor", faceColor);
        if (material.HasProperty("_OutlineColor"))
            material.SetColor("_OutlineColor", outlineColor);
        if (material.HasProperty("_OutlineWidth"))
            material.SetFloat("_OutlineWidth", outlineWidth);
        if (material.HasProperty("_FaceDilate"))
            material.SetFloat("_FaceDilate", 0.08f);
        if (material.HasProperty("_Bevel"))
            material.SetFloat("_Bevel", 0.72f);
        if (material.HasProperty("_BevelWidth"))
            material.SetFloat("_BevelWidth", 0.16f);
        if (material.HasProperty("_BevelRoundness"))
            material.SetFloat("_BevelRoundness", 0.58f);
        if (material.HasProperty("_SpecularPower"))
            material.SetFloat("_SpecularPower", 2.2f);
        material.EnableKeyword("BEVEL_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateLogoLight(
        Transform cameraTransform,
        string name,
        Color color,
        float intensity,
        Vector3 localPosition,
        Vector3 worldTarget,
        float range,
        float spotAngle)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.layer = TitleLayer;
        lightObject.transform.SetParent(cameraTransform, false);
        lightObject.transform.localPosition = localPosition;
        lightObject.transform.rotation = Quaternion.LookRotation(
            worldTarget - lightObject.transform.position,
            Vector3.up);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = spotAngle * 0.62f;
        light.shadows = LightShadows.Soft;
        light.cullingMask = 1 << TitleLayer;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    private static void BuildPostProcessing(Camera camera)
    {
        if (camera == null)
            return;

        UniversalAdditionalCameraData cameraData =
            camera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        VolumeProfile profile =
            AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "YourQuest Title Post Processing";
            AssetDatabase.CreateAsset(profile, PostProfilePath);
        }

        if (!profile.TryGet(out Bloom bloom))
            bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.intensity.Override(0.30f);
        bloom.threshold.Override(1.05f);
        bloom.scatter.Override(0.62f);

        if (!profile.TryGet(out ColorAdjustments color))
            color = profile.Add<ColorAdjustments>(true);
        color.active = true;
        color.postExposure.Override(0.12f);
        color.contrast.Override(10f);
        color.saturation.Override(-4f);
        color.colorFilter.Override(new Color(0.94f, 0.975f, 1f, 1f));

        if (!profile.TryGet(out Vignette vignette))
            vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.Override(new Color(0.005f, 0.018f, 0.045f, 1f));
        vignette.intensity.Override(0.20f);
        vignette.smoothness.Override(0.36f);

        if (!profile.TryGet(out Tonemapping tonemapping))
            tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.active = true;
        tonemapping.mode.Override(TonemappingMode.ACES);
        EditorUtility.SetDirty(profile);

        GameObject volumeObject = new GameObject("Title_GlobalPostProcessing");
        volumeObject.layer = TitleLayer;
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;
        // note: Restrained bloom and grading polish the pearly shrine highlights while preserving menu legibility and the statue silhouette.
    }

    private static Light BuildLighting()
    {
        CreateLight("Moon_Key_Mixed", LightType.Directional, new Color(0.62f, 0.75f, 1f), 0.85f,
            Vector3.zero, new Vector3(38f, -32f, 0f), 0f, LightmapBakeType.Mixed);
        CreateLight("Shrine_Warm_Baked", LightType.Point, new Color(1f, 0.54f, 0.24f), 5.2f,
            new Vector3(4.1f, 2.2f, 4.2f), Vector3.zero, 13f, LightmapBakeType.Baked);
        CreateLight("Statue_Rim_Baked", LightType.Point, new Color(0.30f, 0.68f, 1f), 3.4f,
            new Vector3(9.2f, 4.6f, 10f), Vector3.zero, 16f, LightmapBakeType.Baked);
        CreateLight("Goddess_Halo_Mixed", LightType.Point, new Color(0.55f, 0.88f, 1f), 2.2f,
            new Vector3(5.4f, 6.8f, 6.4f), Vector3.zero, 8.5f, LightmapBakeType.Mixed);

        return CreateSpotLight(
            "Goddess_Portrait_Key_Mixed",
            new Color(0.78f, 0.91f, 1f),
            4.6f,
            new Vector3(10.6f, 7.8f, 0.8f),
            new Vector3(5.5f, 5.35f, 6.5f),
            20f,
            46f,
            LightmapBakeType.Mixed);
    }

    private static Light CreateLight(
        string name,
        LightType type,
        Color color,
        float intensity,
        Vector3 position,
        Vector3 euler,
        float range,
        LightmapBakeType bakeType)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.layer = TitleLayer;
        lightObject.transform.position = position;
        lightObject.transform.rotation = Quaternion.Euler(euler);
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.Soft;
        light.lightmapBakeType = bakeType;
        return light;
    }

    private static Light CreateSpotLight(
        string name,
        Color color,
        float intensity,
        Vector3 position,
        Vector3 target,
        float range,
        float spotAngle,
        LightmapBakeType bakeType)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.layer = TitleLayer;
        lightObject.transform.position = position;
        lightObject.transform.rotation = Quaternion.LookRotation(
            target - position,
            Vector3.up);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = spotAngle * 0.68f;
        light.shadows = LightShadows.Soft;
        light.lightmapBakeType = bakeType;
        return light;
    }

    private static void BuildReflectionProbe()
    {
        GameObject probeObject = new GameObject("Shrine_BakedReflectionProbe");
        probeObject.layer = TitleLayer;
        probeObject.transform.position = new Vector3(5.5f, 3.8f, 7f);
        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.size = new Vector3(26f, 13f, 24f);
        probe.intensity = 0.72f;
        probe.boxProjection = true;
        probe.cullingMask = 1 << TitleLayer;
        // note: One bounded probe restores pearly material response without adding a realtime cubemap render to the title screen.
        probe.resolution = 128;
    }

    private static void BuildTitleAudio(
        out AudioSource uiAudioSource,
        out AudioClip uiHoverClip,
        out AudioClip uiConfirmClip,
        out AudioClip thresholdClip)
    {
        Transform audioRoot = new GameObject("03__TitleAudio").transform;
        audioRoot.gameObject.layer = TitleLayer;

        CreateAmbientAudio(
            audioRoot,
            "Shrine_AmbientHum",
            AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientHumPath),
            0.065f);
        CreateAmbientAudio(
            audioRoot,
            "Shrine_WindBed",
            AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientWindPath),
            0.085f);

        GameObject uiObject = new GameObject("Title_UiAndTransitionAudio");
        uiObject.layer = TitleLayer;
        uiObject.transform.SetParent(audioRoot, false);
        uiAudioSource = uiObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.ignoreListenerPause = true;
        uiAudioSource.volume = 1f;

        // note: Clips are serialized into the additive scene so player builds never depend on editor-only path loading.
        uiHoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiHoverPath);
        uiConfirmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiConfirmPath);
        thresholdClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ThresholdAudioPath);
    }

    private static void CreateAmbientAudio(
        Transform parent,
        string name,
        AudioClip clip,
        float volume)
    {
        if (clip == null)
            return;

        GameObject audioObject = new GameObject(name);
        audioObject.layer = TitleLayer;
        audioObject.transform.SetParent(parent, false);
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = Mathf.Clamp01(volume);
    }

    private static void BuildLightProbes()
    {
        GameObject probeObject = new GameObject("Baked Light Probes");
        probeObject.layer = TitleLayer;
        LightProbeGroup probes = probeObject.AddComponent<LightProbeGroup>();
        List<Vector3> positions = new List<Vector3>();
        for (int x = 0; x < 4; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                positions.Add(new Vector3(-1f + x * 4.5f, 0.8f, 1.5f + z * 5f));
                positions.Add(new Vector3(-1f + x * 4.5f, 3.6f, 1.5f + z * 5f));
            }
        }
        probes.probePositions = positions.ToArray();
    }

    private static LightingSettings ConfigureLightingSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "YourQuest Title Environment Lighting" };
            AssetDatabase.CreateAsset(settings, LightingPath);
        }

        // note: Conservative bake settings prioritize a clean title vignette while keeping rebuild time and lightmap memory bounded.
        settings.realtimeGI = false;
        settings.bakedGI = true;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 18f;
        settings.lightmapMaxSize = 1024;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 128;
        settings.environmentSampleCount = 64;
        settings.maxBounces = 2;
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static void MarkStatic(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private static void EnsureIncludedInBuild()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existingIndex = scenes.FindIndex(scene =>
            string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            EditorBuildSettingsScene existing = scenes[existingIndex];
            if (!existing.enabled)
                scenes[existingIndex] = new EditorBuildSettingsScene(existing.path, true);
        }
        else
        {
            // note: PlaySafe remains build index zero; the enabled title scene is packaged solely for additive loading.
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
