using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class YQSpatialBlueprintV2Tests
{
    [UnityEditor.MenuItem("YourQuest/Verification/Verify Readonly Blueprint Hash")]
    public static void VerifyReadonlyBlueprintHash()
    {
        // note: This named Edit Mode check reads the selected world's artifact and writes only its focused verification receipt.
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Blueprint hash verification requires Edit Mode.");
        var report = new System.Text.StringBuilder("# G08-R1 blueprint integrity and copy removal\n\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Evidence: Edit Mode contracts and microbenchmark; not a runtime frame-rate result.");
        bool passed = false;
        try
        {
            var sparse = new GeneratedSpatialWorldPlanV2Record();
            sparse.blueprint.regions.Add(new YQRegionDomainV2 { biomeTags = null });
            sparse.blueprint.terrainFields.Add(new YQTerrainFieldV2 { controlPoints = null, tags = null });
            sparse.blueprint.hydrology.Add(new YQHydrologyFeatureV2 { controlPoints = null, tags = null });
            sparse.blueprint.sites.Add(new YQSiteAnchorV2 { requiredFunctions = null, memberFootprint = null, tags = null });
            sparse.blueprint.routes.Add(new YQRouteCorridorV2 { controlPoints = null, crossings = null, tags = null });
            AssertReadonlyHashParity(sparse, "Nested absent collections", report);
            if (sparse.blueprint.sites[0].requiredFunctions != null ||
                sparse.blueprint.terrainFields[0].controlPoints != null ||
                sparse.blueprint.routes[0].crossings != null)
                throw new InvalidOperationException("Readonly hashing normalized accepted source collections.");
            sparse.blueprint.regions = null;
            sparse.blueprint.terrainFields = null;
            sparse.blueprint.hydrology = null;
            sparse.blueprint.sites = null;
            sparse.blueprint.routes = null;
            sparse.blueprint.relationships = null;
            AssertReadonlyHashParity(sparse, "Absent top-level collections", report);

            string cacheFailure = TestMaterializationCacheIntegrity();
            if (!string.IsNullOrEmpty(cacheFailure))
                throw new InvalidOperationException(cacheFailure);
            report.AppendLine("PASS: Changed accepted bytes reject cached preparation; restoring bytes recovers the original projection.");

            // note: Deserialize a detached read of the existing save; no profile selection, normalization, generation, or persistence operation is invoked.
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "world_state.json");
            WorldState saved = JsonUtility.FromJson<WorldState>(System.IO.File.ReadAllText(savePath));
            GeneratedSpatialWorldPlanV2Record artifact = saved?.generatedWorldPlan?.spatialPlanV2;
            if (artifact?.blueprint == null)
                throw new InvalidOperationException("The selected saved world has no V2 blueprint to measure.");
            AssertReadonlyHashParity(artifact, "Selected saved blueprint", report);
            string observedHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact);
            if (!string.Equals(artifact.contentHash, observedHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Selected saved blueprint differs from its accepted content hash.");
            report.AppendLine("PASS: Persisted content hash " + observedHash + "; seed " + artifact.worldSeed + ".");
            VerifyPreparedScatterClearance(saved.generatedWorldPlan, report);

            const int iterations = 16;
            MeasureBlueprintHash(artifact, true, 1, out _, out _);
            MeasureBlueprintHash(artifact, false, 1, out _, out _);
            MeasureBlueprintHash(artifact, true, iterations, out double legacyMs, out long legacyBytes);
            MeasureBlueprintHash(artifact, false, iterations, out double readonlyMs, out long readonlyBytes);
            report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "\n{0} iterations per method, same artifact:\n\n| Method | Mean ms/call | Allocated bytes/call |\n| --- | ---: | ---: |\n| JSON copy + canonical hash | {1:F3} | {2} |\n| Readonly canonical hash | {3:F3} | {4} |",
                iterations, legacyMs, legacyBytes > 0 ? legacyBytes.ToString() : "unavailable", readonlyMs, readonlyBytes > 0 ? readonlyBytes.ToString() : "unavailable"));
            if (legacyBytes <= 0 || readonlyBytes <= 0)
                report.AppendLine("Allocation counter returned zero in this Unity runtime; zero is not evidence of allocation-free hashing.");
            passed = true;
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
        }
        report.AppendLine("\nResult: " + (passed ? "PASS" : "FAIL"));
        string receipt = "Docs/G08_R1_Hash_Integrity_2026-09-24.md";
        System.IO.File.WriteAllText(receipt, report.ToString());
        if (passed)
            Debug.Log("[YQBlueprintHashVerification] PASS: " + receipt);
        else
            Debug.LogError("[YQBlueprintHashVerification] FAIL: " + receipt);
    }

    private static void VerifyPreparedScatterClearance(GeneratedWorldPlanRecord plan, System.Text.StringBuilder report)
    {
        // note: Compare the old anchor-resolution path with the immutable projection at both sides of each exact clearance boundary; only detached saved data is read.
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
            throw new InvalidOperationException(failure);
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var timer = new System.Diagnostics.Stopwatch();
        double oldMs = 0, preparedMs = 0;
        int checks = 0;
        for (int index = 0; index < prepared.SiteCount; index++)
        {
            var site = prepared.GetSite(index);
            string methodName = site.kind == YQSiteKindV2.Settlement ? "NearAnySettlement" :
                site.kind == YQSiteKindV2.HostileSite ? "NearAnyEncampment" : null;
            if (methodName == null) continue;
            var method = typeof(YQGeneratedWorldEnvironment).GetMethod(methodName, flags);
            foreach (float offset in new[] { -80.01f, -80f, -79.99f, 0f, 79.99f, 80f, 80.01f })
            {
                object[] args = { plan, null, new Vector3(site.x + offset, 17f, site.z), 80f, null };
                timer.Restart();
                bool previous = (bool)method.Invoke(null, args);
                timer.Stop(); oldMs += timer.Elapsed.TotalMilliseconds;
                args[4] = prepared;
                timer.Restart();
                bool reused = (bool)method.Invoke(null, args);
                timer.Stop(); preparedMs += timer.Elapsed.TotalMilliseconds;
                if (previous != reused) throw new InvalidOperationException("Prepared clearance changed " + methodName + " at offset " + offset);
                checks++;
            }
        }
        if (checks == 0) throw new InvalidOperationException("No settlement/camp clearance boundaries were checked.");
        report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "PASS: {0} settlement/camp clearance comparisons match exactly; mean existing {1:F3} ms, prepared {2:F3} ms (reflection included; not FPS).",
            checks, oldMs / checks, preparedMs / checks));
    }

    [UnityEditor.MenuItem("YourQuest/Verification/Verify Streamed Ribbon Widths")]
    public static void VerifyStreamedRibbonWidths()
    {
        // note: Exercise the production mesh emitter on disposable Editor objects; the named operation writes only a focused receipt.
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ribbon verification requires Edit Mode.");
        var report = new System.Text.StringBuilder("# G08-R1 streamed feature width integrity\n\n");
        report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Evidence: production mesh construction in Edit Mode; manual visual acceptance remains pending.");
        bool passed = false;
        try
        {
            string failure = TestStreamedRibbonWidths();
            if (!string.IsNullOrEmpty(failure))
                throw new InvalidOperationException(failure);
            report.AppendLine("PASS: Joined accepted features retain both copies of their shared endpoint and their independent widths.");
            report.AppendLine("PASS: Road and river meshes retain widths of 4 m and 12 m in one cell.");
            report.AppendLine("PASS: The same 4 m span remains identical when the wider neighbor is absent.");
            report.AppendLine("PASS: Repeated break indices do not split or discard the following span.");
            report.AppendLine("PASS: Misaligned width metadata fails closed.");
            failure = TestStreamedFeatureSurfaces(report);
            if (!string.IsNullOrEmpty(failure))
                throw new InvalidOperationException(failure);
            passed = true;
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
        }
        report.AppendLine("\nResult: " + (passed ? "PASS" : "FAIL"));
        string receipt = "Docs/G08_R1_Ribbon_Integrity_2026-09-24.md";
        System.IO.File.WriteAllText(receipt, report.ToString());
        if (passed)
            Debug.Log("[YQRibbonVerification] PASS: " + receipt);
        else
            Debug.LogError("[YQRibbonVerification] FAIL: " + receipt);
    }

    private static string TestStreamedRibbonWidths()
    {
        // note: Reflection stays in this Editor test; the runtime emitter retains its private API and existing owner.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        Type owner = typeof(YQContinuousWorldFeatureMaterializer);
        var append = owner.GetMethod("AppendAcceptedSegmentPoints", flags);
        var complete = owner.GetMethod("CompleteAcceptedRibbonFeature", flags);
        var build = owner.GetMethod("BuildRibbonSpans", flags);
        var points = new List<Vector3>();
        var widths = new List<float>();
        var breaks = new List<int>();
        for (int feature = 0; feature < 2; feature++)
        {
            int firstPoint = points.Count;
            // note: Separate accepted identities deliberately share their endpoint; the second feature must not lose its first mesh vertex.
            object[] arguments = { points, 0f, feature * 8f, 0f, (feature + 1) * 8f,
                0f, 0f, -64f, 64f, -64f, 64f, 0f, -64f, 64f, -64f, 64f,
                0f, 100f, 0f, breaks, false, Vector2.zero };
            append.Invoke(null, arguments);
            complete.Invoke(null, new object[] { points, widths, breaks, firstPoint, feature == 0 ? 4f : 12f });
        }
        if (breaks.Count != 1 || widths.Count != points.Count ||
            points[breaks[0]] != points[breaks[0] - 1] || widths[breaks[0] - 1] != 4f || widths[breaks[0]] != 12f)
            return "Accepted feature endpoints, breaks, or independent widths were lost during extraction.";
        int split = breaks[0];
        breaks.Add(split);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return "The existing URP shader is unavailable for disposable mesh verification.";
        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        var root = new GameObject("YQRibbonWidthVerification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            foreach (bool water in new[] { false, true })
            {
                string prefix = water ? "Water" : "Road";
                int count = (int)build.Invoke(null, new object[] { root.transform, prefix, points, breaks, 12f, material, water, 128f, widths });
                if (count != 2)
                    return prefix + " lost a span or emitted a bridge between separate features.";
                Mesh narrow = root.transform.Find(prefix + "_Span0").GetComponent<MeshFilter>().sharedMesh;
                Mesh wide = root.transform.Find(prefix + "_Span1").GetComponent<MeshFilter>().sharedMesh;
                Mesh[] meshes = { narrow, wide };
                for (int spanIndex = 0; spanIndex < meshes.Length; spanIndex++)
                {
                    // note: Each straight span's vertex pairs expose its actual emitted width without relying on renderer bounds.
                    Vector3[] vertices = meshes[spanIndex].vertices;
                    float expected = spanIndex == 0 ? 4f : 12f;
                    for (int vertex = 0; vertex < vertices.Length; vertex += 2)
                        if (Mathf.Abs(Vector3.Distance(vertices[vertex], vertices[vertex + 1]) - expected) > 0.0001f)
                            return prefix + " borrowed another feature's width.";
                    Vector2[] uv = meshes[spanIndex].uv;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                        if ((uv[vertex] - new Vector2(vertices[vertex].x, vertices[vertex].z) * 0.25f).sqrMagnitude > 0.000001f)
                            return prefix + " lost its world-space texture scale.";
                    if (water)
                    {
                        Color[] colours = meshes[spanIndex].colors;
                        if (colours.Length != vertices.Length)
                            return "The river has no shader bank/coverage data.";
                        for (int vertex = 0; vertex < colours.Length; vertex += 2)
                            if (colours[vertex].g != 0f || colours[vertex + 1].g != 1f ||
                                colours[vertex].a != 1f || colours[vertex + 1].a != 1f)
                                return "River bank interpolation would hide the whole water surface.";
                    }
                }
                var isolatedPoints = points.GetRange(0, split);
                var isolatedWidths = widths.GetRange(0, split);
                int isolated = (int)build.Invoke(null, new object[] { root.transform, prefix + "Alone", isolatedPoints, null, 4f, material, water, 128f, isolatedWidths });
                if (isolated != 1)
                    return "An isolated feature failed mesh construction.";
                Vector3[] alone = root.transform.Find(prefix + "Alone_Span0").GetComponent<MeshFilter>().sharedMesh.vertices;
                Vector3[] combined = narrow.vertices;
                if (alone.Length != combined.Length)
                    return "A neighboring feature changed the narrow span's vertex count.";
                for (int index = 0; index < alone.Length; index++)
                    if (alone[index] != combined[index])
                        return "A neighboring feature changed the narrow span's geometry.";
            }
            int malformed = (int)build.Invoke(null, new object[] { root.transform, "Malformed", points, breaks, 12f, material, false, 128f, new List<float>() });
            if (malformed != 0)
                return "A misaligned width stream was accepted.";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(material);
        }
        return string.Empty;
    }

    private static string TestStreamedFeatureSurfaces(System.Text.StringBuilder report)
    {
        // note: Use the selected saved lake and an already-approved imported path in a disposable preview scene; no scene, profile, or imported asset is rewritten.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        Type owner = typeof(YQContinuousWorldFeatureMaterializer);
        var findMaterial = owner.GetMethod("FindMaterial", flags);
        var buildArea = owner.GetMethod("BuildAcceptedAreaWaterPatches", flags);
        var buildRibbon = owner.GetMethod("BuildRibbonSpans", flags);
        YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
        const string pathAsset = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_MudMesh.prefab";
        Renderer importedRenderer = registry.ResolvePrefab(pathAsset)?.GetComponentInChildren<Renderer>(true);
        Material importedMaterial = importedRenderer != null ? importedRenderer.sharedMaterial : null;
        if (importedMaterial == null)
            return "The approved path surface is unavailable.";
        string importedBefore = UnityEditor.EditorJsonUtility.ToJson(importedMaterial);
        Texture expectedTexture = importedMaterial.HasProperty("_BaseTexture") ? importedMaterial.GetTexture("_BaseTexture") : null;
        Material road = (Material)findMaterial.Invoke(null, new object[] { registry, new[] { pathAsset } });
        Material water = null;
        TerrainData terrainData = null;
        var meshes = new List<Mesh>();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            if (road == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(road) ||
                expectedTexture == null || !road.HasProperty("_BaseMap") || road.GetTexture("_BaseMap") != expectedTexture ||
                importedBefore != UnityEditor.EditorJsonUtility.ToJson(importedMaterial))
                return "The streamed path failed lazy-shard resolution, texture-preserving URP conversion, or source preservation.";
            report.AppendLine("PASS: The root registry's approved Nordic path resolves through its pack shard, retains the imported texture, and leaves the source material unchanged.");
            water = (Material)findMaterial.Invoke(null, new object[] { registry, new[] { "water", "river" } });
            if (water == null || water.shader.name != "YourQuest/Generated Water" || water.GetFloat("_WorldUV") != 1f)
                return "Streamed water did not receive the project water shader and world-coordinate texture contract.";
            // note: A nonzero flow speed cannot animate a flat default normal. Require the actual imported refraction normal to survive binding.
            var waterSource = ((Renderer)typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveApprovedWaterMaterialSource", flags)
                .Invoke(null, new object[] { registry })).sharedMaterial;
            Texture importedRipple = waterSource.HasProperty("_RefractionNormal") ? waterSource.GetTexture("_RefractionNormal") :
                (Texture)typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveWaterRippleNormal", flags).Invoke(null, new object[] { waterSource });
            if (importedRipple == null || water.GetTexture("_BumpMap") != importedRipple || water.GetFloat("_FlowSpeed") <= 0f)
                return "Streamed water lost its imported ripple normal or flow speed.";
            report.AppendLine("PASS: Imported moving ripple texture " + importedRipple.name + " is bound to the water shader with positive flow speed.");
            WorldState saved = JsonUtility.FromJson<WorldState>(System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.persistentDataPath, "world_state.json")));
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(saved.generatedWorldPlan, out var prepared, out string failure))
                return "Saved lake preparation failed: " + failure;
            int lakeIndex = -1;
            for (int index = 0; index < prepared.WaterCount; index++)
                if (prepared.GetWater(index).kind == YQHydrologyKindV2.Lake) { lakeIndex = index; break; }
            if (lakeIndex < 0)
                return "The saved world has no accepted lake for this check.";
            var lake = prepared.GetWaterPoint(lakeIndex, 0);
            const float cellSize = 128f;
            var cell = new Vector2Int(Mathf.FloorToInt((lake.x + 512f) / cellSize), Mathf.FloorToInt((lake.z + 512f) / cellSize));
            terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(cellSize, 128f, cellSize) };
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrainObject.transform.position = new Vector3(-512f + cell.x * cellSize, 0f, -512f + cell.y * cellSize);
            terrainObject.SetActive(false);
            var root = new GameObject("StreamedSurfacePreview") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            int areaCount = (int)buildArea.Invoke(null, new object[] { root.transform, terrainObject.GetComponent<Terrain>(), prepared, cell, cellSize, water });
            if (areaCount < 1)
                return "The saved lake emitted no surface in its owning cell.";
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                meshes.Add(mesh);
                Color[] colours = mesh.colors;
                if (colours.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount)
                    return "The lake has no shader bank/coverage or texture data.";
                foreach (Color colour in colours)
                    if (colour.g != 0.5f || colour.a != 1f)
                        return "The lake's bank values would fade its entire interior.";
            }
            report.AppendLine("PASS: The saved lake builds a surface with full interior shader coverage; river banks interpolate from 0 to 1 with full coverage.");
            float surfaceY = lake.waterSurfaceNormalized * 128f + 0.045f;
            var roadPoints = new List<Vector3> { new Vector3(lake.x + 75f, surfaceY + 4f, lake.z - 24f), new Vector3(lake.x + 75f, surfaceY + 4f, lake.z), new Vector3(lake.x + 75f, surfaceY + 4f, lake.z + 24f) };
            buildRibbon.Invoke(null, new object[] { root.transform, "PathSurface", roadPoints, null, 5f, road, false, 128f, null });
            var riverPoints = new List<Vector3>();
            for (int sample = 0; sample <= 10; sample++)
                riverPoints.Add(new Vector3(lake.x - 75f, surfaceY + 4f, lake.z - 40f + sample * 8f));
            int riverCount = (int)buildRibbon.Invoke(null, new object[] { root.transform, "RiverSurface", riverPoints, null, 8f, water, true, 128f, null });
            if (riverCount != 1)
                return "The preview's ordinary sampled river failed to materialize.";
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                if (!meshes.Contains(filter.sharedMesh)) meshes.Add(filter.sharedMesh);
            var cameraObject = new GameObject("SurfaceEvidenceCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .09f, .1f);
            camera.orthographic = true;
            camera.orthographicSize = 100f;
            camera.transform.SetPositionAndRotation(new Vector3(lake.x, surfaceY + 150f, lake.z), Quaternion.Euler(90f, 0f, 0f));
            var lightObject = new GameObject("SurfaceEvidenceLight", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            lightObject.GetComponent<Light>().intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
            CaptureStreamedSurfacePreview(camera);
            report.AppendLine("Preview: Docs/G08_R1_Surface_Contract_Preview.png (isolated shader/material evidence; not a gameplay screenshot).");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            foreach (Mesh mesh in meshes) if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (terrainData != null) UnityEngine.Object.DestroyImmediate(terrainData);
            if (road != null) UnityEngine.Object.DestroyImmediate(road);
            if (water != null) UnityEngine.Object.DestroyImmediate(water);
        }
        return string.Empty;
    }

    private static void CaptureStreamedSurfacePreview(Camera camera)
    {
        // note: Capture only the disposable preview scene, restoring render state and releasing every temporary buffer.
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(768, 768, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
            pixels.Apply();
            System.IO.File.WriteAllBytes("Docs/G08_R1_Surface_Contract_Preview.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    private static void AssertReadonlyHashParity(GeneratedSpatialWorldPlanV2Record artifact,
        string label, System.Text.StringBuilder report)
    {
        // note: Preserve the previous detached-copy calculation as the comparison and independently assert that the source bytes did not change.
        string before = JsonUtility.ToJson(artifact);
        string observed = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact);
        string legacy = YQSpatialBlueprintHasherV2.ComputeContentHash(
            JsonUtility.FromJson<GeneratedSpatialWorldPlanV2Record>(before));
        if (!string.Equals(observed, legacy, StringComparison.Ordinal) ||
            !string.Equals(before, JsonUtility.ToJson(artifact), StringComparison.Ordinal))
            throw new InvalidOperationException(label + ": content hash parity or source immutability failed.");
        report.AppendLine("PASS: " + label + " retain the canonical hash without source mutation.");
    }

    private static void MeasureBlueprintHash(GeneratedSpatialWorldPlanV2Record artifact,
        bool copyJson, int iterations, out double meanMilliseconds, out long meanAllocatedBytes)
    {
        // note: Measure only the replaced lookup operation; this deliberately makes no prediction about whole-game frame rate.
        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int index = 0; index < iterations; index++)
        {
            if (copyJson)
                YQSpatialBlueprintHasherV2.ComputeContentHash(
                    JsonUtility.FromJson<GeneratedSpatialWorldPlanV2Record>(JsonUtility.ToJson(artifact)));
            else
                YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact);
        }
        stopwatch.Stop();
        meanMilliseconds = stopwatch.Elapsed.TotalMilliseconds / iterations;
        meanAllocatedBytes = (GC.GetAllocatedBytesForCurrentThread() - beforeBytes) / iterations;
    }

    public static int RunAll(out int tested)
    {
        int failures = 0;
        tested = 0;
        failures += Run("streamed ribbon widths remain independent across accepted identities", TestStreamedRibbonWidths, ref tested);
        failures += Run(
            "causal terrain-water-road blueprint",
            TestCausalBlueprint,
            ref tested);
        failures += Run(
            "streamed beta fixture is route-connected and bounded",
            TestStreamedBetaFixtureContract,
            ref tested);
        failures += Run(
            "semantic input ordering independence",
            TestInputOrderingIndependence,
            ref tested);
        failures += Run(
            "settlement water intent preserves dry inland reserves",
            TestSettlementWaterIntent,
            ref tested);
        failures += Run(
            "blueprint validator rejects broken causality",
            TestValidatorRejectsBrokenCausality,
            ref tested);
        failures += Run("blueprint acceptance enforces measured water requirements",
            TestWaterRequirementAcceptance, ref tested);
        failures += Run("legacy acceptance revalidates without rewriting persisted evidence",
            TestLegacyWaterAcceptance, ref tested);
        failures += Run("shadow compilation cannot replace accepted invalid content",
            TestAcceptedShadowPreservation, ref tested);
        failures += Run("shared water query measures bank width and rejects malformed geometry",
            TestWaterAccessGeometry, ref tested);
        failures += Run(
            "frame-yielding shadow compilation",
            TestIncrementalShadowCompilation,
            ref tested);
        failures += Run(
            "prepared terrain-water-route-site sampler",
            TestPreparedTerrainSampler,
            ref tested);
        failures += Run(
            "road edges conform independently to terrain cross-slope",
            TestLivedPathCrossSlope,
            ref tested);
        failures += Run(
            "origin landmark route detour preserves the protected shrine reserve",
            TestOriginLandmarkRouteDetour,
            ref tested);
        failures += Run(
            "compiled cell grounding requires coherent foundation contacts",
            TestCompiledCellFoundationCoherence,
            ref tested);
        failures += Run("structural settlement anchors outrank decorative tag matches",
            TestStructuralAnchorPriority, ref tested);
        failures += Run("semantic cache follows source, manifest and selection policy",
            TestSemanticSelectionCacheIdentity, ref tested);
        failures += Run("procedural settlement geometry survives default world JSON serialization",
            TestProceduralLayoutWorldSerialization, ref tested);
        failures += Run("settlement earthworks preserve terrain outside blocks and streets",
            TestConstructionEarthworkFootprint, ref tested);
        failures += Run("terrain tree heights compensate scaled imported base offsets",
            TestTerrainTreeBaseOffset, ref tested);
        failures += Run("built-in lit materials cannot pass URP validation",
            TestBuiltInMaterialPipelineCompatibility, ref tested);
        failures += Run("site terrain sampling rejects positions outside its owner",
            TestGeneratedTerrainContainment, ref tested);
        failures += Run(
            "missing mesh material slots are repaired before publication",
            TestMissingMeshMaterialRepair,
            ref tested);
        failures += Run(
            "spawned asset LOD ownership and bounds are stabilized",
            TestSpawnedAssetLodStability,
            ref tested);
        failures += Run(
            "enemy movement presentation preserves combat renderers",
            TestEnemyMovementPresentationVisibility,
            ref tested);
        failures += Run(
            "streamed and enemy collider envelopes reject invisible walls",
            TestColliderEnvelopeIntegrity,
            ref tested);
        failures += Run(
            "shared runtime V2 height sampling preserves authority",
            TestSharedRuntimeHeightSampler,
            ref tested);
        failures += Run(
            "single-source materialization projection",
            TestMaterializationProjection,
            ref tested);
        failures += Run("detached candidate preparation preserves live artifacts",
            TestDetachedCandidatePreparation, ref tested);
        failures += Run("gameplay destinations follow accepted V2 coordinates",
            TestRuntimeDestinationProjection, ref tested);
        failures += Run(
            "materialization cache rejects mutable blueprint drift",
            TestMaterializationCacheIntegrity,
            ref tested);
        failures += Run(
            "prepared hydrology and ecology projection",
            TestPreparedEnvironmentProjection,
            ref tested);
        failures += Run(
            "strict semantic settlement composition",
            TestStrictSemanticSettlementComposition,
            ref tested);
        failures += Run(
            "semantic composition rejects missing roles",
            TestSemanticCompositionRejectsMissingRoles,
            ref tested);
        failures += Run("cell candidate population is conservative and idempotent",
            TestFunctionCandidatePopulation, ref tested);
        failures += Run("exact-cell functional evidence and shell restrictions",
            TestSelectedCellFunctions, ref tested);
        failures += Run("functional evidence rejects stale sources and empty sockets",
            TestInvalidFunctionEvidence, ref tested);
        failures += Run("streaming subcells cannot inherit district-wide functions",
            TestPartialDistrictFunctionCoverage, ref tested);
        failures += Run("prefab cells prioritize usable functions over descriptive tags",
            () => TestFunctionDrivenCellSelection(false), ref tested);
        failures += Run("streamed cells prioritize usable functions over descriptive tags",
            () => TestFunctionDrivenCellSelection(true), ref tested);
        failures += Run("reviewed doors reuse one provider and authored collider",
            TestReviewedDoorBinding, ref tested);
        failures += Run("door bindings reject drafts, stale geometry and ambiguous paths",
            TestDoorBindingRejections, ref tested);
        failures += Run("generated door state is stable and instance-scoped",
            TestGeneratedDoorPersistence, ref tested);
        failures += Run("owned generation preserves waits and successful parent continuation",
            TestOwnedGenerationSuccess, ref tested);
        failures += Run("nested generation exceptions unwind without continuing the parent",
            TestOwnedGenerationFailure, ref tested);
        failures += Run("owned generation cancellation disposes children before parents",
            TestOwnedGenerationCancellation, ref tested);
        failures += Run("immediate generation iterator chains yield between bounded batches",
            TestOwnedGenerationSchedulingBudget, ref tested);
        failures += Run("canonical population bindings preserve saved death state",
            TestCanonicalPopulationBindingAcceptance, ref tested);
        failures += Run("canonical population rejects duplicate or unsupported site assignments",
            TestCanonicalPopulationBindingRejection, ref tested);
        failures += Run("foundation cache identity follows exact reviewed cell selection",
            TestFoundationSelectionCacheIdentity, ref tested);
        failures += Run("prepared-site readiness follows the exact active owner",
            TestPreparedSiteReadinessLifecycle, ref tested);
        failures += Run("owned generation unwinds every parent after cleanup failure",
            TestOwnedGenerationCleanupFailure, ref tested);
        failures += Run("resident feet retain reviewed support across creation and relocation",
            TestResidentReviewedSupport, ref tested);
        failures += Run("resident separation is bounded, deterministic and rejects exhausted supports",
            TestResidentSeparationBounds, ref tested);
        return failures;
    }

    private static string TestGeneratedTerrainContainment()
    {
        // note: Exact tile borders are valid; neighboring positions must not silently sample a clamped heightmap edge.
        var method = typeof(YQCompiledWorldSiteInstance).GetMethod("ContainsGeneratedTerrainPoint",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool Contains(Vector3 point) => (bool)method.Invoke(null,
            new object[] { new Vector3(-50f, 10f, -25f), new Vector3(100f, 80f, 50f), point });
        if (!Contains(new Vector3(-50f, 0f, -25f)) || !Contains(new Vector3(50f, 0f, 25f)))
            return "A valid terrain boundary was rejected.";
        if (Contains(new Vector3(50.01f, 0f, 0f)) || Contains(new Vector3(0f, 0f, -25.01f)) ||
            Contains(new Vector3(float.NaN, 0f, 0f)))
            return "An out-of-bounds or invalid site position was accepted.";
        return null;
    }

    private static string TestBuiltInMaterialPipelineCompatibility()
    {
        // note: Repair and final validation share this predicate; test known incompatible shaders without requiring a graphics device.
        var method = typeof(YQRuntimeUrpMaterialRepair).GetMethod("IsPipelineCompatible",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool Compatible(string shader, string tag, bool urp) =>
            (bool)method.Invoke(null, new object[] { shader, tag, urp });
        foreach (string shader in new[] { "Standard", "Standard (Specular setup)", "Legacy Shaders/Diffuse", "HDRP/Lit" })
        {
            if (Compatible(shader, "", true)) return "Incompatible shader passed URP validation: " + shader;
            if (!Compatible(shader, "", false)) return "The URP-specific check changed another pipeline's behavior.";
        }
        if (!Compatible("Universal Render Pipeline/Lit", "UniversalPipeline", true) ||
            !Compatible("TextMeshPro/Distance Field", "", true) ||
            !Compatible("Shader Graphs/Vendor", "UniversalPipeline", true) ||
            Compatible("Shader Graphs/Vendor", "HDRenderPipeline", true))
            return "Supported shaders or tagged HDRP graphs were misclassified.";
        return null;
    }

    private static string TestTerrainTreeBaseOffset()
    {
        // note: Reconstruct the final visible base from the exact native-instance height calculation, across offsets, scales and elevated terrain origins.
        var method = typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveTerrainTreeNormalizedHeight",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        foreach (float offset in new[] { -3f, 0f, 2f })
            foreach (float scale in new[] { 0.7f, 1f, 1.8f })
            {
                float normalized = (float)method.Invoke(null, new object[] { 42f, 20f, 100f, offset, scale });
                float visibleBase = 20f + normalized * 100f + offset * scale;
                if (Mathf.Abs(visibleBase - 41.95f) > 0.0001f)
                    return "Scaled tree geometry does not contact the sampled terrain.";
            }
        float belowMinimum = (float)method.Invoke(null, new object[] { 0f, 0f, 100f, 2f, 1f });
        if (belowMinimum >= 0f) return "Clamping the pivot reintroduced a floating tree at minimum terrain height.";
        return null;
    }

    private static string TestConstructionEarthworkFootprint()
    {
        // note: Test the actual mask consumed by the terrain writer, including heading and saved-origin transforms.
        var method = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("ResolveConstructionEarthworkWeight",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        var layout = new YQProceduralSettlementLayoutRecord
        {
            origin = new Vector3(5f, -3f, 7f),
            cells = new List<YQProceduralCellPlacement>
            {
                new YQProceduralCellPlacement { boundsCenter = new Vector3(35f, 0f, 7f), boundsSize = new Vector3(10f, 8f, 10f) }
            },
            streets = new List<YQProceduralStreet>
            {
                new YQProceduralStreet { start = new Vector3(5f, 0f, -33f), end = new Vector3(5f, 0f, 47f), width = 4f }
            }
        };
        float Weight(float x, float z, float heading = 0f) =>
            (float)method.Invoke(null, new object[] { layout, new Vector2(x, z), heading });
        if (Weight(30f, 0f) != 1f || Weight(0f, 20f) != 1f)
            return "A foundation or declared street lost full terrain support.";
        if (Weight(-30f, 0f) != 0f || Weight(0f, 60f) != 0f)
            return "Empty town land or terrain beyond the street cap is still flattened.";
        if (Mathf.Abs(Weight(0f, -30f, 90f) - 1f) > 0.001f)
            return "Earthworks no longer align with the rotated site.";
        float shoulder = Weight(45f, 0f);
        if (shoulder <= 0f || shoulder >= 1f)
            return "The building shelf lacks a gradual local transition.";
        return null;
    }

    private static string TestProceduralLayoutWorldSerialization()
    {
        // note: Exercise the same explicit Unity-value converters and strict loop policy used by production world saves; raw JsonConvert would enumerate Vector3's computed properties and create a false renderOrigin failure.
        var world = WorldState.CreateDefault();
        world.generatedWorldPlan = new GeneratedWorldPlanRecord
        {
            settlements = new List<GeneratedSettlementRecord>
            {
                new GeneratedSettlementRecord
                {
                    settlementId = "serialization_fixture",
                    proceduralLayout = new YQProceduralSettlementLayoutRecord
                    {
                        origin = new Vector3(12f, -3f, 25f),
                        cells = new List<YQProceduralCellPlacement>
                        {
                            new YQProceduralCellPlacement { cellId = "home", position = new Vector3(4f, 5f, 6f),
                                boundsCenter = new Vector3(7f, 8f, 9f), boundsSize = new Vector3(10f, 11f, 12f), yaw = 90f }
                        },
                        streets = new List<YQProceduralStreet>
                        {
                            new YQProceduralStreet { start = new Vector3(1f, 2f, 3f), end = new Vector3(4f, 5f, 6f), width = 3f }
                        }
                    }
                }
            }
        };
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Error,
            Converters =
            {
                new Vector3JsonConverter(),
                new Vector2JsonConverter(),
                new QuaternionJsonConverter()
            }
        };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(world, settings);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(json, settings);
        string roundTrip = Newtonsoft.Json.JsonConvert.SerializeObject(restored.generatedWorldPlan.settlements[0].proceduralLayout, settings);
        string expected = Newtonsoft.Json.JsonConvert.SerializeObject(world.generatedWorldPlan.settlements[0].proceduralLayout, settings);
        if (roundTrip != expected || json.Contains("\"normalized\"") || json.Contains("\"magnitude\""))
            return "Procedural geometry changed or computed vector properties leaked into the world save.";
        return null;
    }

    private static string TestSemanticSelectionCacheIdentity()
    {
        // note: Exercise the runtime key builder directly; these cases previously shared cache entries despite selecting different geometry.
        var method = typeof(YQCompiledWorldSiteInstance).GetMethod("BuildSemanticSelectionCacheIdentity",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) return "Semantic cache identity builder is missing.";
        string Key(string source, int instance, string seed, string[] tags, bool structural) =>
            (string)method.Invoke(null, new object[] { "kit", source, instance, seed, tags, structural });
        string baseline = Key("sourceA", 1, "seed", new[] { "home", "service" }, true);
        if (baseline != Key("sourceA", 1, "seed", new[] { "home", "service" }, true))
            return "Identical requests cannot reuse their selection.";
        var alternatives = new[]
        {
            Key("sourceB", 1, "seed", new[] { "home", "service" }, true),
            Key("sourceA", 2, "seed", new[] { "home", "service" }, true),
            Key("sourceA", 1, "seed", new[] { "home", "service" }, false),
            Key("sourceA", 1, "seed", new[] { "service", "home" }, true),
            Key("sourceA", 1, "seed", new[] { "home,service" }, true)
        };
        foreach (string alternative in alternatives)
            if (baseline == alternative) return "Distinct semantic selection requests share a cache entry.";
        // note: A separator inside a seed must not move text into the following tag field.
        if (Key("sourceA", 1, "seed|home", new[] { "service" }, true) ==
            Key("sourceA", 1, "seed", new[] { "home|service" }, true))
            return "Seed/tag delimiter ambiguity remains.";
        return null;
    }

    private static string TestStructuralAnchorPriority()
    {
        // note: Exercise the runtime comparison directly without loading assets or changing saved-world selection policy.
        var owner = typeof(YQCompiledWorldSiteInstance);
        var candidateType = owner.GetNestedType("SemanticCellCandidate", System.Reflection.BindingFlags.NonPublic);
        var compare = owner.GetMethod("CompareSemanticCellPriority", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (candidateType == null || compare == null) return "Runtime anchor priority contract is missing.";
        object Candidate(int score, bool structure, int functionCount)
        {
            object value = Activator.CreateInstance(candidateType, true);
            candidateType.GetField("semanticScore").SetValue(value, score);
            candidateType.GetField("preferredStructuralAnchor").SetValue(value, structure);
            candidateType.GetField("functions").SetValue(value, new YQAssetFunctionV2[functionCount]);
            return value;
        }
        int Compare(object left, object right) => (int)compare.Invoke(null, new[] { left, right });

        object prop = Candidate(1000, false, 0);
        object building = Candidate(100, true, 0);
        if (Compare(building, prop) >= 0 || Compare(prop, building) <= 0)
            return "A higher descriptive tag score overrode measured structural evidence.";
        // note: Verified function providers remain authoritative even when a non-provider has measured support.
        object provider = Candidate(100, false, 1);
        if (Compare(provider, building) >= 0)
            return "Structural preference displaced an approved function provider.";
        // note: An old save has neither structural flag; its original tag ordering must be unchanged.
        if (Compare(prop, Candidate(100, false, 0)) >= 0)
            return "Legacy tag priority changed without opting into structural selection.";
        if (Compare(building, Candidate(100, true, 0)) != 0 ||
            Compare(building, Candidate(90, true, 0)) >= 0)
            return "Equivalent anchor bands or within-band semantic ordering are inconsistent.";
        return null;
    }

    private static string TestCompiledCellFoundationCoherence()
    {
        var method = typeof(YQCompiledWorldSiteInstance).GetMethod(
            "TryResolveCompiledFoundationCorrection",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public,
            null,
            new[]
            {
                typeof(List<Vector2>),
                typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType()
            },
            null);
        if (method == null)
            return "Compiled foundation correction resolver was not found.";

        List<Vector2> coherent = new List<Vector2>
        {
            new Vector2(1.00f, 5f),
            new Vector2(1.08f, 3f),
            new Vector2(0.94f, 2f)
        };
        object[] coherentArgs = { coherent, 0f, 0f };
        if (!(bool)method.Invoke(null, coherentArgs) ||
            Mathf.Abs((float)coherentArgs[1] - 1f) > 0.1f ||
            (float)coherentArgs[2] < 0.99f)
        {
            return "A coherent group of foundation contacts was rejected.";
        }

        // note: Equal-weight structures requiring incompatible vertical corrections represent a genuinely broken authored cell, not a value that one median can safely conceal.
        List<Vector2> splitFoundations = new List<Vector2>
        {
            new Vector2(-0.9f, 1f),
            new Vector2(0.9f, 1f)
        };
        object[] splitArgs = { splitFoundations, 0f, 0f };
        if ((bool)method.Invoke(null, splitArgs) ||
            (float)splitArgs[2] >= 0.82f)
        {
            return "Incompatible foundation contacts were accepted.";
        }

        // note: The solver may lower or raise a rigid cell within the asymmetric contact limits when that preserves the greatest supported weight.
        object[] floatingArgs = { new List<Vector2>
        {
            new Vector2(0f, 6f), new Vector2(-0.4f, 4f)
        }, 0f, 0f };
        if (!(bool)method.Invoke(null, floatingArgs) ||
            !AllContactsSupported(floatingArgs))
            return "A feasible floating-secondary correction was rejected.";

        // note: The opposite offset is foundation embedment, which has a separate, deliberately larger allowance.
        object[] embeddedArgs = { new List<Vector2>
        {
            new Vector2(0f, 6f), new Vector2(0.4f, 4f)
        }, 0f, 0f };
        if (!(bool)method.Invoke(null, embeddedArgs) ||
            !AllContactsSupported(embeddedArgs))
            return "Permitted foundation embedment was rejected.";

        // note: A spread wider than the combined air-gap and penetration limits cannot share one rigid correction.
        object[] unsupportedFloatingArgs = { new List<Vector2>
        {
            new Vector2(0f, 6f), new Vector2(-1f, 4f)
        }, 0f, 0f };
        if ((bool)method.Invoke(null, unsupportedFloatingArgs) ||
            Mathf.Abs((float)unsupportedFloatingArgs[2] - 0.6f) > 0.001f)
            return "An irreconcilable floating secondary foundation was accepted.";

        object[] buriedArgs = { new List<Vector2>
        {
            new Vector2(0f, 6f), new Vector2(1.0f, 4f)
        }, 0f, 0f };
        if ((bool)method.Invoke(null, buriedArgs) ||
            Mathf.Abs((float)buriedArgs[2] - 0.6f) > 0.001f)
            return "Excessive foundation penetration was accepted.";

        List<Vector2> invalid = new List<Vector2>
        {
            new Vector2(float.NaN, 1f)
        };
        object[] invalidArgs = { invalid, 0f, 0f };
        if ((bool)method.Invoke(null, invalidArgs))
            return "A nonfinite foundation correction was accepted.";

        return null;

        // note: Check the final physical contacts independently of the strategy used to select a shared correction.
        bool AllContactsSupported(object[] args)
        {
            float correction = (float)args[1];
            float supportRatio = (float)args[2];
            if (float.IsNaN(correction) || float.IsInfinity(correction) ||
                float.IsNaN(supportRatio) || float.IsInfinity(supportRatio) ||
                Mathf.Abs(supportRatio - 1f) > 0.001f)
                return false;
            foreach (Vector2 sample in (List<Vector2>)args[0])
            {
                if (correction - sample.x > 0.18001f || sample.x - correction > 0.65001f)
                    return false;
            }
            return true;
        }
    }

    private static string TestMissingMeshMaterialRepair()
    {
        GameObject root = new GameObject("V2_MissingMaterialFixture");
        Mesh mesh = new Mesh { name = "V2_TwoSurfaceMesh" };
        Mesh sourceLodMesh = null;
        Mesh missingLodMesh = null;
        Material sourceLodMaterial = null;
        MeshFilter filter = root.AddComponent<MeshFilter>();
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        try
        {
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 1 }, 1);
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = Array.Empty<Material>();

            // note: A renderer with real submeshes but no serialized material array must receive one valid runtime surface per submesh.
            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
            Material[] repaired = renderer.sharedMaterials;
            if (repaired == null || repaired.Length < 2)
                return "Repair did not restore every required mesh material slot.";
            for (int index = 0; index < 2; index++)
            {
                if (repaired[index] == null || repaired[index].shader == null ||
                    !repaired[index].shader.isSupported)
                {
                    return "Repair left an unusable mesh material slot.";
                }
            }

            Shader sourceShader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            if (sourceShader == null)
                return "No supported shader was available for LOD material recovery coverage.";

            GameObject sourceLod = new GameObject("AncientOak_LOD0");
            sourceLod.transform.SetParent(root.transform, false);
            sourceLodMesh = UnityEngine.Object.Instantiate(mesh);
            sourceLodMesh.name = "AncientOak_LOD0";
            sourceLod.AddComponent<MeshFilter>().sharedMesh = sourceLodMesh;
            MeshRenderer sourceLodRenderer = sourceLod.AddComponent<MeshRenderer>();
            sourceLodMaterial = new Material(sourceShader)
            {
                name = "AncientOak_Bark_Source"
            };
            sourceLodRenderer.sharedMaterials =
                new[] { sourceLodMaterial, sourceLodMaterial };

            GameObject missingLod = new GameObject("AncientOak_LOD1");
            missingLod.transform.SetParent(root.transform, false);
            missingLodMesh = UnityEngine.Object.Instantiate(mesh);
            missingLodMesh.name = "AncientOak_LOD1";
            missingLod.AddComponent<MeshFilter>().sharedMesh = missingLodMesh;
            MeshRenderer missingLodRenderer = missingLod.AddComponent<MeshRenderer>();
            missingLodRenderer.sharedMaterials = Array.Empty<Material>();

            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
            Material[] recoveredLodMaterials = missingLodRenderer.sharedMaterials;
            if (recoveredLodMaterials.Length < 2 ||
                recoveredLodMaterials[0] == null ||
                recoveredLodMaterials[1] == null ||
                recoveredLodMaterials[0].name.IndexOf(
                    "AncientOak_Bark_Source",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "A missing LOD surface did not recover its valid sibling material family.";
            }

            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(mesh);
            if (sourceLodMesh != null)
                UnityEngine.Object.DestroyImmediate(sourceLodMesh);
            if (missingLodMesh != null)
                UnityEngine.Object.DestroyImmediate(missingLodMesh);
            if (sourceLodMaterial != null)
                UnityEngine.Object.DestroyImmediate(sourceLodMaterial);
        }
    }

    private static string TestEnemyMovementPresentationVisibility()
    {
        GameObject root = new GameObject("V2_EnemyVisibilityFixture");
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Enemy_CombatVisual";
        visual.transform.SetParent(root.transform, false);
        try
        {
            YQInvestorEnemy enemy = root.AddComponent<YQInvestorEnemy>();
            var usesBurrowField = typeof(YQInvestorEnemy).GetField(
                "_usesBurrowMovement",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var setBurrowMethod = typeof(YQInvestorEnemy).GetMethod(
                "SetBurrowMovementActive",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            if (usesBurrowField == null || setBurrowMethod == null)
                return "Enemy movement presentation contract was not found.";

            usesBurrowField.SetValue(enemy, true);
            Renderer renderer = visual.GetComponent<Renderer>();
            setBurrowMethod.Invoke(enemy, new object[] { true });
            if (!renderer.enabled)
                return "Moving enemy presentation disabled the combat renderer.";
            setBurrowMethod.Invoke(enemy, new object[] { false });
            if (!renderer.enabled)
                return "Ending enemy movement did not preserve the combat renderer.";

            Renderer[] renderers = { renderer };
            bool[] baseline = { true };
            renderer.enabled = false;
            var restoreMethod = typeof(YQGeneratedEnemyRuntimeSafety).GetMethod(
                "RestoreBaselineRendererVisibility",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (restoreMethod == null)
                return "Enemy renderer continuity guard was not found.";
            int restored = (int)restoreMethod.Invoke(
                null,
                new object[] { renderers, baseline });
            if (restored != 1 || !renderer.enabled)
                return "Enemy renderer continuity did not recover a fully hidden live model.";

            // note: A moving enemy may add a ground tell, but its targetable and attacking model remains continuously visible.
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string TestColliderEnvelopeIntegrity()
    {
        Bounds visible = new Bounds(Vector3.up, new Vector3(2f, 2f, 2f));
        Bounds closeCollider = new Bounds(Vector3.up, new Vector3(2.5f, 2.4f, 2.3f));
        Bounds invisibleWall = new Bounds(new Vector3(4f, 1f, 0f),
            new Vector3(12f, 3f, 3f));
        Bounds detachedCollider = new Bounds(new Vector3(5f, 1f, 0f),
            new Vector3(2f, 2f, 2f));

        var siteMethod = typeof(YQCompiledWorldSiteInstance).GetMethod(
            "IsColliderVisuallyOversized",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        var enemyMethod = typeof(YQGeneratedEnemyRuntimeSafety).GetMethod(
            "IsColliderEnvelopeCredible",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        if (siteMethod == null || enemyMethod == null)
            return "Collider envelope integrity contracts were not found.";

        if ((bool)siteMethod.Invoke(null, new object[] { closeCollider, visible }))
            return "A close-fitting streamed collider was classified as an invisible wall.";
        if (!(bool)siteMethod.Invoke(null, new object[] { invisibleWall, visible }))
            return "An oversized streamed collider was not classified as an invisible wall.";
        if (!(bool)siteMethod.Invoke(null, new object[] { detachedCollider, visible }))
            return "A detached renderer-less collider was not classified as an invisible wall.";
        if (!(bool)enemyMethod.Invoke(null, new object[] { closeCollider, visible }))
            return "A close-fitting enemy collider was rejected.";
        if ((bool)enemyMethod.Invoke(null, new object[] { invisibleWall, visible }))
            return "An oversized detached enemy collider was accepted.";

        // note: Both streamed structures and enemies retain modest simplified collision while rejecting visibly detached blocking envelopes.
        return null;
    }

    private static string TestSpawnedAssetLodStability()
    {
        GameObject root = new GameObject("V2_LodStabilityFixture");
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(root.transform, false);
        try
        {
            Renderer renderer = visual.GetComponent<Renderer>();
            LODGroup group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(new[]
            {
                new LOD(0.7f, new[] { renderer, renderer }) { fadeTransitionWidth = 0.25f },
                new LOD(0.2f, new[] { renderer }),
                new LOD(0.01f, Array.Empty<Renderer>())
            });

            int repaired = YQRuntimeUrpMaterialRepair.StabilizeLodHierarchy(root);
            LOD[] lods = group.GetLODs();
            if (repaired != 1 || !group.enabled || lods.Length != 2)
                return "Blank LOD levels survived repair or valid shared-renderer levels were removed.";
            if (lods[0].renderers.Length != 1 || lods[0].renderers[0] != renderer ||
                lods[1].renderers.Length != 1 || lods[1].renderers[0] != renderer)
                return "Spawned-asset LOD repair lost its authoritative renderer.";
            // note: Shared trunks across tree LODs and authored fades are valid; only duplicates within one level are malformed.
            if (group.fadeMode != LODFadeMode.CrossFade || !group.animateCrossFading ||
                !Mathf.Approximately(lods[0].screenRelativeTransitionHeight, 0.7f) ||
                !Mathf.Approximately(lods[1].screenRelativeTransitionHeight, 0.2f) ||
                !Mathf.Approximately(lods[0].fadeTransitionWidth, 0.25f))
                return "Spawned-asset LOD repair changed valid imported transitions.";
            if (float.IsNaN(group.size) || float.IsInfinity(group.size) || group.size <= 0f)
                return "Spawned-asset LOD bounds were not recalculated.";

            // note: A renderer may repeat within its owner, but a parent controller must relinquish it to the deepest group.
            LODGroup childGroup = visual.AddComponent<LODGroup>();
            childGroup.SetLODs(new[] { new LOD(0.1f, new[] { renderer }) });
            YQRuntimeUrpMaterialRepair.StabilizeLodHierarchy(root);
            if (group.enabled || !childGroup.enabled ||
                childGroup.GetLODs()[0].renderers[0] != renderer)
                return "Nested spawned-asset LOD groups retained competing renderer ownership.";

            // note: Runtime-scaled actors keep one authoritative renderer owner and fresh bounds without degrading imported presentation.
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string TestLivedPathCrossSlope()
    {
        TerrainData data = new TerrainData
        {
            heightmapResolution = 33,
            size = new Vector3(32f, 10f, 32f)
        };
        float[,] heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        {
            for (int x = 0; x < 33; x++)
                heights[z, x] = (x / 32f) * 0.5f;
        }
        data.SetHeights(0, 0, heights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.transform.position = new Vector3(10f, -2f, 20f);
        Terrain terrain = terrainObject.GetComponent<Terrain>();

        try
        {
            var method = typeof(YQGeneratedWorldEnvironment).GetMethod(
                "TryResolveLivedPathEdgePair",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (method == null)
                return "Road terrain-contact resolver was not found.";

            Vector2 center = new Vector2(26f, 36f);
            object[] args =
            {
                terrain,
                center,
                Vector2.up,
                4f,
                Vector3.zero,
                Vector3.zero,
                Vector3.up,
                Vector3.up
            };
            if (!(bool)method.Invoke(null, args))
                return "Valid cross-slope road contact was rejected.";

            Vector3 left = (Vector3)args[4];
            Vector3 right = (Vector3)args[5];
            Vector3 leftNormal = (Vector3)args[6];
            Vector3 rightNormal = (Vector3)args[7];
            const float expectedRoadSurfaceOffset = 0.028f;
            float expectedLeftY = terrain.transform.position.y +
                data.GetInterpolatedHeight(
                    (left.x - terrain.transform.position.x) / data.size.x,
                    (left.z - terrain.transform.position.z) / data.size.z) +
                expectedRoadSurfaceOffset;
            float expectedRightY = terrain.transform.position.y +
                data.GetInterpolatedHeight(
                    (right.x - terrain.transform.position.x) / data.size.x,
                    (right.z - terrain.transform.position.z) / data.size.z) +
                expectedRoadSurfaceOffset;

            // note: Both edges must meet their own terrain contacts; sharing centerline height would recreate the visible floating/buried ribbon defect.
            if (Mathf.Abs(left.y - expectedLeftY) > 0.001f ||
                Mathf.Abs(right.y - expectedRightY) > 0.001f ||
                Mathf.Abs(left.y - right.y) < 0.1f)
            {
                return "Road edges did not independently follow the terrain cross-slope.";
            }
            if (Vector3.Dot(leftNormal, Vector3.up) > 0.9999f ||
                Vector3.Dot(rightNormal, Vector3.up) > 0.9999f)
            {
                return "Road edge normals ignored the underlying terrain slope.";
            }

            object[] outsideArgs =
            {
                terrain,
                new Vector2(-100f, -100f),
                Vector2.up,
                4f,
                Vector3.zero,
                Vector3.zero,
                Vector3.up,
                Vector3.up
            };
            object[] zeroTangentArgs =
            {
                terrain,
                center,
                Vector2.zero,
                4f,
                Vector3.zero,
                Vector3.zero,
                Vector3.up,
                Vector3.up
            };
            if ((bool)method.Invoke(null, outsideArgs) ||
                (bool)method.Invoke(null, zeroTangentArgs))
            {
                return "Invalid road contact was accepted.";
            }

            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static string TestOriginLandmarkRouteDetour()
    {
        Vector2 center = new Vector2(
            YQGeneratedWorldTerrain.OriginWorldPosition.x + YQGeneratedWorldLayout.OriginGoddessSummitOffset.x,
            YQGeneratedWorldTerrain.OriginWorldPosition.z + YQGeneratedWorldLayout.OriginGoddessSummitOffset.z);
        Vector2 start = center + new Vector2(0f, 45f);
        Vector2 end = center + new Vector2(0f, -45f);
        bool shrineDetourBuilt = YQGeneratedWorldLayout.TryBuildOriginLandmarkDetour(
            start, end, 1.25f, out Vector2[] waypoints);
        if (!shrineDetourBuilt || waypoints == null || waypoints.Length == 0)
        {
            return "A route crossing the authored shrine reserve did not receive a detour. start=" + start +
                ", end=" + end +
                ", startInsideReserve=" + YQGeneratedWorldLayout.IsOriginLandmarkRouteControlPointInsideReserve(start, 1.25f) +
                ", endInsideReserve=" + YQGeneratedWorldLayout.IsOriginLandmarkRouteControlPointInsideReserve(end, 1.25f) +
                ", built=" + shrineDetourBuilt +
                ", waypointCount=" + (waypoints == null ? -1 : waypoints.Length);
        }

        Vector2 previous = start;
        for (int segment = 0; segment <= waypoints.Length; segment++)
        {
            Vector2 next = segment < waypoints.Length ? waypoints[segment] : end;
            for (int sample = 1; sample < 32; sample++)
            {
                Vector2 point = Vector2.Lerp(previous, next, sample / 32f);
                // note: Keep every route centre outside the shrine's radial
                // reserve expanded by its own corridor width.
                if ((point - center).sqrMagnitude < 8.25f * 8.25f)
                    return "The detoured route still entered the protected shrine reserve.";
            }
            previous = next;
        }

        // note: An accepted control inside the WitchHouse reserve must not disable the physical route's exterior detour.
        Vector2 witchHouseCenter = new Vector2(
            YQGeneratedWorldTerrain.OriginWorldPosition.x + YQGeneratedWorldLayout.OriginWitchHouseOffset.x,
            YQGeneratedWorldTerrain.OriginWorldPosition.z + YQGeneratedWorldLayout.OriginWitchHouseOffset.z);
        List<Vector2> projectedRoute = new List<Vector2>
        {
            witchHouseCenter + new Vector2(-40f, 0f),
            witchHouseCenter + new Vector2(-5f, 0f),
            witchHouseCenter + new Vector2(40f, 0f)
        };
        YQGeneratedWorldLayout.FilterOriginLandmarkRouteControlPoints(
            projectedRoute,
            1.25f);
        if (projectedRoute.Count != 2)
            return "An interior accepted route control was not removed before segment detouring.";
        if (!YQGeneratedWorldLayout.TryBuildOriginLandmarkDetour(
                projectedRoute[0], projectedRoute[1], 1.25f, out waypoints) ||
            waypoints == null || waypoints.Length == 0)
            return "A route collapsed across the WitchHouse reserve did not receive a detour.";

        float witchHouseRouteRadius = 14f + 1.25f + 2f;
        float goddessRouteRadius = 7f + 1.25f + 1f;
        Vector2 goddessCenter = new Vector2(
            YQGeneratedWorldTerrain.OriginWorldPosition.x + YQGeneratedWorldLayout.OriginGoddessSummitOffset.x,
            YQGeneratedWorldTerrain.OriginWorldPosition.z + YQGeneratedWorldLayout.OriginGoddessSummitOffset.z);
        previous = projectedRoute[0];
        for (int segment = 0; segment <= waypoints.Length; segment++)
        {
            Vector2 next = segment < waypoints.Length ? waypoints[segment] : projectedRoute[1];
            for (int sample = 1; sample < 32; sample++)
            {
                Vector2 point = Vector2.Lerp(previous, next, sample / 32f);
                if ((point - witchHouseCenter).sqrMagnitude < witchHouseRouteRadius * witchHouseRouteRadius)
                    return "The route still entered the WitchHouse collider reserve after collapsing its interior control.";
                if ((point - goddessCenter).sqrMagnitude < goddessRouteRadius * goddessRouteRadius)
                    return "The combined origin detour crossed the Goddess reserve while clearing the WitchHouse.";
            }
            previous = next;
        }
        return null;
    }

    private static string TestResidentReviewedSupport()
    {
        // note: Primitive fixtures exercise offset/scaled pivots without loading imported NPCs or requiring a running physics loop.
        GameObject root = new GameObject("V2_ResidentSupportFixture");
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject empty = new GameObject("V2_EmptyResidentFixture");
        try
        {
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 2f, 0.6f);
            Renderer renderer = visual.GetComponent<Renderer>();
            CharacterController controller = root.AddComponent<CharacterController>();
            foreach (float scale in new[] { 0.65f, 1f, 1.7f })
            {
                root.transform.localScale = new Vector3(1f, scale, 1f);
                root.transform.position = new Vector3(2f, -4f, 3f);
                controller.enabled = true;
                Vector3 support = new Vector3(10f, 6f, -8f);
                if (!YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(root, support) ||
                    Mathf.Abs(renderer.bounds.min.y - (support.y - 0.005f)) > 0.001f || !controller.enabled)
                    return "Scaled/offset resident feet did not meet the reviewed support or collision was disabled.";
                Vector3 firstPosition = root.transform.position;
                if (!YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(root, support) ||
                    (root.transform.position - firstPosition).sqrMagnitude > 0.000001f)
                    return "Repeated placement drifted away from the same floor.";
                controller.enabled = false;
                support = new Vector3(-3f, 12f, 4f);
                if (!YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(root, support) ||
                    Mathf.Abs(renderer.bounds.min.y - (support.y - 0.005f)) > 0.001f || controller.enabled)
                    return "Relocation lost the feet offset or enabled a deliberately disabled controller.";
                if (Mathf.Abs(root.transform.position.x - support.x) > 0.001f ||
                    Mathf.Abs(root.transform.position.z - support.z) > 0.001f)
                    return "Relocation failed to preserve the selected horizontal anchor.";
            }
            Vector3 unchanged = root.transform.position;
            if (YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(root, new Vector3(float.NaN, 1f, 0f)) ||
                root.transform.position != unchanged ||
                YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(empty, Vector3.zero))
                return "Invalid support or missing contact geometry was accepted.";
            return null;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(empty);
        }
    }

    private static string TestResidentSeparationBounds()
    {
        // note: Reflect only the private placement helper in this editor fixture; production keeps its existing typed site-query boundary.
        var method = typeof(YQGeneratedWorldPopulation).GetMethod("TryResolveSeparatedResidentPosition",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Vector3 anchor = new Vector3(7f, 3f, -2f);
        List<Vector3> occupied = new List<Vector3>();
        bool exhausted = false;
        for (int index = 0; index < 10; index++)
        {
            object[] args = { anchor, occupied, "resident-separation-fixture", null, Vector3.zero };
            bool found = (bool)method.Invoke(null, args);
            object[] repeat = { anchor, occupied, "resident-separation-fixture", null, Vector3.zero };
            if (found != (bool)method.Invoke(null, repeat) || (Vector3)args[4] != (Vector3)repeat[4])
                return "Repeated placement changed for the same occupied positions and seed.";
            if (!found)
            {
                exhausted = true;
                break;
            }
            Vector3 position = (Vector3)args[4];
            if ((position - anchor).magnitude > 10.451f || Mathf.Abs(position.y - anchor.y) > 0.001f)
                return "Separation walked beyond the bounded role-anchor search.";
            foreach (Vector3 previous in occupied)
                if ((position - previous).sqrMagnitude < 2.4f * 2.4f - 0.0001f)
                    return "An unchecked final candidate overlapped an earlier resident.";
            occupied.Add(position);
        }
        if (!exhausted || occupied.Count == 0)
            return "The finite set of placement candidates did not exhaust safely.";
        object[] unsupported = { anchor, new List<Vector3>(), "resident-separation-fixture",
            "missing-site-fixture-" + Guid.NewGuid().ToString("N"), Vector3.zero };
        return !(bool)method.Invoke(null, unsupported)
            ? null : "A missing reviewed floor silently accepted a terrain fallback.";
    }

    private static string TestPreparedSiteReadinessLifecycle()
    {
        // note: Editor-only reflection arranges private streaming state without loading assets or widening the runtime API for tests.
        Type type = typeof(YQCompiledWorldSiteInstance);
        const System.Reflection.BindingFlags instanceFlags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags staticFlags =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var instances = (Dictionary<string, YQCompiledWorldSiteInstance>)
            type.GetField("Instances", staticFlags).GetValue(null);
        var activeLoaderField = type.GetField("ActiveStreamLoader", staticFlags);
        var currentMethod = type.GetMethod("IsCurrentPreparedSite", staticFlags);
        var loadingField = type.GetField("loading", instanceFlags);
        var loadedField = type.GetField("loaded", instanceFlags);
        object previousLoader = activeLoaderField.GetValue(null);
        string locationId = "site-readiness-fixture-" + Guid.NewGuid().ToString("N");
        GameObject root = new GameObject("V2_SiteReadinessFixture");
        GameObject otherRoot = new GameObject("V2_OtherSiteFixture");
        IEnumerator waiter = null;
        try
        {
            YQCompiledWorldSiteInstance site = root.AddComponent<YQCompiledWorldSiteInstance>();
            YQCompiledWorldSiteInstance other = otherRoot.AddComponent<YQCompiledWorldSiteInstance>();
            type.GetField("settlementId", instanceFlags).SetValue(site, locationId);
            instances.Add(locationId, site);
            bool IsCurrent() => (bool)currentMethod.Invoke(null, new object[] { locationId, site });

            loadedField.SetValue(site, true);
            if (!IsCurrent() || !YQCompiledWorldSiteInstance.IsSiteLoaded(locationId))
                return "A completed active site was not available.";
            root.SetActive(false);
            if (IsCurrent() || YQCompiledWorldSiteInstance.IsSiteLoaded(locationId))
                return "A disabled site retained playable readiness.";
            root.SetActive(true);
            site.enabled = false;
            if (IsCurrent() || YQCompiledWorldSiteInstance.IsSiteLoaded(locationId))
                return "A disabled streaming component retained playable readiness.";
            site.enabled = true;

            foreach (string flag in new[] { "loading", "unloading", "loadRejected" })
            {
                var field = type.GetField(flag, instanceFlags);
                field.SetValue(site, true);
                if (YQCompiledWorldSiteInstance.IsSiteLoaded(locationId))
                    return "The site was reported ready while " + flag + ".";
                field.SetValue(site, false);
            }

            // note: Another site's occupied slot must not delay a captured site that finished while this caller was waiting.
            loadedField.SetValue(site, false);
            loadingField.SetValue(site, true);
            activeLoaderField.SetValue(null, other);
            int callbacks = 0;
            bool accepted = false;
            waiter = YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(locationId,
                result => { callbacks++; accepted = result; });
            if (!waiter.MoveNext() || waiter.Current != null || callbacks != 0)
                return "An unfinished site did not wait cooperatively.";
            loadingField.SetValue(site, false);
            loadedField.SetValue(site, true);
            if (waiter.MoveNext() || !accepted || callbacks != 1 ||
                !ReferenceEquals(activeLoaderField.GetValue(null), other))
                return "Completed-site waiting stole another site's slot or did not finish exactly once.";

            // note: Reusing a semantic ID on a replacement root must invalidate the original request's ownership.
            instances[locationId] = other;
            if (IsCurrent())
                return "A replacement root inherited an old site's materialization request.";
            instances.Remove(locationId);
            return !IsCurrent() ? null : "An unregistered site retained request ownership.";
        }
        finally
        {
            (waiter as IDisposable)?.Dispose();
            instances.Remove(locationId);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(otherRoot);
            activeLoaderField.SetValue(null, previousLoader);
        }
    }

    private static string TestOwnedGenerationCleanupFailure()
    {
        // note: One broken child cleanup must not prevent parent cleanup or report a successful continuation.
        List<string> events = new List<string>();
        Exception failure = null;
        int reports = 0;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            CleanupFailureParent(events), exception => { failure = exception; reports++; });
        try
        {
            if (!execution.MoveNext())
                return "The cleanup-failure fixture did not suspend.";
            (execution as IDisposable)?.Dispose();
            if (execution.MoveNext())
                return "Cancelled work resumed after cleanup failure.";
            AggregateException aggregate = failure as AggregateException;
            return aggregate != null && aggregate.Flatten().InnerExceptions.Count == 2 && reports == 1 &&
                string.Join(",", events) == "child-disposed,parent-disposed"
                ? null : "Cleanup errors were lost, repeated, or skipped a parent.";
        }
        finally { (execution as IDisposable)?.Dispose(); }
    }

    private static IEnumerator CleanupFailureParent(List<string> events)
    {
        try { yield return CleanupFailureChild(events); events.Add("parent-resumed"); }
        finally
        {
            events.Add("parent-disposed");
            throw new InvalidOperationException("Intentional parent cleanup regression fixture.");
        }
    }

    private static IEnumerator CleanupFailureChild(List<string> events)
    {
        try { yield return null; }
        finally
        {
            events.Add("child-disposed");
            throw new InvalidOperationException("Intentional child cleanup regression fixture.");
        }
    }

    private static string TestFoundationSelectionCacheIdentity()
    {
        // note: Distinct cell layouts from one kit must not borrow each other's measured foundation correction.
        string first = YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey(
            "kit", "manifest", "seed", new[] { "cell_b", "cell_a" }, 3f);
        string reordered = YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey(
            "kit", "manifest", "seed", new[] { "cell_a", "cell_b" }, 3f);
        if (first != reordered)
            return "Cell enumeration order changed the cache identity.";
        if (first == YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey(
                "kit", "manifest", "seed", new[] { "cell_a", "cell_c" }, 3f))
            return "Different selected cells share a foundation cache key.";
        if (first == YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey(
                "kit", "manifest", "seed", new[] { "cell_a", "cell_b" }, 4f))
            return "Different authored datums share a foundation cache key.";
        if (first == YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey(
                "kit", "other_manifest", "seed", new[] { "cell_a", "cell_b" }, 3f))
            return "Different source manifests share a foundation cache key.";
        string unsliced = YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey("kit", "manifest", "seed", null, 0f);
        string empty = YQCompiledWorldSiteInstance.BuildFoundationSelectionCacheKey("kit", "manifest", "seed", Array.Empty<string>(), 0f);
        return unsliced != empty ? null : "Empty explicit selection aliases the full-site selection.";
    }

    private static GeneratedWorldPlanRecord BuildPopulationBindingFixture()
    {
        // note: Minimal saved identities exercise placement contracts without importing prefabs or mutating project assets.
        return new GeneratedWorldPlanRecord
        {
            settlements = new List<GeneratedSettlementRecord> { new GeneratedSettlementRecord { settlementId = "town_a" } },
            encampments = new List<GeneratedEncampmentRecord> { new GeneratedEncampmentRecord { encampmentId = "camp_a" } },
            generatedNpcs = new List<GeneratedNpcPlanRecord>
            {
                new GeneratedNpcPlanRecord { npcId = "resident_a", settlementId = "town_a" },
                new GeneratedNpcPlanRecord { npcId = "commander_a", encampmentId = "camp_a", hostile = true }
            }
        };
    }

    private static string TestCanonicalPopulationBindingAcceptance()
    {
        GeneratedWorldPlanRecord plan = BuildPopulationBindingFixture();
        string before = JsonUtility.ToJson(plan);
        if (!YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, null, out int expected, out string failure) || expected != 2)
            return "Valid bindings were rejected: " + failure;
        WorldState world = new WorldState();
        world.EnsureCollections();
        world.npcs.Add(new WorldState.NpcRecord { npcId = "resident_a", status = "dead" });
        if (!YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, world, out expected, out failure) || expected != 1)
            return "Saved death state did not exclude a resident: " + failure;
        return before == JsonUtility.ToJson(plan) ? null : "Binding validation rewrote canonical plan data.";
    }

    private static string TestCanonicalPopulationBindingRejection()
    {
        GeneratedWorldPlanRecord plan = BuildPopulationBindingFixture();
        plan.generatedNpcs[1].npcId = "RESIDENT_A";
        if (YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, null, out _, out _))
            return "Case-insensitive duplicate IDs were accepted.";
        plan = BuildPopulationBindingFixture();
        plan.generatedNpcs[0].settlementId = "absent_town";
        if (YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, null, out _, out _))
            return "An orphaned settlement binding was accepted.";
        plan = BuildPopulationBindingFixture();
        plan.generatedNpcs[0].encampmentId = "camp_a";
        if (YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, null, out _, out _))
            return "Ambiguous settlement and encampment bindings were accepted.";
        plan = BuildPopulationBindingFixture();
        plan.generatedNpcs.Add(new GeneratedNpcPlanRecord { npcId = "commander_b", encampmentId = "camp_a", hostile = true });
        if (YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, null, out _, out _))
            return "Multiple living commanders were accepted by a one-commander placement path.";
        // note: Historical dead commanders are retained, but do not occupy the current physical commander slot.
        WorldState world = new WorldState();
        world.EnsureCollections();
        world.npcs.Add(new WorldState.NpcRecord { npcId = "commander_a", status = "defeated" });
        return YQGeneratedWorldPopulation.TryValidatePopulationBindings(plan, world, out int count, out string failure) && count == 2
            ? null : "A historical defeated commander incorrectly blocked its living counterpart: " + failure;
    }

    private static string TestOwnedGenerationSuccess()
    {
        // note: A Unity wait object must reach the scheduler unchanged, not be synchronously drained.
        List<string> events = new List<string>();
        object wait = new WaitForSeconds(0.01f);
        Exception failure = null;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            OwnedGenerationParent(events, wait, false), exception => failure = exception);
        try
        {
            if (!execution.MoveNext() || !ReferenceEquals(execution.Current, wait))
                return "The child's wait object was not preserved.";
            if (execution.MoveNext() || failure != null)
                return "Successful nested work did not terminate cleanly.";
            return string.Join(",", events) == "child-disposed,parent-resumed,parent-disposed"
                ? null : "Parent continuation or disposal order was incorrect.";
        }
        finally { (execution as IDisposable)?.Dispose(); }
    }

    private static string TestOwnedGenerationFailure()
    {
        // note: A child exception must reach the owner exactly once and must not execute the parent's success path.
        List<string> events = new List<string>();
        Exception failure = null;
        int reports = 0;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            OwnedGenerationParent(events, null, true), exception => { failure = exception; reports++; });
        try
        {
            if (!execution.MoveNext() || execution.MoveNext())
                return "Failed nested work did not terminate after its first yielded frame.";
            return failure is InvalidOperationException && reports == 1 &&
                string.Join(",", events) == "child-disposed,parent-disposed"
                ? null : "Nested failure was lost, repeated, or resumed the parent.";
        }
        finally { (execution as IDisposable)?.Dispose(); }
    }

    private static string TestOwnedGenerationCancellation()
    {
        // note: Explicit disposal models cancellation without requiring a scene or a running Unity player loop.
        List<string> events = new List<string>();
        Exception failure = null;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            OwnedGenerationParent(events, null, false), exception => failure = exception);
        try
        {
            if (!execution.MoveNext())
                return "The cancellation fixture did not start.";
        }
        finally { (execution as IDisposable)?.Dispose(); }
        return failure == null && string.Join(",", events) == "child-disposed,parent-disposed"
            ? null : "Cancellation reported failure or left nested work undisposed.";
    }

    private static string TestOwnedGenerationSchedulingBudget()
    {
        bool completed = false;
        Exception failure = null;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            ManyImmediateGenerationSteps(() => completed = true), exception => failure = exception);
        try
        {
            if (!execution.MoveNext() || execution.Current != null || completed)
                return "Immediate nested work was not split across frames.";
            int frameBoundaries = 1;
            while (execution.MoveNext() && frameBoundaries < 32)
                frameBoundaries++;
            return completed && failure == null && frameBoundaries < 32
                ? null : "Bounded scheduling did not finish the finite iterator chain.";
        }
        finally { (execution as IDisposable)?.Dispose(); }
    }

    private static IEnumerator OwnedGenerationParent(List<string> events, object wait, bool fail)
    {
        try
        {
            yield return OwnedGenerationChild(events, wait, fail);
            events.Add("parent-resumed");
        }
        finally { events.Add("parent-disposed"); }
    }

    private static IEnumerator OwnedGenerationChild(List<string> events, object wait, bool fail)
    {
        try
        {
            yield return wait;
            if (fail)
                throw new InvalidOperationException("Intentional nested-generation regression fixture.");
        }
        finally { events.Add("child-disposed"); }
    }

    private static IEnumerator ManyImmediateGenerationSteps(Action completed)
    {
        // note: Empty child iterators still consume scheduling budget even when they contain no explicit frame yield.
        for (int i = 0; i < 256; i++)
            yield return EmptyGenerationStep();
        completed();
    }

    private static IEnumerator EmptyGenerationStep()
    {
        yield break;
    }

    private static int Run(
        string name,
        Func<string> test,
        ref int tested)
    {
        tested++;
        try
        {
            string failure = test();
            if (string.IsNullOrWhiteSpace(failure))
                return 0;

            Debug.LogError(
                "[YQWorldGenV2Tests] " + name + ": " + failure);
            return 1;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError(
                "[YQWorldGenV2Tests] " + name +
                " threw an exception.");
            return 1;
        }
    }

    private static string TestCausalBlueprint()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string failure))
        {
            return "Compilation failed: " + failure;
        }

        if (compiled.acceptanceState !=
            GeneratedSpatialPlanAcceptanceState.Accepted)
        {
            return "A validated blueprint was not accepted.";
        }

        if (!HasTerrain(compiled.blueprint, YQTerrainFieldKindV2.RidgeChain) ||
            !HasTerrain(compiled.blueprint, YQTerrainFieldKindV2.ValleyCorridor) ||
            !HasTerrain(compiled.blueprint, YQTerrainFieldKindV2.Basin) ||
            !HasTerrain(compiled.blueprint, YQTerrainFieldKindV2.SiteReserve))
        {
            return "The blueprint omitted a causal terrain layer.";
        }

        if (!HasHydrology(compiled.blueprint, YQHydrologyKindV2.River) ||
            !HasHydrology(compiled.blueprint, YQHydrologyKindV2.Lake) ||
            !HasHydrology(compiled.blueprint, YQHydrologyKindV2.Waterfall))
        {
            return "The blueprint omitted its downhill water system.";
        }

        if (!HasRelationship(
                compiled.blueprint,
                YQSpatialRelationshipKindV2.Behind,
                "site:natural:waterfall_cave"))
        {
            return "The waterfall/cave spatial affordance was not represented.";
        }

        // note: A concealed cave is still a traversable physical site; require its bounded trail rather than accepting relationship metadata alone.
        bool caveHasAccessRoute = false;
        for (int routeIndex = 0; routeIndex < compiled.blueprint.routes.Count; routeIndex++)
        {
            YQRouteCorridorV2 route = compiled.blueprint.routes[routeIndex];
            if (route != null &&
                (route.fromSiteId == "site:natural:waterfall_cave" ||
                 route.toSiteId == "site:natural:waterfall_cave") &&
                route.tags.Contains("concealed_site_access"))
            {
                caveHasAccessRoute = true;
                break;
            }
        }
        if (!caveHasAccessRoute)
            return "The waterfall cave has no concealed physical access route.";

        int settlementCount = CountSites(
            compiled.blueprint,
            YQSiteKindV2.Settlement);
        if (settlementCount != plan.settlements.Count ||
            compiled.blueprint.metrics.connectedSettlementCount !=
            settlementCount)
        {
            return "Semantic settlements were not preserved in the connected road graph.";
        }

        if (compiled.blueprint.routes.Count == 0 ||
            compiled.blueprint.metrics.primaryRouteLength <= 0f)
        {
            return "No traversable primary road network was compiled.";
        }

        YQSpatialBlueprintValidationResultV2 validation =
            YQSpatialBlueprintValidatorV2.Validate(compiled);
        return validation.Accepted
            ? string.Empty
            : "The accepted blueprint no longer validates: " +
              string.Join(", ", validation.Errors);
    }

    private static string TestStreamedBetaFixtureContract()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string failure))
        {
            return "Compilation failed: " + failure;
        }

        // note: Validate the authored beta itinerary at the contract boundary so a renderer cannot silently reduce it to tree-and-rock scatter.
        string[] requiredTags =
        {
            "beta_fixture_settlement",
            "beta_fixture_hostile",
            "beta_fixture_cave",
            "beta_fixture_landmark"
        };
        for (int tagIndex = 0; tagIndex < requiredTags.Length; tagIndex++)
        {
            string tag = requiredTags[tagIndex];
            bool found = false;
            for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
            {
                YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
                if (site == null || site.tags == null || !site.tags.Contains(tag))
                    continue;

                found = true;
                bool hasRoute = false;
                for (int routeIndex = 0; routeIndex < compiled.blueprint.routes.Count; routeIndex++)
                {
                    YQRouteCorridorV2 route = compiled.blueprint.routes[routeIndex];
                    if (route != null &&
                        (string.Equals(route.fromSiteId, site.siteId, StringComparison.Ordinal) ||
                         string.Equals(route.toSiteId, site.siteId, StringComparison.Ordinal)))
                    {
                        hasRoute = true;
                        break;
                    }
                }
                if (!hasRoute)
                    return "Beta fixture " + tag + " has no physical access route.";

                float halfExtent = compiled.blueprint.worldSize * 0.5f - 4f;
                if (Mathf.Abs(site.x) + site.reservedRadius > halfExtent ||
                    Mathf.Abs(site.z) + site.reservedRadius > halfExtent)
                {
                    return "Beta fixture " + tag + " exceeds the accepted world bounds.";
                }
                break;
            }

            if (!found)
                return "The accepted beta contract omitted " + tag + ".";
        }

        bool hasMultiCellFixture = false;
        for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
            if (site != null && site.tags != null && site.tags.Contains("multi_cell_site"))
            {
                hasMultiCellFixture = true;
                break;
            }
        }
        if (!hasMultiCellFixture)
            return "The beta contract omitted its representative multi-cell site.";

        // note: The accepted continuation envelope must contain real routed POI anchors so travel beyond the origin cannot exhaust into ecology-only cells while the current contract still has space for content.
        int continuationPoiCount = 0;
        for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
            if (site == null || site.tags == null || !site.tags.Contains("continuation_poi"))
                continue;

            continuationPoiCount++;
            bool hasRoute = false;
            for (int routeIndex = 0; routeIndex < compiled.blueprint.routes.Count; routeIndex++)
            {
                YQRouteCorridorV2 route = compiled.blueprint.routes[routeIndex];
                if (route != null &&
                    (string.Equals(route.fromSiteId, site.siteId, StringComparison.Ordinal) ||
                     string.Equals(route.toSiteId, site.siteId, StringComparison.Ordinal)))
                {
                    hasRoute = true;
                    break;
                }
            }
            if (!hasRoute)
                return "Continuation POI " + site.siteId + " has no accepted access route.";
        }
        // note: The repaired contract carries the first boundary pair plus three farther deterministic distance bands in eight travel directions.
        if (continuationPoiCount < 32)
            return "The accepted continuation envelope omitted its multi-band routed POI family.";

        bool hasSixKilometreBand = false;
        bool hasDiagonalBand = false;
        for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
            if (site == null || site.tags == null || !site.tags.Contains("continuation_poi"))
                continue;
            hasSixKilometreBand |= Mathf.Max(Mathf.Abs(site.x), Mathf.Abs(site.z)) >= 6100f;
            hasDiagonalBand |= Mathf.Abs(site.x) >= 4000f && Mathf.Abs(site.z) >= 4000f;
        }
        if (!hasSixKilometreBand || !hasDiagonalBand)
            return "The continuation POI contract did not cover the far or diagonal travel bands.";

        YQSiteAnchorV2 diagonalContinuation = null;
        for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
            if (site != null && site.tags != null && site.tags.Contains("continuation_poi") &&
                Mathf.Abs(site.x) >= 4000f && Mathf.Abs(site.z) >= 4000f)
            {
                diagonalContinuation = site;
                break;
            }
        }
        if (diagonalContinuation == null)
            return "The accepted continuation contract did not expose a diagonal projection witness.";
        Vector2Int diagonalCell = new Vector2Int(
            Mathf.FloorToInt((diagonalContinuation.x + 512f) / 128f),
            Mathf.FloorToInt((diagonalContinuation.z + 512f) / 128f));
        GeneratedSemanticCellPlanRecord diagonalPlanCell =
            YQGeneratedWorldQuery.GetSemanticCellPlan(plan, diagonalCell);
        // note: The exact diagonal cell must carry both the accepted POI and at least one accepted route before the streamer can admit physical content.
        if (diagonalPlanCell == null || diagonalPlanCell.siteIds == null ||
            !diagonalPlanCell.siteIds.Contains(diagonalContinuation.siteId) ||
            diagonalPlanCell.routeIds == null || diagonalPlanCell.routeIds.Count == 0)
        {
            return "The diagonal continuation POI did not project with its accepted route into the streamed cell record.";
        }

        // note: Re-project the accepted V2 site through the semantic authority and require one stable owner plus a real member footprint.
        GeneratedSemanticWorldAuthorityRecord authority =
            YQSemanticWorldAuthority.Ensure(plan);
        YQSiteAnchorV2 multiCellSite = null;
        for (int siteIndex = 0; siteIndex < compiled.blueprint.sites.Count; siteIndex++)
        {
            YQSiteAnchorV2 site = compiled.blueprint.sites[siteIndex];
            if (site != null && site.tags != null && site.tags.Contains("multi_cell_site"))
            {
                multiCellSite = site;
                break;
            }
        }
        GeneratedSemanticSiteReservationRecord reservation =
            authority.siteReservations.Find(candidate => candidate != null &&
                string.Equals(candidate.siteId, multiCellSite.siteId, StringComparison.Ordinal));
        if (reservation == null || reservation.memberCellIds == null || reservation.memberCellIds.Count < 2 ||
            string.IsNullOrWhiteSpace(reservation.ownerCellId) ||
            !reservation.memberCellIds.Contains(reservation.ownerCellId))
        {
            return "The beta multi-cell site did not project to one owner and multiple member cells.";
        }

        string[] ownerParts = reservation.ownerCellId.Split(':');
        if (ownerParts.Length != 3 || !int.TryParse(ownerParts[1], out int ownerX) ||
            !int.TryParse(ownerParts[2], out int ownerZ))
        {
            return "The beta multi-cell owner cell identity is malformed.";
        }
        // note: Verify the exact cell projection consumed by streaming, not just the detached reservation metadata.
        GeneratedSemanticCellPlanRecord ownerCell =
            YQGeneratedWorldQuery.GetSemanticCellPlan(
                plan,
                new Vector2Int(ownerX, ownerZ));
        if (ownerCell == null || ownerCell.siteIds == null ||
            !ownerCell.siteIds.Contains(reservation.siteId) ||
            ownerCell.routeIds == null || ownerCell.routeIds.Count == 0)
        {
            return "The beta owner cell lost its accepted site or route identity during projection.";
        }

        if (!HasHydrology(compiled.blueprint, YQHydrologyKindV2.River) ||
            !HasTerrain(compiled.blueprint, YQTerrainFieldKindV2.RidgeChain))
        {
            return "The beta contract omitted the streamed river or recognizable relief source.";
        }

        return string.Empty;
    }

    private static string TestInputOrderingIndependence()
    {
        GeneratedWorldPlanRecord forward = BuildSemanticPlan(false);
        GeneratedWorldPlanRecord reverse = BuildSemanticPlan(true);

        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                forward,
                out GeneratedSpatialWorldPlanV2Record forwardCompiled,
                out string forwardFailure))
        {
            return "Forward compilation failed: " + forwardFailure;
        }

        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                reverse,
                out GeneratedSpatialWorldPlanV2Record reverseCompiled,
                out string reverseFailure))
        {
            return "Reversed compilation failed: " + reverseFailure;
        }

        if (!string.Equals(
                forwardCompiled.contentHash,
                reverseCompiled.contentHash,
                StringComparison.Ordinal))
        {
            return "Semantic list enumeration order changed the persisted blueprint.";
        }

        return string.Empty;
    }

    private static string TestSettlementWaterIntent()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        // note: Cross-genre airport/drinking-water language is deliberately not waterfront intent, while the separate river market is explicit bank-facing intent.
        plan.settlements[0].siteRoleIntent =
            "airport logistics with protected drinking water storage";
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string failure))
        {
            return "Settlement water-intent compilation failed: " + failure;
        }

        YQSiteAnchorV2 inland = FindSiteBySemanticId(
            compiled.blueprint,
            plan.settlements[0].settlementId);
        YQSiteAnchorV2 waterfront = FindSiteBySemanticId(
            compiled.blueprint,
            plan.settlements[1].settlementId);
        if (inland.minimumWaterAccess != 0f)
            return "An ordinary inland settlement received an invented water-access requirement.";
        if (waterfront.minimumWaterAccess <= 0f)
            return "An explicit river settlement lost its authored water-access requirement.";

        if (!YQSpatialSiteAccessV2.TryCreateQuery(
                compiled.blueprint,
                out YQSpatialSiteAccessQueryV2 query,
                out failure))
        {
            return "Settlement dry-bank query preparation failed: " + failure;
        }

        if (!query.TryMeasure(
                inland.siteId,
                inland.x,
                inland.z,
                inland.reservedRadius,
                out _,
                out float inlandBankDistance,
                out _,
                out bool inlandCenterInsideWater,
                out failure))
        {
            return "Inland settlement dry-bank measurement failed: " + failure;
        }
        if (inlandCenterInsideWater ||
            inlandBankDistance + 0.001f < inland.reservedRadius)
        {
            return "An ordinary settlement did not keep its full construction reserve dry.";
        }

        if (!query.TryMeasure(
                waterfront.siteId,
                waterfront.x,
                waterfront.z,
                waterfront.reservedRadius,
                out float waterfrontAccess,
                out float waterfrontBankDistance,
                out _,
                out bool waterfrontCenterInsideWater,
                out failure))
        {
            return "Waterfront settlement bank measurement failed: " + failure;
        }
        if (waterfrontCenterInsideWater || waterfrontBankDistance < 1.999f)
            return "An explicit waterfront settlement centered itself in water.";
        if (waterfrontAccess + 0.001f < waterfront.minimumWaterAccess)
            return "An explicit waterfront settlement did not meet its measured bank access.";

        return string.Empty;
    }

    private static string TestValidatorRejectsBrokenCausality()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string failure))
        {
            return "Fixture compilation failed: " + failure;
        }

        YQHydrologyFeatureV2 river = FindHydrology(
            compiled.blueprint,
            YQHydrologyKindV2.River);
        river.controlPoints[1].normalizedElevation =
            Mathf.Clamp01(river.controlPoints[0].normalizedElevation + 0.2f);
        YQSpatialBlueprintValidationResultV2 validation =
            YQSpatialBlueprintValidatorV2.Validate(compiled);

        if (!ContainsFailure(
                validation.Errors,
                YQSpatialBlueprintFailureV2.UphillHydrology))
        {
            return "An uphill river passed the hydrology gate.";
        }

        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out compiled,
                out failure))
        {
            return "Fixture recompilation failed: " + failure;
        }

        compiled.blueprint.routes.Clear();
        validation = YQSpatialBlueprintValidatorV2.Validate(compiled);
        if (!ContainsFailure(
                validation.Errors,
                YQSpatialBlueprintFailureV2.DisconnectedSettlement))
        {
            return "Disconnected settlements passed the route-connectivity gate.";
        }

        return string.Empty;
    }

    private static string TestWaterRequirementAcceptance()
    {
        // note: Change only the origin's requirement in an otherwise accepted seeded blueprint, so unrelated geometry cannot cause this rejection.
        if (!TryBuildWaterAcceptanceFixture(true, out GeneratedWorldPlanRecord plan,
                out string failure))
            return failure;
        YQSpatialBlueprintValidationResultV2 validation =
            YQSpatialBlueprintValidatorV2.Validate(plan.spatialPlanV2);
        if (validation.Accepted || validation.Errors.Count != 1 ||
            !ContainsFailure(validation.Errors, YQSpatialBlueprintFailureV2.InsufficientWaterAccess))
            return "The otherwise valid blueprint did not fail only its unmet water requirement.";
        return string.Empty;
    }

    private static string TestLegacyWaterAcceptance()
    {
        // note: Legacy proof is deliberately valid for each snapshot's bytes; current validation must reject unsafe content rather than blaming a stale hash.
        for (int pass = 0; pass < 2; pass++)
        {
            bool unmetRequirement = pass == 1;
            if (!TryBuildWaterAcceptanceFixture(unmetRequirement,
                    out GeneratedWorldPlanRecord plan, out string failure))
                return failure;
            GeneratedSpatialWorldPlanV2Record snapshot = plan.spatialPlanV2;
            snapshot.validationVersion = GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion;
            snapshot.contentHash = YQSpatialBlueprintHasherV2.ComputeContentHash(snapshot);
            snapshot.validatedContentHash = snapshot.contentHash;
            string before = JsonUtility.ToJson(snapshot);
            bool accepted = YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out failure);
            if (accepted == unmetRequirement)
                return unmetRequirement ? "An accepted gate_1 snapshot bypassed the new water requirement." :
                    "A still-valid gate_1 snapshot was rejected: " + failure;
            if (unmetRequirement && failure.IndexOf("InsufficientWaterAccess", StringComparison.Ordinal) < 0)
                return "Legacy rejection did not identify the unmet water contract: " + failure;
            if (!ReferenceEquals(snapshot, plan.spatialPlanV2) ||
                !string.Equals(before, JsonUtility.ToJson(snapshot), StringComparison.Ordinal))
                return "Legacy validation rewrote the accepted artifact, flags, hashes, or version.";
        }
        return string.Empty;
    }

    private static string TestAcceptedShadowPreservation()
    {
        if (!TryBuildWaterAcceptanceFixture(true, out GeneratedWorldPlanRecord plan,
                out string failure))
            return failure;
        GeneratedSpatialWorldPlanV2Record snapshot = plan.spatialPlanV2;
        snapshot.validationVersion = GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion;
        snapshot.contentHash = YQSpatialBlueprintHasherV2.ComputeContentHash(snapshot);
        snapshot.validatedContentHash = snapshot.contentHash;
        string before = JsonUtility.ToJson(snapshot);
        // note: The public shadow entry point must keep accepted data intact even when a newer gate now diagnoses a repair need.
        if (YQSpatialBlueprintCompilerV2.TryCompileShadow(plan, out failure))
            return "Shadow compilation accepted or replaced a persisted artifact with unmet water access.";
        if (YQWorldGenerationArchitecture.RunsV2Shadow &&
            failure.IndexOf("InsufficientWaterAccess", StringComparison.Ordinal) < 0)
            return "Enabled shadow compilation did not report the accepted artifact's water failure: " + failure;
        if (!ReferenceEquals(snapshot, plan.spatialPlanV2) ||
            !string.Equals(before, JsonUtility.ToJson(snapshot), StringComparison.Ordinal))
            return "Shadow compilation mutated or regenerated accepted persisted content.";
        return string.Empty;
    }

    private static bool TryBuildWaterAcceptanceFixture(bool unmetRequirement,
        out GeneratedWorldPlanRecord plan, out string failure)
    {
        plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan,
                out GeneratedSpatialWorldPlanV2Record compiled, out failure))
        {
            failure = "Water acceptance fixture compilation failed: " + failure;
            return false;
        }
        plan.spatialPlanV2 = compiled;
        if (!YQSpatialBlueprintValidatorV2.Validate(compiled).Accepted)
        {
            failure = "The untouched water acceptance fixture was invalid.";
            return false;
        }
        if (!unmetRequirement)
            return true;
        YQSiteAnchorV2 target = null;
        float access = 1f;
        foreach (YQSiteAnchorV2 site in compiled.blueprint.sites)
        {
            if (!YQSpatialSiteAccessV2.TryMeasureWaterAccess(compiled.blueprint, site,
                    out float measured, out float distance, out _, out failure)) return false;
            if (measured >= 0.999f || distance <= 0f) continue;
            target = site;
            access = measured;
            break;
        }
        if (target == null)
        {
            failure = "The seeded blueprint no longer provides a strict, positive-distance water requirement fixture.";
            return false;
        }
        // note: Keep the chosen site's position, reserve, routes and relationships unchanged; this isolates acceptance from every placement/causality check.
        target.minimumWaterAccess = Mathf.Min(1f, access + 0.1f);
        compiled.contentHash = YQSpatialBlueprintHasherV2.ComputeContentHash(compiled);
        compiled.validatedContentHash = compiled.contentHash;
        return true;
    }

    private static string TestWaterAccessGeometry()
    {
        // note: This pure query fixture intentionally omits unrelated terrain/routes and measures a known 16 m interpolated river width at its midpoint.
        YQSpatialBlueprintV2 blueprint = new YQSpatialBlueprintV2 { worldSize = 512f };
        YQHydrologyFeatureV2 river = new YQHydrologyFeatureV2
        {
            hydrologyId = "water:test",
            kind = YQHydrologyKindV2.River,
            nominalWidth = 8f,
            nominalDepth = 2f,
            waterLevelNormalized = 0.45f,
            controlPoints = new List<YQBlueprintPointV2>
            {
                new YQBlueprintPointV2 { x = -20f, z = 0f, width = 8f, normalizedElevation = 0.5f },
                new YQBlueprintPointV2 { x = 20f, z = 0f, width = 24f, normalizedElevation = 0.4f }
            }
        };
        blueprint.hydrology.Add(river);
        YQSiteAnchorV2 site = new YQSiteAnchorV2
        {
            siteId = "site:test", x = 0f, z = 50f, reservedRadius = 40f, minimumWaterAccess = 0.5f
        };
        if (!YQSpatialSiteAccessV2.TryMeasureWaterAccess(blueprint, site,
                out float access, out float distance, out string nearest, out string failure))
            return "Finite water query failed: " + failure;
        if (Mathf.Abs(distance - 42f) > 0.001f || Mathf.Abs(access - 0.58f) > 0.001f ||
            !string.Equals(nearest, river.hydrologyId, StringComparison.Ordinal))
            return "Water access did not use the interpolated 16 m river width and 42 m bank distance.";

        // note: Narrative topology cannot grant full access to a distant site; only measured bank geometry is authoritative.
        blueprint.relationships.Add(new YQSpatialRelationshipV2
        {
            relationshipId = "relationship:test:behind",
            subjectId = site.siteId,
            objectId = river.hydrologyId,
            kind = YQSpatialRelationshipKindV2.Behind,
            minimumDistance = 0f,
            maximumDistance = 1000f,
            required = true
        });
        if (!YQSpatialSiteAccessV2.TryMeasureWaterAccess(
                blueprint, site, out float linkedAccess,
                out float linkedDistance, out _, out failure) ||
            Mathf.Abs(linkedAccess - 0.58f) > 0.001f ||
            Mathf.Abs(linkedDistance - 42f) > 0.001f)
        {
            return "A non-geometric site/water relationship bypassed measured bank access.";
        }

        if (!YQSpatialSiteAccessV2.TryCreateQuery(
                blueprint, out YQSpatialSiteAccessQueryV2 query,
                out failure) ||
            !query.TryMeasure(
                site.siteId, 0f, 0f, site.reservedRadius,
                out _, out _, out _, out bool insideWater, out failure) ||
            !insideWater)
        {
            return "The prepared bank query did not identify a site center inside the river footprint.";
        }

        // note: Invalid data must return a diagnostic rather than masquerading as zero-distance water access or throwing mid-load.
        YQBlueprintPointV2 first = river.controlPoints[0];
        river.controlPoints[0] = null;
        if (YQSpatialSiteAccessV2.TryMeasureWaterAccess(blueprint, site, out _, out _, out _, out failure) ||
            string.IsNullOrWhiteSpace(failure))
            return "A null water control point was accepted without a diagnostic.";
        river.controlPoints[0] = first;
        first.width = float.NaN;
        if (YQSpatialSiteAccessV2.TryMeasureWaterAccess(blueprint, site, out _, out _, out _, out failure) ||
            string.IsNullOrWhiteSpace(failure))
            return "A non-finite water width was accepted without a diagnostic.";
        first.width = 8f;
        blueprint.hydrology.Add(river);
        if (YQSpatialSiteAccessV2.TryMeasureWaterAccess(blueprint, site, out _, out _, out _, out failure) ||
            string.IsNullOrWhiteSpace(failure))
            return "Duplicate water feature IDs were accepted without a diagnostic.";
        blueprint.hydrology.RemoveAt(1);
        for (int index = river.controlPoints.Count;
             index <= YQSpatialSiteAccessV2.MaximumPointsPerFeature; index++)
            river.controlPoints.Add(first);
        if (YQSpatialSiteAccessV2.TryMeasureWaterAccess(blueprint, site, out _, out _, out _, out failure) ||
            string.IsNullOrWhiteSpace(failure))
            return "The bounded water query accepted a feature exceeding its point limit.";
        return string.Empty;
    }

    private static string TestIncrementalShadowCompilation()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        GeneratedSpatialWorldPlanV2Record compiled = null;
        string failure = string.Empty;
        IEnumerator routine = YQSpatialBlueprintCompilerV2.CompileRoutine(
            plan,
            (result, message) =>
            {
                compiled = result;
                failure = message ?? string.Empty;
            });

        int yieldedFrames = 0;
        while (routine.MoveNext())
        {
            yieldedFrames++;
            if (yieldedFrames > 64)
                return "Incremental compilation did not terminate.";
        }

        if (!string.IsNullOrWhiteSpace(failure) || compiled == null)
            return "Incremental compilation failed: " + failure;
        if (yieldedFrames < 10)
            return "The shadow compiler collapsed too much work into one frame.";

        plan.spatialPlanV2 = compiled;
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(
                plan,
                out string persistenceFailure))
        {
            return "The yielded artifact failed the shared persistence gate: " +
                   persistenceFailure;
        }

        return string.Empty;
    }

    private static string TestPreparedTerrainSampler()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string compileFailure))
        {
            return "Fixture compilation failed: " + compileFailure;
        }

        plan.spatialPlanV2 = compiled;
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(
                plan,
                out YQSpatialBlueprintTerrainSamplerV2 sampler,
                out string preparationFailure))
        {
            return "Sampler preparation failed: " + preparationFailure;
        }

        YQTerrainFieldV2 ridge = FindTerrain(
            compiled.blueprint,
            YQTerrainFieldKindV2.RidgeChain);
        YQTerrainFieldV2 basin = FindTerrain(
            compiled.blueprint,
            YQTerrainFieldKindV2.Basin);
        YQHydrologyFeatureV2 lake = FindHydrology(
            compiled.blueprint,
            YQHydrologyKindV2.Lake);
        YQRouteCorridorV2 route = compiled.blueprint.routes[0];
        YQSiteAnchorV2 settlement = FindSite(
            compiled.blueprint,
            YQSiteKindV2.Settlement);

        YQBlueprintPointV2 ridgePoint =
            ridge.controlPoints[ridge.controlPoints.Count - 1];
        YQBlueprintPointV2 basinPoint = basin.controlPoints[0];
        YQSpatialTerrainSampleV2 ridgeSample =
            sampler.Sample(ridgePoint.x, ridgePoint.z);
        YQSpatialTerrainSampleV2 basinSample =
            sampler.Sample(basinPoint.x, basinPoint.z);
        if (ridgeSample.elevationNormalized <=
            basinSample.elevationNormalized + 0.12f)
        {
            return "Prepared sampling lost the ridge/basin height hierarchy.";
        }

        YQBlueprintPointV2 lakePoint = lake.controlPoints[0];
        if (sampler.Sample(lakePoint.x, lakePoint.z).waterMask < 0.9f)
            return "The compiled lake has no terrain water mask.";

        YQBlueprintPointV2 routePoint = route.controlPoints[0];
        if (sampler.Sample(routePoint.x, routePoint.z).routeMask < 0.9f)
            return "The compiled road has no terrain corridor mask.";

        YQSpatialTerrainSampleV2 reserveSample =
            sampler.Sample(settlement.x, settlement.z);
        if (reserveSample.siteReserveMask < 0.9f)
            return "A settlement did not receive a buildable terrain reserve.";

        // note: Repeated calls prove the prepared sampler is deterministic and does not mutate its cached arrays during heightmap use.
        YQSpatialTerrainSampleV2 repeated =
            sampler.Sample(settlement.x, settlement.z);
        if (!Mathf.Approximately(
                reserveSample.elevationNormalized,
                repeated.elevationNormalized) ||
            !Mathf.Approximately(
                reserveSample.routeMask,
                repeated.routeMask))
        {
            return "Prepared sampling changed between identical coordinates.";
        }

        return string.Empty;
    }

    private static string TestSharedRuntimeHeightSampler()
    {
        // note: Missing or merely drafted data must not acquire a review sampler through a weaker acceptance path.
        if (YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                null, out _, out string missingFailure) ||
            string.IsNullOrWhiteSpace(missingFailure))
        {
            return "The height sampler did not reject a missing plan with a reason.";
        }

        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                plan, out _, out string uncompiledFailure) ||
            string.IsNullOrWhiteSpace(uncompiledFailure))
        {
            return "The height sampler accepted a plan without an accepted artifact.";
        }

        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan, out GeneratedSpatialWorldPlanV2Record compiled,
                out string compileFailure))
        {
            return "Fixture compilation failed: " + compileFailure;
        }
        plan.spatialPlanV2 = compiled;
        compiled.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
        if (YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                plan, out _, out string draftFailure) ||
            string.IsNullOrWhiteSpace(draftFailure))
        {
            return "The height sampler accepted a draft artifact.";
        }
        compiled.acceptanceState = GeneratedSpatialPlanAcceptanceState.Accepted;

        // note: Inspecting accepted V2 terrain is independent of which saved spatial plan the live architecture currently owns.
        YQSpatialPlanningMode configuredMode =
            YQWorldGenerationArchitecture.ActiveSpatialPlanningMode;
        if (!YQSpatialPlanVersionRouter.TryResolveActive(
                plan, out YQSpatialPlanAuthority authorityBefore,
                out string authorityFailure))
        {
            return "The accepted fixture has no active authority: " + authorityFailure;
        }
        if (!YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                plan, out YQGeneratedWorldTerrain.V2HeightSampler first,
                out string firstFailure))
        {
            return "Height sampler preparation failed: " + firstFailure;
        }
        if (!YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                plan, out YQGeneratedWorldTerrain.V2HeightSampler second,
                out string secondFailure))
        {
            return "Repeated height sampler preparation failed: " + secondFailure;
        }

        // note: Twenty-five native lattice samples include terrain edges and interior positions without synthesizing a full heightmap.
        int last = YQGeneratedWorldTerrain.HeightmapResolution - 1;
        int[] gridIndices = { 0, last / 5, last / 2, last * 4 / 5, last };
        for (int z = 0; z < gridIndices.Length; z++)
        {
            float worldZ = gridIndices[z] / (float)last *
                YQGeneratedWorldTerrain.WorldSize -
                YQGeneratedWorldTerrain.WorldSize * 0.5f;
            for (int x = 0; x < gridIndices.Length; x++)
            {
                float worldX = gridIndices[x] / (float)last *
                    YQGeneratedWorldTerrain.WorldSize -
                    YQGeneratedWorldTerrain.WorldSize * 0.5f;
                float sampled = first.SampleNormalized(worldX, worldZ);
                if (float.IsNaN(sampled) || float.IsInfinity(sampled) ||
                    sampled < 0.025f || sampled > 0.88f)
                {
                    return "A native lattice sample escaped the finite terrain height range.";
                }
                if (sampled != second.SampleNormalized(worldX, worldZ) ||
                    sampled != first.SampleNormalized(worldX, worldZ))
                {
                    return "Identical accepted data or repeated sampling changed a terrain height.";
                }
            }
        }

        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(
                plan, out YQSpatialBlueprintTerrainSamplerV2 prepared,
                out string preparationFailure))
        {
            return "Protected-site reference preparation failed: " + preparationFailure;
        }
        YQSiteAnchorV2 settlement = FindSite(
            compiled.blueprint, YQSiteKindV2.Settlement);
        YQSpatialTerrainSampleV2 protectedSite =
            prepared.Sample(settlement.x, settlement.z);
        // note: Full site protection removes all micro-noise, so the shared height must exactly preserve the already-clamped accepted surface.
        if (protectedSite.siteReserveMask != 1f ||
            first.SampleNormalized(settlement.x, settlement.z) !=
            protectedSite.elevationNormalized)
        {
            return "A fully protected site acquired terrain micro-detail or changed height.";
        }
        if (YQWorldGenerationArchitecture.ActiveSpatialPlanningMode != configuredMode ||
            !YQSpatialPlanVersionRouter.TryResolveActive(
                plan, out YQSpatialPlanAuthority authorityAfter, out _) ||
            authorityAfter != authorityBefore)
        {
            return "Offline height review changed the configured runtime spatial authority.";
        }
        return string.Empty;
    }

    private static string TestDetachedCandidatePreparation()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        var previous = new GeneratedSpatialWorldPlanV2Record();
        plan.spatialPlanV2 = previous;
        string before = JsonUtility.ToJson(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var candidate, out string failure))
            return failure;

        // note: Both success and rejection must preserve the live artifact reference and serialized source, so an autosave cannot capture transient acceptance.
        if (!YQSpatialMaterializationCompilerV2.TryPrepareCandidate(
                plan, candidate, out _, out failure))
            return "Valid detached projection rejected: " + failure;
        if (!ReferenceEquals(plan.spatialPlanV2, previous) || JsonUtility.ToJson(plan) != before)
            return "Successful detached preparation mutated the live source.";
        candidate.validatedContentHash = "invalid";
        if (YQSpatialMaterializationCompilerV2.TryPrepareCandidate(
                plan, candidate, out _, out _))
            return "Invalid detached candidate passed preparation.";
        if (!ReferenceEquals(plan.spatialPlanV2, previous) || JsonUtility.ToJson(plan) != before)
            return "Rejected detached preparation mutated the live source.";
        return string.Empty;
    }

    private static string TestRuntimeDestinationProjection()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        // note: Build the complete persisted V1 metadata first so the query does not need to normalize an incomplete synthetic save.
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var candidate, out string failure))
            return failure;
        plan.spatialPlanV2 = candidate;
        YQSiteAnchorV2 site = FindSite(candidate.blueprint, YQSiteKindV2.Settlement);
        if (!YQGeneratedWorldSpatialPlanner.TryGetLocation(
                plan, site.sourceSemanticId, out GeneratedSpatialLocationRecord original))
            return "Fixture settlement metadata is missing.";
        float acceptedSiteX = site.x;
        float acceptedSiteZ = site.z;
        float staleV1X = acceptedSiteX + 1000f;
        float staleV1Z = acceptedSiteZ + 1000f;
        original.worldX = staleV1X;
        original.worldZ = staleV1Z;
        // note: An obsolete V1-only destination at the query point must not beat the real V2 settlement or leak back into gameplay.
        plan.spatialPlan.locations.Add(new GeneratedSpatialLocationRecord
        {
            locationId = "obsolete_v1_site",
            locationKind = "settlement",
            worldX = site.x,
            worldZ = site.z
        });
        GeneratedSpatialLocationRecord result = YQGeneratedWorldQuery.GetNearbySettlement(
            plan, new Vector3(acceptedSiteX, 0f, acceptedSiteZ), 1f);
        if (result == null || result.locationId != site.sourceSemanticId ||
            result.worldX != acceptedSiteX || result.worldZ != acceptedSiteZ)
            return "Gameplay selected stale V1 coordinates instead of the accepted V2 site.";
        if (ReferenceEquals(result, original) ||
            original.worldX != staleV1X || original.worldZ != staleV1Z)
            return "Gameplay projection mutated the saved V1 record.";
        if (site.x != acceptedSiteX || site.z != acceptedSiteZ)
            return "Gameplay projection mutated the accepted V2 site.";
        return string.Empty;
    }

    private static string TestMaterializationProjection()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string compileFailure))
        {
            return "Fixture compilation failed: " + compileFailure;
        }

        plan.spatialPlanV2 = compiled;
        if (!YQSpatialMaterializationCompilerV2.TryPrepare(
                plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out string preparationFailure))
        {
            return "Materialization projection failed: " +
                   preparationFailure;
        }

        if (prepared.RegionCount != compiled.blueprint.regions.Count ||
            prepared.SiteCount != compiled.blueprint.sites.Count ||
            prepared.RouteCount != compiled.blueprint.routes.Count ||
            prepared.CrossingCount != compiled.blueprint.metrics.crossingCount)
        {
            return "The projection dropped accepted spatial records.";
        }

        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[index];
            if (!prepared.TryGetSiteBySemanticId(
                    settlement.settlementId,
                    out YQSpatialMaterializationSiteV2 site))
            {
                return "A semantic settlement has no materialization site.";
            }
            // note: Every accepted functional requirement must survive projection; geometry-only checks must not silently discard habitation or service obligations.
            YQSiteAnchorV2 sourceSite = compiled.blueprint.sites.Find(item => item != null && item.siteId == site.siteId);
            if (sourceSite == null || sourceSite.requiredFunctions.Count != site.RequiredFunctions.Count)
                return "Site projection dropped required functions.";
            for (int functionIndex = 0; functionIndex < sourceSite.requiredFunctions.Count; functionIndex++)
                if (sourceSite.requiredFunctions[functionIndex] != site.RequiredFunctions[functionIndex])
                    return "Site projection changed a required function.";
            if (!site.terrainReserveReady ||
                string.IsNullOrWhiteSpace(site.frontageRouteId) ||
                site.routeAccess < 0.85f)
            {
                return "A settlement lost its terrain reserve or road frontage.";
            }
            if (!prepared.TryValidateFootprint(
                    settlement.settlementId,
                    site.reservedRadius,
                    out string fitFailure))
            {
                return "A reserve rejected its exact accepted radius: " +
                       fitFailure;
            }
            if (prepared.TryValidateFootprint(
                    settlement.settlementId,
                    site.reservedRadius + 0.1f,
                    out _))
            {
                return "An oversized asset was allowed to mutate a V2 reserve.";
            }
        }

        for (int routeIndex = 0;
             routeIndex < prepared.RouteCount;
             routeIndex++)
        {
            if (prepared.GetRoutePointCount(routeIndex) < 2)
                return "A materialized route lost its corridor geometry.";
            YQSpatialMaterializationRoutePointV2 point =
                prepared.GetRoutePoint(routeIndex, 0);
            if (float.IsNaN(point.surfaceElevationNormalized) ||
                float.IsInfinity(point.surfaceElevationNormalized))
            {
                return "A route point has no finite prepared terrain height.";
            }
        }

        if (!prepared.TryGetSiteBySemanticId(
                "generated:waterfall_cave",
                out YQSpatialMaterializationSiteV2 cave) ||
            !cave.concealed ||
            !cave.behindFeature ||
            !cave.concealedAccess ||
            !cave.requiresTransition ||
            !cave.requiresEncounter ||
            !cave.requiresReward ||
            // note: Authored Behind/Concealed flags remain semantic, while the cave's strong water access must come from its measured bank position.
            cave.waterAccess + 0.001f < 0.9f)
        {
            return "The waterfall cave lost its authored spatial relationship.";
        }

        if (!prepared.TryGetSiteBySemanticId(
                "poi:hidden_cache",
                out YQSpatialMaterializationSiteV2 hiddenReward) ||
            hiddenReward.kind != YQSiteKindV2.PointOfInterest ||
            !hiddenReward.concealed ||
            !hiddenReward.requiresReward)
        {
            return "A semantic hidden-reward POI lost its placement function.";
        }

        // note: Runtime cave and reward builders consume explicit projected functions, never narrative POI descriptions or display names.

        return string.Empty;
    }

    private static string TestMaterializationCacheIntegrity()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string compileFailure))
        {
            return "Fixture compilation failed: " + compileFailure;
        }

        plan.spatialPlanV2 = compiled;
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 originalPrepared,
                out string preparationFailure))
        {
            return "Initial cached materialization failed: " +
                   preparationFailure;
        }

        YQSiteAnchorV2 sourceSite = compiled.blueprint.sites[0];
        if (sourceSite == null ||
            !originalPrepared.TryGetSiteBySiteId(sourceSite.siteId,
                out YQSpatialMaterializationSiteV2 originalSite))
        {
            return "The cache fixture has no projected source site.";
        }

        float originalX = sourceSite.x;
        // note: Mutate accepted content without updating either persisted hash; the cache must detect actual blueprint drift rather than trust the stale claim.
        sourceSite.x = originalX + 0.125f;
        bool reusedStaleProjection =
            YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 stalePrepared,
                out string driftFailure);
        sourceSite.x = originalX;

        if (reusedStaleProjection || stalePrepared != null)
            return "A mutated blueprint reused stale prepared authority.";
        if (string.IsNullOrWhiteSpace(driftFailure))
            return "Blueprint drift was rejected without an integrity diagnostic.";

        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 restoredPrepared,
                out string restoredFailure))
        {
            return "Restoring the accepted bytes did not restore the valid cache: " +
                   restoredFailure;
        }

        if (!ReferenceEquals(originalPrepared, restoredPrepared) ||
            !restoredPrepared.TryGetSiteBySiteId(sourceSite.siteId,
                out YQSpatialMaterializationSiteV2 restoredSite) ||
            Mathf.Abs(restoredSite.x - originalSite.x) > 0.0001f)
        {
            return "The rejected mutation poisoned the original valid cache entry.";
        }

        return string.Empty;
    }

    private static string TestPreparedEnvironmentProjection()
    {
        GeneratedWorldPlanRecord plan = BuildSemanticPlan(false);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record compiled,
                out string compileFailure))
        {
            return "Fixture compilation failed: " + compileFailure;
        }

        plan.spatialPlanV2 = compiled;
        if (!YQSpatialMaterializationCompilerV2.TryPrepare(
                plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out string preparationFailure))
        {
            return "Environment projection failed: " +
                   preparationFailure;
        }

        if (prepared.WaterCount != compiled.blueprint.hydrology.Count)
            return "The prepared projection dropped accepted hydrology.";

        for (int waterIndex = 0;
             waterIndex < prepared.WaterCount;
             waterIndex++)
        {
            YQHydrologyFeatureV2 source =
                compiled.blueprint.hydrology[waterIndex];
            int expectedPoints = 0;
            for (int pointIndex = 0;
                 source != null && source.controlPoints != null &&
                 pointIndex < source.controlPoints.Count;
                 pointIndex++)
            {
                if (source.controlPoints[pointIndex] != null)
                    expectedPoints++;
            }

            if (prepared.GetWaterPointCount(waterIndex) != expectedPoints)
                return "Prepared hydrology lost control-point geometry.";

            if (expectedPoints >= 2 &&
                (source.kind == YQHydrologyKindV2.River ||
                 source.kind == YQHydrologyKindV2.Waterfall))
            {
                float previous =
                    prepared.GetWaterPoint(waterIndex, 0)
                        .waterSurfaceNormalized;
                for (int pointIndex = 1;
                     pointIndex < expectedPoints;
                     pointIndex++)
                {
                    float current =
                        prepared.GetWaterPoint(waterIndex, pointIndex)
                            .waterSurfaceNormalized;
                    if (current > previous + 0.0001f)
                        return "Prepared flowing water travels uphill.";
                    previous = current;
                }
            }
        }

        YQHydrologyFeatureV2 lake = FindHydrology(
            compiled.blueprint,
            YQHydrologyKindV2.Lake);
        YQBlueprintPointV2 lakePoint = lake.controlPoints[0];
        YQSpatialEcologySampleV2 shoreline =
            prepared.SampleEcologyContext(lakePoint.x, lakePoint.z);
        if (shoreline.terrain.waterMask < 0.9f ||
            shoreline.tileProfile.DrainageAffinity < 0.75f)
        {
            return "Ecology does not recognize its accepted lake.";
        }

        YQSiteAnchorV2 settlement = FindSite(
            compiled.blueprint,
            YQSiteKindV2.Settlement);
        YQSpatialEcologySampleV2 construction =
            prepared.SampleEcologyContext(settlement.x, settlement.z);
        if (construction.terrain.siteReserveMask < 0.9f ||
            construction.civilizationDensity < 0f ||
            construction.civilizationDensity > 1f ||
            construction.tileProfile.ForestDensity < 0f ||
            construction.tileProfile.ForestDensity > 1f)
        {
            return "Ecology lost its accepted construction or province masks.";
        }

        if (!prepared.TryGetSiteBySemanticId(
                "generated:waterfall_cave",
                out YQSpatialMaterializationSiteV2 caveSite) ||
            prepared.SampleTerrain(caveSite.x, caveSite.z)
                .caveMassMask < 0.9f)
        {
            return "A concealed cave entrance has no physical exclusion mask.";
        }

        // note: Environment materializers receive one deterministic sample carrying province ecology and exact physical masks.
        YQSpatialEcologySampleV2 repeated =
            prepared.SampleEcologyContext(settlement.x, settlement.z);
        if (!Mathf.Approximately(
                construction.terrain.routeMask,
                repeated.terrain.routeMask) ||
            !Mathf.Approximately(
                construction.tileProfile.Moisture,
                repeated.tileProfile.Moisture))
        {
            return "Prepared ecology changed between identical coordinates.";
        }

        return string.Empty;
    }

    private static GeneratedWorldPlanRecord BuildSemanticPlan(bool reverse)
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
        {
            worldSeed = "worldgen_v2_blueprint_seed"
        };
        plan.EnsureCollections();
        plan.spatialPlan.worldSeed = plan.worldSeed;
        plan.spatialPlan.worldSize = 1024f;
        plan.spatialPlan.semanticFingerprint =
            "worldgen_v2_blueprint_semantics";

        plan.regions.Add(new GeneratedRegionRecord
        {
            regionId = "region:north",
            displayName = "North",
            gridX = 0,
            gridY = 1,
            dangerTier = 4,
            terrainProfile = "rugged ridge and forest",
            climateProfile = "wet temperate",
            biomeTags = new List<string> { "forest", "upland" }
        });
        plan.regions.Add(new GeneratedRegionRecord
        {
            regionId = "region:south",
            displayName = "South",
            gridX = 0,
            gridY = -1,
            dangerTier = 2,
            terrainProfile = "rolling plain with rocky outcrops",
            climateProfile = "temperate",
            biomeTags = new List<string> { "grassland", "woodland" }
        });

        plan.settlements.Add(new GeneratedSettlementRecord
        {
            settlementId = "settlement:north_crossing",
            regionId = "region:north",
            displayName = "North Crossing",
            kind = "regional town",
            approxPopulation = 260,
            serviceSlots = new List<string>
            {
                "food",
                "repair",
                "lodging"
            }
        });
        plan.settlements.Add(new GeneratedSettlementRecord
        {
            settlementId = "settlement:south_haven",
            regionId = "region:south",
            displayName = "South Haven",
            kind = "river market",
            approxPopulation = 140,
            serviceSlots = new List<string> { "trade", "rest" }
        });

        plan.encampments.Add(new GeneratedEncampmentRecord
        {
            encampmentId = "hostile:ridge_watch",
            regionId = "region:north",
            displayName = "Ridge Watch",
            kind = "occupied lookout",
            threatTier = 3,
            stealthApproach = "rear slope"
        });

        plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord
        {
            poiId = "poi:river_marker",
            regionId = "region:south",
            displayName = "River Marker",
            kind = "water landmark",
            gameplayHook = "discover a crossing",
            tags = new List<string> { "water", "navigation" }
        });
        plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord
        {
            poiId = "poi:hidden_cache",
            regionId = "region:north",
            displayName = "Hidden Cache",
            kind = "concealed location",
            gameplayHook = "reward exploration",
            tags = new List<string> { "hidden", "reward" }
        });

        plan.routes.Add(new GeneratedWorldRouteRecord
        {
            routeId = "semantic_route:north_south",
            fromRegionId = "region:north",
            toRegionId = "region:south",
            routeKind = "regional road"
        });

        if (reverse)
        {
            plan.regions.Reverse();
            plan.settlements.Reverse();
            plan.encampments.Reverse();
            plan.pointsOfInterest.Reverse();
            plan.routes.Reverse();
        }

        return plan;
    }

    private static bool HasTerrain(
        YQSpatialBlueprintV2 blueprint,
        YQTerrainFieldKindV2 kind)
    {
        for (int index = 0; index < blueprint.terrainFields.Count; index++)
        {
            if (blueprint.terrainFields[index] != null &&
                blueprint.terrainFields[index].kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static YQTerrainFieldV2 FindTerrain(
        YQSpatialBlueprintV2 blueprint,
        YQTerrainFieldKindV2 kind)
    {
        for (int index = 0; index < blueprint.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 field = blueprint.terrainFields[index];
            if (field != null && field.kind == kind)
                return field;
        }

        throw new InvalidOperationException(
            "Missing terrain fixture " + kind + ".");
    }

    private static YQSiteAnchorV2 FindSite(
        YQSpatialBlueprintV2 blueprint,
        YQSiteKindV2 kind)
    {
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            if (site != null && site.kind == kind)
                return site;
        }

        throw new InvalidOperationException(
            "Missing site fixture " + kind + ".");
    }

    private static YQSiteAnchorV2 FindSiteBySemanticId(
        YQSpatialBlueprintV2 blueprint,
        string semanticId)
    {
        // note: Settlement intent tests resolve the compiled site through its persisted semantic identity rather than relying on list order.
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            if (site != null && string.Equals(
                    site.sourceSemanticId,
                    semanticId,
                    StringComparison.Ordinal))
            {
                return site;
            }
        }

        throw new InvalidOperationException(
            "Missing semantic site fixture " + semanticId + ".");
    }

    private static bool HasHydrology(
        YQSpatialBlueprintV2 blueprint,
        YQHydrologyKindV2 kind)
    {
        return FindHydrology(blueprint, kind) != null;
    }

    private static YQHydrologyFeatureV2 FindHydrology(
        YQSpatialBlueprintV2 blueprint,
        YQHydrologyKindV2 kind)
    {
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[index];
            if (feature != null && feature.kind == kind)
                return feature;
        }

        return null;
    }

    private static bool HasRelationship(
        YQSpatialBlueprintV2 blueprint,
        YQSpatialRelationshipKindV2 kind,
        string subjectId)
    {
        for (int index = 0;
             index < blueprint.relationships.Count;
             index++)
        {
            YQSpatialRelationshipV2 relationship =
                blueprint.relationships[index];
            if (relationship != null &&
                relationship.kind == kind &&
                string.Equals(
                    relationship.subjectId,
                    subjectId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSites(
        YQSpatialBlueprintV2 blueprint,
        YQSiteKindV2 kind)
    {
        int count = 0;
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            if (blueprint.sites[index] != null &&
                blueprint.sites[index].kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private static string TestStrictSemanticSettlementComposition()
    {
        YQReviewedSemanticSiteManifest manifest =
            BuildSemanticCompositionFixture(out List<GameObject> prefabs);
        try
        {
            string[] requiredTags =
            {
                "poi",
                "civic",
                "residential",
                "service",
                "circulation"
            };
            if (!YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(
                    manifest,
                    requiredTags,
                    "settlement-alpha",
                    out YQSemanticSiteCompositionV2 first,
                    out string failure))
            {
                return "Valid settlement composition was rejected: " + failure;
            }
            if (!YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(
                    manifest,
                    requiredTags,
                    "settlement-alpha",
                    out YQSemanticSiteCompositionV2 repeat,
                    out failure))
            {
                return "Repeated deterministic composition failed: " + failure;
            }
            if (!string.Equals(
                    first.compositionSignature,
                    repeat.compositionSignature,
                    StringComparison.Ordinal) ||
                first.SelectedCount == 0 ||
                first.sourceInstanceCount <= 0)
            {
                return "Semantic composition is not stable or measurable.";
            }

            HashSet<string> signatures = new HashSet<string>(
                StringComparer.Ordinal);
            for (int index = 0; index < 32; index++)
            {
                if (YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(
                        manifest,
                        requiredTags,
                        "settlement-variant-" + index,
                        out YQSemanticSiteCompositionV2 variant,
                        out _))
                {
                    signatures.Add(variant.compositionSignature);
                }
            }

            return signatures.Count >= 2
                ? string.Empty
                : "Different settlement seeds collapsed into one composition signature.";
        }
        finally
        {
            DestroySemanticCompositionFixture(manifest, prefabs);
        }
    }

    private static string TestReviewedDoorBinding()
    {
        GameObject cell = CreateDoorFixture(out Transform leaf, out YQCellDoorBindingV2 binding);
        try
        {
            Vector3 authoredSize = leaf.GetComponent<BoxCollider>().size;
            if (!YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town-a", "cell-a", "Fixture door", "region-a", out string failure))
                return "Reviewed door binding failed: " + failure;
            if (!YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town-a", "cell-a", "Fixture door", "region-a", out failure))
                return "Repeated door setup was not idempotent: " + failure;
            YQLockpickableDoor[] doors = leaf.GetComponents<YQLockpickableDoor>();
            if (doors.Length != 1 || doors[0].GeneratedDoorId != YQCellDoorBindingsV2.BuildDoorId("town-a", "cell-a", binding.bindingId))
                return "Generated door acquired duplicate/wrong ownership.";
            if (leaf.GetComponent<BoxCollider>().size != authoredSize)
                return "Generated door binding replaced the authored collider dimensions.";
            // note: This edit-mode fixture verifies binding only; Play-mode hinge motion and Awake restoration still require runtime validation.
            return string.Empty;
        }
        finally { UnityEngine.Object.DestroyImmediate(cell); }
    }

    private static string TestDoorBindingRejections()
    {
        GameObject cell = CreateDoorFixture(out Transform leaf, out YQCellDoorBindingV2 binding);
        try
        {
            binding.reviewState = YQSemanticSiteReviewState.Pending;
            if (YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town", "cell", "Fixture", "region", out _))
                return "An unreviewed door acquired gameplay behaviour.";
            binding.reviewState = YQSemanticSiteReviewState.Approved;
            binding.passageVerified = false;
            if (YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town", "cell", "Fixture", "region", out _))
                return "A door with no verified passage was accepted.";
            binding.passageVerified = true;
            leaf.GetComponent<BoxCollider>().size += Vector3.one;
            if (YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town", "cell", "Fixture", "region", out _))
                return "Changed door geometry bypassed review.";
            leaf.GetComponent<BoxCollider>().size = binding.colliderSize;
            GameObject duplicate = new GameObject(leaf.name);
            duplicate.transform.SetParent(cell.transform, false);
            if (YQCellDoorBindingsV2.TryBind(cell.transform, binding, "town", "cell", "Fixture", "region", out _))
                return "An ambiguous door path selected an arbitrary sibling.";
            return cell.GetComponentsInChildren<YQLockpickableDoor>(true).Length == 0
                ? string.Empty : "Rejected bindings still modified the hierarchy.";
        }
        finally { UnityEngine.Object.DestroyImmediate(cell); }
    }

    private static string TestGeneratedDoorPersistence()
    {
        string first = YQCellDoorBindingsV2.BuildDoorId("town-a", "cell-a", "door-a");
        string other = YQCellDoorBindingsV2.BuildDoorId("town-b", "cell-a", "door-a");
        if (first == other ||
            YQCellDoorBindingsV2.BuildDoorId("a:b", "c", "d") == YQCellDoorBindingsV2.BuildDoorId("a", "b:c", "d"))
            return "Generated door identity is ambiguous across instances.";
        WorldState world = WorldState.CreateDefault();
        YQCellDoorBindingsV2.RecordOpened(world, first);
        // note: Round-trip only synthetic data; the test never calls WorldStateManager.Save or writes a profile.
        WorldState restored = WorldState.CreateDefault();
        restored.globalFlags = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float>>(
            Newtonsoft.Json.JsonConvert.SerializeObject(world.globalFlags));
        return YQCellDoorBindingsV2.WasOpened(restored, first) && !YQCellDoorBindingsV2.WasOpened(restored, other)
            ? string.Empty : "Door-open state did not survive its save representation independently.";
    }

    private static GameObject CreateDoorFixture(out Transform leaf, out YQCellDoorBindingV2 binding)
    {
        GameObject cell = new GameObject("V2_DoorCellFixture");
        cell.SetActive(false);
        GameObject door = new GameObject("DoorLeaf");
        door.transform.SetParent(cell.transform, false);
        door.AddComponent<MeshRenderer>();
        BoxCollider box = door.AddComponent<BoxCollider>();
        box.size = new Vector3(0.061f, 2.764f, 1.206f);
        box.center = new Vector3(0f, 1.382f, -0.603f);
        leaf = door.transform;
        binding = new YQCellDoorBindingV2
        {
            bindingId = "reviewed-door",
            targetPath = "DoorLeaf",
            sourcePrefabGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            reviewState = YQSemanticSiteReviewState.Approved,
            passageVerified = true,
            sourceLocalPosition = leaf.localPosition,
            sourceLocalRotation = leaf.localRotation,
            sourceLocalScale = leaf.localScale,
            colliderCenter = box.center,
            colliderSize = box.size,
            openEuler = new Vector3(0f, 86f, 0f)
        };
        return cell;
    }

    private static string TestFunctionCandidatePopulation()
    {
        YQReviewedSemanticSiteManifest manifest = BuildSemanticCompositionFixture(out List<GameObject> prefabs);
        try
        {
            if (YQSiteFunctionContractsV2.PopulateCandidates(manifest) != 4 ||
                YQSiteFunctionContractsV2.PopulateCandidates(manifest) != 0)
                return "Candidate population was not idempotent.";
            foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
                foreach (YQReviewedCellFunctionContractV2 candidate in zone.cellContractsV2)
                    if (candidate.reviewState != YQSemanticSiteReviewState.Pending || candidate.curation.contractVersion != 0)
                        return "Inferred candidate evidence was promoted without review.";
            bool accepted = YQSiteFunctionContractsV2.TryValidate(manifest,
                new[] { manifest.Zones[0].stableId }, new[] { YQAssetFunctionV2.Circulation },
                YQWorldStructureUsagePolicy.FullyEnterable, out _);
            return accepted ? "District labels/candidate contracts bypassed functional review." : string.Empty;
        }
        finally { DestroySemanticCompositionFixture(manifest, prefabs); }
    }

    private static string TestSelectedCellFunctions()
    {
        YQReviewedSemanticSiteManifest manifest = BuildSemanticCompositionFixture(out List<GameObject> prefabs);
        try
        {
            YQReviewedCellFunctionContractV2 binding = AddReviewedFunctionFixture(manifest.Zones[1], YQAssetFunctionV2.Service);
            if (YQSiteFunctionContractsV2.TryValidate(manifest,
                    new[] { manifest.Zones[0].stableId }, new[] { YQAssetFunctionV2.Service },
                    YQWorldStructureUsagePolicy.FullyEnterable, out _))
                return "An unselected cell supplied a missing service.";
            if (!YQSiteFunctionContractsV2.TryValidate(manifest,
                    new[] { binding.cellId }, new[] { YQAssetFunctionV2.Service },
                    YQWorldStructureUsagePolicy.ExteriorShellsOnly, out string failure))
                return "A reviewed outdoor service was rejected: " + failure;
            binding.curation.primaryFunction = YQAssetFunctionV2.Habitation;
            binding.curation.affordances.Add(YQAssetAffordanceV2.Entrance);
            binding.curation.affordances.Add(YQAssetAffordanceV2.Sleeping);
            binding.curation.sockets.Add(FunctionSocketFixture(YQAssetSocketKindV2.Entrance));
            binding.curation.sockets.Add(FunctionSocketFixture(YQAssetSocketKindV2.Furnishing));
            if (YQSiteFunctionContractsV2.TryValidate(manifest,
                    new[] { binding.cellId }, new[] { YQAssetFunctionV2.Habitation },
                    YQWorldStructureUsagePolicy.ExteriorShellsOnly, out _))
                return "Exterior-only shells were accepted as usable homes.";
            if (!YQSiteFunctionContractsV2.TryValidate(manifest,
                    new[] { binding.cellId }, new[] { YQAssetFunctionV2.Habitation },
                    YQWorldStructureUsagePolicy.FullyEnterable, out failure))
                return "A reviewed furnished entrance contract was rejected: " + failure;
            YQSiteFunctionContractsV2.PopulateCandidates(manifest);
            return binding.reviewState == YQSemanticSiteReviewState.Approved && binding.curation.contractVersion == 2
                ? string.Empty : "Candidate refresh replaced an existing reviewed binding.";
        }
        finally { DestroySemanticCompositionFixture(manifest, prefabs); }
    }

    private static string TestInvalidFunctionEvidence()
    {
        YQReviewedSemanticSiteManifest manifest = BuildSemanticCompositionFixture(out List<GameObject> prefabs);
        try
        {
            YQReviewedCellFunctionContractV2 binding = AddReviewedFunctionFixture(manifest.Zones[0], YQAssetFunctionV2.Service);
            binding.sourceSignature = "different-source-revision";
            if (YQSiteFunctionContractsV2.TryValidate(manifest, new[] { binding.cellId },
                    new[] { YQAssetFunctionV2.Service }, YQWorldStructureUsagePolicy.FullyEnterable, out _))
                return "Stale source evidence was accepted.";
            binding.sourceSignature = manifest.SourceSignature;
            binding.curation.sockets[0].clearanceSize = Vector3.zero;
            if (YQSiteFunctionContractsV2.TryValidate(manifest, new[] { binding.cellId },
                    new[] { YQAssetFunctionV2.Service }, YQWorldStructureUsagePolicy.FullyEnterable, out _))
                return "An unmeasured interaction clearance was accepted.";
            binding.curation.sockets[0].clearanceSize = Vector3.one;
            return YQSiteFunctionContractsV2.TryValidate(manifest, new[] { binding.cellId },
                new[] { YQAssetFunctionV2.None }, YQWorldStructureUsagePolicy.FullyEnterable, out _)
                ? "Empty required function passed the acceptance gate." : string.Empty;
        }
        finally { DestroySemanticCompositionFixture(manifest, prefabs); }
    }

    private static string TestPartialDistrictFunctionCoverage()
    {
        GameObject prefab = new GameObject("V2_PartialDistrictFixture");
        YQAuthoredSiteStreamingManifest streaming = ScriptableObject.CreateInstance<YQAuthoredSiteStreamingManifest>();
        YQReviewedSemanticSiteManifest manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        try
        {
            YQAuthoredSiteStreamingCellRecord small = new YQAuthoredSiteStreamingCellRecord();
            YQAuthoredSiteStreamingCellRecord expensive = new YQAuthoredSiteStreamingCellRecord();
            small.Configure("road-cell", prefab, Vector3.zero, Vector3.zero, Vector3.one * 10f, 10);
            expensive.Configure("service-cell", prefab, Vector3.right * 20f, Vector3.zero, Vector3.one * 10f, 2000);
            streaming.ConfigureCandidate("fixture", YQAuthoredSiteKind.Settlement, "", "fixture-source", prefab, new[] { small, expensive });
            streaming.MarkReleaseEligible();
            YQReviewedSemanticZoneRecord zone = new YQReviewedSemanticZoneRecord
            {
                stableId = "district",
                semanticTags = new List<string> { "service", "circulation" },
                streamingCellIds = new List<string> { small.StableCellId, expensive.StableCellId }
            };
            manifest.ConfigureCandidate("fixture", "fixture", "fixture-source", YQSemanticExtractionTopology.Unknown,
                streaming, 2010, new[] { zone });
            manifest.MarkReleaseEligible();
            if (YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(manifest, new[] { "service" }, "fixture",
                    out _, out _))
                return "A road subcell inherited the unavailable district service tag.";
            YQReviewedCellFunctionContractV2 binding = AddReviewedFunctionFixture(zone, YQAssetFunctionV2.Circulation);
            binding.cellId = small.StableCellId;
            binding.sourceSignature = manifest.SourceSignature;
            binding.curation.affordances.Add(YQAssetAffordanceV2.TraversableSurface);
            binding.curation.sockets.Add(FunctionSocketFixture(YQAssetSocketKindV2.Connection));
            bool accepted = YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(manifest, new[] { "circulation" }, "fixture",
                out _, out string failure, new[] { YQAssetFunctionV2.Circulation }, YQWorldStructureUsagePolicy.ExteriorShellsOnly);
            return accepted ? string.Empty : "Exact-cell function proof incorrectly required its whole district: " + failure;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(manifest);
            UnityEngine.Object.DestroyImmediate(streaming);
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    private static string TestFunctionDrivenCellSelection(bool useStreaming)
    {
        // note: A misleading high-score district can consume the entire budget; only the other cell has usable reviewed service evidence.
        List<GameObject> prefabs = new List<GameObject>();
        YQReviewedSemanticZoneRecord decoy = BuildSemanticZoneFixture(
            "tagged_without_provider", 0f, new[] { "civic" }, prefabs);
        YQReviewedSemanticZoneRecord provider = BuildSemanticZoneFixture(
            "actual_provider", 24f, new[] { "workshop" }, prefabs);
        decoy.sourceInstanceCount = provider.sourceInstanceCount = 1100;
        YQReviewedSemanticSiteManifest manifest = ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        YQAuthoredSiteStreamingManifest streaming = null;
        try
        {
            var zones = new[] { decoy, provider };
            if (useStreaming)
            {
                streaming = ScriptableObject.CreateInstance<YQAuthoredSiteStreamingManifest>();
                var cells = new List<YQAuthoredSiteStreamingCellRecord>();
                foreach (YQReviewedSemanticZoneRecord zone in zones)
                {
                    var cell = new YQAuthoredSiteStreamingCellRecord();
                    cell.Configure(zone.stableId, zone.prefab, zone.authoredSourceOrigin,
                        zone.localBoundsCenter, zone.localBoundsSize, zone.sourceInstanceCount);
                    cells.Add(cell);
                    zone.streamingCellIds = new List<string> { zone.stableId };
                }
                streaming.ConfigureCandidate("fixture", YQAuthoredSiteKind.Settlement,
                    "", "fixture-source", prefabs[0], cells);
                streaming.MarkReleaseEligible();
                manifest.ConfigureCandidate("fixture", "fixture", "fixture-source",
                    YQSemanticExtractionTopology.Unknown, streaming, 2200, zones);
                manifest.MarkReleaseEligible();
            }
            else
                manifest.Configure("fixture", "fixture", 2200, zones, true);

            var invalid = AddReviewedFunctionFixture(decoy, YQAssetFunctionV2.Service);
            invalid.sourceSignature = manifest.SourceSignature;
            invalid.curation.sockets.Clear();
            var valid = AddReviewedFunctionFixture(provider, YQAssetFunctionV2.Service);
            valid.sourceSignature = manifest.SourceSignature;
            for (int seed = 0; seed < 16; seed++)
            {
                if (!YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(manifest,
                    new[] { "civic" }, "provider-selection-" + seed, out var composition, out string failure,
                    new[] { YQAssetFunctionV2.Service }, YQWorldStructureUsagePolicy.ExteriorShellsOnly))
                    return "Usable provider was displaced by tags or incomplete evidence: " + failure;
                if (composition.SelectedCount != 1 || composition.selectedIds[0] != provider.stableId)
                    return "The selected composition included a label-only or over-budget cell.";
            }
            // note: Losing exact-source approval must immediately prevent that same cell from being accepted on the next attempt.
            valid.sourceSignature = "stale-source";
            return YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(manifest,
                new[] { "civic" }, "stale-provider", out _, out _,
                new[] { YQAssetFunctionV2.Service }, YQWorldStructureUsagePolicy.ExteriorShellsOnly)
                ? "Stale provider evidence was accepted." : string.Empty;
        }
        finally
        {
            DestroySemanticCompositionFixture(manifest, prefabs);
            if (streaming != null) UnityEngine.Object.DestroyImmediate(streaming);
        }
    }

    private static YQReviewedCellFunctionContractV2 AddReviewedFunctionFixture(YQReviewedSemanticZoneRecord zone, YQAssetFunctionV2 function)
    {
        // note: Synthetic approved evidence exercises the acceptance boundary only; this helper never promotes real project assets.
        YQReviewedCellFunctionContractV2 binding = new YQReviewedCellFunctionContractV2
        {
            cellId = zone.stableId,
            reviewState = YQSemanticSiteReviewState.Approved,
            curation = new YQAssetCurationContractV2 { contractVersion = 2, primaryFunction = function }
        };
        binding.curation.sockets.Add(FunctionSocketFixture(YQAssetSocketKindV2.Interaction));
        zone.cellContractsV2.Add(binding);
        return binding;
    }

    private static YQAssetSocketRecordV2 FunctionSocketFixture(YQAssetSocketKindV2 kind)
    {
        return new YQAssetSocketRecordV2
        {
            socketId = kind.ToString(), kind = kind, transformPath = "Sockets/" + kind,
            clearanceSize = Vector3.one
        };
    }

    private static string TestSemanticCompositionRejectsMissingRoles()
    {
        YQReviewedSemanticSiteManifest manifest =
            BuildSemanticCompositionFixture(out List<GameObject> prefabs);
        try
        {
            bool accepted =
                YQCompiledWorldSiteInstance.TryBuildSemanticCompositionV2(
                    manifest,
                    new[] { "poi", "civic", "spaceport" },
                    "invalid-role-settlement",
                    out _,
                    out string failure);
            return !accepted && failure.IndexOf(
                    "spaceport",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? string.Empty
                : "A missing required role did not fail closed with a useful reason.";
        }
        finally
        {
            DestroySemanticCompositionFixture(manifest, prefabs);
        }
    }

    private static YQReviewedSemanticSiteManifest
        BuildSemanticCompositionFixture(out List<GameObject> prefabs)
    {
        prefabs = new List<GameObject>();
        List<YQReviewedSemanticZoneRecord> zones =
            new List<YQReviewedSemanticZoneRecord>();
        zones.Add(BuildSemanticZoneFixture(
            "civic_core", 0f,
            new[] { "poi", "civic", "circulation" }, prefabs));
        zones.Add(BuildSemanticZoneFixture(
            "residential_lane", 24f,
            new[] { "residential", "service", "circulation" }, prefabs));
        zones.Add(BuildSemanticZoneFixture(
            "market_lane", 48f,
            new[] { "market", "commerce", "circulation" }, prefabs));
        zones.Add(BuildSemanticZoneFixture(
            "guarded_edge", 72f,
            new[] { "perimeter", "defense", "circulation" }, prefabs));

        YQReviewedSemanticSiteManifest manifest =
            ScriptableObject.CreateInstance<YQReviewedSemanticSiteManifest>();
        manifest.Configure(
            "test-semantic-settlement-kit",
            "test_settlement",
            80,
            zones,
            true);
        return manifest;
    }

    private static YQReviewedSemanticZoneRecord BuildSemanticZoneFixture(
        string id,
        float x,
        IEnumerable<string> tags,
        List<GameObject> prefabs)
    {
        GameObject prefab = new GameObject("Fixture__" + id);
        prefabs.Add(prefab);
        return new YQReviewedSemanticZoneRecord
        {
            stableId = id,
            displayName = id,
            prefab = prefab,
            authoredSourceOrigin = new Vector3(x, 0f, 0f),
            localBoundsCenter = Vector3.zero,
            localBoundsSize = new Vector3(20f, 12f, 20f),
            sourceInstanceCount = 20,
            semanticTags = new List<string>(tags)
        };
    }

    private static void DestroySemanticCompositionFixture(
        YQReviewedSemanticSiteManifest manifest,
        IReadOnlyList<GameObject> prefabs)
    {
        // note: Contract fixtures are transient editor objects and never enter the project asset database or a runtime scene.
        if (prefabs != null)
        {
            for (int index = 0; index < prefabs.Count; index++)
            {
                if (prefabs[index] != null)
                    UnityEngine.Object.DestroyImmediate(prefabs[index]);
            }
        }
        if (manifest != null)
            UnityEngine.Object.DestroyImmediate(manifest);
    }

    private static bool ContainsFailure(
        IReadOnlyList<string> errors,
        YQSpatialBlueprintFailureV2 failure)
    {
        string prefix = failure + ":";
        for (int index = 0; index < errors.Count; index++)
        {
            if ((errors[index] ?? string.Empty).StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
