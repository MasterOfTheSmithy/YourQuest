#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: Explicit one-shot requests exercise current loaded production code; ordinary Editor startup does not touch gameplay or profiles.
[InitializeOnLoad]
internal static class YQOriginClearingRepairVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/OriginClearingRepair.request";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static YQInvestorPlayerMotor portalMotor;
    private static Vector3 portalOriginal;
    private static Quaternion portalRotation;
    private static bool portalMotorEnabled;
    private static double portalStarted;
    private static int portalPhase;
    private static int initialRecoveries;
    private static bool interiorSupported, interiorRetained, exteriorReturned;
    private static string portalArtifact;
    static YQOriginClearingRepairVerification() => EditorApplication.update += Tick;
    private static void Tick()
    {
        if (portalPhase > 0) { PortalTick(); return; }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode = File.ReadAllText(Request).Trim();
        if (mode == "portals")
        {
            var builder = UnityEngine.Object.FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>();
            if (!EditorApplication.isPlaying || builder == null || builder.IsMaterializationInProgress) return;
            AssetDatabase.DeleteAsset(Request);
            if (builder.HasMaterializationFailed) { Write("Portal_Runtime", new { passed = false, reason = "World materialization rejected before assisted portal verification" }); return; }
            try { BeginPortals(); } catch (Exception error) { Debug.LogException(error); }
            return;
        }
        if (mode == "runtime" && (!EditorApplication.isPlaying ||
            !(bool)typeof(YQGeneratedWorldRuntimeBuilder).GetField("_hasGeneratedOriginSpawnOverride", PrivateStatic).GetValue(null))) return;
        if (mode != "runtime" && EditorApplication.isPlaying) return;
        AssetDatabase.DeleteAsset(Request);
        try { if (mode == "runtime") Runtime(); else Contracts(); }
        catch (Exception error) { Debug.LogException(error); }
    }
    private static void Write(string name, object report)
    {
        Directory.CreateDirectory("outputs/Origin_Clearing_Repair_20261006");
        File.WriteAllText("outputs/Origin_Clearing_Repair_20261006/" + name + ".json", JsonConvert.SerializeObject(report, Formatting.Indented));
    }
    private static void Contracts()
    {
        // note: Inspect the existing imported modules before choosing a physical snap axis or origin facade; this reads assets without changing them.
        object BoundsAt(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Bounds bounds = default; bool first = true;
            foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) continue;
                var local = mesh.sharedMesh.bounds;
                if (first) { bounds = local; first = false; } else bounds.Encapsulate(local);
            }
            return new { path, min = new { bounds.min.x, bounds.min.y, bounds.min.z }, max = new { bounds.max.x, bounds.max.y, bounds.max.z } };
        }
        Write("Approved_Asset_Bounds", new[] { BoundsAt("Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_CaveStraight.prefab"),
            BoundsAt("Assets/HIVEMIND/HouseOnaHill/HDRP/Art/Prefabs/SM_House.prefab") });
        var checks = YQCanopyPaletteRepairCases.Run(typeof(YQGeneratedWorldEnvironment), out int passed, out int failed);
        Write("Canopy_Contracts", new { evidence = "native loaded selector cases only", passed, failed, checks,
            runtimeMvid = typeof(YQGeneratedWorldEnvironment).Module.ModuleVersionId, utc = DateTime.UtcNow });
        if (failed > 0) throw new InvalidOperationException("Canopy repair cases failed: " + failed);
        var build = typeof(YQSemanticWorldAuthorityTests).GetMethod("BuildFixture", PrivateStatic);
        var plan = (GeneratedWorldPlanRecord)build.Invoke(null, new object[] { "semantic-v2-318" });
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var accepted, out string reason)) throw new InvalidOperationException(reason);
        plan.spatialPlanV2 = accepted;
        string canon = accepted.contentHash;
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        TerrainData data = new TerrainData { heightmapResolution = 257, size = new Vector3(1024f, 256f, 1024f) };
        try
        {
            GameObject go = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, preview);
            go.transform.position = new Vector3(-512f, 0f, -512f);
            Terrain terrain = go.GetComponent<Terrain>();
            var grade = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("GradeOriginClearing", PrivateStatic);
            bool first = (bool)grade.Invoke(null, new object[] { terrain, plan });
            float[,] a = data.GetHeights(0, 0, 257, 257);
            bool second = (bool)grade.Invoke(null, new object[] { terrain, plan });
            float[,] b = data.GetHeights(0, 0, 257, 257);
            float delta = 0f;
            for (int z = 0; z < 257; z++) for (int x = 0; x < 257; x++) delta = Mathf.Max(delta, Mathf.Abs(a[z, x] - b[z, x]));
            bool preserved = accepted.contentHash == canon;
            Write("Origin_Contracts", new { evidence = "detached native terrain repair", first, second, maximumRepeatDelta = delta,
                canonicalArtifactPreserved = preserved, shrineHeight = terrain.SampleHeight(YQGeneratedWorldLayout.OriginGoddessSummitOffset),
                hutHeight = terrain.SampleHeight(YQGeneratedWorldLayout.OriginWitchHouseOffset), utc = DateTime.UtcNow });
            if (!first || !second || delta > .00001f || !preserved) throw new InvalidOperationException("Origin repair idempotence or canon failed.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); UnityEngine.Object.DestroyImmediate(data); }
        Debug.Log("[YQOriginClearingRepairVerification] Native canopy 13 cases and origin idempotence PASS.");
    }
    private static void Runtime()
    {
        GameObject shrine = GameObject.Find("Origin_Goddess_MessengerMountain");
        GameObject hut = GameObject.Find("Origin_Vey_WitchHouse");
        Terrain terrain = (Terrain)typeof(YQGeneratedWorldRuntimeBuilder).GetField("_generatedTerrain", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(UnityEngine.Object.FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>());
        YQInvestorPlayerMotor motor = UnityEngine.Object.FindFirstObjectByType<YQInvestorPlayerMotor>();
        var plan = WorldStateManager.Instance.State.generatedWorldPlan;
        Vector3 requested = (Vector3)typeof(YQGeneratedWorldRuntimeBuilder).GetField("_generatedOriginSpawnOverride", PrivateStatic).GetValue(null);
        Vector3 resolved = (Vector3)typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("ResolveDryPlayerHorizontalPosition", PrivateStatic)
            .Invoke(null, new object[] { terrain, plan, requested, false });
        float floor = terrain.SampleHeight(resolved) + terrain.transform.position.y;
        bool water = YQWaterSurfaceMotion.TryGetWaterHeight(resolved, out float surface);
        bool dry = !water || surface < floor;
        float slope = terrain.terrainData.GetSteepness((resolved.x - terrain.transform.position.x) / terrain.terrainData.size.x,
            (resolved.z - terrain.transform.position.z) / terrain.terrainData.size.z);
        Transform statue = shrine.transform.Find("SM_AngelStatue_Origin");
        int dressing = shrine.transform.childCount - 1;
        // note: A separate disabled diagnostic camera records real geometry without moving the player, title camera, or accepted state.
        GameObject cameraObject = new GameObject("OriginRepairWitnessCamera");
        Camera camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = ~(1 << 31);
        camera.transform.position = resolved + Vector3.up * 1.8f;
        camera.transform.LookAt(statue.GetComponentInChildren<Renderer>().bounds.center);
        RenderTexture target = new RenderTexture(1280, 720, 24); Texture2D picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); picture.Apply();
            File.WriteAllBytes("outputs/Origin_Clearing_Repair_20261006/Origin_Player_View.png", picture.EncodeToPNG());
            camera.transform.position = new Vector3(85f, floor + 20f, -28f); camera.transform.LookAt(new Vector3(50f, floor + 4f, 17f));
            camera.Render(); picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); picture.Apply();
            File.WriteAllBytes("outputs/Origin_Clearing_Repair_20261006/Origin_Overview.png", picture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.Destroy(cameraObject); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(picture); }
        object Position(Vector3 value) => new { value.x, value.y, value.z };
        Write("Origin_Runtime", new { evidence = "production Continue world, diagnostic camera; requested arrival resolved without player teleport",
            seed = plan.worldSeed, artifact = plan.spatialPlanV2.contentHash, requested = Position(requested), resolved = Position(resolved), dry, floor, surface = water ? (float?)surface : null, slope,
            dressing, hutRenderers = hut.GetComponentsInChildren<Renderer>(true).Length, playerPosition = Position(motor.transform.position),
            gameplayReleased = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased, utc = DateTime.UtcNow });
        if (!dry || slope > 5f || dressing < 10 || hut.GetComponentsInChildren<Renderer>(true).Length < 100)
            throw new InvalidOperationException("Origin arrival or authored composition failed runtime checks.");
        Debug.Log("[YQOriginClearingRepairVerification] Production dry origin and furnished composition PASS (diagnostic camera).");
        File.WriteAllText(Request, "portals");
    }
    private static void BeginPortals()
    {
        // note: Assisted door execution is isolated behind the existing profile save barrier and never substitutes for ordinary player-input traversal.
        portalMotor = UnityEngine.Object.FindFirstObjectByType<YQInvestorPlayerMotor>();
        if (!YQDeveloperTestSession.Begin(out string failure)) throw new InvalidOperationException(failure);
        portalOriginal = portalMotor.transform.position; portalRotation = portalMotor.transform.rotation; portalMotorEnabled = portalMotor.enabled;
        YQDeveloperTestSession.OnRestore(() =>
        {
            var controller = portalMotor.GetComponent<CharacterController>(); bool active = controller.enabled;
            controller.enabled = false; portalMotor.transform.SetPositionAndRotation(portalOriginal, portalRotation); controller.enabled = active;
            portalMotor.enabled = portalMotorEnabled;
        });
        portalMotor.enabled = false;
        initialRecoveries = portalMotor.GetComponent<YQGeneratedWorldPlayerFallSafety>().RecoveryCount;
        portalArtifact = WorldStateManager.Instance.State.generatedWorldPlan.spatialPlanV2.contentHash;
        var enter = GameObject.Find("Origin_Vey_HutEntrance").GetComponent<YQOriginSitePortal>();
        typeof(YQOriginSitePortal).GetMethod("TeleportAuthoritativePlayer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enter, new object[] { portalMotor.gameObject });
        Physics.SyncTransforms();
        portalStarted = EditorApplication.timeSinceStartup; portalPhase = 1;
    }
    private static void PortalTick()
    {
        if (!EditorApplication.isPlaying || portalMotor == null) { portalPhase = 0; return; }
        if (EditorApplication.timeSinceStartup - portalStarted < 2.5) return;
        if (portalPhase == 1)
        {
            Vector3 position = portalMotor.transform.position;
            interiorRetained = YQOriginSitePortal.IsInsideReviewedInterior(position);
            interiorSupported = Physics.Raycast(position + Vector3.up * .4f, Vector3.down, out RaycastHit floorHit, 2.4f, Physics.AllLayers, QueryTriggerInteraction.Ignore) && floorHit.normal.y >= .45f;
            var exit = GameObject.Find("Origin_Vey_HutExit").GetComponent<YQOriginSitePortal>();
            typeof(YQOriginSitePortal).GetMethod("TeleportAuthoritativePlayer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(exit, new object[] { portalMotor.gameObject });
            portalPhase = 2; portalStarted = EditorApplication.timeSinceStartup;
            return;
        }
        exteriorReturned = !YQOriginSitePortal.IsInsideReviewedInterior(portalMotor.transform.position) &&
            Vector2.Distance(new Vector2(portalMotor.transform.position.x, portalMotor.transform.position.z),
                new Vector2(YQGeneratedWorldLayout.OriginWitchHouseOffset.x, YQGeneratedWorldLayout.OriginWitchHouseOffset.z)) < 15f;
        bool noRecoveries = initialRecoveries == portalMotor.GetComponent<YQGeneratedWorldPlayerFallSafety>().RecoveryCount;
        bool canonPreserved = portalArtifact == WorldStateManager.Instance.State.generatedWorldPlan.spatialPlanV2.contentHash;
        bool restored = YQDeveloperTestSession.RestoreForPlayModeExit(out string failure);
        portalPhase = 0;
        Write("Portal_Runtime", new { evidence = "assisted existing-door execution; save barrier and physical floor checks", interiorSupported, interiorRetained,
            exteriorReturned, noRecoveries, canonPreserved, restored, failure, utc = DateTime.UtcNow });
        if (!interiorSupported || !interiorRetained || !exteriorReturned || !noRecoveries || !canonPreserved || !restored)
            Debug.LogError("[YQOriginClearingRepairVerification] Assisted furnished hut portal check FAILED.");
        else Debug.Log("[YQOriginClearingRepairVerification] Assisted furnished hut entry, supported interior and return PASS.");
    }
}
#endif
