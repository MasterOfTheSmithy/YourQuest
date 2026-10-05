#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// note: This read-only review measures the real palette library and source materials, not the older discovery snapshot or repaired runtime instances.
public static class YQAssetContractLibraryReview
{
    private const string DirectoryPath = "outputs/Asset_Contracts_20261002";

    private static YQInvestorPlayerMotor probeMotor;
    private static CharacterController probeController;
    private static bool probeControllerEnabled;
    private static Vector3 probePosition;
    private static Quaternion probeRotation;
    private static readonly object ProbeToken = new object();
    private static readonly List<YQSpatialMaterializationSiteV2> ProbeSites = new List<YQSpatialMaterializationSiteV2>();
    private static readonly List<(string id, Vector3 position)> ProbeWaters = new List<(string, Vector3)>();
    private static readonly List<string> ProbeReport = new List<string>();
    private static int probeIndex;
    private static double probeDeadline;
    private static double probeNextObservation;
    private static double probeReadyAt;
    private static float probeTimeScale;

    public static bool CanBeginRemoteSiteProbe
    {
        get
        {
            // note: Await owned construction as well as transport; a terminal model callback can precede its paired publication by several frames.
            if (!EditorApplication.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
                probeMotor != null || YQDeveloperConsoleGate.BlocksPersistence ||
                LLMClient.Instance != null && LLMClient.Instance.IsBusy ||
                YQWorldGenerationService.Instance != null && YQWorldGenerationService.Instance.IsRequestInFlight ||
                YQGeneratedNpcPlanningService.Instance != null && YQGeneratedNpcPlanningService.Instance.IsRequestInFlight)
                return false;
            var streamer = UnityEngine.Object.FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            if (streamer == null) return false;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (string name in new[] { "_frontierConstructionWork", "_continuationPopulationWork" })
            {
                var work = typeof(YQPlayerFollowingSemanticChunkStreamer).GetField(name, fields)?.GetValue(streamer) as System.Collections.ICollection;
                if (work == null || work.Count > 0) return false;
            }
            return true;
        }
    }

    [MenuItem("YourQuest/World Generation/Verify Remote Sites With Temporary Player Visit")]
    public static void BeginRemoteSiteProbe()
    {
        BeginRemoteSiteProbeForSite(null);
    }

    public static void BeginRemoteSiteProbeForSite(string requestedSiteId)
    {
        if (!CanBeginRemoteSiteProbe)
            throw new InvalidOperationException("Remote-site witness requires released gameplay and no active witness.");
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (plan == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
            throw new InvalidOperationException("Accepted spatial plan unavailable.");
        // note: A focused request must name an accepted site before the save-protected visit begins; the existing menu still observes all sites and area water.
        bool focused = !string.IsNullOrWhiteSpace(requestedSiteId);
        if (focused)
        {
            bool found = false;
            for (int i = 0; i < prepared.SiteCount; i++)
            {
                var candidate = prepared.GetSite(i);
                if ((candidate.siteId == requestedSiteId || candidate.sourceSemanticId == requestedSiteId) &&
                    (candidate.kind == YQSiteKindV2.Settlement || candidate.kind == YQSiteKindV2.HostileSite || candidate.kind == YQSiteKindV2.PointOfInterest))
                    found = true;
            }
            if (!found) throw new InvalidOperationException("Focused remote-site request does not match an accepted site.");
        }
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor == null) throw new InvalidOperationException("Player unavailable.");
        if (!YQDeveloperTestSession.Begin(out string message)) throw new InvalidOperationException("Cannot protect test state: " + message);
        // note: This assisted witness moves the existing player under the existing developer save barrier. It creates no site, world, player, or persistent plan authority.
        probeMotor = motor;
        probeController = motor.GetComponent<CharacterController>();
        probeControllerEnabled = probeController != null && probeController.enabled;
        probePosition = motor.transform.position; probeRotation = motor.transform.rotation;
        Vector3 lastPosition = PlayerStateManager.Instance.state.lastPosition;
        Vector3 logicalPosition = PlayerStateManager.Instance.state.logicalPosition;
        Vector3 renderOrigin = PlayerStateManager.Instance.state.renderOrigin;
        YQDeveloperTestSession.OnRestore(() => {
            if (probeMotor != null) SetProbePosition(probePosition, probeRotation);
            if (PlayerStateManager.Instance != null) {
                PlayerStateManager.Instance.state.lastPosition = lastPosition;
                PlayerStateManager.Instance.state.logicalPosition = logicalPosition;
                PlayerStateManager.Instance.state.renderOrigin = renderOrigin;
            }
        });
        ProbeSites.Clear(); ProbeWaters.Clear(); ProbeReport.Clear(); probeIndex = 0;
        ProbeReport.Add("Evidence: assisted runtime visits through the production streamer; not an ordinary traversal or R2/R3 receipt.");
        ProbeReport.Add("utc=" + DateTime.UtcNow.ToString("O") + " seed=" + plan.worldSeed);
        for (int i = 0; i < prepared.SiteCount; i++) {
            var site = prepared.GetSite(i);
            if ((site.kind == YQSiteKindV2.Settlement || site.kind == YQSiteKindV2.HostileSite || site.kind == YQSiteKindV2.PointOfInterest) &&
                (!focused || site.siteId == requestedSiteId || site.sourceSemanticId == requestedSiteId)) ProbeSites.Add(site);
        }
        // note: Staging targets come from accepted area-water geometry, never an invented site or replacement water authority.
        MethodInfo footprint = typeof(YQContinuousWorldFeatureAuthority).GetMethod("GetAcceptedAreaWaterFootprint", BindingFlags.Static | BindingFlags.NonPublic);
        for (int index = 0; !focused && index < prepared.WaterCount; index++)
        {
            var water = prepared.GetWater(index);
            if (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland && water.kind != YQHydrologyKindV2.Coastline) continue;
            object[] arguments = { prepared, index, null, null, 0f, 0f };
            footprint.Invoke(null, arguments);
            Vector2 center = (Vector2)arguments[2];
            ProbeWaters.Add((water.hydrologyId, new Vector3(center.x, water.waterLevelNormalized * YQGeneratedWorldTerrain.TerrainHeight + 3f, center.y)));
        }
        probeTimeScale = Time.timeScale;
        RuntimeModalUiBlocker.Acquire(ProbeToken);
        // note: Suppress motor input while keeping the engine clock running, so the witness exercises ordinary streaming and gameplay lifecycle timing.
        Time.timeScale = probeTimeScale;
        EditorApplication.update += UpdateRemoteSiteProbe;
        EditorApplication.playModeStateChanged += EndProbeOnPlayExit;
        AdvanceRemoteSiteProbe();
    }

    private static void SetProbePosition(Vector3 position, Quaternion rotation)
    {
        // note: Disable the existing controller only for the synchronous pose change; modal input suppression holds position while streaming catches up.
        if (probeController != null) probeController.enabled = false;
        if (probeMotor != null) probeMotor.transform.SetPositionAndRotation(position, rotation);
        if (probeController != null) probeController.enabled = probeControllerEnabled;
    }

    private static void AdvanceRemoteSiteProbe()
    {
        if (probeIndex >= ProbeSites.Count + ProbeWaters.Count) { FinishRemoteSiteProbe(); return; }
        if (probeIndex < ProbeSites.Count)
        {
            var site = ProbeSites[probeIndex];
            // note: This is an assisted observation pose above the accepted datum; it supplies no traversal or actual-ground proof.
            // note: A frontage joint may be hundreds of metres along an access road; this assisted pose must stay near the site being observed.
            Vector2 frontage = string.IsNullOrWhiteSpace(site.frontageRouteId) ? Vector2.zero :
                Vector2.ClampMagnitude(new Vector2(site.frontageX - site.x, site.frontageZ - site.z), Mathf.Min(128f, site.reservedRadius + 8f));
            Vector3 position = new Vector3(site.x + frontage.x, site.surfaceElevationNormalized * YQGeneratedWorldTerrain.TerrainHeight + 3f, site.z + frontage.y);
            SetProbePosition(position, Quaternion.LookRotation(new Vector3(site.x - position.x, 0f, site.z - position.z).sqrMagnitude > .001f
                ? new Vector3(site.x - position.x, 0f, site.z - position.z) : Vector3.forward));
            Debug.Log("[YQAssetContracts] Remote witness visiting " + site.siteId + " " + site.kind);
        }
        else
        {
            var water = ProbeWaters[probeIndex - ProbeSites.Count];
            SetProbePosition(water.position, probeRotation);
            Debug.Log("[YQAssetContracts] Remote witness visiting accepted area water " + water.id);
        }
        probeDeadline = EditorApplication.timeSinceStartup + 90d;
        probeNextObservation = 0d;
        probeReadyAt = 0d;
    }

    private static void UpdateRemoteSiteProbe()
    {
        try {
            if (!EditorApplication.isPlaying || probeMotor == null) { FinishRemoteSiteProbe(); return; }
            if (EditorApplication.timeSinceStartup < probeNextObservation) return;
            probeNextObservation = EditorApplication.timeSinceStartup + .5d;
            if (probeIndex >= ProbeSites.Count)
            {
                var water = ProbeWaters[probeIndex - ProbeSites.Count];
                int surfaces = 0;
                foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (renderer.enabled && renderer.name.StartsWith("ContinuousAreaWater_" + water.id + "_", StringComparison.Ordinal)) surfaces++;
                if (surfaces == 0 && EditorApplication.timeSinceStartup < probeDeadline) return;
                // note: The first clipped patch may appear before neighbouring lake tiles; allow the same live streamer to finish its nearby publication.
                if (surfaces > 0)
                {
                    if (probeReadyAt == 0d) probeReadyAt = EditorApplication.timeSinceStartup + 10d;
                    if (EditorApplication.timeSinceStartup < probeReadyAt && EditorApplication.timeSinceStartup < probeDeadline) return;
                }
                ProbeReport.Add((surfaces > 0 ? "SURFACE_OBSERVED" : "FAIL") + "|" + water.id + "|areaWaterSurfaces=" + surfaces);
                CaptureRemoteHydrology();
                probeIndex++; AdvanceRemoteSiteProbe(); return;
            }
            var site = ProbeSites[probeIndex];
            int visible = 0;
            foreach (var target in UnityEngine.Object.FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (target.featureId == site.siteId || target.featureId == site.sourceSemanticId)
                    foreach (var renderer in target.GetComponentsInChildren<Renderer>(false)) if (renderer.enabled) visible++;
            bool compiled = YQCompiledWorldSiteInstance.IsSiteLoaded(site.siteId) || YQCompiledWorldSiteInstance.IsSiteLoaded(site.sourceSemanticId);
            if (visible == 0 && !compiled && EditorApplication.timeSinceStartup < probeDeadline) return;
            // note: Root publication can precede actor activation; this assisted observer lets that lifecycle settle and never supplies performance evidence.
            if (visible > 0 || compiled)
            {
                if (probeReadyAt == 0d) probeReadyAt = EditorApplication.timeSinceStartup + 5d;
                if (EditorApplication.timeSinceStartup < probeReadyAt && EditorApplication.timeSinceStartup < probeDeadline) return;
            }
            ProbeReport.Add((visible > 0 || compiled ? "PASS" : "FAIL") + "|" + site.siteId + "|kind=" + site.kind + "|visibleRenderers=" + visible + "|compiledLoaded=" + compiled);
            if (YQCompiledWorldSiteInstance.TryGetLastLoadFailure(site.sourceSemanticId, out string loadFailure))
                ProbeReport.Add("PROVIDER_FAILURE|" + site.siteId + "|" + loadFailure);
            int npcs = 0, enemies = 0;
            Vector2 center = new Vector2(site.x, site.z);
            foreach (var npc in UnityEngine.Object.FindObjectsByType<NpcDialogueAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Vector2.Distance(center, new Vector2(npc.transform.position.x, npc.transform.position.z)) <= site.reservedRadius + 32f) npcs++;
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<YQInvestorEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Vector2.Distance(center, new Vector2(enemy.transform.position.x, enemy.transform.position.z)) <= site.reservedRadius + 32f) enemies++;
            ProbeReport.Add("ACTORS_OBSERVED|" + site.siteId + "|nearbyNpcAgents=" + npcs + "|nearbyEnemies=" + enemies + "|scope=active objects in central reserve; not full population or equipment acceptance");
            CaptureRemoteHydrology();
            probeIndex++; AdvanceRemoteSiteProbe();
        } catch (Exception exception) { ProbeReport.Add("FAIL exception=" + exception); FinishRemoteSiteProbe(); }
    }

    private static void CaptureRemoteHydrology()
    {
        // note: The existing observer records actual terrain, meshes and player camera separately from this assisted placement and its narrow root counts.
        string command = Newtonsoft.Json.JsonConvert.SerializeObject(new { action = "hydrology-snapshot",
            profileId = YQProfileSaveSystem.Instance.ActiveProfileId,
            artifactHash = WorldStateManager.Instance.State.generatedWorldPlan.spatialPlanV2.contentHash });
        YQG08R3PhysicalItineraryVerification.Dispatch(command);
    }

    private static void EndProbeOnPlayExit(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) { ProbeReport.Add("INTERRUPTED Play Mode stopped"); FinishRemoteSiteProbe(); }
    }

    private static void FinishRemoteSiteProbe()
    {
        EditorApplication.update -= UpdateRemoteSiteProbe;
        EditorApplication.playModeStateChanged -= EndProbeOnPlayExit;
        // note: Return the actual player before lifting the save barrier, and retain the barrier if the developer snapshot cannot safely restore.
        if (probeMotor != null) SetProbePosition(probePosition, probeRotation);
        bool restored = YQDeveloperTestSession.Restore(out string message);
        ProbeReport.Add("restored=" + restored + " " + message);
        RuntimeModalUiBlocker.Release(ProbeToken);
        Time.timeScale = probeTimeScale;
        Directory.CreateDirectory(DirectoryPath);
        string output = DirectoryPath + "/Remote_Sites_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt";
        File.WriteAllLines(output, ProbeReport);
        Debug.Log("[YQAssetContracts] Remote-site witness finished: " + output);
        probeMotor = null; probeController = null;
    }

    [MenuItem("YourQuest/World Generation/Review Source Contracts and Item Type Coverage")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || YQUrpAssetConversionBatch.IsRunning) return;
        StringBuilder report = new StringBuilder();
        report.AppendLine("YourQuest source asset and generation library review");
        report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
        report.AppendLine("evidence=Edit mode source bindings; no instance repair or save mutation");
        string[] slots = typeof(YQWorldAssetCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.StartsWith("Slot", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
        // note: Read the existing authoritative style set for coverage only; no second style list participates in runtime selection.
        IEnumerable<string> configured = typeof(YQWorldAssetCatalog).GetField("SupportedStyleKeys", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IEnumerable<string>;
        if (configured == null) throw new InvalidOperationException("Authoritative palette styles unavailable.");
        // note: The runtime's internal eligibility gate is reused by this editor-only observer without broadening its production API.
        MethodInfo gateMethod = typeof(YQWorldAssetCatalog).GetMethod("IsAllowedWorldReferenceForSlot", BindingFlags.Static | BindingFlags.NonPublic);
        if (gateMethod == null) throw new InvalidOperationException("Authoritative asset eligibility gate unavailable.");
        var referenceAllowed = (Func<GeneratedAssetReferenceRecord, string, bool>)Delegate.CreateDelegate(typeof(Func<GeneratedAssetReferenceRecord, string, bool>), gateMethod);
        Dictionary<string, HashSet<string>> bound = slots.ToDictionary(s => s, s => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Dictionary<string, HashSet<string>> clean = slots.ToDictionary(s => s, s => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        int failures = 0;
        // note: A source used by many region palettes is inspected once, avoiding repeated expensive prefab dependency loads.
        Dictionary<string, int> sourceResults = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string style in configured.OrderBy(s => s, StringComparer.Ordinal))
        {
            GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
            {
                worldSeed = "asset-contract-review|" + style,
                regions = new List<GeneratedRegionRecord> { new GeneratedRegionRecord { regionId = "review-region", displayName = style, assetStyleKey = style, assetStyleRationale = "Source contract verification." } }
            };
            YQWorldAssetCatalog.EnsureAssetPalettes(plan);
            if (plan.assetPalettes.Count == 0) { failures++; report.AppendLine("FAIL|palette_missing|" + style); continue; }
            GeneratedRegionAssetPaletteRecord palette = plan.assetPalettes[0];
            foreach (string slot in slots)
            {
                List<GeneratedAssetReferenceRecord> candidates = YQWorldAssetCatalog.GetSlotList(palette, slot);
                foreach (GeneratedAssetReferenceRecord candidate in candidates ?? new List<GeneratedAssetReferenceRecord>())
                {
                    if (candidate == null || !referenceAllowed(candidate, slot)) continue;
                    string path = candidate.assetPath;
                    if (!sourceResults.TryGetValue(path, out int invalid))
                    {
                        UnityEngine.Object source = AssetDatabase.LoadMainAssetAtPath(path);
                        invalid = source == null ? -1 : CountInvalidSourceMaterials(source);
                        sourceResults.Add(path, invalid);
                    }
                    if (invalid < 0) { report.AppendLine("FAIL|missing_reference|" + slot + "|" + path); failures++; continue; }
                    bound[slot].Add(path);
                    if (invalid == 0) clean[slot].Add(path);
                    report.AppendLine((invalid == 0 ? "SOURCE_PASS" : "SOURCE_FAIL") + "|" + style + "|" + slot + "|" + path + "|invalidSlots=" + invalid);
                }
            }
        }
        report.AppendLine("ITEM_TYPE_COVERAGE");
        foreach (string slot in slots)
        {
            bool enough = clean[slot].Count >= 3;
            if (!enough) failures++;
            report.AppendLine((enough ? "PASS" : "FAIL") + "|" + slot + "|registered=" + bound[slot].Count + "|sourceClean=" + clean[slot].Count + "|required=3");
        }
        YQRuntimeWorldSiteCatalog sites = Resources.Load<YQRuntimeWorldSiteCatalog>("YQRuntimeWorldSiteCatalog");
        report.AppendLine("STREAMED_SITE_LIBRARY");
        if (sites == null) { failures++; report.AppendLine("FAIL|site_catalog_missing"); }
        else foreach (YQRuntimeWorldSiteRecord site in sites.Sites)
        {
            bool present = !string.IsNullOrEmpty(site.runtimeManifestResourceKey) && Resources.Load<YQReviewedSemanticSiteManifest>(site.runtimeManifestResourceKey) != null;
            report.AppendLine((site.spatiallyValidated && site.seamlessPlacementEligible && present ? "SITE_AVAILABLE" : "SITE_UNAVAILABLE") + "|" + site.kitId + "|kind=" + site.siteKind + "|style=" + site.semanticStyleKey + "|functions=" + string.Join(",", site.reviewedFunctionsV2 ?? new List<YQAssetFunctionV2>()));
        }
        report.AppendLine("RESULT " + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures);
        Directory.CreateDirectory(DirectoryPath);
        string output = DirectoryPath + "/Library_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt";
        File.WriteAllText(output, report.ToString());
        Debug.Log("[YQAssetContracts] Library review " + (failures == 0 ? "PASS" : "FAIL") + ": " + output);
    }

    private static int CountInvalidSourceMaterials(UnityEngine.Object source)
    {
        if (source is Material material) return YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material) ? 0 : 1;
        if (!(source is GameObject prefab)) return 1;
        int invalid = 0;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is BillboardRenderer billboard && (billboard.billboard == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(billboard.billboard.material))) invalid++;
            Material[] materials = renderer.sharedMaterials;
            int required = YQRuntimeUrpMaterialRepair.ResolveRequiredMaterialSlotCount(renderer);
            for (int slot = 0; slot < required; slot++)
                if (YQRuntimeUrpMaterialRepair.IsMaterialSlotRequired(renderer, slot) && !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(slot < materials.Length ? materials[slot] : null)) invalid++;
        }
        return invalid;
    }

    [MenuItem("YourQuest/World Generation/Audit Current Streamed Site Owners")]
    public static void AuditPlay()
    {
        if (!EditorApplication.isPlaying) return;
        // note: Observe existing prepared and loaded owners without requesting loads, moving the player, or mutating site identity.
        StringBuilder report = new StringBuilder();
        report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
        report.AppendLine("released=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
        YQCompiledWorldSiteInstance[] owners = UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (YQCompiledWorldSiteInstance owner in owners)
            report.AppendLine("SITE|" + owner.name + "|loaded=" + owner.IsLoaded + "|active=" + owner.gameObject.activeInHierarchy + "|position=" + owner.transform.position + "|renderers=" + owner.GetComponentsInChildren<Renderer>(true).Length);
        report.AppendLine("owners=" + owners.Length + "|loaded=" + owners.Count(o => o.IsLoaded));
        // note: Approved fallback sites have overlay identities rather than compiled provider components; audit both against the accepted graph.
        GeneratedWorldPlanRecord plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (plan != null && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
        {
            var targets = UnityEngine.Object.FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            report.AppendLine("acceptedSites=" + prepared.SiteCount + "|routes=" + prepared.RouteCount + "|water=" + prepared.WaterCount);
            for (int index = 0; index < prepared.SiteCount; index++)
            {
                var site = prepared.GetSite(index);
                int roots = 0, visibleRenderers = 0;
                foreach (var target in targets)
                {
                    if (target.featureId != site.siteId) continue;
                    roots++;
                    foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                        if (renderer.enabled && renderer.gameObject.activeInHierarchy) visibleRenderers++;
                }
                report.AppendLine("ACCEPTED_SITE|" + site.siteId + "|kind=" + site.kind + "|x=" + site.x + "|z=" + site.z +
                    "|overlayRoots=" + roots + "|visibleRenderers=" + visibleRenderers);
            }
        }
        foreach (Terrain terrain in Terrain.activeTerrains)
            if (terrain.terrainData != null)
                report.AppendLine("DETAIL|" + terrain.name + "|foliage=" + terrain.drawTreesAndFoliage +
                    "|mode=" + terrain.terrainData.detailScatterMode + "|prototypes=" + terrain.terrainData.detailPrototypes.Length +
                    "|distance=" + terrain.detailObjectDistance + "|density=" + terrain.detailObjectDensity);
        // note: Limit payload reads to the three terrains nearest the actual player; prototype presence alone does not prove grass was painted.
        Vector3 playerPosition = YQInvestorPlayerMotor.ActiveMotor != null ? YQInvestorPlayerMotor.ActiveMotor.transform.position : Vector3.zero;
        foreach (Terrain terrain in Terrain.activeTerrains.OrderBy(t => t.terrainData.bounds.SqrDistance(playerPosition - t.transform.position)).Take(3))
        {
            TerrainData data = terrain.terrainData;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                DetailPrototype prototype = data.detailPrototypes[layer];
                int[,] density = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
                long instances = 0;
                int occupied = 0;
                foreach (int count in density) { instances += count; if (count > 0) occupied++; }
                report.AppendLine("DETAIL_PAYLOAD|" + terrain.name + "|layer=" + layer + "|instances=" + instances +
                    "|occupied=" + occupied + "|grid=" + data.detailWidth + "|texture=" + AssetDatabase.GetAssetPath(prototype.prototypeTexture) +
                    "|size=" + prototype.minWidth + "," + prototype.maxWidth + "," + prototype.minHeight + "," + prototype.maxHeight +
                    "|renderMode=" + prototype.renderMode);
            }
        }
        // note: Capture a bounded set of actual leaf-card bindings, including alpha and textures, without assigning a repair.
        int foliageSlots = 0;
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || foliageSlots >= 24) continue;
                string identity = (material.name + " " + renderer.name).ToLowerInvariant();
                if (!identity.Contains("leaf") && !identity.Contains("leaves") && !identity.Contains("needle")) continue;
                foliageSlots++;
                report.AppendLine("FOLIAGE_MATERIAL|" + renderer.name + "|material=" + material.name + "|shader=" + material.shader?.name +
                    "|alphaClip=" + (material.HasProperty("_AlphaClip") ? material.GetFloat("_AlphaClip").ToString() : "n/a") +
                    "|texture=" + AssetDatabase.GetAssetPath(material.mainTexture) + "|source=" + AssetDatabase.GetAssetPath(material));
            }
        }
        Directory.CreateDirectory(DirectoryPath);
        string output = DirectoryPath + "/Play_Sites_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".txt";
        File.WriteAllText(output, report.ToString());
        Debug.Log("[YQAssetContracts] Live site owner audit: " + output);
    }
}
#endif
