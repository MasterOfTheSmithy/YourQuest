#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// note: Controlled contracts run production policies with temporary in-memory catalogs; imported-source checks are a separate, read-only acceptance step.
public static class YQEcologyStartupRepairTests
{
    private const string Pirate = "Assets/HIVEMIND/PirateIsland/HDRP(Default)/Art/Prefabs/";
    private const string Fern = Pirate + "SM_EagleFern_01.prefab";
    private const string Grass = Pirate + "SM_WildGrass_01.prefab";
    private const string Bush = "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab";
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("YourQuest/Verification/Ecology Startup Repair Contracts")]
    public static void RunFromMenu() => RunContracts();

    public static void RunBatch() => EditorApplication.Exit(RunContracts() == 0 ? 0 : 1);

    [MenuItem("YourQuest/Verification/Audit Repaired Ecology Imported Sources")]
    public static void AuditImportedSourcesFromMenu() => RunImportedAudit();

    public static void AuditImportedSourcesBatch() => EditorApplication.Exit(RunImportedAudit() == 0 ? 0 : 1);

    public static void RunRepairAndTestsBatch()
    {
        // note: This explicit mutating batch entry point uses the same bounded production migration as the menu, then audits its actual imported result.
        try
        {
            YQWorldAssetIntakeBuilder.RepairRequiredEcologySources();
            int failures = RunContracts() + RunImportedAudit();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogError("[YQEcologyStartupRepairTests] Repair/test batch failed: " + exception);
            EditorApplication.Exit(1);
        }
    }

    private static int RunContracts()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Ecology contracts must run outside Play Mode.");
        var suite = new Suite("controlled production contracts");
        suite.Check("colliderless measured fern and shrub qualify in their own layers", () =>
        {
            foreach (string path in new[] { Fern, Grass, Bush, YQWorldAssetIntakeBuilder.VikingUnderstoryPath })
            {
                YQSpatialAssetRecord record = Measured(path);
                Require(YQEcologySourcePolicy.TryClassifySmallFoliage(record, out var kind), path);
                Require(kind == (path == Bush ? YQSmallFoliageKind.Shrub : YQSmallFoliageKind.Understory), "Layer classification changed.");
                Require(YQWorldAssetIntakeBuilder.CanApproveDecorative(record), "Normal intake rejected suitable colliderless foliage.");
                Require(!YQEcologySourcePolicy.AllowsCollisionProfile(record, true), "Explicit blocking foliage bypassed collision.");
                Require(!YQWorldAssetIntakeBuilder.CanApproveDecorative(record, true), "Blocking foliage bypassed normal approval collision policy.");
            }
        });
        suite.Check("trees rocks and structures still require collision", () =>
        {
            foreach (string leaf in new[] { "SM_PalmTree.prefab", "SM_Rock.prefab", "SM_Wall.prefab", "SM_FernRock.prefab", "SM_ThatchGrass01.prefab" })
            {
                YQSpatialAssetRecord record = Measured(Pirate + leaf);
                Require(!YQEcologySourcePolicy.AllowsCollisionProfile(record, false), "Nonblocking obstacle bypassed collision: " + leaf);
                Require(!YQWorldAssetIntakeBuilder.CanApproveDecorative(record), "Colliderless obstacle was approved: " + leaf);
                record.hasCollider = true;
                record.colliderCount = 1;
                Require(YQEcologySourcePolicy.AllowsCollisionProfile(record, true), "Measured blocking collision rejected.");
            }
        });
        suite.Check("invalid material geometry identity and scripts remain rejected", () =>
        {
            var corruptions = new Action<YQSpatialAssetRecord>[]
            {
                r => r.invalidMaterialSlotCount = 1, r => r.materialReviewSlotCount = 1,
                r => r.materialSlotCount = 0, r => r.hasRenderer = false,
                r => r.rendererCount = 0, r => r.missingScriptCount = 1,
                r => r.sourceGuid = "", r => r.stableAssetId = "wrong_identity",
                r => r.localBoundsSize = Vector3.zero,
                r => r.localBoundsSize = new Vector3(float.NaN, 1, 1),
                r => r.validationIssues.Add("source validation failed")
            };
            for (int i = 0; i < corruptions.Length; i++)
            {
                var record = Measured(Fern);
                corruptions[i](record);
                Require(!YQEcologySourcePolicy.HasValidMeasuredSource(record), "Measured gate admitted corruption " + i);
                Require(!YQWorldAssetIntakeBuilder.CanApproveDecorative(record), "Normal intake admitted corruption " + i);
            }
            var oversized = Measured(Fern);
            oversized.localBoundsSize = new Vector3(1, 5, 1);
            Require(!YQWorldAssetIntakeBuilder.CanApproveDecorative(oversized), "Oversized scenery received small-foliage exemption.");
        });
        suite.Check("staging preserves unrelated approval and never mutates original records", () =>
        {
            var target = Measured(Fern);
            target.libraryEvidenceIds.Add("keep_documentary_link");
            var unrelated = Approved(Measured("Assets/Other/ApprovedRock.prefab"));
            unrelated.hasCollider = true;
            string beforeTarget = JsonUtility.ToJson(target), beforeOther = JsonUtility.ToJson(unrelated);
            var originals = new List<YQSpatialAssetRecord> { unrelated, target };
            List<YQSpatialAssetRecord> staged = Stage(originals, r => { r.ecologySourceDependencyHash = "refreshed"; return Approved(r); }, () => false);
            Require(staged.Count == 2 && ReferenceEquals(staged[0], unrelated), "Unrelated approval was replaced.");
            Require(!ReferenceEquals(staged[1], target), "Target staging edited the published record.");
            Require(JsonUtility.ToJson(target) == beforeTarget && JsonUtility.ToJson(unrelated) == beforeOther, "Original catalog records mutated.");
            Require(staged[1].libraryEvidenceIds.Contains("keep_documentary_link"), "Documentary metadata lost.");
        });
        suite.Check("cancellation after first refresh leaves complete original snapshot intact", () =>
        {
            var originals = new List<YQSpatialAssetRecord> { Measured(Fern), Measured(Grass) };
            string before = JsonUtility.ToJson(originals[0]) + JsonUtility.ToJson(originals[1]);
            int refreshed = 0;
            Expect<OperationCanceledException>(() => Stage(originals, r => { refreshed++; return Approved(r); }, () => refreshed > 0));
            Require(refreshed == 1, "Cancellation did not interrupt between source refreshes.");
            Require(before == JsonUtility.ToJson(originals[0]) + JsonUtility.ToJson(originals[1]), "Cancelled stage leaked changes.");
        });
        suite.Check("refresh failure and invalid refreshed sources cannot produce a staged publication", () =>
        {
            var source = Measured(Fern);
            string before = JsonUtility.ToJson(source);
            Expect<InvalidOperationException>(() => Stage(new[] { source }, r => throw new InvalidOperationException("source changed during validation"), () => false));
            Expect<InvalidOperationException>(() => Stage(new[] { source }, r => null, () => false));
            Expect<InvalidOperationException>(() => Stage(new[] { source }, r => { r.invalidMaterialSlotCount = 1; return r; }, () => false));
            Expect<InvalidOperationException>(() => Stage(new[] { source }, r => { r.sourceGuid = "changed"; r.stableAssetId = "asset_changed"; return Approved(r); }, () => false));
            Expect<InvalidOperationException>(() => Stage(new[] { source }, r => { r.assetPath = Grass; return Approved(r); }, () => false));
            Require(before == JsonUtility.ToJson(source), "Rejected refresh mutated the published source.");
        });
        suite.Check("coastal pools separate understory shrubs and blocking canopy with exact bindings", () =>
        {
            using (var fixture = new PoolFixture())
            {
                var palette = fixture.Palette;
                string before = JsonUtility.ToJson(palette);
                List<GeneratedAssetReferenceRecord> understory = Pool(palette, fixture.Registry, YQSmallFoliageKind.Understory, out string underReason);
                List<GeneratedAssetReferenceRecord> shrubs = Pool(palette, fixture.Registry, YQSmallFoliageKind.Shrub, out string shrubReason);
                Require(understory.Count == 2 && HasPath(understory, Fern) && HasPath(understory, Grass), underReason);
                Require(shrubs.Count == 1 && shrubs[0].assetPath == Bush, shrubReason);
                Require(before == JsonUtility.ToJson(palette), "Pool construction rewrote an accepted palette.");
                fixture.Registry.SetEntries(new List<YQRuntimeWorldAssetEntry>());
                Require(Pool(palette, fixture.Registry, YQSmallFoliageKind.Understory, out string missing).Count == 0 && missing.Contains("unbound="), missing);
            }
        });
        suite.Check("live pools reject invalid source unapproved source and blocking colliderless references", () =>
        {
            using (var fixture = new PoolFixture())
            {
                fixture.Records[0].invalidMaterialSlotCount = 1;
                fixture.Records[1].releaseEligible = false;
                Require(Pool(fixture.Palette, fixture.Registry, YQSmallFoliageKind.Understory, out string why).Count == 0, why);
                fixture.Records[0].invalidMaterialSlotCount = 0;
                fixture.Palette.vegetation[0].blocksNav = true;
                Require(Pool(fixture.Palette, fixture.Registry, YQSmallFoliageKind.Understory, out why).Count == 0, why);
            }
        });
        suite.Check("retry admission respects chunk identity and preserves structural ownership", () =>
        {
            using (var fixture = new RetryFixture())
            {
                fixture.Schedule(fixture.NewChunk(), "transient placement failure");
                Require(fixture.Counts.Count == 0 && fixture.Tickets.Count == 0, "Stale owner scheduled retry.");
                GameObject structure = new GameObject("EcologyContractStructure");
                try
                {
                    fixture.Set("generationRoot", structure);
                    fixture.Set("requiredContentReady", true);
                    fixture.Set("overlayReady", true);
                    fixture.Set("appearanceReady", true);
                    fixture.Schedule(fixture.Chunk, "transient placement failure");
                    Require(fixture.Counts[fixture.Coordinate] == 1 && fixture.Tickets.ContainsKey(fixture.Coordinate), "Transient failure did not retain bounded retry.");
                    Require(ReferenceEquals(fixture.Get("generationRoot"), structure) &&
                        (bool)fixture.Get("requiredContentReady") && (bool)fixture.Get("overlayReady") && (bool)fixture.Get("appearanceReady"),
                        "Ecology retry changed successful structural ownership/readiness.");
                    Require(!fixture.Streamer.TryGetTerminalRequiredEcologyFailure(out _), "Live retry was reported as terminal.");
                }
                finally { fixture.Set("generationRoot", null); UnityEngine.Object.DestroyImmediate(structure); }
            }
        });
        suite.Check("permanent empty pool fails once with no ticket and remains startup-blocking", () =>
        {
            using (var fixture = new RetryFixture())
            {
                // note: The following production Error is expected evidence for the permanent-failure path, not an unexpected regression failure.
                Debug.Log("[YQEcologyStartupRepairTests] EXPECTED production error follows: permanent source qualification failure.");
                fixture.Schedule(fixture.Chunk, YQEcologySourcePolicy.PermanentFailurePrefix + " understory qualified=0");
                Require(!fixture.Tickets.ContainsKey(fixture.Coordinate), "Permanent source failure retained a pointless retry.");
                Require(fixture.Get("state").ToString() == "Failed" && !(bool)fixture.Get("requiredEcologyReady"), "Permanent source failure released readiness.");
                Require(fixture.Streamer.TryGetTerminalRequiredEcologyFailure(out string failure) && failure.Contains("understory qualified=0"), "Startup lost the actual source failure.");
                fixture.Tickets[fixture.Coordinate] = Time.unscaledTime + 1;
                Require(!fixture.Streamer.TryGetTerminalRequiredEcologyFailure(out _), "Pending retry ticket was treated as stalled.");
                fixture.Tickets.Clear();
                fixture.Set("requiredEcologyReady", true);
                Require(!fixture.Streamer.TryGetTerminalRequiredEcologyFailure(out _), "Completed ecology was treated as terminal.");
            }
        });
        suite.Check("reviewed material correction applies while legacy recovery retains usable sources", () =>
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "Configured URP shader unavailable.");
            var source = new Material(shader);
            var replacement = new Material(shader);
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.GetComponent<Renderer>().sharedMaterial = source;
                var binding = new YQRuntimeWorldMaterialOverride { transformPath = "", rendererIndex = 0, materialIndex = 0, replacementMaterial = replacement };
                registry.SetEntries(new List<YQRuntimeWorldAssetEntry> { new YQRuntimeWorldAssetEntry
                    { assetPath = "Assets/EcologyContract.prefab", prefab = instance, materialOverrides = new List<YQRuntimeWorldMaterialOverride> { binding } } });
                Require(registry.ApplyMaterialOverrides("Assets/EcologyContract.prefab", instance, false) == 0 && instance.GetComponent<Renderer>().sharedMaterial == source,
                    "Legacy recovery replaced a usable source.");
                binding.replaceUsableSource = true;
                Require(registry.ApplyMaterialOverrides("Assets/EcologyContract.prefab", instance, false) == 1 && instance.GetComponent<Renderer>().sharedMaterial == replacement,
                    "Reviewed authored-surface correction was ignored.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registry); UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(replacement);
            }
        });
        return suite.Finish();
    }

    private static int RunImportedAudit()
    {
        // note: This audit reads actual imported sources and post-migration approvals; it does not approve assets, save catalogs or select a player profile.
        var suite = new Suite("actual imported sources after migration");
        YQWorldAssetCatalog.InvalidateRuntimeIntakeCatalog();
        var paths = new List<string>();
        for (int i = 1; i <= 4; i++) paths.Add(Pirate + "SM_EagleFern_" + i.ToString("00") + ".prefab");
        for (int i = 1; i <= 6; i++) paths.Add(Pirate + "SM_WildGrass_" + i.ToString("00") + ".prefab");
        foreach (string leaf in new[] { "SM_EagleFern_M_01", "SM_EagleFern_S_01", "SM_EagleFern_S_02", "SM_EagleFern_S_04",
                     "SM_WildGrass_L_01", "SM_WildGrass_M_01", "SM_WildGrass_M_02", "SM_WildGrass_S_01", "SM_WildGrass_S_02", "SM_WildGrass_S_03" })
            paths.Add(YQWorldAssetIntakeBuilder.WoodlandFoliageRoot + leaf + ".prefab");
        paths.Add(YQWorldAssetIntakeBuilder.VikingUnderstoryPath);
        paths.Add(Bush);
        YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
        foreach (string path in paths)
            suite.Check(path, () =>
            {
                Require(YQWorldAssetCatalog.TryGetSpatialRecord(path, out var record), "Missing spatial metadata.");
                Require(YQWorldAssetCatalog.IsSpatiallyApprovedForRuntime(path), "Not migrated/release approved.");
                Require(YQEcologySourcePolicy.HasValidMeasuredSource(record), "Measured source rejected.");
                Require(AssetDatabase.AssetPathToGUID(path) == record.sourceGuid, "Source identity changed.");
                Require(!string.IsNullOrWhiteSpace(record.ecologySourceDependencyHash) &&
                    AssetDatabase.GetAssetDependencyHash(path).ToString() == record.ecologySourceDependencyHash, "Source dependency revision changed; requalification required.");
                Require(registry != null && registry.ContainsPrefabPath(path), "Missing exact runtime binding.");
                Require(YQWorldAssetIntakeBuilder.HasExactEcologyBinding(record, out string bindingReason), bindingReason);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, "Imported prefab is unavailable.");
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Require(renderers.Length > 0, "Actual prefab has no renderers.");
                int materialCount = 0;
                foreach (Renderer renderer in renderers)
                {
                    Require(renderer.bounds.size.sqrMagnitude > 0.0001f, "Actual renderer has zero geometry bounds.");
                    for (int slot = 0; slot < renderer.sharedMaterials.Length; slot++)
                    {
                        materialCount++;
                        Material material = YQWorldAssetIntakeBuilder.ResolveBoundIntakeMaterial(path, renderer, slot);
                        var state = YQWorldAssetIntakeBuilder.EvaluateRuntimeReadyMaterialForAudit(material);
                        Require(state == YQMaterialCompatibilityState.VerifiedUrp || state == YQMaterialCompatibilityState.VerifiedUrpAdapter,
                            "Actual source material cannot resolve to a verified runtime material.");
                    }
                }
                Require(materialCount > 0, "Actual prefab has no material slots.");
            });
        suite.Check("shared shrub preserves original material textures and binds every authored LOD", () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Bush);
            Require(prefab != null && registry != null, "Shared shrub or registry unavailable.");
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeBushes2018/Materials/M_Bush01.mat");
            YQRuntimeWorldAssetEntry entry = registry.ResolveEntry(Bush);
            Require(entry != null && entry.prefab == prefab, "Shrub binding changed source prefab.");
            LODGroup group = prefab.GetComponent<LODGroup>();
            Require(group != null && group.GetLODs().Length == 4, "Authored shrub must retain all four LOD levels.");
            int checkedSlots = 0;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                string transformPath = AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform);
                int rendererIndex = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    string sourceGuid = materials[slot] != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(materials[slot])) : string.Empty;
                    Require(materials[slot] == null || materials[slot] == source || sourceGuid == "465ea732ef1f3e044b316a1cb3cd20b9", "Imported shrub source material was replaced.");
                    int materialSlot = slot;
                    var overrides = entry.materialOverrides.FindAll(item => item.transformPath == transformPath &&
                        item.rendererIndex == rendererIndex && item.materialIndex == materialSlot);
                    Require(overrides.Count == 1, "LOD material slot lacks one exact override: " + transformPath);
                    YQWorldAssetIntakeBuilder.ValidateShrubAdapter(source, overrides[0].replacementMaterial);
                    checkedSlots++;
                }
            }
            Require(checkedSlots == 4, "Expected one material binding per authored shrub LOD.");
        });
        suite.Check("existing coastal palette gains appropriate required layers without save rewrite", () =>
        {
            var palette = new GeneratedRegionAssetPaletteRecord { styleKey = "hivemind_pirate_island" };
            palette.vegetation.Add(Reference(Pirate + "SM_PalmTree_01.prefab"));
            string before = JsonUtility.ToJson(palette);
            var understory = Pool(palette, registry, YQSmallFoliageKind.Understory, out string underReason);
            var shrubs = Pool(palette, registry, YQSmallFoliageKind.Shrub, out string shrubReason);
            Require(understory.Count > 0, underReason);
            Require(shrubs.Count > 0, shrubReason);
            foreach (var reference in understory)
                Require(reference.assetPath.StartsWith(Pirate, StringComparison.Ordinal) &&
                    YQEcologySourcePolicy.ClassifyPath(reference.assetPath) == YQSmallFoliageKind.Understory, "Foreign or unsuitable understory.");
            foreach (var reference in shrubs)
                Require(reference.assetPath == Bush && YQEcologySourcePolicy.ClassifyPath(reference.assetPath) == YQSmallFoliageKind.Shrub, "Unexpected shrub source.");
            Require(before == JsonUtility.ToJson(palette), "Accepted palette mutated.");
        });
        suite.Check("instantiated shrub receives reviewed material on all LODs and normal rebuild retains it", () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Bush);
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/YughuesFreeBushes2018/Materials/M_Bush01.mat");
            Require(YQWorldAssetIntakeBuilder.TryBuildReviewedEcologyMaterialOverrides(prefab, Bush, out var rebuilt) && rebuilt.Count == 4 &&
                rebuilt.TrueForAll(binding => binding.replaceUsableSource), "Normal registry rebuild lost reviewed foliage surface authority.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Require(registry.ApplyMaterialOverrides(Bush, instance, false) == 4, "Runtime did not apply all four reviewed overrides.");
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials) YQWorldAssetIntakeBuilder.ValidateShrubAdapter(source, material);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        });
        suite.Check("all three retained region palettes have separate suitable required foliage pools", () =>
        {
            foreach (string style in new[] { "hivemind_pirate_island", "hivemind_woodland_village", "viking_rural" })
            {
                var palette = new GeneratedRegionAssetPaletteRecord { styleKey = style };
                var understory = Pool(palette, registry, YQSmallFoliageKind.Understory, out string detail);
                Require(understory.Count > 0, detail);
                Require(Pool(palette, registry, YQSmallFoliageKind.Shrub, out detail).Count > 0, detail);
                foreach (var reference in understory)
                    Require(style == "hivemind_pirate_island" ? reference.assetPath.StartsWith(Pirate, StringComparison.Ordinal) :
                        style == "hivemind_woodland_village" ? reference.assetPath.StartsWith(YQWorldAssetIntakeBuilder.WoodlandFoliageRoot, StringComparison.Ordinal) :
                        reference.assetPath == YQWorldAssetIntakeBuilder.VikingUnderstoryPath, "Required flora crossed its authored palette: " + reference.assetPath);
            }
        });
        return suite.Finish();
    }

    private static YQSpatialAssetRecord Measured(string path) => new YQSpatialAssetRecord
    {
        sourceGuid = "ecology_contract_source", stableAssetId = "asset_ecology_contract_source", assetPath = path,
        kitId = "contract_kit", semanticRole = YQWorldAssetCatalog.SlotVegetation,
        compositionScale = YQSpatialCompositionScale.Prop, disposition = YQAssetIntakeDisposition.NeedsSpatialReview,
        hasRenderer = true, rendererCount = 1, materialSlotCount = 1,
        localBoundsSize = new Vector3(1.5f, 1f, 1.5f), footprintX = 1.5f, footprintZ = 1.5f, height = 1
    };

    private static YQSpatialAssetRecord Approved(YQSpatialAssetRecord record)
    {
        record.disposition = YQAssetIntakeDisposition.Candidate;
        record.releaseEligible = true;
        record.spatialMetadataAuthored = true;
        record.curationV2.contractVersion = YQAssetCurationContractV2.SupportedContractVersion;
        return record;
    }

    private static GeneratedAssetReferenceRecord Reference(string path) => new GeneratedAssetReferenceRecord
    {
        assetPath = path, assetType = "prefab", slotTag = YQWorldAssetCatalog.SlotVegetation,
        runtimeEligible = true, weight = 1, scaleMin = 1, scaleMax = 1
    };

    private static List<YQSpatialAssetRecord> Stage(IReadOnlyList<YQSpatialAssetRecord> source,
        Func<YQSpatialAssetRecord, YQSpatialAssetRecord> refresh, Func<bool> cancelled) =>
        YQWorldAssetIntakeBuilder.StageRequiredEcologyRecords(source, refresh, cancelled);

    private static List<GeneratedAssetReferenceRecord> Pool(GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry, YQSmallFoliageKind kind, out string diagnostic)
    {
        // note: Runtime internals remain non-public; reflection crosses Unity's separate Editor assembly only for the production pool entry point.
        MethodInfo method = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildRequiredLowFoliageReferences", StaticPrivate);
        Require(method != null, "Production pool entry point missing.");
        object[] arguments = { palette, registry, kind, null };
        var result = (List<GeneratedAssetReferenceRecord>)method.Invoke(null, arguments);
        diagnostic = (string)arguments[3];
        return result;
    }

    private static bool HasPath(List<GeneratedAssetReferenceRecord> references, string path) => references.Exists(r => r.assetPath == path);
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private sealed class Suite
    {
        private readonly string name;
        private int passed, failed;
        public Suite(string name) { this.name = name; }
        public void Check(string test, Action action)
        {
            try { action(); passed++; Debug.Log("[YQEcologyStartupRepairTests] PASS " + test); }
            catch (Exception exception) { failed++; Debug.LogError("[YQEcologyStartupRepairTests] FAIL " + test + ": " + (exception.InnerException ?? exception)); }
        }
        public int Finish()
        {
            Debug.Log("[YQEcologyStartupRepairTests] " + name + ": passed=" + passed + " failed=" + failed + ". Editor evidence only; no affected-profile Play Mode certification.");
            return failed;
        }
    }

    private sealed class PoolFixture : IDisposable
    {
        private readonly Dictionary<FieldInfo, object> previous = new Dictionary<FieldInfo, object>();
        private readonly YQWorldAssetIntakeCatalog catalog = ScriptableObject.CreateInstance<YQWorldAssetIntakeCatalog>();
        private readonly YQDiscoveredWorldAssetCatalog discovered = ScriptableObject.CreateInstance<YQDiscoveredWorldAssetCatalog>();
        private readonly GameObject binding = new GameObject("EcologyContractBinding");
        public readonly YQRuntimeWorldAssetRegistry Registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
        public readonly List<YQSpatialAssetRecord> Records = new List<YQSpatialAssetRecord>();
        public readonly GeneratedRegionAssetPaletteRecord Palette = new GeneratedRegionAssetPaletteRecord { styleKey = "hivemind_pirate_island" };

        public PoolFixture()
        {
            // note: Snapshot every touched static cache, replace only in-memory catalogs, and restore the exact former objects in Dispose.
            foreach (string fieldName in new[] { "_runtimeIntakeCatalog", "_runtimeIntakeCatalogLoaded", "_indexedSpatialAssets", "_spatialAssetsByPath", "_indexedDiscoveredEntries", "_discoveredEntriesBySlot" })
            {
                FieldInfo field = typeof(YQWorldAssetCatalog).GetField(fieldName, StaticPrivate);
                previous.Add(field, field.GetValue(null));
            }
            FieldInfo discoveredField = typeof(YQDiscoveredWorldAssetCatalog).GetField("_instance", StaticPrivate);
            previous.Add(discoveredField, discoveredField.GetValue(null));
            YQWorldAssetCatalog.InvalidateRuntimeIntakeCatalog();
            typeof(YQWorldAssetCatalog).GetField("_runtimeIntakeCatalog", StaticPrivate).SetValue(null, catalog);
            typeof(YQWorldAssetCatalog).GetField("_runtimeIntakeCatalogLoaded", StaticPrivate).SetValue(null, true);
            discovered.SetEntries(new List<GeneratedAssetReferenceRecord>());
            discoveredField.SetValue(null, discovered);
            var bindings = new List<YQRuntimeWorldAssetEntry>();
            foreach (string path in new[] { Fern, Grass, Bush, Pirate + "SM_PalmTree_01.prefab" })
            {
                Records.Add(Approved(Measured(path)));
                Palette.vegetation.Add(Reference(path));
                bindings.Add(new YQRuntimeWorldAssetEntry { assetPath = path, prefab = binding });
            }
            catalog.SetRecords("ecology_contract", "", new List<YQAssetKitManifest>(), Records, new List<YQMaterialAssetRecord>());
            Registry.SetEntries(bindings);
        }

        public void Dispose()
        {
            foreach (var entry in previous) entry.Key.SetValue(null, entry.Value);
            UnityEngine.Object.DestroyImmediate(Registry);
            UnityEngine.Object.DestroyImmediate(binding);
            UnityEngine.Object.DestroyImmediate(discovered);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private sealed class RetryFixture : IDisposable
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly GameObject owner = new GameObject("EcologyRetryContract");
        private readonly Type chunkType = typeof(YQPlayerFollowingSemanticChunkStreamer).GetNestedType("RuntimeChunk", BindingFlags.NonPublic);
        public readonly Vector2Int Coordinate = new Vector2Int(2, 3);
        public readonly YQPlayerFollowingSemanticChunkStreamer Streamer;
        public readonly object Chunk;
        public readonly Dictionary<Vector2Int, int> Counts;
        public readonly Dictionary<Vector2Int, float> Tickets;
        private readonly IDictionary chunks;

        public RetryFixture()
        {
            // note: An inactive owner exercises only production admission/accounting methods; no terrain, profile, Start callback or Play Mode coroutine runs.
            owner.SetActive(false);
            Streamer = owner.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            Counts = (Dictionary<Vector2Int, int>)Field("_requiredEcologyRetryCount");
            Tickets = (Dictionary<Vector2Int, float>)Field("_requiredEcologyRetryAt");
            chunks = (IDictionary)Field("_chunks");
            ((HashSet<Vector2Int>)Field("_hardViewDemand")).Add(Coordinate);
            Chunk = NewChunk();
            chunks.Add(Coordinate, Chunk);
        }

        private object Field(string name) => typeof(YQPlayerFollowingSemanticChunkStreamer).GetField(name, InstancePrivate).GetValue(Streamer);
        public object NewChunk() => Activator.CreateInstance(chunkType, true);
        public object Get(string name) => chunkType.GetField(name).GetValue(Chunk);
        public void Set(string name, object value) => chunkType.GetField(name).SetValue(Chunk, value);
        public void Schedule(object chunk, string reason) => typeof(YQPlayerFollowingSemanticChunkStreamer)
            .GetMethod("ScheduleRequiredEcologyRetry", InstancePrivate).Invoke(Streamer, new[] { (object)Coordinate, chunk, reason });
        public void Dispose()
        {
            chunks.Clear();
            Counts.Clear();
            Tickets.Clear();
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }
}
#endif
