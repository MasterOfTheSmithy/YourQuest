using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class YQSemanticWorldAuthorityTests
{
    // note: This focused runner records only the current imported semantic authority assembly and its V1 immutability checks.
    private const string PendingRequestPath =
        "Assets/Assets/EditorBuildRequests/RunSemanticWorldAuthorityTests.request";

    private static bool polling;
    private static double nextPoll;

    public static void RunConstructionContractsFromCommandLine()
    {
        // note: The same detached owner fixtures can run in an isolated batch Editor when the interactive Editor is closed; this never enters Play Mode.
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Construction command-line checks require an idle batch Editor.");
        int failures = 1;
        try { failures = RunConstructionContractSuites(); }
        catch (Exception exception) { Debug.LogException(exception); }
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int RunConstructionContractSuites()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Construction command-line checks require an idle batch Editor.");
        // note: Exit status comes from these exact invocations, not an older receipt or the last general suite alone.
        int frontierFailures = RunFrontierContinuationContracts();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-yqFrontierOnly") >= 0)
        {
            Debug.Log("[YQConstructionContracts] frontier-only failures=" + frontierFailures);
            return frontierFailures;
        }
        int sectorFailures = RunSiteSectorLayoutProbe();
        int sharedFailures = RunSharedSiteFootprintProbe();
        int generalFailures = RunAndReportSemanticContracts();
        int failures = frontierFailures + sectorFailures + sharedFailures + generalFailures;
        Debug.Log("[YQConstructionContracts] failures=" + failures + "; frontier=" + frontierFailures +
            "; sector=" + sectorFailures + "; shared=" + sharedFailures + "; general=" + generalFailures);
        return failures;
    }

    public static void RunColdProviderReplayContractsFromCommandLine()
    {
        // note: Real Editor ticks drive asynchronous Resources completion. This detached runner neither enters Play Mode nor creates a manager/profile or publishes a site.
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-quit") >= 0)
            throw new InvalidOperationException("Cold provider checks require an idle batch Editor without -quit; the runner exits after recording completion.");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var catalogField = typeof(YQCompiledWorldSiteBindingService).GetField("catalog", flags);
        var savedCatalog = catalogField.GetValue(null);
        var manifests = (Dictionary<string, YQReviewedSemanticSiteManifest>)typeof(YQCompiledWorldSiteInstance)
            .GetField("PreparedManifestCache", flags).GetValue(null);
        var savedManifests = new Dictionary<string, YQReviewedSemanticSiteManifest>(manifests);
        var pending = (Dictionary<string, YQProceduralSettlementLayoutRecord>)typeof(YQProceduralSettlementLayout)
            .GetField("pending", flags).GetValue(null);
        var savedPending = new Dictionary<string, YQProceduralSettlementLayoutRecord>(pending);
        var worldBefore = WorldStateManager.Instance; var playerBefore = PlayerStateManager.Instance;
        int epoch = YQServiceLifecycle.RequestEpoch, failures = 0, ticks = 0, catalogTicks = 0, manifestTicks = 0;
        bool finished = false; string manifestKey = string.Empty;
        var checks = new List<object>();
        void Check(string name, bool pass) { checks.Add(new { name, passed = pass }); if (!pass) failures++; }
        bool Current() => YQServiceLifecycle.IsCurrent(epoch) && !EditorApplication.isPlayingOrWillChangePlaymode &&
            ReferenceEquals(worldBefore, WorldStateManager.Instance) && ReferenceEquals(playerBefore, PlayerStateManager.Instance);
        IEnumerator Replay()
        {
            Check("batch starts with empty existing canonical/provider caches", savedCatalog == null && savedManifests.Count == 0);
            if (savedCatalog != null || savedManifests.Count != 0) yield break;
            var warm = typeof(YQCompiledWorldSiteBindingService).GetMethod("WarmCanonicalCatalogRoutine", flags);
            int callbacks = 0; bool ready = false; string failure = string.Empty; int catalogStarted = ticks;
            yield return (IEnumerator)warm.Invoke(null, new object[] { (Func<bool>)Current,
                (Action<bool, string>)((passed, reason) => { callbacks++; ready = passed; failure = reason; }) });
            catalogTicks = ticks - catalogStarted;
            Check("canonical catalog completes its real asynchronous request once", ready && callbacks == 1 && string.IsNullOrEmpty(failure));
            if (!ready) yield break;
            var catalog = catalogField.GetValue(null) as YQRuntimeWorldSiteCatalog;
            var record = catalog?.FindByKitId("qualified_viking_home");
            Check("loaded catalog resolves the exact qualified saved kit", record != null && record.spatiallyValidated && record.seamlessPlacementEligible);
            if (record == null) yield break;
            string recordBody = JsonUtility.ToJson(record);
            manifestKey = record.runtimeManifestResourceKey;
            ResourceRequest request = Resources.LoadAsync<YQReviewedSemanticSiteManifest>(manifestKey);
            int manifestStarted = ticks;
            yield return request;
            manifestTicks = ticks - manifestStarted;
            if (!Current()) throw new InvalidOperationException("Detached provider loading ownership changed.");
            var manifest = request.asset as YQReviewedSemanticSiteManifest;
            Check("exact allow-listed manifest request completes before asset consumption", request.isDone && manifest != null && manifest.ReleaseEligible && manifest.KitId == record.kitId);
            if (manifest == null) yield break;
            string manifestBody = JsonUtility.ToJson(manifest);
            var site = new YQSpatialMaterializationSiteV2 { siteId = "detached-cold-replay-owner", sourceSemanticId = "detached-cold-replay-semantic",
                kind = YQSiteKindV2.Settlement, x = 0f, z = 0f, headingDegrees = 0f, reservedRadius = 72f };
            var members = new[] {
                new YQSiteMemberFootprintV2 { memberId = "east", x = 384f, z = 0f, reservedRadius = 32f, sectorIndex = 1 },
                new YQSiteMemberFootprintV2 { memberId = "west", x = -384f, z = 0f, reservedRadius = 32f, sectorIndex = 2 } };
            // note: Reflection must update the boxed immutable projection value before it is passed to the real sector solver.
            object boxedSite = site;
            typeof(YQSpatialMaterializationSiteV2).GetField("memberFootprint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(boxedSite, members);
            site = (YQSpatialMaterializationSiteV2)boxedSite;
            var sources = new HashSet<string>(manifest.Zones.Where(zone => zone != null && zone.prefab != null &&
                YQProceduralSettlementLayout.HasUsableExternalConnection(zone)).Select(zone => zone.stableId), StringComparer.OrdinalIgnoreCase);
            string seed = YQProceduralSettlementLayout.BuildReusableSectorSeed("detached-cold-provider-replay", site);
            bool resolved = YQProceduralSettlementLayout.TryResolve(manifest, sources, seed, out var layout, out failure, site);
            Check("cold approved source builds the actual reusable three-sector layout", resolved && layout.version == 6 && layout.cells.Count == 3);
            if (!resolved) throw new InvalidOperationException(failure);
            var location = new GeneratedSpatialContinuationLocationV2Record {
                anchor = new YQSiteAnchorV2 { siteId = site.siteId, sourceSemanticId = site.sourceSemanticId, kind = site.kind,
                    x = site.x, z = site.z, preferredHeadingDegrees = site.headingDegrees, reservedRadius = site.reservedRadius,
                    memberFootprint = members.ToList(), requiredFunctions = new List<YQAssetFunctionV2> {
                        YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Circulation } },
                settlement = new GeneratedSettlementRecord { settlementId = site.sourceSemanticId, runtimeSiteKitId = record.kitId,
                    runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion, proceduralLayout = layout },
                selectedSourceCellIds = sources.OrderBy(id => id, StringComparer.Ordinal).ToList(), compositionSeed = seed,
                compositionGeometrySignature = YQProceduralSettlementLayout.GeometrySignature(layout) };
            var validate = typeof(YQCompiledWorldSiteInstance).GetMethod("TryValidateAcceptedContinuationFunctionsWithManifest", flags);
            bool Validate(GeneratedSpatialContinuationLocationV2Record input)
            {
                object[] args = { input, manifest, record, null };
                bool pass = (bool)validate.Invoke(null, args); failure = args[3] as string; return pass;
            }
            Check("cold committed provider passes actual functions/source/layout/sector/budget gates", Validate(location));
            Check("three cold sector placements account for all 171 source instances", YQCompiledWorldSiteInstance.TryMeasureSectorPayload(
                manifest, sources, layout, out int cost, out _) && cost == 171);
            var settings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings", flags).GetValue(null);
            string body = Newtonsoft.Json.JsonConvert.SerializeObject(location, settings);
            var replay = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(body, settings);
            Check("actual save serialization replays the committed cold provider", Validate(replay) &&
                Newtonsoft.Json.JsonConvert.SerializeObject(replay, settings) == body);
            replay.selectedSourceCellIds[0] = "missing-source";
            Check("cold replay rejects a changed committed source", !Validate(replay));
            replay = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(body, settings);
            replay.compositionGeometrySignature = "changed";
            Check("cold replay rejects a changed geometry signature", !Validate(replay));
            replay = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(body, settings);
            replay.anchor.memberFootprint[0].x += 2f;
            Check("cold replay rejects a changed immutable member reserve", !Validate(replay));
            replay = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(body, settings);
            replay.anchor.requiredFunctions.Add(YQAssetFunctionV2.Transition);
            Check("cold replay rejects an unsupported physical function", !Validate(replay));
            Check("provider validation leaves real approved assets and committed layout unchanged", JsonUtility.ToJson(record) == recordBody &&
                JsonUtility.ToJson(manifest) == manifestBody && Newtonsoft.Json.JsonConvert.SerializeObject(location, settings) == body);
            Check("detached provider validation does not admit a manifest into runtime caches", manifests.Count == savedManifests.Count);
            callbacks = 0; ready = true;
            yield return (IEnumerator)warm.Invoke(null, new object[] { (Func<bool>)(() => false),
                (Action<bool, string>)((passed, reason) => { callbacks++; ready = passed; failure = reason; }) });
            Check("stale catalog warmup rejects once without changing the loaded catalog", !ready && callbacks == 1 &&
                ReferenceEquals(catalogField.GetValue(null), catalog));
        }
        var stack = new Stack<IEnumerator>(); stack.Push(Replay());
        AsyncOperation waiting = null; double deadline = EditorApplication.timeSinceStartup + 120d;
        void Finish(Exception error)
        {
            if (finished) return;
            finished = true; EditorApplication.update -= Tick;
            if (error != null) Check("execution: " + error.GetBaseException().Message, false);
            // note: Drain every owned iterator and restore existing cache objects before running other fixtures or exiting.
            while (stack.Count > 0)
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception cleanup) { Check("cleanup: " + cleanup.GetBaseException().Message, false); }
            catalogField.SetValue(null, savedCatalog);
            manifests.Clear(); foreach (var pair in savedManifests) manifests.Add(pair.Key, pair.Value);
            pending.Clear(); foreach (var pair in savedPending) pending.Add(pair.Key, pair.Value);
            Check("actual player/world managers and existing caches are restored", ReferenceEquals(worldBefore, WorldStateManager.Instance) &&
                ReferenceEquals(playerBefore, PlayerStateManager.Instance) && ReferenceEquals(catalogField.GetValue(null), savedCatalog) &&
                manifests.Count == savedManifests.Count && pending.Count == savedPending.Count);
            try
            {
                string HashFile(string path) { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
                var hashes = new Dictionary<string, string>();
                foreach (string file in new[] { "YQRuntimeWorldSiteCatalog.cs", "YQProceduralSettlementLayout.cs", "Editor/YQSemanticWorldAuthorityTests.cs" })
                    hashes[file] = HashFile("Assets/Assets/Scripts/Generated/" + file);
                string directory = "outputs/YourQuest_Tandem_20261004/cold-provider-replay-verification";
                Directory.CreateDirectory(directory);
                string path = directory + "/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json";
                File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    utc = DateTime.UtcNow.ToString("O"), verdict = failures == 0 ? "PASS" : "FAIL", failures, checks,
                    evidence = "DETACHED_EDITOR_ASYNC_RESOURCES_AND_PROVIDER_REPLAY_NOT_WORLD_ADMISSION_OR_SPAWNING",
                    batchEditor = Application.isBatchMode, unity = Application.unityVersion, ticks, catalogTicks, manifestTicks, manifestKey,
                    sourceHashes = hashes, catalogAssetSha256 = HashFile("Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset"),
                    manifestAssetSha256 = HashFile("Assets/Assets/Resources/YQWorldSites/qualified_viking_home/YQRuntimeSemanticSite.asset"),
                    runtimeMvid = typeof(YQCompiledWorldSiteInstance).Assembly.ManifestModule.ModuleVersionId.ToString(),
                    editorMvid = typeof(YQSemanticWorldAuthorityTests).Assembly.ManifestModule.ModuleVersionId.ToString(),
                    runtimeAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll"),
                    editorAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll") }, Newtonsoft.Json.Formatting.Indented));
                Debug.Log("[YQColdProviderReplay] " + checks.Count + " checks; failures=" + failures + "; receipt=" + path);
                // note: The cold receipt certifies its own suite; the process exit additionally accounts for every following construction suite.
                if (failures == 0) failures += RunConstructionContractSuites();
            }
            catch (Exception recording) { failures++; Debug.LogException(recording); }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        void Tick()
        {
            try
            {
                ticks++;
                if (!Current()) throw new InvalidOperationException("Detached asynchronous runner lost its owner epoch.");
                if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Detached approved provider loading exceeded 120 seconds.");
                if (waiting != null) { if (!waiting.isDone) return; waiting = null; }
                for (int step = 0; step < 16; step++)
                {
                    if (stack.Count == 0) { Finish(null); return; }
                    var current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); (current as IDisposable)?.Dispose(); continue; }
                    object yielded = current.Current;
                    if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                    if (yielded is AsyncOperation operation) { waiting = operation; return; }
                    if (yielded != null) throw new InvalidOperationException("Unsupported detached provider wait: " + yielded.GetType().Name);
                    return;
                }
            }
            catch (Exception error) { Finish(error); }
        }
        EditorApplication.update += Tick;
    }

    public static void RunHiddenProviderPhysicsProbeFromCommandLine()
    {
        // note: Measure the real Unity collider API before choosing an inactive-provider actor staging path; this diagnostic never enters Play Mode.
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Hidden provider diagnostic requires an idle batch Editor.");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.hideFlags = HideFlags.HideAndDontSave;
        floor.transform.position = new Vector3(32768f, 8192f, 32768f);
        floor.transform.localScale = new Vector3(8f, 1f, 8f);
        try
        {
            var collider = floor.GetComponent<Collider>();
            var ray = new Ray(floor.transform.position + Vector3.up * 5f, Vector3.down);
            Physics.SyncTransforms();
            bool activeDirect = collider.Raycast(ray, out _, 10f);
            bool activeScene = Physics.Raycast(ray, out _, 10f);
            floor.SetActive(false);
            Physics.SyncTransforms();
            bool inactiveDirect = collider.Raycast(ray, out _, 10f);
            bool inactiveScene = Physics.Raycast(ray, out _, 10f);
            var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"),
                ["evidence"] = "DETACHED_EDITOR_COLLIDER_API_DIAGNOSTIC_NOT_GAMEPLAY",
                ["activeDirect"] = activeDirect, ["activeScene"] = activeScene,
                ["inactiveDirect"] = inactiveDirect, ["inactiveScene"] = inactiveScene };
            string path = Path.GetFullPath("outputs/YourQuest_Tandem_20261004/hidden-provider-physics-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
            File.WriteAllText(path, receipt.ToString());
            Debug.Log("[YQHiddenProviderPhysics] " + receipt.ToString(Newtonsoft.Json.Formatting.None));
        }
        finally { UnityEngine.Object.DestroyImmediate(floor); }
    }

    [InitializeOnLoadMethod]
    private static void QueueRequestedRun()
    {
        // note: A request marker keeps the fixture runnable in the existing Unity project owner without inventing a test scene.
        EditorApplication.delayCall += RunRequestedTests;
        if (polling)
            return;
        polling = true;
        EditorApplication.update += RunRequestedTests;
    }

    private static void RunRequestedTests()
    {
        if (EditorApplication.timeSinceStartup < nextPoll)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 1d;
        if (!File.Exists(Path.GetFullPath(PendingRequestPath)) ||
            EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        // note: The explicit cost probe shares this existing one-shot request path; ordinary semantic contract requests retain their original runner.
        string requestedMode = File.ReadAllText(Path.GetFullPath(PendingRequestPath)).Trim();
        bool sharedSitesOnly = string.Equals(requestedMode, "shared-site-footprints", StringComparison.Ordinal);
        bool sectorLayoutsOnly = string.Equals(requestedMode, "site-sector-layouts", StringComparison.Ordinal);
        bool continuationOnly = string.Equals(requestedMode, "frontier-continuation-contracts", StringComparison.Ordinal);
        bool queryCostOnly = string.Equals(requestedMode,
            "r2-query-cost", StringComparison.Ordinal);
        // note: Consume the marker only after imports settle so the result describes one stable source/build identity.
        AssetDatabase.DeleteAsset(PendingRequestPath);
        // note: Keep the single idle poller registered so later one-shot requests do not depend on an unrelated domain reload.
        if (continuationOnly) { RunFrontierContinuationContracts(); return; }
        if (sectorLayoutsOnly) { RunSiteSectorLayoutProbe(); return; }
        if (sharedSitesOnly)
        {
            RunSharedSiteFootprintProbe();
            return;
        }
        if (queryCostOnly)
        {
            RunR2QueryCostProbe();
            return;
        }
        RunFromMenu();
    }

    private static int RunFrontierContinuationContracts()
    {
        // note: Synthetic detached documents exercise nullable compatibility and hashing only; no generated site, owner proof, profile or acceptance transaction is published.
        var checks = new List<object>(); int failures = 0;
        var physicalSamples = new List<object>();
        void Check(string name, bool pass) { checks.Add(new { name, passed = pass }); if (!pass) failures++; }
        var worldBefore = WorldStateManager.Instance; var playerBefore = PlayerStateManager.Instance;
        try
        {
            var parent = new GeneratedSpatialWorldPlanV2Record { worldSeed = "detached-continuation-parent",
                generationVersion = GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
                validationVersion = GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                acceptanceState = GeneratedSpatialPlanAcceptanceState.Accepted };
            parent.contentHash = parent.validatedContentHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(parent);
            string baseHash = parent.contentHash, memberHash = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(parent);
            var settings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            T Clone<T>(T value) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value, settings), settings);
            Check("absent continuation preserves nullable old-save contract", YQSpatialContinuationValidatorV2.ValidateBasic(parent).IsStructurallyValid &&
                !Newtonsoft.Json.JsonConvert.SerializeObject(parent, settings).Contains("acceptedContinuation"));
            var continuation = new GeneratedSpatialContinuationV2Record { worldSeed = parent.worldSeed,
                parentSpatialContentHash = baseHash, parentMemberFootprintHash = memberHash };
            GeneratedSpatialContinuationLocationV2Record Location(string suffix, float x)
            {
                var anchor = new YQSiteAnchorV2 { siteId = "detached-site-" + suffix, sourceSemanticId = "detached-settlement-" + suffix,
                    parentRegionId = "detached-region", kind = YQSiteKindV2.Settlement, x = x, z = -6000f,
                    reservedRadius = 64f, terrainSearchRadius = 128f, maximumSlopeDegrees = 18f, placementMode = YQSitePlacementModeV2.RouteFrontage,
                    requiredFunctions = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Circulation } };
                anchor.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = "member-b", x = x + 256f, z = -6000f, reservedRadius = 32f, sectorIndex = 2 });
                anchor.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = "member-a", x = x - 256f, z = -6000f, reservedRadius = 32f, sectorIndex = 1 });
                return new GeneratedSpatialContinuationLocationV2Record { contentId = "detached-content-" + suffix, deterministicSeed = "detached-seed-" + suffix,
                    source = YQSpatialContinuationSourceV2.ExplicitFallback, sourceContentId = "detached-fixture", sourceContentHash = "detached-fixture-only",
                    anchor = anchor, settlement = new GeneratedSettlementRecord { settlementId = anchor.sourceSemanticId, regionId = anchor.parentRegionId },
                    entrances = new List<GeneratedSemanticEntranceRecord> { new GeneratedSemanticEntranceRecord { entranceId = "entry-" + suffix, worldX = x, worldZ = -6000f } } };
            }
            continuation.locations.Add(Location("a", 6000f)); continuation.locations.Add(Location("b", -6000f));
            Check("staged typed documents are structurally valid without physical acceptance", YQSpatialContinuationValidatorV2.ValidateBasic(parent, continuation).IsStructurallyValid);
            foreach (var location in continuation.locations) location.contentHash = YQSpatialContinuationHasherV2.ComputeLocationContentHash(location);
            continuation.contentHash = YQSpatialContinuationHasherV2.ComputeContentHash(continuation);
            string hash = continuation.contentHash;
            var opposite = Clone(continuation); opposite.locations.Reverse(); foreach (var location in opposite.locations) location.anchor.memberFootprint.Reverse();
            Check("location and member enumeration does not change checksum", YQSpatialContinuationHasherV2.ComputeContentHash(opposite) == hash);
            Check("actual world serializer round-trip preserves checksum", YQSpatialContinuationHasherV2.ComputeContentHash(Clone(continuation)) == hash);
            parent.acceptedContinuation = continuation;
            Check("nullable extension leaves historical base and member hash unchanged", baseHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(parent) &&
                memberHash == YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(parent));
            Check("basic validation does not promote staged records", YQSpatialContinuationValidatorV2.ValidateBasic(parent).IsStructurallyValid &&
                continuation.state == YQSpatialContinuationStateV2.Staged && continuation.locations.All(l => l.state == YQSpatialContinuationStateV2.Staged));
            var corrupt = Clone(continuation); corrupt.locations[0].anchor.x += 1f;
            Check("changed geometry rejects stale checksums", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, corrupt).IsStructurallyValid);
            Check("geometry mutation changes payload checksum", YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(corrupt.locations[0]) !=
                YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(continuation.locations[0]));
            var stale = Clone(continuation); stale.parentMemberFootprintHash = "stale";
            Check("stale parent member identity rejects", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, stale).IsStructurallyValid);
            var unsupported = Clone(continuation); unsupported.schemaVersion = "future";
            Check("unsupported continuation version rejects", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, unsupported).IsStructurallyValid);
            var duplicate = Clone(continuation); duplicate.locations.Add(Clone(duplicate.locations[0])); duplicate.contentHash = null;
            Check("duplicate site and semantic identities reject", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, duplicate).IsStructurallyValid);
            var nonfinite = Clone(continuation); nonfinite.locations[0].anchor.x = float.NaN; nonfinite.contentHash = null;
            Check("nonfinite spatial records reject", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, nonfinite).IsStructurallyValid);
            var accepted = Clone(continuation); accepted.state = YQSpatialContinuationStateV2.Accepted; accepted.revision = 1;
            Check("accepted envelope cannot contain staged locations", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, accepted).IsStructurallyValid);
            foreach (var location in accepted.locations) { location.state = YQSpatialContinuationStateV2.Accepted; location.revision = 1; }
            Check("claimed accepted records without owner proofs reject", !YQSpatialContinuationValidatorV2.ValidateBasic(parent, accepted).IsStructurallyValid);
            // note: Synthetic proof claims below test persistence reference rejection only; they never certify or publish a physical site.
            void Seal(GeneratedSpatialContinuationV2Record document)
            {
                foreach (var location in document.locations)
                {
                    location.proofClaims.Clear(); string payload = YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(location);
                    for (int kind = 1; kind <= (int)YQSpatialContinuationProofKindV2.PopulationAndInteractions; kind++)
                        location.proofClaims.Add(new GeneratedSpatialContinuationProofV2Record { kind = (YQSpatialContinuationProofKindV2)kind,
                            outcome = YQSpatialContinuationProofOutcomeV2.Passed, subjectSiteId = location.anchor.siteId,
                            subjectPayloadHash = payload, ownerValidationVersion = "detached-reference-fixture-only", evidenceId = "synthetic", evidenceHash = "synthetic" });
                    location.contentHash = location.validatedContentHash = YQSpatialContinuationHasherV2.ComputeLocationContentHash(location);
                }
                document.contentHash = document.validatedContentHash = YQSpatialContinuationHasherV2.ComputeContentHash(document);
            }
            foreach (var location in accepted.locations)
            {
                location.settlement.runtimeSiteKitId = "fixture-kit"; location.settlement.runtimeSiteBindingVersion = "fixture-binding";
                location.compositionSeed = "fixture-composition"; location.compositionGeometrySignature = "fixture-only";
                location.selectedSourceCellIds.Add("fixture-source");
                location.population.Add(new GeneratedNpcPlanRecord { npcId = location.contentId + "-npc", regionId = location.anchor.parentRegionId,
                    settlementId = location.settlement.settlementId, factionId = "detached-faction" });
            }
            Seal(accepted);
            var detached = WorldState.CreateDefault(); detached.generatedWorldPlan.worldSeed = parent.worldSeed;
            detached.generatedWorldPlan.spatialPlanV2 = Clone(parent); detached.generatedWorldPlan.spatialPlanV2.acceptedContinuation = accepted;
            detached.generatedWorldPlan.regions.Add(new GeneratedRegionRecord { regionId = "detached-region" });
            detached.generatedWorldPlan.factions.Add(new GeneratedFactionPlanRecord { factionId = "detached-faction" });
            YQStateIdentity.EnsureWorldState(detached);
            Check("accepted-shaped continuation participates in normal state references", YQStateReferenceValidator.Validate(null, detached).IsValid);
            string acceptedHash = accepted.contentHash; long revisionBefore = detached.stateRevision;
            Check("accepted identities retain exact supplied IDs and parents", accepted.locations.All(location =>
                detached.identityRecords.Any(identity => identity.id == location.anchor.sourceSemanticId && identity.parentId == "detached-region") &&
                detached.identityRecords.Any(identity => identity.id == location.population[0].npcId && identity.parentId == location.anchor.sourceSemanticId)));
            YQStateIdentity.EnsureWorldState(detached);
            Check("identity registration is idempotent and does not rewrite accepted hashes or revision", detached.identityRecords.Select(identity => identity.id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == detached.identityRecords.Count &&
                accepted.contentHash == acceptedHash && YQSpatialContinuationHasherV2.ComputeContentHash(accepted) == acceptedHash && detached.stateRevision == revisionBefore);
            var restored = Clone(detached); restored.EnsureCollections(); YQStateIdentity.EnsureWorldState(restored);
            Check("world round-trip retains accepted continuation references", YQStateReferenceValidator.Validate(null, restored).IsValid &&
                restored.generatedWorldPlan.spatialPlanV2.acceptedContinuation.contentHash == acceptedHash);
            var stagedWorld = Clone(detached); stagedWorld.identityRecords.Clear(); stagedWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation = Clone(continuation);
            YQStateIdentity.EnsureWorldState(stagedWorld);
            Check("staged continuation registers no accepted site or NPC identity", !stagedWorld.identityRecords.Any(identity => continuation.locations.Any(location =>
                identity.id == location.anchor.siteId || identity.id == location.anchor.sourceSemanticId || identity.id == location.contentId)));
            var badWorld = Clone(detached); badWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation.locations[0].anchor.x += 1f;
            Check("normal save/load reference gate rejects checksum corruption", !YQStateReferenceValidator.Validate(null, badWorld).IsValid);
            var missingRegion = Clone(detached); missingRegion.generatedWorldPlan.regions.Clear();
            Check("normal reference gate rejects missing continuation region", !YQStateReferenceValidator.Validate(null, missingRegion).IsValid);
            var missingFaction = Clone(detached); missingFaction.generatedWorldPlan.factions.Clear();
            Check("normal reference gate rejects missing population faction", !YQStateReferenceValidator.Validate(null, missingFaction).IsValid);
            var collision = Clone(detached); collision.generatedWorldPlan.generatedNpcs.Add(new GeneratedNpcPlanRecord { npcId = accepted.locations[0].population[0].npcId });
            Check("opening NPC identity cannot be shadowed by continuation", !YQStateReferenceValidator.Validate(null, collision).IsValid);
            var conflictingIdentity = Clone(detached); conflictingIdentity.identityRecords.Find(identity => identity.id == accepted.locations[0].population[0].npcId).parentId = "detached-region";
            Check("persisted population identity parent cannot be reassigned", !YQStateReferenceValidator.Validate(null, conflictingIdentity).IsValid);
            var existingNpc = Clone(detached); existingNpc.npcs.Add(new WorldState.NpcRecord { npcId = accepted.locations[0].population[0].npcId,
                locationId = accepted.locations[0].anchor.sourceSemanticId, factionId = "detached-faction" });
            Check("existing population resolves accepted continuation location", YQStateReferenceValidator.Validate(null, existingNpc).IsValid);
            existingNpc.npcs[0].locationId = accepted.locations[1].anchor.sourceSemanticId;
            Check("persisted population cannot move to a different continuation parent silently", !YQStateReferenceValidator.Validate(null, existingNpc).IsValid);
            var existingLocation = Clone(detached); existingLocation.locations.Add(new WorldState.LocationRecord {
                locationId = accepted.locations[0].anchor.sourceSemanticId, regionId = "another-region" });
            Check("persisted site cannot move to another region silently", !YQStateReferenceValidator.Validate(null, existingLocation).IsValid);
            var hostileWorld = Clone(detached); hostileWorld.identityRecords.Clear();
            var hostile = hostileWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation.locations[0];
            hostile.encampment = new GeneratedEncampmentRecord { encampmentId = hostile.anchor.sourceSemanticId, regionId = hostile.anchor.parentRegionId,
                runtimeSiteKitId = hostile.settlement.runtimeSiteKitId, runtimeSiteBindingVersion = hostile.settlement.runtimeSiteBindingVersion };
            hostile.settlement = null; hostile.anchor.kind = YQSiteKindV2.HostileSite;
            hostile.anchor.requiredFunctions = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Encounter, YQAssetFunctionV2.Reward, YQAssetFunctionV2.Security };
            hostile.population[0].encampmentId = hostile.anchor.sourceSemanticId; hostile.population[0].settlementId = null;
            Seal(hostileWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation); YQStateIdentity.EnsureWorldState(hostileWorld);
            Check("hostile continuation resolves through the existing site and NPC vocabulary", YQStateReferenceValidator.Validate(null, hostileWorld).IsValid);
            var poiWorld = Clone(detached); poiWorld.identityRecords.Clear();
            var poi = poiWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation.locations[0];
            poi.pointOfInterest = new GeneratedPointOfInterestRecord { poiId = poi.anchor.siteId, regionId = poi.anchor.parentRegionId };
            poi.poiRuntimeSiteKitId = poi.settlement.runtimeSiteKitId; poi.poiRuntimeSiteBindingVersion = poi.settlement.runtimeSiteBindingVersion;
            poi.settlement = null; poi.anchor.kind = YQSiteKindV2.PointOfInterest; poi.anchor.sourceSemanticId = poi.pointOfInterest.poiId; poi.population.Clear();
            Seal(poiWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation); YQStateIdentity.EnsureWorldState(poiWorld);
            Check("POI can retain one shared site and semantic identity", YQStateReferenceValidator.Validate(null, poiWorld).IsValid);
            var oldWorld = Clone(detached); oldWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation = null;
            Check("absent continuation preserves normal legacy state validation", YQStateReferenceValidator.Validate(null, oldWorld).IsValid);
            bool? populationCompleted = null;
            var noContext = YQGeneratedWorldPopulation.BuildContinuationLocationRoutine(null, null, null, null, null, null,
                (success, failure) => populationCompleted = success);
            Check("population adapter rejects absent live acceptance and provider before staging", !noContext.MoveNext() && populationCompleted == false);
            (noContext as IDisposable)?.Dispose();
            var residentSelector = typeof(YQGeneratedWorldPopulation).GetMethod("FindSettlementNpcs", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var scoped = (List<GeneratedNpcPlanRecord>)residentSelector.Invoke(null, new object[] { detached.generatedWorldPlan,
                accepted.locations[0].anchor.sourceSemanticId, accepted.locations[0].population });
            Check("scoped population selection does not append to base canonical NPCs", scoped.Count == 1 &&
                ReferenceEquals(scoped[0], accepted.locations[0].population[0]) && detached.generatedWorldPlan.generatedNpcs.Count == 0);
            var baseResidents = (List<GeneratedNpcPlanRecord>)residentSelector.Invoke(null, new object[] { detached.generatedWorldPlan,
                accepted.locations[0].anchor.sourceSemanticId, null });
            Check("ordinary resident selection retains the existing base population source", baseResidents.Count == 0);
            var identityMethod = typeof(YQSpatialMaterializationResolverV2).GetMethod("TryGetAcceptedContinuationIdentity",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] identityArgs = { stagedWorld.generatedWorldPlan, null, null, null };
            Check("staged continuation remains absent from prepared identity", (bool)identityMethod.Invoke(null, identityArgs) &&
                identityArgs[1] == null && (string)identityArgs[2] == string.Empty);
            // note: The detached accepted cache-identity fixture supplies the existing opening semantic-parent stamp; its owner claims remain synthetic.
            detached.generatedWorldPlan.spatialPlan.semanticFingerprint = detached.generatedWorldPlan.spatialPlanV2.semanticFingerprint;
            identityArgs = new object[] { detached.generatedWorldPlan, null, null, null };
            Check("accepted identity derives its actual checksum without promoting records", (bool)identityMethod.Invoke(null, identityArgs) &&
                ReferenceEquals(identityArgs[1], accepted) && ((string)identityArgs[2]).Contains(acceptedHash));
            identityArgs = new object[] { badWorld.generatedWorldPlan, null, null, null };
            Check("corrupt accepted identity cannot reuse a claimed cache fingerprint", !(bool)identityMethod.Invoke(null, identityArgs));
            RunFrontierBriefAndPhysicalContracts(Check, continuation.locations[0]);
            RunFrontierPhysicalPlanningContracts(Check, physicalSamples);
            RunPairedProfileRecoveryContracts(Check);
            RunFrontierPublicationOwnershipContracts(Check);
            RunFrontierRefreshOwnershipContracts(Check);
            RunLandmarkInteractionPrecedenceContracts(Check);
            RunFrontierCandidateAndGraphStagingContracts(Check);
            RunFrontierColdReplayBoundaryContracts(Check);
            // note: Force a derived graph refresh on a disposable plan and retain exact accepted mutation ownership.
            var overlayPlan = BuildFixture("detached-overlay-refresh");
            var previousAuthority = YQSemanticWorldAuthority.Ensure(overlayPlan);
            previousAuthority.featureOverlayRevision = 17;
            previousAuthority.overlaySchemaVersion = "detached-overlay-policy";
            var overlay = new GeneratedSemanticFeatureOverlayRecord();
            previousAuthority.featureOverlays.Add(overlay);
            var previousOverlays = previousAuthority.featureOverlays;
            previousAuthority.sourceSpatialFingerprint = "force-detached-derived-refresh";
            var refreshedAuthority = YQSemanticWorldAuthority.Ensure(overlayPlan);
            Check("derived graph refresh preserves durable overlay list and records", !ReferenceEquals(previousAuthority, refreshedAuthority) &&
                ReferenceEquals(previousOverlays, refreshedAuthority.featureOverlays) && ReferenceEquals(overlay, refreshedAuthority.featureOverlays[0]));
            Check("derived graph refresh preserves overlay revision and schema", refreshedAuthority.featureOverlayRevision == 17 &&
                refreshedAuthority.overlaySchemaVersion == "detached-overlay-policy");
        }
        catch (Exception exception) { Check("fixture execution: " + exception.GetBaseException().Message, false); }
        Check("active player/world authorities remain unchanged", ReferenceEquals(worldBefore, WorldStateManager.Instance) && ReferenceEquals(playerBefore, PlayerStateManager.Instance));
        string HashFile(string path) { using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        var sourceHashes = new Dictionary<string, string>();
        foreach (string file in new[] { "YQWorldGenerationV2Contracts.cs", "YQSpatialContinuationV2Validation.cs", "YQGeneratedWorldPopulation.cs", "YQWorldGenerationService.cs",
            "YQSpatialMaterializationV2.cs", "YQSemanticWorldAuthority.cs", "YQContinuousWorldCellAuthority.cs", "YQContinuousWorldFeatureAuthority.cs", "YQRuntimeWorldSiteCatalog.cs",
            "YQGeneratedWorldRuntimeBuilder.cs", "YQProceduralSettlementLayout.cs", "YQPlayerFollowingSemanticChunkStreamer.cs", "Editor/YQSemanticWorldAuthorityTests.cs" })
            sourceHashes[file] = HashFile("Assets/Assets/Scripts/Generated/" + file);
        sourceHashes["../Data/State/YQStateFoundation.cs"] = HashFile("Assets/Assets/Scripts/Data/State/YQStateFoundation.cs");
        sourceHashes["../Data/State/World State/WorldStateManager.cs"] = HashFile("Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs");
        sourceHashes["../Tutorial/YQProfileSaveSystem.cs"] = HashFile("Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs");
        string directory = "outputs/YourQuest_Tandem_20261004/frontier-continuation-verification"; Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), verdict = failures == 0 ? "PASS" : "FAIL", failures, checks, sourceHashes, physicalSamples,
            evidence = "DETACHED_FRONTIER_PLANNING_RECORD_AND_ADMISSION_CONTRACTS_NOT_LIVE_GENERATION_PUBLICATION_OR_GAMEPLAY",
            batchEditor = Application.isBatchMode,
            runtimeMvid = typeof(YQSpatialContinuationValidatorV2).Assembly.ManifestModule.ModuleVersionId,
            editorMvid = typeof(YQSemanticWorldAuthorityTests).Assembly.ManifestModule.ModuleVersionId,
            runtimeAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll"), editorAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll")
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[YQFrontierContinuationContracts] " + checks.Count + " checks; failures=" + failures);
        return failures;
    }

    private static void RunPairedProfileRecoveryContracts(Action<string, bool> check)
    {
        // note: Detached revision tests prevent a newer wall-clock timestamp from making Continue discard more recent accepted world state.
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var newer = typeof(YQProfileSaveSystem).GetMethod("IsNewerRecoveryPair", flags);
        var baselinePlayer = new PlayerState { stateRevision = 100, lastUpdatedUnix = 1000 };
        var baselineWorld = new WorldState { stateRevision = 100, lastUpdatedUnix = 1000, worldIdentity = new YQWorldIdentityRecord { worldId = "recovery-world" } };
        var candidatePlayer = new PlayerState { stateRevision = 101, lastUpdatedUnix = 2000 };
        var candidateWorld = new WorldState { stateRevision = 101, lastUpdatedUnix = 2000, worldIdentity = new YQWorldIdentityRecord { worldId = "recovery-world" } };
        bool IsNewer() => (bool)newer.Invoke(null, new object[] { candidatePlayer, candidateWorld, baselinePlayer, baselineWorld });
        check("paired recovery accepts monotonically newer player and world", IsNewer());
        candidateWorld.stateRevision = 99;
        check("paired recovery rejects newer time with older accepted world", !IsNewer());
        candidateWorld.stateRevision = 101; candidatePlayer.stateRevision = 99;
        check("paired recovery rejects newer world with older player", !IsNewer());
        candidatePlayer.stateRevision = 100; candidateWorld.stateRevision = 100;
        check("equal canonical revisions do not publish a timestamp-only recovery", !IsNewer());
        candidatePlayer.stateRevision = 101; candidateWorld.stateRevision = 101; candidateWorld.worldIdentity.worldId = "other-world";
        check("paired recovery cannot cross world identity", !IsNewer());
        string[] args = Environment.GetCommandLineArgs();
        int folderArgument = Array.IndexOf(args, "-yqRecoverySnapshotFolder");
        if (folderArgument >= 0 && folderArgument + 1 < args.Length)
        {
            string folder = args[folderArgument + 1];
            string playerPath = Path.Combine(folder, "player_state.json"), worldPath = Path.Combine(folder, "world_state.json");
            string playerBefore = File.ReadAllText(playerPath), worldBefore = File.ReadAllText(worldPath);
            string profileId = (string)JObject.Parse(playerBefore)["playerId"];
            object[] readArgs = { playerPath, worldPath, profileId, null, null, null, null };
            check("supplied recovered snapshot passes paired identity and reference validation", (bool)typeof(YQProfileSaveSystem).GetMethod("TryReadRecoveryPair", flags).Invoke(null, readArgs));
            check("recovery preflight preserves supplied snapshot documents", File.ReadAllText(playerPath) == playerBefore && File.ReadAllText(worldPath) == worldBefore);
        }
    }

    private static void RunFrontierBriefAndPhysicalContracts(Action<string, bool> check,
        GeneratedSpatialContinuationLocationV2Record fixtureLocation)
    {
        // note: These disposable data-only fixtures certify shape/rejection and deterministic opportunities; no model, profile, runtime population or physical publication is exercised.
        var settings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        GeneratedSpatialContinuationLocationV2Record Clone(GeneratedSpatialContinuationLocationV2Record value) =>
            Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationLocationV2Record>(Newtonsoft.Json.JsonConvert.SerializeObject(value, settings), settings);
        var contextLocation = Clone(fixtureLocation);
        contextLocation.physicalContext = new GeneratedSpatialContinuationPhysicalContextV2Record();
        contextLocation.physicalContext.terrainPads.Add(new GeneratedSpatialContinuationTerrainPadV2Record
            { sectorId = contextLocation.anchor.siteId, elevationNormalized = .42f });
        foreach (var member in contextLocation.anchor.memberFootprint)
            contextLocation.physicalContext.terrainPads.Add(new GeneratedSpatialContinuationTerrainPadV2Record { sectorId = member.memberId, elevationNormalized = .42f });
        check("physical context covers exact central and member sector IDs", YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(contextLocation).IsStructurallyValid);
        string physicalHash = YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(contextLocation);
        contextLocation.physicalContext.terrainPads.Reverse();
        check("terrain pad enumeration does not alter canonical payload", physicalHash == YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(contextLocation));
        var invalid = Clone(contextLocation); invalid.physicalContext.terrainPads[0].elevationNormalized = float.NaN;
        check("nonfinite physical pad elevation rejects", !YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(invalid).IsStructurallyValid);
        invalid = Clone(contextLocation); invalid.physicalContext.terrainPads.RemoveAt(0);
        check("omitting a physical member pad rejects", !YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(invalid).IsStructurallyValid);
        invalid = Clone(contextLocation); invalid.physicalContext.terrainPads[0].sectorId = "foreign-sector";
        check("foreign pad identity rejects", !YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(invalid).IsStructurallyValid);
        invalid = Clone(contextLocation); invalid.anchor.reservedRadius = 8192f;
        check("oversized physical footprint rejects before cell enumeration", !YQSpatialContinuationValidatorV2.ValidatePhysicalContextOnly(invalid).IsStructurallyValid);
        check("uncached provider cannot be admitted from proof claims", !YQCompiledWorldSiteInstance.TryValidateAcceptedContinuationFunctions(contextLocation, out _));
        check("missing base physical authority cannot be measured", !YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(null, null, contextLocation, out _, out _));

        var schemaMethod = typeof(YQWorldGenerationService).GetMethod("BuildFrontierLocationBriefSchema", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var prepareMethod = typeof(YQWorldGenerationService).GetMethod("TryPrepareFrontierLocationBrief", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        JToken Fill(JObject schema)
        {
            string type = (string)schema["type"];
            if (type == "object") { var obj = new JObject(); foreach (var property in ((JObject)schema["properties"]).Properties()) obj[property.Name] = Fill((JObject)property.Value); return obj; }
            if (type == "array") { var array = new JArray(); for (int i = 0; i < (int)schema["minItems"]; i++) array.Add(Fill((JObject)schema["items"])); return array; }
            if (type == "integer") return new JValue((int)schema["minimum"]);
            return schema["enum"] is JArray values ? values[0].DeepClone() : new JValue("Detached fixture text");
        }
        bool Prepare(JObject brief, JObject schema, GeneratedSpatialContinuationLocationV2Record candidate, out GeneratedSpatialContinuationLocationV2Record result)
        {
            object[] args = { brief.ToString(Newtonsoft.Json.Formatting.None), "detached fixture prompt", Newtonsoft.Json.JsonConvert.SerializeObject(candidate, settings), schema, null, null };
            bool prepared = (bool)prepareMethod.Invoke(null, args); result = (GeneratedSpatialContinuationLocationV2Record)args[4]; return prepared;
        }
        foreach (var kind in new[] { YQSiteKindV2.Settlement, YQSiteKindV2.HostileSite, YQSiteKindV2.PointOfInterest })
        {
            var candidate = Clone(fixtureLocation); candidate.anchor.kind = kind; candidate.settlement = null;
            candidate.population.Clear(); candidate.source = YQSpatialContinuationSourceV2.None; candidate.sourceContentId = null; candidate.sourceContentHash = null;
            candidate.contentHash = null; candidate.anchor.sourceSemanticId = "detached-brief-" + kind;
            var schema = (JObject)schemaMethod.Invoke(null, new object[] { kind, "detached-style", new[] { string.Empty, "detached-faction" } });
            var brief = (JObject)Fill(schema); brief["location"]["displayName"] = "Detached fixture location";
            int npcIndex = 0;
            foreach (JObject npc in (JArray)brief["population"])
            {
                npc["displayName"] = "Detached person " + npcIndex++;
                if (kind == YQSiteKindV2.HostileSite) npc["factionId"] = "detached-faction";
            }
            string before = JsonUtility.ToJson(candidate);
            bool parsed = Prepare(brief, schema, candidate, out var staged);
            string diagnostic = " parsed=" + parsed + " contextNull=" + (staged?.physicalContext == null) +
                " seedEmpty=" + string.IsNullOrEmpty(staged?.compositionSeed) + " proofCount=" + staged?.proofClaims?.Count +
                " unchangedAnchor=" + (staged != null && JsonUtility.ToJson(staged.anchor) == JsonUtility.ToJson(candidate.anchor)) +
                " unchangedCandidate=" + (JsonUtility.ToJson(candidate) == before);
            check(kind + " brief produces staged typed content without binding or proof;" + diagnostic, parsed && staged.state == YQSpatialContinuationStateV2.Staged &&
                staged.source == YQSpatialContinuationSourceV2.LlmProposal && staged.proofClaims.Count == 0 && string.IsNullOrEmpty(staged.validatedContentHash) &&
                staged.physicalContext == null && string.IsNullOrEmpty(staged.compositionSeed) && JsonUtility.ToJson(staged.anchor) == JsonUtility.ToJson(candidate.anchor) &&
                staged.contentId == candidate.contentId && staged.deterministicSeed == candidate.deterministicSeed && JsonUtility.ToJson(candidate) == before);
            check(kind + " brief retains exactly one matching typed semantic payload", parsed &&
                (staged.settlement != null) == (kind == YQSiteKindV2.Settlement) &&
                (staged.encampment != null) == (kind == YQSiteKindV2.HostileSite) &&
                (staged.pointOfInterest != null) == (kind == YQSiteKindV2.PointOfInterest));
            var reservationFingerprint = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("BuildFrontierReservationFingerprint",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            string Fingerprint(GeneratedSpatialContinuationLocationV2Record value) => (string)reservationFingerprint.Invoke(null, new object[] { value });
            check(kind + " typed proposal preserves the exact early physical reservation identity", parsed && Fingerprint(staged) == Fingerprint(candidate));
            if (parsed)
            {
                // note: A model/preparation geometry change cannot reuse the terrain context computed for the original untouched reserve.
                staged.anchor.x += 2f;
                check(kind + " changed proposal geometry cannot reuse early terrain preflight", Fingerprint(staged) != Fingerprint(candidate));
            }
            var injected = (JObject)brief.DeepClone(); injected["anchor"] = new JObject { ["x"] = 999f };
            check(kind + " brief cannot inject engine geometry", !Prepare(injected, schema, candidate, out _));
            injected = (JObject)brief.DeepClone(); injected["state"] = "Accepted";
            check(kind + " brief cannot claim acceptance", !Prepare(injected, schema, candidate, out _));
            injected = (JObject)brief.DeepClone(); injected["location"]["displayName"] = 44;
            check(kind + " brief rejects wrong scalar types", !Prepare(injected, schema, candidate, out _));
            injected = (JObject)brief.DeepClone(); ((JArray)injected["population"]).Add(((JArray)brief["population"]).Count > 0 ? brief["population"][0].DeepClone() : new JObject());
            check(kind + " brief rejects invalid population or duplicate identity", !Prepare(injected, schema, candidate, out _));
        }
        var rawOpportunity = typeof(YQSemanticWorldAuthority).GetMethod("TryBuildContinuationOpportunity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var forward = new Dictionary<int, string>(); int present = 0, quiet = 0;
        for (int index = 0; index < 256; index++)
        {
            object[] args = { "detached-opportunity-fixture", index % 16 - 8, index / 16 - 8, null, (uint)0 };
            bool exists = (bool)rawOpportunity.Invoke(null, args);
            if (exists) present++; else quiet++;
            forward[index] = exists ? JsonUtility.ToJson(args[3]) : string.Empty;
        }
        bool stable = true;
        for (int index = 255; index >= 0; index--)
        {
            object[] args = { "detached-opportunity-fixture", index % 16 - 8, index / 16 - 8, null, (uint)0 };
            bool exists = (bool)rawOpportunity.Invoke(null, args);
            stable &= forward[index] == (exists ? JsonUtility.ToJson(args[3]) : string.Empty);
            if (exists) stable &= !((GeneratedSemanticSiteReservationRecord)args[3]).accepted;
        }
        // note: The requested denser frontier policy still leaves quiet blocks; the previous sub-50% fixture assumed the superseded sparse chance.
        check("seeded opportunity policy retains quiet wilderness", present > 0 && quiet > 0 && present < 256);
        check("opportunity results are unaccepted and independent of traversal order", stable);
        var unsupportedPlan = new GeneratedWorldPlanRecord { worldSeed = "detached-opportunity-fixture" };
        check("public opportunity query rejects absent accepted parent", !YQSemanticWorldAuthority.TryGetUnacceptedContinuationOpportunity(unsupportedPlan, int.MaxValue, int.MinValue, out _, out _));
    }

    private static void RunFrontierPhysicalPlanningContracts(Action<string, bool> check, List<object> physicalSamples)
    {
        // note: Numeric prepared fixtures isolate the production planner without installing an accepted world, loading assets, issuing inference or publishing profiles.
        const string seed = "frontier-positive-numeric-fixture-v1";
        var settings = new Newtonsoft.Json.JsonSerializerSettings { Converters = {
            new Vector2JsonConverter(), new Vector3JsonConverter(), new QuaternionJsonConverter() } };
        T Clone<T>(T value) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(value, settings), settings);
        var preparedConstructor = typeof(YQPreparedSpatialMaterializationV2).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single();
        YQPreparedSpatialMaterializationV2 Prepared(YQSpatialMaterializationRouteV2[] routes, YQSpatialMaterializationRoutePointV2[] points) =>
            (YQPreparedSpatialMaterializationV2)preparedConstructor.Invoke(new object[] {
                new[] { new YQSpatialMaterializationRegionV2 { regionId = "numeric-region", radius = 25000f } },
                Array.Empty<YQSpatialMaterializationSiteV2>(), Array.Empty<YQSpatialMaterializationWaterV2>(), Array.Empty<YQSpatialMaterializationWaterPointV2>(),
                routes, points, Array.Empty<YQSpatialMaterializationCrossingV2>(),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["numeric-region"] = 0 },
                new Dictionary<string, int>(), new Dictionary<string, int>(), null, "", 0L, null });
        var empty = Prepared(Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>());
        var authorityConstructor = typeof(YQContinuousWorldCellAuthority).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 8);
        var authority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, empty, null, true });
        // note: A real accepted terminal ray must grade distant terrain and remain available to frontage admission, including the rendered clamped datum.
        object terminalRoute = new YQSpatialMaterializationRouteV2 { routeId = "numeric-terminal-road", width = 6f, shoulderWidth = 12f,
            parentRegionId = "numeric-region", permittedBoundaryContinuation = true, maximumGradeDegrees = 28f };
        typeof(YQSpatialMaterializationRouteV2).GetField("pointStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(terminalRoute, 0);
        typeof(YQSpatialMaterializationRouteV2).GetField("pointCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(terminalRoute, 2);
        var terminalNetwork = Prepared(new[] { (YQSpatialMaterializationRouteV2)terminalRoute }, new[] {
            new YQSpatialMaterializationRoutePointV2 { x = 512f, z = 2048f, surfaceElevationNormalized = .5f, width = 6f },
            new YQSpatialMaterializationRoutePointV2 { x = 544f, z = 2048f, surfaceElevationNormalized = .51f, width = 6f } });
        var terminalAuthority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, terminalNetwork, null, true });
        var privateStatic = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        // note: Base blueprint sites outside the origin collar need the same immutable terrain reserves as accepted frontier additions.
        var remotePrepared = (YQPreparedSpatialMaterializationV2)preparedConstructor.Invoke(new object[] {
            new[] { new YQSpatialMaterializationRegionV2 { regionId = "numeric-region", radius = 25000f } },
            new[] { new YQSpatialMaterializationSiteV2 { siteId = "numeric-remote-settlement", kind = YQSiteKindV2.Settlement,
                x = 4096f, z = 2048f, reservedRadius = 40f, surfaceElevationNormalized = .7f, terrainReserveReady = true } },
            Array.Empty<YQSpatialMaterializationWaterV2>(), Array.Empty<YQSpatialMaterializationWaterPointV2>(),
            Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(), Array.Empty<YQSpatialMaterializationCrossingV2>(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["numeric-region"] = 0 },
            new Dictionary<string, int>(), new Dictionary<string, int>(), null, "", 0L, null });
        var remoteAuthority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, remotePrepared, null, true });
        check("remote base settlement receives its accepted ground datum", Mathf.Abs(remoteAuthority.SampleHeightNormalizedOffMainThread(4096f, 2048f) - .7f) < .00001f);
        var padProjection = typeof(YQContinuousWorldCellAuthority).GetMethod("ApplyContinuationPads", privateInstance);
        float shoulderHeight = (float)padProjection.Invoke(remoteAuthority, new object[] { 4186f, 2048f, .1f });
        check("remote settlement shoulder stays within accepted grade", Mathf.Abs(shoulderHeight - (.7f - 48f * .45f / 140f)) < .00001f);
        check("remote settlement reserve edge keeps its flat conformance probe", Mathf.Abs((float)padProjection.Invoke(remoteAuthority,
            new object[] { 4138f, 2048f, .1f }) - .7f) < .00001f);
        check("remote settlement shoulder projection is idempotent", shoulderHeight == (float)padProjection.Invoke(remoteAuthority, new object[] { 4186f, 2048f, shoulderHeight }));
        var connectionType = typeof(YQContinuousWorldFeatureAuthority).GetNestedType("FrontierRouteConnection", System.Reflection.BindingFlags.NonPublic);
        var terminalConnections = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(connectionType));
        object[] connectionArgs = { terminalNetwork, 4096f, 2048f, seed, terminalConnections, null };
        bool connectedTerminal = (bool)typeof(YQContinuousWorldFeatureAuthority).GetMethod("TryCollectFrontierRouteConnections", privateStatic).Invoke(null, connectionArgs);
        check("distant accepted terminal road remains a valid bounded network connection", connectedTerminal && terminalConnections.Count > 0 &&
            ((Vector3)connectionType.GetField("point", privateInstance).GetValue(terminalConnections[0])).y == 1f);
        check("accepted terminal terrain matches the rendered clamped road datum", Mathf.Abs(terminalAuthority.SampleHeightNormalizedOffMainThread(4096f, 2048f) - 1f) < .00001f);
        var roadPaint = typeof(YQContinuousWorldCellAuthority).GetMethod("SampleRoadPaintWeight", privateInstance);
        check("accepted terminal road receives packed-earth paint", (float)roadPaint.Invoke(terminalAuthority, new object[] { 4096f, 2048f }) > .99f &&
            (float)roadPaint.Invoke(terminalAuthority, new object[] { 4096f, 2080f }) == 0f);
        // note: Accepted area water must carve remote macro terrain even after the origin sampling collar has ended.
        object lake = new YQSpatialMaterializationWaterV2 { hydrologyId = "numeric-streamed-lake", kind = YQHydrologyKindV2.Lake,
            nominalWidth = 100f, nominalDepth = 2f, waterLevelNormalized = .4f };
        typeof(YQSpatialMaterializationWaterV2).GetField("pointStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(lake, 0);
        typeof(YQSpatialMaterializationWaterV2).GetField("pointCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(lake, 2);
        var lakePrepared = (YQPreparedSpatialMaterializationV2)preparedConstructor.Invoke(new object[] {
            new[] { new YQSpatialMaterializationRegionV2 { regionId = "numeric-region", radius = 25000f } }, Array.Empty<YQSpatialMaterializationSiteV2>(),
            new[] { (YQSpatialMaterializationWaterV2)lake }, new[] {
                new YQSpatialMaterializationWaterPointV2 { x = 3800f, z = 2048f, width = 100f, waterSurfaceNormalized = .4f },
                new YQSpatialMaterializationWaterPointV2 { x = 4200f, z = 2048f, width = 100f, waterSurfaceNormalized = .4f } },
            Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(), Array.Empty<YQSpatialMaterializationCrossingV2>(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["numeric-region"] = 0 },
            new Dictionary<string, int>(), new Dictionary<string, int>(), null, "", 0L, null });
        object[] footprintArgs = { lakePrepared, 0, null, null, 0f, 0f };
        typeof(YQContinuousWorldFeatureAuthority).GetMethod("GetAcceptedAreaWaterFootprint", privateStatic).Invoke(null, footprintArgs);
        check("elongated streamed lake shares the canonical basin inset", (Vector2)footprintArgs[2] == new Vector2(4000f, 2048f) &&
            Mathf.Abs((float)footprintArgs[4] - 235f) < .001f && Mathf.Abs((float)footprintArgs[5] - 47f) < .001f);
        var waterProjection = typeof(YQContinuousWorldFeatureAuthority).GetMethod("TryApplyAcceptedWaterTerrainModifiers", privateStatic);
        object[] waterArgs = { lakePrepared, 4000f, 2048f, .9f, 140f, 0f, true };
        bool carved = (bool)waterProjection.Invoke(null, waterArgs);
        float lakeBed = (float)waterArgs[5];
        check("remote accepted lake center is submerged", carved && lakeBed < .4f);
        waterArgs[3] = lakeBed;
        check("remote lake terrain projection is idempotent", (bool)waterProjection.Invoke(null, waterArgs) && lakeBed == (float)waterArgs[5]);
        var parent = new GeneratedSpatialWorldPlanV2Record { worldSeed = seed, acceptanceState = GeneratedSpatialPlanAcceptanceState.Accepted };
        parent.contentHash = parent.validatedContentHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(parent);
        var plan = new GeneratedWorldPlanRecord { worldSeed = seed, spatialPlanV2 = parent };
        GeneratedSpatialContinuationLocationV2Record Candidate(float x, float z, bool members)
        {
            var result = new GeneratedSpatialContinuationLocationV2Record { contentId = "numeric-content", deterministicSeed = "numeric-frontier-site",
                anchor = new YQSiteAnchorV2 { siteId = "numeric-site", sourceSemanticId = "numeric-semantic", parentRegionId = "numeric-region",
                    kind = YQSiteKindV2.Settlement, x = x, z = z, reservedRadius = 24f, maximumSlopeDegrees = 28f,
                    terrainSearchRadius = 128f, placementMode = YQSitePlacementModeV2.RouteFrontage, requiresTerrainConformance = true,
                    requiredFunctions = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Circulation } } };
            result.entrances.Add(new GeneratedSemanticEntranceRecord { entranceId = "numeric-entry", worldX = x, worldZ = z + 24f, headingDegrees = 0f,
                permittedRouteId = "numeric-frontage-route" });
            if (members)
                foreach (int offset in new[] { -256, 256 })
                {
                    string id = offset < 0 ? "numeric-west" : "numeric-east";
                    result.anchor.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = id, sectorIndex = offset < 0 ? 1 : 2,
                        x = x + offset, z = z, reservedRadius = 24f });
                    result.entrances.Add(new GeneratedSemanticEntranceRecord { entranceId = id + "-entry", worldX = x + offset,
                        worldZ = z + 24f, headingDegrees = 0f, permittedRouteId = id + "-route" });
                }
            return result;
        }
        YQPreparedSpatialMaterializationV2 Network(float x, float z)
        {
            // note: Accepted fixture geometry follows sampled terrain rather than interpolating one long chord across unrelated hills.
            var points = new List<YQSpatialMaterializationRoutePointV2>();
            for (int offset = -512; offset <= 512; offset += 32)
                points.Add(new YQSpatialMaterializationRoutePointV2 { x = x + offset, z = z + 144f, width = 6f,
                    surfaceElevationNormalized = authority.SampleHeightNormalizedOffMainThread(x + offset, z + 144f) });
            object route = new YQSpatialMaterializationRouteV2 { routeId = "numeric-accepted-network", parentRegionId = "numeric-region",
                width = 6f, shoulderWidth = 12f, maximumGradeDegrees = 28f, routeClass = YQRouteClassV2.SecondaryRoad,
                permittedBoundaryContinuation = false };
            typeof(YQSpatialMaterializationRouteV2).GetField("pointStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(route, 0);
            typeof(YQSpatialMaterializationRouteV2).GetField("pointCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(route, points.Count);
            return Prepared(new[] { (YQSpatialMaterializationRouteV2)route }, points.ToArray());
        }
        GeneratedSpatialContinuationLocationV2Record viable = null;
        YQPreparedSpatialMaterializationV2 acceptedNetwork = null;
        GeneratedSpatialContinuationPhysicalContextV2Record context = null;
        string failure = string.Empty;
        for (int index = 0; index < 128 && viable == null; index++)
        {
            float x = (index % 2 == 0 ? 1f : -1f) * (2304f + index / 2 * 128f),
                z = (index % 4 < 2 ? 1f : -1f) * (2048f + index / 8 * 128f);
            var candidate = Candidate(x, z, true); var network = Network(x, z);
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(candidate, settings);
            bool built = YQContinuousWorldCellAuthority.TryBuildFrontierPhysicalContext(plan, network, candidate, out var proposed, out failure);
            physicalSamples.Add(new { x, z, accepted = built, failure });
            if (!built) continue;
            viable = candidate; acceptedNetwork = network; context = proposed;
            check("pure physical planner leaves candidate unchanged", before == Newtonsoft.Json.JsonConvert.SerializeObject(candidate, settings));
        }
        check("bounded signed-coordinate sample contains connected multi-sector physical context", viable != null);
        if (viable == null) throw new InvalidOperationException("No positive numeric frontier fixture: " + failure);
        check("every explicit member frontage has a finite network-connected route", context.routes.Count == 3 && context.terrainPads.Count == 3);
        var measured = Clone(viable); measured.physicalContext = context;
        check("actual terrain owner admits the complete planned multi-sector context", YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan,
            acceptedNetwork, measured, out _, out _));
        var projectedRoutes = new List<YQSpatialMaterializationRouteV2>();
        var projectedPoints = new List<YQSpatialMaterializationRoutePointV2>();
        foreach (var route in context.routes.OrderBy(value => value.routeId, StringComparer.Ordinal))
        {
            object projected = new YQSpatialMaterializationRouteV2 { routeId = route.routeId, continuationOwnerSiteId = measured.anchor.siteId,
                parentRegionId = route.parentRegionId, width = route.width, shoulderWidth = route.shoulderWidth,
                maximumGradeDegrees = route.maximumGradeDegrees, routeClass = route.routeClass, permittedBoundaryContinuation = false };
            typeof(YQSpatialMaterializationRouteV2).GetField("pointStart", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(projected, projectedPoints.Count);
            typeof(YQSpatialMaterializationRouteV2).GetField("pointCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(projected, route.controlPoints.Count);
            projectedRoutes.Add((YQSpatialMaterializationRouteV2)projected);
            foreach (var point in route.controlPoints) projectedPoints.Add(new YQSpatialMaterializationRoutePointV2 {
                x = point.x, z = point.z, surfaceElevationNormalized = point.normalizedElevation, width = route.width });
        }
        var projectedPads = new List<YQSpatialMaterializationContinuationPadV2>();
        foreach (var pad in context.terrainPads)
        {
            var member = measured.anchor.memberFootprint.FirstOrDefault(value => value.memberId == pad.sectorId);
            projectedPads.Add(new YQSpatialMaterializationContinuationPadV2(measured.anchor.siteId, pad.sectorId,
                member?.x ?? measured.anchor.x, member?.z ?? measured.anchor.z, member?.reservedRadius ?? measured.anchor.reservedRadius,
                pad.elevationNormalized, pad.shoulderWidth));
        }
        var append = typeof(YQPreparedSpatialMaterializationV2).GetMethod("WithAcceptedContinuation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var union = (YQPreparedSpatialMaterializationV2)append.Invoke(acceptedNetwork, new object[] {
            Array.Empty<YQSpatialMaterializationSiteV2>(), projectedRoutes.ToArray(), projectedPoints.ToArray(), projectedPads.ToArray(), "numeric-union", 1L });
        RunFrontierTerrainInfluenceContracts(check, viable, context, union);
        var initialAuthority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, acceptedNetwork, measured, true });
        var finalAuthority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, union, measured, true });
        var runtimeAuthority = (YQContinuousWorldCellAuthority)authorityConstructor.Invoke(new object[] { seed, null, null, null, 128f, union, null, true });
        bool sameProjection = true;
        foreach (var route in context.routes)
            for (int index = 1; index < route.controlPoints.Count; index++)
            {
                var first = route.controlPoints[index - 1]; var last = route.controlPoints[index];
                var center = new Vector2((first.x + last.x) * .5f, (first.z + last.z) * .5f);
                var direction = new Vector2(last.x - first.x, last.z - first.z).normalized;
                foreach (float offset in new[] { 0f, 4f, 8f, 14f })
                {
                    var point = center + new Vector2(-direction.y, direction.x) * offset;
                    float initial = initialAuthority.SampleHeightNormalizedOffMainThread(point.x, point.y);
                    sameProjection &= Mathf.Abs(initial - finalAuthority.SampleHeightNormalizedOffMainThread(point.x, point.y)) < .000001f &&
                        Mathf.Abs(initial - runtimeAuthority.SampleHeightNormalizedOffMainThread(point.x, point.y)) < .000001f;
                }
            }
        check("initial admission final-union replay and runtime share exact route core and shoulder heights", sameProjection);
        RunStagedContinuationAuthorityContracts(authorityConstructor, empty, union, check);
        var reordered = Clone(viable); reordered.anchor.memberFootprint.Reverse(); reordered.entrances.Reverse();
        check("physical planning is independent of member and entrance order", YQContinuousWorldCellAuthority.TryBuildFrontierPhysicalContext(plan,
            acceptedNetwork, reordered, out var opposite, out _) && Newtonsoft.Json.JsonConvert.SerializeObject(opposite, settings) == Newtonsoft.Json.JsonConvert.SerializeObject(context, settings));
        var corrupt = Clone(measured); corrupt.physicalContext.routes[0].controlPoints.Last().x += 10f;
        check("replay rejects a physically detached route endpoint", !YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan, acceptedNetwork, corrupt, out _, out _));
        corrupt = Clone(measured); corrupt.entrances[0].permittedRouteId = "foreign-route";
        check("replay rejects an entrance bound to another route", !YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan, acceptedNetwork, corrupt, out _, out _));
        corrupt = Clone(measured); corrupt.entrances[0].headingDegrees = 180f;
        check("replay rejects a route approaching an entrance from its back", !YQContinuousWorldCellAuthority.TryMeasureAcceptedContinuationSite(plan, acceptedNetwork, corrupt, out _, out _));
        corrupt = Clone(viable); corrupt.entrances.RemoveAt(2);
        check("physical planner rejects a member without an explicit frontage", !YQContinuousWorldCellAuthority.TryBuildFrontierPhysicalContext(plan, acceptedNetwork, corrupt, out _, out _));
        var issue = typeof(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance).GetMethod("Issue", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        bool forgedRejected = false;
        try { issue.Invoke(null, new object[] { new WorldState(), "", new GeneratedSpatialContinuationV2Record(), new object() }); }
        catch (System.Reflection.TargetInvocationException exception) { forgedRejected = exception.InnerException is InvalidOperationException; }
        check("a foreign issuer cannot create a construction admission token", forgedRejected);
    }

    private static void RunFrontierTerrainInfluenceContracts(Action<string, bool> check,
        GeneratedSpatialContinuationLocationV2Record candidate, GeneratedSpatialContinuationPhysicalContextV2Record context,
        YQPreparedSpatialMaterializationV2 prepared)
    {
        // note: Reuse the actually measured numeric fixture to compare pre-inference bounds with final prepared geometry without inference or publication.
        const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var builderType = typeof(YQGeneratedWorldRuntimeBuilder);
        var build = builderType.GetMethod("TryBuildFrontierTerrainInfluences", statics);
        var padBounds = builderType.GetMethod("ContinuationPadTerrainInfluence", statics);
        var segmentBounds = builderType.GetMethod("ContinuationRouteTerrainInfluence", statics);
        bool Build(GeneratedSpatialContinuationPhysicalContextV2Record value, out IReadOnlyList<Rect> result)
        {
            object[] args = { candidate, value, null, null };
            bool success = (bool)build.Invoke(null, args); result = (IReadOnlyList<Rect>)args[2]; return success;
        }
        string before = JsonUtility.ToJson(candidate), contextBefore = JsonUtility.ToJson(context);
        check("actual numeric context produces bounded transient terrain influences", Build(context, out var influences));
        check("early influence extraction preserves untouched candidate and numeric context", before == JsonUtility.ToJson(candidate) && contextBefore == JsonUtility.ToJson(context));
        var finalBounds = new List<Rect>();
        for (int index = 0; index < prepared.ContinuationPadCount; index++)
        {
            var pad = prepared.GetContinuationPad(index);
            if (pad.ownerSiteId != candidate.anchor.siteId) continue;
            finalBounds.Add((Rect)padBounds.Invoke(null, new object[] { pad.x, pad.z, pad.reservedRadius, pad.shoulderWidth }));
        }
        for (int index = 0; index < prepared.RouteCount; index++)
        {
            var route = prepared.GetRoute(index);
            if (route.continuationOwnerSiteId != candidate.anchor.siteId) continue;
            for (int point = 1; point < prepared.GetRoutePointCount(index); point++)
            {
                var a = prepared.GetRoutePoint(index, point - 1); var b = prepared.GetRoutePoint(index, point);
                finalBounds.Add((Rect)segmentBounds.Invoke(null, new object[] { a.x, a.z, b.x, b.z, route.width, route.shoulderWidth }));
            }
        }
        IEnumerable<Rect> Sorted(IEnumerable<Rect> values) => values.OrderBy(value => value.xMin).ThenBy(value => value.yMin)
            .ThenBy(value => value.xMax).ThenBy(value => value.yMax);
        check("early central member and finite route envelopes equal final prepared envelopes", influences != null &&
            Sorted(influences).SequenceEqual(Sorted(finalBounds)) && influences.Count == context.terrainPads.Count + context.routes.Sum(route => route.controlPoints.Count - 1));
        float margin = candidate.anchor.reservedRadius + context.terrainPads.Single(pad => pad.sectorId == candidate.anchor.siteId).shoulderWidth;
        check("early central pad includes its exact reserve and shoulder", influences != null && influences.Contains(Rect.MinMaxRect(
            candidate.anchor.x - margin, candidate.anchor.z - margin, candidate.anchor.x + margin, candidate.anchor.z + margin)));
        check("route envelope retains minimum core and broad terrain transition", (Rect)segmentBounds.Invoke(null,
            new object[] { -1000f, -900f, -800f, -900f, .5f, 0f }) == Rect.MinMaxRect(-1386f, -1286f, -414f, -514f));
        var opposite = JsonUtility.FromJson<GeneratedSpatialContinuationPhysicalContextV2Record>(contextBefore);
        opposite.terrainPads.Reverse(); opposite.routes.Reverse();
        check("unordered numeric pad and route enumeration preserves influence geometry", Build(opposite, out var reversed) &&
            Sorted(influences).SequenceEqual(Sorted(reversed)));
        check("early influence extraction rejects absent numeric context", !Build(null, out _));
        var invalid = JsonUtility.FromJson<GeneratedSpatialContinuationPhysicalContextV2Record>(contextBefore);
        invalid.terrainPads[0].sectorId = "foreign-numeric-sector";
        check("early influence extraction rejects foreign pad geometry ownership", !Build(invalid, out _));
        invalid = JsonUtility.FromJson<GeneratedSpatialContinuationPhysicalContextV2Record>(contextBefore);
        invalid.terrainPads.Add(invalid.terrainPads[0]);
        check("early influence extraction rejects duplicate pad geometry", !Build(invalid, out _));
        invalid = JsonUtility.FromJson<GeneratedSpatialContinuationPhysicalContextV2Record>(contextBefore);
        invalid.routes[0].controlPoints[0].x = float.NaN;
        check("early influence extraction rejects nonfinite route bounds", !Build(invalid, out _));
        var root = new GameObject("DetachedFrontierInfluenceScreen") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var streamer = root.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            var screenType = builderType.GetNestedType("FrontierTerrainOwnershipScreen", System.Reflection.BindingFlags.NonPublic);
            var screenMethod = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("TryScreenFrontierTerrainOwnership",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var callback = Delegate.CreateDelegate(screenType, streamer, screenMethod);
            object[] args = { candidate, context, callback, null };
            check("stale streamer rejects the actual pre-inference numeric screen", !(bool)builderType.GetMethod("TryScreenFrontierTerrainInfluences", statics).Invoke(null, args) &&
                ((string)args[3]).Contains("stale streaming owner"));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void RunStagedContinuationAuthorityContracts(System.Reflection.ConstructorInfo constructor,
        YQPreparedSpatialMaterializationV2 original, YQPreparedSpatialMaterializationV2 replacement, Action<string, bool> check)
    {
        // note: A real captured heightfield witnesses detached staging without installing a world, publishing a save or recapturing terrain.
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(YQContinuousWorldCellAuthority);
        object Field(object owner, string name) => type.GetField(name, flags).GetValue(owner);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(1024f, 128f, 1024f), hideFlags = HideFlags.HideAndDontSave };
        GameObject terrainObject = null;
        try
        {
            terrainObject = Terrain.CreateTerrainGameObject(data); terrainObject.hideFlags = HideFlags.HideAndDontSave;
            terrainObject.transform.position = new Vector3(-512f, 0f, -512f);
            var plan = BuildFixture("detached-projection-stage");
            var source = (YQContinuousWorldCellAuthority)constructor.Invoke(new object[] { plan.worldSeed, terrainObject.GetComponent<Terrain>(), null, plan, 128f, original, null, true });
            var authority = plan.semanticAuthority;
            var overlays = authority.featureOverlays;
            authority.sourceSpatialFingerprint = "staging-must-not-rebuild-this-derived-record";
            var cacheField = typeof(YQSpatialMaterializationResolverV2).GetField("cachedPrepared", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object cached = cacheField.GetValue(null);
            var changed = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) changed[z, x] = .75f;
            data.SetHeights(0, 0, changed);
            var method = type.GetMethod("WithAcceptedMaterialization", flags);
            var staged = (YQContinuousWorldCellAuthority)method.Invoke(source, new object[] { replacement });
            var captured = (float[,])Field(source, "originHeightSamples");
            check("staged authority shares its original captured heightfield without recapture", captured != null && captured[0, 0] == 0f &&
                ReferenceEquals(captured, Field(staged, "originHeightSamples")));
            check("staged authority retains exact seed sampler origin bounds and plan", new[] { "worldSeed", "seedHash", "originSampler", "semanticPlan", "originMinX", "originMaxX", "originMinZ", "originMaxZ", "originY", "originHeight", "cellSize", "macroOffset", "ridgeOffset", "biomeOffset" }
                .All(name => Equals(Field(source, name), Field(staged, name))));
            check("staged authority installs only its supplied immutable projection", ReferenceEquals(Field(source, "acceptedMaterialization"), original) &&
                ReferenceEquals(Field(staged, "acceptedMaterialization"), replacement));
            check("staged authority leaves semantic overlay and resolver ownership unchanged", ReferenceEquals(plan.semanticAuthority, authority) &&
                ReferenceEquals(authority.featureOverlays, overlays) && authority.sourceSpatialFingerprint == "staging-must-not-rebuild-this-derived-record" &&
                ReferenceEquals(cacheField.GetValue(null), cached));
            bool rejectsNull = false;
            try { method.Invoke(source, new object[] { null }); }
            catch (System.Reflection.TargetInvocationException exception) { rejectsNull = exception.InnerException is ArgumentNullException; }
            check("staged authority rejects absent prepared projection", rejectsNull);
        }
        finally
        {
            if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void RunFrontierPublicationOwnershipContracts(Action<string, bool> check)
    {
        // note: Inactive disposable components exercise transaction ownership and delta application only; no Awake, profile path, save or accepted construction is used.
        var worldOwnerBefore = WorldStateManager.Instance;
        var profileOwnerBefore = YQProfileSaveSystem.Instance;
        var root = new GameObject("DetachedFrontierPublicationOwner") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            var manager = root.AddComponent<WorldStateManager>();
            var profiles = root.AddComponent<YQProfileSaveSystem>();
            check("publication preparation rejects absent construction token", !profiles.TryPrepareContinuationPublication(null, out _, out _));
            var create = typeof(YQProfileSaveSystem.PreparedContinuationPublication).GetMethod("Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var foreign = (YQProfileSaveSystem.PreparedContinuationPublication)create.Invoke(null, null);
            check("foreign publication marker cannot write a paired revision", !profiles.TryPublishContinuation(foreign, out var receipt, out _) && receipt == null);
            var settings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            var source = new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord {
                worldSeed = "detached-publication-delta", spatialPlanV2 = new GeneratedSpatialWorldPlanV2Record() } };
            typeof(WorldStateManager).GetProperty("State").SetValue(manager, source);
            string sourceJson = Newtonsoft.Json.JsonConvert.SerializeObject(source, settings);
            var detached = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(sourceJson, settings);
            var accepted = new GeneratedSpatialContinuationV2Record { state = YQSpatialContinuationStateV2.Accepted, revision = 1 };
            detached.generatedWorldPlan.spatialPlanV2.acceptedContinuation = accepted;
            detached.TouchNow();
            var appended = new List<YQEntityIdentityRecord> { new YQEntityIdentityRecord { id = "detached-delta-identity", kind = YQStableEntityKind.Content } };
            var snapshotType = typeof(WorldStateManager).GetNestedType("PreparedContinuationSnapshot", System.Reflection.BindingFlags.NonPublic);
            var snapshot = snapshotType.GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single().Invoke(
                new object[] { manager, source, detached, sourceJson, Newtonsoft.Json.JsonConvert.SerializeObject(detached, settings), appended });
            var validate = typeof(WorldStateManager).GetMethod("TryValidateContinuationSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            bool Current() => (bool)validate.Invoke(manager, new object[] { snapshot, null });
            check("detached delta snapshot retains exact live baseline", Current() && source.generatedWorldPlan.spatialPlanV2.acceptedContinuation == null && source.stateRevision == 0);
            source.globalFlags["unrevisioned-mutation"] = 1f;
            check("unrevisioned deep world mutation invalidates staged publication", !Current());
            source.globalFlags.Remove("unrevisioned-mutation");
            var identities = source.identityRecords;
            source.identityRecords = new List<YQEntityIdentityRecord>(identities);
            check("equal-valued replacement identity list invalidates publication", !Current());
            source.identityRecords = identities;
            var plan = source.generatedWorldPlan; var parent = plan.spatialPlanV2;
            var containers = source.containers; var events = source.eventLog; var flags = source.globalFlags;
            typeof(WorldStateManager).GetMethod("ReserveContinuationPublication", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, new[] { snapshot });
            check("reserving delta capacity does not publish authority", Current() && parent.acceptedContinuation == null && source.identityRecords.Count == 0);
            typeof(WorldStateManager).GetMethod("ApplyPublishedContinuation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(manager, new[] { snapshot });
            check("internal delta application preserves document inventory and event references", ReferenceEquals(manager.State, source) &&
                ReferenceEquals(source.generatedWorldPlan, plan) && ReferenceEquals(plan.spatialPlanV2, parent) && ReferenceEquals(source.identityRecords, identities) &&
                ReferenceEquals(source.containers, containers) && ReferenceEquals(source.eventLog, events) && ReferenceEquals(source.globalFlags, flags) &&
                ReferenceEquals(parent.acceptedContinuation, accepted) && identities.Count == 1 && source.stateRevision == detached.stateRevision);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        check("disposable publication components leave canonical managers unchanged", ReferenceEquals(WorldStateManager.Instance, worldOwnerBefore) &&
            ReferenceEquals(YQProfileSaveSystem.Instance, profileOwnerBefore));
    }

    private static void RunFrontierRefreshOwnershipContracts(Action<string, bool> check)
    {
        // note: Disposable owners exercise append/retirement/cancellation only; no LLM, profile, accepted publication or Play operation runs.
        const System.Reflection.BindingFlags member = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var streamerType = typeof(YQPlayerFollowingSemanticChunkStreamer);
        var refreshType = streamerType.GetNestedType("PreparedContinuationRefresh", System.Reflection.BindingFlags.NonPublic);
        var refresh = Activator.CreateInstance(refreshType, true);
        var influences = (List<Rect>)refreshType.GetField("terrainInfluences").GetValue(refresh);
        influences.Add(Rect.MinMaxRect(-512f, -512f, -384f, -384f));
        var intersects = streamerType.GetMethod("IntersectsContinuationInfluence", statics);
        bool Touches(Rect bounds) => (bool)intersects.Invoke(null, new object[] { refresh, bounds });
        check("append influence includes closed shared cell edges", Touches(Rect.MinMaxRect(-640f, -512f, -512f, -384f)));
        check("append influence excludes disjoint negative-coordinate cells", !Touches(Rect.MinMaxRect(-768f, -768f, -641f, -641f)));
        var root = new GameObject("DetachedFrontierRefreshOwner") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        TerrainData originData = null;
        try
        {
            var streamer = root.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            var builderBefore = YQGeneratedWorldRuntimeBuilder.Instance;
            var builder = root.AddComponent<YQGeneratedWorldRuntimeBuilder>();
            int proposalsPrepared = 0, constructionCompleted = 0;
            var prepare = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("PrepareFrontierConstructionWithAdmissionRoutine", member);
            var rejected = (IEnumerator)prepare.Invoke(builder, new object[] { null, null,
                (Action<YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance, string>)((value, reason) => constructionCompleted++),
                (Action)(() => proposalsPrepared++), null });
            check("unavailable frontier owner cannot spend a substantive proposal attempt", !rejected.MoveNext() && proposalsPrepared == 0 && constructionCompleted == 1);
            (rejected as IDisposable)?.Dispose();
            check("detached rejected construction preserves the existing builder authority", ReferenceEquals(builderBefore, YQGeneratedWorldRuntimeBuilder.Instance));
            var guard = streamerType.GetMethod("RejectOwnedContinuationCell", member);
            object[] args = { refresh, new Vector2Int(0, 0), "fixture terrain", null };
            check("append guard rejects a published cell inside the terrain modifier", (bool)guard.Invoke(streamer, args) &&
                ((string)args[3]).Contains("fixture terrain"));
            args[1] = new Vector2Int(8, 8);
            check("append guard retains unrelated live owners", !(bool)guard.Invoke(streamer, args));
            // note: The factored predicate is used before inference and again behind the complete final publication gate.
            var ownership = streamerType.GetMethod("TryValidateContinuationTerrainOwnership", member);
            bool Ownership(IReadOnlyList<Rect> bounds, out string reason)
            {
                object[] ownershipArgs = { bounds, null };
                bool result = (bool)ownership.Invoke(streamer, ownershipArgs); reason = (string)ownershipArgs[1]; return result;
            }
            check("detached idle owners pass the shared numeric influence screen", Ownership(influences, out _));
            check("shared ownership screen rejects absent or empty influences", !Ownership(null, out _) && !Ownership(Array.Empty<Rect>(), out _));
            check("shared ownership screen rejects nonfinite influence geometry", !Ownership(new[] { new Rect(float.NaN, 0f, 1f, 1f) }, out _));
            check("shared ownership screen rejects inverted influence geometry", !Ownership(new[] { new Rect(0f, 0f, -1f, 1f) }, out _));
            var origin = new GameObject("DetachedAcceptedOriginTerrain") { hideFlags = HideFlags.HideAndDontSave };
            origin.SetActive(false); origin.transform.SetParent(root.transform, false); origin.transform.position = new Vector3(-512f, 0f, -512f);
            originData = new TerrainData { heightmapResolution = 33, size = new Vector3(128f, 64f, 128f), hideFlags = HideFlags.HideAndDontSave };
            var originTerrain = origin.AddComponent<Terrain>(); originTerrain.terrainData = originData;
            var terrainField = streamerType.GetField("_terrain", member); terrainField.SetValue(streamer, originTerrain);
            check("early shared guard retains the accepted origin terrain veto", !Ownership(influences, out string originReason) && originReason.Contains("accepted origin terrain"));
            terrainField.SetValue(streamer, null);
            foreach (var owner in new[] {
                new KeyValuePair<string, string>("_extendedTerrainTiles", "published terrain"),
                new KeyValuePair<string, string>("_provisionalGroundTiles", "provisional terrain"),
                new KeyValuePair<string, string>("_pendingTerrainPublications", "pending terrain publication"),
                new KeyValuePair<string, string>("_terrainPainting", "terrain painting"),
                new KeyValuePair<string, string>("_activeGenerations", "content generation") })
            {
                var owned = (IDictionary)streamerType.GetField(owner.Key, member).GetValue(streamer);
                owned[new Vector2Int(0, 0)] = null;
                check("shared early/final guard rejects " + owner.Value, !Ownership(influences, out string reason) && reason.Contains(owner.Value));
                owned.Clear(); owned[new Vector2Int(8, 8)] = null;
                check("shared guard preserves unrelated " + owner.Value, Ownership(influences, out _));
                owned.Clear();
            }
            var chunkOwners = (IDictionary)streamerType.GetField("_chunks", member).GetValue(streamer);
            var chunkType = streamerType.GetNestedType("RuntimeChunk", System.Reflection.BindingFlags.NonPublic);
            var numericChunk = Activator.CreateInstance(chunkType, true);
            chunkOwners[new Vector2Int(0, 0)] = numericChunk;
            check("record-only chunk does not claim materialized terrain content", Ownership(influences, out _));
            foreach (string marker in new[] { "physicalRepresentation", "generationRoot", "decorativeRoot", "ownedObjects", "generatedObjects" })
            {
                var field = chunkType.GetField(marker);
                if (marker == "physicalRepresentation") field.SetValue(numericChunk, true);
                else if (marker.EndsWith("Objects", StringComparison.Ordinal)) ((List<GameObject>)field.GetValue(numericChunk)).Add(root);
                else field.SetValue(numericChunk, root);
                check("shared guard rejects materialized chunk " + marker, !Ownership(influences, out string reason) && reason.Contains("materialized content"));
                if (marker == "physicalRepresentation") field.SetValue(numericChunk, false);
                else if (marker.EndsWith("Objects", StringComparison.Ordinal)) ((List<GameObject>)field.GetValue(numericChunk)).Clear();
                else field.SetValue(numericChunk, null);
            }
            chunkOwners.Clear();
            var lateOwners = (IDictionary)streamerType.GetField("_pendingTerrainPublications", member).GetValue(streamer);
            bool earlyPassed = Ownership(influences, out _);
            lateOwners[new Vector2Int(0, 0)] = null;
            check("early success cannot authorize influence over a later terrain owner", earlyPassed && !Ownership(influences, out _));
            lateOwners.Clear();
            object[] finalArgs = { null, null };
            check("shared numeric guard cannot replace full final refresh provenance", !(bool)streamerType.GetMethod("TryValidateContinuationRefresh", member).Invoke(streamer, finalArgs));
            var tasks = (Dictionary<Vector2Int, System.Threading.Tasks.Task<float[,]>>)streamerType.GetField("_terrainHeightPrefetches", member).GetValue(streamer);
            var cancellations = (Dictionary<Vector2Int, System.Threading.CancellationTokenSource>)streamerType.GetField("_terrainHeightPrefetchCancellations", member).GetValue(streamer);
            var affected = new Vector2Int(0, 0); var unrelated = new Vector2Int(8, 8);
            var retainedTask = System.Threading.Tasks.Task.FromResult(new float[2, 2]);
            tasks[affected] = System.Threading.Tasks.Task.FromResult(new float[2, 2]); tasks[unrelated] = retainedTask;
            cancellations[affected] = new System.Threading.CancellationTokenSource();
            using (var retainedCancellation = new System.Threading.CancellationTokenSource())
            {
                cancellations[unrelated] = retainedCancellation;
                streamerType.GetMethod("RetireContinuationPrefetches", member).Invoke(streamer, new[] { refresh });
                check("append refresh retires already-completed stale height results", !tasks.ContainsKey(affected) && !cancellations.ContainsKey(affected));
                check("append refresh preserves unrelated sampler and cancellation identities", ReferenceEquals(tasks[unrelated], retainedTask) &&
                    ReferenceEquals(cancellations[unrelated], retainedCancellation) && !retainedCancellation.IsCancellationRequested);
                tasks.Clear(); cancellations.Clear();
            }
            bool disposed = false;
            System.Collections.IEnumerator OwnedIterator()
            { try { yield return null; } finally { disposed = true; } }
            var iterator = OwnedIterator(); iterator.MoveNext();
            var work = (Stack<System.Collections.IEnumerator>)streamerType.GetField("_frontierConstructionWork", member).GetValue(streamer);
            work.Push(iterator);
            streamerType.GetMethod("CancelFrontierConstructionWork", member).Invoke(streamer, null);
            check("frontier cancellation disposes owned nested work", disposed && work.Count == 0);
            check("disabled owner cannot retain a frontier construction admission", !(bool)streamerType.GetMethod("IsFrontierConstructionWorkCurrent", member).Invoke(streamer, null));
            disposed = false;
            var parent = OwnedIterator(); parent.MoveNext();
            System.Collections.IEnumerator FailingCleanup()
            { try { yield return null; } finally { throw new InvalidOperationException("detached child cleanup"); } }
            var child = FailingCleanup(); child.MoveNext(); work.Push(parent); work.Push(child);
            var cleanupFailure = (AggregateException)streamerType.GetMethod("DisposeOwnedContinuationIterators", statics).Invoke(null, new object[] { work });
            check("throwing child cleanup still disposes the owned parent", disposed && work.Count == 0);
            check("cleanup retains the original failure for reporting", cleanupFailure?.InnerExceptions.Count == 1 &&
                cleanupFailure.InnerExceptions[0].Message == "detached child cleanup");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (originData != null) UnityEngine.Object.DestroyImmediate(originData);
        }
        var preserve = typeof(WorldStateManager).GetMethod("TryPreserveContinuationLocationReferences", statics);
        var publicationSettings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings", statics).GetValue(null);
        string PublicationJson(object value) => Newtonsoft.Json.JsonConvert.SerializeObject(value, publicationSettings);
        GeneratedSpatialContinuationV2Record ClonePublication(GeneratedSpatialContinuationV2Record value) =>
            Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(PublicationJson(value), publicationSettings);
        var oldLocation = new GeneratedSpatialContinuationLocationV2Record { contentId = "detached-retained-location", deterministicSeed = "detached-seed" };
        var previous = new GeneratedSpatialContinuationV2Record { locations = new List<GeneratedSpatialContinuationLocationV2Record> { oldLocation } };
        var next = ClonePublication(previous);
        next.locations.Add(new GeneratedSpatialContinuationLocationV2Record { contentId = "detached-appended-location" });
        string body = PublicationJson(next);
        check("paired append keeps actual prior location object for active providers", (bool)preserve.Invoke(null, new object[] { previous, next, null }) &&
            ReferenceEquals(next.locations[0], oldLocation) && next.locations.Count == 2 && PublicationJson(next) == body);
        var changed = ClonePublication(previous);
        changed.locations[0].deterministicSeed = "changed";
        check("paired append rejects changed accepted payload before sharing references", !(bool)preserve.Invoke(null, new object[] { previous, changed, null }) &&
            !ReferenceEquals(changed.locations[0], oldLocation));
        check("paired append rejects removed accepted locations", !(bool)preserve.Invoke(null, new object[] { previous, new GeneratedSpatialContinuationV2Record(), null }));
        var stage = typeof(YQSpatialMaterializationResolverV2).GetMethod("TryStageContinuationInstallation", statics);
        check("opaque installation cannot stage without canonical world and construction token", !(bool)stage.Invoke(null, new object[] { null, null, null, null }));
        var install = typeof(YQSpatialMaterializationResolverV2).GetMethod("TryInstallPublishedContinuation", statics);
        check("opaque installation cannot install an absent staged token", !(bool)install.Invoke(null, new object[] { null, null, null }));
    }

    private static void RunLandmarkInteractionPrecedenceContracts(Action<string, bool> check)
    {
        // note: Inactive real adapters test only the interaction-owner predicate; no interaction, reward generation, physics or profile operation runs.
        var method = typeof(YQInvestorCombat).GetMethod("HasSpecificLandmarkInteraction",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var root = new GameObject("DetachedLandmarkInteractionOwners") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        bool Specific(Collider collider, Transform landmark) => (bool)method.Invoke(null, new object[] { collider, landmark });
        try
        {
            var ancestorCollider = root.AddComponent<BoxCollider>();
            foreach (Type adapter in new[] { typeof(YQLockpickableDoor), typeof(YQLockpickableLoot), typeof(YQWorldContainer) })
            {
                var landmark = new GameObject("DetachedLandmark_" + adapter.Name);
                landmark.SetActive(false); landmark.transform.SetParent(root.transform, false);
                landmark.AddComponent<YQGeneratedLandmarkResource>();
                var target = new GameObject("DetachedSpecific_" + adapter.Name);
                target.SetActive(false); target.transform.SetParent(landmark.transform, false);
                var specificOwner = target.AddComponent(adapter);
                var targetCollider = target.AddComponent<BoxCollider>();
                check(adapter.Name + " descendant interaction takes precedence over broad landmark", !target.activeInHierarchy &&
                    targetCollider.GetComponentInParent(adapter, true) == specificOwner && Specific(targetCollider, landmark.transform));
                var sameOwner = landmark.AddComponent(adapter);
                var sameCollider = landmark.AddComponent<BoxCollider>();
                check(adapter.Name + " on the landmark root does not suppress its generic interaction", sameCollider.GetComponentInParent(adapter, true) == sameOwner &&
                    !Specific(sameCollider, landmark.transform));
                var ancestorOwner = root.AddComponent(adapter);
                check(adapter.Name + " ancestor adapter cannot claim a descendant landmark interaction", ancestorCollider.GetComponentInParent(adapter, true) == ancestorOwner &&
                    !Specific(ancestorCollider, landmark.transform));
                var unrelated = new GameObject("DetachedUnrelatedLandmark_" + adapter.Name);
                unrelated.SetActive(false); unrelated.transform.SetParent(root.transform, false);
                unrelated.AddComponent<YQGeneratedLandmarkResource>();
                check(adapter.Name + " adapter cannot suppress an unrelated landmark owner", !Specific(targetCollider, unrelated.transform));
            }
            check("landmark predicate fixtures remain inactive", !root.activeInHierarchy && root.GetComponentsInChildren<Transform>(true).All(value => !value.gameObject.activeSelf));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void RunFrontierCandidateAndGraphStagingContracts(Action<string, bool> check)
    {
        // note: A complete compiler-produced detached parent exercises the actual candidate boundary; none of these unaccepted sites enters a profile or provider.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var candidateJsonSettings = (Newtonsoft.Json.JsonSerializerSettings)typeof(WorldStateManager)
            .GetField("JsonSettings", flags).GetValue(null);
        T CloneCandidate<T>(T value) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(
            Newtonsoft.Json.JsonConvert.SerializeObject(value, candidateJsonSettings), candidateJsonSettings);
        var saved = new List<KeyValuePair<System.Reflection.FieldInfo, object>>();
        void Save(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                var field = type.GetField(name, flags);
                saved.Add(new KeyValuePair<System.Reflection.FieldInfo, object>(field, field.GetValue(null)));
            }
        }
        Save(typeof(YQWorldGenerationArchitecture), "_runtimeAuthorityPlan", "_runtimeAuthority", "_runtimeAuthorityArtifact", "_runtimeAuthorityHash");
        Save(typeof(YQSpatialMaterializationResolverV2), "cachedPlan", "cachedArtifact", "cachedHash", "cachedMemberFootprintHash", "cachedPrepared", "cachedContinuation", "cachedContinuationFingerprint");
        Save(typeof(YQSemanticWorldAuthority), "s_runtimeCellCoreCacheAuthority");
        var cells = (Dictionary<long, GeneratedSemanticCellPlanRecord>)typeof(YQSemanticWorldAuthority).GetField("s_runtimeCellCoreCache", flags).GetValue(null);
        var order = (Queue<KeyValuePair<long, GeneratedSemanticCellPlanRecord>>)typeof(YQSemanticWorldAuthority).GetField("s_runtimeCellCoreCacheOrder", flags).GetValue(null);
        var savedCells = new Dictionary<long, GeneratedSemanticCellPlanRecord>(cells); var savedOrder = order.ToArray();
        try
        {
            var plan = BuildFixture("semantic-v2-318");
            foreach (var region in plan.regions) region.assetStyleKey = "viking_rural";
            YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
            if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var parent, out string failure)) throw new InvalidOperationException(failure);
            plan.spatialPlanV2 = parent;
            YQWorldGenerationArchitecture.LockRuntimeAuthority(plan, YQSpatialPlanAuthority.AcceptedV2);
            if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out failure) ||
                !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out failure)) throw new InvalidOperationException(failure);
            var authority = YQSemanticWorldAuthority.Ensure(plan);
            string baseHash = parent.contentHash, memberHash = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(parent);
            string parentBody = Newtonsoft.Json.JsonConvert.SerializeObject(parent);
            var candidateMethod = typeof(YQSemanticWorldAuthority).GetMethod("TryBuildFrontierLocationCandidate", flags);
            var candidateFailures = new SortedSet<string>(StringComparer.Ordinal);
            bool Candidate(Vector2Int block, string regionId, out GeneratedSpatialContinuationLocationV2Record candidate)
            {
                object[] args = { plan, block.x, block.y, regionId, null, null };
                bool result = (bool)candidateMethod.Invoke(null, args); candidate = (GeneratedSpatialContinuationLocationV2Record)args[4];
                if (!result) candidateFailures.Add(args[5] as string ?? "no rejection reason");
                return result;
            }
            var raw = typeof(YQSemanticWorldAuthority).GetMethod("TryBuildContinuationOpportunity", flags);
            var chosen = new List<KeyValuePair<Vector2Int, GeneratedSpatialContinuationLocationV2Record>>();
            var ordinaryKinds = new HashSet<string>(StringComparer.Ordinal);
            bool multiple = false;
            for (int z = -32; z <= 31 && !(ordinaryKinds.Count == 3 && multiple); z++)
            for (int x = -32; x <= 31 && !(ordinaryKinds.Count == 3 && multiple); x++)
            {
                // note: Cheap raw presence only shortlists inputs; every retained result passes the full public accepted-parent/suppression/region boundary.
                object[] rawArgs = { plan.worldSeed, x, z, null, (uint)0 };
                if (!(bool)raw.Invoke(null, rawArgs)) continue;
                var opportunity = (GeneratedSemanticSiteReservationRecord)rawArgs[3];
                bool isMultiple = opportunity.structuralIntent == YQSemanticWorldAuthority.FrontierOpportunityVersion + "_large_settlement";
                if (isMultiple ? multiple : ordinaryKinds.Contains(opportunity.siteKind)) continue;
                var block = new Vector2Int(x, z);
                // note: Production chooses the nearest accepted region before physical recovery; the fixture must use that same region owner.
                string nearestRegion = plan.regions.Where(region => prepared.TryGetRegion(region.regionId, out _)).OrderBy(region => {
                    prepared.TryGetRegion(region.regionId, out var physical);
                    return (physical.centerX - opportunity.worldX) * (physical.centerX - opportunity.worldX) +
                        (physical.centerZ - opportunity.worldZ) * (physical.centerZ - opportunity.worldZ);
                }).ThenBy(region => region.regionId, StringComparer.Ordinal).First().regionId;
                if (!Candidate(block, nearestRegion, out var candidate)) continue;
                chosen.Add(new KeyValuePair<Vector2Int, GeneratedSpatialContinuationLocationV2Record>(block, candidate));
                if (isMultiple) multiple = true; else ordinaryKinds.Add(opportunity.siteKind);
            }
            foreach (string kind in new[] { "settlement", "hostile_site", "landmark" })
                check("complete accepted parent produces an ordinary untouched " + kind + " candidate", ordinaryKinds.Contains(kind));
            check("complete accepted parent produces a rare multi-cell candidate", multiple);
            check("versioned frontier shortlisting reaches beyond the finite origin continent envelope", chosen.Any(pair =>
                Mathf.Max(Mathf.Abs(pair.Value.anchor.x), Mathf.Abs(pair.Value.anchor.z)) > 4000f) &&
                chosen.All(pair => pair.Value.deterministicSeed.Contains(YQSemanticWorldAuthority.FrontierOpportunityVersion)));
            if (ordinaryKinds.Count != 3 || !multiple)
                check("bounded candidate scan rejection reasons: " + string.Join(" | ", candidateFailures), false);
            var untouched = typeof(YQWorldGenerationService).GetMethod("IsUntouchedFrontierLocationCandidate", flags);
            var offsetCandidate = typeof(YQSemanticWorldAuthority).GetMethod("TryOffsetFrontierCandidateWithinOpportunityBlock", flags);
            float maximumRecoveryDistance = (float)typeof(YQSemanticWorldAuthority)
                .GetField("FrontierPhysicalRecoveryDistance", flags).GetRawConstantValue();
            var collarDistanceMethod = typeof(YQContinuousWorldCellAuthority).GetMethod("TryGetFrontierOpeningCollarRecoveryDistance", flags);
            var checkSpacing = typeof(YQSemanticWorldAuthority).GetMethod("IsFrontierCandidateWithinLargeSettlementSpacing", flags);
            var smallCollarCandidate = new GeneratedSpatialContinuationLocationV2Record
            {
                anchor = new YQSiteAnchorV2 { kind = YQSiteKindV2.PointOfInterest, x = 704f, z = 704f,
                    reservedRadius = 36f, memberFootprint = new List<YQSiteMemberFootprintV2>() }
            };
            object[] smallCollarArgs = { smallCollarCandidate, Vector2.right, 0f };
            bool smallCollarClearance = (bool)collarDistanceMethod.Invoke(null, smallCollarArgs);
            float smallClearance = (float)smallCollarArgs[2];
            check("single-sector collar recovery clears the exact protected radius", smallCollarClearance &&
                Mathf.Abs(704f + smallClearance - 1213f) < .01f && 704f + smallClearance - 36f - 24f > 1152f);
            var largeCollarCandidate = new GeneratedSpatialContinuationLocationV2Record
            {
                anchor = new YQSiteAnchorV2 { kind = YQSiteKindV2.Settlement, x = 704f, z = 704f,
                    reservedRadius = 64f, memberFootprint = new List<YQSiteMemberFootprintV2>
                    {
                        new YQSiteMemberFootprintV2 { x = 576f, z = 704f, reservedRadius = 64f },
                        new YQSiteMemberFootprintV2 { x = 832f, z = 704f, reservedRadius = 64f }
                    } }
            };
            object[] largeCollarArgs = { largeCollarCandidate, Vector2.right, 0f };
            bool largeCollarClearance = (bool)collarDistanceMethod.Invoke(null, largeCollarArgs);
            float largeClearance = (float)largeCollarArgs[2];
            check("multi-sector settlement collar recovery clears every reserved sector", largeCollarClearance &&
                Mathf.Abs(704f + largeClearance - 1369f) < .01f &&
                largeCollarCandidate.anchor.memberFootprint.All(member =>
                    Mathf.Abs(member.x + largeClearance) - member.reservedRadius - 24f > 1152f));
            foreach (var pair in chosen)
            {
                var candidate = pair.Value;
                string label = (candidate.anchor.memberFootprint.Count == 0 ? "ordinary " : "multi-cell ") +
                    candidate.anchor.kind + " block(" + pair.Key.x + "," + pair.Key.y + ")";
                check(label + " candidate is untouched at the real brief boundary", (bool)untouched.Invoke(null, new object[] { candidate }) &&
                    candidate.source == YQSpatialContinuationSourceV2.None && candidate.revision == 0);
                check(label + " candidate records its canonical styled region and full versioned seed", plan.regions.Any(region => region.regionId == candidate.anchor.parentRegionId && !string.IsNullOrWhiteSpace(region.assetStyleKey)) &&
                    candidate.deterministicSeed.Contains("frontier_candidate_reserves_v2") && candidate.contentId != candidate.anchor.siteId &&
                    candidate.anchor.sourceSemanticId != candidate.anchor.siteId);
                bool uniqueFrontages = candidate.entrances.Count == candidate.anchor.memberFootprint.Count + 1;
                foreach (var entry in candidate.entrances)
                {
                    int matches = Vector2.Distance(new Vector2(entry.worldX, entry.worldZ), new Vector2(candidate.anchor.x, candidate.anchor.z)) <= candidate.anchor.reservedRadius + .01f ? 1 : 0;
                    foreach (var member in candidate.anchor.memberFootprint)
                        if (Vector2.Distance(new Vector2(entry.worldX, entry.worldZ), new Vector2(member.x, member.z)) <= member.reservedRadius + .01f) matches++;
                    uniqueFrontages &= matches == 1 && string.IsNullOrEmpty(entry.permittedRouteId);
                }
                check(label + " candidate has one unique engine-owned reserve frontage per sector", uniqueFrontages);
                bool repeat = Candidate(pair.Key, candidate.anchor.parentRegionId.ToUpperInvariant(), out var replay);
                check(label + " candidate canonical region spelling and repeat query preserve the body", repeat &&
                    JsonUtility.ToJson(replay) == JsonUtility.ToJson(candidate));
                var moved = CloneCandidate(candidate);
                float offset = Mathf.Min(16f, moved.anchor.terrainSearchRadius * .5f);
                float originalX = moved.anchor.x, originalZ = moved.anchor.z;
                var originalMembers = moved.anchor.memberFootprint.Select(member => new Vector2(member.x, member.z)).ToArray();
                var originalEntrances = moved.entrances.Select(entry => new Vector2(entry.worldX, entry.worldZ)).ToArray();
                bool shifted = (bool)offsetCandidate.Invoke(null, new object[] { plan, prepared, moved, offset, -offset * .5f });
                bool rigidTranslation = shifted && Mathf.Abs(moved.anchor.x - originalX - offset) < .001f &&
                    Mathf.Abs(moved.anchor.z - originalZ + offset * .5f) < .001f && moved.blockX == candidate.blockX &&
                    moved.blockZ == candidate.blockZ && moved.contentId == candidate.contentId &&
                    moved.deterministicSeed == candidate.deterministicSeed;
                for (int memberIndex = 0; memberIndex < originalMembers.Length; memberIndex++)
                    rigidTranslation &= Mathf.Abs(moved.anchor.memberFootprint[memberIndex].x - originalMembers[memberIndex].x - offset) < .001f &&
                        Mathf.Abs(moved.anchor.memberFootprint[memberIndex].z - originalMembers[memberIndex].y + offset * .5f) < .001f;
                for (int entranceIndex = 0; entranceIndex < originalEntrances.Length; entranceIndex++)
                    rigidTranslation &= Mathf.Abs(moved.entrances[entranceIndex].worldX - originalEntrances[entranceIndex].x - offset) < .001f &&
                        Mathf.Abs(moved.entrances[entranceIndex].worldZ - originalEntrances[entranceIndex].y + offset * .5f) < .001f;
                check(label + " bounded physical retry translates its untouched geometry within the same opportunity block", rigidTranslation &&
                    (bool)checkSpacing.Invoke(null, new object[] { prepared, moved }));
                var rejectedMove = CloneCandidate(candidate);
                float rejectedX = rejectedMove.anchor.x, rejectedZ = rejectedMove.anchor.z;
                bool excessiveMoveRejected = !(bool)offsetCandidate.Invoke(null,
                    new object[] { plan, prepared, rejectedMove, maximumRecoveryDistance + 1f, 0f });
                check(label + " physical retry rejects out-of-envelope movement without mutating its staged candidate", excessiveMoveRejected &&
                    rejectedMove.anchor.x == rejectedX && rejectedMove.anchor.z == rejectedZ);
                var classify = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("FindAcceptedContinuationLocation", flags);
                var projected = new YQSpatialMaterializationSiteV2 { siteId = candidate.anchor.siteId, sourceSemanticId = candidate.anchor.sourceSemanticId };
                check(label + " staged candidate cannot enter saved replay preflight", classify.Invoke(null, new object[] { plan, projected }) == null);
            }
            if (chosen.Count > 0)
            {
                var block = chosen[0].Key; string style = plan.regions[0].assetStyleKey;
                check("candidate rejects a region absent from canonical data", !Candidate(block, "missing-region", out _));
                plan.regions[0].assetStyleKey = null;
                check("candidate rejects a canonical region with no approved style intent", !Candidate(block, plan.regions[0].regionId, out _));
                plan.regions[0].assetStyleKey = style;
                plan.regions.Add(new GeneratedRegionRecord { regionId = plan.regions[0].regionId, assetStyleKey = style });
                check("candidate rejects duplicate canonical region identity", !Candidate(block, plan.regions[0].regionId, out _));
                plan.regions.RemoveAt(plan.regions.Count - 1);
                check("candidate rejects signed block overflow", !Candidate(new Vector2Int(int.MaxValue, int.MinValue), plan.regions[0].regionId, out _));
            }
            check("candidate creation leaves accepted parent and member hashes unchanged", parentBody == Newtonsoft.Json.JsonConvert.SerializeObject(parent) &&
                parent.contentHash == baseHash && YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(parent) == memberHash && parent.acceptedContinuation == null);
            authority.featureOverlayRevision = 29;
            authority.featureOverlays.Add(new GeneratedSemanticFeatureOverlayRecord());
            var overlays = authority.featureOverlays; var overlay = overlays[0];
            var stage = typeof(YQSemanticWorldAuthority).GetMethod("TryPrepareContinuationAuthority", flags);
            object[] stageArgs = { plan, null, prepared, null, null };
            check("detached graph staging rejects an absent accepted extension", !(bool)stage.Invoke(null, stageArgs) && stageArgs[3] == null);
            stageArgs[1] = new GeneratedSpatialContinuationV2Record { worldSeed = plan.worldSeed,
                parentSpatialContentHash = baseHash, parentMemberFootprintHash = memberHash };
            check("detached graph staging rejects a staged envelope before derivation", !(bool)stage.Invoke(null, stageArgs) && stageArgs[3] == null);
            var invalid = (GeneratedSpatialContinuationV2Record)stageArgs[1]; invalid.state = YQSpatialContinuationStateV2.Accepted;
            invalid.contentHash = "wrong"; invalid.validatedContentHash = "wrong";
            check("detached graph staging rejects a copied invalid accepted checksum", !(bool)stage.Invoke(null, stageArgs) && stageArgs[3] == null);
            check("rejected graph staging preserves live accepted pointers, overlay records and revisions", ReferenceEquals(plan.semanticAuthority, authority) &&
                ReferenceEquals(authority.featureOverlays, overlays) && ReferenceEquals(overlays[0], overlay) && authority.featureOverlayRevision == 29 && parent.acceptedContinuation == null);
            check("rejected graph staging preserves the exact existing compiled cache owner", ReferenceEquals(
                typeof(YQSpatialMaterializationResolverV2).GetField("cachedPrepared", flags).GetValue(null), prepared));
        }
        finally
        {
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
            cells.Clear(); foreach (var pair in savedCells) cells.Add(pair.Key, pair.Value);
            order.Clear(); foreach (var pair in savedOrder) order.Enqueue(pair);
        }
    }

    private static void RunFrontierColdReplayBoundaryContracts(Action<string, bool> check)
    {
        // note: These detached cache/lookup checks cannot certify provider loading, accepted geography, population or saved-world gameplay.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var catalogField = typeof(YQCompiledWorldSiteBindingService).GetField("catalog", flags);
        var savedCatalog = catalogField.GetValue(null);
        var manifests = (Dictionary<string, YQReviewedSemanticSiteManifest>)typeof(YQCompiledWorldSiteInstance)
            .GetField("PreparedManifestCache", flags).GetValue(null);
        var savedManifests = new Dictionary<string, YQReviewedSemanticSiteManifest>(manifests);
        var warm = typeof(YQCompiledWorldSiteInstance).GetMethod("WarmAcceptedContinuationProvidersRoutine", flags);
        var lookup = typeof(YQCompiledWorldSiteBindingService).GetMethod("TryGetCachedContinuationReplayRecord", flags);
        YQRuntimeWorldSiteCatalog detachedCatalog = null;
        IEnumerator routine = null;
        try
        {
            int callbacks = 0; bool success = true; string reason = null;
            routine = (IEnumerator)warm.Invoke(null, new object[] { BuildFixture("stale-cold-replay"),
                (Func<bool>)(() => false), (Action<bool, string>)((passed, failure) => { callbacks++; success = passed; reason = failure; }) });
            check("cold replay rejects stale ownership before issuing any asset request", !routine.MoveNext() && callbacks == 1 && !success && !string.IsNullOrEmpty(reason));
            (routine as IDisposable)?.Dispose(); routine = null;
            check("cold replay completion callback stays single after iterator disposal", callbacks == 1);
            check("rejected cold replay leaves exact existing provider caches unchanged", ReferenceEquals(catalogField.GetValue(null), savedCatalog) &&
                manifests.Count == savedManifests.Count && savedManifests.All(pair => manifests.TryGetValue(pair.Key, out var value) && ReferenceEquals(value, pair.Value)));

            var canonical = Resources.Load<YQRuntimeWorldSiteCatalog>("YQRuntimeWorldSiteCatalog");
            var approved = canonical != null ? canonical.FindByKitId("qualified_viking_home") : null;
            check("cold lookup fixture uses an actual qualified catalog record", approved != null && approved.spatiallyValidated && approved.seamlessPlacementEligible);
            if (approved == null) return;
            string approvedBody = JsonUtility.ToJson(approved);
            var location = new GeneratedSpatialContinuationLocationV2Record { settlement = new GeneratedSettlementRecord {
                runtimeSiteKitId = approved.kitId, runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion } };
            bool Lookup(out object record)
            {
                object[] args = { location, null, null };
                bool passed = (bool)lookup.Invoke(null, args); record = args[1]; return passed;
            }
            catalogField.SetValue(null, null);
            check("cold lookup cannot select a kit before the canonical catalog is loaded", !Lookup(out _));
            catalogField.SetValue(null, canonical);
            check("cold lookup returns the exact saved approved record", Lookup(out var found) && ReferenceEquals(found, approved));
            location.settlement.runtimeSiteBindingVersion = "reviewed-site-binding-4-semantic-slices";
            check("cold lookup preserves accepted version-four bindings", Lookup(out found) && ReferenceEquals(found, approved));
            location.settlement.runtimeSiteBindingVersion = "unsupported-binding";
            check("cold lookup rejects unsupported saved binding versions", !Lookup(out _));
            location.settlement.runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion;
            location.settlement.runtimeSiteKitId = "missing-saved-kit";
            check("cold lookup rejects missing saved kits without choosing another", !Lookup(out _));
            location.settlement.runtimeSiteKitId = approved.kitId;
            detachedCatalog = ScriptableObject.CreateInstance<YQRuntimeWorldSiteCatalog>();
            detachedCatalog.hideFlags = HideFlags.HideAndDontSave;
            detachedCatalog.Configure(new[] { approved, approved }); catalogField.SetValue(null, detachedCatalog);
            check("cold lookup rejects duplicate saved-kit catalog identity", !Lookup(out _));
            var rejected = JsonUtility.FromJson<YQRuntimeWorldSiteRecord>(approvedBody);
            rejected.runtimeManifestResourceKey = string.Empty; detachedCatalog.Configure(new[] { rejected });
            check("cold lookup rejects missing approved manifest resource keys", !Lookup(out _));
            rejected.runtimeManifestResourceKey = approved.runtimeManifestResourceKey; rejected.seamlessPlacementEligible = false;
            check("cold lookup rejects kits withdrawn from seamless placement", !Lookup(out _));
            check("cold lookup leaves the actual approved record unchanged", JsonUtility.ToJson(approved) == approvedBody);
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
            catalogField.SetValue(null, savedCatalog);
            manifests.Clear(); foreach (var pair in savedManifests) manifests.Add(pair.Key, pair.Value);
            if (detachedCatalog != null) UnityEngine.Object.DestroyImmediate(detachedCatalog);
        }
    }

    private static int RunSiteSectorLayoutProbe()
    {
        // note: Exercise the production numeric solver and coverage helper without loading a profile, configuring a streamer or instantiating gameplay content.
        var checks = new List<object>(); int failures = 0;
        var reviewedAssemblyCoverage = new List<object>();
        void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) failures++; }
        var worldBefore = WorldStateManager.Instance;
        var playerBefore = PlayerStateManager.Instance;
        var pendingField = typeof(YQProceduralSettlementLayout).GetField("pending", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var pending = (Dictionary<string, YQProceduralSettlementLayoutRecord>)pendingField.GetValue(null);
        var pendingBefore = new Dictionary<string, YQProceduralSettlementLayoutRecord>(pending);
        try
        {
            YQSpatialMaterializationSiteV2 Site(float heading = 0f)
            {
                object value = new YQSpatialMaterializationSiteV2 { siteId = "detached_owner", x = 0f, z = 0f, headingDegrees = heading, reservedRadius = 72f };
                typeof(YQSpatialMaterializationSiteV2).GetField("memberFootprint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(value, new[] { new YQSiteMemberFootprintV2 { memberId = "east", x = 384f, z = 0f, reservedRadius = 32f, sectorIndex = 1 },
                        new YQSiteMemberFootprintV2 { memberId = "west", x = -384f, z = 0f, reservedRadius = 32f, sectorIndex = 2 } });
                return (YQSpatialMaterializationSiteV2)value;
            }
            var cells = new List<YQProceduralSettlementLayout.Cell>();
            foreach (string id in new[] { "assembly_a", "assembly_b", "assembly_c" })
                cells.Add(new YQProceduralSettlementLayout.Cell { id = id, center = new Vector3(0f, 3f, 0f), size = new Vector3(12f, 6f, 10f),
                    entrance = new Vector3(0f, 0f, 5f), outward = Vector3.forward, datum = 0f });
            var site = Site(); string seed = YQProceduralSettlementLayout.BuildSectorSeed("fixture-sector-owner", site);
            bool built = YQProceduralSettlementLayout.TryBuildSectors(cells, seed, site, out var layout, out string failure);
            Check("large accepted owner receives real selected placements", built);
            if (!built) throw new InvalidOperationException(failure);
            Check("one unique selected assembly per sector without duplication", layout.cells.Count == 3 && layout.sectors.Count == 3 &&
                layout.cells.Select(c => c.cellId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3 &&
                layout.cells.Select(c => c.sectorId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3);
            Check("large aggregate retains bounded physical assemblies", layout.radius > 210f && layout.cells.All(c => c.boundsSize.x <= 12.001f && c.boundsSize.z <= 12.001f));
            Check("derived record validates", YQProceduralSettlementLayout.ValidateRecord(layout, out _));
            // note: The production builder must admit the exact member reserves rather than comparing the distant aggregate to the central radius.
            site.sourceSemanticId = "detached-sector-semantic";
            var preparedConstructor = typeof(YQPreparedSpatialMaterializationV2).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single();
            var sectorPrepared = (YQPreparedSpatialMaterializationV2)preparedConstructor.Invoke(new object[] {
                Array.Empty<YQSpatialMaterializationRegionV2>(), new[] { site }, Array.Empty<YQSpatialMaterializationWaterV2>(),
                Array.Empty<YQSpatialMaterializationWaterPointV2>(), Array.Empty<YQSpatialMaterializationRouteV2>(), Array.Empty<YQSpatialMaterializationRoutePointV2>(),
                Array.Empty<YQSpatialMaterializationCrossingV2>(), new Dictionary<string, int>(),
                new Dictionary<string, int> { [site.sourceSemanticId] = 0 }, new Dictionary<string, int> { [site.siteId] = 0 }, null, "", 0L, null });
            pending[seed] = layout;
            var footprintGate = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("TryValidatePreparedCompositionFootprint",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            object[] footprintArgs = { sectorPrepared, site.sourceSemanticId, seed, layout.radius, null };
            Check("builder preflight admits an aggregate spanning exact member reserves", (bool)footprintGate.Invoke(null, footprintArgs));
            float sectorRadius = layout.sectors[1].radius;
            layout.sectors[1].radius += 1f;
            Check("builder preflight rejects changed physical member reserves", !(bool)footprintGate.Invoke(null, footprintArgs));
            layout.sectors[1].radius = sectorRadius;
            string geometry = YQProceduralSettlementLayout.GeometrySignature(layout);
            cells.Reverse();
            Check("selection enumeration does not alter geometry", YQProceduralSettlementLayout.TryBuildSectors(cells, seed, site, out var reversed, out _) &&
                geometry == YQProceduralSettlementLayout.GeometrySignature(reversed));
            object boxed = site;
            var memberField = typeof(YQSpatialMaterializationSiteV2).GetField("memberFootprint", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            memberField.SetValue(boxed, site.MemberFootprint.Reverse().ToArray()); var reordered = (YQSpatialMaterializationSiteV2)boxed;
            Check("member enumeration does not alter identity or geometry", seed == YQProceduralSettlementLayout.BuildSectorSeed("fixture-sector-owner", reordered) &&
                YQProceduralSettlementLayout.TryBuildSectors(cells, seed, reordered, out var memberReversed, out _) &&
                geometry == YQProceduralSettlementLayout.GeometrySignature(memberReversed));
            Check("too few selected assemblies reject every-member omission", !YQProceduralSettlementLayout.TryBuildSectors(cells.Take(2).ToArray(), seed, site, out _, out _));
            var tooMany = new List<YQProceduralSettlementLayout.Cell>(cells);
            for (int i = 3; i < 9; i++) tooMany.Add(new YQProceduralSettlementLayout.Cell { id = "extra_" + i });
            Check("existing eight-cell payload limit remains", !YQProceduralSettlementLayout.TryBuildSectors(tooMany, seed, site, out _, out _));
            var duplicate = new List<YQProceduralSettlementLayout.Cell>(cells) { cells[0] };
            Check("duplicate source identity rejected", !YQProceduralSettlementLayout.TryBuildSectors(duplicate, seed, site, out _, out _));
            var tiny = Site(); tiny.MemberFootprint[0].reservedRadius = 2f;
            Check("unfitted member is rejected with its identity", !YQProceduralSettlementLayout.TryBuildSectors(cells, seed, tiny, out _, out string tinyFailure) && tinyFailure.Contains("east"));
            var rotated = Site(90f);
            Check("accepted heading resolves world-aligned member positions", YQProceduralSettlementLayout.TryBuildSectors(cells,
                YQProceduralSettlementLayout.BuildSectorSeed("fixture-sector-owner", rotated), rotated, out var turned, out _) &&
                turned.sectors.Any(s => s.id == "east" && Math.Abs(s.center.x) < .001f && Math.Abs(s.center.z - 384f) < .001f));
            layout.cells[0].sectorId = "missing";
            Check("persisted orphan sector placement rejected", !YQProceduralSettlementLayout.ValidateRecord(layout, out _));
            layout.cells[0].sectorId = "east";
            var coverage = typeof(YQCompiledWorldSiteInstance).GetMethod("BuildSectorCoveragePoints", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var boxes = new[] { new Bounds(new Vector3(-384f, 0f, 0f), new Vector3(12f, 6f, 10f)),
                new Bounds(new Vector3(384f, 0f, 0f), new Vector3(12f, 6f, 10f)) };
            var points = (Vector3[])coverage.Invoke(null, new object[] { boxes });
            Check("terrain coverage is physical union without empty middle demand", points.Length == 8 && points.All(p => Math.Abs(p.x) > 370f));
            Check("nonfinite actual bounds reject", coverage.Invoke(null, new object[] { new[] { new Bounds(new Vector3(float.NaN, 0f, 0f), Vector3.one) } }) == null);
            Check("legacy bounded grammar remains version four", YQProceduralSettlementLayout.TryBuild(cells, YQProceduralSettlementLayout.SeedPrefix + "legacy-fixture",
                out var old, out _) && old.version == 4 && old.sectors.Count == 0 && old.radius <= 210f);
            // note: Replay must select approved source keys before the loader expands persistent sector instance keys; earlier versions retain their exact cell IDs.
            var committedSources = typeof(YQCompiledWorldSiteInstance).GetMethod("BuildCommittedSourceIds", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Check("legacy committed source selection retains exact cell IDs",
                ((HashSet<string>)committedSources.Invoke(null, new object[] { old })).SetEquals(old.cells.Select(c => c.cellId)));
            Check("version-five committed distinct source selection remains unchanged",
                ((HashSet<string>)committedSources.Invoke(null, new object[] { layout })).SetEquals(layout.cells.Select(c => c.cellId)));
            Check("version-five physical sectors match immutable accepted reservations",
                YQProceduralSettlementLayout.TryValidateAcceptedSectorFootprint(layout, site, out _));
            var alteredReserve = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(layout));
            alteredReserve.sectors[0].radius -= 1f;
            Check("copied accepted signature cannot authorize a changed central reserve",
                YQProceduralSettlementLayout.ValidateRecord(alteredReserve, out _) &&
                !YQProceduralSettlementLayout.TryValidateAcceptedSectorFootprint(alteredReserve, site, out _));
            bool broadBuilt = true, broadOrderStable = true;
            for (int index = 0; index < 64; index++)
            {
                var sample = Site(index % 8 * 45f);
                float dx = index % 2 == 0 ? -2048f : 2048f, dz = index % 3 == 0 ? -1536f : 1536f;
                sample.x += dx; sample.z += dz;
                foreach (var member in sample.MemberFootprint) { member.x += dx; member.z += dz; }
                string sampleSeed = YQProceduralSettlementLayout.BuildSectorSeed("sector-sample-" + index, sample);
                bool valid = YQProceduralSettlementLayout.TryBuildSectors(cells, sampleSeed, sample, out var candidate, out _);
                broadBuilt &= valid;
                if (valid)
                {
                    string signature = YQProceduralSettlementLayout.GeometrySignature(candidate);
                    broadOrderStable &= YQProceduralSettlementLayout.TryBuildSectors(cells.AsEnumerable().Reverse().ToArray(), sampleSeed, sample,
                        out var opposite, out _) && signature == YQProceduralSettlementLayout.GeometrySignature(opposite);
                }
            }
            Check("64 signed-coordinate and heading cases produce fitted sectors", broadBuilt);
            Check("64 opposite selection orders reproduce sector geometry", broadOrderStable);
            foreach (string kit in new[] { "qualified_viking_home", "qualified_occupied_homestead" })
            {
                string path = "Assets/Assets/Resources/YQWorldSites/" + kit + "/YQRuntimeSemanticSite.asset";
                var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(path);
                var zones = new List<YQReviewedSemanticZoneRecord>(); string sourceFailure = string.Empty;
                bool usable = manifest != null && manifest.ReleaseEligible;
                if (usable && manifest.StreamingSite != null)
                    usable = YQProceduralSettlementLayout.TryGetIndependentStreamingZones(manifest, out zones, out sourceFailure);
                else if (usable)
                {
                    // note: Reviewed authored zones already are complete rigid assemblies; the streaming adapter is inapplicable to these existing qualified assets.
                    zones.AddRange(manifest.Zones.Where(z => z != null && z.prefab != null && YQProceduralSettlementLayout.HasUsableExternalConnection(z)));
                    usable = zones.Count > 0;
                }
                reviewedAssemblyCoverage.Add(new { kit, path, usable, completeSourceAssemblies = zones.Count,
                    sectorEligibleDistinctAssemblies = YQProceduralSettlementLayout.CountReviewedSectorAssemblies(manifest),
                    topology = manifest?.StreamingSite != null ? "streamed_cells" : "authored_zones",
                    threeSectorDistinctAssemblyCapacity = usable && zones.Count >= 3, limitation = sourceFailure });
                Check("existing approved independent manifest remains usable: " + kit, usable);
                Check("sector capability preserves the actual single-source limit: " + kit,
                    YQProceduralSettlementLayout.CountReviewedSectorAssemblies(manifest) == 1);
                var realSite = Site(); string reusableSeed = YQProceduralSettlementLayout.BuildReusableSectorSeed("fixture-approved-" + kit, realSite);
                var selected = new HashSet<string>(zones.Select(z => z.stableId), StringComparer.OrdinalIgnoreCase);
                bool resolved = YQProceduralSettlementLayout.TryResolve(manifest, selected, reusableSeed, out var reusable, out string reusableFailure, realSite);
                Check("approved source produces owner plus two reusable sectors: " + kit, resolved && reusable.version == 6 && reusable.cells.Count == 3);
                if (!resolved) throw new InvalidOperationException(kit + ": " + reusableFailure);
                Check("every reusable placement retains its approved source: " + kit,
                    reusable.cells.All(c => selected.Contains(c.sourceCellId)) && reusable.cells.Select(c => c.cellId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3);
                Check("reused sources consume all physical instance costs: " + kit,
                    YQCompiledWorldSiteInstance.TryMeasureSectorPayload(manifest, selected, reusable, out int cost, out _) && cost == zones[0].sourceInstanceCount * 3);
                int originalSourceCost = zones[0].sourceInstanceCount;
                var costFixture = UnityEngine.Object.Instantiate(manifest);
                costFixture.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    if (ReferenceEquals(costFixture.Zones[0], zones[0])) throw new InvalidOperationException("Cost fixture did not isolate serialized zone data.");
                    costFixture.Zones[0].sourceInstanceCount = 400;
                    Check("reused hierarchy cost cannot bypass 1100-instance budget: " + kit,
                        !YQCompiledWorldSiteInstance.TryMeasureSectorPayload(costFixture, selected, reusable, out _, out _));
                    costFixture.Zones[0].sourceInstanceCount = 0;
                    Check("unknown reusable source cost is not normalized into admission: " + kit,
                        !YQCompiledWorldSiteInstance.TryMeasureSectorPayload(costFixture, selected, reusable, out _, out _));
                }
                finally { UnityEngine.Object.DestroyImmediate(costFixture); }
                Check("reused door and storage identities remain distinct: " + kit,
                    reusable.cells.Select(c => YQCellDoorBindingsV2.BuildDoorId(realSite.siteId, c.cellId, "fixture-door")).Distinct().Count() == 3 &&
                    reusable.cells.Select(c => YQCellLootBindingsV2.BuildLootId(realSite.siteId, c.cellId, "fixture-storage")).Distinct().Count() == 3);
                Check("central source identity is retained: " + kit,
                    reusable.cells.Single(c => c.sectorId == realSite.siteId).cellId == zones[0].stableId);
                Check("loader expands approved source into exactly three stable placements: " + kit,
                    YQProceduralSettlementLayout.InstanceCellIds(reusable, zones[0].stableId).Count == 3 &&
                    reusable.cells.All(c => YQProceduralSettlementLayout.SourceCellId(reusable, c.cellId) == zones[0].stableId));
                var serializedReplay = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(reusable));
                var restoredSources = (HashSet<string>)committedSources.Invoke(null, new object[] { serializedReplay });
                Check("serialized reusable replay selects source IDs rather than instance aliases: " + kit,
                    restoredSources.Count == 1 && restoredSources.SetEquals(selected));
                Check("serialized reusable replay retains all instance IDs and geometry: " + kit,
                    YQProceduralSettlementLayout.ValidateRecord(serializedReplay, out _) &&
                    YQProceduralSettlementLayout.GeometrySignature(serializedReplay) == YQProceduralSettlementLayout.GeometrySignature(reusable) &&
                    restoredSources.SelectMany(id => YQProceduralSettlementLayout.InstanceCellIds(serializedReplay, id)).OrderBy(id => id, StringComparer.Ordinal)
                        .SequenceEqual(reusable.cells.Select(c => c.cellId).OrderBy(id => id, StringComparer.Ordinal)));
                Check("serialized reusable physical sectors match immutable accepted reservations: " + kit,
                    YQProceduralSettlementLayout.TryValidateAcceptedSectorFootprint(serializedReplay, realSite, out _));
                string reusableGeometry = YQProceduralSettlementLayout.GeometrySignature(reusable);
                Check("reusable footprint order reproduces geometry: " + kit,
                    YQProceduralSettlementLayout.TryResolve(manifest, selected, reusableSeed, out var repeated, out _, reordered) &&
                    reusableGeometry == YQProceduralSettlementLayout.GeometrySignature(repeated));
                GameObject shadow = new GameObject("DetachedReusableSectorClones") { hideFlags = HideFlags.HideAndDontSave };
                shadow.SetActive(false);
                try
                {
                    bool placed = true;
                    foreach (var placement in reusable.cells)
                    {
                        GameObject clone = UnityEngine.Object.Instantiate(zones[0].prefab, shadow.transform);
                        clone.name = "CompiledZone__" + placement.cellId;
                        placed &= YQProceduralSettlementLayout.ApplyPlacement(reusable, placement.cellId, clone.transform, reusable.origin) &&
                            clone.GetComponentsInChildren<Renderer>(true).Length > 0 && clone.GetComponentsInChildren<Collider>(true).Length > 0 && !clone.activeInHierarchy;
                    }
                    Check("actual approved rigid prefabs clone into inactive sector placements: " + kit, placed && shadow.transform.childCount == 3);
                }
                finally { UnityEngine.Object.DestroyImmediate(shadow); }
                var invalid = Newtonsoft.Json.JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(Newtonsoft.Json.JsonConvert.SerializeObject(reusable));
                invalid.cells[0].cellId = invalid.cells[1].cellId;
                Check("persisted reusable identity collision rejected: " + kit, !YQProceduralSettlementLayout.ValidateRecord(invalid, out _));
                Check("source manifest remains unmodified by cost fixture: " + kit, zones[0].sourceInstanceCount == originalSourceCost);
            }
            var candidateManifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                "Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset");
            Check("candidate district source is not silently promoted", YQProceduralSettlementLayout.CountReviewedSectorAssemblies(candidateManifest) == 0);
        }
        catch (Exception exception) { Check("fixture completed: " + exception.GetBaseException().Message, false); }
        finally { pending.Clear(); foreach (var pair in pendingBefore) pending.Add(pair.Key, pair.Value); }
        Check("active player/world authorities untouched", ReferenceEquals(worldBefore, WorldStateManager.Instance) && ReferenceEquals(playerBefore, PlayerStateManager.Instance));
        string HashFile(string path)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        var sourceHashes = new Dictionary<string, string>();
        foreach (string file in new[] { "YQProceduralSettlementLayout.cs", "YQRuntimeWorldSiteCatalog.cs", "YQContinuousWorldFeatureAuthority.cs", "YQGeneratedWorldRuntimeBuilder.cs", "Editor/YQSemanticWorldAuthorityTests.cs" })
            sourceHashes[file] = HashFile("Assets/Assets/Scripts/Generated/" + file);
        string directory = "outputs/YourQuest_Tandem_20261004/site-sector-layout-verification"; Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), verdict = failures == 0 ? "PASS" : "FAIL", failures, checks,
            evidence = "DETACHED_EDITOR_GEOMETRY_AND_COVERAGE_NOT_GAMEPLAY_ACCEPTANCE", sourceHashes, reviewedAssemblyCoverage,
            productionSectorAcceptance = "OPEN_REQUIRES_PRODUCTION_LOAD_ACTIVATION_REPLAY_AND_PLAYSAFE",
            runtimeMvid = typeof(YQCompiledWorldSiteInstance).Assembly.ManifestModule.ModuleVersionId,
            editorMvid = typeof(YQSemanticWorldAuthorityTests).Assembly.ManifestModule.ModuleVersionId,
            runtimeAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll"), editorAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll")
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[YQSiteSectorLayouts] " + checks.Count + " checks; failures=" + failures);
        return failures;
    }

    private static int RunSharedSiteFootprintProbe()
    {
        // note: This detached contract fixture exercises production projection methods while restoring every existing authority/cache owner afterward.
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var snapshots = new List<KeyValuePair<System.Reflection.FieldInfo, object>>();
        void Save(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                var field = type.GetField(name, flags);
                if (field == null) throw new InvalidOperationException("Missing fixture restoration field: " + name);
                snapshots.Add(new KeyValuePair<System.Reflection.FieldInfo, object>(field, field.GetValue(null)));
            }
        }
        Save(typeof(YQWorldGenerationArchitecture), "_runtimeAuthorityPlan", "_runtimeAuthority", "_runtimeAuthorityArtifact", "_runtimeAuthorityHash");
        Save(typeof(YQSpatialMaterializationResolverV2), "cachedPlan", "cachedArtifact", "cachedHash", "cachedMemberFootprintHash", "cachedPrepared", "cachedContinuation", "cachedContinuationFingerprint");
        Save(typeof(YQSemanticWorldAuthority), "s_runtimeCellCoreCacheAuthority");
        var cache = (Dictionary<long, GeneratedSemanticCellPlanRecord>)typeof(YQSemanticWorldAuthority).GetField("s_runtimeCellCoreCache", flags).GetValue(null);
        var order = (Queue<KeyValuePair<long, GeneratedSemanticCellPlanRecord>>)typeof(YQSemanticWorldAuthority).GetField("s_runtimeCellCoreCacheOrder", flags).GetValue(null);
        var savedCache = new Dictionary<long, GeneratedSemanticCellPlanRecord>(cache);
        var savedOrder = order.ToArray();
        var playerBefore = PlayerStateManager.Instance;
        var worldBefore = WorldStateManager.Instance;
        var checks = new List<object>();
        int failures = 0;
        void Check(string name, bool passed)
        {
            checks.Add(new { name, passed });
            if (!passed) failures++;
        }
        try
        {
            GeneratedWorldPlanRecord plan = BuildFixture("semantic-v2-318");
            YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
            if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var compiled, out string reason))
                throw new InvalidOperationException(reason);
            plan.spatialPlanV2 = compiled;
            YQWorldGenerationArchitecture.LockRuntimeAuthority(plan, YQSpatialPlanAuthority.AcceptedV2);
            var initialAuthority = YQSemanticWorldAuthority.Ensure(plan);
            string originalHash = compiled.contentHash;
            YQSiteAnchorV2 target = compiled.blueprint.sites.Find(site => site != null && site.kind != YQSiteKindV2.Origin);
            if (target == null) throw new InvalidOperationException("Fixture has no non-origin site");
            float extent = Mathf.Min(384f, compiled.blueprint.worldSize * .35f);
            target.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = "detached_sector_a", x = -extent, z = -extent, reservedRadius = 16f, sectorIndex = 0 });
            target.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = "detached_sector_b", x = extent, z = extent, reservedRadius = 16f, sectorIndex = 1 });
            target.memberFootprint.Add(new YQSiteMemberFootprintV2 { memberId = "detached_sector_border", x = -368f, z = -368f, reservedRadius = 16f, sectorIndex = 2 });
            if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out reason))
                throw new InvalidOperationException("Detached member fixture rejected: " + reason);
            string artifactBefore = JsonUtility.ToJson(compiled);
            var authority = YQSemanticWorldAuthority.Ensure(plan);
            Check("persisted opening envelope rebuilt for member geometry", !ReferenceEquals(initialAuthority, authority));
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out reason))
                throw new InvalidOperationException(reason);
            var scratch = new List<int>();
            int targetIndex = -1;
            for (int index = 0; index < prepared.SiteCount; index++)
                if (prepared.GetSite(index).siteId == target.siteId) targetIndex = index;
            Check("single prepared owner retained", targetIndex >= 0);
            bool semanticMatches = true, cellMetadataMatches = true, indexedMatches = true, fallbackMatches = true;
            bool Intersects(float x, float z, float radius, Vector2Int cell, float size)
            {
                float minX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + cell.x * size;
                float minZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + cell.y * size;
                return x + radius >= minX && x - radius <= minX + size && z + radius >= minZ && z - radius <= minZ + size;
            }
            bool Expected(Vector2Int cell, float size)
            {
                if (Intersects(target.x, target.z, target.reservedRadius, cell, size)) return true;
                foreach (var member in target.memberFootprint)
                    if (Intersects(member.x, member.z, member.reservedRadius, cell, size)) return true;
                return false;
            }
            // note: Query in reverse traversal order, including opening cells, exact shared borders and empty gaps between separated sectors.
            int compared = 0;
            for (int z = 12; z >= -4; z--)
            for (int x = 12; x >= -4; x--)
            {
                Vector2Int cell = new Vector2Int(x, z);
                bool expected = Expected(cell, 128f);
                semanticMatches &= YQGeneratedWorldQuery.GetSemanticSites(plan, cell).Exists(site => site.siteId == target.siteId) == expected;
                cellMetadataMatches &= YQGeneratedWorldQuery.GetSemanticCellPlan(plan, cell).siteIds.Contains(target.siteId) == expected;
                prepared.CollectSiteIndicesForCell(cell, 128f, scratch);
                indexedMatches &= scratch.Contains(targetIndex) == expected;
                prepared.CollectSiteIndicesForCell(cell, 128.00002f, scratch);
                fallbackMatches &= scratch.Contains(targetIndex) == Expected(cell, 128.00002f);
                compared++;
            }
            Check("semantic union matches closed rectangles across " + compared + " cells", semanticMatches);
            Check("opening and frontier cell metadata matches sector union", cellMetadataMatches);
            Check("canonical index matches exact footprint union", indexedMatches);
            Check("noncanonical grid uses exact fallback", fallbackMatches);
            RunSharedSiteDemandContracts(prepared, target, Check);
            string memberHash = YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(compiled);
            target.memberFootprint.Reverse();
            Check("member signature independent of traversal order", memberHash == YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(compiled));
            target.memberFootprint.Reverse();
            Check("accepted artifact unchanged by queries", artifactBefore == JsonUtility.ToJson(compiled) && originalHash == compiled.contentHash);
            float frozenMemberRadius = prepared.GetSite(targetIndex).MemberFootprint[0].reservedRadius;
            target.memberFootprint[0].reservedRadius = 600f;
            Check("source edits cannot mutate frozen member geometry", prepared.GetSite(targetIndex).MemberFootprint[0].reservedRadius == frozenMemberRadius);
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var broadPrepared, out reason))
                throw new InvalidOperationException(reason);
            Check("member edit invalidates derived prepared cache", !ReferenceEquals(prepared, broadPrepared));
            bool broadMatches = true;
            for (int z = -6; z <= 14; z++)
            for (int x = -6; x <= 14; x++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                broadPrepared.CollectSiteIndicesForCell(cell, 128f, scratch);
                broadMatches &= scratch.Contains(targetIndex) == Expected(cell, 128f);
            }
            Check("large footprint exact fallback omits no sector cells", broadMatches);
            target.memberFootprint[0].x = float.NaN;
            Check("nonfinite member geometry rejected", !YQSpatialBlueprintValidatorV2.Validate(compiled).Accepted);
            Check("active player and world owners unchanged", ReferenceEquals(playerBefore, PlayerStateManager.Instance) && ReferenceEquals(worldBefore, WorldStateManager.Instance));
        }
        catch (Exception exception)
        {
            failures++;
            checks.Add(new { name = "fixture execution", passed = false, exception = exception.ToString() });
        }
        finally
        {
            foreach (var snapshot in snapshots) snapshot.Key.SetValue(null, snapshot.Value);
            cache.Clear();
            foreach (var pair in savedCache) cache.Add(pair.Key, pair.Value);
            order.Clear();
            foreach (var pair in savedOrder) order.Enqueue(pair);
        }
        string directory = "outputs/YourQuest_Tandem_20261004/shared-site-footprint-verification";
        Directory.CreateDirectory(directory);
        string HashFile(string path)
        {
            // note: Bind this fixture result to the imported assemblies and exact source snapshot, without treating either as gameplay evidence.
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        var sourceHashes = new Dictionary<string, string>();
        foreach (string file in new[] { "YQSemanticWorldAuthority.cs", "YQSpatialMaterializationV2.cs",
            "YQSpatialBlueprintV2Validation.cs", "YQPlayerFollowingSemanticChunkStreamer.cs", "YQRuntimeWorldSiteCatalog.cs", "YQGeneratedWorldPopulation.cs",
            "Editor/YQSemanticWorldAuthorityTests.cs" })
            sourceHashes[file] = HashFile("Assets/Assets/Scripts/Generated/" + file);
        File.WriteAllText(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json"),
            Newtonsoft.Json.JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow.ToString("O"), verdict = failures == 0 ? "PASS" : "FAIL", failures,
                evidence = "DETACHED_EDITOR_CONTRACT_NOT_GAMEPLAY_ACCEPTANCE", checks,
                seed = "semantic-v2-318", scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path,
                runtimeMvid = typeof(YQSemanticWorldAuthority).Assembly.ManifestModule.ModuleVersionId,
                editorMvid = typeof(YQSemanticWorldAuthorityTests).Assembly.ManifestModule.ModuleVersionId,
                runtimeAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp.dll"),
                editorAssemblySha256 = HashFile("Library/ScriptAssemblies/Assembly-CSharp-Editor.dll"),
                sourceHashes, authorityAndCacheRestored = true
            }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[YQSharedSiteFootprints] " + checks.Count + " checks; failures=" + failures);
        return failures;
    }

    private static void RunSharedSiteDemandContracts(YQPreparedSpatialMaterializationV2 prepared,
        YQSiteAnchorV2 target, Action<string, bool> check)
    {
        // note: Use a disposable, unconfigured component in Edit Mode; it never registers as Active, runs generation or touches a profile.
        GameObject root = new GameObject("DetachedSharedSiteDemandFixture") { hideFlags = HideFlags.HideAndDontSave };
        var activeBefore = YQPlayerFollowingSemanticChunkStreamer.Active;
        try
        {
            var streamer = root.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            Type type = typeof(YQPlayerFollowingSemanticChunkStreamer);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object Get(string name) => type.GetField(name, flags).GetValue(streamer);
            void Set(string name, object value) => type.GetField(name, flags).SetValue(streamer, value);
            object Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(streamer, args);
            Set("_acceptedSpatialProjection", prepared);
            streamer.chunkWorldSize = 128;
            Vector2Int owner = new Vector2Int(Mathf.FloorToInt((target.x + 512f) / 128f), Mathf.FloorToInt((target.z + 512f) / 128f));
            var member = target.memberFootprint[0];
            Vector2Int cell = new Vector2Int(Mathf.FloorToInt((member.x + 512f) / 128f), Mathf.FloorToInt((member.z + 512f) / 128f));
            if (owner == cell) throw new InvalidOperationException("Fixture did not separate its owner and member cells");
            Set("_currentChunk", cell);
            var content = (HashSet<Vector2Int>)Get("_contentDemand");
            var visible = (HashSet<Vector2Int>)Get("_guaranteedViewDemand");
            content.Add(cell);
            visible.Add(cell);
            var predictions = (Dictionary<Vector2Int, float>)Get("_semanticViewPredictionArrivalSeconds");
            predictions[cell] = .125f;
            Call("RebuildSharedSiteDemand");
            var owners = (HashSet<Vector2Int>)Get("_sharedSiteOwnerDemand");
            check("member demand retains the original central owner", owners.Contains(owner));
            check("loader refuses an owner without a physical admission slot", !streamer.IsSharedSiteDemanded(target.siteId));
            check("member exposure activates its owner", ((HashSet<Vector2Int>)Get("_sharedSiteVisibleOwners")).Contains(owner));
            check("owner inherits member arrival deadline", ((Dictionary<Vector2Int, float>)Get("_sharedSiteOwnerArrivalSeconds"))[owner] == .125f);
            int ownerCount = owners.Count;
            Call("RebuildSharedSiteDemand");
            check("repeat member demand does not duplicate owners", owners.Count == ownerCount);
            var physical = (System.Collections.IDictionary)Get("_physicalDemand");
            Type reasonType = physical.GetType().GetGenericArguments()[1];
            physical.Add(owner, Enum.Parse(reasonType, "SharedSite"));
            check("admitted loader demand uses original site identity", streamer.IsSharedSiteDemanded(target.siteId));
            check("missing owner blocks member publication", !(bool)Call("AreSharedSiteOwnersReady", cell));

            Type chunkType = type.GetNestedType("RuntimeChunk", System.Reflection.BindingFlags.NonPublic);
            object chunk = Activator.CreateInstance(chunkType, true);
            chunkType.GetField("state").SetValue(chunk, YQSemanticChunkLifecycle.Queued);
            ((System.Collections.IDictionary)Get("_chunks")).Add(owner, chunk);
            var queue = (List<Vector2Int>)Get("_queue");
            queue.Add(owner);
            Call("TrimContentQueue");
            check("content trim retains a demanded remote owner", queue.Contains(owner));
            RunSharedSiteVisualContracts(streamer, root, prepared, target, owner, chunk, check);
            Set("_currentChunk", new Vector2Int(1000, 1000));
            check("terrain relevance retains a demanded remote owner", (bool)Call("IsTerrainRequestStillRelevant", owner));
            content.Clear();
            visible.Clear();
            predictions.Clear();
            physical.Clear();
            Call("RebuildSharedSiteDemand");
            check("last member leaving releases owner and loader demand", owners.Count == 0 && !streamer.IsSharedSiteDemanded(target.siteId));
            Call("TrimContentQueue");
            check("obsolete remote content leaves the queue", !queue.Contains(owner));
            check("obsolete remote terrain demand expires", !(bool)Call("IsTerrainRequestStillRelevant", owner));
            ((System.Collections.IDictionary)Get("_chunks")).Clear();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            check("temporary component preserves Active streamer", ReferenceEquals(activeBefore, YQPlayerFollowingSemanticChunkStreamer.Active));
        }
    }

    private static void RunSharedSiteVisualContracts(YQPlayerFollowingSemanticChunkStreamer streamer, GameObject root,
        YQPreparedSpatialMaterializationV2 prepared, YQSiteAnchorV2 anchor, Vector2Int owner, object chunk, Action<string, bool> check)
    {
        // note: Fake only a detached provider's loaded receipt; actual renderer/collider objects exercise publication gates, never reviewed content or gameplay acceptance.
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(YQPlayerFollowingSemanticChunkStreamer), chunkType = chunk.GetType();
        object Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(streamer, args);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(streamer, value);
        void Chunk(string name, object value) => chunkType.GetField(name).SetValue(chunk, value);
        var instances = (Dictionary<string, YQCompiledWorldSiteInstance>)typeof(YQCompiledWorldSiteInstance).GetField("Instances",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        string key = string.IsNullOrWhiteSpace(anchor.sourceSemanticId) ? anchor.siteId : anchor.sourceSemanticId;
        bool hadOld = instances.TryGetValue(key, out var previous);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(128f, 32f, 128f), hideFlags = HideFlags.HideAndDontSave };
        GameObject terrainObject = null, siteObject = null, route = null, impostor = null;
        try
        {
            Set("_worldRoot", root.transform);
            terrainObject = Terrain.CreateTerrainGameObject(data); terrainObject.hideFlags = HideFlags.HideAndDontSave;
            terrainObject.transform.position = new Vector3(-512f + owner.x * 128f, 0f, -512f + owner.y * 128f);
            Set("_terrain", terrainObject.GetComponent<Terrain>());
            Chunk("record", new GeneratedSemanticChunkRecord { chunkX = owner.x, chunkZ = owner.y });
            Chunk("state", YQSemanticChunkLifecycle.Generated); Chunk("physicalRepresentation", true);
            Chunk("requiredContentReady", true); Chunk("overlayReady", true); Chunk("activationComplete", true); Chunk("activeState", (sbyte)1);
            Chunk("appearanceReady", false); Chunk("visualReady", false); Chunk("requiredEcologyReady", false);
            siteObject = new GameObject("DetachedSharedPhysicalSite") { hideFlags = HideFlags.HideAndDontSave };
            siteObject.transform.SetParent(root.transform, false);
            var provider = siteObject.AddComponent<YQCompiledWorldSiteInstance>();
            var target = siteObject.AddComponent<YQStreamedFeatureOverlayTarget>(); target.featureId = anchor.siteId;
            typeof(YQCompiledWorldSiteInstance).GetField("settlementId", flags).SetValue(provider, key);
            typeof(YQCompiledWorldSiteInstance).GetField("loaded", flags).SetValue(provider, true);
            instances[key] = provider;
            GameObject visible = GameObject.CreatePrimitive(PrimitiveType.Cube); visible.transform.SetParent(siteObject.transform, false);
            var renderer = visible.GetComponent<Renderer>(); renderer.enabled = false;
            GameObject hidden = GameObject.CreatePrimitive(PrimitiveType.Cube); hidden.transform.SetParent(siteObject.transform, false); hidden.SetActive(false);
            if (!prepared.TryGetSiteBySiteId(anchor.siteId, out var projected)) throw new InvalidOperationException("Detached accepted site missing");
            var populationMatch = typeof(YQGeneratedWorldPopulation).GetMethod("ContinuationPreparedSiteMatches", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            bool Matches(YQSiteAnchorV2 candidate) => (bool)populationMatch.Invoke(null, new object[] { candidate, projected });
            check("population projection requires the actual prepared accepted site", Matches(anchor));
            var changedAnchor = JsonUtility.FromJson<YQSiteAnchorV2>(JsonUtility.ToJson(anchor)); changedAnchor.x += 1f;
            check("population projection rejects changed anchor geometry", !Matches(changedAnchor));
            changedAnchor = JsonUtility.FromJson<YQSiteAnchorV2>(JsonUtility.ToJson(anchor)); changedAnchor.memberFootprint.Reverse();
            check("population projection tolerates member query order", Matches(changedAnchor));
            changedAnchor.memberFootprint[0].reservedRadius += 1f;
            check("population projection rejects changed accepted member geometry", !Matches(changedAnchor));
            Call("CacheSharedSitePresentation", chunk, target, projected);
            check("shared site is not ready before renderer publication", !(bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            Call("ReconcileSharedSiteVisuals", chunk, false);
            check("member publishes real site renderers without center appearance or ecology", renderer.enabled &&
                (bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            check("authored inactive descendants stay inactive and do not block publication", !hidden.activeSelf &&
                (bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            route = GameObject.CreatePrimitive(PrimitiveType.Cube); route.transform.SetParent(root.transform, false);
            ((List<GameObject>)chunkType.GetField("generatedObjects").GetValue(chunk)).Add(route);
            Call("SetChunkVisualsEnabled", chunk, false);
            check("site-only exposure preserves ordinary route hiding", renderer.enabled && !route.GetComponent<Renderer>().enabled);
            renderer.enabled = false;
            check("disabled actual site renderer blocks member readiness", !(bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            renderer.enabled = true;
            typeof(YQCompiledWorldSiteInstance).GetField("loaded", flags).SetValue(provider, false);
            check("unloaded provider blocks member readiness", !(bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            typeof(YQCompiledWorldSiteInstance).GetField("loaded", flags).SetValue(provider, true);
            impostor = GameObject.CreatePrimitive(PrimitiveType.Cube); impostor.transform.SetParent(root.transform, false);
            var fakeTarget = impostor.AddComponent<YQStreamedFeatureOverlayTarget>(); fakeTarget.featureId = anchor.siteId;
            Call("CacheSharedSitePresentation", chunk, fakeTarget, projected);
            check("providerless same-ID marker cannot replace accepted physical root", (bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            int epoch = (int)chunkType.GetField("contentOwnerEpoch").GetValue(chunk); Chunk("contentOwnerEpoch", epoch + 1);
            Call("ReconcileSharedSiteVisuals", chunk, false);
            check("stale content epoch hides site and blocks member readiness", !renderer.enabled && !(bool)Call("IsSharedSitePresentationReady", chunk, anchor.siteId));
            Chunk("contentOwnerEpoch", epoch);
            var identities = (HashSet<string>)type.GetField("_sharedSiteVisibleIdentities", flags).GetValue(streamer);
            identities.Remove(anchor.siteId); Call("ReconcileSharedSiteVisuals", chunk, false);
            check("last member exposure leaving hides site renderers", !renderer.enabled);
            RunSiteStreamRecoveryContracts(provider, key, check);
        }
        finally
        {
            // note: Restore exactly the borrowed registry entry after disposing the fixture's provider; ordinary prepared sites are untouched.
            if (siteObject != null) UnityEngine.Object.DestroyImmediate(siteObject);
            if (route != null) UnityEngine.Object.DestroyImmediate(route);
            if (impostor != null) UnityEngine.Object.DestroyImmediate(impostor);
            if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
            if (hadOld) instances[key] = previous; else instances.Remove(key);
            Set("_terrain", null);
        }
    }

    private static void RunSiteStreamRecoveryContracts(YQCompiledWorldSiteInstance provider, string key, Action<string, bool> check)
    {
        // note: Exercise cancellation and re-entry decisions without executing the nested Unity destruction/loading routines or using a real profile.
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(YQCompiledWorldSiteInstance);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(provider, value);
        T Get<T>(string name) => (T)type.GetField(name, flags).GetValue(provider);
        object Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(provider, args);
        var staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        bool GeometryContext()
        {
            object[] args = { key, "detached-kit", "detached-composition", null };
            return (bool)type.GetMethod("TryGetContinuationGeometryContext", staticFlags).Invoke(null, args) && ReferenceEquals(args[3], provider);
        }
        var contextType = typeof(YQCompiledWorldSiteInstance.ContinuationPopulationContext);
        object CaptureContext(Transform content, object issuer) => contextType.GetMethod("Capture", staticFlags).Invoke(null, new object[] { provider, content, issuer });
        bool ContextOwner(object context, Transform content)
        {
            object[] args = { content, null };
            return (bool)contextType.GetMethod("TryGetOwner", flags).Invoke(context, args) && ReferenceEquals(args[1], provider);
        }
        var slotField = type.GetField("ActiveStreamLoader", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        object previousSlot = slotField.GetValue(null);
        GameObject quarantine = null;
        try
        {
            Set("expectedKitId", "detached-kit"); Set("semanticSliceSeed", "detached-composition"); Set("canonicalRegionId", "detached-region");
            bool Context(string kit = "detached-kit", string seed = "detached-composition") =>
                YQCompiledWorldSiteInstance.TryGetLoadedPopulationContext(key, kit, seed, out var owner) && ReferenceEquals(owner, provider);
            check("population context returns the exact registered loaded provider", Context());
            Set("continuationLocation", new GeneratedSpatialContinuationLocationV2Record { settlement = new GeneratedSettlementRecord() });
            Set("continuationPopulationReady", false);
            check("continued geometry cannot certify its missing cast", !provider.IsLoaded && !Context());
            check("internal geometry readiness retains exact provider identity", (bool)type.GetProperty("IsContinuationGeometryLoaded", flags).GetValue(provider) && GeometryContext());
            check("foreign issuer cannot authorize pending actor context", CaptureContext(provider.transform, new object()) == null);
            Set("continuationPopulationReady", true);
            check("completed continued cast restores strict provider readiness", provider.IsLoaded && Context());
            Set("continuationLocation", null);
            check("population context retains the provider canonical region", provider.CanonicalRegionId == "detached-region" && Context("DETACHED-KIT"));
            check("population context rejects a mismatched kit or composition", !Context("other-kit") && !Context(seed: "other-composition"));
            check("population context requires an explicit composition identity", !Context(seed: "") && !Context(seed: null));
            Set("loading", true); check("population context rejects loading provider", !Context()); Set("loading", false);
            Set("unloading", true); check("population context rejects retiring provider", !Context()); Set("unloading", false);
            Set("loadRejected", true); check("population context rejects failed provider", !Context()); Set("loadRejected", false);
            Set("interruptedStreamCleanupRequired", true); check("population context rejects interrupted cleanup", !Context()); Set("interruptedStreamCleanupRequired", false);
            Set("settlementId", "another-location"); check("population context rejects mismatched canonical location", !Context()); Set("settlementId", key);
            quarantine = new GameObject("CompiledSiteContent") { hideFlags = HideFlags.HideAndDontSave };
            quarantine.transform.SetParent(provider.transform, false); Set("pendingSiteContent", quarantine);
            check("population context rejects pending quarantined content", !Context());
            Set("pendingSiteContent", null); Set("continuationPopulationInFlight", true);
            object issuer = type.GetField("ContinuationPopulationIssuer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            var stagingContext = CaptureContext(quarantine.transform, issuer);
            check("owned active geometry admits a scoped staging context", ContextOwner(stagingContext, quarantine.transform));
            check("staging context rejects another content root", !ContextOwner(stagingContext, provider.transform));
            int contextEpoch = Get<int>("streamExecutionEpoch"); Set("streamExecutionEpoch", contextEpoch + 1);
            check("staging context rejects a superseded provider epoch", !ContextOwner(stagingContext, quarantine.transform));
            Set("streamExecutionEpoch", contextEpoch); quarantine.SetActive(false);
            check("staging context cannot place actors against inactive geometry", !ContextOwner(stagingContext, quarantine.transform));
            quarantine.SetActive(true); Set("pendingSiteContent", quarantine);
            Set("loading", true); Set("loadFailure", string.Empty); slotField.SetValue(null, provider);
            int before = Get<int>("streamExecutionEpoch"); Call("CancelOwnedSiteStreaming");
            check("intentional cancellation advances epoch and quarantines content", Get<int>("streamExecutionEpoch") != before &&
                Get<bool>("interruptedStreamCleanupRequired") && Get<bool>("loadRejected") && !Get<bool>("loaded") &&
                !Get<bool>("loading") && !quarantine.activeSelf && slotField.GetValue(null) == null);
            slotField.SetValue(null, provider);
            var recovery = (IEnumerator)Call("RecoverInterruptedStreamAndLoadRoutine", key, Get<int>("streamExecutionEpoch"));
            check("recovery owns the existing slot while yielding bounded retirement", recovery.MoveNext() && recovery.Current is IEnumerator &&
                Get<bool>("loading") && Get<bool>("loadRejected") && ReferenceEquals(slotField.GetValue(null), provider) &&
                ReferenceEquals(Get<GameObject>("pendingSiteContent"), quarantine));
            Call("CancelOwnedSiteStreaming"); slotField.SetValue(null, provider);
            check("stale recovery cannot reopen or release a later execution slot", !recovery.MoveNext() && Get<bool>("loadRejected") &&
                Get<bool>("interruptedStreamCleanupRequired") && ReferenceEquals(slotField.GetValue(null), provider));
            (recovery as IDisposable)?.Dispose();
            Set("pendingSiteContent", null); UnityEngine.Object.DestroyImmediate(quarantine); quarantine = null;
            var cleanRecovery = (IEnumerator)Call("RecoverInterruptedStreamAndLoadRoutine", key, Get<int>("streamExecutionEpoch"));
            bool retirementRequested = cleanRecovery.MoveNext(); var retirement = cleanRecovery.Current as IEnumerator;
            check("empty retirement keeps cleanup and rejection through its frame barrier", retirementRequested && retirement != null && retirement.MoveNext() &&
                retirement.Current == null && Get<bool>("loading") && Get<bool>("unloading") && Get<bool>("loadRejected") &&
                Get<bool>("interruptedStreamCleanupRequired") && ReferenceEquals(slotField.GetValue(null), provider));
            check("empty retirement finishes without yielding a physical load", !retirement.MoveNext() && !Get<bool>("unloading") && Get<bool>("loadRejected"));
            check("only completed retirement reopens the accepted load", cleanRecovery.MoveNext() && cleanRecovery.Current is IEnumerator &&
                !Get<bool>("loadRejected") && !Get<bool>("interruptedStreamCleanupRequired") && Get<bool>("loading") &&
                ReferenceEquals(slotField.GetValue(null), provider));
            (cleanRecovery.Current as IDisposable)?.Dispose(); (cleanRecovery as IDisposable)?.Dispose(); Call("CancelOwnedSiteStreaming");
            Set("pendingSiteContent", null); Set("interruptedStreamCleanupRequired", false); Set("loadFailure", "detached-terminal-failure");
            bool? completed = null; var rejected = YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(key, value => completed = value);
            check("real rejection without pending content remains terminal", !rejected.MoveNext() && completed == false &&
                Get<bool>("loadRejected") && !Get<bool>("interruptedStreamCleanupRequired"));
            (rejected as IDisposable)?.Dispose();
        }
        finally
        {
            // note: Restore slot ownership after disposing only this fixture's state; nested physical retirement was never run.
            Call("CancelOwnedSiteStreaming"); Set("pendingSiteContent", null); Set("interruptedStreamCleanupRequired", false);
            Set("loadRejected", false); Set("loadFailure", string.Empty); Set("loaded", true);
            if (quarantine != null) UnityEngine.Object.DestroyImmediate(quarantine);
            slotField.SetValue(null, previousSlot);
        }
    }

    private static void RunR2QueryCostProbe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Profiling.Profiler.enabled)
            throw new InvalidOperationException("Query cost probe requires idle, unprofiled Edit Mode");
        const string fixturePath = "outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json";
        var settings = new Newtonsoft.Json.JsonSerializerSettings {
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        WorldState detached = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(fixturePath), settings);
        GeneratedWorldPlanRecord plan = detached?.generatedWorldPlan;
        if (plan?.worldSeed != "4bb221dc" || plan.spatialPlanV2?.contentHash != "c1d6ce9d3e06f760")
            throw new InvalidOperationException("Query cost probe requires the accepted detached fixture");
        string artifactBefore = Newtonsoft.Json.JsonConvert.SerializeObject(plan.spatialPlanV2, settings);
        var playerBefore = PlayerStateManager.Instance;
        var worldBefore = WorldStateManager.Instance;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var architectureType = typeof(YQWorldGenerationArchitecture);
        string[] authorityFields = { "_runtimeAuthorityPlan", "_runtimeAuthority", "_runtimeAuthorityArtifact", "_runtimeAuthorityHash" };
        object[] savedAuthority = new object[authorityFields.Length];
        for (int i = 0; i < authorityFields.Length; i++)
            savedAuthority[i] = architectureType.GetField(authorityFields[i], flags).GetValue(null);
        var projectionType = typeof(YQSpatialMaterializationResolverV2);
        string[] projectionFields = { "cachedPlan", "cachedArtifact", "cachedHash", "cachedMemberFootprintHash", "cachedPrepared", "cachedContinuation", "cachedContinuationFingerprint" };
        object[] savedProjection = new object[projectionFields.Length];
        for (int i = 0; i < projectionFields.Length; i++)
            savedProjection[i] = projectionType.GetField(projectionFields[i], flags).GetValue(null);
        var semanticType = typeof(YQSemanticWorldAuthority);
        var cacheOwnerField = semanticType.GetField("s_runtimeCellCoreCacheAuthority", flags);
        object savedCacheOwner = cacheOwnerField.GetValue(null);
        var cache = (Dictionary<long, GeneratedSemanticCellPlanRecord>)semanticType.GetField("s_runtimeCellCoreCache", flags).GetValue(null);
        var order = (Queue<KeyValuePair<long, GeneratedSemanticCellPlanRecord>>)semanticType.GetField("s_runtimeCellCoreCacheOrder", flags).GetValue(null);
        var savedCache = new Dictionary<long, GeneratedSemanticCellPlanRecord>(cache);
        var savedOrder = order.ToArray();
        // note: Some Unity Mono builds expose this API but return zero; a known live allocation must calibrate it before reporting byte measurements.
        long calibrationBefore = GC.GetAllocatedBytesForCurrentThread();
        byte[] calibration = new byte[4096];
        GC.KeepAlive(calibration);
        bool allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() - calibrationBefore >= 4096;
        var report = new List<string> {
            "# Detached Edit query cost; utc=" + DateTime.UtcNow.ToString("O") +
                "; runtimeMvid=" + typeof(YQSemanticWorldAuthority).Assembly.ManifestModule.ModuleVersionId +
                "; editorMvid=" + typeof(YQSemanticWorldAuthorityTests).Assembly.ManifestModule.ModuleVersionId +
                "; fixture=" + fixturePath + "; seed=4bb221dc; V2=c1d6ce9d3e06f760; notRuntimeCertification=true; allocationCounterAvailable=" + allocationCounterAvailable,
            "scope\titerations\ttotalMs\tmeanMs\tallocatedBytes\tmeanAllocatedBytes"
        };
        void Measure(string name, int iterations, Action<int> action)
        {
            // note: Delegate/report allocations are outside the counter interval; no forced collection or live cache clearing occurs.
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) action(i);
            double milliseconds = 1000d * (System.Diagnostics.Stopwatch.GetTimestamp() - started) / System.Diagnostics.Stopwatch.Frequency;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            report.Add(name + "\t" + iterations + "\t" + milliseconds.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) +
                "\t" + (milliseconds / iterations).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) +
                "\t" + (allocationCounterAvailable ? allocated.ToString() : "unavailable") +
                "\t" + (allocationCounterAvailable ? (allocated / (double)iterations).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "unavailable"));
        }
        try
        {
            // note: Match the already-frozen gameplay authority branch only on this detached document, then restore all prior authority/cache objects in finally.
            YQWorldGenerationArchitecture.LockRuntimeAuthority(plan, YQSpatialPlanAuthority.AcceptedV2);
            YQSemanticWorldAuthority.Ensure(plan);
            YQGeneratedWorldQuery.GetSemanticCellPlan(plan, new Vector2Int(1000, 1000));
            YQGeneratedWorldQuery.GetSemanticSites(plan, new Vector2Int(1000, 1000));
            if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out _, out string preparationFailure))
                throw new InvalidOperationException("Accepted query fixture could not prepare: " + preparationFailure);
            Measure("sourceFingerprint", 200, _ => YQSemanticWorldAuthority.BuildSourceFingerprint(plan));
            Measure("ensureAuthority", 200, _ => YQSemanticWorldAuthority.Ensure(plan));
            Measure("cellAndSitesFirstQuery", 32, i => {
                Vector2Int coordinate = new Vector2Int(11 + i, 5);
                YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
                YQGeneratedWorldQuery.GetSemanticSites(plan, coordinate);
            });
            Measure("cellAndSitesCachedCore", 128, i => {
                Vector2Int coordinate = new Vector2Int(11 + i % 32, 5);
                YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
                YQGeneratedWorldQuery.GetSemanticSites(plan, coordinate);
            });
            Measure("currentContentHashReadOnly", 64, _ => YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2));
            Measure("currentPreparedProjectionValidation", 64, iteration => {
                if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out _, out string failure))
                    throw new InvalidOperationException(failure);
            });
            if (artifactBefore != Newtonsoft.Json.JsonConvert.SerializeObject(plan.spatialPlanV2, settings) ||
                !ReferenceEquals(playerBefore, PlayerStateManager.Instance) || !ReferenceEquals(worldBefore, WorldStateManager.Instance))
                throw new InvalidOperationException("Detached probe changed accepted artifact or active state owners");
            report.Add("# PASS acceptedArtifactUnchanged=true activeOwnersUnchanged=true");
        }
        finally
        {
            for (int i = 0; i < authorityFields.Length; i++)
                architectureType.GetField(authorityFields[i], flags).SetValue(null, savedAuthority[i]);
            for (int i = 0; i < projectionFields.Length; i++)
                projectionType.GetField(projectionFields[i], flags).SetValue(null, savedProjection[i]);
            cache.Clear();
            foreach (var pair in savedCache) cache.Add(pair.Key, pair.Value);
            order.Clear();
            foreach (var pair in savedOrder) order.Enqueue(pair);
            cacheOwnerField.SetValue(null, savedCacheOwner);
        }
        report.Add("# authorityAndDerivedCacheRestored=true");
        File.WriteAllLines("Logs/G08_R2_D48_QueryCost_20261002.tsv", report);
    }

    [MenuItem("YourQuest/World Generation/Run Semantic World Authority Tests")]
    public static void RunFromMenu()
    {
        RunAndReportSemanticContracts();
    }

    private static int RunAndReportSemanticContracts()
    {
        int failures = RunTests(out int tested);
        WriteReport(tested, failures);
        Debug.Log("[YQSemanticWorldTests] Tested " + tested + " semantic contracts; failures=" + failures + ".");
        return failures;
    }

    public static void RunBatch()
    {
        int failures = RunTests(out int tested);
        Debug.Log("[YQSemanticWorldTests] Tested " + tested + " semantic contracts; failures=" + failures + ".");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static int RunTests(out int tested)
    {
        tested = 0;
        int failures = 0;
        Run("deterministic coordinates and order", TestDeterminismAndOrder, ref tested, ref failures);
        Run("shared boundary contracts", TestSharedBoundaries, ref tested, ref failures);
        // note: Cell projection requires a real origin sampling input; a disposable flat terrain isolates semantic contracts without touching the PlaySafe terrain or saves.
        TerrainData data = new TerrainData { heightmapResolution = 33, size = new Vector3(1024f, 140f, 1024f), hideFlags = HideFlags.HideAndDontSave };
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.hideFlags = HideFlags.HideAndDontSave;
        terrainObject.transform.position = new Vector3(-512f, 0f, -512f);
        Terrain fixtureTerrain = terrainObject.GetComponent<Terrain>();
        try
        {
            Run("site reservations and route ownership", () => TestSitesRoutesAndOwnership(fixtureTerrain), ref tested, ref failures);
            Run("water source/downstream/sink graph", TestWaterGraph, ref tested, ref failures);
            Run("empty cells do not synthesize routes", TestEmptyCellRoutePolicy, ref tested, ref failures);
            Run("accepted V2 adapter and opening envelope", () => TestAcceptedV2Adapter(fixtureTerrain), ref tested, ref failures);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
        return failures;
    }

    private static void Run(string name, Func<string> test, ref int tested, ref int failures)
    {
        tested++;
        string failure;
        try { failure = test(); }
        catch (Exception exception) { failure = exception.ToString(); }
        if (string.IsNullOrWhiteSpace(failure))
            return;
        failures++;
        Debug.LogError("[YQSemanticWorldTests] " + name + ": " + failure);
    }

    private static void WriteReport(int tested, int failures)
    {
        // note: Persist the latest focused result separately from the rolling Unity editor log so acceptance evidence is unambiguous.
        string path = Path.GetFullPath("Logs/YQSemanticWorldAuthorityTests.md");
        File.WriteAllText(path,
            "# YourQuest Semantic World Authority Tests\n" +
            "- generatedUtc: " + DateTime.UtcNow.ToString("O") + "\n" +
            "- contracts: " + tested + "\n" +
            "- failures: " + failures + "\n" +
            "- status: " + (failures == 0 ? "PASS" : "FAIL") + "\n" +
            "- evidence: deterministic coordinate/order, shared boundaries, site ownership, accepted overrides, route endpoints, water graph/crossings, empty-cell policy, huge-POI policy, accepted V2 preservation, opening envelope, bounded neighborhood\n");
    }

    private static string TestDeterminismAndOrder()
    {
        GeneratedWorldPlanRecord firstPlan = BuildFixture("semantic-golden-042");
        GeneratedWorldPlanRecord secondPlan = BuildFixture("semantic-golden-042");
        Vector2Int[] coordinates =
        {
            new Vector2Int(4, 4), new Vector2Int(8, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, -1), new Vector2Int(-7, 9), new Vector2Int(41, -37)
        };
        for (int index = 0; index < coordinates.Length; index++)
        {
            GeneratedSemanticCellPlanRecord first = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]);
            GeneratedSemanticCellPlanRecord second = YQGeneratedWorldQuery.GetSemanticCellPlan(secondPlan, coordinates[index]);
            if (first == null || second == null || first.semanticHash != second.semanticHash)
                return "same seed changed cell hash at " + coordinates[index];
            if (string.IsNullOrWhiteSpace(first.ownerRegionId) || string.IsNullOrWhiteSpace(first.continentId) ||
                string.IsNullOrWhiteSpace(first.landformRegime) || string.IsNullOrWhiteSpace(first.paletteContext) ||
                string.IsNullOrWhiteSpace(first.localSeed) || string.IsNullOrWhiteSpace(first.routeConstraint) ||
                string.IsNullOrWhiteSpace(first.waterConstraint) || first.featureIds.Count == 0)
                return "cell plan omitted a required semantic field at " + coordinates[index];
        }

        Vector2Int[] orderA = { new Vector2Int(41, -37), new Vector2Int(-1, 0), new Vector2Int(4, 4), new Vector2Int(0, -1) };
        Vector2Int[] orderB = { new Vector2Int(0, -1), new Vector2Int(4, 4), new Vector2Int(41, -37), new Vector2Int(-1, 0) };
        Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < orderA.Length; index++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, orderA[index]);
            hashes[cell.cellId] = cell.semanticHash;
        }
        for (int index = 0; index < orderB.Length; index++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, orderB[index]);
            if (!hashes.TryGetValue(cell.cellId, out string expected) || expected != cell.semanticHash)
                return "query order changed " + cell.cellId;
        }
        // note: Read the initialized immutable authority concurrently to prove parallel query order does not alter hashes or feature ownership.
        YQSemanticWorldAuthority.Ensure(firstPlan);
        string[] parallelHashes = new string[coordinates.Length];
        Parallel.For(0, coordinates.Length, index =>
        {
            parallelHashes[index] = YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]).semanticHash;
        });
        for (int index = 0; index < coordinates.Length; index++)
            if (parallelHashes[index] != YQGeneratedWorldQuery.GetSemanticCellPlan(firstPlan, coordinates[index]).semanticHash)
                return "parallel query changed " + coordinates[index];
        return string.Empty;
    }

    private static string TestSharedBoundaries()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-boundary-019");
        Vector2Int[] coordinates = { new Vector2Int(4, 4), new Vector2Int(-1, 0), new Vector2Int(12, -9) };
        for (int index = 0; index < coordinates.Length; index++)
        {
            Vector2Int coordinate = coordinates[index];
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
            GeneratedSemanticCellPlanRecord east = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate + Vector2Int.right);
            GeneratedSemanticCellPlanRecord north = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate + Vector2Int.up);
            GeneratedSemanticBoundaryRecord eastContract = FindBoundary(cell, "east");
            GeneratedSemanticBoundaryRecord westContract = FindBoundary(east, "west");
            GeneratedSemanticBoundaryRecord northContract = FindBoundary(cell, "north");
            GeneratedSemanticBoundaryRecord southContract = FindBoundary(north, "south");
            if (eastContract == null || westContract == null || eastContract.canonicalKey != westContract.canonicalKey || eastContract.contractHash != westContract.contractHash)
                return "east/west boundary mismatch at " + coordinate;
            if (northContract == null || southContract == null || northContract.canonicalKey != southContract.canonicalKey || northContract.contractHash != southContract.contractHash)
                return "north/south boundary mismatch at " + coordinate;
        }
        return string.Empty;
    }

    private static string TestSitesRoutesAndOwnership(Terrain fixtureTerrain)
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-sites-733");
        GeneratedSpatialWorldPlanRecord v1Before = YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        string v1ArtifactBefore = JsonUtility.ToJson(v1Before);
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.siteReservations.Count < 4 || authority.routeGraph.Count < 3)
            return "accepted site/route graph is incomplete";
        if (authority.acceptedOverrides.Count < 6)
            return "accepted names/layout bindings were not persisted in the semantic envelope";
        if (v1ArtifactBefore != JsonUtility.ToJson(plan.spatialPlan))
            return "semantic adaptation rewrote the accepted V1 spatial artifact";
        GeneratedSemanticAcceptedOverrideRecord settlementOverride = FindAcceptedOverride(authority, "fixture_settlement_0");
        if (settlementOverride == null || settlementOverride.displayName != "Fixture Gate" || settlementOverride.layoutBindingId != "fixture-town-kit")
            return "accepted settlement presentation binding was not persisted";
        // note: Add deterministic city/castle/dungeon large-site contracts to the isolated fixture so multi-cell ownership is tested independently of whichever small V1 locations the planner emits.
        GeneratedSemanticSiteReservationRecord largeSite = BuildLargeSiteFixtureReservation(
            "fixture:city:large", "city", "multi_cell_city_contract", 64f, 64f, "fixture:city:ingress");
        GeneratedSemanticSiteReservationRecord castleSite = BuildLargeSiteFixtureReservation(
            "fixture:castle:large", "castle", "multi_cell_castle_contract", 640f, -64f, string.Empty);
        GeneratedSemanticSiteReservationRecord dungeonSite = BuildLargeSiteFixtureReservation(
            "fixture:dungeon:large", "multi_cell_dungeon", "multi_cell_dungeon_contract", -192f, 640f, string.Empty);
        string connectedSiteId = authority.siteReservations[0].siteId;
        authority.siteReservations.Add(largeSite);
        authority.siteReservations.Add(castleSite);
        authority.siteReservations.Add(dungeonSite);
        largeSite.routeIds.Add("fixture:city:ingress");
            authority.routeGraph.Add(new GeneratedSemanticRouteGraphRecord
        {
            routeId = "fixture:city:ingress",
            parentRouteId = "fixture:city:ingress",
            ownerRegionId = largeSite.ownerRegionId,
            fromSiteId = connectedSiteId,
            toSiteId = largeSite.siteId,
            routeClass = "city_ingress",
            accepted = true,
            permittedBoundaryContinuation = false,
            points = new List<GeneratedSemanticRoutePointRecord>
            {
                new GeneratedSemanticRoutePointRecord { worldX = largeSite.worldX - largeSite.footprintRadius, worldZ = largeSite.worldZ },
                new GeneratedSemanticRoutePointRecord { worldX = largeSite.worldX, worldZ = largeSite.worldZ }
            }
        });
        // note: Fixture-only graph additions postdate the opening cache. Rebuild detached cells instead of weakening their identity checks or changing production cache behavior.
        authority = JsonUtility.FromJson<GeneratedSemanticWorldAuthorityRecord>(JsonUtility.ToJson(authority));
        plan.semanticAuthority = authority;
        var buildCell = typeof(YQSemanticWorldAuthority).GetMethod("BuildCellWithEdges",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        for (int index = 0; index < authority.openingEnvelope.Count; index++)
        {
            var cached = authority.openingEnvelope[index];
            authority.openingEnvelope[index] = (GeneratedSemanticCellPlanRecord)buildCell.Invoke(null,
                new object[] { plan, authority, new Vector2Int(cached.cellX, cached.cellZ), null });
        }
        // note: Keep the city/castle/dungeon contract kinds explicit so future materializers cannot silently collapse all large footprints into one settlement-only branch.
        if (castleSite.memberCellIds.Count < 9 || dungeonSite.memberCellIds.Count < 9 ||
            castleSite.siteKind != "castle" || dungeonSite.siteKind != "multi_cell_dungeon")
            return "large-site city/castle/dungeon contract coverage is incomplete";
        // note: Re-query the authority after adding the fixture reservation to prove normalization preserves its footprint and owner identity across a second access.
        GeneratedSemanticWorldAuthorityRecord normalizedAuthority = YQSemanticWorldAuthority.Ensure(plan);
        GeneratedSemanticSiteReservationRecord normalizedLargeSite = normalizedAuthority.siteReservations.Find(candidate => candidate != null && candidate.siteId == largeSite.siteId);
        if (normalizedLargeSite == null || normalizedLargeSite.memberCellIds.Count != largeSite.memberCellIds.Count ||
            normalizedLargeSite.ownerCellId != largeSite.ownerCellId)
            return "large-site reservation changed during authority normalization";
        // note: Confirm the representative city spans multiple sectors and exposes stable ingress/egress anchors inside its persisted bounds.
        if (largeSite.memberCellIds.Count < 9 || largeSite.entrances.Count != 2 ||
            largeSite.entrances[0].entranceId != largeSite.siteId + "|entrance|west" ||
            largeSite.entrances[1].entranceId != largeSite.siteId + "|entrance|east" ||
            Mathf.Abs(largeSite.entrances[0].worldX - (largeSite.worldX - largeSite.footprintRadius)) > 0.01f ||
            Mathf.Abs(largeSite.entrances[1].worldX - (largeSite.worldX + largeSite.footprintRadius)) > 0.01f)
            return "large-site ingress/egress contract is not stable";
        HashSet<string> owners = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < authority.siteReservations.Count; index++)
        {
            GeneratedSemanticSiteReservationRecord site = authority.siteReservations[index];
            if (site == null || string.IsNullOrWhiteSpace(site.siteId) || site.entrances.Count == 0 || site.memberCellIds.Count == 0)
                return "site reservation lacks immutable footprint or entrance: " + site?.siteId;
            // note: Require one persisted owner that is also inside the accepted member footprint; neighboring sectors may project the site but cannot claim publication ownership.
            if (string.IsNullOrWhiteSpace(site.ownerCellId) || !site.memberCellIds.Contains(site.ownerCellId))
                return "site reservation lacks a member-cell owner: " + site?.siteId;
            if (!owners.Add(site.ownerFeatureId))
                return "two reservations share an owner feature: " + site.ownerFeatureId;
            // note: Verify a reserved site can be discovered from its owning cell through the stable public query adapter.
            Vector2Int siteCell = new Vector2Int(
                Mathf.FloorToInt((site.worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters),
                Mathf.FloorToInt((site.worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters));
            if (!YQSemanticWorldAuthority.IsOwnerCell(site, siteCell))
                return "site center does not resolve to its persisted owner cell: " + site.siteId;
            // note: Exercise the streamed projection at the owner boundary so a persisted footprint cannot be mistaken for physical publication in every member sector.
            YQContinuousWorldCellAuthority cellAuthority =
                new YQContinuousWorldCellAuthority(
                    plan.worldSeed,
                    fixtureTerrain,
                    null,
                    plan,
                    YQSemanticWorldAuthority.CellSizeMeters);
            GeneratedSemanticChunkRecord ownerRecord =
                new GeneratedSemanticChunkRecord();
            cellAuthority.PopulateCellRecord(
                ownerRecord,
                siteCell,
                YQSemanticWorldAuthority.CellSizeMeters,
                plan);
            GeneratedSemanticSiteRecord projectedOwner =
                ownerRecord.sites.Find(
                    candidate => candidate != null && candidate.siteId == site.siteId);
            if (projectedOwner == null || !projectedOwner.isOwnerCell ||
                projectedOwner.ownerCellId != site.ownerCellId)
                return "owner sector did not publish the persisted site ownership projection: " + site.siteId;
            // note: The physical materializer consumes the cell identity index, so ownership projection is incomplete if the accepted site ID remains only in the nested site record.
            if (!ownerRecord.featureIds.Contains(site.siteId))
                return "owner sector omitted the accepted site identity index: " + site.siteId;
            if (site.memberCellIds.Count > 1)
            {
                string memberId = site.memberCellIds.Find(
                    candidate => !string.Equals(candidate, site.ownerCellId, StringComparison.Ordinal));
                if (TryParseCellId(memberId, out Vector2Int memberCell))
                {
                    GeneratedSemanticChunkRecord memberRecord =
                        new GeneratedSemanticChunkRecord();
                    cellAuthority.PopulateCellRecord(
                        memberRecord,
                        memberCell,
                        YQSemanticWorldAuthority.CellSizeMeters,
                        plan);
                    GeneratedSemanticSiteRecord projectedMember =
                        memberRecord.sites.Find(
                            candidate => candidate != null && candidate.siteId == site.siteId);
                    if (projectedMember == null || projectedMember.isOwnerCell ||
                        projectedMember.ownerCellId != site.ownerCellId)
                        return "non-owner member sector claimed site publication ownership: " + site.siteId;
                }
            }
            bool siteFoundInCell = false;
            List<GeneratedSemanticSiteReservationRecord> cellSites = YQGeneratedWorldQuery.GetSemanticSites(plan, siteCell);
            for (int siteIndex = 0; siteIndex < cellSites.Count; siteIndex++)
            {
                GeneratedSemanticSiteReservationRecord candidate = cellSites[siteIndex];
                siteFoundInCell |= candidate != null && candidate.siteId == site.siteId;
                if (candidate != null && !candidate.accepted && candidate.accessScore < authority.syntheticMinimumAccessScore)
                    return "lazy candidate violated access budget: " + candidate.siteId;
                if (candidate != null && candidate.siteId != site.siteId && !candidate.accepted &&
                    Vector2.Distance(new Vector2(candidate.worldX, candidate.worldZ), new Vector2(site.worldX, site.worldZ)) <
                    Mathf.Max(site.minimumExclusionRadius, site.footprintRadius) + candidate.footprintRadius)
                    return "lazy candidate violated accepted site exclusion: " + candidate.siteId;
            }
            if (!siteFoundInCell)
                return "site reservation was not returned by its owning cell query: " + site.siteId;
            if (!YQGeneratedWorldQuery.TryGetSemanticSite(plan, site.siteId, out GeneratedSemanticSiteReservationRecord resolved) || resolved.worldX != site.worldX)
                return "unloaded site lookup did not resolve: " + site.siteId;
        }
        // note: Visit every persisted member sector of the representative city and require exactly one physical owner projection with a shared footprint identity.
        YQContinuousWorldCellAuthority largeSiteCellAuthority =
            new YQContinuousWorldCellAuthority(plan.worldSeed, fixtureTerrain, null, plan, YQSemanticWorldAuthority.CellSizeMeters);
        int largeSiteOwnerCount = 0;
        for (int memberIndex = 0; memberIndex < largeSite.memberCellIds.Count; memberIndex++)
        {
            if (!TryParseCellId(largeSite.memberCellIds[memberIndex], out Vector2Int memberCell))
                return "large-site member cell identity was not parseable";
            GeneratedSemanticChunkRecord memberRecord = new GeneratedSemanticChunkRecord();
            largeSiteCellAuthority.PopulateCellRecord(memberRecord, memberCell, YQSemanticWorldAuthority.CellSizeMeters, plan);
            GeneratedSemanticSiteRecord projected = memberRecord.sites.Find(candidate => candidate != null && candidate.siteId == largeSite.siteId);
            if (projected == null || projected.ownerCellId != largeSite.ownerCellId ||
                projected.memberCellIds.Count != largeSite.memberCellIds.Count)
                return "large-site member sector lost persisted footprint identity: " + largeSite.memberCellIds[memberIndex];
            if (projected.isOwnerCell)
                largeSiteOwnerCount++;
        }
        if (largeSiteOwnerCount != 1)
            return "large-site projected more than one physical owner sector";
        for (int index = 0; index < authority.routeGraph.Count; index++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[index];
            if (route == null || route.points.Count < 2 || !YQGeneratedWorldQuery.TryGetSemanticSite(plan, route.fromSiteId, out _) || !YQGeneratedWorldQuery.TryGetSemanticSite(plan, route.toSiteId, out _))
                return "route endpoint did not resolve: " + route?.routeId;
        }
        return string.Empty;
    }

    private static GeneratedSemanticSiteReservationRecord BuildLargeSiteFixtureReservation(
        string siteId, string siteKind, string structuralIntent, float worldX, float worldZ, string routeId)
    {
        const float radius = 190f;
        // note: Use the same signed 128 metre grid formula as production ownership so this fixture proves the real sector contract instead of a test-only coordinate convention.
        int ownerX = Mathf.FloorToInt((worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int ownerZ = Mathf.FloorToInt((worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        List<string> memberCellIds = new List<string>();
        int minX = Mathf.FloorToInt((worldX - radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int maxX = Mathf.FloorToInt((worldX + radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int minZ = Mathf.FloorToInt((worldZ - radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        int maxZ = Mathf.FloorToInt((worldZ + radius + 512f) / YQSemanticWorldAuthority.CellSizeMeters);
        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
            memberCellIds.Add("cell:" + x + ":" + z);
        // note: Keep two opposite anchors on the accepted footprint boundary so future city materialization can bind ingress and egress without inventing a second site identity.
        return new GeneratedSemanticSiteReservationRecord
        {
            siteId = siteId,
            ownerRegionId = "fixture_region_0",
            ownerFeatureId = siteId,
            displayName = "Fixture Multi-Cell " + siteKind,
            layoutBindingId = "fixture-" + siteKind + "-kit",
            layoutBindingVersion = "fixture-" + siteKind + "-v1",
            accessScore = 1f,
            accessConstraint = "accepted_layout_access",
            siteKind = siteKind,
            structuralIntent = structuralIntent,
            worldX = worldX,
            worldZ = worldZ,
            footprintRadius = radius,
            minimumExclusionRadius = radius + 18f,
            expansionRadius = radius * 1.35f,
            culturalIntent = "fixture_large_site_contract",
            immutable = true,
            accepted = true,
            provenance = "semantic_authority_fixture",
            ownerCellId = "cell:" + ownerX + ":" + ownerZ,
            memberCellIds = memberCellIds,
            entrances = new List<GeneratedSemanticEntranceRecord>
            {
                new GeneratedSemanticEntranceRecord
                {
                    entranceId = siteId + "|entrance|west",
                    worldX = worldX - radius,
                    worldZ = worldZ,
                    headingDegrees = 270f,
                    permittedRouteId = routeId
                },
                new GeneratedSemanticEntranceRecord
                {
                    entranceId = siteId + "|entrance|east",
                    worldX = worldX + radius,
                    worldZ = worldZ,
                    headingDegrees = 90f,
                    permittedRouteId = string.Empty
                }
            }
        };
    }

    private static bool TryParseCellId(string cellId, out Vector2Int coordinate)
    {
        // note: Decode only the authority's explicit cell identity format; malformed persisted IDs remain test failures rather than guessed coordinates.
        coordinate = default;
        if (string.IsNullOrWhiteSpace(cellId))
            return false;
        string[] parts = cellId.Split(':');
        if (parts.Length != 3 || parts[0] != "cell" ||
            !int.TryParse(parts[1], out int x) ||
            !int.TryParse(parts[2], out int z))
            return false;
        coordinate = new Vector2Int(x, z);
        return true;
    }

    private static string TestWaterGraph()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-water-511");
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.waterNetworks.Count == 0)
            return "no accepted basin/water record was adapted";
        if (authority.hugePoiSpacingTargetCells != 30 || authority.lazyCandidateBudgetPerQuery != 9 ||
            authority.syntheticFeaturePolicy != "seeded_spacing_geography_access_bounded" || authority.betaDeferredFeatureKinds.Count == 0)
            return "huge-POI pacing and deferred-type policy was not persisted";
        for (int index = 0; index < authority.waterNetworks.Count; index++)
        {
            GeneratedSemanticWaterNetworkRecord water = authority.waterNetworks[index];
            if (water == null || string.IsNullOrWhiteSpace(water.waterId) || string.IsNullOrWhiteSpace(water.basinId) ||
                string.IsNullOrWhiteSpace(water.sourceId) || string.IsNullOrWhiteSpace(water.sinkId) || water.points.Count == 0)
                return "water record lacks basin/source/sink/geometry identity";
            if (!water.declaredSink && string.IsNullOrWhiteSpace(water.downstreamWaterId))
                return "water record has neither downstream link nor declared sink: " + water.waterId;
            // note: Verify a basin record is discoverable from a cell intersecting its persisted geometry.
            GeneratedSemanticRoutePointRecord firstPoint = water.points[0];
            Vector2Int waterCell = new Vector2Int(
                Mathf.FloorToInt((firstPoint.worldX + 512f) / YQSemanticWorldAuthority.CellSizeMeters),
                Mathf.FloorToInt((firstPoint.worldZ + 512f) / YQSemanticWorldAuthority.CellSizeMeters));
            bool waterFoundInCell = false;
            List<GeneratedSemanticWaterNetworkRecord> cellWater = YQGeneratedWorldQuery.GetSemanticWater(plan, waterCell);
            for (int waterIndex = 0; waterIndex < cellWater.Count; waterIndex++)
                waterFoundInCell |= cellWater[waterIndex] != null && cellWater[waterIndex].waterId == water.waterId;
            if (!waterFoundInCell)
                return "water record was not returned by an intersecting cell query: " + water.waterId;
        }
        return string.Empty;
    }

    private static string TestEmptyCellRoutePolicy()
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-empty-902");
        Vector2Int candidate = new Vector2Int(70, -71);
        for (int z = -80; z <= 80; z++)
        for (int x = -80; x <= 80; x++)
        {
            GeneratedSemanticCellPlanRecord cell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, new Vector2Int(x, z));
            if (cell.siteIds.Count == 0 && cell.routeIds.Count == 0 && cell.waterIds.Count == 0)
            {
                candidate = new Vector2Int(x, z);
                if (cell.routeConstraint != "no_route_synthesis")
                    return "empty cell published a route constraint: " + cell.cellId;
                break;
            }
        }
        GeneratedSemanticChunkRecord record = new GeneratedSemanticChunkRecord { chunkX = candidate.x, chunkZ = candidate.y };
        YQContinuousWorldCellAuthority authority = new YQContinuousWorldCellAuthority(plan.worldSeed, null, null, plan, 128f);
        authority.PopulateCellRecord(record, candidate, 128f, plan);
        for (int index = 0; index < record.edgeContracts.Count; index++)
            if (record.edgeContracts[index].routePortals.Count != 0)
                return "empty cell synthesized a route portal";
        return string.Empty;
    }

    private static string TestAcceptedV2Adapter(Terrain fixtureTerrain)
    {
        GeneratedWorldPlanRecord plan = BuildFixture("semantic-v2-318");
        // note: The production V2 compiler consumes the persisted semantic fingerprint prepared by the V1-compatible spatial envelope.
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out GeneratedSpatialWorldPlanV2Record compiled, out string compileFailure))
            return "V2 fixture did not compile: " + compileFailure;
        string artifactBefore = JsonUtility.ToJson(compiled);
        plan.spatialPlanV2 = compiled;
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out string validationFailure))
            return "compiled V2 fixture was not accepted: " + validationFailure;
        GeneratedSemanticWorldAuthorityRecord authority = YQSemanticWorldAuthority.Ensure(plan);
        if (authority.siteReservations.Count == 0 || authority.routeGraph.Count == 0 || authority.waterNetworks.Count == 0)
            return "accepted V2 adapter dropped a site, route, or water graph collection";
        // note: Exercise the exact streamed projection used by physical materialization so accepted V2 identities cannot remain trapped in typed semantic lists.
        YQContinuousWorldCellAuthority projectionAuthority =
            new YQContinuousWorldCellAuthority(plan.worldSeed, fixtureTerrain, null, plan, 128f);
        bool projectedStructuralCell = false;
        for (int z = -24; z <= 24 && !projectedStructuralCell; z++)
        {
            for (int x = -24; x <= 24 && !projectedStructuralCell; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                GeneratedSemanticCellPlanRecord semanticCell = YQGeneratedWorldQuery.GetSemanticCellPlan(plan, coordinate);
                if (semanticCell == null || (semanticCell.siteIds.Count == 0 && semanticCell.routeIds.Count == 0 && semanticCell.waterIds.Count == 0))
                    continue;
                GeneratedSemanticChunkRecord projected = new GeneratedSemanticChunkRecord { chunkX = x, chunkZ = z };
                projectionAuthority.PopulateCellRecord(projected, coordinate, 128f, plan);
                for (int index = 0; index < semanticCell.siteIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.siteIds[index]))
                        return "accepted V2 site identity was lost during streamed projection: " + semanticCell.siteIds[index];
                for (int index = 0; index < semanticCell.routeIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.routeIds[index]))
                        return "accepted V2 route identity was lost during streamed projection: " + semanticCell.routeIds[index];
                for (int index = 0; index < semanticCell.waterIds.Count; index++)
                    if (!projected.featureIds.Contains(semanticCell.waterIds[index]))
                        return "accepted V2 water identity was lost during streamed projection: " + semanticCell.waterIds[index];
                projectedStructuralCell = true;
            }
        }
        if (!projectedStructuralCell)
            return "accepted V2 fixture exposed no structural cell for streamed projection";
        if (authority.openingEnvelope.Count != 81 || YQGeneratedWorldQuery.GetSemanticNeighborhood(plan, new Vector2Int(-17, 19), 99).Count != 25)
            return "opening envelope or bounded query neighborhood violated its contract";
        // note: Every accepted route crossing must also be represented by the water-owned crossing contract.
        for (int routeIndex = 0; routeIndex < authority.routeGraph.Count; routeIndex++)
        {
            GeneratedSemanticRouteGraphRecord route = authority.routeGraph[routeIndex];
            for (int crossingIndex = 0; crossingIndex < route.crossings.Count; crossingIndex++)
            {
                GeneratedSemanticRouteCrossingRecord crossing = route.crossings[crossingIndex];
                GeneratedSemanticWaterNetworkRecord water = authority.waterNetworks.Find(item => item != null && item.waterId == crossing.waterId);
                if (water == null || water.crossings.Find(item => item != null && item.crossingId == crossing.crossingId) == null)
                    return "accepted route crossing lacks a shared water crossing contract: " + crossing.crossingId;
            }
        }
        if (artifactBefore != JsonUtility.ToJson(plan.spatialPlanV2))
            return "semantic adaptation rewrote the accepted V2 artifact";
        return string.Empty;
    }

    private static GeneratedSemanticBoundaryRecord FindBoundary(GeneratedSemanticCellPlanRecord cell, string edge)
    {
        for (int index = 0; index < cell.edgeContracts.Count; index++)
            if (cell.edgeContracts[index] != null && cell.edgeContracts[index].edge == edge)
                return cell.edgeContracts[index];
        return null;
    }

    private static GeneratedSemanticAcceptedOverrideRecord FindAcceptedOverride(
        GeneratedSemanticWorldAuthorityRecord authority,
        string objectId)
    {
        for (int index = 0; index < authority.acceptedOverrides.Count; index++)
            if (authority.acceptedOverrides[index] != null && authority.acceptedOverrides[index].objectId == objectId)
                return authority.acceptedOverrides[index];
        return null;
    }

    private static GeneratedWorldPlanRecord BuildFixture(string seed)
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord { worldSeed = seed, summary = "high fantasy river kingdom", designNotes = "semantic authority fixture" };
        plan.EnsureCollections();
        for (int index = 0; index < 3; index++)
            plan.regions.Add(new GeneratedRegionRecord { regionId = "fixture_region_" + index, terrainProfile = index == 1 ? "mountain ridge" : "river forest", climateProfile = index == 2 ? "wet temperate" : "seasonal", gameplayPremise = "trade kingdom", biomeTags = new List<string> { index == 1 ? "highland" : "forest" } });
        for (int index = 0; index < 3; index++)
            plan.settlements.Add(new GeneratedSettlementRecord { settlementId = "fixture_settlement_" + index, regionId = "fixture_region_" + (index % 3), displayName = index == 0 ? "Fixture Gate" : "Fixture Village " + index, runtimeSiteKitId = index == 0 ? "fixture-town-kit" : string.Empty, runtimeSiteBindingVersion = index == 0 ? "fixture-site-bind-v1" : string.Empty, kind = index == 0 ? "trade town" : "village", approxPopulation = 35 + index * 20 });
        for (int index = 0; index < 2; index++)
            plan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "fixture_hostile_" + index, regionId = "fixture_region_" + index, displayName = "Fixture Hostile " + index, layoutIntent = index == 0 ? "checkpoint_layout" : "cave_layout", kind = index == 1 ? "cave" : "bandit checkpoint", threatTier = index + 2 });
        plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord { poiId = "fixture_poi_0", regionId = "fixture_region_2", displayName = "Fixture Shrine", visualStyleKey = "shrine_visual_v1", kind = "lakeside shrine", gameplayHook = "exploration" });
        return plan;
    }
}
