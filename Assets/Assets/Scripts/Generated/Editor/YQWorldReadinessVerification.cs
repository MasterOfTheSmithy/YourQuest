using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: These detached fixtures exercise production entry points without selecting, saving, or regenerating a profile.
public static class YQWorldReadinessVerification
{
    [MenuItem("YourQuest/Verification/Repair Landscape Texture Imports")]
    public static void RepairLandscapeTextureImports()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Texture contract repair requires Edit Mode.");
        // note: These colour/alpha sources were imported as normal maps. Correct only their importer contracts; preserve image contents, GUIDs, and material references.
        string[] paths = {
            "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Textures/SycamoreLeaves5.psd",
            "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Textures/ScotsPinebranches.psd",
            "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Textures/ScotsPineTrunk.psd",
            "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Textures/SycamoreBark4.psd",
            "Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Textures/T_Grass_01_BaseColor_Alpha.png"
        };
        var report = new List<string> { "Evidence: explicit Edit Mode texture importer repair", "utc=" + DateTime.UtcNow.ToString("O") };
        foreach (string path in paths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Required landscape texture missing: " + path);
            report.Add("BEFORE " + path + " type=" + importer.textureType + " alpha=" + importer.alphaSource +
                " transparency=" + importer.alphaIsTransparency + " preserveCoverage=" + importer.mipMapsPreserveCoverage);
            Undo.RegisterCompleteObjectUndo(importer, "Repair landscape texture contract");
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = .38f;
            importer.SaveAndReimport();
            report.Add("AFTER " + path + " guid=" + AssetDatabase.AssetPathToGUID(path) + " type=" + importer.textureType);
        }
        // note: Repair existing generated landscape variants as well as future conversions. Only remove optional maps demonstrably copied from their colour source; retain the material, GUID, and authored base texture.
        int repairedMaterials = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Assets/GeneratedAssets/MaterialVariants/URP" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // note: Do not load thousands of unrelated high-resolution material dependencies for a bounded leaf/ground correction.
            string fileName = Path.GetFileName(path).ToLowerInvariant();
            if (!fileName.StartsWith("ground") && !fileName.StartsWith("sycamore") && !fileName.StartsWith("scotspine")) continue;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_BaseMap")) continue;
            Texture colour = material.GetTexture("_BaseMap");
            string colourPath = AssetDatabase.GetAssetPath(colour);
            bool landscape = Array.Exists(paths, source => string.Equals(source, colourPath, StringComparison.OrdinalIgnoreCase)) ||
                (colourPath.StartsWith("Assets/ADG_Textures/", StringComparison.OrdinalIgnoreCase) && colourPath.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!landscape || colour == null) continue;
            bool changed = false;
            string[] properties = { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };
            string[] keywords = { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" };
            for (int i = 0; i < properties.Length; i++)
            {
                if (!material.HasProperty(properties[i]) || material.GetTexture(properties[i]) != colour) continue;
                if (!changed) Undo.RecordObject(material, "Repair landscape material map contract");
                material.SetTexture(properties[i], null);
                material.DisableKeyword(keywords[i]);
                report.Add("CLEAR " + path + " " + properties[i] + " incorrectly used " + colourPath);
                changed = true;
            }
            if (changed) { EditorUtility.SetDirty(material); repairedMaterials++; }
        }
        AssetDatabase.SaveAssets();
        report.Add("repairedMaterials=" + repairedMaterials);
        Directory.CreateDirectory("outputs/World_Presentation_Repair_20261003");
        string output = "outputs/World_Presentation_Repair_20261003/Texture_Contracts_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQWorldReadinessVerification] Texture contract receipt=" + output);
    }

    [MenuItem("YourQuest/Verification/Verify Water Width and Material Reuse")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("World readiness fixtures require Edit Mode.");
        var report = new List<string> { "# World readiness: detached Edit fixtures",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "runtimeMvid=" + typeof(YQContinuousWorldFeatureAuthority).Assembly.ManifestModule.ModuleVersionId };
        int checks = 0, failures = 0;
        void Check(bool ok, string label) { checks++; if (!ok) failures++; report.Add((ok ? "PASS " : "FAIL ") + label); }
        Scene scene = EditorSceneManager.NewPreviewScene();
        var owned = new List<UnityEngine.Object>();
        try
        {
            // note: A colour-only authored material must not supply colour pixels to optional map channels during conversion.
            var colourOnly = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var colourTexture = new Texture2D(2, 2);
            owned.Add(colourOnly); owned.Add(colourTexture);
            colourOnly.SetTexture("_BaseMap", colourTexture);
            colourOnly.SetTexture("_MainTex", colourTexture);
            MethodInfo findTexture = typeof(YQRuntimeUrpMaterialRepair).GetMethod("FindTexture", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Material), typeof(string[]) }, null);
            foreach (string field in new[] { "NormalTextureProperties", "MetallicTextureProperties", "OcclusionTextureProperties", "EmissionTextureProperties" })
            {
                var properties = (string[])typeof(YQRuntimeUrpMaterialRepair).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Check(findTexture.Invoke(null, new object[] { colourOnly, properties }) == null, "colour-only source keeps optional map empty " + field);
            }
            MethodInfo terminal = typeof(YQContinuousWorldFeatureAuthority).GetMethod(
                "TryApplyAcceptedWaterTerminalModifiers", BindingFlags.NonPublic | BindingFlags.Static);
            // note: Detached Editor fixtures inspect internal pure helpers without widening the runtime API.
            MethodInfo carve = typeof(YQContinuousWorldFeatureAuthority).GetMethod("SampleAcceptedWaterCarve", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo gradeMethod = typeof(YQContinuousWorldFeatureAuthority).GetMethod("SampleAcceptedRouteGrade", BindingFlags.NonPublic | BindingFlags.Static);
            float transitionDistance = (float)typeof(YQContinuousWorldFeatureAuthority).GetField("AcceptedTerrainTransitionDistance", BindingFlags.NonPublic | BindingFlags.Static).GetRawConstantValue();
            float Grade(float original, float target, float terrainHeight, float halfWidth, float shoulder, float distance) =>
                (float)gradeMethod.Invoke(null, new object[] { original, target, terrainHeight, halfWidth, shoulder, distance });
            // note: Vary accepted widths, depths and uncarved hill height; sample both sides of the finite-to-continuation join.
            foreach (float width in new[] { 8f, 14f, 32f })
            foreach (float depth in new[] { 1f, 4.5f, 9f })
            foreach (float original in new[] { 0.2f, 0.5f, 0.9f })
            {
                var prepared = CreateRiver(width, depth);
                foreach (float fraction in new[] { -1f, -0.8f, 0f, 0.8f, 1f })
                {
                    float x = fraction * width * 0.5f;
                    bool influenced = YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(
                        prepared, x, 511f, original, 140f, out float finite);
                    object[] args = { prepared, x, 513f, original, 140f, 0f };
                    bool continued = (bool)terminal.Invoke(null, args);
                    float extended = (float)args[5];
                    string label = "width=" + width + " depth=" + depth + " base=" + original + " across=" + fraction;
                    Check(influenced && continued && finite < 0.4f && extended < 0.4f, "full wet width submerged " + label);
                    Check(Mathf.Abs(finite - extended) < 0.000001f, "finite/terminal cross-section agrees " + label);
                    if (fraction == 0f)
                        Check(Mathf.Abs((0.4f - extended) * 140f - depth) < 0.0001f, "accepted center depth " + label);
                    if (Mathf.Abs(fraction) == 1f)
                        Check((0.4f - finite) * 140f <= 0.13f, "shallow supported bank contact " + label);
                }
                YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(prepared,
                    width + transitionDistance + 8f, 511f, original, 140f, out float dry);
                Check(dry == original, "distant terrain unchanged width=" + width);
                YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(prepared, width * 0.5f + 5f, 511f, 0.2f, 140f, out float bank);
                Check(bank > 0.2f, "accepted bank supports water above low terrain width=" + width);
                // note: Dry bank transitions must agree across the finite/terminal seam and remain idempotent on restored terrain.
                foreach (float offset in new[] { 5f, 20f, 60f, 180f })
                {
                    float x = width * .5f + offset;
                    YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(prepared, x, 511f, original, 140f, out float finiteBank);
                    object[] bankArgs = { prepared, x, 513f, original, 140f, 0f };
                    terminal.Invoke(null, bankArgs);
                    Check(Mathf.Abs(finiteBank - (float)bankArgs[5]) < .000001f, "dry bank seam offset=" + offset);
                    float repeated = (float)carve.Invoke(null, new object[] { finiteBank, 140f, .4f, depth, width * .5f, x });
                    Check(Mathf.Abs(repeated - finiteBank) < .000001f, "dry bank idempotent offset=" + offset);
                }
            }

            foreach (float original in new[] { .1f, .4f, .9f })
            foreach (float offset in new[] { 0f, 5f, 20f, 60f, 200f })
            {
                float grade = Grade(original, .4f, 140f, 3f, 2f, offset + 5f);
                Check(Mathf.Abs(grade - .4f) * 140f <= offset * .65f + .0001f, "route shoulder bounded grade offset=" + offset);
                Check(Mathf.Abs(grade - Grade(grade, .4f, 140f, 3f, 2f, offset + 5f)) < .000001f,
                    "route grade idempotent offset=" + offset);
            }
            // note: The broad dry bank of a lower parallel river cannot undercut a higher river's wet centre.
            var overlap = CreateRiver(14f, 4.5f, true);
            YQContinuousWorldFeatureAuthority.TryApplyAcceptedWaterModifier(overlap, 0f, 511f, .2f, 140f, out float wetPriority);
            object[] overlapTerminal = { overlap, 0f, 513f, .2f, 140f, 0f };
            terminal.Invoke(null, overlapTerminal);
            Check(Mathf.Abs(wetPriority - (.4f - 4.5f / 140f)) < .000001f, "wet core overrides neighbouring lower dry bank");
            Check(Mathf.Abs(wetPriority - (float)overlapTerminal[5]) < .000001f, "overlapping dry bank finite/terminal seam");
            // note: The former wet-priority/minimum-dry-bank switch passed contact checks while producing a many-metre cliff just outside the wet edge.
            MethodInfo combinedWater = typeof(YQContinuousWorldFeatureAuthority).GetMethod("TryApplyAcceptedWaterTerrainModifiers", BindingFlags.NonPublic | BindingFlags.Static);
            float bankEdge = 7f + YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f) * 1.414214f + 2f;
            foreach (float original in new[] { .1f, .5f, .9f })
            foreach (float along in new[] { 511f, 513f, 570f })
            foreach (float side in new[] { -1f, 1f })
            {
                object[] inside = { overlap, side * (bankEdge - .001f), along, original, 140f, 0f, true };
                object[] outside = { overlap, side * (bankEdge + .001f), along, original, 140f, 0f, true };
                combinedWater.Invoke(null, inside); combinedWater.Invoke(null, outside);
                float projected = (float)outside[5];
                Check(Mathf.Abs(projected - (float)inside[5]) * 140f < .01f,
                    "overlapping river bank remains continuous base=" + original + " z=" + along + " side=" + side);
                object[] repeat = { overlap, outside[1], along, projected, 140f, 0f, true };
                combinedWater.Invoke(null, repeat);
                Check(Mathf.Abs(projected - (float)repeat[5]) < .000001f,
                    "combined bank projection remains idempotent base=" + original + " z=" + along + " side=" + side);
            }

            Shader legacy = Shader.Find("Standard");
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null)
            {
                // note: Exercise production streamed mesh construction against a sloped, uneven terrain and variable accepted river widths.
                TerrainData groundData = new TerrainData { heightmapResolution = 65, size = new Vector3(128f, 140f, 128f) };
                owned.Add(groundData);
                float[,] heights = new float[65, 65];
                for (int z = 0; z < 65; z++) for (int x = 0; x < 65; x++)
                    heights[z, x] = 0.3f + x * 0.0015f + Mathf.Sin(z * 0.12f) * 0.03f;
                groundData.SetHeights(0, 0, heights);
                GameObject groundObject = Terrain.CreateTerrainGameObject(groundData);
                SceneManager.MoveGameObjectToScene(groundObject, scene);
                Terrain ground = groundObject.GetComponent<Terrain>();
                GameObject host = new GameObject("StreamedContactFixture");
                SceneManager.MoveGameObjectToScene(host, scene);
                Material surface = new Material(urp);
                owned.Add(surface);
                MethodInfo build = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildRibbonSpans", BindingFlags.NonPublic | BindingFlags.Static);
                var points = new List<Vector3>();
                for (int z = 12; z <= 116; z += 8) points.Add(new Vector3(64f, 100f, z));
                int built = (int)build.Invoke(null, new object[] { host.transform, "Road", points, null, 8f, surface, false, 128f, null, ground });
                Check(built == 1, "terrain-following road emitted");
                Mesh roadMesh = host.transform.Find("Road_Span0").GetComponent<MeshFilter>().sharedMesh;
                Check(roadMesh.tangents.Length == roadMesh.vertexCount && Array.TrueForAll(roadMesh.tangents, tangent => new Vector3(tangent.x, tangent.y, tangent.z).sqrMagnitude > .9f),
                    "grounded road supplies normal-map tangents");
                foreach (Vector3 vertex in roadMesh.vertices)
                {
                    float offset = vertex.y - ground.SampleHeight(vertex);
                    Check(offset > 0f && offset < 0.15f, "streamed road vertex contacts sloped terrain");
                }
                var widths = new List<float> { 8f, 24f };
                points = new List<Vector3> { new Vector3(64f, 100f, 12f), new Vector3(64f, 100f, 116f) };
                built = (int)build.Invoke(null, new object[] { host.transform, "River", points, null, 8f, surface, true, 128f, widths, null });
                Check(built == 0, "unsampled long river rejects discontinuity");
                points = new List<Vector3> { new Vector3(64f, 100f, 12f), new Vector3(64f, 100f, 28f) };
                built = (int)build.Invoke(null, new object[] { host.transform, "RiverWidths", points, null, 8f, surface, true, 128f, widths, null });
                Mesh riverMesh = host.transform.Find("RiverWidths_Span0").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = riverMesh.vertices;
                Check(built == 1 && Mathf.Abs(Vector3.Distance(vertices[0], vertices[1]) - 8f) < 0.001f &&
                    Mathf.Abs(Vector3.Distance(vertices[2], vertices[3]) - 24f) < 0.001f, "river retains per-point widths");

                // note: Replay the four observed failed boundary owners from a read-only accepted-plan snapshot, never from an active profile mutation.
                const string boundaryFixture = "outputs/World_Presentation_Repair_20261003/accepted-plan-fixture.json";
                if (File.Exists(boundaryFixture))
                {
                    var plan = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(boundaryFixture));
                    // note: Verify the actual saved palette bindings, including palettes that have no path prefab list, rather than only a synthetic material.
                    var registry = Resources.Load<YQRuntimeWorldAssetRegistry>("YQRuntimeWorldAssetRegistry");
                    MethodInfo paletteSurface = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("FindPaletteMaterial", BindingFlags.NonPublic | BindingFlags.Static);
                    foreach (var palette in plan.assetPalettes)
                    foreach (string roadSeed in new[] { "|fixture", "|continuous-road|3|5", "|continuous-road|8|4" })
                    {
                        Material road = (Material)paletteSurface.Invoke(null, new object[] { palette, registry, YQWorldAssetCatalog.SlotPath, plan.worldSeed + roadSeed });
                        if (road != null) owned.Add(road);
                        Check(road != null && road.mainTexture != null && YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(road),
                            "saved palette resolves textured road surface " + palette.paletteId + roadSeed);
                        Check(road != null && road.renderQueue < 2450 && (!road.HasProperty("_AlphaClip") || road.GetFloat("_AlphaClip") < .5f),
                            "saved palette road excludes foliage cutout " + palette.paletteId + roadSeed);
                    }
                    bool preparedOk = YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure);
                    Check(preparedOk, "accepted boundary fixture resolves " + failure);
                    if (preparedOk)
                    {
                        MethodInfo extract = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryGetAcceptedRoadPoints", BindingFlags.NonPublic | BindingFlags.Static);
                        MethodInfo demand = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("HasAcceptedRoadDemand", BindingFlags.NonPublic | BindingFlags.Static);
                        foreach (Vector2Int cell in new[] { new Vector2Int(1, -3), new Vector2Int(-1, 3), new Vector2Int(0, 8), new Vector2Int(-1, 9),
                            new Vector2Int(2, -3), new Vector2Int(-1, 2), new Vector2Int(-1, 8), new Vector2Int(-2, 9) })
                        {
                            ground.transform.position = new Vector3(-512f + cell.x * 128f, 0f, -512f + cell.y * 128f);
                            object[] roadArgs = { prepared, cell, 128f, ground, null, 0f, null, null, null };
                            bool extracted = (bool)extract.Invoke(null, roadArgs);
                            var routePoints = (List<Vector3>)roadArgs[4];
                            int physical = (int)build.Invoke(null, new object[] { host.transform, "BoundaryRoad_" + cell.x + "_" + cell.y,
                                routePoints, roadArgs[6], roadArgs[5], surface, false, 128f, roadArgs[7], ground });
                            report.Add("BOUNDARY " + cell + " points=" + routePoints.Count + " width=" + roadArgs[5] +
                                " first=" + (routePoints.Count > 0 ? routePoints[0].ToString("F3") : "none") +
                                " last=" + (routePoints.Count > 0 ? routePoints[routePoints.Count - 1].ToString("F3") : "none") + " terrain=" + ground.transform.position);
                            bool required = (bool)demand.Invoke(null, new object[] { prepared, cell, 128f });
                            Check(extracted && required == (physical > 0), "accepted paved footprint agrees with clipped physical surface " + cell);
                            if (cell == new Vector2Int(2, -3) || cell == new Vector2Int(-1, 2) || cell == new Vector2Int(-1, 8) || cell == new Vector2Int(-2, 9))
                                Check(required && physical > 0, "neighbor still publishes the accepted route " + cell);
                        }
                    }
                }
            }
            Check(legacy != null && urp != null, "fixture shaders available");
            if (legacy != null && urp != null)
            {
                var root = new GameObject("GeneratedStructure_Fixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "SM_Wall_Fixture";
                cube.transform.SetParent(root.transform, false);
                var source = new Material(legacy) { name = "Wood_Source", color = new Color(0.6f, 0.5f, 0.4f) };
                owned.Add(source);
                Renderer renderer = cube.GetComponent<Renderer>();
                renderer.sharedMaterial = source;
                int first = YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
                Material repaired = renderer.sharedMaterial;
                int second = YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(root);
                Check(first > 0 && YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(repaired), "legacy structural source repaired");
                Check(second == 0 && renderer.sharedMaterial == repaired, "repeated structural repair reuses material");
                Check(source.shader == legacy, "source asset shader preserved");
            }

            // note: Inventory only the approved visible tree family, retaining exact shaders and slots for material diagnosis.
            var paths = (string[])typeof(YQGeneratedWorldEnvironment).GetField("ApprovedVisibleTreePrefabs",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            foreach (string path in paths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, "approved tree exists " + path);
                if (prefab == null) continue;
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        report.Add("TREE_SOURCE " + path + " renderer=" + renderer.name + " material=" +
                            (material == null ? "<missing>" : material.name + " shader=" + material.shader?.name +
                             " usable=" + YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material)));
                YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Check(YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material), "repaired tree slot " + path + " / " + renderer.name);
                        if (material != null && material.name.IndexOf("needle", StringComparison.OrdinalIgnoreCase) >= 0)
                            Check(material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f &&
                                material.IsKeywordEnabled("_ALPHATEST_ON"), "pine needle alpha preserved " + path);
                    }
            }
        }
        catch (Exception ex) { failures++; report.Add("FAIL " + ex); }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var item in owned) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        }
        report.Add("RESULT " + (failures == 0 ? "PASS" : "FAIL") + " checks=" + checks + " failures=" + failures);
        Directory.CreateDirectory("outputs/World_Readiness_20261002");
        string output = "outputs/World_Readiness_20261002/Fixtures_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQWorldReadinessVerification] " + report[report.Count - 1] + " receipt=" + output);
    }

    [MenuItem("YourQuest/Verification/Audit Current Runtime Materials")]
    public static void AuditRuntime()
    {
        if (!EditorApplication.isPlaying) return;
        var report = new List<string> { "# Read-only runtime material snapshot", "utc=" + DateTime.UtcNow.ToString("O"),
            "runtimeMvid=" + typeof(YQContinuousWorldFeatureAuthority).Assembly.ManifestModule.ModuleVersionId,
            "presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
            "loadingVisible=" + YQStartupLoadingScreen.IsVisible };
        int checkedSlots = 0, invalidSlots = 0;
        // note: A one-shot audit inspects active world renderers and all their LOD slots, without assigning or repairing anything.
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // note: Billboards render their BillboardAsset material; inherited renderer slots are not their material contract.
            if (renderer is BillboardRenderer billboard)
            {
                checkedSlots++;
                if (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))
                {
                    invalidSlots++;
                    report.Add("UNUSABLE billboard=" + renderer.name + " asset=" + AssetDatabase.GetAssetPath(billboard.billboard));
                }
                continue;
            }
            Material[] materials = renderer.sharedMaterials;
            // note: A missing required submesh slot must remain visible even when the serialized array is shorter than the mesh.
            int requiredSlots = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            for (int slot = 0; slot < requiredSlots; slot++)
            {
                Material material = slot < materials.Length ? materials[slot] : null;
                if (!YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot)) continue;
                checkedSlots++;
                if (YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material)) continue;
                invalidSlots++;
                string path = renderer.name;
                for (Transform ancestor = renderer.transform.parent; ancestor != null; ancestor = ancestor.parent)
                    path = ancestor.name + "/" + path;
                report.Add("UNUSABLE path=" + path + " slot=" + slot + " material=" +
                    (material == null ? "<missing>" : material.name + " shader=" + material.shader?.name +
                     " source=" + AssetDatabase.GetAssetPath(material)));
            }
        }
        foreach (Terrain terrain in Terrain.activeTerrains)
            report.Add("TERRAIN name=" + terrain.name + " position=" + terrain.transform.position + " trees=" +
                terrain.terrainData.treeInstanceCount + " detailPrototypes=" + terrain.terrainData.detailPrototypes.Length +
                " material=" + terrain.materialTemplate?.name + " shader=" + terrain.materialTemplate?.shader?.name);
        report.Add("checkedSlots=" + checkedSlots + " unusableSlots=" + invalidSlots);
        Directory.CreateDirectory("outputs/World_Readiness_20261002");
        string output = "outputs/World_Readiness_20261002/Runtime_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
        File.WriteAllLines(output, report);
        Debug.Log("[YQWorldReadinessVerification] Runtime snapshot receipt=" + output);
    }

    private static YQPreparedSpatialMaterializationV2 CreateRiver(float width, float depth, bool overlapDryBank = false)
    {
        // note: Reflection constructs the immutable projection only in this detached fixture; production still consumes validated accepted artifacts.
        object water = new YQSpatialMaterializationWaterV2 { hydrologyId = "fixture-river", kind = YQHydrologyKindV2.River,
            nominalWidth = width, nominalDepth = depth, waterLevelNormalized = 0.4f };
        typeof(YQSpatialMaterializationWaterV2).GetField("pointStart", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(water, 0);
        typeof(YQSpatialMaterializationWaterV2).GetField("pointCount", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(water, 2);
        var waters = new List<YQSpatialMaterializationWaterV2> { (YQSpatialMaterializationWaterV2)water };
        var points = new List<YQSpatialMaterializationWaterPointV2> {
            new YQSpatialMaterializationWaterPointV2 { x = 0f, z = 450f, width = width, waterSurfaceNormalized = .4f },
            new YQSpatialMaterializationWaterPointV2 { x = 0f, z = 512f, width = width, waterSurfaceNormalized = .4f }
        };
        if (overlapDryBank)
        {
            object lower = (YQSpatialMaterializationWaterV2)water;
            typeof(YQSpatialMaterializationWaterV2).GetField("pointStart", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(lower, 2);
            waters.Add((YQSpatialMaterializationWaterV2)lower);
            points.Add(new YQSpatialMaterializationWaterPointV2 { x = 30f, z = 450f, width = width, waterSurfaceNormalized = .15f });
            points.Add(new YQSpatialMaterializationWaterPointV2 { x = 30f, z = 512f, width = width, waterSurfaceNormalized = .15f });
        }
        return (YQPreparedSpatialMaterializationV2)Activator.CreateInstance(typeof(YQPreparedSpatialMaterializationV2),
            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] {
                Array.Empty<YQSpatialMaterializationRegionV2>(), Array.Empty<YQSpatialMaterializationSiteV2>(),
                waters.ToArray(), points.ToArray(),
                Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(),
                Array.Empty<YQSpatialMaterializationCrossingV2>(), new Dictionary<string, int>(),
                new Dictionary<string, int>(), new Dictionary<string, int>(), null }, null);
    }
}
