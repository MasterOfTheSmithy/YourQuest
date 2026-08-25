using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQRuntimeWorldSiteRecord
{
    public string kitId = string.Empty;
    public string semanticStyleKey = string.Empty;
    public YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;
    public YQSemanticExtractionTopology topology =
        YQSemanticExtractionTopology.Unknown;
    public YQWorldSitePresentationMode presentationMode =
        YQWorldSitePresentationMode.Unknown;
    public YQWorldStructureUsagePolicy structureUsagePolicy =
        YQWorldStructureUsagePolicy.Unspecified;
    public int maximumEnterableStructures;
    public List<string> semanticTags = new List<string>();
    public string runtimeManifestResourceKey = string.Empty;

    // note: Editor compilation measures the complete reviewed active-cell union once; runtime binding consumes this compact contract instead of loading and rescanning every authored pack.
    public string spatialMetadataVersion = string.Empty;
    public bool spatiallyValidated;
    public bool seamlessPlacementEligible;
    public Vector3 authoredFootprintCenter;
    public Vector3 authoredFootprintSize;
    public float authoredFoundationY;
    public float authoredFootprintRadius;
    public int activeCellCount;
    public int activeInstanceCount;
    public string spatialSignature = string.Empty;
    public string spatialValidationFailure = string.Empty;

    [HideInInspector]
    public YQAuthoredSiteStreamingManifest streamingSite;

    [HideInInspector]
    public YQReviewedSemanticSiteManifest semanticSite;
}

public static class YQCompiledWorldSiteBindingService
{
    public const string BindingVersion = "reviewed-site-binding-3-spatial";

    private static YQRuntimeWorldSiteCatalog catalog;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache()
    {
        catalog = null;
    }

    public static bool TryResolveSettlementSite(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        out YQRuntimeWorldSiteRecord selected,
        out bool bindingChanged)
    {
        selected = null;
        bindingChanged = false;
        catalog ??= Resources.Load<YQRuntimeWorldSiteCatalog>(
            "YQRuntimeWorldSiteCatalog");

        if (catalog == null || catalog.Sites.Count == 0 || settlement == null)
            return false;

        if (string.Equals(settlement.runtimeSiteBindingVersion,
                BindingVersion, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(settlement.runtimeSiteKitId))
        {
            selected = catalog.FindByKitId(settlement.runtimeSiteKitId);

            if (IsSettlementCandidate(selected))
                return true;
        }

        string[] intents =
        {
            settlement.siteStyleIntent,
            settlement.siteRoleIntent,
            palette != null ? palette.styleKey : string.Empty,
            region != null ? region.assetStyleKey : string.Empty,
            palette != null ? palette.architecturePack : string.Empty,
            palette != null ? palette.settlementPack : string.Empty,
            settlement.kind,
            settlement.marketBias
        };
        int bestScore = int.MinValue;
        uint bestTie = uint.MaxValue;

        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord candidate = catalog.Sites[index];

            if (!IsSettlementCandidate(candidate))
                continue;

            int score = ScoreCandidate(candidate, intents);
            score += 1800;

            // note: Prefer a different reviewed map for each world location while still allowing deterministic reuse when the catalog is genuinely exhausted.
            if (IsKitAlreadyBound(plan, candidate.kitId,
                    settlement.settlementId))
                score -= 7000;

            uint tie = StableHash(
                (plan != null ? plan.worldSeed : string.Empty) + "|" +
                settlement.deterministicSeed + "|" + candidate.kitId);

            if (score > bestScore ||
                (score == bestScore && tie < bestTie))
            {
                selected = candidate;
                bestScore = score;
                bestTie = tie;
            }
        }

        if (selected == null)
            return false;

        // note: The semantic match becomes persisted save authority; adding another asset pack later cannot silently redesign an accepted settlement.
        settlement.runtimeSiteKitId = selected.kitId;
        settlement.runtimeSiteSemanticStyle = selected.semanticStyleKey;
        settlement.runtimeSiteBindingVersion = BindingVersion;
        bindingChanged = true;
        return true;
    }

    public static bool TryResolveEncampmentSite(
        GeneratedWorldPlanRecord plan,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        out YQRuntimeWorldSiteRecord selected,
        out bool bindingChanged)
    {
        selected = null;
        bindingChanged = false;
        catalog ??= Resources.Load<YQRuntimeWorldSiteCatalog>(
            "YQRuntimeWorldSiteCatalog");

        if (catalog == null || catalog.Sites.Count == 0 || encampment == null)
            return false;

        if (string.Equals(encampment.runtimeSiteBindingVersion,
                BindingVersion, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(encampment.runtimeSiteKitId))
        {
            selected = catalog.FindByKitId(encampment.runtimeSiteKitId);

            if (IsEncampmentCandidate(selected, new[]
                {
                    encampment.siteStyleIntent,
                    encampment.siteRoleIntent,
                    encampment.kind,
                    encampment.layoutIntent,
                    encampment.surfacePresentation,
                    encampment.monsterFamily
                }))
                return true;
        }

        string[] intents =
        {
            encampment.siteStyleIntent,
            encampment.siteRoleIntent,
            encampment.kind,
            encampment.layoutIntent,
            encampment.surfacePresentation,
            encampment.monsterFamily,
            palette != null ? palette.styleKey : string.Empty,
            region != null ? region.assetStyleKey : string.Empty
        };
        int bestScore = int.MinValue;
        uint bestTie = uint.MaxValue;

        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord candidate = catalog.Sites[index];

            // note: Transition-only interiors remain portal destinations and ordinary towns cannot silently become small hostile camps.
            if (!IsEncampmentCandidate(candidate, intents))
                continue;

            int score = ScoreCandidate(candidate, intents);

            if (candidate.siteKind == YQAuthoredSiteKind.Camp)
                score += 2600;
            else if (candidate.siteKind == YQAuthoredSiteKind.SciFiSite)
                score += ContainsAny(intents, "sci", "cyber", "bio", "container")
                    ? 2300
                    : 500;
            else if (candidate.siteKind == YQAuthoredSiteKind.Landmark ||
                     candidate.siteKind == YQAuthoredSiteKind.Wilderness)
                score += 900;

            if (IsKitAlreadyBound(plan, candidate.kitId,
                    encampment.encampmentId))
                score -= 7000;

            uint tie = StableHash(
                (plan != null ? plan.worldSeed : string.Empty) + "|" +
                encampment.deterministicSeed + "|" + candidate.kitId);

            if (score > bestScore ||
                (score == bestScore && tie < bestTie))
            {
                selected = candidate;
                bestScore = score;
                bestTie = tie;
            }
        }

        if (selected == null)
            return false;

        // note: Hostile-site geometry is accepted once and persisted independently from mutable faction prose or threat scaling.
        encampment.runtimeSiteKitId = selected.kitId;
        encampment.runtimeSiteSemanticStyle = selected.semanticStyleKey;
        encampment.runtimeSiteBindingVersion = BindingVersion;
        bindingChanged = true;
        return true;
    }

    private static bool IsSettlementCandidate(YQRuntimeWorldSiteRecord candidate)
    {
        // note: A generated settlement receives a reviewed settlement map, not an arena, ruin, dungeon, or interior that merely shares a style word.
        return candidate != null &&
            candidate.spatiallyValidated &&
            candidate.seamlessPlacementEligible &&
            candidate.presentationMode ==
                YQWorldSitePresentationMode.SeamlessExterior &&
            candidate.siteKind == YQAuthoredSiteKind.Settlement;
    }

    private static bool IsEncampmentCandidate(
        YQRuntimeWorldSiteRecord candidate,
        IReadOnlyList<string> intents)
    {
        if (candidate == null ||
            !candidate.spatiallyValidated ||
            !candidate.seamlessPlacementEligible ||
            candidate.presentationMode !=
            YQWorldSitePresentationMode.SeamlessExterior)
        {
            return false;
        }

        if (candidate.siteKind == YQAuthoredSiteKind.Camp ||
            candidate.siteKind == YQAuthoredSiteKind.Landmark ||
            candidate.siteKind == YQAuthoredSiteKind.Wilderness)
        {
            return true;
        }

        if (candidate.siteKind == YQAuthoredSiteKind.SciFiSite)
            return ContainsAny(intents, "sci", "cyber", "bio", "container");

        // note: A full settlement is valid for an explicitly authored hostile town or stronghold, but never as the fallback for a nest or ordinary camp.
        return candidate.siteKind == YQAuthoredSiteKind.Settlement &&
            ContainsAny(intents, "settlement", "village", "town",
                "stronghold", "fortress", "occupied_city");
    }

    private static bool IsKitAlreadyBound(
        GeneratedWorldPlanRecord plan,
        string kitId,
        string currentLocationId)
    {
        if (plan == null || string.IsNullOrWhiteSpace(kitId))
            return false;

        if (plan.settlements != null)
        {
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];

                if (settlement != null &&
                    !string.Equals(settlement.settlementId, currentLocationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(settlement.runtimeSiteKitId, kitId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (plan.encampments != null)
        {
            for (int index = 0; index < plan.encampments.Count; index++)
            {
                GeneratedEncampmentRecord encampment = plan.encampments[index];

                if (encampment != null &&
                    !string.Equals(encampment.encampmentId, currentLocationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(encampment.runtimeSiteKitId, kitId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int ScoreCandidate(
        YQRuntimeWorldSiteRecord candidate,
        IReadOnlyList<string> intents)
    {
        string candidateStyle = Canonicalize(candidate.semanticStyleKey);
        string candidateKit = Canonicalize(candidate.kitId);
        int score = 0;

        for (int index = 0; index < intents.Count; index++)
        {
            string intent = Canonicalize(intents[index]);

            if (string.IsNullOrWhiteSpace(intent))
                continue;

            if (string.Equals(intent, candidateStyle,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(intent, candidateKit,
                    StringComparison.OrdinalIgnoreCase))
            {
                score += 12000;
                continue;
            }

            if (intent.Contains(candidateStyle) ||
                candidateStyle.Contains(intent) ||
                intent.Contains(candidateKit) ||
                candidateKit.Contains(intent))
            {
                score += 3600;
            }

            score += CountSharedTokens(intent, candidateStyle) * 320;
        }

        return score;
    }

    private static string Canonicalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Trim().ToLowerInvariant()
            .Replace("hivemind_", string.Empty)
            .Replace("_environment", string.Empty)
            .Replace("the_", string.Empty)
            .Replace("scifi", "sci_fi")
            .Replace("cave_tomb", "cave_hidden_tomb")
            .Replace("house_on_hill", "house_on_a_hill")
            .Replace("mountain_messenger", "messenger_mountain");
        char[] characters = normalized.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]))
                characters[index] = '_';
        }

        return new string(characters).Trim('_');
    }

    private static int CountSharedTokens(string left, string right)
    {
        string[] leftTokens = left.Split('_');
        string[] rightTokens = right.Split('_');
        int count = 0;

        for (int leftIndex = 0;
             leftIndex < leftTokens.Length;
             leftIndex++)
        {
            if (leftTokens[leftIndex].Length < 3)
                continue;

            for (int rightIndex = 0;
                 rightIndex < rightTokens.Length;
                 rightIndex++)
            {
                if (string.Equals(leftTokens[leftIndex],
                        rightTokens[rightIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static bool ContainsAny(
        IReadOnlyList<string> values,
        params string[] fragments)
    {
        for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
        {
            string value = values[valueIndex] ?? string.Empty;

            for (int fragmentIndex = 0;
                 fragmentIndex < fragments.Length;
                 fragmentIndex++)
            {
                if (value.IndexOf(
                        fragments[fragmentIndex],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;

            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }

            return hash;
        }
    }
}

[DisallowMultipleComponent]
public sealed class YQCompiledWorldSiteInstance : MonoBehaviour
{
    private static readonly Dictionary<string, YQCompiledWorldSiteInstance>
        Instances = new Dictionary<string, YQCompiledWorldSiteInstance>(
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float>
        ExteriorFoundationCorrectionCache = new Dictionary<string, float>(
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, bool>
        MalformedColliderPrefabCache = new Dictionary<int, bool>();
    private static readonly List<BoxCollider>
        ColliderScanBuffer = new List<BoxCollider>(256);
    private static readonly List<Collider>
        TraversalColliderScanBuffer = new List<Collider>(256);

    private struct SourceColliderSnapshot
    {
        public BoxCollider collider;
        public Vector3 size;
        public bool enabled;
    }

    private string settlementId = string.Empty;
    private string runtimeManifestResourceKey = string.Empty;
    private string expectedKitId = string.Empty;
    private Vector3 authoredOrigin;
    private YQReviewedSemanticSiteManifest manifest;
    private YQWorldSitePresentationMode presentationMode =
        YQWorldSitePresentationMode.Unknown;
    private string[] semanticSliceTags = Array.Empty<string>();
    private HashSet<string> activeCellIds;
    private bool loading;
    private bool loaded;
    private bool loadRejected;
    private float preparedSiteRadius;
    private float loadedSiteRadius;
    private float nextDistanceCheckTime;

    // note: Keep ordinary authored sites outside the origin's startup memory footprint; the curated origin pair is pinned explicitly below.
    private const float LoadDistance = 135f;
    private const float UnloadDistance = 210f;
    private const float DistanceCheckInterval = 0.40f;
    private const float SeamlessSiteRadiusLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.22f;
    private const float SeamlessSiteDimensionLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.45f;
    private const float RuntimeSiteRadiusLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.22f;
    private const float GeneratedTerrainEdgeClearance = 16f;
    private const float StreamingFrameBudgetSeconds = 0.0015f;
    private const int ComplexCellInstanceThreshold = 256;
    private const int CuratedSiteSourceInstanceBudget = 640;
    private const int MaximumDefaultSemanticZones = 3;
    private const float MinimumExteriorFoundationCorrection = 0.35f;
    private const float MaximumExteriorFoundationCorrection = 96f;
    private const float MinimumFoundationRendererFootprint = 0.36f;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeInstances()
    {
        Instances.Clear();
        ExteriorFoundationCorrectionCache.Clear();
        MalformedColliderPrefabCache.Clear();
        ColliderScanBuffer.Clear();
        TraversalColliderScanBuffer.Clear();
    }

    public static IEnumerator MaterializeRoutine(
        Transform settlementRoot,
        GeneratedSettlementRecord settlement,
        YQRuntimeWorldSiteRecord record,
        Action<bool> completed)
    {
        yield return MaterializeRoutine(
            settlementRoot,
            settlement != null ? settlement.settlementId : string.Empty,
            record,
            completed);
    }

    public static IEnumerator MaterializeRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        Action<bool> completed)
    {
        if (siteRoot == null || string.IsNullOrWhiteSpace(locationId) ||
            record == null ||
            string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey))
        {
            completed?.Invoke(false);
            yield break;
        }

        if (Application.isPlaying)
        {
            // note: Validate the lightweight reviewed contract before the world builder counts this streamed location as successfully materialized; distant cell geometry remains unloaded.
            yield return PrepareValidatedSiteRoutine(
                siteRoot,
                locationId,
                record,
                Array.Empty<string>(),
                completed);
            yield break;
        }

        // note: Only the selected reviewed site and its prefab dependencies enter memory; the other 28 world packs remain unloaded.
        ResourceRequest request = Resources.LoadAsync<
            YQReviewedSemanticSiteManifest>(
                record.runtimeManifestResourceKey);
        yield return request;
        YQReviewedSemanticSiteManifest selectedManifest =
            request.asset as YQReviewedSemanticSiteManifest;

        if (selectedManifest == null || !selectedManifest.ReleaseEligible ||
            !string.Equals(selectedManifest.KitId, record.kitId,
                StringComparison.OrdinalIgnoreCase))
        {
            completed?.Invoke(false);
            yield break;
        }

        HashSet<string> selectedCellIds = BuildActiveCellIds(
            selectedManifest,
            Array.Empty<string>());
        if (!TryValidateSelectedSite(
                selectedManifest,
                selectedCellIds,
                record.presentationMode,
                siteRoot,
                out Vector3 origin,
                out float _unusedRadius,
                out string validationFailure))
        {
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] MATERIALIZATION REJECTED\n" +
                "Location: " + locationId + "\n" +
                "Reviewed site: " + record.kitId + "\n" +
                "Reason: " + validationFailure);
            completed?.Invoke(false);
            yield break;
        }

        YQAuthoredSiteStreamingManifest streaming =
            selectedManifest.StreamingSite;
        int spawned = 0;
        float frameStartedAt = Time.realtimeSinceStartup;

        if (streaming != null)
        {
            for (int index = 0; index < streaming.Cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

                if (cell == null || cell.CellPrefab == null ||
                    (selectedCellIds != null &&
                     !selectedCellIds.Contains(cell.StableCellId)))
                    continue;

                GameObject instance = Instantiate(
                    cell.CellPrefab,
                    siteRoot,
                    false);
                instance.name = "CompiledCell__" + cell.StableCellId;
                instance.transform.localPosition =
                    cell.AuthoredLocalPosition - origin;
                instance.transform.localRotation = Quaternion.identity;
                spawned++;

                // note: A reviewed cell can contain hundreds of authored roots, so yield by measured complexity or elapsed frame budget rather than by cell count alone.
                if (cell.SourceInstanceCount >= ComplexCellInstanceThreshold ||
                    Time.realtimeSinceStartup - frameStartedAt >=
                        StreamingFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }
        else
        {
            for (int index = 0;
                 index < selectedManifest.Zones.Count;
                 index++)
            {
                YQReviewedSemanticZoneRecord zone =
                    selectedManifest.Zones[index];

                if (zone == null || zone.prefab == null)
                    continue;

                GameObject instance = Instantiate(
                    zone.prefab,
                    siteRoot,
                    false);
                instance.name = "CompiledZone__" + zone.stableId;
                instance.transform.localPosition =
                    zone.authoredSourceOrigin - origin;
                instance.transform.localRotation = Quaternion.identity;
                spawned++;

                if (Time.realtimeSinceStartup - frameStartedAt >=
                    StreamingFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        if (spawned == 0)
        {
            completed?.Invoke(false);
            yield break;
        }

        YQCompiledWorldSiteInstance site =
            siteRoot.gameObject.AddComponent<
                YQCompiledWorldSiteInstance>();
        site.Configure(locationId, selectedManifest, origin);
        // note: Direct materialization publishes transforms naturally on the next physics step; callers do not need a full-scene loading sync.
        yield return null;
        completed?.Invoke(true);
    }

    public static IEnumerator MaterializeSemanticSliceRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        Action<bool> completed)
    {
        if (!Application.isPlaying)
        {
            yield return MaterializeRoutine(
                siteRoot, locationId, record, completed);
            yield break;
        }

        if (siteRoot == null || string.IsNullOrWhiteSpace(locationId) ||
            record == null ||
            string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey))
        {
            completed?.Invoke(false);
            yield break;
        }

        // note: A semantic slice preserves complete authored cells while excluding unrelated encounter districts from a curated landmark composition.
        yield return PrepareValidatedSiteRoutine(
            siteRoot,
            locationId,
            record,
            requiredSemanticTags,
            completed);
    }

    private static IEnumerator PrepareValidatedSiteRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        Action<bool> completed)
    {
        // note: Resource validation is asynchronous and instantiates no authored cell, preventing an invalid distant site from being accepted merely because a streaming component exists.
        ResourceRequest request = Resources.LoadAsync<
            YQReviewedSemanticSiteManifest>(
            record.runtimeManifestResourceKey);
        yield return request;
        YQReviewedSemanticSiteManifest selectedManifest =
            request.asset as YQReviewedSemanticSiteManifest;

        if (selectedManifest == null || !selectedManifest.ReleaseEligible ||
            !string.Equals(selectedManifest.KitId, record.kitId,
                StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] PREPARE REJECTED\n" +
                "Location: " + locationId + "\n" +
                "Reviewed site: " + record.kitId + "\n" +
                "Reason: reviewed runtime manifest is missing, unreleased, or mismatched.");
            completed?.Invoke(false);
            yield break;
        }

        HashSet<string> selectedCellIds = BuildActiveCellIds(
            selectedManifest,
            requiredSemanticTags);
        if (!TryValidateSelectedSite(
                selectedManifest,
                selectedCellIds,
                record.presentationMode,
                siteRoot,
                out Vector3 _unusedOrigin,
                out float validatedRadius,
                out string validationFailure))
        {
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] PREPARE REJECTED\n" +
                "Location: " + locationId + "\n" +
                "Reviewed site: " + record.kitId + "\n" +
                "Reason: " + validationFailure);
            completed?.Invoke(false);
            yield break;
        }

        YQCompiledWorldSiteInstance streamingSite =
            siteRoot.gameObject.GetComponent<YQCompiledWorldSiteInstance>() ??
            siteRoot.gameObject.AddComponent<YQCompiledWorldSiteInstance>();
        streamingSite.Prepare(
            locationId,
            record.runtimeManifestResourceKey,
            record.kitId,
            record.presentationMode,
            requiredSemanticTags,
            validatedRadius);
        completed?.Invoke(true);
    }

    private void Prepare(
        string locationId,
        string resourceKey,
        string kitId,
        YQWorldSitePresentationMode newPresentationMode,
        string[] requiredSemanticTags = null,
        float validatedRadius = 0f)
    {
        settlementId = locationId ?? string.Empty;
        runtimeManifestResourceKey = resourceKey ?? string.Empty;
        expectedKitId = kitId ?? string.Empty;
        presentationMode = newPresentationMode;
        semanticSliceTags = requiredSemanticTags != null
            ? (string[])requiredSemanticTags.Clone()
            : Array.Empty<string>();
        activeCellIds = null;
        // note: The caller has already validated this exact aggregate radius; preserving it verbatim prevents clamping malformed data into an accepted streaming contract.
        preparedSiteRadius = validatedRadius;
        loadedSiteRadius = 0f;
        manifest = null;
        authoredOrigin = Vector3.zero;
        loaded = false;
        loading = false;
        loadRejected = false;
        // note: Stable per-site phasing prevents every prepared settlement from running its distance check on the same frame.
        nextDistanceCheckTime = Time.unscaledTime +
            StableDistanceCheckPhase(settlementId);
        Instances[settlementId] = this;
    }

    private static float StableDistanceCheckPhase(string stableId)
    {
        unchecked
        {
            uint hash = 2166136261;
            string value = stableId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619;
            }

            // note: Ten-bit phasing is deterministic, allocation-free, and sufficiently disperses all currently supported world-site counts.
            return (hash & 1023u) / 1024f * DistanceCheckInterval;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextDistanceCheckTime)
            return;

        nextDistanceCheckTime = Time.unscaledTime + DistanceCheckInterval;
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;

        if (motor == null || !motor.IsAuthoritative)
            return;

        float distanceSquared =
            (motor.transform.position - transform.position).sqrMagnitude;

        bool pinnedOriginSite = settlementId.StartsWith(
            "origin_", StringComparison.OrdinalIgnoreCase);
        float loadRadius = LoadDistance + preparedSiteRadius;

        if (!loaded && !loading && !loadRejected &&
            (pinnedOriginSite ||
             distanceSquared <= loadRadius * loadRadius))
        {
            StartCoroutine(LoadPreparedSiteRoutine());
        }
        else if (loaded && !loading &&
                 !pinnedOriginSite &&
                 distanceSquared >=
                    (UnloadDistance + loadedSiteRadius) *
                    (UnloadDistance + loadedSiteRadius))
        {
            UnloadPreparedSite();
        }
    }

    private IEnumerator LoadPreparedSiteRoutine()
    {
        loading = true;
        ResourceRequest request = Resources.LoadAsync<
            YQReviewedSemanticSiteManifest>(runtimeManifestResourceKey);
        yield return request;
        YQReviewedSemanticSiteManifest selectedManifest =
            request.asset as YQReviewedSemanticSiteManifest;

        if (selectedManifest == null || !selectedManifest.ReleaseEligible ||
            !string.Equals(selectedManifest.KitId, expectedKitId,
                StringComparison.OrdinalIgnoreCase))
        {
            loading = false;
            loadRejected = true;
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] STREAM LOAD REJECTED " +
                settlementId + " -> " + expectedKitId);
            yield break;
        }

        manifest = selectedManifest;
        activeCellIds = BuildActiveCellIds(
            selectedManifest,
            semanticSliceTags);
        if (!TryValidateSelectedSite(
                selectedManifest,
                activeCellIds,
                presentationMode,
                transform,
                out authoredOrigin,
                out loadedSiteRadius,
                out string validationFailure))
        {
            // note: A rejected spatial contract is terminal for this prepared instance; retrying every distance poll would reload the same broken pack and spam the player log.
            manifest = null;
            loading = false;
            loadRejected = true;
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] STREAM LOAD REJECTED\n" +
                "Location: " + settlementId + "\n" +
                "Reviewed site: " + expectedKitId + "\n" +
                "Reason: " + validationFailure);
            yield break;
        }

        // note: Reviewed geometry keeps its authored transform; seamless exterior streaming may add one terrain-support patch before publishing the site as loaded.

        YQAuthoredSiteStreamingManifest streaming =
            selectedManifest.StreamingSite;
        int spawned = 0;
        int sanitizedColliderCount = 0;
        int traversalColliderCount = 0;
        int disabledReflectionProbeCount = 0;
        int removedPreviewArtifactCount = 0;
        float frameStartedAt = Time.realtimeSinceStartup;
        // note: Cells are assembled under an inactive staging root so malformed vendor LOD ownership can be repaired before Unity enables any LODGroup.
        GameObject contentRoot = new GameObject("CompiledSiteContent");
        contentRoot.SetActive(false);
        contentRoot.transform.SetParent(transform, false);

        if (streaming != null)
        {
            for (int index = 0; index < streaming.Cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

                if (cell == null || cell.CellPrefab == null)
                    continue;

                if (activeCellIds != null &&
                    !activeCellIds.Contains(cell.StableCellId))
                    continue;

                GameObject instance = null;
                int repairedColliders = 0;
                // note: A reviewed cell can contain hundreds of authored objects; Unity's asynchronous clone path keeps that hierarchy copy from monopolizing the Goddess/loading frame.
                yield return InstantiateReviewedPrefabRoutine(
                    cell.CellPrefab,
                    contentRoot.transform,
                    expectedKitId,
                    cell.SourceInstanceCount,
                    (created, repaired) =>
                    {
                        instance = created;
                        repairedColliders = repaired;
                    });

                sanitizedColliderCount += repairedColliders;
                if (instance == null)
                    continue;
                instance.name = "CompiledCell__" + cell.StableCellId;
                instance.transform.localPosition =
                    cell.AuthoredLocalPosition - authoredOrigin;
                instance.transform.localRotation = Quaternion.identity;
                // note: Known source-pack preview props are removed while the cell is still hidden so they cannot leak into the curated generated environment.
                yield return CurateKnownPreviewArtifactsRoutine(
                    instance,
                    expectedKitId,
                    count => removedPreviewArtifactCount += count);
                int cellTraversalColliders = 0;
                int cellReflectionProbes = 0;
                yield return SanitizeStreamedCellRoutine(
                    instance,
                    (colliders, probes) =>
                    {
                        cellTraversalColliders = colliders;
                        cellReflectionProbes = probes;
                    });
                traversalColliderCount += cellTraversalColliders;
                disabledReflectionProbeCount += cellReflectionProbes;
                spawned++;

                // note: Yield immediately after a complex authored cell or whenever this streaming slice has consumed its small main-thread budget.
                if (cell.SourceInstanceCount >= ComplexCellInstanceThreshold ||
                    Time.realtimeSinceStartup - frameStartedAt >=
                        StreamingFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }
        else
        {
            for (int index = 0;
                 index < selectedManifest.Zones.Count;
                 index++)
            {
                YQReviewedSemanticZoneRecord zone =
                    selectedManifest.Zones[index];

                if (zone == null || zone.prefab == null)
                    continue;

                GameObject instance = null;
                int repairedColliders = 0;
                // note: Legacy semantic zones use the same non-blocking clone boundary as reviewed streaming cells.
                yield return InstantiateReviewedPrefabRoutine(
                    zone.prefab,
                    contentRoot.transform,
                    expectedKitId,
                    zone.sourceInstanceCount,
                    (created, repaired) =>
                    {
                        instance = created;
                        repairedColliders = repaired;
                    });

                sanitizedColliderCount += repairedColliders;
                if (instance == null)
                    continue;
                instance.name = "CompiledZone__" + zone.stableId;
                instance.transform.localPosition =
                    zone.authoredSourceOrigin - authoredOrigin;
                instance.transform.localRotation = Quaternion.identity;
                yield return CurateKnownPreviewArtifactsRoutine(
                    instance,
                    expectedKitId,
                    count => removedPreviewArtifactCount += count);
                int zoneTraversalColliders = 0;
                int zoneReflectionProbes = 0;
                yield return SanitizeStreamedCellRoutine(
                    instance,
                    (colliders, probes) =>
                    {
                        zoneTraversalColliders = colliders;
                        zoneReflectionProbes = probes;
                    });
                traversalColliderCount += zoneTraversalColliders;
                disabledReflectionProbeCount += zoneReflectionProbes;
                spawned++;

                if (Time.realtimeSinceStartup - frameStartedAt >=
                    StreamingFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        // note: Do not publish the loaded state until LOD repair, exterior foundation alignment, and physics synchronization are complete; origin setup and NPC placement consume IsSiteLoaded as a readiness contract.
        bool contentSpawned = spawned > 0;

        if (contentSpawned)
        {
            // note: Keep the potentially expensive site-wide LOD ownership pass out of the same frame that instantiated the final authored cell.
            yield return null;
            // note: Repair renderer ownership once across the assembled site so duplicate LOD references spanning separate authored cells are also removed.
            yield return RepairDuplicateLodOwnershipRoutine(
                contentRoot);

            float foundationCorrection = 0f;
            bool hasFoundationCorrection = false;
            string foundationCorrectionSource = string.Empty;

            if (presentationMode ==
                    YQWorldSitePresentationMode.SeamlessExterior &&
                !UsesAuthoredTerrainRelief())
            {
                string foundationCacheKey = BuildFoundationCacheKey();

                if (TryResolveCompiledFoundationCorrection(
                        streaming,
                        activeCellIds,
                        authoredOrigin.y,
                        out foundationCorrection))
                {
                    hasFoundationCorrection = true;
                    foundationCorrectionSource = "reviewed structural metadata";
                    ExteriorFoundationCorrectionCache[foundationCacheKey] =
                        foundationCorrection;
                }
                else if (TryResolveAuthoredDatumCorrection(
                             streaming,
                             activeCellIds,
                             authoredOrigin.y,
                             out foundationCorrection))
                {
                    // note: Older reviewed manifests predate structural-floor metadata; their cell datum still preserves the source scene's intended zero-height construction plane.
                    hasFoundationCorrection = true;
                    foundationCorrectionSource = "authored cell datum";
                    ExteriorFoundationCorrectionCache[foundationCacheKey] =
                        foundationCorrection;
                }
                else if (ExteriorFoundationCorrectionCache.TryGetValue(
                             foundationCacheKey,
                             out foundationCorrection))
                {
                    // note: A cached zero means this reviewed slice was already measured as aligned; do not rescan it whenever distance streaming reloads the town.
                    hasFoundationCorrection = foundationCorrection >=
                        MinimumExteriorFoundationCorrection;
                    foundationCorrectionSource = "runtime correction cache";
                }
                else
                {
                    bool runtimeCorrectionResolved = false;
                    float runtimeCorrection = 0f;
                    yield return TryResolveExteriorFoundationCorrectionRoutine(
                        contentRoot,
                        (success, value) =>
                        {
                            runtimeCorrectionResolved = success;
                            runtimeCorrection = value;
                        });
                    hasFoundationCorrection = runtimeCorrectionResolved;
                    foundationCorrection = runtimeCorrection;
                    foundationCorrectionSource =
                        hasFoundationCorrection
                            ? "runtime structural bounds"
                            : string.Empty;
                    ExteriorFoundationCorrectionCache[foundationCacheKey] =
                        hasFoundationCorrection ? foundationCorrection : 0f;
                }
            }

            if (hasFoundationCorrection)
            {
                // note: The terrain prepass is canonical and wilderness already sampled it; lower the reviewed assembly to that surface instead of raising late terrain pillars beneath floating source geometry.
                contentRoot.transform.position +=
                    Vector3.down * foundationCorrection;
            }

            int groundedCellCount = 0;
            if (presentationMode ==
                    YQWorldSitePresentationMode.SeamlessExterior &&
                !UsesAuthoredTerrainRelief())
            {
                Terrain generatedTerrain = ResolveGeneratedTerrain(
                    contentRoot.transform.position);
                if (generatedTerrain != null)
                {
                    if (string.Equals(
                            expectedKitId,
                            "witch_house",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        // note: Witch House is a furnished structure embedded high and off-center inside a large source demo cell; curate that coherent cluster directly onto Vey's prepared terrain pad.
                        yield return CurateAndGroundWitchHouseRoutine(
                            contentRoot,
                            generatedTerrain,
                            (grounded, removed) =>
                            {
                                groundedCellCount = grounded;
                                removedPreviewArtifactCount += removed;
                            });
                    }
                    else
                    {
                        yield return AlignCompiledCellsToTerrainRoutine(
                            contentRoot,
                            generatedTerrain,
                            count => groundedCellCount = count);
                    }
                }
            }

            Debug.Log(
                "[YQCompiledWorldSiteInstance] SITE GROUNDING READY\n" +
                "Location: " + settlementId + "\n" +
                "Reviewed site: " + expectedKitId + "\n" +
                "Correction source: " + foundationCorrectionSource + "\n" +
                "Assembly lowering: " +
                (hasFoundationCorrection
                    ? foundationCorrection.ToString("F2")
                    : "0.00") + "m\n" +
                "Cells grounded to canonical terrain: " + groundedCellCount);

            // note: Players never see a floating intermediate frame; authored content becomes visible only after support terrain and LOD ownership are stable.
            yield return ActivateHierarchyCooperativelyRoutine(
                contentRoot);
        }
        else
        {
            Destroy(contentRoot);
            loadRejected = true;
        }

        // note: Newly enabled streamed colliders join the next normal physics step; forcing a full-scene synchronization here caused a loading hitch and was immediately repeated by actor relocation.
        yield return null;
        loaded = contentSpawned;
        loading = false;
        // note: Persisted actors already retain authoritative world positions; rescanning every EntityInfo and forcing physics whenever a site streams in caused both loading and traversal hitches.
        // note: Distance streaming already bounds this site's lifetime; rescanning the whole generated world here previously allocated a renderer table large enough to stall dense authored maps.
        Debug.Log(
            "[YQCompiledWorldSiteInstance] SITE STREAMED IN\n" +
            "Location: " + settlementId + "\n" +
            "Reviewed site: " + expectedKitId + "\n" +
            "Cells/zones: " + spawned + "\n" +
            "Malformed vendor colliders disabled: " +
            sanitizedColliderCount + "\n" +
            "Traversal obstruction colliders disabled: " +
            traversalColliderCount + "\n" +
            "Source preview artifacts removed: " +
            removedPreviewArtifactCount + "\n" +
            "Imported reflection probes disabled: " +
            disabledReflectionProbeCount);
    }

    private static IEnumerator ActivateHierarchyCooperativelyRoutine(
        GameObject contentRoot)
    {
        if (contentRoot == null)
            yield break;

        List<GameObject> originallyActive =
            new List<GameObject>();
        Stack<Transform> pending =
            new Stack<Transform>();

        for (int childIndex = contentRoot.transform.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            pending.Push(
                contentRoot.transform.GetChild(childIndex));
        }

        float frameStartedAt =
            Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current =
                pending.Pop();

            for (int childIndex = current.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                pending.Push(
                    current.GetChild(childIndex));
            }

            GameObject currentObject =
                current.gameObject;

            if (currentObject.activeSelf)
            {
                originallyActive.Add(
                    currentObject);
                currentObject.SetActive(
                    false);
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: Capture authored active-state intent cooperatively while the staging root is hidden, so even very large sites cannot monopolize a loading frame.
                yield return null;
                frameStartedAt =
                    Time.realtimeSinceStartup;
            }
        }

        // note: With every originally active descendant temporarily disabled, enabling the empty staging root cannot register the complete site in one frame.
        contentRoot.SetActive(
            true);
        yield return null;
        frameStartedAt =
            Time.realtimeSinceStartup;

        for (int index = 0;
             index < originallyActive.Count;
             index++)
        {
            GameObject currentObject =
                originallyActive[index];

            if (currentObject != null)
            {
                // note: Pre-order traversal restores parents before descendants, preserving authored activeSelf state while renderer, collider, and behaviour registration stays frame-budgeted.
                currentObject.SetActive(
                    true);
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt =
                    Time.realtimeSinceStartup;
            }
        }
    }

    private string BuildFoundationCacheKey()
    {
        if (semanticSliceTags == null || semanticSliceTags.Length == 0)
            return expectedKitId + "|full";

        string[] orderedTags = (string[])semanticSliceTags.Clone();
        Array.Sort(orderedTags, StringComparer.OrdinalIgnoreCase);
        // note: Semantic slices from one asset pack can have different support planes, so cache by the normalized reviewed tag selection rather than by pack identity alone.
        return expectedKitId + "|" + string.Join("|", orderedTags);
    }

    private static bool TryResolveCompiledFoundationCorrection(
        YQAuthoredSiteStreamingManifest streaming,
        HashSet<string> selectedCellIds,
        float selectedAuthoredOriginY,
        out float correction)
    {
        correction = 0f;

        if (streaming == null || selectedCellIds == null ||
            selectedCellIds.Count == 0)
        {
            return false;
        }

        List<Vector2> samples = new List<Vector2>();
        float totalWeight = 0f;
        int selectedCount = 0;

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

            if (cell == null ||
                !selectedCellIds.Contains(cell.StableCellId))
            {
                continue;
            }

            selectedCount++;

            if (!cell.HasStructuralFoundation ||
                !IsFinite(cell.AuthoredStructuralFoundationY) ||
                !IsFinite(cell.StructuralFoundationWeight) ||
                cell.StructuralFoundationWeight <= 0f)
            {
                // note: Mixed old/new cell metadata falls back as one unit so a partially upgraded manifest cannot bias the complete site toward only its rebuilt cells.
                return false;
            }

            samples.Add(new Vector2(
                cell.AuthoredStructuralFoundationY,
                cell.StructuralFoundationWeight));
            totalWeight += cell.StructuralFoundationWeight;
        }

        if (selectedCount != selectedCellIds.Count || samples.Count == 0 ||
            !IsFinite(totalWeight) || totalWeight <= 0f)
        {
            return false;
        }

        samples.Sort((left, right) => left.x.CompareTo(right.x));
        float targetWeight = totalWeight * 0.5f;
        float accumulatedWeight = 0f;
        float authoredStructuralFloor = 0f;

        for (int index = 0; index < samples.Count; index++)
        {
            accumulatedWeight += samples[index].y;

            if (accumulatedWeight < targetWeight)
                continue;

            authoredStructuralFloor = samples[index].x;
            break;
        }

        float candidate = authoredStructuralFloor - selectedAuthoredOriginY;

        if (!IsFinite(candidate) ||
            candidate < MinimumExteriorFoundationCorrection ||
            candidate > MaximumExteriorFoundationCorrection)
        {
            return false;
        }

        correction = candidate;
        return true;
    }

    private static bool TryResolveAuthoredDatumCorrection(
        YQAuthoredSiteStreamingManifest streaming,
        HashSet<string> selectedCellIds,
        float selectedAuthoredOriginY,
        out float correction)
    {
        correction = 0f;

        if (streaming == null || selectedCellIds == null ||
            selectedCellIds.Count == 0)
        {
            return false;
        }

        List<float> sourceDatums = new List<float>();

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

            if (cell == null ||
                !selectedCellIds.Contains(cell.StableCellId) ||
                !IsFinite(cell.AuthoredLocalPosition.y))
            {
                continue;
            }

            sourceDatums.Add(cell.AuthoredLocalPosition.y);
        }

        if (sourceDatums.Count != selectedCellIds.Count)
            return false;

        sourceDatums.Sort();
        float authoredDatum = sourceDatums[sourceDatums.Count / 2];
        float candidate = authoredDatum - selectedAuthoredOriginY;

        // note: A positive gap means low backdrop/terrain bounds pulled the aggregate origin beneath the authored construction datum; move the whole reviewed assembly, never individual buildings.
        if (!IsFinite(candidate) ||
            candidate < MinimumExteriorFoundationCorrection ||
            candidate > MaximumExteriorFoundationCorrection)
        {
            return false;
        }

        correction = candidate;
        return true;
    }

    private IEnumerator TryResolveExteriorFoundationCorrectionRoutine(
        GameObject contentRoot,
        Action<bool, float> completed)
    {
        if (contentRoot == null)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        List<Renderer> renderers = new List<Renderer>();
        bool initialized = false;
        Bounds contentBounds = new Bounds();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(contentRoot.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;

            Renderer[] localRenderers = current.GetComponents<Renderer>();
            for (int index = 0; index < localRenderers.Length; index++)
            {
                Renderer renderer = localRenderers[index];

                if (!IsFoundationRenderer(renderer))
                    continue;

                renderers.Add(renderer);

                if (!initialized)
                {
                    contentBounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    contentBounds.Encapsulate(renderer.bounds);
                }
            }

            for (int index = 0; index < current.childCount; index++)
                pending.Push(current.GetChild(index));

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: Even the legacy metadata fallback scans a reviewed cell cooperatively so it cannot freeze the loading presentation.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (!initialized || !IsFiniteVector(contentBounds.center) ||
            !IsFiniteVector(contentBounds.size))
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        float lowerBandCeiling = contentBounds.min.y + Mathf.Min(
            12f,
            Mathf.Max(2f, contentBounds.size.y * 0.35f));
        List<Vector2> samples = new List<Vector2>();
        float totalWeight = 0f;

        for (int index = 0; index < renderers.Count; index++)
        {
            Renderer renderer = renderers[index];

            if (!IsFoundationRenderer(renderer))
                continue;

            Bounds bounds = renderer.bounds;
            float footprint = bounds.size.x * bounds.size.z;

            if (!IsFinite(footprint) ||
                footprint < MinimumFoundationRendererFootprint ||
                bounds.min.y > lowerBandCeiling)
            {
                continue;
            }

            // note: Square-root weighting lets real floors, walls, steps, and platforms outvote scattered grass and debris without allowing one oversized backdrop renderer to dictate the site elevation.
            float weight = Mathf.Sqrt(footprint);
            float localBottom = bounds.min.y - transform.position.y;

            if (!IsFinite(localBottom) || !IsFinite(weight) || weight <= 0f)
                continue;

            samples.Add(new Vector2(localBottom, weight));
            totalWeight += weight;

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: Foundation sampling obeys the same small frame budget as hierarchy discovery.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (samples.Count == 0 || !IsFinite(totalWeight) || totalWeight <= 0f)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        samples.Sort((left, right) => left.x.CompareTo(right.x));
        float targetWeight = totalWeight * 0.5f;
        float accumulatedWeight = 0f;
        float dominantBottom = 0f;

        for (int index = 0; index < samples.Count; index++)
        {
            accumulatedWeight += samples[index].y;

            if (accumulatedWeight < targetWeight)
                continue;

            dominantBottom = samples[index].x;
            break;
        }

        // note: Only correct a credible missing-foundation gap; negative offsets are authored basements/foundations and extreme offsets indicate a pack that requires explicit review instead of a runtime guess.
        if (!IsFinite(dominantBottom) ||
            dominantBottom < MinimumExteriorFoundationCorrection ||
            dominantBottom > MaximumExteriorFoundationCorrection)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        completed?.Invoke(true, dominantBottom);
    }

    private static bool IsFoundationRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled ||
            renderer is ParticleSystemRenderer ||
            renderer is TrailRenderer ||
            renderer is LineRenderer)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        return IsFiniteVector(bounds.center) &&
            IsFiniteVector(bounds.size) &&
            bounds.size.x > 0.05f &&
            bounds.size.y > 0.01f &&
            bounds.size.z > 0.05f;
    }

    private void UnloadPreparedSite()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);

            if (child.name.StartsWith("CompiledCell__",
                    StringComparison.Ordinal) ||
                child.name.StartsWith("CompiledZone__",
                    StringComparison.Ordinal) ||
                string.Equals(child.name, "CompiledSiteContent",
                    StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        // note: Clearing the selected manifest reference allows Unity to reclaim that pack after its instantiated cells are gone; the lightweight catalog remains resident.
        manifest = null;
        authoredOrigin = Vector3.zero;
        activeCellIds = null;
        loadedSiteRadius = 0f;
        loaded = false;
        // note: Resources.UnloadUnusedAssets scans the complete live object graph and can stall gameplay even when its AsyncOperation is used; reclamation is deferred to controlled scene/loading boundaries.
        Debug.Log(
            "[YQCompiledWorldSiteInstance] SITE STREAMED OUT " +
            settlementId + " -> " + expectedKitId);
    }

    private void RelocateExistingActors()
    {
        EntityInfo[] entities = FindObjectsByType<EntityInfo>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < entities.Length; index++)
        {
            EntityInfo entity = entities[index];

            if (entity == null || !HasLocationTag(entity.tags, settlementId))
                continue;

            string seed = settlementId + "|streamed_actor|" + entity.entityId;
            string role = entity.hostility + " " + entity.displayName;

            if (TryResolveRolePosition(role, seed, index,
                    out Vector3 position))
            {
                entity.transform.position = position;
            }
        }

        // note: Actor transforms are consumed by the next normal physics step; relocation never forces a global synchronization.
    }

    private static bool HasLocationTag(string[] tags, string locationId)
    {
        if (tags == null || string.IsNullOrWhiteSpace(locationId))
            return false;

        for (int index = 0; index < tags.Length; index++)
        {
            if (string.Equals(tags[index], locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveResidentPosition(
        string targetSettlementId,
        GeneratedNpcPlanRecord npc,
        string seed,
        int index,
        out Vector3 position)
    {
        position = default;

        return Instances.TryGetValue(
                targetSettlementId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            site != null &&
            site.TryResolveResidentPosition(npc, seed, index, out position);
    }

    public static bool TryResolveWorldActorPosition(
        string locationId,
        string roleIntent,
        string seed,
        int index,
        out Vector3 position)
    {
        position = default;

        return Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            site != null &&
            site.TryResolveRolePosition(roleIntent, seed, index, out position);
    }

    public static bool HasSite(string locationId)
    {
        return Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            site != null;
    }

    public static bool IsSiteLoaded(string locationId)
    {
        return Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            site != null && site.loaded;
    }

    public static bool TryProjectToSiteSurface(
        string targetSettlementId,
        Vector3 candidate,
        out Vector3 projected)
    {
        projected = candidate;

        return Instances.TryGetValue(
                targetSettlementId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            site != null &&
            site.TryProjectToSurface(candidate, out projected);
    }

    private void Configure(
        string newSettlementId,
        YQReviewedSemanticSiteManifest newManifest,
        Vector3 newAuthoredOrigin)
    {
        settlementId = newSettlementId ?? string.Empty;
        manifest = newManifest;
        authoredOrigin = newAuthoredOrigin;
        loaded = true;
        loading = false;
        Instances[settlementId] = this;
    }

    private void OnDestroy()
    {
        if (Instances.TryGetValue(settlementId,
                out YQCompiledWorldSiteInstance existing) &&
            existing == this)
        {
            Instances.Remove(settlementId);
        }
    }

    private bool TryResolveResidentPosition(
        GeneratedNpcPlanRecord npc,
        string seed,
        int index,
        out Vector3 position)
    {
        position = default;

        if (manifest == null || manifest.Zones.Count == 0)
            return false;

        string role = (npc != null ? npc.role : string.Empty) + " " +
            (npc != null ? npc.archetype : string.Empty);
        return TryResolveRolePosition(role, seed, index, out position);
    }

    private bool TryResolveRolePosition(
        string role,
        string seed,
        int index,
        out Vector3 position)
    {
        position = default;

        if (manifest == null || manifest.Zones.Count == 0)
            return false;

        YQReviewedSemanticZoneRecord zone = SelectRoleZone(role, seed);

        if (zone == null)
            return false;

        Vector3 localCenter = zone.authoredSourceOrigin +
            zone.localBoundsCenter - authoredOrigin;
        Vector3 extents = zone.localBoundsSize * 0.5f;

        for (int attempt = 0; attempt < 24; attempt++)
        {
            float x = Mathf.Lerp(-0.72f, 0.72f,
                Deterministic01(seed + "|site_x|" + index + "|" + attempt));
            float z = Mathf.Lerp(-0.72f, 0.72f,
                Deterministic01(seed + "|site_z|" + index + "|" + attempt));
            Vector3 candidate = transform.TransformPoint(
                localCenter + new Vector3(
                    x * Mathf.Max(2f, extents.x),
                    0f,
                    z * Mathf.Max(2f, extents.z)));

            if (TryProjectToSurface(candidate, out position))
                return true;
        }

        return false;
    }

    private YQReviewedSemanticZoneRecord SelectRoleZone(
        string roleIntent,
        string seed)
    {
        int bestScore = int.MinValue;
        uint bestTie = uint.MaxValue;
        YQReviewedSemanticZoneRecord best = null;
        string role = (roleIntent ?? string.Empty).ToLowerInvariant();

        for (int index = 0; index < manifest.Zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = manifest.Zones[index];

            if (zone == null || !IsZoneActive(zone) ||
                !IsPlausibleZone(zone, authoredOrigin.y))
                continue;

            string tags = string.Join(" ", zone.semanticTags).ToLowerInvariant();
            int score = tags.Contains("residential") ||
                tags.Contains("core") ? 30 : 0;

            if ((role.Contains("merchant") || role.Contains("smith") ||
                 role.Contains("inn")) &&
                (tags.Contains("market") || tags.Contains("commerce") ||
                 tags.Contains("service")))
            {
                score += 80;
            }

            if ((role.Contains("entrance") || role.Contains("portal") ||
                 role.Contains("route")) &&
                (tags.Contains("entrance") || tags.Contains("route") ||
                 tags.Contains("circulation") ||
                 tags.Contains("transition")))
            {
                score += 100;
            }

            if ((role.Contains("goddess") || role.Contains("landmark") ||
                 role.Contains("vista")) &&
                (tags.Contains("landmark") || tags.Contains("vista")))
            {
                score += 120;
            }

            if ((role.Contains("alchemy") || role.Contains("service") ||
                 role.Contains("room")) &&
                (tags.Contains("service") || tags.Contains("room") ||
                 tags.Contains("interior")))
            {
                score += 100;
            }

            if (role.Contains("guard") &&
                (tags.Contains("civic") || tags.Contains("perimeter") ||
                 tags.Contains("gate")))
            {
                score += 80;
            }

            if ((role.Contains("hostile") || role.Contains("leader") ||
                 role.Contains("enemy") || role.Contains("boss")) &&
                (tags.Contains("encounter") || tags.Contains("core") ||
                 tags.Contains("camp") || tags.Contains("arena")))
            {
                score += 90;
            }

            if ((role.Contains("reward") || role.Contains("loot") ||
                 role.Contains("cache")) &&
                (tags.Contains("reward") || tags.Contains("interior") ||
                 tags.Contains("core")))
            {
                score += 90;
            }

            uint tie = StableHash(seed + "|" + zone.stableId);

            if (score > bestScore ||
                (score == bestScore && tie < bestTie))
            {
                best = zone;
                bestScore = score;
                bestTie = tie;
            }
        }

        return best;
    }

    private bool TryProjectToSurface(Vector3 candidate, out Vector3 projected)
    {
        projected = candidate;
        RaycastHit[] hits = Physics.RaycastAll(
            candidate + Vector3.up * 120f,
            Vector3.down,
            260f,
            ~0,
            QueryTriggerInteraction.Ignore);
        float maximumResidentHeight = transform.position.y + 6f;
        float bestHeight = float.MinValue;

        for (int index = 0; index < hits.Length; index++)
        {
            RaycastHit hit = hits[index];

            if (hit.collider == null || hit.normal.y < 0.55f ||
                hit.point.y > maximumResidentHeight ||
                !hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.point.y > bestHeight)
            {
                bestHeight = hit.point.y;
                projected = hit.point + Vector3.up * 0.08f;
            }
        }

        return bestHeight > float.MinValue;
    }

    private static bool TryValidateSelectedSite(
        YQReviewedSemanticSiteManifest selectedManifest,
        HashSet<string> allowedCellIds,
        YQWorldSitePresentationMode selectedPresentationMode,
        Transform siteRoot,
        out Vector3 origin,
        out float radius,
        out string failure)
    {
        origin = Vector3.zero;
        radius = 0f;
        failure = string.Empty;

        if (selectedManifest == null)
        {
            failure = "reviewed semantic manifest is null.";
            return false;
        }

        YQAuthoredSiteStreamingManifest streaming =
            selectedManifest.StreamingSite;
        bool initialized = false;
        Bounds aggregateBounds = new Bounds(Vector3.zero, Vector3.one);
        List<float> groundCandidates = new List<float>();
        HashSet<string> validatedCellIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (streaming != null)
        {
            if (allowedCellIds == null || allowedCellIds.Count == 0)
            {
                failure = "semantic selection contains no active streaming cells.";
                return false;
            }

            for (int index = 0; index < streaming.Cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

                if (cell == null ||
                    !allowedCellIds.Contains(cell.StableCellId))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cell.StableCellId) ||
                    !validatedCellIds.Add(cell.StableCellId))
                {
                    failure = "selected streaming cell has an empty or duplicate stable ID.";
                    return false;
                }

                if (cell.CellPrefab == null)
                {
                    failure = "selected cell " + cell.StableCellId +
                        " has no runtime prefab.";
                    return false;
                }

                if (!IsFiniteVector(cell.AuthoredLocalPosition) ||
                    !IsPlausibleBounds(
                        cell.LocalBoundsCenter,
                        cell.LocalBoundsSize))
                {
                    failure = "selected cell " + cell.StableCellId +
                        " has nonfinite or invalid authored placement bounds.";
                    return false;
                }

                Vector3 authoredCenter = cell.AuthoredLocalPosition +
                    cell.LocalBoundsCenter;
                if (!IsFiniteVector(authoredCenter))
                {
                    failure = "selected cell " + cell.StableCellId +
                        " overflows its authored world-space center.";
                    return false;
                }

                Bounds cellBounds = new Bounds(
                    authoredCenter,
                    cell.LocalBoundsSize);
                IncludeValidatedBounds(
                    cellBounds,
                    ref aggregateBounds,
                    ref initialized,
                    groundCandidates);
            }

            if (!validatedCellIds.SetEquals(allowedCellIds))
            {
                failure = "semantic selection references a missing or invalid streaming cell.";
                return false;
            }
        }
        else
        {
            HashSet<string> validatedZoneIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                 index < selectedManifest.Zones.Count;
                 index++)
            {
                YQReviewedSemanticZoneRecord zone =
                    selectedManifest.Zones[index];

                if (zone == null ||
                    !ZoneOverlapsAllowedCells(zone, allowedCellIds))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(zone.stableId) ||
                    !validatedZoneIds.Add(zone.stableId) ||
                    zone.prefab == null ||
                    !IsFiniteVector(zone.authoredSourceOrigin) ||
                    !IsPlausibleBounds(
                        zone.localBoundsCenter,
                        zone.localBoundsSize))
                {
                    failure = "selected legacy zone has invalid identity, prefab, or authored bounds.";
                    return false;
                }

                Vector3 authoredCenter = zone.authoredSourceOrigin +
                    zone.localBoundsCenter;
                if (!IsFiniteVector(authoredCenter))
                {
                    failure = "selected legacy zone overflows its authored world-space center.";
                    return false;
                }

                Bounds zoneBounds = new Bounds(
                    authoredCenter,
                    zone.localBoundsSize);
                IncludeValidatedBounds(
                    zoneBounds,
                    ref aggregateBounds,
                    ref initialized,
                    groundCandidates);
            }
        }

        if (!initialized || groundCandidates.Count == 0)
        {
            failure = "selected semantic site has no valid authored geometry.";
            return false;
        }

        // note: The median floor remains deterministic, but unlike the previous implementation every selected cell has already passed the same spatial contract and none can be spawned outside this aggregate.
        groundCandidates.Sort();
        float authoredFloor =
            groundCandidates[groundCandidates.Count / 2];
        origin = new Vector3(
            aggregateBounds.center.x,
            authoredFloor,
            aggregateBounds.center.z);
        Vector3 horizontalExtents = new Vector3(
            aggregateBounds.extents.x,
            0f,
            aggregateBounds.extents.z);
        radius = horizontalExtents.magnitude;

        if (!IsFiniteVector(origin) || !IsFinite(radius) || radius <= 0f)
        {
            failure = "aggregate authored origin or radius is invalid.";
            return false;
        }

        if (radius > RuntimeSiteRadiusLimit)
        {
            // note: Transition-only sites are isolated vertically, not exempt from memory and precision limits; kilometre-scale context/backdrop cells must be explicitly segmented or quarantined before runtime.
            failure = "reviewed runtime footprint radius " +
                radius.ToString("F1") + "m exceeds the safe " +
                RuntimeSiteRadiusLimit.ToString("F1") +
                "m runtime envelope.";
            return false;
        }

        if (selectedPresentationMode ==
            YQWorldSitePresentationMode.SeamlessExterior)
        {
            if (aggregateBounds.size.x > SeamlessSiteDimensionLimit ||
                aggregateBounds.size.z > SeamlessSiteDimensionLimit ||
                aggregateBounds.size.y >
                    YQGeneratedWorldTerrain.TerrainHeight * 2f ||
                radius > SeamlessSiteRadiusLimit)
            {
                failure = "reviewed exterior footprint " +
                    aggregateBounds.size.ToString("F1") +
                    " (radius " + radius.ToString("F1") +
                    "m) exceeds the safe " +
                    SeamlessSiteRadiusLimit.ToString("F1") +
                    "m generated-terrain envelope.";
                return false;
            }

            if (siteRoot != null && !FitsGeneratedTerrainAtAnchor(
                    siteRoot,
                    aggregateBounds,
                    origin))
            {
                failure = "reviewed exterior footprint crosses the generated " +
                    "terrain boundary at anchor " +
                    siteRoot.position.ToString("F1") + ".";
                return false;
            }
        }

        return true;
    }

    private static void IncludeValidatedBounds(
        Bounds candidate,
        ref Bounds aggregate,
        ref bool initialized,
        List<float> groundCandidates)
    {
        // note: One aggregate is the shared authority for origin, footprint validation, streaming distance, and placement acceptance.
        groundCandidates.Add(candidate.min.y);

        if (!initialized)
        {
            aggregate = candidate;
            initialized = true;
            return;
        }

        aggregate.Encapsulate(candidate);
    }

    private static bool FitsGeneratedTerrainAtAnchor(
        Transform siteRoot,
        Bounds authoredBounds,
        Vector3 authoredOrigin)
    {
        float limit = YQGeneratedWorldTerrain.WorldSize * 0.5f -
            GeneratedTerrainEdgeClearance;
        float minX = authoredBounds.min.x - authoredOrigin.x;
        float maxX = authoredBounds.max.x - authoredOrigin.x;
        float minZ = authoredBounds.min.z - authoredOrigin.z;
        float maxZ = authoredBounds.max.z - authoredOrigin.z;
        Vector3[] corners =
        {
            new Vector3(minX, 0f, minZ),
            new Vector3(minX, 0f, maxZ),
            new Vector3(maxX, 0f, minZ),
            new Vector3(maxX, 0f, maxZ)
        };

        for (int index = 0; index < corners.Length; index++)
        {
            Vector3 world = siteRoot.TransformPoint(corners[index]);

            if (!IsFiniteVector(world) ||
                Mathf.Abs(world.x) > limit ||
                Mathf.Abs(world.z) > limit)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> BuildActiveCellIds(
        YQReviewedSemanticSiteManifest selectedManifest,
        IReadOnlyList<string> requiredTags)
    {
        if (selectedManifest == null ||
            selectedManifest.StreamingSite == null)
            return null;

        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        bool filterByTags = requiredTags != null &&
            requiredTags.Count > 0;

        if (!filterByTags && selectedManifest.SourceInstanceCount >
            CuratedSiteSourceInstanceBudget)
        {
            // note: Oversized source scenes are asset libraries, not finished generated locations; publish a deterministic reviewed district slice instead of dumping every demo zone into one POI.
            return BuildCuratedDefaultCellIds(selectedManifest);
        }

        for (int zoneIndex = 0;
             zoneIndex < selectedManifest.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone =
                selectedManifest.Zones[zoneIndex];

            if (zone == null ||
                (filterByTags && !ContainsRequiredTag(
                    zone.semanticTags, requiredTags)))
            {
                continue;
            }

            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                selected.Add(zone.streamingCellIds[cellIndex]);
            }
        }

        // note: Even an unsliced runtime site is the union of reviewed semantic cells, never every raw source-scene cell left in its streaming manifest.
        return selected;
    }

    private static HashSet<string> BuildCuratedDefaultCellIds(
        YQReviewedSemanticSiteManifest selectedManifest)
    {
        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<YQReviewedSemanticZoneRecord> candidates =
            new List<YQReviewedSemanticZoneRecord>();

        for (int index = 0; index < selectedManifest.Zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = selectedManifest.Zones[index];
            if (zone != null && zone.streamingCellIds != null &&
                zone.streamingCellIds.Count > 0)
            {
                candidates.Add(zone);
            }
        }

        candidates.Sort((left, right) =>
        {
            int scoreComparison = ResolveZoneCurationScore(right).CompareTo(
                ResolveZoneCurationScore(left));
            if (scoreComparison != 0)
                return scoreComparison;

            int sizeComparison = Mathf.Max(0, left.sourceInstanceCount).
                CompareTo(Mathf.Max(0, right.sourceInstanceCount));
            return sizeComparison != 0
                ? sizeComparison
                : string.Compare(
                    left.stableId,
                    right.stableId,
                    StringComparison.OrdinalIgnoreCase);
        });

        int retainedInstances = 0;
        int retainedZones = 0;
        string retainedZoneNames = string.Empty;

        for (int index = 0;
             index < candidates.Count &&
             retainedZones < MaximumDefaultSemanticZones;
             index++)
        {
            YQReviewedSemanticZoneRecord zone = candidates[index];
            int zoneInstances = Mathf.Max(1, zone.sourceInstanceCount);
            if (zoneInstances > CuratedSiteSourceInstanceBudget -
                retainedInstances)
            {
                continue;
            }

            AddZoneCellIds(zone, selected);
            retainedInstances += zoneInstances;
            retainedZones++;
            retainedZoneNames += (retainedZoneNames.Length > 0 ? ", " : "") +
                (!string.IsNullOrWhiteSpace(zone.displayName)
                    ? zone.displayName
                    : zone.stableId);
        }

        if (selected.Count == 0 && candidates.Count > 0)
        {
            YQReviewedSemanticZoneRecord smallest = candidates[0];
            for (int index = 1; index < candidates.Count; index++)
            {
                if (candidates[index].sourceInstanceCount <
                    smallest.sourceInstanceCount)
                {
                    smallest = candidates[index];
                }
            }

            // note: A reviewed site with no zone under budget still receives its smallest coherent authored unit rather than failing or combining unrelated zones.
            AddZoneCellIds(smallest, selected);
            retainedInstances = Mathf.Max(1, smallest.sourceInstanceCount);
            retainedZoneNames = !string.IsNullOrWhiteSpace(smallest.displayName)
                ? smallest.displayName
                : smallest.stableId;
        }

        Debug.Log(
            "[YQCompiledWorldSiteInstance] DEFAULT CURATED SLICE\n" +
            "Reviewed site: " + selectedManifest.KitId + "\n" +
            "Source instances: " + selectedManifest.SourceInstanceCount +
            "\nRetained instances: " + retainedInstances +
            "\nSemantic zones: " + retainedZoneNames);
        return selected;
    }

    private static void AddZoneCellIds(
        YQReviewedSemanticZoneRecord zone,
        HashSet<string> selected)
    {
        for (int index = 0; index < zone.streamingCellIds.Count; index++)
            selected.Add(zone.streamingCellIds[index]);
    }

    private static int ResolveZoneCurationScore(
        YQReviewedSemanticZoneRecord zone)
    {
        string identity = ((zone.displayName ?? string.Empty) + " " +
            (zone.stableId ?? string.Empty) + " " +
            string.Join(" ", zone.semanticTags ?? new List<string>())).
            ToLowerInvariant();
        int score = Mathf.Min(8, Mathf.Max(0, zone.authoredBuildingCount)) *
            24;

        // note: The default slice favors recognizable civic/residential anchors and connective space, while source-pack perimeter/support dumps are low-priority dressing.
        if (identity.Contains("central") || identity.Contains("core"))
            score += 90;
        if (identity.Contains("civic") || identity.Contains("poi"))
            score += 75;
        if (identity.Contains("residential") || identity.Contains("market"))
            score += 60;
        if (identity.Contains("entrance") || identity.Contains("approach"))
            score += 50;
        if (identity.Contains("circulation"))
            score += 35;
        if (identity.Contains("service") || identity.Contains("support"))
            score -= 25;
        if (identity.Contains("perimeter"))
            score -= 60;

        score -= Mathf.Max(0, zone.sourceInstanceCount) / 24;
        return score;
    }

    private bool IsZoneActive(YQReviewedSemanticZoneRecord zone)
    {
        return ZoneOverlapsAllowedCells(zone, activeCellIds);
    }

    private static bool ZoneOverlapsAllowedCells(
        YQReviewedSemanticZoneRecord zone,
        HashSet<string> allowedCellIds)
    {
        if (zone == null)
            return false;

        if (allowedCellIds == null)
            return true;

        for (int index = 0; index < zone.streamingCellIds.Count; index++)
        {
            if (allowedCellIds.Contains(zone.streamingCellIds[index]))
                return true;
        }

        return false;
    }

    private static bool ContainsRequiredTag(
        IReadOnlyList<string> availableTags,
        IReadOnlyList<string> requiredTags)
    {
        if (availableTags == null)
            return false;

        for (int requiredIndex = 0;
             requiredIndex < requiredTags.Count;
             requiredIndex++)
        {
            for (int availableIndex = 0;
                 availableIndex < availableTags.Count;
                 availableIndex++)
            {
                if (string.Equals(
                        requiredTags[requiredIndex],
                        availableTags[availableIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPlausibleBounds(Vector3 center, Vector3 size)
    {
        return IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z) &&
            IsFinite(size.x) && IsFinite(size.y) && IsFinite(size.z) &&
            size.x > 0.001f && size.y > 0.001f && size.z > 0.001f &&
            size.x <= 2048f && size.y <= 256f && size.z <= 2048f;
    }

    private static bool IsPlausibleZone(
        YQReviewedSemanticZoneRecord zone,
        float floorHeight)
    {
        if (!IsPlausibleBounds(zone.localBoundsCenter, zone.localBoundsSize))
            return false;

        float centerHeight = zone.authoredSourceOrigin.y +
            zone.localBoundsCenter.y;
        return Mathf.Abs(centerHeight - floorHeight) <= 256f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFiniteVector(Vector3 value)
    {
        // note: Unity vectors do not reject NaN/Infinity at assignment time, so spatial contracts validate every component before bounds math or spawning.
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static IEnumerator RepairDuplicateLodOwnershipRoutine(
        GameObject root)
    {
        if (root == null)
            yield break;

        List<LODGroup> gatheredGroups = new List<LODGroup>();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float gatherFrameStartedAt = Time.realtimeSinceStartup;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            LODGroup[] localGroups = current.GetComponents<LODGroup>();
            for (int groupIndex = 0;
                 groupIndex < localGroups.Length;
                 groupIndex++)
            {
                gatheredGroups.Add(localGroups[groupIndex]);
            }
            if (Time.realtimeSinceStartup - gatherFrameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: Discover dense vendor LOD hierarchies incrementally; the former site-wide component query produced a visible loading-frame spike before repair even began.
                yield return null;
                gatherFrameStartedAt = Time.realtimeSinceStartup;
            }
        }

        LODGroup[] groups = gatheredGroups.ToArray();
        Array.Sort(groups, (left, right) =>
            GetTransformDepth(right.transform).CompareTo(
                GetTransformDepth(left.transform)));
        HashSet<Renderer> claimed = new HashSet<Renderer>();
        float frameStartedAt =
            Time.realtimeSinceStartup;

        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            LOD[] lods = groups[groupIndex].GetLODs();
            bool changed = false;

            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] source = lods[lodIndex].renderers;
                List<Renderer> unique = new List<Renderer>(source.Length);

                for (int rendererIndex = 0;
                     rendererIndex < source.Length;
                     rendererIndex++)
                {
                    Renderer renderer = source[rendererIndex];

                    if (renderer != null && claimed.Add(renderer))
                        unique.Add(renderer);
                }

                if (unique.Count != source.Length)
                {
                    lods[lodIndex].renderers = unique.ToArray();
                    changed = true;
                }
            }

            // note: Preserve authored LOD geometry for performance while replacing unreliable vendor transition distances with stable, non-fading runtime thresholds.
            if (NormalizeRuntimeLodThresholds(lods, out LOD[] stableLods))
            {
                lods = stableLods;
                changed = true;
            }

            if (changed)
            {
                // note: The deepest authored LOD group owns a renderer; parent groups retain only renderers not already claimed by their children.
                groups[groupIndex].SetLODs(lods);
                groups[groupIndex].RecalculateBounds();
            }

            groups[groupIndex].fadeMode = LODFadeMode.None;
            groups[groupIndex].animateCrossFading = false;

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: Dense vendor sites can contain hundreds of LOD groups; repair them cooperatively while the assembled site remains invisible.
                yield return null;
                frameStartedAt =
                    Time.realtimeSinceStartup;
            }
        }
    }

    private bool UsesAuthoredTerrainRelief()
    {
        // note: The origin Goddess statue is authored on a mountain profile supplied by the deterministic terrain prepass; uniform foundation lowering would detach the statue from that relief.
        return string.Equals(
            settlementId,
            "origin_goddess_threshold",
            StringComparison.OrdinalIgnoreCase);
    }

    private static Terrain ResolveGeneratedTerrain(Vector3 worldPosition)
    {
        Terrain[] terrains = Terrain.activeTerrains;
        Terrain nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        for (int index = 0; terrains != null && index < terrains.Length; index++)
        {
            Terrain terrain = terrains[index];
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            bool contains = worldPosition.x >= origin.x &&
                worldPosition.x <= origin.x + size.x &&
                worldPosition.z >= origin.z &&
                worldPosition.z <= origin.z + size.z;
            if (contains)
                return terrain;

            Vector3 center = origin + new Vector3(size.x, 0f, size.z) * 0.5f;
            float distanceSquared = new Vector2(
                worldPosition.x - center.x,
                worldPosition.z - center.z).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = terrain;
            }
        }

        // note: Multi-terrain worlds prefer containment; the nearest active terrain is a safe fallback for an anchor sitting exactly on a tile seam.
        return nearest;
    }

    private static bool NormalizeRuntimeLodThresholds(
        LOD[] lods,
        out LOD[] stableLods)
    {
        stableLods = lods;
        if (lods == null || lods.Length == 0)
            return false;

        stableLods = new LOD[lods.Length];
        bool changed = false;
        float previousThreshold = 1f;

        for (int index = 0; index < lods.Length; index++)
        {
            float desiredThreshold = GetStableRuntimeLodThreshold(
                index,
                previousThreshold);
            stableLods[index] = lods[index];
            stableLods[index].screenRelativeTransitionHeight =
                desiredThreshold;
            changed |= !Mathf.Approximately(
                lods[index].screenRelativeTransitionHeight,
                desiredThreshold);
            previousThreshold = desiredThreshold;
        }

        return changed;
    }

    private static float GetStableRuntimeLodThreshold(
        int lodIndex,
        float previousThreshold)
    {
        // note: These conservative tiers keep full geometry close to the player, switch cleanly without cross-fade transparency, and permit dense reviewed towns to reduce draw cost at distance.
        float desired = lodIndex switch
        {
            0 => 0.18f,
            1 => 0.075f,
            2 => 0.03f,
            3 => 0.012f,
            _ => Mathf.Max(0.0015f, previousThreshold * 0.42f)
        };

        return Mathf.Min(desired, previousThreshold - 0.0005f);
    }

    private static GameObject InstantiateReviewedPrefab(
        GameObject prefab,
        Transform parent,
        ref int sanitizedColliderCount)
    {
        if (prefab == null)
            return null;

        int prefabId = prefab.GetInstanceID();
        if (!MalformedColliderPrefabCache.TryGetValue(
                prefabId,
                out bool containsMalformedColliders))
        {
            // note: Source prefab collider validity is immutable during play; cache it so a streamed site reload never rescans the same authored hierarchy.
            containsMalformedColliders = ContainsMalformedBoxCollider(prefab);
            MalformedColliderPrefabCache[prefabId] = containsMalformedColliders;
        }
        bool previousLogging = Debug.unityLogger.logEnabled;
        GameObject instance;

        try
        {
            // note: Unity emits one warning per malformed vendor collider during cloning; suppress only that synchronous clone and report the repaired count once per streamed site.
            if (containsMalformedColliders)
                Debug.unityLogger.logEnabled = false;

            instance = Instantiate(prefab, parent, false);
        }
        finally
        {
            Debug.unityLogger.logEnabled = previousLogging;
        }

        sanitizedColliderCount += SanitizeMalformedBoxColliders(instance);
        // note: Reviewed source materials remain authoritative; this scoped pass changes only missing/unsupported residual slots and avoids any scene-wide renderer scan.
        YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(instance);
        return instance;
    }

    private static IEnumerator InstantiateReviewedPrefabRoutine(
        GameObject prefab,
        Transform parent,
        string kitId,
        int sourceInstanceCount,
        Action<GameObject, int> completed)
    {
        if (prefab == null)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        if (string.Equals(
                kitId,
                "witch_house",
                StringComparison.OrdinalIgnoreCase))
        {
            GameObject curatedCell = null;
            int curatedRepairs = 0;
            yield return InstantiateCuratedWitchHouseCellRoutine(
                prefab,
                parent,
                (created, repairs) =>
                {
                    curatedCell = created;
                    curatedRepairs = repairs;
                });
            if (curatedCell != null)
            {
                yield return YQRuntimeUrpMaterialRepair.
                    RepairMaterialHierarchyRoutine(curatedCell, null);
                completed?.Invoke(curatedCell, curatedRepairs);
                yield break;
            }
        }

        if (sourceInstanceCount >= ComplexCellInstanceThreshold &&
            prefab.transform.childCount > 1)
        {
            GameObject fragmentedCell = null;
            int fragmentedRepairs = 0;
            yield return InstantiateFragmentedReviewedCellRoutine(
                prefab,
                parent,
                (created, repairs) =>
                {
                    fragmentedCell = created;
                    fragmentedRepairs = repairs;
                });
            if (fragmentedCell != null)
            {
                yield return YQRuntimeUrpMaterialRepair.
                    RepairMaterialHierarchyRoutine(fragmentedCell, null);
                completed?.Invoke(fragmentedCell, fragmentedRepairs);
                yield break;
            }
        }

        GameObject instance = null;
        List<SourceColliderSnapshot> colliderSnapshots =
            new List<SourceColliderSnapshot>();
        // note: Repair malformed vendor collider source data cooperatively before cloning; this keeps dense towns on Unity's asynchronous path instead of forcing one blocking Instantiate frame.
        yield return PreparePrefabCollidersForAsyncCloneRoutine(
            prefab,
            colliderSnapshots);

        AsyncInstantiateOperation<GameObject> operation =
            UnityEngine.Object.InstantiateAsync(
                prefab,
                parent);

        // note: Async clone work is lower priority than the frame that animates and types the loading presentation.
        operation.priority =
            -1;

        yield return operation;

        RestorePrefabColliderSource(colliderSnapshots);

        if (operation.Result != null &&
            operation.Result.Length > 0)
        {
            instance =
                operation.Result[0];
        }

        if (instance == null)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        // note: Keep hierarchy integration and material validation on separate rendered frames; cloned colliders already inherited the repaired source values.
        yield return null;
        yield return YQRuntimeUrpMaterialRepair.
            RepairMaterialHierarchyRoutine(
                instance,
                null);

        completed?.Invoke(
            instance,
            colliderSnapshots.Count);
    }

    private static IEnumerator InstantiateCuratedWitchHouseCellRoutine(
        GameObject prefab,
        Transform parent,
        Action<GameObject, int> completed)
    {
        Transform sourceRoot = prefab != null ? prefab.transform : null;
        if (sourceRoot == null || sourceRoot.childCount == 0)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        Vector3 structuralPositionSum = Vector3.zero;
        int structuralRoots = 0;
        float minimumStructuralY = float.PositiveInfinity;
        float maximumStructuralY = float.NegativeInfinity;
        for (int index = 0; index < sourceRoot.childCount; index++)
        {
            Transform child = sourceRoot.GetChild(index);
            if (child == null || !IsWitchHouseStructuralRoot(child.name))
                continue;

            structuralPositionSum += child.localPosition;
            minimumStructuralY = Mathf.Min(
                minimumStructuralY,
                child.localPosition.y);
            maximumStructuralY = Mathf.Max(
                maximumStructuralY,
                child.localPosition.y);
            structuralRoots++;
        }

        if (structuralRoots == 0)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        Vector3 clusterCenter = structuralPositionSum / structuralRoots;
        const float clusterRadius = 27f;
        float minimumY = minimumStructuralY - 5f;
        float maximumY = maximumStructuralY + 16f;
        GameObject container = new GameObject(prefab.name + "__Curated");
        container.transform.SetParent(parent, false);
        int spawned = 0;
        int repairedColliders = 0;
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int index = 0; index < sourceRoot.childCount; index++)
        {
            Transform sourceChild = sourceRoot.GetChild(index);
            if (sourceChild == null || sourceChild.name.StartsWith(
                    "SM_big_rock",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Vector2 offset = new Vector2(
                sourceChild.localPosition.x - clusterCenter.x,
                sourceChild.localPosition.z - clusterCenter.z);
            bool withinCluster = offset.sqrMagnitude <=
                clusterRadius * clusterRadius &&
                sourceChild.localPosition.y >= minimumY &&
                sourceChild.localPosition.y <= maximumY;
            if (!withinCluster)
                continue;

            GameObject instance = null;
            int repaired = 0;
            yield return InstantiateSourceChildRoutine(
                sourceChild,
                container.transform,
                (created, repairs) =>
                {
                    instance = created;
                    repaired = repairs;
                });
            if (instance != null)
            {
                spawned++;
                repairedColliders += repaired;
            }

            // note: The 656-object source demo is never cloned wholesale; retained hut roots are integrated only while the loading frame remains inside its tight budget.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (spawned == 0)
        {
            Destroy(container);
            completed?.Invoke(null, 0);
            yield break;
        }

        Debug.Log(
            "[YQCompiledWorldSiteInstance] WITCH HOUSE PREFILTERED\n" +
            "Source roots: " + sourceRoot.childCount + "\n" +
            "Retained coherent roots: " + spawned);
        completed?.Invoke(container, repairedColliders);
    }

    private static IEnumerator InstantiateFragmentedReviewedCellRoutine(
        GameObject prefab,
        Transform parent,
        Action<GameObject, int> completed)
    {
        Transform sourceRoot = prefab != null ? prefab.transform : null;
        if (sourceRoot == null || sourceRoot.childCount == 0)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        GameObject container = new GameObject(prefab.name + "__Fragmented");
        container.transform.SetParent(parent, false);
        int spawned = 0;
        int repairedColliders = 0;
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int index = 0; index < sourceRoot.childCount; index++)
        {
            Transform sourceChild = sourceRoot.GetChild(index);
            if (sourceChild == null)
                continue;

            GameObject instance = null;
            int repaired = 0;
            yield return InstantiateSourceChildRoutine(
                sourceChild,
                container.transform,
                (created, repairs) =>
                {
                    instance = created;
                    repaired = repairs;
                });
            if (instance != null)
            {
                spawned++;
                repairedColliders += repaired;
            }

            // note: Dense reviewed cells are reconstructed from their authored root instances under a strict frame budget, preserving layout without one monolithic hierarchy integration spike.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (spawned == 0)
        {
            Destroy(container);
            completed?.Invoke(null, 0);
            yield break;
        }

        completed?.Invoke(container, repairedColliders);
    }

    private static IEnumerator InstantiateSourceChildRoutine(
        Transform sourceChild,
        Transform parent,
        Action<GameObject, int> completed)
    {
        if (sourceChild == null || parent == null)
        {
            completed?.Invoke(null, 0);
            yield break;
        }

        string sourceName = sourceChild.name;
        Vector3 sourcePosition = sourceChild.localPosition;
        Quaternion sourceRotation = sourceChild.localRotation;
        Vector3 sourceScale = sourceChild.localScale;
        List<SourceColliderSnapshot> snapshots =
            new List<SourceColliderSnapshot>();
        yield return PreparePrefabCollidersForAsyncCloneRoutine(
            sourceChild.gameObject,
            snapshots);

        AsyncInstantiateOperation<GameObject> operation =
            UnityEngine.Object.InstantiateAsync(
                sourceChild.gameObject,
                parent);
        // note: Even an unusually complex authored root clones through Unity's background-capable path; no single source object is allowed to force a synchronous loading-frame copy.
        operation.priority = -1;
        yield return operation;
        RestorePrefabColliderSource(snapshots);

        GameObject instance = operation.Result != null &&
            operation.Result.Length > 0
                ? operation.Result[0]
                : null;
        if (instance != null)
        {
            instance.name = sourceName;
            instance.transform.localPosition = sourcePosition;
            instance.transform.localRotation = sourceRotation;
            instance.transform.localScale = sourceScale;
        }

        completed?.Invoke(instance, snapshots.Count);
    }

    private static IEnumerator PreparePrefabCollidersForAsyncCloneRoutine(
        GameObject prefab,
        List<SourceColliderSnapshot> snapshots)
    {
        if (prefab == null || snapshots == null)
            yield break;

        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(prefab.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            BoxCollider[] colliders =
                current.GetComponents<BoxCollider>();
            for (int colliderIndex = 0;
                 colliderIndex < colliders.Length;
                 colliderIndex++)
            {
                BoxCollider collider = colliders[colliderIndex];
                if (!IsMalformedBoxCollider(collider))
                    continue;

                snapshots.Add(new SourceColliderSnapshot
                {
                    collider = collider,
                    size = collider.size,
                    enabled = collider.enabled
                });
                Vector3 repairedSize = collider.size;
                repairedSize.x = Mathf.Abs(repairedSize.x);
                repairedSize.y = Mathf.Abs(repairedSize.y);
                repairedSize.z = Mathf.Abs(repairedSize.z);
                collider.size = repairedSize;
                Vector3 scale = collider.transform.lossyScale;
                if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
                    collider.enabled = false;
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    private static void RestorePrefabColliderSource(
        List<SourceColliderSnapshot> snapshots)
    {
        for (int index = 0; index < snapshots.Count; index++)
        {
            SourceColliderSnapshot snapshot = snapshots[index];
            if (snapshot.collider == null)
                continue;

            snapshot.collider.size = snapshot.size;
            snapshot.collider.enabled = snapshot.enabled;
        }
    }

    private static bool ContainsMalformedBoxCollider(GameObject root)
    {
        if (root == null)
            return false;

        ColliderScanBuffer.Clear();
        root.GetComponentsInChildren(true, ColliderScanBuffer);

        for (int index = 0; index < ColliderScanBuffer.Count; index++)
        {
            BoxCollider collider = ColliderScanBuffer[index];
            if (collider != null && IsMalformedBoxCollider(collider))
            {
                ColliderScanBuffer.Clear();
                return true;
            }
        }

        ColliderScanBuffer.Clear();
        return false;
    }

    private static int SanitizeMalformedBoxColliders(GameObject root)
    {
        if (root == null)
            return 0;

        int count = 0;
        ColliderScanBuffer.Clear();
        root.GetComponentsInChildren(true, ColliderScanBuffer);

        for (int index = 0; index < ColliderScanBuffer.Count; index++)
        {
            BoxCollider collider = ColliderScanBuffer[index];
            if (collider == null || !IsMalformedBoxCollider(collider))
                continue;

            Vector3 size = collider.size;
            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            size.z = Mathf.Abs(size.z);
            collider.size = size;

            Vector3 scale = collider.transform.lossyScale;
            if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
            {
                // note: Mirrored decorative vendor colliders are non-authoritative and are disabled instead of retaining incorrect forced-positive collision geometry.
                collider.enabled = false;
            }

            count++;
        }

        ColliderScanBuffer.Clear();
        return count;
    }

    private static IEnumerator CurateKnownPreviewArtifactsRoutine(
        GameObject root,
        string kitId,
        Action<int> completed)
    {
        if (root == null || !string.Equals(
                kitId,
                "witch_house",
                StringComparison.OrdinalIgnoreCase))
        {
            completed?.Invoke(0);
            yield break;
        }

        int removed = 0;
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform candidate = pending.Pop();
            if (candidate == null)
                continue;
            for (int childIndex = 0;
                 childIndex < candidate.childCount;
                 childIndex++)
            {
                pending.Push(candidate.GetChild(childIndex));
            }

            if (candidate == root.transform ||
                !candidate.name.StartsWith(
                    "SM_big_rock",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // note: SM_big_rock is the Witch House source scene's scale/preview prop, not a reviewed piece of Vey's authored location.
            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
            removed++;

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(removed);
    }

    private static IEnumerator SanitizeStreamedCellRoutine(
        GameObject root,
        Action<int, int> completed)
    {
        if (root == null)
        {
            completed?.Invoke(0, 0);
            yield break;
        }

        int disabledColliders = 0;
        int disabledProbes = 0;
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            Collider[] colliders = current.GetComponents<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled ||
                    collider.isTrigger || collider is TerrainCollider ||
                    collider is CharacterController)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float largestDimension = Mathf.Max(
                    bounds.size.x,
                    Mathf.Max(bounds.size.y, bounds.size.z));
                string objectName = collider.name.ToLowerInvariant();
                bool explicitBarrier = ContainsTraversalToken(
                    objectName,
                    "invisible", "blocker", "boundary", "killvolume",
                    "kill_volume", "blockingvolume", "blocking_volume");
                bool smallTraversalClutter =
                    largestDimension <= 4.5f && bounds.size.y <= 3.5f &&
                    ContainsTraversalToken(
                        objectName,
                        "pebble", "rubble", "debris", "clutter", "grass",
                        "flower", "bush", "branch", "root", "smallrock",
                        "small_rock", "crate", "barrel", "basket", "chair",
                        "table", "pot", "wheel", "prop");
                bool lowDecorativeRock =
                    largestDimension <= 3.5f && bounds.size.y <= 1.4f &&
                    ContainsTraversalToken(
                        objectName,
                        "rock", "boulder", "pebble");
                if (!explicitBarrier && !smallTraversalClutter &&
                    !lowDecorativeRock)
                    continue;

                // note: Low decorative rocks never own player blocking; structural stone, walls, stairs, and large boulders retain authored collision.
                collider.enabled = false;
                disabledColliders++;
            }

            ReflectionProbe[] probes =
                current.GetComponents<ReflectionProbe>();
            for (int index = 0; index < probes.Length; index++)
            {
                ReflectionProbe probe = probes[index];
                if (probe == null || !probe.enabled)
                    continue;

                probe.enabled = false;
                disabledProbes++;
            }

            // note: Collider and reflection-probe cleanup walks dense imported cells incrementally instead of allocating and processing the entire hierarchy on one loading frame.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(disabledColliders, disabledProbes);
    }

    private static int SanitizeTraversalObstructionColliders(GameObject root)
    {
        if (root == null)
            return 0;

        int disabled = 0;
        TraversalColliderScanBuffer.Clear();
        root.GetComponentsInChildren(true, TraversalColliderScanBuffer);

        for (int index = 0;
             index < TraversalColliderScanBuffer.Count;
             index++)
        {
            Collider collider = TraversalColliderScanBuffer[index];
            if (collider == null || !collider.enabled || collider.isTrigger ||
                collider is TerrainCollider ||
                collider is CharacterController)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            float largestDimension = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            string objectName = collider.name.ToLowerInvariant();
            bool explicitBarrier = ContainsTraversalToken(
                objectName,
                "invisible", "blocker", "boundary", "killvolume",
                "kill_volume", "blockingvolume", "blocking_volume");
            bool smallTraversalClutter =
                largestDimension <= 4.5f &&
                bounds.size.y <= 3.5f &&
                ContainsTraversalToken(
                    objectName,
                    "pebble", "rubble", "debris", "clutter", "grass",
                    "flower", "bush", "branch", "root", "smallrock",
                    "small_rock", "crate", "barrel", "basket", "chair",
                    "table", "pot", "wheel", "prop");
            bool lowDecorativeRock =
                largestDimension <= 3.5f && bounds.size.y <= 1.4f &&
                ContainsTraversalToken(
                    objectName,
                    "rock", "boulder", "pebble");

            if (!explicitBarrier && !smallTraversalClutter &&
                !lowDecorativeRock)
                continue;

            // note: Generated traversal collision keeps structural floors and walls; invisible volumes and ankle-height dressing never own authoritative player blocking.
            collider.enabled = false;
            disabled++;
        }

        TraversalColliderScanBuffer.Clear();
        return disabled;
    }

    private static int DisableImportedReflectionProbes(GameObject root)
    {
        if (root == null)
            return 0;

        ReflectionProbe[] probes =
            root.GetComponentsInChildren<ReflectionProbe>(true);
        int disabled = 0;

        for (int index = 0; index < probes.Length; index++)
        {
            ReflectionProbe probe = probes[index];
            if (probe == null || !probe.enabled)
                continue;

            // note: Source-scene reflection captures are invalid in the generated world and triggered URP ReflectionProbeManager RenderGraph failures on the minimap camera.
            probe.enabled = false;
            disabled++;
        }

        return disabled;
    }

    private static IEnumerator CurateAndGroundWitchHouseRoutine(
        GameObject contentRoot,
        Terrain terrain,
        Action<int, int> completed)
    {
        if (contentRoot == null || terrain == null ||
            contentRoot.transform.childCount == 0)
        {
            completed?.Invoke(0, 0);
            yield break;
        }

        Transform cell = contentRoot.transform.GetChild(0);
        Bounds structuralBounds = default;
        bool foundStructure = false;
        float frameStartedAt = Time.realtimeSinceStartup;

        Stack<Transform> structuralPending = new Stack<Transform>();
        for (int childIndex = 0; childIndex < cell.childCount; childIndex++)
        {
            Transform child = cell.GetChild(childIndex);
            if (child != null && IsWitchHouseStructuralRoot(child.name))
                structuralPending.Push(child);
        }

        while (structuralPending.Count > 0)
        {
            Transform current = structuralPending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                structuralPending.Push(current.GetChild(childIndex));
            }

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (!IsGroundingRenderer(renderer))
                    continue;

                if (!foundStructure)
                {
                    structuralBounds = renderer.bounds;
                    foundStructure = true;
                }
                else
                {
                    structuralBounds.Encapsulate(renderer.bounds);
                }
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (!foundStructure)
        {
            int fallbackGrounded = 0;
            yield return AlignCompiledCellsToTerrainRoutine(
                contentRoot,
                terrain,
                count => fallbackGrounded = count);
            completed?.Invoke(fallbackGrounded, 0);
            yield break;
        }

        Vector2 clusterCenter = new Vector2(
            structuralBounds.center.x,
            structuralBounds.center.z);
        float clusterRadius = Mathf.Clamp(
            Mathf.Max(structuralBounds.extents.x, structuralBounds.extents.z) +
                9f,
            18f,
            27f);
        int removed = 0;
        // note: Incoherent showcase roots were rejected before instantiation; grounding never rescans every furnished child or performs a second curation pass.

        Vector3 desiredAnchor = contentRoot.transform.position;
        cell.position += new Vector3(
            desiredAnchor.x - structuralBounds.center.x,
            0f,
            desiredAnchor.z - structuralBounds.center.z);
        float terrainHeight = YQGeneratedWorldTerrain.SampleWorldHeight(
            terrain,
            desiredAnchor);
        float verticalDelta = terrainHeight + 0.03f - structuralBounds.min.y;
        cell.position += Vector3.up * verticalDelta;

        Debug.Log(
            "[YQCompiledWorldSiteInstance] WITCH HOUSE CURATED\n" +
            "Showcase roots removed: " + removed + "\n" +
            "Cluster radius: " + clusterRadius.ToString("F1") + "m\n" +
            "Foundation correction: " + verticalDelta.ToString("F2") +
            "m");
        completed?.Invoke(1, removed);
    }

    private static bool IsWitchHouseStructuralRoot(string objectName)
    {
        string identity = (objectName ?? string.Empty).ToLowerInvariant();
        return ContainsTraversalToken(
            identity,
            "sm_shopwalls", "stairs_cube", "sm_beam_01", "sm_beam_02",
            "sm_roofsupports", "storagedoorframe",
            "windowntrim_str_mainshop");
    }

    private static IEnumerator AlignCompiledCellsToTerrainRoutine(
        GameObject contentRoot,
        Terrain terrain,
        Action<int> completed)
    {
        if (contentRoot == null || terrain == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        int grounded = 0;
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int index = 0;
             index < contentRoot.transform.childCount;
            index++)
        {
            Transform cell = contentRoot.transform.GetChild(index);
            bool hasGroundingDelta = false;
            float verticalDelta = 0f;
            if (cell != null)
            {
                yield return TryResolveCellGroundingDeltaRoutine(
                    cell.gameObject,
                    terrain,
                    (resolved, delta) =>
                    {
                        hasGroundingDelta = resolved;
                        verticalDelta = delta;
                    });
            }

            if (cell != null && hasGroundingDelta)
            {
                cell.position += Vector3.up * verticalDelta;
                grounded++;
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(grounded);
    }

    private static IEnumerator TryResolveCellGroundingDeltaRoutine(
        GameObject cell,
        Terrain terrain,
        Action<bool, float> completed)
    {
        if (cell == null || terrain == null)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        List<Renderer> renderers = new List<Renderer>();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(cell.transform);
        bool initialized = false;
        Bounds aggregate = new Bounds();
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            Renderer[] localRenderers = current.GetComponents<Renderer>();
            for (int rendererIndex = 0;
                 rendererIndex < localRenderers.Length;
                 rendererIndex++)
            {
                Renderer renderer = localRenderers[rendererIndex];
                if (!IsGroundingRenderer(renderer))
                    continue;

                renderers.Add(renderer);
                if (!initialized)
                {
                    aggregate = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    aggregate.Encapsulate(renderer.bounds);
                }
            }

            // note: Foundation discovery walks dense authored cells incrementally; grounding can never monopolize the loading thread with a hierarchy-wide renderer query.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (!initialized)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        float lowerBandCeiling = aggregate.min.y + Mathf.Min(
            8f,
            Mathf.Max(1.5f, aggregate.size.y * 0.25f));
        List<Vector2> samples = new List<Vector2>();
        float totalWeight = 0f;

        for (int index = 0; index < renderers.Count; index++)
        {
            Renderer renderer = renderers[index];
            if (!IsGroundingRenderer(renderer))
                continue;

            Bounds bounds = renderer.bounds;
            float footprint = bounds.size.x * bounds.size.z;
            if (bounds.min.y > lowerBandCeiling ||
                !IsFinite(footprint) ||
                footprint < MinimumFoundationRendererFootprint)
            {
                continue;
            }

            float weight = Mathf.Sqrt(footprint);
            samples.Add(new Vector2(bounds.min.y, weight));
            totalWeight += weight;

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (samples.Count == 0 || !IsFinite(totalWeight) || totalWeight <= 0f)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        samples.Sort((left, right) => left.x.CompareTo(right.x));
        float targetWeight = totalWeight * 0.5f;
        float accumulated = 0f;
        float structuralFloor = samples[0].x;

        for (int index = 0; index < samples.Count; index++)
        {
            accumulated += samples[index].y;
            if (accumulated < targetWeight)
                continue;

            structuralFloor = samples[index].x;
            break;
        }

        Vector3 samplePosition = aggregate.center;
        float terrainHeight = YQGeneratedWorldTerrain.SampleWorldHeight(
            terrain,
            samplePosition);
        float candidate = terrainHeight + 0.03f - structuralFloor;
        if (!IsFinite(candidate) ||
            Mathf.Abs(candidate) > MaximumExteriorFoundationCorrection)
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        completed?.Invoke(
            true,
            Mathf.Abs(candidate) >= 0.03f ? candidate : 0f);
    }

    private static bool IsGroundingRenderer(Renderer renderer)
    {
        if (!IsFoundationRenderer(renderer))
            return false;

        string objectName = renderer.name.ToLowerInvariant();
        return !ContainsTraversalToken(
            objectName,
            "grass", "flower", "tree", "bush", "leaf", "branch",
            "rock", "boulder", "water", "mist", "cloud", "particle",
            "vfx", "decal");
    }

    private static bool ContainsTraversalToken(
        string value,
        params string[] tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null)
            return false;

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (!string.IsNullOrEmpty(token) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // note: Imported object names provide the bounded semantic hint used to reject non-authoritative traversal clutter.
                return true;
            }
        }

        return false;
    }

    private static bool IsMalformedBoxCollider(BoxCollider collider)
    {
        if (collider == null)
            return false;

        Vector3 size = collider.size;
        Vector3 scale = collider.transform.lossyScale;
        return size.x < 0f || size.y < 0f || size.z < 0f ||
               scale.x < 0f || scale.y < 0f || scale.z < 0f;
    }

    private static int GetTransformDepth(Transform target)
    {
        int depth = 0;

        while (target != null)
        {
            depth++;
            target = target.parent;
        }

        return depth;
    }

    private static float Deterministic01(string value)
    {
        return (StableHash(value) & 0x00ffffff) / 16777215f;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;

            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }

            return hash;
        }
    }
}

public struct YQTerrainSupportStamp
{
    public Vector2 center;
    public Vector2 flatHalfExtents;
    public float blendDistance;
    public float targetWorldHeight;
    public string materialHint;
}

public static class YQTerrainSupportComposer
{
    private const float FrameBudgetSeconds = 0.0015f;
    private const float MinimumFlatHalfExtent = 0.65f;
    private const float MaximumAssemblyHalfExtent = 90f;
    private const float SupportSurfaceOffset = 0.03f;

    public static bool TryCreateAssemblyStamp(
        GameObject assembly,
        Terrain terrain,
        float blendDistance,
        out YQTerrainSupportStamp stamp)
    {
        stamp = default;

        if (assembly == null || terrain == null ||
            IsExplicitlySuspended(assembly) ||
            !TryGetSolidBoundsAndMaterial(
                assembly,
                out Bounds bounds,
                out string materialHint))
        {
            return false;
        }

        float currentTerrainHeight = terrain.SampleHeight(bounds.center) +
            terrain.transform.position.y;
        float targetHeight = bounds.min.y + SupportSurfaceOffset;

        if (float.IsNaN(targetHeight) || float.IsInfinity(targetHeight) ||
            targetHeight <= currentTerrainHeight + 0.06f)
        {
            return false;
        }

        // note: A reviewed assembly keeps its authored transform; this compact stamp describes only the terrain support that must rise beneath its footprint.
        stamp = new YQTerrainSupportStamp
        {
            center = new Vector2(bounds.center.x, bounds.center.z),
            flatHalfExtents = new Vector2(
                Mathf.Clamp(
                    bounds.extents.x,
                    MinimumFlatHalfExtent,
                    MaximumAssemblyHalfExtent),
                Mathf.Clamp(
                    bounds.extents.z,
                    MinimumFlatHalfExtent,
                    MaximumAssemblyHalfExtent)),
            blendDistance = Mathf.Max(1.25f, blendDistance),
            targetWorldHeight = targetHeight,
            materialHint = materialHint
        };
        return true;
    }

    public static void BuildRaisedAssemblySupportStamps(
        GameObject siteContent,
        Terrain terrain,
        List<YQTerrainSupportStamp> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (siteContent == null || terrain == null ||
            IsExplicitlySuspended(siteContent))
        {
            return;
        }

        Transform siteTransform = siteContent.transform;
        for (int index = 0; index < siteTransform.childCount; index++)
        {
            Transform authoredCell = siteTransform.GetChild(index);
            if (authoredCell == null || !authoredCell.gameObject.activeSelf)
                continue;

            if (TryCreateAssemblyStamp(
                    authoredCell.gameObject,
                    terrain,
                    7f,
                    out YQTerrainSupportStamp stamp))
            {
                // note: Streaming cells are the reviewed spatial unit; supporting that unit preserves internal building relationships instead of moving individual authored meshes.
                results.Add(stamp);
            }
        }
    }

    public static bool TryCreateSiteStamp(
        GameObject siteContent,
        Terrain terrain,
        float targetWorldHeight,
        float maximumRadius,
        out YQTerrainSupportStamp stamp)
    {
        stamp = default;

        if (siteContent == null || terrain == null ||
            IsExplicitlySuspended(siteContent) ||
            !TryGetSolidBoundsAndMaterial(
                siteContent,
                out Bounds bounds,
                out string materialHint))
        {
            return false;
        }

        float currentTerrainHeight = terrain.SampleHeight(bounds.center) +
            terrain.transform.position.y;
        if (float.IsNaN(targetWorldHeight) ||
            float.IsInfinity(targetWorldHeight) ||
            targetWorldHeight <= currentTerrainHeight + 0.06f)
        {
            return false;
        }

        float radius = Mathf.Clamp(
            maximumRadius,
            MinimumFlatHalfExtent,
            YQGeneratedWorldTerrain.WorldSize * 0.40f);
        // note: A compiled site's reviewed footprint is clamped by its validated radius so distant backdrops cannot inflate the generated support plateau.
        stamp = new YQTerrainSupportStamp
        {
            center = new Vector2(bounds.center.x, bounds.center.z),
            flatHalfExtents = new Vector2(
                Mathf.Clamp(bounds.extents.x, MinimumFlatHalfExtent, radius),
                Mathf.Clamp(bounds.extents.z, MinimumFlatHalfExtent, radius)),
            blendDistance = Mathf.Clamp(radius * 0.12f, 8f, 28f),
            targetWorldHeight = targetWorldHeight,
            materialHint = materialHint
        };
        return true;
    }

    public static void BuildSiteSupportStamps(
        GameObject siteContent,
        Terrain terrain,
        float targetWorldHeight,
        float maximumRadius,
        List<YQTerrainSupportStamp> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (siteContent == null || terrain == null ||
            IsExplicitlySuspended(siteContent))
        {
            return;
        }

        Transform siteTransform = siteContent.transform;
        for (int index = 0; index < siteTransform.childCount; index++)
        {
            Transform child = siteTransform.GetChild(index);
            if (child == null || !child.gameObject.activeSelf ||
                !TryCreateSupportStampAtHeight(
                    child.gameObject,
                    terrain,
                    targetWorldHeight,
                    maximumRadius,
                    out YQTerrainSupportStamp stamp))
            {
                continue;
            }

            results.Add(stamp);
        }

        if (results.Count == 0 &&
            TryCreateSupportStampAtHeight(
                siteContent,
                terrain,
                targetWorldHeight,
                maximumRadius,
                out YQTerrainSupportStamp fallback))
        {
            // note: Legacy one-zone reviewed sites still receive support when they do not expose direct compiled-cell children.
            results.Add(fallback);
        }
    }

    private static bool TryCreateSupportStampAtHeight(
        GameObject assembly,
        Terrain terrain,
        float targetWorldHeight,
        float maximumRadius,
        out YQTerrainSupportStamp stamp)
    {
        stamp = default;
        if (assembly == null || terrain == null ||
            IsExplicitlySuspended(assembly) ||
            !TryGetSolidBoundsAndMaterial(
                assembly,
                out Bounds bounds,
                out string materialHint))
        {
            return false;
        }

        float currentTerrainHeight = terrain.SampleHeight(bounds.center) +
            terrain.transform.position.y;
        if (float.IsNaN(targetWorldHeight) ||
            float.IsInfinity(targetWorldHeight) ||
            targetWorldHeight <= currentTerrainHeight + 0.06f)
        {
            return false;
        }

        float radius = Mathf.Clamp(
            maximumRadius,
            MinimumFlatHalfExtent,
            YQGeneratedWorldTerrain.WorldSize * 0.40f);
        float dominantHalfExtent = Mathf.Max(
            bounds.extents.x,
            bounds.extents.z);
        stamp = new YQTerrainSupportStamp
        {
            center = new Vector2(bounds.center.x, bounds.center.z),
            flatHalfExtents = new Vector2(
                Mathf.Clamp(bounds.extents.x, MinimumFlatHalfExtent, radius),
                Mathf.Clamp(bounds.extents.z, MinimumFlatHalfExtent, radius)),
            blendDistance = Mathf.Clamp(
                dominantHalfExtent * 0.16f,
                4f,
                24f),
            targetWorldHeight = targetWorldHeight,
            materialHint = materialHint
        };
        return true;
    }

    public static IEnumerator RaiseTerrainRoutine(
        Terrain terrain,
        IReadOnlyList<YQTerrainSupportStamp> stamps,
        Action<int> completed)
    {
        if (terrain == null || terrain.terrainData == null ||
            stamps == null || stamps.Count == 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        TerrainData data = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = data.size;
        int resolution = data.heightmapResolution;
        int minimumX = resolution - 1;
        int minimumZ = resolution - 1;
        int maximumX = 0;
        int maximumZ = 0;
        bool hasValidStamp = false;

        for (int index = 0; index < stamps.Count; index++)
        {
            YQTerrainSupportStamp stamp = stamps[index];
            float extentX = stamp.flatHalfExtents.x + stamp.blendDistance;
            float extentZ = stamp.flatHalfExtents.y + stamp.blendDistance;
            minimumX = Mathf.Min(
                minimumX,
                WorldToHeightIndex(
                    stamp.center.x - extentX,
                    terrainOrigin.x,
                    terrainSize.x,
                    resolution));
            maximumX = Mathf.Max(
                maximumX,
                WorldToHeightIndex(
                    stamp.center.x + extentX,
                    terrainOrigin.x,
                    terrainSize.x,
                    resolution));
            minimumZ = Mathf.Min(
                minimumZ,
                WorldToHeightIndex(
                    stamp.center.y - extentZ,
                    terrainOrigin.z,
                    terrainSize.z,
                    resolution));
            maximumZ = Mathf.Max(
                maximumZ,
                WorldToHeightIndex(
                    stamp.center.y + extentZ,
                    terrainOrigin.z,
                    terrainSize.z,
                    resolution));
            hasValidStamp = true;
        }

        if (!hasValidStamp || maximumX < minimumX || maximumZ < minimumZ)
        {
            completed?.Invoke(0);
            yield break;
        }

        int width = maximumX - minimumX + 1;
        int height = maximumZ - minimumZ + 1;
        float[,] heights = data.GetHeights(minimumX, minimumZ, width, height);
        float heightStepX = terrainSize.x / (resolution - 1f);
        float heightStepZ = terrainSize.z / (resolution - 1f);
        bool changed = false;
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int z = 0; z < height; z++)
        {
            float worldZ = terrainOrigin.z + (minimumZ + z) * heightStepZ;

            for (int x = 0; x < width; x++)
            {
                float worldX = terrainOrigin.x + (minimumX + x) * heightStepX;
                float normalizedHeight = heights[z, x];

                for (int stampIndex = 0;
                     stampIndex < stamps.Count;
                     stampIndex++)
                {
                    YQTerrainSupportStamp stamp = stamps[stampIndex];
                    float influence = ResolveStampInfluence(
                        stamp,
                        worldX,
                        worldZ);
                    if (influence <= 0f)
                        continue;

                    float target = Mathf.Clamp01(
                        (stamp.targetWorldHeight - terrainOrigin.y) /
                        terrainSize.y);
                    if (target <= normalizedHeight)
                        continue;

                    normalizedHeight = Mathf.Max(
                        normalizedHeight,
                        Mathf.Lerp(normalizedHeight, target, influence));
                }

                if (normalizedHeight > heights[z, x] + 0.00001f)
                {
                    heights[z, x] = normalizedHeight;
                    changed = true;
                }
            }

            // note: Height calculation is sliced across frames; Unity receives one delayed-LOD write only after the complete deterministic patch is ready.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                FrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (!changed)
        {
            completed?.Invoke(0);
            yield break;
        }

        data.SetHeightsDelayLOD(minimumX, minimumZ, heights);
        // note: Each terrain synchronization boundary gets its own loading frame so height LOD, collision refresh, and surface paint cannot stack into one visible hitch.
        yield return null;
        data.SyncHeightmap();
        yield return null;
        terrain.Flush();
        yield return null;

        // note: Surface painting is a visual blend only; failure to find a compatible terrain layer never invalidates the collision-support height contract.
        yield return PaintSupportSurfaceRoutine(terrain, stamps);
        completed?.Invoke(stamps.Count);
    }

    public static bool IsExplicitlySuspended(GameObject root)
    {
        if (root == null)
            return false;

        string objectTag = root.tag ?? string.Empty;
        if (IsSuspendedToken(objectTag) || IsSuspendedToken(root.name))
            return true;

        YQWorldAssemblyDescriptor descriptor =
            root.GetComponent<YQWorldAssemblyDescriptor>();
        if (descriptor == null)
            return false;

        IReadOnlyList<string> tags = descriptor.SemanticTags;
        for (int index = 0; tags != null && index < tags.Count; index++)
        {
            if (IsSuspendedToken(tags[index]))
                return true;
        }

        return false;
    }

    private static IEnumerator PaintSupportSurfaceRoutine(
        Terrain terrain,
        IReadOnlyList<YQTerrainSupportStamp> stamps)
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        if (layers == null || layers.Length == 0 ||
            data.alphamapWidth <= 0 || data.alphamapHeight <= 0)
        {
            yield break;
        }

        int[] layerIndices = new int[stamps.Count];
        bool hasPaintableStamp = false;
        for (int index = 0; index < stamps.Count; index++)
        {
            layerIndices[index] = ResolveTerrainLayer(
                layers,
                stamps[index].materialHint);
            hasPaintableStamp |= layerIndices[index] >= 0;
        }

        if (!hasPaintableStamp)
            yield break;

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        int alphaWidth = data.alphamapWidth;
        int alphaHeight = data.alphamapHeight;
        int minimumX = alphaWidth - 1;
        int minimumZ = alphaHeight - 1;
        int maximumX = 0;
        int maximumZ = 0;

        for (int index = 0; index < stamps.Count; index++)
        {
            if (layerIndices[index] < 0)
                continue;

            YQTerrainSupportStamp stamp = stamps[index];
            float extentX = stamp.flatHalfExtents.x + stamp.blendDistance;
            float extentZ = stamp.flatHalfExtents.y + stamp.blendDistance;
            minimumX = Mathf.Min(minimumX, WorldToMapIndex(
                stamp.center.x - extentX, origin.x, size.x, alphaWidth));
            maximumX = Mathf.Max(maximumX, WorldToMapIndex(
                stamp.center.x + extentX, origin.x, size.x, alphaWidth));
            minimumZ = Mathf.Min(minimumZ, WorldToMapIndex(
                stamp.center.y - extentZ, origin.z, size.z, alphaHeight));
            maximumZ = Mathf.Max(maximumZ, WorldToMapIndex(
                stamp.center.y + extentZ, origin.z, size.z, alphaHeight));
        }

        int width = maximumX - minimumX + 1;
        int height = maximumZ - minimumZ + 1;
        float[,,] weights = data.GetAlphamaps(
            minimumX,
            minimumZ,
            width,
            height);
        float frameStartedAt = Time.realtimeSinceStartup;

        for (int z = 0; z < height; z++)
        {
            float worldZ = origin.z +
                (minimumZ + z) * size.z / Mathf.Max(1f, alphaHeight - 1f);

            for (int x = 0; x < width; x++)
            {
                float worldX = origin.x +
                    (minimumX + x) * size.x / Mathf.Max(1f, alphaWidth - 1f);
                int selectedLayer = -1;
                float strongestInfluence = 0f;

                for (int stampIndex = 0;
                     stampIndex < stamps.Count;
                     stampIndex++)
                {
                    if (layerIndices[stampIndex] < 0)
                        continue;

                    float influence = ResolveStampInfluence(
                        stamps[stampIndex], worldX, worldZ);
                    if (influence > strongestInfluence)
                    {
                        strongestInfluence = influence;
                        selectedLayer = layerIndices[stampIndex];
                    }
                }

                if (selectedLayer < 0 || strongestInfluence <= 0f)
                    continue;

                float blend = strongestInfluence * 0.62f;
                for (int layer = 0; layer < layers.Length; layer++)
                    weights[z, x, layer] *= 1f - blend;
                weights[z, x, selectedLayer] += blend;
            }

            // note: Splat blending shares the same small frame budget as height construction so a large city cannot monopolize the main thread.
            if (Time.realtimeSinceStartup - frameStartedAt >=
                FrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        data.SetAlphamaps(minimumX, minimumZ, weights);
    }

    private static bool TryGetSolidBoundsAndMaterial(
        GameObject root,
        out Bounds bounds,
        out string materialHint)
    {
        bounds = default;
        materialHint = string.Empty;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        float dominantFootprint = 0f;

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled ||
                renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }

            float footprint = renderer.bounds.size.x * renderer.bounds.size.z;
            if (footprint <= dominantFootprint)
                continue;

            dominantFootprint = footprint;
            Material material = renderer.sharedMaterial;
            if (material != null)
            {
                materialHint = material.name + " " +
                    (material.mainTexture != null
                        ? material.mainTexture.name
                        : string.Empty);
            }
        }

        return initialized;
    }

    private static float ResolveStampInfluence(
        YQTerrainSupportStamp stamp,
        float worldX,
        float worldZ)
    {
        float outsideX = Mathf.Max(
            0f,
            Mathf.Abs(worldX - stamp.center.x) - stamp.flatHalfExtents.x);
        float outsideZ = Mathf.Max(
            0f,
            Mathf.Abs(worldZ - stamp.center.y) - stamp.flatHalfExtents.y);
        float distance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
        if (distance >= stamp.blendDistance)
            return 0f;

        float normalized = Mathf.Clamp01(distance / stamp.blendDistance);
        return 1f - normalized * normalized * (3f - 2f * normalized);
    }

    private static int ResolveTerrainLayer(
        TerrainLayer[] layers,
        string materialHint)
    {
        if (layers.Length == 1)
            return 0;

        string hint = NormalizeMaterialFamily(materialHint);
        if (string.IsNullOrEmpty(hint))
            return -1;

        int bestIndex = -1;
        int bestScore = 0;
        for (int index = 0; index < layers.Length; index++)
        {
            TerrainLayer layer = layers[index];
            if (layer == null)
                continue;

            string candidate = NormalizeMaterialFamily(
                layer.name + " " +
                (layer.diffuseTexture != null
                    ? layer.diffuseTexture.name
                    : string.Empty));
            int score = candidate == hint ? 4 :
                candidate.Contains(hint) || hint.Contains(candidate) ? 2 : 0;
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static string NormalizeMaterialFamily(string value)
    {
        string lower = (value ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(lower, "rock", "stone", "cliff", "slate"))
            return "stone";
        if (ContainsAny(lower, "sand", "desert", "beach"))
            return "sand";
        if (ContainsAny(lower, "snow", "ice", "frost"))
            return "snow";
        if (ContainsAny(lower, "grass", "moss", "meadow"))
            return "grass";
        if (ContainsAny(lower, "dirt", "soil", "mud", "earth", "ground",
                "wood", "plank", "timber"))
        {
            return "dirt";
        }

        return string.Empty;
    }

    private static bool IsSuspendedToken(string value)
    {
        string lower = (value ?? string.Empty).ToLowerInvariant();
        return ContainsAny(
            lower,
            "floating", "suspended", "airborne", "flying",
            "skyborne", "levitating");
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            if (value.Contains(tokens[index]))
                return true;
        }

        return false;
    }

    private static int WorldToHeightIndex(
        float world,
        float origin,
        float size,
        int resolution)
    {
        return WorldToMapIndex(world, origin, size, resolution);
    }

    private static int WorldToMapIndex(
        float world,
        float origin,
        float size,
        int resolution)
    {
        float normalized = size > 0.001f ? (world - origin) / size : 0f;
        return Mathf.Clamp(
            Mathf.RoundToInt(normalized * (resolution - 1)),
            0,
            resolution - 1);
    }
}

[Serializable]
public sealed class YQRuntimeWorldSiteQuery
{
    public string semanticStyleKey = string.Empty;
    public YQAuthoredSiteKind siteKind = YQAuthoredSiteKind.Unknown;
    public YQSemanticExtractionTopology topology =
        YQSemanticExtractionTopology.Unknown;
    public List<string> requiredSemanticTags = new List<string>();
}

[CreateAssetMenu(
    fileName = "YQRuntimeWorldSiteCatalog",
    menuName = "YourQuest/World/Runtime World Site Catalog")]
public sealed class YQRuntimeWorldSiteCatalog : ScriptableObject
{
    [SerializeField]
    private string schemaVersion = "runtime-world-sites-1.0.0";

    [SerializeField]
    private List<YQRuntimeWorldSiteRecord> sites =
        new List<YQRuntimeWorldSiteRecord>();

    public string SchemaVersion => schemaVersion;
    public IReadOnlyList<YQRuntimeWorldSiteRecord> Sites => sites;

    public void Configure(IEnumerable<YQRuntimeWorldSiteRecord> newSites)
    {
        // note: This is the runtime allow-list; only reviewed semantic manifests are copied into it and the LLM can select only their stable semantic keys.
        sites = newSites != null
            ? new List<YQRuntimeWorldSiteRecord>(newSites)
            : new List<YQRuntimeWorldSiteRecord>();
    }

    public YQRuntimeWorldSiteRecord FindByKitId(string kitId)
    {
        // note: Stable kit IDs resolve persisted approved geometry without accepting arbitrary asset paths from generated text.
        for (int index = 0; index < sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord site = sites[index];

            if (site != null && string.Equals(
                    site.kitId,
                    kitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return site;
            }
        }

        return null;
    }

    public YQReviewedSemanticSiteManifest LoadSiteByKitId(string kitId)
    {
        YQRuntimeWorldSiteRecord record = FindByKitId(kitId);

        if (record == null ||
            string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey))
        {
            return null;
        }

        // note: Runtime loads only the selected site's semantic manifest and streaming-prefab dependencies instead of retaining every world pack in memory.
        return Resources.Load<YQReviewedSemanticSiteManifest>(
            record.runtimeManifestResourceKey);
    }

    public IReadOnlyList<YQRuntimeWorldSiteRecord> FindCompatibleSites(
        YQRuntimeWorldSiteQuery query)
    {
        List<YQRuntimeWorldSiteRecord> matches =
            new List<YQRuntimeWorldSiteRecord>();

        if (query == null)
            return matches;

        for (int index = 0; index < sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord site = sites[index];

            if (site != null && Matches(site, query))
                matches.Add(site);
        }

        // note: Catalog order is stable by kit ID, so the same accepted query and seed can choose deterministically without exposing Unity asset paths to the LLM.
        return matches;
    }

    public YQReviewedSemanticSiteManifest LoadFirstCompatibleSite(
        YQRuntimeWorldSiteQuery query)
    {
        IReadOnlyList<YQRuntimeWorldSiteRecord> matches =
            FindCompatibleSites(query);

        if (matches.Count == 0)
            return null;

        return Resources.Load<YQReviewedSemanticSiteManifest>(
            matches[0].runtimeManifestResourceKey);
    }

    private static bool Matches(
        YQRuntimeWorldSiteRecord site,
        YQRuntimeWorldSiteQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.semanticStyleKey) &&
            !string.Equals(
                site.semanticStyleKey,
                query.semanticStyleKey,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.siteKind != YQAuthoredSiteKind.Unknown &&
            site.siteKind != query.siteKind)
        {
            return false;
        }

        if (query.topology != YQSemanticExtractionTopology.Unknown &&
            site.topology != query.topology)
        {
            return false;
        }

        List<string> requiredTags = query.requiredSemanticTags ??
            new List<string>();
        List<string> availableTags = site.semanticTags ??
            new List<string>();

        for (int tagIndex = 0;
             tagIndex < requiredTags.Count;
             tagIndex++)
        {
            string requiredTag = requiredTags[tagIndex];

            if (string.IsNullOrWhiteSpace(requiredTag))
                continue;

            bool found = false;

            for (int siteTagIndex = 0;
                 siteTagIndex < availableTags.Count;
                 siteTagIndex++)
            {
                if (string.Equals(
                        availableTags[siteTagIndex],
                        requiredTag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }
}
