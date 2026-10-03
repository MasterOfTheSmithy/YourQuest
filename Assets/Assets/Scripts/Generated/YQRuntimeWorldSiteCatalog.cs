using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

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
    // note: This editor-produced summary only filters impossible primary choices; loaded manifests still revalidate every required source-matched contract.
    public List<YQAssetFunctionV2> reviewedFunctionsV2 = new List<YQAssetFunctionV2>();

    [HideInInspector]
    public YQAuthoredSiteStreamingManifest streamingSite;

    [HideInInspector]
    public YQReviewedSemanticSiteManifest semanticSite;
}

[DisallowMultipleComponent]
public sealed class YQGeneratedSettlementService : MonoBehaviour
{
    private const int ProvisionCost = 8;
    private const int MaximumPurchases = 3;
    private string _siteId = string.Empty;
    private string _worldSeed = string.Empty;

    public void Configure(string siteId, string worldSeed)
    {
        // note: The accepted site ID is the durable service identity; it is never derived from a transient GameObject instance.
        _siteId = siteId ?? string.Empty;
        _worldSeed = worldSeed ?? string.Empty;
    }

    public bool TryUse(GameObject interactor)
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        PlayerState state = manager != null ? manager.state : null;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        if (state == null || content == null || string.IsNullOrWhiteSpace(_siteId))
            return false;

        state.EnsureCollections();
        string counterKey = "settlement_service:" + _siteId + ":provisions";
        state.behaviorCounters.TryGetValue(counterKey, out float priorPurchases);
        if (priorPurchases >= MaximumPurchases)
        {
            content.SetInventoryMessage("The settlement has no more provisions to spare.");
            return false;
        }

        if (state.currency < ProvisionCost)
        {
            content.SetInventoryMessage("The settlement provision costs " + ProvisionCost + " gold.");
            return false;
        }

        int purchaseIndex = Mathf.Max(0, Mathf.FloorToInt(priorPurchases));
        InventoryItemRecord item = content.GenerateItem(
            _worldSeed + "|" + _siteId + "|provisions|" + purchaseIndex,
            Mathf.Max(1, state.level),
            "consumable",
            true);
        if (item == null)
        {
            content.SetInventoryMessage("The settlement service could not prepare its provisions.");
            return false;
        }

        // note: Currency, inventory, counter, and ledger mutation all commit through the authoritative PlayerState before the site can report success.
        state.currency -= ProvisionCost;
        item.familyKey = "settlement_service:" + _siteId;
        item.displayName = "Settlement Provisions";
        item.description = "Supplies purchased from the accepted settlement service.";
        state.AddOrUpdateItem(item, true);
        state.IncCounter(counterKey, 1f);
        state.AddLedgerLine("Purchased settlement provisions for " + ProvisionCost + " gold.");
        manager.Save();
        content.SetInventoryMessage("Purchased settlement provisions for " + ProvisionCost + " gold.");
        return true;
    }
}

[DisallowMultipleComponent]
public sealed class YQGeneratedLandmarkResource : MonoBehaviour
{
    private const int ResourceGold = 6;
    private string _featureId = string.Empty;
    private string _worldSeed = string.Empty;

    public void Configure(string featureId, string worldSeed)
    {
        // note: Stable feature identity, not the streamed hierarchy, owns the harvest receipt across unload and revisit.
        _featureId = featureId ?? string.Empty;
        _worldSeed = worldSeed ?? string.Empty;
    }

    public bool TryUse(GameObject interactor)
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        PlayerState state = manager != null ? manager.state : null;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        if (state == null || content == null || string.IsNullOrWhiteSpace(_featureId))
            return false;

        state.EnsureCollections();
        string counterKey = "landmark_resource:" + _featureId + ":harvested";
        state.behaviorCounters.TryGetValue(counterKey, out float harvested);
        if (harvested >= 1f)
        {
            content.SetInventoryMessage("This landmark has already yielded its resource.");
            return false;
        }

        InventoryItemRecord item = content.GenerateItem(
            _worldSeed + "|" + _featureId + "|resource",
            Mathf.Max(1, state.level),
            "consumable",
            true);
        if (item == null)
        {
            content.SetInventoryMessage("The landmark resource could not be recovered.");
            return false;
        }

        // note: The resource, gold, receipt counter, and ledger entry commit to PlayerState before the streamed landmark can be unloaded.
        item.familyKey = "landmark_resource:" + _featureId;
        item.displayName = "Landmark Resource";
        item.description = "A resource recovered from an accepted world landmark.";
        state.AddOrUpdateItem(item, true);
        state.currency += ResourceGold;
        state.IncCounter(counterKey, 1f);
        state.AddLedgerLine("Recovered a landmark resource and " + ResourceGold + " gold.");
        manager.Save();
        content.SetInventoryMessage("Recovered a landmark resource and " + ResourceGold + " gold.");
        return true;
    }
}

public readonly struct YQSemanticSiteCompositionV2
{
    public readonly string kitId;
    public readonly string selectionSeed;
    public readonly string compositionSignature;
    public readonly string[] selectedIds;
    public readonly string[] coveredSemanticTags;
    public readonly int sourceInstanceCount;

    public int SelectedCount => selectedIds != null
        ? selectedIds.Length
        : 0;

    public YQSemanticSiteCompositionV2(
        string newKitId,
        string newSelectionSeed,
        string newCompositionSignature,
        string[] newSelectedIds,
        string[] newCoveredSemanticTags,
        int newSourceInstanceCount)
    {
        // note: The immutable composition contract is safe to compare during the V2 prepass without instantiating, moving, or activating authored geometry.
        kitId = newKitId ?? string.Empty;
        selectionSeed = newSelectionSeed ?? string.Empty;
        compositionSignature = newCompositionSignature ?? string.Empty;
        selectedIds = newSelectedIds ?? Array.Empty<string>();
        coveredSemanticTags = newCoveredSemanticTags ?? Array.Empty<string>();
        sourceInstanceCount = Mathf.Max(0, newSourceInstanceCount);
    }
}

public static class YQCompiledWorldSiteBindingService
{
    // note: Version four migrates previously persisted site bindings so the semantic-slice selector can replace legacy golden-scene assignments once, then remain save-authoritative.
    public const string BindingVersion = "reviewed-site-binding-4-semantic-slices";

    private static YQRuntimeWorldSiteCatalog catalog;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCache()
    {
        catalog = null;
    }

    public static string[] BuildSettlementSemanticSliceTags(
        GeneratedSettlementRecord settlement)
    {
        List<string> tags = new List<string>(8);

        // note: Generated structured priorities enter the actual cell selector before mandatory baseline roles; old saves retain their existing empty-list behavior.
        foreach (string role in NormalizeCellRoleIntents(settlement?.cellRoleIntents))
            AddSemanticTag(tags, role);

        // note: Every generated settlement requests a readable civic slice; the order is semantic priority used by the seeded cell selector.
        AddSemanticTag(tags, "poi");
        AddSemanticTag(tags, "civic");
        AddSemanticTag(tags, "residential");
        AddSemanticTag(tags, "service");
        AddSemanticTag(tags, "circulation");

        if (settlement != null)
        {
            string meaning = (settlement.kind ?? string.Empty) + " " +
                (settlement.siteRoleIntent ?? string.Empty) + " " +
                (settlement.marketBias ?? string.Empty);

            if (ContainsSemanticFragment(meaning, "fort", "stronghold",
                    "defen", "guard", "watch"))
            {
                AddSemanticTag(tags, "perimeter");
                AddSemanticTag(tags, "defense");
            }

            if (ContainsSemanticFragment(meaning, "market", "trade",
                    "merchant", "smith", "inn", "service", "repair"))
            {
                AddSemanticTag(tags, "market");
                AddSemanticTag(tags, "commerce");
            }

            if (settlement.serviceSlots != null)
            {
                for (int index = 0; index < settlement.serviceSlots.Count;
                     index++)
                {
                    string service = settlement.serviceSlots[index] ??
                        string.Empty;
                    if (ContainsSemanticFragment(service, "merchant", "smith",
                            "inn", "healer", "alchemist", "vendor"))
                    {
                        AddSemanticTag(tags, "service");
                        break;
                    }
                }
            }
        }

        return tags.ToArray();
    }

    public static string[] BuildEncampmentSemanticSliceTags(
        GeneratedEncampmentRecord encampment)
    {
        List<string> tags = new List<string>(7);

        foreach (string role in NormalizeCellRoleIntents(encampment?.cellRoleIntents))
            AddSemanticTag(tags, role);

        // note: Hostile locations use a compact defensible approach slice; they must never load an entire settlement showcase merely because that pack matched their style.
        AddSemanticTag(tags, "poi");
        AddSemanticTag(tags, "perimeter");
        AddSemanticTag(tags, "circulation");

        if (encampment == null)
            return tags.ToArray();

        string meaning =
            (encampment.kind ?? string.Empty) + " " +
            (encampment.siteRoleIntent ?? string.Empty) + " " +
            (encampment.layoutIntent ?? string.Empty) + " " +
            (encampment.surfacePresentation ?? string.Empty) + " " +
            (encampment.monsterFamily ?? string.Empty);

        if (ContainsSemanticFragment(meaning, "fort", "stronghold",
                "guard", "watch", "defen", "siege", "occupied"))
        {
            AddSemanticTag(tags, "defense");
        }

        if (ContainsSemanticFragment(meaning, "village", "town", "city",
                "occupied", "raider", "bandit"))
        {
            AddSemanticTag(tags, "civic");
            AddSemanticTag(tags, "residential");
        }

        if (ContainsSemanticFragment(meaning, "camp", "outpost", "lair",
                "nest", "den"))
        {
            AddSemanticTag(tags, "service");
        }

        return tags.ToArray();
    }

    public const string CellRoleVocabulary = "residential, civic, service, circulation, market, commerce, defense, perimeter, poi, storage, manufacturing, encounter, reward";

    public static List<string> NormalizeCellRoleIntents(IReadOnlyList<string> requested)
    {
        // note: Generated requests are bounded exact semantic tokens, never arbitrary tags, prose, Unity paths or executable asset identifiers.
        var result = new List<string>(6);
        if (requested == null) return result;
        for (int index = 0; index < requested.Count && index < 32 && result.Count < 6; index++)
        {
            string role = (requested[index] ?? string.Empty).Trim().ToLowerInvariant();
            switch (role)
            {
                case "residential": case "civic": case "service": case "circulation":
                case "market": case "commerce": case "defense": case "perimeter":
                case "poi": case "storage": case "manufacturing": case "encounter": case "reward":
                    if (!result.Contains(role)) result.Add(role);
                    break;
            }
        }
        return result;
    }

    private static void AddSemanticTag(List<string> tags, string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag))
            return;

        for (int index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], tag,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        tags.Add(tag);
    }

    private static bool ContainsSemanticFragment(
        string value,
        params string[] fragments)
    {
        for (int index = 0; index < fragments.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(fragments[index]) &&
                value.IndexOf(fragments[index],
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveSettlementSite(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        out YQRuntimeWorldSiteRecord selected,
        out bool bindingChanged,
        ISet<string> excludedKitIds = null,
        bool persistBinding = true)
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

            if (IsSettlementCandidate(selected) && IsSitePaletteCompatible(selected, palette, region) &&
                (excludedKitIds == null || !excludedKitIds.Contains(selected.kitId)))
                return true;
        }

        // note: A rejected saved/candidate lookup must not survive when no alternative is eligible.
        selected = null;

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
        bool unusedSettlementCandidateAvailable =
            HasUnusedSettlementCandidate(plan, settlement.settlementId, excludedKitIds, palette, region);

        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord candidate = catalog.Sites[index];

            if (!IsSettlementCandidate(candidate) || !IsSitePaletteCompatible(candidate, palette, region) ||
                (excludedKitIds != null && excludedKitIds.Contains(candidate.kitId)))
                continue;

            // note: Spread a new plan across every eligible reviewed settlement profile before allowing deterministic profile reuse after the catalog is exhausted.
            if (unusedSettlementCandidateAvailable &&
                IsKitAlreadyBound(plan, candidate.kitId,
                    settlement.settlementId))
            {
                continue;
            }

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

        // note: Preflight can inspect another kit without changing persisted location identity before its composition passes.
        if (!persistBinding)
            return true;

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
        out bool bindingChanged,
        ISet<string> excludedKitIds = null,
        bool persistBinding = true)
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
                }) && IsSitePaletteCompatible(selected, palette, region) && (excludedKitIds == null || !excludedKitIds.Contains(selected.kitId)))
                return true;
        }

        // note: An excluded prior lookup is not a successful alternative when the candidate set is exhausted.
        selected = null;

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
            if (!IsEncampmentCandidate(candidate, intents) || !IsSitePaletteCompatible(candidate, palette, region) ||
                (excludedKitIds != null && excludedKitIds.Contains(candidate.kitId)))
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

        // note: Selection-only probes leave the save untouched until the caller validates the actual cells.
        if (!persistBinding)
            return true;

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
            (!YQWorldGenerationArchitecture.UsesV2SpatialRuntime ||
             (candidate.reviewedFunctionsV2 != null &&
              candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Habitation) &&
              candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Service) &&
              candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Circulation))) &&
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

        // note: A hostile site cannot gain encounter capability from a shared style word; avoid loading entire legacy maps that have no reviewed providers.
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntime &&
            (candidate.reviewedFunctionsV2 == null ||
             !candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Encounter) ||
             !candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Reward) ||
             !candidate.reviewedFunctionsV2.Contains(YQAssetFunctionV2.Security))) return false;

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

    private static bool HasUnusedSettlementCandidate(
        GeneratedWorldPlanRecord plan,
        string currentLocationId,
        ISet<string> excludedKitIds = null,
        GeneratedRegionAssetPaletteRecord palette = null,
        GeneratedRegionRecord region = null)
    {
        if (catalog == null || catalog.Sites == null)
            return false;

        for (int index = 0; index < catalog.Sites.Count; index++)
        {
            YQRuntimeWorldSiteRecord candidate = catalog.Sites[index];
            if (IsSettlementCandidate(candidate) && IsSitePaletteCompatible(candidate, palette, region) &&
                (excludedKitIds == null || !excludedKitIds.Contains(candidate.kitId)) &&
                !IsKitAlreadyBound(plan, candidate.kitId,
                    currentLocationId))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsSitePaletteCompatible(YQRuntimeWorldSiteRecord site, GeneratedRegionAssetPaletteRecord palette, GeneratedRegionRecord region)
    {
        // note: Functional site availability cannot override the accepted region's genre boundary; only the existing authored transition rule can authorize a change.
        if (site == null) return false;
        string style = palette != null ? palette.styleKey : region != null ? region.assetStyleKey : string.Empty;
        string reason = region != null ? region.assetStyleRationale : palette != null ? palette.rationale : string.Empty;
        return string.IsNullOrWhiteSpace(style) || YQWorldAssetCatalog.IsCoherentStyleTransition(style, site.semanticStyleKey, reason);
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
    private static readonly ProfilerMarker G08UpdateMarker = new ProfilerMarker("G08FrameCost.YQCompiledWorldSiteInstance.Update()");
    private static readonly Dictionary<string, YQCompiledWorldSiteInstance>
        Instances = new Dictionary<string, YQCompiledWorldSiteInstance>(
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float>
        ExteriorFoundationCorrectionCache = new Dictionary<string, float>(
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, bool>
        MalformedColliderPrefabCache = new Dictionary<int, bool>();
    private static readonly Dictionary<string, HashSet<string>>
        SemanticSelectionCache = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, YQReviewedSemanticSiteManifest>
        PreparedManifestCache =
            new Dictionary<string, YQReviewedSemanticSiteManifest>(
                StringComparer.OrdinalIgnoreCase);
    private static readonly List<BoxCollider>
        ColliderScanBuffer = new List<BoxCollider>(256);
    private static readonly List<Collider>
        TraversalColliderScanBuffer = new List<Collider>(256);
    private static readonly RaycastHit[] ResidentSurfaceHitBuffer =
        new RaycastHit[64];
    private static readonly Collider[] ResidentClearanceBuffer =
        new Collider[24];
    private static YQCompiledWorldSiteInstance ActiveStreamLoader;
    private static Camera StreamVisibilityCamera;

    private struct SourceColliderSnapshot
    {
        public BoxCollider collider;
        public Vector3 size;
        public bool enabled;
    }

    private string settlementId = string.Empty;
    private string canonicalRegionId = string.Empty;
    private string runtimeManifestResourceKey = string.Empty;
    private string expectedKitId = string.Empty;
    private Vector3 authoredOrigin;
    private YQReviewedSemanticSiteManifest manifest;
    private YQWorldSitePresentationMode presentationMode =
        YQWorldSitePresentationMode.Unknown;
    private string[] semanticSliceTags = Array.Empty<string>();
    private string semanticSliceSeed = string.Empty;
    private HashSet<string> activeCellIds;
    private bool requiresV2CellSelection;
    private bool loading;
    private bool loaded;
    private bool loadRejected;
    // note: Preserve the last site-admission reason for the editor heartbeat so a hidden required site can be diagnosed without exposing partial geometry.
    private string loadFailure = string.Empty;
    private bool streamScheduledLogged;
    private float preparedSiteRadius;
    private float loadedSiteRadius;
    private float nextDistanceCheckTime;
    private bool unloading;
    // note: Expose site readiness to companion runtime binders without allowing them to mutate the serialized streaming state.
    public bool IsLoaded => loaded;
    // note: Site streaming owns its nested work even when a world preload coroutine is waiting on it.
    private IEnumerator streamLoadExecution;
    private IEnumerator streamUnloadExecution;
    private Coroutine streamLoadCoroutine;
    private Coroutine streamUnloadCoroutine;
    private GameObject pendingSiteContent;
    private readonly List<ResidentPositionBinding> residentPositionBindings =
        new List<ResidentPositionBinding>();

    // note: Keep ordinary authored sites outside the origin's startup memory footprint; the curated origin pair is pinned explicitly below.
    // note: Begin hidden staging well before an approaching player reaches town sightlines; the former 260m visible-camera gate could prevent a settlement from ever instantiating.
    private const float LoadDistance = 520f;
    private const float UnloadDistance = 700f;
    private const float HardUnloadDistanceMargin = 360f;
    private const float HiddenActivationWaitSeconds = 2.5f;
    private const float UrgentActivationDistance = 120f;
    private const float DistanceCheckInterval = 0.40f;
    private const float StreamSlotDiagnosticSeconds = 45f;
    private const float SeamlessSiteRadiusLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.22f;
    private const float SeamlessSiteDimensionLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.45f;
    private const float RuntimeSiteRadiusLimit =
        YQGeneratedWorldTerrain.WorldSize * 0.22f;
    private const float GeneratedTerrainEdgeClearance = 16f;
    private const float StreamingFrameBudgetSeconds = 0.0015f;
    private const int StreamOutDiscoveryLimitPerFrame = 128;
    private const int StreamOutDisableLimitPerFrame = 48;
    private const int StreamOutDestroyLimitPerFrame = 16;
    private const int ResidentRelocationLimitPerFrame = 2;
    private const float MinimumResidentSurfaceNormalY = 0.68f;
    private const int ComplexCellInstanceThreshold = 256;
    private const int CuratedSiteSourceInstanceBudget = 640;
    private const int MaximumDefaultSemanticZones = 3;
    // note: A generated district is a compact semantic composition, not a hidden clone of an asset-pack showcase; this ceiling prevents two 2,500-object cells from occupying the stream slot for minutes.
    private const int SettlementSemanticInstanceBudget = 1100;
    private const int MaximumSettlementSemanticCells = 8;
    private const int MaximumSettlementSemanticZones = 3;
    private const float SettlementSemanticRadiusLimit = 190f;
    private const float SettlementSemanticVerticalLimit = 140f;
    private const float MaximumSemanticCompositionGap = 48f;
    private const float MinimumExteriorFoundationCorrection = 0.35f;
    private const float MaximumExteriorFoundationCorrection = 96f;
    private const float MinimumFoundationRendererFootprint = 0.36f;
    private const float MaximumCompiledFoundationAirGap = 0.18f;
    private const float MaximumCompiledFoundationPenetration = 0.65f;
    private const float MinimumCompiledFoundationSupportRatio = 0.82f;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeInstances()
    {
        Instances.Clear();
        ExteriorFoundationCorrectionCache.Clear();
        MalformedColliderPrefabCache.Clear();
        SemanticSelectionCache.Clear();
        PreparedManifestCache.Clear();
        ColliderScanBuffer.Clear();
        TraversalColliderScanBuffer.Clear();
        ActiveStreamLoader = null;
        StreamVisibilityCamera = null;
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
                locationId,
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
            Array.Empty<string>(),
            locationId);
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

                if (zone == null || zone.prefab == null ||
                    !ZoneOverlapsAllowedCells(zone, selectedCellIds))
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

    public static IEnumerator PreloadPreparedSitesRoutine(
        IReadOnlyList<string> orderedLocationIds,
        int maximumSites,
        Action<int> completed)
    {
        int loadedCount = 0;
        if (orderedLocationIds == null || maximumSites <= 0)
        {
            completed?.Invoke(0);
            yield break;
        }

        for (int index = 0;
             index < orderedLocationIds.Count && loadedCount < maximumSites;
             index++)
        {
            string locationId = orderedLocationIds[index];
            if (string.IsNullOrWhiteSpace(locationId) ||
                !Instances.TryGetValue(locationId,
                    out YQCompiledWorldSiteInstance site) ||
                site == null || site.loaded || site.loading || site.loadRejected)
            {
                continue;
            }

            float slotWaitStartedAt = Time.realtimeSinceStartup;
            bool slotWaitWarningLogged = false;
            while (ActiveStreamLoader != null && ActiveStreamLoader != site)
            {
                if (!slotWaitWarningLogged &&
                    Time.realtimeSinceStartup - slotWaitStartedAt >
                    StreamSlotDiagnosticSeconds)
                {
                    slotWaitWarningLogged = true;
                    Debug.LogWarning(
                        "[YQCompiledWorldSiteInstance] Prepared-site preload is waiting for the active cooperative stream. " +
                        "Location=" + locationId + ", pass=initial_preload.");
                }
                yield return null;
            }

            // note: The title Goddess camera is the safest hidden streaming window; semantic settlements finish one at a time before the generated-world reveal.
            ActiveStreamLoader = site;
            yield return site.LoadPreparedSiteGuardedRoutine();
            if (site.loaded)
                loadedCount++;
        }

        completed?.Invoke(loadedCount);
    }

    public static IEnumerator MaterializeSemanticSliceRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        Action<bool> completed)
    {
        yield return MaterializeSemanticSliceRoutine(
            siteRoot,
            locationId,
            record,
            requiredSemanticTags,
            locationId,
            completed);
    }

    public static IEnumerator MaterializeSemanticSliceNowRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        string selectionSeed,
        Action<bool> completed)
    {
        // note: Streamed beta cells need the same reviewed composition and collision gate, but must finish it while their parent remains hidden so readiness cannot expose a prepared-only shell.
        if (!Application.isPlaying)
        {
            yield return MaterializeRoutine(siteRoot, locationId, record, completed);
            yield break;
        }

        if (siteRoot == null || string.IsNullOrWhiteSpace(locationId) || record == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        bool prepared = false;
        yield return PrepareValidatedSiteRoutine(
            siteRoot,
            locationId,
            record,
            requiredSemanticTags,
            selectionSeed,
            success => prepared = success);
        if (!prepared)
        {
            completed?.Invoke(false);
            yield break;
        }

        YQCompiledWorldSiteInstance site =
            siteRoot.GetComponent<YQCompiledWorldSiteInstance>();
        if (site == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        // note: Reuse the one-at-a-time authored-site slot without requiring an inactive staged root to report isActiveAndEnabled.
        while (ActiveStreamLoader != null && ActiveStreamLoader != site)
            yield return null;
        ActiveStreamLoader = site;
        yield return site.LoadPreparedSiteDetachedRoutine();
        completed?.Invoke(site.loaded && !site.loading && !site.unloading && !site.loadRejected);
    }

    public static IEnumerator MaterializeSemanticSliceRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        string selectionSeed,
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
            selectionSeed,
            completed);
    }

    public static IEnumerator ResolveSemanticSliceRadiusRoutine(
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        string selectionSeed,
        Action<bool, float> completed)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey))
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        // note: Terrain reserves the exact seeded semantic-cell aggregate, not the much larger reviewed source scene that supplied those cells.
        YQReviewedSemanticSiteManifest manifest;
        if (!PreparedManifestCache.TryGetValue(
                record.runtimeManifestResourceKey,
                out manifest))
        {
            ResourceRequest request = Resources.LoadAsync<
                YQReviewedSemanticSiteManifest>(
                record.runtimeManifestResourceKey);
            yield return request;
            manifest = request.asset as YQReviewedSemanticSiteManifest;
            if (manifest != null)
            {
                // note: Construction is the first authoritative read; later validation and streaming reuse this reviewed manifest instead of loading its dependency graph again.
                PreparedManifestCache[record.runtimeManifestResourceKey] =
                    manifest;
            }
        }
        if (manifest == null || !manifest.ReleaseEligible ||
            !string.Equals(manifest.KitId, record.kitId,
                StringComparison.OrdinalIgnoreCase))
        {
            completed?.Invoke(false, 0f);
            yield break;
        }

        YQSemanticSiteCompositionV2 composition = default;
        // note: Start with an empty selection so both the curated-origin and generated-site branches satisfy definite assignment before validation.
        HashSet<string> selectedCellIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntime)
        {
            if (!TryBuildSemanticCompositionV2(
                    manifest,
                    requiredSemanticTags,
                    selectionSeed,
                    out composition,
                    out string compositionFailure))
            {
                Debug.LogError(
                    "[YQCompiledWorldSiteInstance] V2 SEMANTIC COMPOSITION REJECTED\n" +
                    "Reviewed site: " + record.kitId + "\n" +
                    "Reason: " + compositionFailure);
                completed?.Invoke(false, 0f);
                yield break;
            }

            selectedCellIds = new HashSet<string>(
                composition.selectedIds,
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            selectedCellIds = BuildActiveCellIds(
                manifest,
                requiredSemanticTags,
                selectionSeed);
        }
        bool valid = TryValidateSelectedSite(
            manifest,
            selectedCellIds,
            record.presentationMode,
            null,
            out Vector3 _unusedOrigin,
            out float radius,
            out string _unusedFailure,
            selectionSeed);
        completed?.Invoke(valid, valid ? radius : 0f);
    }

    public static IEnumerator ResolveSemanticCompositionV2Routine(
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        string selectionSeed,
        Action<bool, float, YQSemanticSiteCompositionV2, string> completed,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions = null,
        Action<bool> reportVariantIndependentFailure = null)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.runtimeManifestResourceKey))
        {
            reportVariantIndependentFailure?.Invoke(true);
            completed?.Invoke(
                false,
                0f,
                default,
                "runtime manifest binding is missing.");
            yield break;
        }

        YQReviewedSemanticSiteManifest manifest;
        if (!PreparedManifestCache.TryGetValue(
                record.runtimeManifestResourceKey,
                out manifest))
        {
            // note: V2 resolves and validates its complete immutable site decision asynchronously before the terrain prepass accepts a footprint.
            ResourceRequest request = Resources.LoadAsync<
                YQReviewedSemanticSiteManifest>(
                record.runtimeManifestResourceKey);
            yield return request;
            manifest = request.asset as YQReviewedSemanticSiteManifest;
            if (manifest != null)
            {
                PreparedManifestCache[record.runtimeManifestResourceKey] =
                    manifest;
            }
        }

        if (manifest == null ||
            !string.Equals(manifest.KitId, record.kitId,
                StringComparison.OrdinalIgnoreCase))
        {
            reportVariantIndependentFailure?.Invoke(true);
            completed?.Invoke(
                false,
                0f,
                default,
                "reviewed runtime manifest is missing or mismatched.");
            yield break;
        }

        // note: Reject missing kit-wide capabilities before repeatedly selecting different subsets of the same incapable kit.
        if (requiredFunctions != null && requiredFunctions.Count > 0 &&
            !YQSiteFunctionContractsV2.TryValidateAvailableFunctions(
                manifest, requiredFunctions, record.structureUsagePolicy, out string availabilityFailure))
        {
            reportVariantIndependentFailure?.Invoke(true);
            completed?.Invoke(false, 0f, default, availabilityFailure);
            yield break;
        }

        if (!TryBuildSemanticCompositionV2(
                manifest,
                requiredSemanticTags,
                selectionSeed,
                out YQSemanticSiteCompositionV2 composition,
                out string compositionFailure,
                requiredFunctions,
                record.structureUsagePolicy))
        {
            completed?.Invoke(
                false,
                0f,
                default,
                compositionFailure);
            yield break;
        }

        HashSet<string> selectedIds = new HashSet<string>(
            composition.selectedIds,
            StringComparer.OrdinalIgnoreCase);
        bool valid = TryValidateSelectedSite(
            manifest,
            selectedIds,
            record.presentationMode,
            null,
            out Vector3 _unusedOrigin,
            out float radius,
            out string validationFailure,
            selectionSeed);
        completed?.Invoke(
            valid,
            valid ? radius : 0f,
            valid ? composition : default,
            valid ? string.Empty : validationFailure);
    }

    public static bool TryBuildSemanticCompositionV2(
        YQReviewedSemanticSiteManifest selectedManifest,
        IReadOnlyList<string> requiredTags,
        string selectionSeed,
        out YQSemanticSiteCompositionV2 composition,
        out string failure,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions = null,
        YQWorldStructureUsagePolicy structurePolicy = YQWorldStructureUsagePolicy.Unspecified)
    {
        composition = default;
        failure = string.Empty;

        if (selectedManifest == null || !selectedManifest.ReleaseEligible)
        {
            failure = "reviewed semantic manifest is missing or not release eligible.";
            return false;
        }
        if (requiredTags == null || requiredTags.Count == 0)
        {
            failure = "V2 composition has no required semantic roles.";
            return false;
        }

        // note: V2 deliberately calls only the seeded semantic selector; it never substitutes the legacy smallest-civic fallback when the requested roles cannot be composed.
        HashSet<string> selectedIds = selectedManifest.StreamingSite != null
            ? BuildSemanticStreamingCellIds(
                selectedManifest,
                requiredTags,
                selectionSeed,
                requiredFunctions,
                structurePolicy)
            : BuildSemanticLegacyZoneIds(
                selectedManifest,
                requiredTags,
                selectionSeed,
                requiredFunctions,
                structurePolicy);
        if (selectedIds == null || selectedIds.Count == 0)
        {
            failure = "no reviewed cells satisfy the requested semantic roles.";
            return false;
        }

        List<string> coveredTags = new List<string>(requiredTags.Count);
        for (int tagIndex = 0; tagIndex < requiredTags.Count; tagIndex++)
        {
            string tag = requiredTags[tagIndex];
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            if (!SelectionCoversTag(selectedManifest, selectedIds, tag))
            {
                // note: Once typed site functions exist, district labels are selection hints only; selected cell contracts below provide the functional acceptance evidence.
                if (requiredFunctions != null)
                    continue;
                failure = "selected cells do not cover required semantic role '" +
                    tag + "'.";
                return false;
            }

            AddUniqueSemanticValue(coveredTags, tag);
        }

        if (!TryMeasureSemanticComposition(
                selectedManifest,
                selectedIds,
                out int sourceInstanceCount,
                out List<Bounds> selectedBounds,
                out failure))
        {
            return false;
        }
        if (sourceInstanceCount > SettlementSemanticInstanceBudget)
        {
            failure = "selected composition exceeds the " +
                SettlementSemanticInstanceBudget + " instance budget.";
            return false;
        }
        // note: New worlds connect cells by generated streets; authored source proximity is only the legacy layout's connectivity rule.
        if (!YQProceduralSettlementLayout.TryResolve(selectedManifest, selectedIds, selectionSeed, out var blockLayout, out failure))
            return false;
        if (blockLayout == null && !IsConnectedSemanticComposition(selectedBounds))
        {
            failure = "selected composition contains spatially disconnected districts.";
            return false;
        }

        List<string> orderedIds = new List<string>(selectedIds);
        // note: Spatially valid and visually tagged cells are not a functional settlement until their selected cell contracts satisfy the accepted site's explicit requirements.
        if (requiredFunctions != null && !YQSiteFunctionContractsV2.TryValidate(
                selectedManifest, selectedIds, requiredFunctions, structurePolicy, out failure))
            return false;
        orderedIds.Sort(StringComparer.OrdinalIgnoreCase);
        coveredTags.Sort(StringComparer.OrdinalIgnoreCase);
        // note: The signature represents visible geometry only; two semantic labels cannot disguise the same selected cell assembly as a distinct generated place.
        string signatureSource = selectedManifest.KitId + "|" +
            string.Join(",", orderedIds);
        string signature = blockLayout != null ? YQProceduralSettlementLayout.GeometrySignature(blockLayout) : StableHash(signatureSource).ToString("X8");
        composition = new YQSemanticSiteCompositionV2(
            selectedManifest.KitId,
            selectionSeed,
            signature,
            orderedIds.ToArray(),
            coveredTags.ToArray(),
            sourceInstanceCount);
        return true;
    }

    private static IEnumerator PrepareValidatedSiteRoutine(
        Transform siteRoot,
        string locationId,
        YQRuntimeWorldSiteRecord record,
        string[] requiredSemanticTags,
        string selectionSeed,
        Action<bool> completed)
    {
        // note: Resource validation is asynchronous and instantiates no authored cell, preventing an invalid distant site from being accepted merely because a streaming component exists.
        YQReviewedSemanticSiteManifest selectedManifest;
        if (!PreparedManifestCache.TryGetValue(
                record.runtimeManifestResourceKey,
                out selectedManifest))
        {
            ResourceRequest request = Resources.LoadAsync<
                YQReviewedSemanticSiteManifest>(
                record.runtimeManifestResourceKey);
            yield return request;
            selectedManifest =
                request.asset as YQReviewedSemanticSiteManifest;
            if (selectedManifest != null)
            {
                PreparedManifestCache[record.runtimeManifestResourceKey] =
                    selectedManifest;
            }
        }

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

        // note: Start with an empty selection so both the curated-origin and generated-site branches satisfy definite assignment before validation.
        HashSet<string> selectedCellIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        string preparedCanonicalRegionId = string.Empty;
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntime)
        {
            IReadOnlyList<YQAssetFunctionV2> siteFunctions = null;
            YQSemanticSiteCompositionV2 composition = default;
            bool curatedOriginSite = IsCuratedOriginSite(locationId);
            // note: Curated origin landmarks are not generated settlement sites. All other V2 sites must carry the same typed requirements used at preflight.
            if (curatedOriginSite)
            {
                // note: The Witch House structural showcase cell stays within the semantic budget and is runtime-prefiltered to the bounded hut structure before publication.
                selectedCellIds = BuildCuratedOriginCellIds(selectedManifest);
                if (selectedCellIds == null || selectedCellIds.Count == 0)
                {
                    Debug.LogError("[YQCompiledWorldSiteInstance] PREPARE REJECTED: curated origin cells are missing.");
                    completed?.Invoke(false);
                    yield break;
                }
            }
            else
            {
                GeneratedWorldPlanRecord plan = WorldStateManager.Instance != null && WorldStateManager.Instance.State != null
                    ? WorldStateManager.Instance.State.generatedWorldPlan : null;
                if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out string preparationFailure) ||
                    !prepared.TryGetSiteBySemanticId(locationId, out YQSpatialMaterializationSiteV2 acceptedSite) ||
                    acceptedSite.RequiredFunctions.Count == 0)
                {
                    Debug.LogError("[YQCompiledWorldSiteInstance] PREPARE REJECTED: accepted V2 site functions are missing. Location=" +
                        locationId + ", reason=" + preparationFailure);
                    completed?.Invoke(false);
                    yield break;
                }
                // note: Carry the accepted region identity into the staged site so reviewed storage can bind to the same persisted world record as the generator.
                preparedCanonicalRegionId = acceptedSite.parentRegionId ?? string.Empty;
                // note: Resource-area POIs retain their accepted cultural/transition/reward intent, while the reviewed authored shell is supplied by the qualified home provider and the generated landmark resource supplies the reward interaction.
                siteFunctions = GetReviewedProviderFunctions(acceptedSite);
            }
            // note: Keep the generated composition in a declared local so the curated-origin bypass remains definitely assigned under all compiler paths.
            if (!curatedOriginSite &&
                !TryBuildSemanticCompositionV2(
                    selectedManifest,
                    requiredSemanticTags,
                    selectionSeed,
                    out composition,
                    out string compositionFailure,
                    siteFunctions,
                    record.structureUsagePolicy))
            {
                Debug.LogError(
                    "[YQCompiledWorldSiteInstance] PREPARE REJECTED\n" +
                    "Location: " + locationId + "\n" +
                    "Reviewed site: " + record.kitId + "\n" +
                    "Reason: " + compositionFailure);
                completed?.Invoke(false);
                yield break;
            }

            if (!curatedOriginSite)
            {
                selectedCellIds = new HashSet<string>(
                    composition.selectedIds,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
        else
        {
            selectedCellIds = BuildActiveCellIds(
                selectedManifest,
                requiredSemanticTags,
                selectionSeed);
        }
        if (!TryValidateSelectedSite(
                selectedManifest,
                selectedCellIds,
                record.presentationMode,
                siteRoot,
                out Vector3 _unusedOrigin,
                out float validatedRadius,
                out string validationFailure,
                selectionSeed,
                enforceOriginTerrainBoundary: !YQWorldGenerationArchitecture.UsesV2SpatialRuntime ||
                    IsCuratedOriginSite(locationId)))
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
        // note: Make the compatibility boundary explicit so an unsupported kit cannot masquerade as a newly composed settlement.
        if (YQProceduralSettlementLayout.Enabled(selectionSeed) && YQProceduralSettlementLayout.Get(selectionSeed) == null)
            Debug.LogWarning("[WORLDGEN] AUTHORED CELL COMPATIBILITY: location=" + locationId + ", kit=" + record.kitId +
                ". Independent exterior block connections are unavailable; this site retains authored composition.");
        streamingSite.Prepare(
            locationId,
            record.runtimeManifestResourceKey,
            record.kitId,
            record.presentationMode,
            requiredSemanticTags,
            selectionSeed,
            validatedRadius,
            selectedManifest,
            selectedCellIds,
            preparedCanonicalRegionId);
        completed?.Invoke(true);
    }

    private void Prepare(
        string locationId,
        string resourceKey,
        string kitId,
        YQWorldSitePresentationMode newPresentationMode,
        string[] requiredSemanticTags = null,
        string newSelectionSeed = null,
        float validatedRadius = 0f,
        YQReviewedSemanticSiteManifest preparedManifest = null,
        HashSet<string> preparedCellIds = null,
        string preparedCanonicalRegionId = null)
    {
        settlementId = locationId ?? string.Empty;
        canonicalRegionId = preparedCanonicalRegionId ?? string.Empty;
        runtimeManifestResourceKey = resourceKey ?? string.Empty;
        expectedKitId = kitId ?? string.Empty;
        presentationMode = newPresentationMode;
        semanticSliceTags = requiredSemanticTags != null
            ? (string[])requiredSemanticTags.Clone()
            : Array.Empty<string>();
        semanticSliceSeed = newSelectionSeed ?? string.Empty;
        activeCellIds = preparedCellIds != null
            ? new HashSet<string>(preparedCellIds,
                StringComparer.OrdinalIgnoreCase)
            : null;
        // note: Remember the authority used for this prepared instance; a missing V2 selection must never silently invoke legacy tag scatter.
        requiresV2CellSelection = YQWorldGenerationArchitecture.UsesV2SpatialRuntime;
        // note: The caller has already validated this exact aggregate radius; preserving it verbatim prevents clamping malformed data into an accepted streaming contract.
        preparedSiteRadius = validatedRadius;
        loadedSiteRadius = 0f;
        // note: Retain the exact manifest used for radius and cell validation; discarding it forced a second heavy Resources load immediately after gameplay unlock.
        manifest = preparedManifest;
        // note: Bind the durable settlement identity to the real authored site root so feature overlays can target it before gameplay publication and after site rebind.
        YQStreamedFeatureOverlayTarget overlayTarget = gameObject.GetComponent<YQStreamedFeatureOverlayTarget>() ?? gameObject.AddComponent<YQStreamedFeatureOverlayTarget>();
        overlayTarget.featureId = settlementId;
        overlayTarget.objectId = gameObject.name;
        authoredOrigin = Vector3.zero;
        loaded = false;
        loading = false;
        loadRejected = false;
        loadFailure = string.Empty;
        streamScheduledLogged = false;
        // note: Stable per-site phasing prevents every prepared settlement from running its distance check on the same frame.
        nextDistanceCheckTime = Time.unscaledTime +
            StableDistanceCheckPhase(settlementId);
        Instances[settlementId] = this;

        // note: Emit construction evidence at preparation time so a playable authored slice cannot be mistaken for a population-scaled procedural town.
        var constructionOwner = YQProceduralSettlementLayout.FindOwner(semanticSliceSeed);
        var constructionReport = YQWorldConstructionReport.Describe(settlementId,
            manifest, activeCellIds, semanticSliceSeed,
            YQProceduralSettlementLayout.Get(semanticSliceSeed),
            constructionOwner != null ? constructionOwner.proceduralBlockTarget : 0);
        Debug.Log("[WORLDGEN CONSTRUCTION] " + JsonUtility.ToJson(constructionReport));

        Debug.Log(
            "[YQCompiledWorldSiteInstance] SITE PREPARED\n" +
            "Location: " + settlementId + "\n" +
            "Reviewed site: " + expectedKitId + "\n" +
            "Runtime root: " + transform.name + "\n" +
            "World position: " + transform.position + "\n" +
            "Validated radius: " + preparedSiteRadius.ToString("F2") + "m\n" +
            "Semantic tags: " +
            (semanticSliceTags.Length > 0
                ? string.Join(", ", semanticSliceTags)
                : "<full reviewed site>"));
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

    private static bool IsCuratedOriginSite(string locationId)
    {
        // note: Only the builder-owned Witch House landmark may bypass generated-site V2 and player-distance contracts.
        return string.Equals(locationId, "origin_vey_witch_house",
            StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildCuratedOriginCellIds(
        YQReviewedSemanticSiteManifest manifest)
    {
        // note: Prefer the reviewed structural showcase cell; its 656 source objects remain within the semantic budget and the runtime curator retains only the bounded hut structure instead of cloning the showcase wholesale.
        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (manifest == null || manifest.Zones == null ||
            manifest.StreamingSite == null || manifest.StreamingSite.Cells == null)
            return selected;

        Dictionary<string, YQAuthoredSiteStreamingCellRecord> cellsById =
            new Dictionary<string, YQAuthoredSiteStreamingCellRecord>(
                StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < manifest.StreamingSite.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = manifest.StreamingSite.Cells[index];
            if (cell != null && !string.IsNullOrWhiteSpace(cell.StableCellId))
                cellsById[cell.StableCellId] = cell;
        }

        const string structuralShowcaseCellId =
            "yq_cell_witch_house_p1_p1_p00";
        if (cellsById.ContainsKey(structuralShowcaseCellId))
        {
            selected.Add(structuralShowcaseCellId);
            return selected;
        }

        // note: Keep a role-based fallback for regenerated manifests that have not yet emitted the structural showcase cell.
        string[] requiredOriginRoles = { "entrance", "room", "circulation", "service" };
        for (int roleIndex = 0; roleIndex < requiredOriginRoles.Length; roleIndex++)
        {
            string role = requiredOriginRoles[roleIndex];
            for (int zoneIndex = 0; zoneIndex < manifest.Zones.Count; zoneIndex++)
            {
                YQReviewedSemanticZoneRecord zone = manifest.Zones[zoneIndex];
                if (zone == null || zone.semanticTags == null ||
                    zone.streamingCellIds == null)
                    continue;

                bool hasRoleTag = false;
                for (int tagIndex = 0; tagIndex < zone.semanticTags.Count; tagIndex++)
                {
                    if (string.Equals(zone.semanticTags[tagIndex], role,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        hasRoleTag = true;
                        break;
                    }
                }
                if (!hasRoleTag)
                    continue;

                for (int cellIndex = 0; cellIndex < zone.streamingCellIds.Count; cellIndex++)
                {
                    string cellId = zone.streamingCellIds[cellIndex];
                    if (string.IsNullOrWhiteSpace(cellId) || !cellsById.ContainsKey(cellId))
                        continue;
                    selected.Add(cellId);
                }
                break;
            }
        }

        return selected;
    }

    // note: Attribute this project-owned callback during the focused G08 frame-budget witness.
    private void Update()
    {
        using (G08UpdateMarker.Auto())
            UpdateCore();
    }

    private void UpdateCore()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextDistanceCheckTime)
            return;

        nextDistanceCheckTime = Time.unscaledTime + DistanceCheckInterval;
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        bool pinnedOriginSite = IsCuratedOriginSite(settlementId);
        // note: The pinned origin must stream during startup before the authoritative motor registers; distance-gated sites still require that motor contract.
        if (!pinnedOriginSite && (motor == null || !motor.IsAuthoritative))
            return;

        float distanceSquared = motor != null
            ? (motor.transform.position - transform.position).sqrMagnitude
            : 0f;

        float loadRadius = LoadDistance + preparedSiteRadius;

        if (!loaded && !loading && !unloading && !loadRejected &&
            ActiveStreamLoader == null &&
            (pinnedOriginSite ||
             (!YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked &&
              distanceSquared <= loadRadius * loadRadius)))
        {
            // note: Only one authored hierarchy may enter Unity at a time; inactive staging starts by distance rather than camera visibility, because looking toward an empty town site must never suppress its construction.
            ActiveStreamLoader = this;
            if (!streamScheduledLogged)
            {
                streamScheduledLogged = true;
                Debug.Log(
                    "[YQCompiledWorldSiteInstance] SITE STREAM SCHEDULED\n" +
                    "Location: " + settlementId + "\n" +
                    "Reviewed site: " + expectedKitId + "\n" +
                    "Player distance: " + Mathf.Sqrt(distanceSquared).ToString("F1") + "m\n" +
                    "Load radius: " + loadRadius.ToString("F1") + "m");
            }
            Coroutine started = StartCoroutine(LoadPreparedSiteGuardedRoutine());
            streamLoadCoroutine = loading ? started : null;
        }
        else if (loaded && !loading && !unloading &&
                 !pinnedOriginSite)
        {
            float unloadRadius = UnloadDistance + loadedSiteRadius;
            float hardUnloadRadius = unloadRadius + HardUnloadDistanceMargin;
            bool outsideUnloadRadius = distanceSquared >= unloadRadius * unloadRadius;
            bool outsideHardLimit = distanceSquared >= hardUnloadRadius * hardUnloadRadius;
            if (outsideUnloadRadius &&
                (!IsVisibleToGameplayCamera() || outsideHardLimit))
            {
                // note: Normal retirement waits for the settlement to leave the camera; the hard distance cap prevents a backward-looking camera from retaining remote worlds forever.
                streamUnloadExecution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
                    UnloadPreparedSiteRoutine(), ReportStreamExecutionFailure);
                Coroutine started = StartCoroutine(streamUnloadExecution);
                streamUnloadCoroutine = unloading ? started : null;
            }
        }
    }

    private IEnumerator LoadPreparedSiteRoutine()
    {
        loading = true;
        ReportSiteLoadingProgress("Loading reviewed assets", false);
        Debug.Log(
            "[YQCompiledWorldSiteInstance] SITE STREAM STARTED " +
            settlementId + " -> " + expectedKitId);
        YQReviewedSemanticSiteManifest selectedManifest = manifest;
        if (selectedManifest == null &&
            !PreparedManifestCache.TryGetValue(
                runtimeManifestResourceKey,
                out selectedManifest))
        {
            // note: Recovery-only path; normal prepared sites retain the manifest already validated during terrain construction.
            ResourceRequest request = Resources.LoadAsync<
                YQReviewedSemanticSiteManifest>(runtimeManifestResourceKey);
            yield return request;
            selectedManifest =
                request.asset as YQReviewedSemanticSiteManifest;
        }

        if (selectedManifest == null || !selectedManifest.ReleaseEligible ||
            !string.Equals(selectedManifest.KitId, expectedKitId,
                StringComparison.OrdinalIgnoreCase))
        {
            // note: Keep the admission failure visible to the owning settlement transaction while preserving the terminal rejection.
            loadFailure = "reviewed runtime manifest missing, unreleased, or mismatched";
            loading = false;
            loadRejected = true;
            ReleaseStreamLoadSlot();
            Debug.LogError(
                "[YQCompiledWorldSiteInstance] STREAM LOAD REJECTED " +
                settlementId + " -> " + expectedKitId);
            yield break;
        }

        manifest = selectedManifest;
        PreparedManifestCache[runtimeManifestResourceKey] = selectedManifest;
        if (activeCellIds == null)
        {
            // note: Curated origin landmarks intentionally bypass generated V2 function selection; recover their reviewed semantic slice from the same manifest instead of rejecting startup.
            bool curatedOrigin = IsCuratedOriginSite(settlementId);
            if (requiresV2CellSelection && !curatedOrigin)
            {
                // note: A V2 site without its persisted selected cells cannot safely fall back to an arbitrary legacy composition.
                loadFailure = "persisted V2 cell selection missing";
                loadRejected = true;
                loading = false;
                ReleaseStreamLoadSlot();
                Debug.LogError("[WORLDGEN ERROR] V2 site lost its prepared cell selection; refusing legacy reselection. Location=" + settlementId, this);
                yield break;
            }
            activeCellIds = BuildActiveCellIds(
                selectedManifest,
                semanticSliceTags,
                semanticSliceSeed);
        }
        if (semanticSliceTags != null && semanticSliceTags.Length > 0 &&
            selectedManifest.StreamingSite != null)
        {
            int selectedSourceInstances = 0;
            int selectedCellCount = 0;
            for (int index = 0;
                 index < selectedManifest.StreamingSite.Cells.Count;
                 index++)
            {
                YQAuthoredSiteStreamingCellRecord cell =
                    selectedManifest.StreamingSite.Cells[index];
                if (cell == null ||
                    (activeCellIds != null &&
                     !activeCellIds.Contains(cell.StableCellId)))
                {
                    continue;
                }

                selectedCellCount++;
                selectedSourceInstances += Mathf.Max(1,
                    cell.SourceInstanceCount);
            }

            // note: One bounded diagnostic exposes semantic-slice cost before cloning, making an accidental golden-example load visible without per-frame log spam.
            Debug.Log(
                "[YQCompiledWorldSiteInstance] SEMANTIC SLICE BUDGET\n" +
                "Location: " + settlementId + "\n" +
                "Selected cells: " + selectedCellCount + "\n" +
                "Estimated source instances: " +
                selectedSourceInstances + "/" +
                SettlementSemanticInstanceBudget);
        }
        if (!TryValidateSelectedSite(
                selectedManifest,
                activeCellIds,
                presentationMode,
                transform,
                out authoredOrigin,
                out loadedSiteRadius,
                out string validationFailure,
                semanticSliceSeed,
                // note: Accepted V2 sites own streamed terrain outside the finite origin tile; only curated origin sites retain the legacy boundary check.
                enforceOriginTerrainBoundary: !YQWorldGenerationArchitecture.UsesV2SpatialRuntime ||
                    IsCuratedOriginSite(settlementId)))
        {
            // note: Preserve the validator's exact reason so a rejected site can be repaired from evidence rather than guessed around.
            loadFailure = "selected-site validation failed: " + validationFailure;
            // note: A rejected spatial contract is terminal for this prepared instance; retrying every distance poll would reload the same broken pack and spam the player log.
            manifest = null;
            loading = false;
            loadRejected = true;
            ReleaseStreamLoadSlot();
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
        int requiredParts = 0;
        int traversalColliderCount = 0;
        int disabledReflectionProbeCount = 0;
        int removedPreviewArtifactCount = 0;
        List<KeyValuePair<string, GameObject>> spawnedCells =
            new List<KeyValuePair<string, GameObject>>(MaximumSettlementSemanticCells);
        float frameStartedAt = Time.realtimeSinceStartup;
        // note: Cells are assembled under an inactive staging root so malformed vendor LOD ownership can be repaired before Unity enables any LODGroup.
        GameObject contentRoot = new GameObject("CompiledSiteContent");
        pendingSiteContent = contentRoot;
        contentRoot.SetActive(false);
        contentRoot.transform.SetParent(transform, false);

        if (streaming != null)
        {
            for (int index = 0; index < streaming.Cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];

                if (cell == null)
                    continue;

                if (activeCellIds != null &&
                    !activeCellIds.Contains(cell.StableCellId))
                    continue;

                // note: A selected cell is mandatory even if its prefab fails to load or instantiate.
                requiredParts++;
                if (cell.CellPrefab == null)
                    continue;

                GameObject instance = null;
                int repairedColliders = 0;
                // note: Record the reviewed source and cloned hierarchy counts at the cell boundary so a rendererless stream failure identifies whether the prefab wrapper or clone path lost authored geometry.
                int sourceRendererCount = cell.CellPrefab.GetComponentsInChildren<Renderer>(true).Length;
                int sourceTransformCount = cell.CellPrefab.GetComponentsInChildren<Transform>(true).Length;
                // note: A reviewed cell can contain hundreds of authored objects; Unity's asynchronous clone path keeps that hierarchy copy from monopolizing the Goddess/loading frame.
                ReportSiteLoadingProgress("Assembling cell " + cell.StableCellId, false);
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
                int clonedRendererCount = instance.GetComponentsInChildren<Renderer>(true).Length;
                int clonedTransformCount = instance.GetComponentsInChildren<Transform>(true).Length;
                Debug.Log(
                    "[YQCompiledWorldSiteInstance] CELL HIERARCHY BOUNDARY\n" +
                    "Cell: " + cell.StableCellId + "\n" +
                    "Source transforms/renderers: " + sourceTransformCount + "/" + sourceRendererCount + "\n" +
                    "Clone transforms/renderers: " + clonedTransformCount + "/" + clonedRendererCount + "\n" +
                    "Clone renderer names: " + SummarizeRendererNames(instance));
                instance.name = "CompiledCell__" + cell.StableCellId;
                instance.transform.localPosition =
                    cell.AuthoredLocalPosition - authoredOrigin;
                instance.transform.localRotation = Quaternion.identity;
                // note: Accepted independent layouts own streamed transforms; old authored slices retain their original placement.
                YQProceduralSettlementLayout.ApplyPlacement(YQProceduralSettlementLayout.Get(semanticSliceSeed),
                    cell.StableCellId, instance.transform, authoredOrigin);
                // note: Known source-pack preview props are removed while the cell is still hidden so they cannot leak into the curated generated environment.
                yield return CurateKnownPreviewArtifactsRoutine(
                    instance,
                    expectedKitId,
                    count => removedPreviewArtifactCount += count);
                int postCurationRendererCount = instance.GetComponentsInChildren<Renderer>(true).Length;
                if (postCurationRendererCount == 0)
                {
                    // note: A selected cell that loses every renderer during preview cleanup is rejected as an unusable authored slice instead of being published as invisible geometry.
                    Debug.LogError(
                        "[WORLDGEN ERROR] Curated cell lost all renderers. Cell=" +
                        cell.StableCellId + ", removedPreviewArtifacts=" +
                        removedPreviewArtifactCount + ", cloneRendererNames=" +
                        SummarizeRendererNames(instance));
                }
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
                spawnedCells.Add(new KeyValuePair<string, GameObject>(cell.StableCellId, instance));
                spawned++;
                ReportSiteLoadingProgress("Prepared " + spawned + " cells; latest: " + cell.StableCellId, true);

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

                if (zone == null ||
                    !IsZoneActive(zone))
                    continue;

                requiredParts++;
                if (zone.prefab == null)
                    continue;

                GameObject instance = null;
                int repairedColliders = 0;
                // note: Legacy semantic zones use the same non-blocking clone boundary as reviewed streaming cells.
                ReportSiteLoadingProgress("Assembling zone " + zone.stableId, false);
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
                // note: Instantiate the same rigid-cell transform reserved by the terrain prepass, not the donor district's coordinates.
                if (YQProceduralSettlementLayout.TryPlacement(YQProceduralSettlementLayout.Get(semanticSliceSeed), zone.stableId, out var placement))
                {
                    instance.transform.localPosition = placement.position - authoredOrigin;
                    instance.transform.localRotation = Quaternion.Euler(0, placement.yaw, 0);
                }
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
                spawnedCells.Add(new KeyValuePair<string, GameObject>(zone.stableId, instance));
                spawned++;
                ReportSiteLoadingProgress("Prepared " + spawned + " zones; latest: " + zone.stableId, true);

                if (Time.realtimeSinceStartup - frameStartedAt >=
                    StreamingFrameBudgetSeconds)
                {
                    yield return null;
                    frameStartedAt = Time.realtimeSinceStartup;
                }
            }
        }

        // note: Do not publish the loaded state until LOD repair, exterior foundation alignment, and physics synchronization are complete; origin setup and NPC placement consume IsSiteLoaded as a readiness contract.
        bool contentSpawned = spawned > 0 && spawned == requiredParts;
        if (!contentSpawned)
        {
            // note: A missing selected cell is a load-admission failure, not an empty-but-playable settlement.
            loadFailure = "incomplete reviewed site: spawned=" + spawned + ", required=" + requiredParts;
            Debug.LogError("[WORLDGEN ERROR] Incomplete reviewed site. Location=" + settlementId +
                ", kit=" + expectedKitId + ", instantiated=" + spawned + ", required=" + requiredParts, this);
        }

        if (contentSpawned)
        {
            ReportSiteLoadingProgress("Checking LOD ownership and foundation alignment", false);
            // note: Keep the potentially expensive site-wide LOD ownership pass out of the same frame that instantiated the final authored cell.
            yield return null;
            // note: Repair renderer ownership once across the assembled site so duplicate LOD references spanning separate authored cells are also removed.
            yield return RepairDuplicateLodOwnershipRoutine(
                contentRoot);

            Terrain generatedTerrain = ResolveGeneratedTerrain(
                contentRoot.transform.position);
            int streamedSupportStampCount = 0;
            bool streamedTerrainCoverageReady = true;
            // note: A reviewed site may straddle multiple streamed collision tiles; request and wait for its full hidden footprint before foundation sampling can reject it as unsupported.
            if (generatedTerrain != null &&
                YQWorldGenerationArchitecture.UsesV2SpatialRuntime &&
                !IsCuratedOriginSite(settlementId) &&
                !UsesAuthoredTerrainRelief())
            {
                yield return WaitForStreamedSiteTerrainRoutine(
                    contentRoot,
                    generatedTerrain,
                    ready => streamedTerrainCoverageReady = ready);
            }
            // note: Streamed reviewed assemblies are staged before publication; prepare bounded support on every published collision tile touched by the authored footprint before judging foundation contacts.
            if (streamedTerrainCoverageReady &&
                generatedTerrain != null &&
                YQWorldGenerationArchitecture.UsesV2SpatialRuntime &&
                !IsCuratedOriginSite(settlementId) &&
                !UsesAuthoredTerrainRelief())
            {
                List<Terrain> supportTerrains = new List<Terrain>();
                CollectStreamedSiteTerrains(contentRoot, generatedTerrain, supportTerrains);
                for (int terrainIndex = 0; terrainIndex < supportTerrains.Count; terrainIndex++)
                {
                    List<YQTerrainSupportStamp> streamedSupportStamps =
                        new List<YQTerrainSupportStamp>();
                    YQTerrainSupportComposer.BuildRaisedAssemblySupportStamps(
                        contentRoot,
                        supportTerrains[terrainIndex],
                        streamedSupportStamps);
                    if (streamedSupportStamps.Count == 0)
                        continue;

                    int appliedStampCount = 0;
                    yield return YQTerrainSupportComposer.RaiseTerrainRoutine(
                        supportTerrains[terrainIndex],
                        streamedSupportStamps,
                        count => appliedStampCount = count);
                    streamedSupportStampCount += appliedStampCount;
                }
            }
            bool terrainSupportChanged = streamedSupportStampCount > 0;

            float foundationCorrection = 0f;
            bool hasFoundationCorrection = false;
            string foundationCorrectionSource = string.Empty;
            bool independentlyPlacedSite =
                YQProceduralSettlementLayout.Get(semanticSliceSeed) != null;
            Dictionary<GameObject, YQReviewedCellFunctionContractV2> groundingContracts =
                BuildGroundingContracts(selectedManifest, spawnedCells);
            bool hasSingleReviewedCellGroundingContract =
                spawnedCells.Count == 1 && groundingContracts.Count == 1;

            if (presentationMode ==
                    YQWorldSitePresentationMode.SeamlessExterior &&
                !UsesAuthoredTerrainRelief() &&
                !hasSingleReviewedCellGroundingContract)
            {
                string foundationCacheKey = BuildFoundationCacheKey();

                if (!terrainSupportChanged &&
                    TryResolveCompiledFoundationCorrection(
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
                else if (!terrainSupportChanged &&
                         TryResolveAuthoredDatumCorrection(
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
                else if (!terrainSupportChanged &&
                         ExteriorFoundationCorrectionCache.TryGetValue(
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
                            ? terrainSupportChanged
                                ? "runtime structural bounds after terrain support"
                                : "runtime structural bounds"
                            : string.Empty;
                    ExteriorFoundationCorrectionCache[foundationCacheKey] =
                        hasFoundationCorrection ? foundationCorrection : 0f;
                }
            }

            if (hasFoundationCorrection && !independentlyPlacedSite)
            {
                // note: The terrain prepass is canonical and wilderness already sampled it; lower the reviewed assembly to that surface instead of raising late terrain pillars beneath floating source geometry.
                contentRoot.transform.position +=
                    Vector3.down * foundationCorrection;
            }
            else if (hasFoundationCorrection && independentlyPlacedSite)
            {
                // note: Independent reviewed cells carry their own terrain-contact contracts; applying the aggregate root correction first would double-shift their entrances.
                foundationCorrection = 0f;
                foundationCorrectionSource = "per-cell reviewed terrain contacts";
            }

            int groundedCellCount = 0;
            bool cellGroundingReady = streamedTerrainCoverageReady;
            bool curatedWitchHouse = string.Equals(
                expectedKitId,
                "witch_house",
                StringComparison.OrdinalIgnoreCase);

            if (generatedTerrain != null && curatedWitchHouse)
            {
                // note: The curated Witch House discards its source-demo relief, so it must always bind to generated terrain even when legacy manifest presentation metadata is not SeamlessExterior.
                yield return CurateAndGroundWitchHouseRoutine(
                    contentRoot,
                    generatedTerrain,
                    (grounded, removed) =>
                    {
                        groundedCellCount = grounded;
                        removedPreviewArtifactCount += removed;
                    });
                cellGroundingReady = groundedCellCount > 0;
            }
            else if (streamedTerrainCoverageReady &&
                     generatedTerrain != null &&
                     !UsesAuthoredTerrainRelief())
            {
                // note: Only an explicit independent-block layout permits per-cell movement. Streaming chunks otherwise share one authored datum, including roofs, walls and furnishings spanning chunk boundaries.
                yield return AlignCompiledCellsToTerrainRoutine(
                    contentRoot,
                    generatedTerrain,
                    independentlyPlacedSite,
                    count => groundedCellCount = count,
                    groundingContracts);
                cellGroundingReady =
                    groundedCellCount == spawnedCells.Count &&
                    spawnedCells.Count > 0;
            }
            else if (!UsesAuthoredTerrainRelief())
            {
                cellGroundingReady = false;
            }

            int validatedMaterialRenderers = 0;
            int unresolvedMaterialSlots = 0;
            yield return YQRuntimeUrpMaterialRepair.ValidateMaterialHierarchyRoutine(
                contentRoot,
                (renderers, unresolved) =>
                {
                    validatedMaterialRenderers = renderers;
                    unresolvedMaterialSlots = unresolved;
                });
            bool materialCoverageReady =
                validatedMaterialRenderers > 0 && unresolvedMaterialSlots == 0;

            Debug.Log(
                "[YQCompiledWorldSiteInstance] SITE GROUNDING READY\n" +
                "Location: " + settlementId + "\n" +
                "Reviewed site: " + expectedKitId + "\n" +
                "Correction source: " + foundationCorrectionSource + "\n" +
                "Assembly lowering: " +
                (hasFoundationCorrection
                    ? foundationCorrection.ToString("F2")
                    : "0.00") + "m\n" +
                "Cells grounded to canonical terrain: " + groundedCellCount +
                "/" + spawnedCells.Count + "\n" +
                "Grounding contract ready: " + cellGroundingReady + "\n" +
                "Terrain footprint coverage ready: " + streamedTerrainCoverageReady + "\n" +
                "Terrain support stamps applied: " + streamedSupportStampCount + "\n" +
                "Material renderers validated: " + validatedMaterialRenderers + "\n" +
                "Unresolved material slots: " + unresolvedMaterialSlots);

            if (!cellGroundingReady)
            {
                // note: Record the grounding counts before hiding the staged hierarchy so the final settlement decision identifies this contract failure.
                if (string.IsNullOrWhiteSpace(loadFailure))
                    loadFailure = "complete cell grounding failed: grounded=" + groundedCellCount + ", required=" + spawnedCells.Count;
                // note: A partially grounded authored slice remains hidden; collider presence underneath a floating building is not proof of foundation contact.
                Debug.LogError(
                    "[WORLDGEN ERROR] Streamed site failed complete cell grounding. " +
                    "Location=" + settlementId + ", kit=" + expectedKitId +
                    ", grounded=" + groundedCellCount +
                    ", required=" + spawnedCells.Count + ".");
            }
            if (!materialCoverageReady)
            {
                // note: Preserve material coverage as a separate admission reason because valid geometry with unresolved slots is still non-playable.
                if (string.IsNullOrWhiteSpace(loadFailure))
                    loadFailure = "material coverage failed: renderers=" + validatedMaterialRenderers + ", unresolved=" + unresolvedMaterialSlots;
                // note: Unsupported or absent materials keep the hidden staging root unpublished; magenta/missing geometry is not a successful semantic site.
                Debug.LogError(
                    "[WORLDGEN ERROR] Streamed site failed material coverage. " +
                    "Location=" + settlementId + ", kit=" + expectedKitId +
                    ", renderers=" + validatedMaterialRenderers +
                    ", unresolvedSlots=" + unresolvedMaterialSlots + ".");
            }

            YQGeneratedWorldIntegrityValidator.Report integrityReport = null;
            // note: Collider and renderer integrity is mandatory before an inactive streamed hierarchy is exposed to the player or NPC population.
            if (cellGroundingReady && materialCoverageReady)
            {
                yield return YQGeneratedWorldIntegrityValidator.ValidateAndRepairRoutine(
                    contentRoot,
                    semanticSliceSeed,
                    "streamed_site:" + settlementId,
                    report => integrityReport = report);
            }

            if (integrityReport == null ||
                !integrityReport.IsValid ||
                !integrityReport.HasPlayableCollision)
            {
                // note: Capture the collision gate before quarantining the staged content so a required site never reports only a generic geometry failure.
                if (string.IsNullOrWhiteSpace(loadFailure))
                    loadFailure = "walkable collision failed: missing=" +
                        (integrityReport != null ? integrityReport.missingColliders : -1) +
                        ", enabled=" +
                        (integrityReport != null ? integrityReport.enabledNonTriggerColliders : -1);
                Debug.LogError(
                    "[WORLDGEN ERROR] Streamed site failed walkable collision validation. " +
                    "Location=" + settlementId + ", kit=" + expectedKitId +
                    ", missingColliders=" +
                    (integrityReport != null ? integrityReport.missingColliders : -1) +
                    ", collisionCoverage=" +
                    (integrityReport != null ? integrityReport.enabledNonTriggerColliders : -1) + ".");
                contentRoot.SetActive(false);
                Destroy(contentRoot);
                loadRejected = true;
                contentSpawned = false;
            }
            else
            {
                bool providersReady = true;
                if (YQWorldGenerationArchitecture.UsesV2SpatialRuntime)
                {
                    foreach (KeyValuePair<string, GameObject> cell in spawnedCells)
                    {
                        yield return BindReviewedCellDoorsRoutine(selectedManifest, cell.Key,
                            cell.Value, success => providersReady = success);
                        if (!providersReady)
                            break;
                    }
                }
                if (!providersReady)
                {
                    // note: Reviewed interaction binding is part of site admission; keep the staged hierarchy hidden when a provider is invalid.
                    if (string.IsNullOrWhiteSpace(loadFailure))
                        loadFailure = "reviewed interaction provider binding failed";
                    // note: A broken approved provider keeps the whole staged site hidden; exposing visual shells as functioning cells would violate the accepted plan.
                    Destroy(contentRoot);
                    loadRejected = true;
                    contentSpawned = false;
                }
                else
                {
                    // note: Players never see a floating or non-colliding intermediate frame; activation follows grounding, collider validation and reviewed interaction binding.
                    ReportSiteLoadingProgress("Grounding and collision verified; activating reviewed cells", true);
                    // note: The staged site has already passed grounding, integrity, and provider checks; publish it atomically without a timer-based reveal fallback.
                    yield return ActivateHierarchyCooperativelyRoutine(contentRoot);
                    yield return RelocateRegisteredResidentsRoutine();
                }
            }
        }
        else
        {
            // note: Preserve the non-spawned terminal state even when the staged content was discarded before publication.
            if (string.IsNullOrWhiteSpace(loadFailure))
                loadFailure = "reviewed site produced no publishable content";
            Destroy(contentRoot);
            loadRejected = true;
        }

        // note: Newly enabled streamed colliders join the next normal physics step; forcing a full-scene synchronization here caused a loading hitch and was immediately repeated by actor relocation.
        yield return null;
        loaded = contentSpawned;
        loading = false;
        if (loaded)
        {
            // note: Replay durable overlays at the single authoritative site-publication boundary so every load owner cannot resurrect a deleted feature.
            YQPlayerFollowingSemanticChunkStreamer overlayStreamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            YQStreamedFeatureOverlayTarget overlayTarget = GetComponent<YQStreamedFeatureOverlayTarget>();
            if (overlayStreamer != null && overlayTarget != null &&
                !string.IsNullOrWhiteSpace(overlayTarget.featureId))
                overlayStreamer.RegisterFeatureOverlayTarget(overlayTarget.featureId, gameObject, overlayTarget.objectId);
        }
        if (loaded)
            ReportSiteLoadingProgress("Site ready", true);
        ReleaseStreamLoadSlot();
        // note: Persisted actors already retain authoritative world positions; rescanning every EntityInfo and forcing physics whenever a site streams in caused both loading and traversal hitches.
        // note: Distance streaming already bounds this site's lifetime; rescanning the whole generated world here previously allocated a renderer table large enough to stall dense authored maps.
        Debug.Log(
            (loaded ? "[YQCompiledWorldSiteInstance] SITE STREAMED IN\n" :
                "[YQCompiledWorldSiteInstance] SITE STREAM REJECTED\n") +
            "Location: " + settlementId + "\n" +
            "Reviewed site: " + expectedKitId + "\n" +
            "Cells/zones: " + spawned + "\n" +
            "Malformed vendor colliders disabled: " +
            sanitizedColliderCount + "\n" +
            "Traversal obstruction colliders disabled/tightened: " +
            traversalColliderCount + "\n" +
            "Source preview artifacts removed: " +
            removedPreviewArtifactCount + "\n" +
            "Imported reflection probes disabled: " +
            disabledReflectionProbeCount);
    }

    private IEnumerator BindReviewedCellDoorsRoutine(YQReviewedSemanticSiteManifest source,
        string cellId, GameObject cell, Action<bool> completed)
    {
        YQReviewedCellFunctionContractV2 contract = null;
        foreach (YQReviewedSemanticZoneRecord zone in source.Zones)
        {
            if (zone?.cellContractsV2 == null)
                continue;
            foreach (YQReviewedCellFunctionContractV2 candidate in zone.cellContractsV2)
            {
                if (candidate == null || candidate.reviewState != YQSemanticSiteReviewState.Approved ||
                    !string.Equals(candidate.cellId, cellId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (contract != null || candidate.sourceSignature != source.SourceSignature)
                {
                    Debug.LogError("[YQCompiledWorldSiteInstance] Conflicting/stale door contract: " + cellId);
                    completed?.Invoke(false);
                    yield break;
                }
                contract = candidate;
            }
        }
        if (contract == null || ((contract.doorBindings == null || contract.doorBindings.Count == 0) &&
            (contract.lootBindings == null || contract.lootBindings.Count == 0)))
        {
            completed?.Invoke(true);
            yield break;
        }

        GeneratedWorldPlanRecord plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        GeneratedSettlementRecord settlement = plan?.settlements?.Find(item => item != null && item.settlementId == settlementId);
        GeneratedEncampmentRecord hostile = plan?.encampments?.Find(item => item != null && item.encampmentId == settlementId);
        string label = settlement != null ? settlement.displayName : hostile != null ? hostile.displayName : settlementId;
        // note: V2 site identity is authoritative even when the legacy settlement/encampment lookup is unavailable during a retry.
        string regionId = settlement != null ? settlement.regionId : hostile != null ? hostile.regionId : canonicalRegionId;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (YQCellDoorBindingV2 binding in contract.doorBindings ?? new List<YQCellDoorBindingV2>())
        {
            // note: One explicit yield per candidate bounds even an entirely unapproved cell; no name search or scene-wide component scan runs during streaming.
            yield return null;
            if (binding == null || binding.reviewState != YQSemanticSiteReviewState.Approved)
                continue;
            if (!ids.Add(binding.bindingId ?? string.Empty) || !paths.Add(binding.targetPath ?? string.Empty) || ids.Count > 32)
            {
                Debug.LogError("[YQCompiledWorldSiteInstance] Duplicate or over-budget door providers: " + cellId);
                completed?.Invoke(false);
                yield break;
            }
            // note: V2's final placement gate uses the actual generated tile after cell grounding. A plan that still needs earthworks cannot publish an apparently usable door.
            Vector3 terrainHandoff = binding.terrainApproach != null
                ? cell.transform.TransformPoint(binding.terrainApproach.localStart) : cell.transform.position;
            Terrain generatedTerrain = ResolveGeneratedTerrain(terrainHandoff);
            YQPlayerFollowingSemanticChunkStreamer terrainStreamer =
                YQPlayerFollowingSemanticChunkStreamer.Active ??
                FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            YQTerrainApproachV2.SampleHeight seamAwareSample =
                (Vector3 point, out float height) =>
                    TrySampleGeneratedTerrainAcrossTiles(
                        generatedTerrain,
                        terrainStreamer,
                        point,
                        out height);
            List<Terrain> approachTerrains =
                CollectApproachTerrains(
                    cell.transform,
                    binding.terrainApproach,
                    generatedTerrain,
                    terrainStreamer);
            // note: Record the final post-grounding handoff once when certification fails so placement drift can be fixed at its owner.
            if (generatedTerrain != null && binding.terrainApproach != null &&
                YQTerrainApproachV2.TrySampleTerrain(generatedTerrain, terrainHandoff, out float handoffSoil))
            {
                Debug.LogWarning("[YQCompiledWorldSiteInstance] HANDOFF DIAGNOSTIC cell=" + cellId +
                    " worldStart=" + terrainHandoff + " soil=" + handoffSoil.ToString("F2") +
                    " cellWorld=" + cell.transform.position + " siteWorld=" + transform.position);
            }
            if (!YQTerrainApproachV2.TryValidateReviewedConnection(
                    cell.transform,
                    binding.terrainApproach,
                    seamAwareSample,
                    out string terrainFailure) &&
                !YQTerrainApproachV2.TryConstructReviewedConnection(
                    cell.transform,
                    binding.terrainApproach,
                    seamAwareSample,
                    approachTerrains,
                    out terrainFailure))
            {
                if (hostile != null)
                {
                    // note: Hostile camp door providers are optional presentation affordances; a non-fitting authored approach must not reject the whole generated camp.
                    Debug.LogWarning("[YQCompiledWorldSiteInstance] Optional hostile door skipped for " + cellId + ": " + terrainFailure);
                    continue;
                }
                Debug.LogError("[YQCompiledWorldSiteInstance] Terrain approach rejected for " + cellId + ": " + terrainFailure);
                completed?.Invoke(false);
                yield break;
            }
            if (!YQCellDoorBindingsV2.TryBind(cell.transform, binding, settlementId, cellId,
                    label + " Door", regionId, out string failure))
            {
                Debug.LogError("[YQCompiledWorldSiteInstance] Door provider rejected for " + cellId + ": " + failure);
                completed?.Invoke(false);
                yield break;
            }
        }
        // note: Storage providers share the cell's reviewed revision and inactive publication boundary. One yield per slot keeps installation budgeted.
        ids.Clear();
        paths.Clear();
        foreach (YQCellLootBindingV2 binding in contract.lootBindings ?? new List<YQCellLootBindingV2>())
        {
            yield return null;
            if (binding == null || binding.reviewState != YQSemanticSiteReviewState.Approved) continue;
            if (!ids.Add(binding.bindingId ?? string.Empty) || !paths.Add(binding.targetPath ?? string.Empty) || ids.Count > 32)
            {
                Debug.LogError("[YQCompiledWorldSiteInstance] Duplicate or over-budget storage provider: " + cellId);
                completed?.Invoke(false);
                yield break;
            }
            if (!YQCellLootBindingsV2.TryBind(cell.transform, binding, source.SourceSignature, settlementId, cellId,
                    label, regionId, hostile != null ? hostile.threatTier : 1, out string storageFailure))
            {
                Debug.LogError("[YQCompiledWorldSiteInstance] Reviewed storage provider rejected: " + cellId + " / " +
                    binding.bindingId + ": " + storageFailure);
                completed?.Invoke(false);
                yield break;
            }
        }
        completed?.Invoke(true);
    }

    private IEnumerator LoadPreparedSiteGuardedRoutine()
    {
        if (!isActiveAndEnabled || loading || unloading || loaded || loadRejected)
        {
            // note: A duplicate request must not release a slot still owned by an already-running load.
            if (!loading && !unloading)
                ReleaseStreamLoadSlot();
            yield break;
        }

        bool completedNormally = false;
        IEnumerator execution = YQGeneratedWorldRuntimeBuilder.RunOwnedGenerationRoutine(
            LoadPreparedSiteRoutine(), ReportStreamExecutionFailure);
        streamLoadExecution = execution;
        try
        {
            // note: Cooperative work is intentionally allowed to exceed wall-clock thresholds; a same-thread timer cannot detect a frozen Unity frame and previously aborted healthy large cells.
            yield return execution;
            completedNormally = true;
        }
        finally
        {
            // note: Stop/dispose the child runner before releasing the shared slot; unfinished site work cannot overlap the next owner.
            try { (execution as IDisposable)?.Dispose(); }
            finally
            {
                if (ReferenceEquals(streamLoadExecution, execution))
                    streamLoadExecution = null;
                streamLoadCoroutine = null;
                try
                {
                    if (!completedNormally && loading)
                    {
                        loading = false;
                        loaded = false;
                        loadRejected = true;
                        QuarantinePendingSiteContent();
                        Debug.LogError(
                            "[WORLDGEN ERROR] Streamed site coroutine ended unexpectedly; its slot was released. " +
                            "Location=" + settlementId + ", kit=" + expectedKitId + ".");
                    }
                    if (loaded && !loadRejected)
                        pendingSiteContent = null;
                }
                finally { ReleaseStreamLoadSlot(); }
            }
        }
    }

    private IEnumerator LoadPreparedSiteDetachedRoutine()
    {
        // note: Direct cell publication drives the existing staged loader even when its owning chunk is inactive; the content remains hidden under that parent until the chunk activation gate completes.
        if (loading || unloading || loaded || loadRejected)
            yield break;

        yield return LoadPreparedSiteRoutine();
    }

    private void ReportSiteLoadingProgress(string detail, bool completedWork)
    {
        if (!YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
            return;
        YQStartupLoadingScreen.SetGenerationSubstep(settlementId + ": " + detail);
        // note: Only completed work refreshes the watchdog; waiting or repainting a label cannot conceal a stalled load.
        if (completedWork)
            YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
    }

    private void ReportStreamExecutionFailure(Exception exception)
    {
        // note: Preserve the exception and site identity; rejected content stays non-playable and does not retry every distance-check tick.
        Debug.LogException(exception, this);
        Debug.LogError("[WORLDGEN ERROR] Site stream failed. Location=" + settlementId + ", kit=" + expectedKitId, this);
        loaded = false;
        loading = false;
        unloading = false;
        loadRejected = true;
        QuarantinePendingSiteContent();
        ReleaseStreamLoadSlot();
    }

    private void QuarantinePendingSiteContent()
    {
        // note: Retain rejected geometry for diagnostics/rebuild cleanup, but never leave a partially activated site playable.
        if (pendingSiteContent != null && pendingSiteContent.activeSelf)
            pendingSiteContent.SetActive(false);
    }

    private void CancelOwnedSiteStreaming()
    {
        bool interrupted = loading || unloading;
        // note: Detach ownership before invoking iterator cleanup; OnDisable/OnDestroy may both cancel the same site.
        Coroutine loadCoroutine = streamLoadCoroutine;
        Coroutine unloadCoroutine = streamUnloadCoroutine;
        IEnumerator loadExecution = streamLoadExecution;
        IEnumerator unloadExecution = streamUnloadExecution;
        streamLoadCoroutine = null;
        streamUnloadCoroutine = null;
        streamLoadExecution = null;
        streamUnloadExecution = null;
        // note: Publish intentional cancellation before disposal runs a parent's finally block; it is not an unexpected stream failure.
        loading = false;
        unloading = false;
        if (interrupted)
        {
            // note: A disabled partial site requires a fresh prepared instance, not a second load on top of orphaned cells.
            loaded = false;
            loadRejected = true;
        }
        try
        {
            // note: Attempt both cleanup paths even if one iterator's failure callback throws.
            try { StopOwnedSiteExecution(loadCoroutine, loadExecution); }
            finally { StopOwnedSiteExecution(unloadCoroutine, unloadExecution); }
        }
        finally
        {
            // note: Release the queue even if hiding interrupted geometry invokes failing lifecycle work.
            try { if (interrupted) QuarantinePendingSiteContent(); }
            finally { ReleaseStreamLoadSlot(); }
        }
    }

    private void StopOwnedSiteExecution(Coroutine coroutine, IEnumerator execution)
    {
        // note: Unity owns the scheduler handle, while this site owns explicit nested-iterator disposal.
        try { if (coroutine != null) StopCoroutine(coroutine); }
        finally { (execution as IDisposable)?.Dispose(); }
    }

    private bool IsVisibleToGameplayCamera()
    {
        if (StreamVisibilityCamera == null ||
            !StreamVisibilityCamera.isActiveAndEnabled)
        {
            StreamVisibilityCamera = Camera.main;
        }

        Camera camera = StreamVisibilityCamera;
        if (camera == null || !camera.isActiveAndEnabled)
            return true;

        Vector3 toSite = transform.position - camera.transform.position;
        float forwardDistance = Vector3.Dot(camera.transform.forward, toSite);
        float radius = Mathf.Max(8f, preparedSiteRadius);
        if (forwardDistance < -radius)
            return false;

        Vector3 viewport = camera.WorldToViewportPoint(transform.position);
        float distance = Mathf.Max(1f, toSite.magnitude);
        float margin = Mathf.Clamp(radius / distance, 0.04f, 0.42f);

        // note: A conservative radius margin treats any potentially visible edge as visible without allocating frustum planes during periodic stream checks.
        return viewport.z > -radius &&
               viewport.x >= -margin && viewport.x <= 1f + margin &&
               viewport.y >= -margin && viewport.y <= 1f + margin;
    }

    private IEnumerator WaitForHiddenActivationWindowRoutine()
    {
        if (YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
            yield break;

        float waitStartedAt = Time.realtimeSinceStartup;
        while (IsVisibleToGameplayCamera() &&
               Time.realtimeSinceStartup - waitStartedAt < HiddenActivationWaitSeconds)
        {
            YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
            float urgentRadius = UrgentActivationDistance + preparedSiteRadius;
            if (motor == null ||
                (motor.transform.position - transform.position).sqrMagnitude <=
                urgentRadius * urgentRadius)
            {
                // note: Collision and settlement availability take priority once the player is close enough to reach the staged site.
                yield break;
            }

            yield return null;
        }

        // note: The short bounded window hides ordinary background publication without allowing camera direction to block the global stream loader.
    }

    private void ReleaseStreamLoadSlot()
    {
        if (ActiveStreamLoader == this)
            ActiveStreamLoader = null;
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

        // note: Restore authored descendant active states while the staging root is still hidden, preventing any partial-visible frame.
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
                // note: Restore authored activeSelf state under the hidden root; no renderer or behaviour can publish until the final root promotion.
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

        // note: Publish the complete validated hierarchy in one root activation after all descendant states are prepared.
        contentRoot.SetActive(true);
        yield return null;
    }

    private string BuildFoundationCacheKey()
    {
        return BuildFoundationSelectionCacheKey(expectedKitId, runtimeManifestResourceKey,
            semanticSliceSeed, activeCellIds, authoredOrigin.y);
    }

    public static string BuildFoundationSelectionCacheKey(string kitId, string manifestKey,
        string selectionSeed, IEnumerable<string> selectedCells, float authoredDatum)
    {
        // note: Same genre tags can select different foundations; key the exact accepted variant and coordinate datum instead.
        List<string> cells = new List<string>(selectedCells ?? Array.Empty<string>());
        cells.Sort(StringComparer.OrdinalIgnoreCase);
        System.Text.StringBuilder key = new System.Text.StringBuilder(128);
        string[] parts = { kitId, manifestKey, selectionSeed,
            authoredDatum.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            selectedCells == null ? "all" : "selected" };
        foreach (string part in parts)
        {
            string value = part ?? string.Empty;
            key.Append(value.Length).Append(':').Append(value);
        }
        foreach (string cell in cells)
        {
            string value = cell ?? string.Empty;
            // note: Length prefixes avoid collisions when an authored ID contains a separator character.
            key.Append(value.Length).Append(':').Append(value);
        }
        return key.ToString();
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

    private IEnumerator UnloadPreparedSiteRoutine()
    {
        if (unloading)
            yield break;

        unloading = true;
        loaded = false;
        List<Transform> hierarchy = new List<Transform>();
        Stack<Transform> pending = new Stack<Transform>();
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
                if (string.Equals(child.name, "CompiledSiteContent", StringComparison.Ordinal))
                    pendingSiteContent = child.gameObject;
                pending.Push(child);
            }
        }

        float frameStartedAt = Time.realtimeSinceStartup;
        int workThisFrame = 0;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;
            hierarchy.Add(current);
            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                pending.Push(current.GetChild(childIndex));
            workThisFrame++;

            if (workThisFrame >= StreamOutDiscoveryLimitPerFrame ||
                Time.realtimeSinceStartup - frameStartedAt >= StreamingFrameBudgetSeconds)
            {
                // note: Stream-out discovery is incremental so even a large reviewed city cannot cause a hierarchy-scan frame peak.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
                workThisFrame = 0;
            }
        }

        workThisFrame = 0;
        for (int index = hierarchy.Count - 1; index >= 0; index--)
        {
            Transform current = hierarchy[index];
            if (current != null && current.gameObject.activeSelf)
                current.gameObject.SetActive(false);
            workThisFrame++;

            if (workThisFrame >= StreamOutDisableLimitPerFrame ||
                Time.realtimeSinceStartup - frameStartedAt >= StreamingFrameBudgetSeconds)
            {
                // note: Descendants deactivate before parents, preventing one parent toggle from unregistering the complete settlement in a single frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
                workThisFrame = 0;
            }
        }

        workThisFrame = 0;
        for (int index = hierarchy.Count - 1; index >= 0; index--)
        {
            Transform current = hierarchy[index];
            if (current != null)
                Destroy(current.gameObject);
            workThisFrame++;

            if (workThisFrame >= StreamOutDestroyLimitPerFrame ||
                Time.realtimeSinceStartup - frameStartedAt >= StreamingFrameBudgetSeconds)
            {
                // note: Bottom-up retirement distributes component destruction and OnDestroy work across frames rather than batching it at frame end.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
                workThisFrame = 0;
            }
        }

        // note: Clearing the selected manifest reference allows Unity to reclaim that pack after its instantiated cells are gone; the lightweight catalog remains resident.
        manifest = null;
        authoredOrigin = Vector3.zero;
        // note: Only geometry is streamed out. Retain the accepted cell IDs so returning to this location rebuilds the same functional V2 composition.
        loadedSiteRadius = 0f;
        unloading = false;
        pendingSiteContent = null;
        // note: Resources.UnloadUnusedAssets scans the complete live object graph and can stall gameplay even when its AsyncOperation is used; reclamation is deferred to controlled scene/loading boundaries.
        Debug.Log(
            "[YQCompiledWorldSiteInstance] SITE STREAMED OUT " +
            settlementId + " -> " + expectedKitId);
    }

    public static void RegisterResidentPositionBinding(
        string locationId,
        EntityInfo entity,
        string roleIntent,
        string seed,
        int index)
    {
        if (entity == null || string.IsNullOrWhiteSpace(locationId) ||
            !Instances.TryGetValue(
                locationId,
                out YQCompiledWorldSiteInstance site) ||
            site == null)
        {
            return;
        }

        for (int bindingIndex = 0;
             bindingIndex < site.residentPositionBindings.Count;
             bindingIndex++)
        {
            ResidentPositionBinding existing =
                site.residentPositionBindings[bindingIndex];
            if (existing != null && existing.entity == entity)
            {
                existing.roleIntent = roleIntent ?? string.Empty;
                existing.seed = seed ?? string.Empty;
                existing.index = index;
                return;
            }
        }

        site.residentPositionBindings.Add(new ResidentPositionBinding
        {
            entity = entity,
            roleIntent = roleIntent ?? string.Empty,
            seed = seed ?? string.Empty,
            index = index
        });
    }

    private IEnumerator RelocateRegisteredResidentsRoutine()
    {
        int movedThisFrame = 0;
        int movedTotal = 0;
        int unresolvedTotal = 0;
        float frameStartedAt = Time.realtimeSinceStartup;
        for (int bindingIndex = residentPositionBindings.Count - 1;
             bindingIndex >= 0;
             bindingIndex--)
        {
            ResidentPositionBinding binding =
                residentPositionBindings[bindingIndex];
            if (binding == null || binding.entity == null)
            {
                residentPositionBindings.RemoveAt(bindingIndex);
                continue;
            }

            if (TryResolveRolePosition(
                    binding.roleIntent,
                    binding.seed,
                    binding.index,
                    out Vector3 position,
                    binding.entity.transform) &&
                YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(binding.entity.gameObject, position))
            {
                // note: A streamed floor is a contact height, not an imported model's root pivot; preserve its feet-to-root offset on every reload.
                movedTotal++;
            }
            else
            {
                unresolvedTotal++;
            }

            movedThisFrame++;
            if (movedThisFrame >= ResidentRelocationLimitPerFrame ||
                Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                // note: At most two residents perform reviewed-floor projection per frame, bounding raycasts and component registration when a town appears.
                yield return null;
                movedThisFrame = 0;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (residentPositionBindings.Count > 0)
        {
            // note: One summary per site exposes missing semantic floor sockets without per-NPC console spam.
            Debug.Log(
                "[YQCompiledWorldSiteInstance] SITE RESIDENTS REBOUND\n" +
                "Location: " + settlementId + "\n" +
                "Moved to reviewed floors: " + movedTotal + "\n" +
                "Unresolved residents: " + unresolvedTotal);
        }
    }

    private sealed class ResidentPositionBinding
    {
        public EntityInfo entity;
        public string roleIntent;
        public string seed;
        public int index;
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

    public static bool TryGetLastLoadFailure(string locationId, out string failure)
    {
        // note: Return only the current registered site's bounded failure text; stale site instances cannot explain a newer settlement rejection.
        failure = string.Empty;
        if (!Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) ||
            site == null ||
            string.IsNullOrWhiteSpace(site.loadFailure))
            return false;
        failure = site.loadFailure;
        return true;
    }

    public static bool IsSiteLoaded(string locationId)
    {
        return Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) &&
            // note: Cached completion alone cannot make a disabled, rejected or retiring hierarchy usable.
            site != null && site.isActiveAndEnabled && site.loaded &&
            !site.loading && !site.unloading && !site.loadRejected;
    }

    public static IEnumerator EnsureSiteLoadedRoutine(
        string locationId,
        Action<bool> completed)
    {
        if (!Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) ||
            site == null)
        {
            Debug.LogError(
                "[WORLDGEN ERROR] Required site has no prepared runtime root. " +
                "Location=" + (locationId ?? "<null>") + ", pass=settlement_instantiation.");
            completed?.Invoke(false);
            yield break;
        }

        float waitStartedAt = Time.realtimeSinceStartup;
        bool waitWarningLogged = false;
        while (true)
        {
            // note: Never transfer an old request to a replacement site or keep it queued after its captured owner disappears.
            if (!IsCurrentPreparedSite(locationId, site))
            {
                Debug.LogError(
                    "[WORLDGEN ERROR] Required site became unavailable while awaiting materialization. " +
                    "Location=" + locationId + ", pass=settlement_instantiation.");
                completed?.Invoke(false);
                yield break;
            }
            if (site.loadRejected)
            {
                // note: A cancelled unload or replaced stream can leave a transient rejection without pending content; reopen that recoverable request once instead of making a required site permanently unavailable.
                if (site.pendingSiteContent == null)
                    site.loadRejected = false;
                else
                {
                    completed?.Invoke(false);
                    yield break;
                }
            }
            if (site.loaded && !site.loading && !site.unloading)
            {
                completed?.Invoke(true);
                yield break;
            }
            if (!site.loading && !site.unloading &&
                (ActiveStreamLoader == null || ActiveStreamLoader == site))
                break;

            if (!waitWarningLogged &&
                Time.realtimeSinceStartup - waitStartedAt >
                StreamSlotDiagnosticSeconds)
            {
                waitWarningLogged = true;
                Debug.LogWarning(
                    "[YQCompiledWorldSiteInstance] Required settlement remains in cooperative streaming. " +
                    "Location=" + locationId + ", pass=settlement_instantiation.");
            }
            yield return null;
        }

        // note: Minimum guaranteed settlements bypass the post-unlock distance gate, but still use the same cooperative streaming implementation and frame budget.
        ActiveStreamLoader = site;
        yield return site.LoadPreparedSiteGuardedRoutine();
        // note: Replay the accepted overlay only after the authored site load has published its content, because disabling the owner root during Prepare would cancel its load coroutine.
        YQPlayerFollowingSemanticChunkStreamer overlayStreamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (overlayStreamer != null && site != null && site.loaded && !site.loading && !site.unloading && !site.loadRejected)
        {
            YQStreamedFeatureOverlayTarget overlayTarget = site.GetComponent<YQStreamedFeatureOverlayTarget>();
            if (overlayTarget != null && !string.IsNullOrWhiteSpace(overlayTarget.featureId))
                overlayStreamer.RegisterFeatureOverlayTarget(overlayTarget.featureId, site.gameObject, overlayTarget.objectId);
        }
        // note: A completed child does not authorize publishing geometry from a superseded or disabled prepared root.
        completed?.Invoke(IsCurrentPreparedSite(locationId, site) &&
            site.loaded && !site.loading && !site.unloading && !site.loadRejected);
    }

    private static bool IsCurrentPreparedSite(string locationId, YQCompiledWorldSiteInstance site)
    {
        // note: Compare the exact registered owner, not only its semantic ID; a rebuild may reuse that ID on a new root.
        return site != null && site.isActiveAndEnabled &&
            Instances.TryGetValue(locationId ?? string.Empty, out YQCompiledWorldSiteInstance current) &&
            current == site;
    }

    public static void RemovePreparedSite(string locationId)
    {
        if (!Instances.TryGetValue(
                locationId ?? string.Empty,
                out YQCompiledWorldSiteInstance site) ||
            site == null)
        {
            return;
        }

        Instances.Remove(locationId ?? string.Empty);
        site.gameObject.SetActive(false);
        Destroy(site.gameObject);
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
        CancelOwnedSiteStreaming();
        if (Instances.TryGetValue(settlementId,
                out YQCompiledWorldSiteInstance existing) &&
            existing == this)
        {
            Instances.Remove(settlementId);
        }
    }

    private void OnDisable()
    {
        // note: A disabled or destroyed prepared root must release the global single-loader slot or every remaining settlement will wait forever behind an owner that can no longer advance.
        CancelOwnedSiteStreaming();
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
        out Vector3 position,
        Transform ignoredResident = null)
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
        // note: Resident placement follows the assembled block rather than searching the now-empty donor location.
        var residentLayout = YQProceduralSettlementLayout.Get(semanticSliceSeed);
        if (residentLayout != null)
        {
            if (!YQProceduralSettlementLayout.TryResolveZonePlacement(residentLayout, zone, seed + "|" + index, out var placedZone))
                return false;
            localCenter = placedZone.boundsCenter - authoredOrigin;
            extents = placedZone.boundsSize * .5f;
        }

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

            if (TryProjectToSurface(candidate, out position, ignoredResident))
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

    private bool TryProjectToSurface(Vector3 candidate, out Vector3 projected, Transform ignoredResident = null)
    {
        projected = candidate;
        int hitCount = Physics.RaycastNonAlloc(
            candidate + Vector3.up * 120f,
            Vector3.down,
            ResidentSurfaceHitBuffer,
            260f,
            ~0,
            QueryTriggerInteraction.Ignore);
        // note: Resident sockets stay near the authored floor band; a tight ceiling guard prevents roof shells from becoming NPC standing surfaces.
        float maximumResidentHeight = Mathf.Min(
            transform.position.y + 1.25f,
            candidate.y + 1.25f);
        float bestHeight = float.MinValue;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = ResidentSurfaceHitBuffer[index];

            if (hit.collider == null ||
                hit.normal.y < MinimumResidentSurfaceNormalY ||
                hit.point.y > maximumResidentHeight ||
                !hit.collider.transform.IsChildOf(transform) ||
                IsRoofOrCeilingCollider(hit.collider) ||
                !HasResidentStandingClearance(hit.point, hit.collider, ignoredResident))
            {
                continue;
            }

            if (hit.point.y > bestHeight)
            {
                bestHeight = hit.point.y;
                // note: Return the real support contact; each actor/prop owns its pivot offset rather than inheriting a hidden eight-centimetre hover.
                projected = hit.point;
            }
        }

        return bestHeight > float.MinValue;
    }

    private static bool IsRoofOrCeilingCollider(Collider collider)
    {
        // note: Imported Nordic/Viking cells expose roof names inconsistently; this semantic filter keeps their upper shells out of resident sockets.
        string name = collider != null && collider.transform != null
            ? collider.transform.name.ToLowerInvariant()
            : string.Empty;
        return name.Contains("roof") || name.Contains("ceiling") || name.Contains("gable") || name.Contains("rafter");
    }

    private bool HasResidentStandingClearance(
        Vector3 surfacePoint,
        Collider supportCollider,
        Transform ignoredResident)
    {
        Vector3 lowerCenter = surfacePoint + Vector3.up * 0.38f;
        Vector3 upperCenter = surfacePoint + Vector3.up * 1.55f;
        const float radius = 0.30f;
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            lowerCenter,
            upperCenter,
            radius,
            ResidentClearanceBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int index = 0; index < overlapCount; index++)
        {
            Collider obstacle = ResidentClearanceBuffer[index];
            if (obstacle == null || obstacle == supportCollider ||
                obstacle is TerrainCollider ||
                obstacle.transform == supportCollider.transform ||
                // note: Reloading an existing resident must not reject its own valid floor socket because its current body occupies the clearance capsule.
                (ignoredResident != null && obstacle.transform.IsChildOf(ignoredResident)))
            {
                continue;
            }

            if (obstacle.bounds.max.y <= surfacePoint.y + 0.12f)
                continue;

            // note: A valid resident socket needs an unobstructed standing capsule; roofs, walls, props, and overlapping cell pieces cannot become NPC spawn positions.
            return false;
        }

        return true;
    }

    private static bool TryValidateSelectedSite(
        YQReviewedSemanticSiteManifest selectedManifest,
        HashSet<string> allowedCellIds,
        YQWorldSitePresentationMode selectedPresentationMode,
        Transform siteRoot,
        out Vector3 origin,
        out float radius,
        out string failure,
        string selectionSeed = null,
        bool enforceOriginTerrainBoundary = true)
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

        // note: Resolve before accepting any footprint so roads, terrain reservation and streaming share one versioned layout.
        if (!YQProceduralSettlementLayout.TryResolve(selectedManifest, allowedCellIds, selectionSeed, out var proceduralLayout, out failure))
            return false;
        if (proceduralLayout != null)
        {
            origin = proceduralLayout.origin;
            radius = proceduralLayout.radius;
            bool firstCell = true;
            foreach (var cell in proceduralLayout.cells)
            {
                var cellBounds = new Bounds(cell.boundsCenter, cell.boundsSize);
                if (firstCell) { aggregateBounds = cellBounds; firstCell = false; } else aggregateBounds.Encapsulate(cellBounds);
            }
            foreach (var street in proceduralLayout.streets)
            {
                aggregateBounds.Encapsulate(street.start + Vector3.up * origin.y);
                aggregateBounds.Encapsulate(street.end + Vector3.up * origin.y);
            }
        }

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

            if (siteRoot != null && enforceOriginTerrainBoundary && !FitsGeneratedTerrainAtAnchor(
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

    private static IReadOnlyList<YQAssetFunctionV2> GetReviewedProviderFunctions(
        YQSpatialMaterializationSiteV2 acceptedSite)
    {
        // note: This projection keeps the immutable spatial contract intact while selecting the currently reviewed physical shell for a resource-area POI.
        if (acceptedSite.kind == YQSiteKindV2.PointOfInterest &&
            HasSiteTag(acceptedSite, "resource_area"))
        {
            return new[]
            {
                YQAssetFunctionV2.Habitation,
                YQAssetFunctionV2.Circulation,
                YQAssetFunctionV2.Service
            };
        }

        return acceptedSite.RequiredFunctions;
    }

    private static bool HasSiteTag(
        YQSpatialMaterializationSiteV2 site,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        IReadOnlyList<string> tags = site.Tags;
        for (int index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static bool SelectionCoversTag(
        YQReviewedSemanticSiteManifest manifest,
        HashSet<string> selectedIds,
        string requiredTag)
    {
        if (manifest == null || selectedIds == null ||
            string.IsNullOrWhiteSpace(requiredTag))
        {
            return false;
        }

        for (int zoneIndex = 0; zoneIndex < manifest.Zones.Count; zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone = manifest.Zones[zoneIndex];
            if (zone == null ||
                !HasSemanticTag(zone.semanticTags, requiredTag))
            {
                continue;
            }

            if (manifest.StreamingSite == null)
            {
                if (selectedIds.Contains(zone.stableId))
                    return true;
                continue;
            }

            if (zone.streamingCellIds == null || zone.streamingCellIds.Count == 0)
                continue;

            // note: Selecting a lone tree/road cell must not inherit every function tagged on its entire district; cell-specific functional contracts are checked separately.
            bool completeZone = true;
            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                if (!selectedIds.Contains(zone.streamingCellIds[cellIndex]))
                {
                    completeZone = false;
                    break;
                }
            }
            if (completeZone)
                return true;
        }

        return false;
    }

    private static void AddUniqueSemanticValue(
        List<string> values,
        string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
            return;

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(value);
    }

    private static bool TryMeasureSemanticComposition(
        YQReviewedSemanticSiteManifest manifest,
        HashSet<string> selectedIds,
        out int sourceInstanceCount,
        out List<Bounds> selectedBounds,
        out string failure)
    {
        sourceInstanceCount = 0;
        selectedBounds = new List<Bounds>(selectedIds != null
            ? selectedIds.Count
            : 0);
        failure = string.Empty;

        if (manifest == null || selectedIds == null ||
            selectedIds.Count == 0)
        {
            failure = "semantic composition is empty.";
            return false;
        }

        if (manifest.StreamingSite != null)
        {
            HashSet<string> measured = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<YQAuthoredSiteStreamingCellRecord> cells =
                manifest.StreamingSite.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                YQAuthoredSiteStreamingCellRecord cell = cells[index];
                if (cell == null || !selectedIds.Contains(cell.StableCellId))
                    continue;

                measured.Add(cell.StableCellId);
                sourceInstanceCount += Mathf.Max(1,
                    cell.SourceInstanceCount);
                selectedBounds.Add(new Bounds(
                    cell.AuthoredLocalPosition + cell.LocalBoundsCenter,
                    cell.LocalBoundsSize));
            }

            if (!measured.SetEquals(selectedIds))
            {
                failure = "semantic composition references an unavailable streaming cell.";
                return false;
            }

            return true;
        }

        HashSet<string> measuredZones = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < manifest.Zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = manifest.Zones[index];
            if (zone == null || !selectedIds.Contains(zone.stableId))
                continue;

            measuredZones.Add(zone.stableId);
            sourceInstanceCount += Mathf.Max(1, zone.sourceInstanceCount);
            selectedBounds.Add(new Bounds(
                zone.authoredSourceOrigin + zone.localBoundsCenter,
                zone.localBoundsSize));
        }

        if (!measuredZones.SetEquals(selectedIds))
        {
            failure = "semantic composition references an unavailable legacy zone.";
            return false;
        }

        return true;
    }

    private static bool IsConnectedSemanticComposition(
        IReadOnlyList<Bounds> selectedBounds)
    {
        if (selectedBounds == null || selectedBounds.Count == 0)
            return false;
        if (selectedBounds.Count == 1)
            return true;

        bool[] visited = new bool[selectedBounds.Count];
        Queue<int> pending = new Queue<int>(selectedBounds.Count);
        visited[0] = true;
        pending.Enqueue(0);
        int visitedCount = 1;

        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            for (int candidate = 0;
                 candidate < selectedBounds.Count;
                 candidate++)
            {
                if (visited[candidate] ||
                    !AreSemanticBoundsConnected(
                        selectedBounds[current],
                        selectedBounds[candidate]))
                {
                    continue;
                }

                visited[candidate] = true;
                visitedCount++;
                pending.Enqueue(candidate);
            }
        }

        return visitedCount == selectedBounds.Count;
    }

    private static bool AreSemanticBoundsConnected(
        Bounds left,
        Bounds right)
    {
        float horizontalGapX = Mathf.Max(
            0f,
            Mathf.Max(left.min.x, right.min.x) -
            Mathf.Min(left.max.x, right.max.x));
        float horizontalGapZ = Mathf.Max(
            0f,
            Mathf.Max(left.min.z, right.min.z) -
            Mathf.Min(left.max.z, right.max.z));
        return horizontalGapX * horizontalGapX +
            horizontalGapZ * horizontalGapZ <=
            MaximumSemanticCompositionGap *
            MaximumSemanticCompositionGap;
    }

    private static HashSet<string> BuildActiveCellIds(
        YQReviewedSemanticSiteManifest selectedManifest,
        IReadOnlyList<string> requiredTags,
        string selectionSeed)
    {
        if (selectedManifest == null)
            return null;

        bool filterByTags = requiredTags != null &&
            requiredTags.Count > 0;

        if (selectedManifest.StreamingSite == null)
        {
            return filterByTags
                ? BuildSemanticLegacyZoneIds(
                    selectedManifest,
                    requiredTags,
                    selectionSeed)
                : null;
        }

        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (filterByTags)
        {
            string selectionCacheKey = BuildSemanticSelectionCacheKey(
                selectedManifest,
                requiredTags,
                selectionSeed);
            if (SemanticSelectionCache.TryGetValue(selectionCacheKey, out
                    HashSet<string> cachedSelection))
            {
                // note: Reusing the immutable selection decision keeps distance-streaming reloads from rescanning a large reviewed manifest on the loading frame.
                return new HashSet<string>(cachedSelection,
                    StringComparer.OrdinalIgnoreCase);
            }

            selected = BuildSemanticStreamingCellIds(
                selectedManifest,
                requiredTags,
                selectionSeed);

            if (selected.Count > 0)
            {
                CacheSemanticSelection(selectionCacheKey, selected);
                return selected;
            }

            // note: A semantic mismatch falls back to the smallest reviewed civic slice instead of producing an empty settlement or loading the entire showcase scene.
            return BuildCuratedDefaultCellIds(selectedManifest);
        }

        if (selectedManifest.SourceInstanceCount >
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

    private static string BuildSemanticSelectionCacheKey(
        YQReviewedSemanticSiteManifest manifest,
        IReadOnlyList<string> requiredTags,
        string selectionSeed)
    {
        // note: A replaced manifest or changed engine-owned selection policy must not reuse a different source's cell IDs.
        return BuildSemanticSelectionCacheIdentity(
            manifest != null ? manifest.KitId : string.Empty,
            manifest != null ? manifest.SourceSignature : string.Empty,
            manifest != null ? manifest.GetInstanceID() : 0,
            selectionSeed, requiredTags,
            YQProceduralSettlementLayout.FindOwner(selectionSeed)?.preferMeasuredStructuralCells == true);
    }

    private static string BuildSemanticSelectionCacheIdentity(string kitId, string sourceSignature,
        int manifestInstanceId, string selectionSeed, IReadOnlyList<string> requiredTags, bool preferMeasuredStructures)
    {
        // note: Length-prefixed fields distinguish literal delimiters in IDs and tags; tag order is preserved because it affects semantic priority.
        var key = new System.Text.StringBuilder(160);
        void Append(string value)
        {
            value ??= string.Empty;
            key.Append(value.Length).Append(':').Append(value);
        }
        Append(kitId);
        Append(sourceSignature);
        Append(manifestInstanceId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(selectionSeed);
        Append(preferMeasuredStructures ? "structural" : "legacy");
        if (requiredTags != null)
            foreach (string tag in requiredTags) Append(tag);
        return key.ToString();
    }

    private static void CacheSemanticSelection(
        string key,
        HashSet<string> selected)
    {
        if (string.IsNullOrWhiteSpace(key) || selected == null)
            return;

        // note: The bounded cache covers normal settlement counts without allowing a procedurally expanded world to retain unbounded cell-ID tables.
        if (SemanticSelectionCache.Count >= 64)
            SemanticSelectionCache.Clear();
        SemanticSelectionCache[key] = new HashSet<string>(selected,
            StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SemanticCellCandidate
    {
        public string id = string.Empty;
        public Vector3 center;
        public Vector3 size;
        public int sourceInstanceCount;
        public int semanticScore;
        public bool preferredStructuralAnchor;
        public string[] tags = Array.Empty<string>();
        public YQAssetFunctionV2[] functions = Array.Empty<YQAssetFunctionV2>();
    }

    private static int CompareSemanticCellPriority(SemanticCellCandidate left, SemanticCellCandidate right)
    {
        // note: Proven functions own the skeleton; for opted-in settlements, measured structure outranks descriptive tag order and cheap prop counts.
        int functions = right.functions.Length.CompareTo(left.functions.Length);
        if (functions != 0) return functions;
        int structure = right.preferredStructuralAnchor.CompareTo(left.preferredStructuralAnchor);
        if (structure != 0) return structure;
        return right.semanticScore.CompareTo(left.semanticScore);
    }

    private static HashSet<string> BuildSemanticStreamingCellIds(
        YQReviewedSemanticSiteManifest selectedManifest,
        IReadOnlyList<string> requiredTags,
        string selectionSeed,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions = null,
        YQWorldStructureUsagePolicy structurePolicy = YQWorldStructureUsagePolicy.Unspecified)
    {
        // note: Fresh worlds select complete reviewed assemblies through the existing demand-aware block selector, independently of donor adjacency.
        if (YQProceduralSettlementLayout.UsesIndependentStreaming(selectionSeed) &&
            YQProceduralSettlementLayout.HasIndependentStreamingReview(selectedManifest))
        {
            if (!YQProceduralSettlementLayout.TryGetIndependentStreamingZones(selectedManifest, out var independentZones, out _))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return BuildSemanticLegacyZoneIds(selectedManifest, requiredTags, selectionSeed,
                requiredFunctions, structurePolicy, independentZones);
        }
        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        YQAuthoredSiteStreamingManifest streaming =
            selectedManifest.StreamingSite;
        Dictionary<string, YQAuthoredSiteStreamingCellRecord> cellsById =
            new Dictionary<string, YQAuthoredSiteStreamingCellRecord>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < streaming.Cells.Count; index++)
        {
            YQAuthoredSiteStreamingCellRecord cell = streaming.Cells[index];
            if (cell != null && !string.IsNullOrWhiteSpace(cell.StableCellId))
                cellsById[cell.StableCellId] = cell;
        }

        List<SemanticCellCandidate> candidates =
            new List<SemanticCellCandidate>();
        // note: Old saved selection keeps its original ordering; fresh plans opt into structural evidence once, then persist that choice.
        bool preferMeasuredStructures =
            YQProceduralSettlementLayout.FindOwner(selectionSeed)?.preferMeasuredStructuralCells == true &&
            WantsStructuralSemanticCell(requiredTags);
        HashSet<string> assigned = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int zoneIndex = 0;
             zoneIndex < selectedManifest.Zones.Count;
             zoneIndex++)
        {
            YQReviewedSemanticZoneRecord zone =
                selectedManifest.Zones[zoneIndex];
            if (zone == null ||
                zone.streamingCellIds == null)
            {
                continue;
            }

            int semanticScore = ResolveSemanticTagScore(
                zone.semanticTags,
                requiredTags);
            for (int cellIndex = 0;
                 cellIndex < zone.streamingCellIds.Count;
                 cellIndex++)
            {
                string cellId = zone.streamingCellIds[cellIndex];
                if (string.IsNullOrWhiteSpace(cellId) ||
                    !cellsById.TryGetValue(cellId, out
                        YQAuthoredSiteStreamingCellRecord cell) ||
                    cell.CellPrefab == null ||
                    !IsFiniteVector(cell.AuthoredLocalPosition) ||
                    !IsPlausibleBounds(cell.LocalBoundsCenter,
                        cell.LocalBoundsSize) ||
                    cell.LocalBoundsSize.x > SettlementSemanticRadiusLimit * 2f ||
                    cell.LocalBoundsSize.z > SettlementSemanticRadiusLimit * 2f ||
                    cell.LocalBoundsSize.y > SettlementSemanticVerticalLimit)
                {
                    continue;
                }

                YQAssetFunctionV2[] approvedFunctions =
                    ResolveApprovedCellFunctions(
                        zone,
                        cellId,
                        selectedManifest.SourceSignature,
                        requiredFunctions,
                        structurePolicy);
                bool matchesTag = ContainsRequiredTag(
                    zone.semanticTags,
                    requiredTags);
                bool matchesFunction = approvedFunctions.Length > 0;
                if ((!matchesTag && !matchesFunction) || !assigned.Add(cellId))
                    continue;

                candidates.Add(new SemanticCellCandidate
                {
                    id = cellId,
                    center = cell.AuthoredLocalPosition +
                        cell.LocalBoundsCenter,
                    size = cell.LocalBoundsSize,
                    sourceInstanceCount = Mathf.Max(1,
                        cell.SourceInstanceCount),
                    // note: Typed reviewed functions outweigh descriptive district tags so actual providers become the settlement skeleton rather than an after-the-fact acceptance check.
                    // note: Within a labelled district prefer measured structural cells for a requested built area, not the cheapest isolated prop. This does not certify habitation or any gameplay function.
                    semanticScore = semanticScore + approvedFunctions.Length * 100,
                    preferredStructuralAnchor = cell.HasStructuralFoundation && preferMeasuredStructures,
                    tags = zone.semanticTags != null
                        ? zone.semanticTags.ToArray()
                        : Array.Empty<string>(),
                    functions = approvedFunctions
                });
            }
        }

        if (candidates.Count == 0)
            return selected;

        candidates.Sort((left, right) =>
        {
            // note: Anchor eligibility and sorting share the same priority tuple, so random choice cannot undo structural preference.
            int priority = CompareSemanticCellPriority(left, right);
            if (priority != 0)
                return priority;
            int cost = left.sourceInstanceCount.CompareTo(
                right.sourceInstanceCount);
            if (cost != 0)
                return cost;
            uint leftTie = StableHash((selectionSeed ?? string.Empty) +
                "|cell|" + left.id);
            uint rightTie = StableHash((selectionSeed ?? string.Empty) +
                "|cell|" + right.id);
            return leftTie.CompareTo(rightTie);
        });

        List<SemanticCellCandidate> affordableAnchors =
            new List<SemanticCellCandidate>(8);
        SemanticCellCandidate bestAffordable = null;
        for (int index = 0;
             index < candidates.Count && affordableAnchors.Count < 8;
             index++)
        {
            if (candidates[index].sourceInstanceCount <=
                SettlementSemanticInstanceBudget)
            {
                // note: Skip unaffordable structures before establishing the winning band; every randomized anchor must have that exact priority.
                if (bestAffordable == null)
                    bestAffordable = candidates[index];
                if (CompareSemanticCellPriority(candidates[index], bestAffordable) != 0)
                    break;
                affordableAnchors.Add(candidates[index]);
            }
        }

        if (affordableAnchors.Count == 0)
            return selected;

        // note: The seeded anchor is chosen only from cells that can fit the entire runtime budget; an expensive high-score demo cell can no longer bypass the cap as the first selection.
        SemanticCellCandidate anchor = affordableAnchors[
            (int)(StableHash((selectionSeed ?? string.Empty) +
                "|anchor") % (uint)affordableAnchors.Count)];
        Bounds aggregate = new Bounds(anchor.center, anchor.size);
        List<Bounds> selectedBounds = new List<Bounds>(
            MaximumSettlementSemanticCells)
        {
            aggregate
        };
        int instanceTotal = anchor.sourceInstanceCount;
        selected.Add(anchor.id);

        if (requiredFunctions != null)
        {
            for (int functionIndex = 0;
                 functionIndex < requiredFunctions.Count;
                 functionIndex++)
            {
                YQAssetFunctionV2 requiredFunction =
                    requiredFunctions[functionIndex];
                if (requiredFunction == YQAssetFunctionV2.None ||
                    SelectionCoversFunction(
                        candidates,
                        selected,
                        requiredFunction))
                {
                    continue;
                }

                SemanticCellCandidate best = null;
                float bestDistance = float.MaxValue;
                uint bestTie = uint.MaxValue;
                for (int candidateIndex = 0;
                     candidateIndex < candidates.Count;
                     candidateIndex++)
                {
                    SemanticCellCandidate candidate = candidates[candidateIndex];
                    if (selected.Contains(candidate.id) ||
                        !CandidateHasFunction(candidate, requiredFunction) ||
                        candidate.sourceInstanceCount >
                            SettlementSemanticInstanceBudget - instanceTotal ||
                        !ConnectsToSelectedSemanticBounds(candidate, selectedBounds))
                    {
                        continue;
                    }

                    float distance = (candidate.center - anchor.center).sqrMagnitude;
                    uint tie = StableHash((selectionSeed ?? string.Empty) +
                        "|function|" + requiredFunction + "|" + candidate.id);
                    if (distance < bestDistance ||
                        (Mathf.Approximately(distance, bestDistance) && tie < bestTie))
                    {
                        best = candidate;
                        bestDistance = distance;
                        bestTie = tie;
                    }
                }

                if (best != null)
                {
                    TryAddSemanticCell(
                        best,
                        selected,
                        selectedBounds,
                        ref aggregate,
                        ref instanceTotal);
                }
            }
        }

        // note: First satisfy each semantic role near one seeded anchor so a town slice has a civic core, homes, services, paths, and a POI instead of a random single-tag dump.
        for (int tagIndex = 0; tagIndex < requiredTags.Count; tagIndex++)
        {
            // note: One usable district can fulfill several descriptive roles; repeated tags must not force extra decorative districts into every settlement.
            if (SelectionCoversTag(selectedManifest, selected, requiredTags[tagIndex]))
                continue;
            SemanticCellCandidate best = null;
            float bestDistance = float.MaxValue;
            uint bestTie = uint.MaxValue;
            for (int index = 0; index < candidates.Count; index++)
            {
                SemanticCellCandidate candidate = candidates[index];
                if (selected.Contains(candidate.id) ||
                    !HasSemanticTag(candidate.tags, requiredTags[tagIndex]) ||
                    candidate.sourceInstanceCount >
                        SettlementSemanticInstanceBudget - instanceTotal ||
                    !ConnectsToSelectedSemanticBounds(
                        candidate,
                        selectedBounds))
                {
                    continue;
                }

                float distance = (candidate.center - anchor.center).sqrMagnitude;
                uint tie = StableHash((selectionSeed ?? string.Empty) +
                    "|role|" + tagIndex + "|" + candidate.id);
                if (distance < bestDistance ||
                    (Mathf.Approximately(distance, bestDistance) &&
                     tie < bestTie))
                {
                    best = candidate;
                    bestDistance = distance;
                    bestTie = tie;
                }
            }

            if (best != null &&
                TryAddSemanticCell(best, selected, selectedBounds, ref aggregate,
                    ref instanceTotal))
            {
                continue;
            }
        }

        candidates.Sort((left, right) =>
        {
            float leftDistance = (left.center - anchor.center).sqrMagnitude;
            float rightDistance = (right.center - anchor.center).sqrMagnitude;
            int distance = leftDistance.CompareTo(rightDistance);
            if (distance != 0)
                return distance;
            int score = right.semanticScore.CompareTo(left.semanticScore);
            if (score != 0)
                return score;
            return StableHash((selectionSeed ?? string.Empty) + "|fill|" +
                    left.id).CompareTo(StableHash(
                    (selectionSeed ?? string.Empty) + "|fill|" + right.id));
        });

        int targetCellCount = Mathf.Clamp(
            requiredTags.Count +
                (requiredFunctions != null ? requiredFunctions.Count : 0) + 2,
            3,
            MaximumSettlementSemanticCells);
        for (int index = 0; index < candidates.Count &&
             selected.Count < targetCellCount; index++)
        {
            SemanticCellCandidate candidate = candidates[index];
            if (selected.Contains(candidate.id))
                continue;
            if (!TryAddSemanticCell(candidate, selected, selectedBounds, ref aggregate,
                    ref instanceTotal))
            {
                continue;
            }
        }

        // note: The slice stops after its semantic roles plus two connective cells; it no longer fills toward the source showcase's complete authored layout.

        return selected;
    }

    private static YQAssetFunctionV2[] ResolveApprovedCellFunctions(
        YQReviewedSemanticZoneRecord zone,
        string cellId,
        string sourceSignature,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions,
        YQWorldStructureUsagePolicy structurePolicy)
    {
        if (zone == null || zone.cellContractsV2 == null ||
            requiredFunctions == null || requiredFunctions.Count == 0)
        {
            return Array.Empty<YQAssetFunctionV2>();
        }

        List<YQAssetFunctionV2> resolved = new List<YQAssetFunctionV2>();
        for (int bindingIndex = 0;
             bindingIndex < zone.cellContractsV2.Count;
             bindingIndex++)
        {
            YQReviewedCellFunctionContractV2 binding =
                zone.cellContractsV2[bindingIndex];
            if (binding == null ||
                binding.reviewState != YQSemanticSiteReviewState.Approved ||
                binding.curation == null ||
                binding.curation.contractVersion !=
                    YQAssetCurationContractV2.SupportedContractVersion ||
                !string.Equals(binding.cellId, cellId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(binding.sourceSignature, sourceSignature,
                    StringComparison.Ordinal))
            {
                continue;
            }

            binding.curation.EnsureCollections();
            for (int functionIndex = 0;
                 functionIndex < requiredFunctions.Count;
                 functionIndex++)
            {
                YQAssetFunctionV2 function = requiredFunctions[functionIndex];
                if (function == YQAssetFunctionV2.None ||
                    resolved.Contains(function) ||
                    !YQSiteFunctionContractsV2.HasReviewedFunction(
                        binding, sourceSignature, function, structurePolicy))
                {
                    continue;
                }

                resolved.Add(function);
            }
        }

        return resolved.ToArray();
    }

    private static bool CandidateHasFunction(
        SemanticCellCandidate candidate,
        YQAssetFunctionV2 function)
    {
        if (candidate == null || candidate.functions == null)
            return false;
        for (int index = 0; index < candidate.functions.Length; index++)
        {
            if (candidate.functions[index] == function)
                return true;
        }

        return false;
    }

    private static bool SelectionCoversFunction(
        IReadOnlyList<SemanticCellCandidate> candidates,
        HashSet<string> selected,
        YQAssetFunctionV2 function)
    {
        if (candidates == null || selected == null)
            return false;
        for (int index = 0; index < candidates.Count; index++)
        {
            SemanticCellCandidate candidate = candidates[index];
            if (candidate != null && selected.Contains(candidate.id) &&
                CandidateHasFunction(candidate, function))
            {
                return true;
            }
        }

        // note: Required gameplay functions are tracked against exact selected cell IDs; district-wide labels cannot satisfy the composition implicitly.
        return false;
    }

    internal static bool WantsStructuralSemanticCell(IReadOnlyList<string> requiredTags)
    {
        // note: Exact semantic tags, not substrings or asset names, express the requested physical role.
        if (requiredTags == null) return false;
        foreach (string tag in requiredTags)
            if (string.Equals(tag, "residential", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "habitation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "commerce", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "civic", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool TryAddSemanticCell(
        SemanticCellCandidate candidate,
        HashSet<string> selected,
        List<Bounds> selectedBounds,
        ref Bounds aggregate,
        ref int instanceTotal)
    {
        if (candidate == null || selected.Count >= MaximumSettlementSemanticCells ||
            candidate.sourceInstanceCount >
                SettlementSemanticInstanceBudget - instanceTotal)
        {
            return false;
        }

        Bounds candidateBounds = new Bounds(candidate.center, candidate.size);
        if (!ConnectsToSelectedSemanticBounds(candidate, selectedBounds))
            return false;

        Bounds combined = aggregate;
        combined.Encapsulate(candidateBounds);
        Vector3 horizontalExtents = new Vector3(
            combined.extents.x,
            0f,
            combined.extents.z);
        if (horizontalExtents.magnitude > SettlementSemanticRadiusLimit ||
            combined.size.y > SettlementSemanticVerticalLimit)
        {
            return false;
        }

        selected.Add(candidate.id);
        selectedBounds.Add(candidateBounds);
        aggregate = combined;
        instanceTotal += candidate.sourceInstanceCount;
        return true;
    }

    private static bool ConnectsToSelectedSemanticBounds(
        SemanticCellCandidate candidate,
        IReadOnlyList<Bounds> selectedBounds)
    {
        if (candidate == null || selectedBounds == null ||
            selectedBounds.Count == 0)
        {
            return false;
        }

        Bounds candidateBounds = new Bounds(candidate.center, candidate.size);
        for (int index = 0; index < selectedBounds.Count; index++)
        {
            if (AreSemanticBoundsConnected(
                    candidateBounds,
                    selectedBounds[index]))
            {
                // note: Legacy semantic selection grows from the existing district frontier; disconnected showcase buckets can no longer enter through aggregate bounds alone.
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildSemanticLegacyZoneIds(
        YQReviewedSemanticSiteManifest selectedManifest,
        IReadOnlyList<string> requiredTags,
        string selectionSeed,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions = null,
        YQWorldStructureUsagePolicy structurePolicy = YQWorldStructureUsagePolicy.Unspecified,
        IReadOnlyList<YQReviewedSemanticZoneRecord> independentZones = null)
    {
        // note: New street-composed settlements select functional blocks independently of their donor-scene adjacency.
        bool proceduralBlocks = YQProceduralSettlementLayout.Enabled(selectionSeed);
        var layoutOwner = proceduralBlocks ? YQProceduralSettlementLayout.FindOwner(selectionSeed) : null;
        // note: Continue consumes committed cell IDs verbatim; current demand or selector tuning cannot rewrite an accepted settlement.
        if (layoutOwner?.proceduralLayout != null && layoutOwner.proceduralLayout.seed == selectionSeed)
        {
            var committedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in layoutOwner.proceduralLayout.cells)
                if (cell != null) committedIds.Add(cell.cellId);
            return committedIds;
        }
        int desiredBlocks = layoutOwner != null ? layoutOwner.proceduralBlockTarget : 0;
        int zoneCapacity = desiredBlocks > 0 ? MaximumSettlementSemanticCells : MaximumSettlementSemanticZones;
        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<SemanticCellCandidate> candidates =
            new List<SemanticCellCandidate>();

        // note: Adapted complete streaming assemblies use the same functional selection and hard budgets as authored blocks.
        var candidateZones = independentZones ?? selectedManifest.Zones;
        for (int index = 0; index < candidateZones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = candidateZones[index];
            // note: Fresh layouts cannot select an interior district socket as street frontage; preserve previous seed policies and accepted selections.
            if (YQProceduralSettlementLayout.UsesIndependentStreaming(selectionSeed) &&
                !YQProceduralSettlementLayout.HasUsableExternalConnection(zone))
                continue;
            if (zone == null || string.IsNullOrWhiteSpace(zone.stableId) ||
                zone.prefab == null ||
                !IsFiniteVector(zone.authoredSourceOrigin) ||
                !IsPlausibleBounds(zone.localBoundsCenter,
                    zone.localBoundsSize) ||
                zone.localBoundsSize.x > SettlementSemanticRadiusLimit * 2f ||
                zone.localBoundsSize.z > SettlementSemanticRadiusLimit * 2f ||
                zone.localBoundsSize.y > SettlementSemanticVerticalLimit)
            {
                continue;
            }

            YQAssetFunctionV2[] approvedFunctions = ResolveApprovedCellFunctions(
                zone, zone.stableId, selectedManifest.SourceSignature, requiredFunctions, structurePolicy);
            if (!ContainsRequiredTag(zone.semanticTags, requiredTags) && approvedFunctions.Length == 0)
                continue;

            candidates.Add(new SemanticCellCandidate
            {
                id = zone.stableId,
                center = zone.authoredSourceOrigin + zone.localBoundsCenter,
                size = zone.localBoundsSize,
                sourceInstanceCount = Mathf.Max(1, zone.sourceInstanceCount),
                semanticScore = ResolveSemanticTagScore(
                    zone.semanticTags,
                    requiredTags),
                tags = zone.semanticTags != null
                    ? zone.semanticTags.ToArray()
                    : Array.Empty<string>(),
                functions = approvedFunctions
            });
        }

        if (candidates.Count == 0)
            return selected;

        candidates.Sort((left, right) =>
        {
            // note: Legacy prefab-backed cells obey the same function-first selection contract as streamed cells.
            int functions = right.functions.Length.CompareTo(left.functions.Length);
            if (functions != 0)
                return functions;
            int score = right.semanticScore.CompareTo(left.semanticScore);
            if (score != 0)
                return score;
            return StableHash((selectionSeed ?? string.Empty) + "|zone|" +
                    left.id).CompareTo(StableHash(
                    (selectionSeed ?? string.Empty) + "|zone|" + right.id));
        });

        List<SemanticCellCandidate> affordableAnchors =
            new List<SemanticCellCandidate>(6);
        for (int index = 0;
             index < candidates.Count && affordableAnchors.Count < 6;
             index++)
        {
            if (candidates[index].sourceInstanceCount <=
                SettlementSemanticInstanceBudget)
            {
                // note: Seed variation chooses among equivalent anchors, not among functionally inferior districts.
                if (requiredFunctions != null && requiredFunctions.Count > 0 && affordableAnchors.Count > 0 &&
                    (candidates[index].functions.Length != affordableAnchors[0].functions.Length ||
                     candidates[index].semanticScore != affordableAnchors[0].semanticScore))
                    break;
                affordableAnchors.Add(candidates[index]);
            }
        }

        if (affordableAnchors.Count == 0)
            return selected;

        // note: Legacy semantic zones obey the same hard cost ceiling as streaming cells so a non-streaming pack cannot reintroduce the multi-minute clone path.
        SemanticCellCandidate anchor = affordableAnchors[
            (int)(StableHash((selectionSeed ?? string.Empty) +
                "|zone_anchor") % (uint)affordableAnchors.Count)];
        Bounds aggregate = new Bounds(anchor.center, anchor.size);
        List<Bounds> selectedBounds = new List<Bounds> { aggregate };
        int instanceTotal = anchor.sourceInstanceCount;
        selected.Add(anchor.id);

        // note: Reserve the bounded composition for required providers before adding decorative/tag-only districts.
        if (requiredFunctions != null)
        {
            for (int pass = 0; pass < zoneCapacity; pass++)
            {
                bool added = false;
                foreach (YQAssetFunctionV2 function in requiredFunctions)
                {
                    if (SelectionCoversFunction(candidates, selected, function))
                        continue;
                    for (int index = 0; index < candidates.Count; index++)
                    {
                        SemanticCellCandidate candidate = candidates[index];
                        if (!selected.Contains(candidate.id) && CandidateHasFunction(candidate, function) &&
                            TryAddSemanticZone(candidate, selected, selectedBounds, ref aggregate, ref instanceTotal, proceduralBlocks, zoneCapacity))
                        {
                            added = true;
                            break;
                        }
                    }
                }
                if (!added)
                    break;
            }
        }

        for (int tagIndex = 0; tagIndex < requiredTags.Count; tagIndex++)
        {
            // note: Required roles already covered by selected providers do not require duplicate districts.
            if (SelectionCoversTag(selectedManifest, selected, requiredTags[tagIndex]))
                continue;
            SemanticCellCandidate best = null;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < candidates.Count; index++)
            {
                SemanticCellCandidate candidate = candidates[index];
                if (selected.Contains(candidate.id) ||
                    !HasSemanticTag(candidate.tags, requiredTags[tagIndex]) ||
                    (!proceduralBlocks && !ConnectsToSelectedSemanticBounds(candidate, selectedBounds)) ||
                    candidate.sourceInstanceCount > SettlementSemanticInstanceBudget - instanceTotal)
                {
                    continue;
                }

                float distance = proceduralBlocks ? StableHash(selectionSeed + "|roleblock|" + candidate.id) :
                    (candidate.center - anchor.center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (best != null)
                TryAddSemanticZone(best, selected, selectedBounds, ref aggregate,
                    ref instanceTotal, proceduralBlocks, zoneCapacity);
        }

        candidates.Sort((left, right) =>
        {
            int distance = proceduralBlocks ? 0 : (left.center - anchor.center).sqrMagnitude.CompareTo(
                (right.center - anchor.center).sqrMagnitude);
            if (distance != 0)
                return distance;
            return StableHash((selectionSeed ?? string.Empty) + "|fillzone|" +
                    left.id).CompareTo(StableHash(
                    (selectionSeed ?? string.Empty) + "|fillzone|" + right.id));
        });

        // note: Vary optional district count deterministically after required providers/roles have been reserved, retaining coherent smaller settlements.
        int minimumCount = Mathf.Max(1, selected.Count);
        int targetCount = desiredBlocks > 0
            ? YQProceduralSettlementLayout.ResolveTargetCount(desiredBlocks, minimumCount, zoneCapacity)
            : minimumCount + (int)(StableHash((selectionSeed ?? string.Empty) +
                "|zone_density") % (uint)Mathf.Max(1, MaximumSettlementSemanticZones - minimumCount + 1));
        for (int index = 0; index < candidates.Count &&
             selected.Count < targetCount; index++)
        {
            SemanticCellCandidate candidate = candidates[index];
            if (selected.Contains(candidate.id))
                continue;
            TryAddSemanticZone(candidate, selected, selectedBounds, ref aggregate,
                ref instanceTotal, proceduralBlocks, zoneCapacity);
        }

        return selected;
    }

    private static bool TryAddSemanticZone(
        SemanticCellCandidate candidate,
        HashSet<string> selected,
        List<Bounds> selectedBounds,
        ref Bounds aggregate,
        ref int instanceTotal,
        bool proceduralBlocks = false,
        int zoneCapacity = MaximumSettlementSemanticZones)
    {
        // note: Keep the smaller prefab-zone cap while sharing cell connectivity, bounds and instance-budget enforcement.
        if (selected.Count >= zoneCapacity)
            return false;
        if (proceduralBlocks)
        {
            // note: Enforce cost now; the actual generated bounds, nonoverlap and external connections are validated before terrain mutation.
            if (candidate == null || selected.Contains(candidate.id) ||
                candidate.sourceInstanceCount > SettlementSemanticInstanceBudget - instanceTotal) return false;
            selected.Add(candidate.id);
            instanceTotal += candidate.sourceInstanceCount;
            return true;
        }
        return TryAddSemanticCell(candidate, selected, selectedBounds, ref aggregate, ref instanceTotal);
    }

    private static int ResolveSemanticTagScore(
        IReadOnlyList<string> availableTags,
        IReadOnlyList<string> requiredTags)
    {
        int best = 0;
        for (int index = 0; index < requiredTags.Count; index++)
        {
            if (HasSemanticTag(availableTags, requiredTags[index]))
                best = Mathf.Max(best, 1000 - index * 90);
        }

        return best;
    }

    private static bool HasSemanticTag(
        IReadOnlyList<string> tags,
        string requiredTag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(requiredTag))
            return false;

        for (int index = 0; index < tags.Count; index++)
        {
            if (string.Equals(tags[index], requiredTag,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildCuratedDefaultCellIds(
        YQReviewedSemanticSiteManifest selectedManifest)
    {
        HashSet<string> selected = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        // note: Legacy district totals can describe a label/proxy rather than its referenced streaming content. Measure the actual referenced cells before accepting a fallback.
        var cellsById = new Dictionary<string, YQAuthoredSiteStreamingCellRecord>(StringComparer.OrdinalIgnoreCase);
        if (selectedManifest?.StreamingSite == null)
            return selected;
        foreach (var cell in selectedManifest.StreamingSite.Cells)
            if (cell != null && !string.IsNullOrWhiteSpace(cell.StableCellId))
                cellsById[cell.StableCellId] = cell;
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
        var retainedBounds = new List<Bounds>();
        Bounds combinedBounds = new Bounds();

        for (int index = 0;
             index < candidates.Count &&
             retainedZones < MaximumDefaultSemanticZones;
             index++)
        {
            YQReviewedSemanticZoneRecord zone = candidates[index];
            if (!TryMeasureDefaultStreamingZone(zone, cellsById, selected,
                    out int zoneInstances, out Bounds zoneBounds) || zoneInstances == 0)
                continue;
            if (zoneInstances > CuratedSiteSourceInstanceBudget -
                retainedInstances)
            {
                continue;
            }

            // note: A fallback remains one connected spatial slice, not disconnected showcase groups combined merely because their role names sound useful.
            bool connected = retainedBounds.Count == 0;
            foreach (Bounds bounds in retainedBounds)
                connected |= AreSemanticBoundsConnected(zoneBounds, bounds);
            Bounds proposed = retainedBounds.Count == 0 ? zoneBounds : combinedBounds;
            proposed.Encapsulate(zoneBounds);
            if (!connected || proposed.size.y > SettlementSemanticVerticalLimit ||
                new Vector2(proposed.extents.x, proposed.extents.z).magnitude > SettlementSemanticRadiusLimit)
                continue;

            AddZoneCellIds(zone, selected);
            retainedBounds.Add(zoneBounds);
            combinedBounds = proposed;
            retainedInstances += zoneInstances;
            retainedZones++;
            retainedZoneNames += (retainedZoneNames.Length > 0 ? ", " : "") +
                (!string.IsNullOrWhiteSpace(zone.displayName)
                    ? zone.displayName
                    : zone.stableId);
        }

        // note: Do not override failed cost/geometry checks by loading the smallest oversized district. The caller's existing rejection path must retain authority.

        Debug.Log(
            "[YQCompiledWorldSiteInstance] DEFAULT CURATED SLICE\n" +
            "Reviewed site: " + selectedManifest.KitId + "\n" +
            "Source instances: " + selectedManifest.SourceInstanceCount +
            "\nRetained instances: " + retainedInstances +
            "\nSemantic zones: " + retainedZoneNames);
        return selected;
    }

    internal static bool TryMeasureDefaultStreamingZone(
        YQReviewedSemanticZoneRecord zone,
        IReadOnlyDictionary<string, YQAuthoredSiteStreamingCellRecord> cells,
        ISet<string> alreadySelected,
        out int instanceCount,
        out Bounds bounds)
    {
        // note: Count distinct referenced source instances, not stale zone summary totals; reject missing references instead of publishing a partial district.
        instanceCount = 0;
        bounds = new Bounds();
        if (zone?.streamingCellIds == null || zone.streamingCellIds.Count == 0 || cells == null)
            return false;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool initialized = false;
        foreach (string id in zone.streamingCellIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !cells.TryGetValue(id, out var cell) ||
                cell == null || cell.CellPrefab == null || cell.SourceInstanceCount <= 0 ||
                !IsFiniteVector(cell.AuthoredLocalPosition) || !IsPlausibleBounds(cell.LocalBoundsCenter, cell.LocalBoundsSize))
                return false;
            if (!visited.Add(id))
                continue;
            var cellBounds = new Bounds(cell.AuthoredLocalPosition + cell.LocalBoundsCenter, cell.LocalBoundsSize);
            if (!initialized) { bounds = cellBounds; initialized = true; }
            else bounds.Encapsulate(cellBounds);
            if (alreadySelected == null || !alreadySelected.Contains(id))
            {
                if (cell.SourceInstanceCount > int.MaxValue - instanceCount)
                    return false;
                instanceCount += cell.SourceInstanceCount;
            }
        }
        return initialized;
    }

    private static void AddZoneCellIds(
        YQReviewedSemanticZoneRecord zone,
        HashSet<string> selected)
    {
        if (zone.streamingCellIds == null)
            return;

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

        // note: Legacy reviewed districts have no streaming-cell IDs, so their stable zone ID is the deterministic slice handle.
        if (!string.IsNullOrWhiteSpace(zone.stableId) &&
            allowedCellIds.Contains(zone.stableId))
        {
            return true;
        }

        if (zone.streamingCellIds == null)
            return false;

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

    private Terrain ResolveGeneratedTerrain(Vector3 worldPosition)
    {
        // note: Walk ownership ancestors rather than global active terrain order; imported demo terrains cannot become this site's foundation authority.
        for (Transform owner = transform.parent; owner != null; owner = owner.parent)
        {
            Transform terrainRoot = owner.Find(YQGeneratedWorldTerrain.RuntimeTerrainObjectName);
            if (terrainRoot == null) continue;
            Terrain terrain = terrainRoot.GetComponent<Terrain>();
            // note: Finding an unavailable owning terrain is a failed dependency, not permission to sample another world's terrain.
            if (terrain != null && terrain.isActiveAndEnabled && terrain.terrainData != null &&
                ContainsGeneratedTerrainPoint(terrain.transform.position, terrain.terrainData.size, worldPosition))
            {
                return terrain;
            }
        }

        // note: Distant V2 sites are siblings of the streamer-owned continuation tiles, so resolve the published tile through the authoritative streamer when no ancestor owns the origin terrain.
        YQPlayerFollowingSemanticChunkStreamer streamer =
            FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (streamer != null &&
            streamer.TryGetGeneratedTerrainAt(worldPosition, out Terrain streamedTerrain))
            return streamedTerrain;

        return null;
    }

    private static bool TrySamplePublishedTerrain(
        Terrain preferredTerrain,
        Vector3 worldPosition,
        out float height)
    {
        // note: Door and foundation contacts may cross a continuation-tile edge; sample the collision-ready tile owning the exact contact instead of reusing the site's root tile.
        if (YQTerrainApproachV2.TrySampleTerrain(
                preferredTerrain,
                worldPosition,
                out height))
        {
            return true;
        }

        YQPlayerFollowingSemanticChunkStreamer streamer =
            FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (streamer != null &&
            streamer.TryGetGeneratedTerrainAt(
                worldPosition,
                out Terrain streamedTerrain))
        {
            return YQTerrainApproachV2.TrySampleTerrain(
                streamedTerrain,
                worldPosition,
                out height);
        }

        height = 0f;
        return false;
    }

    private static bool TrySampleGeneratedTerrainAcrossTiles(
        Terrain preferredTerrain,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Vector3 worldPosition,
        out float height)
    {
        // note: Reviewed approaches may cross a chunk seam; resolve the tile owning each sample instead of letting a start-tile query report missing ground.
        if (streamer != null && streamer.TryGetGeneratedTerrainAt(
                worldPosition,
                out Terrain streamedTerrain))
        {
            return YQTerrainApproachV2.TrySampleTerrain(
                streamedTerrain,
                worldPosition,
                out height);
        }

        if (preferredTerrain != null)
            return YQTerrainApproachV2.TrySampleTerrain(
                preferredTerrain,
                worldPosition,
                out height);

        height = 0f;
        return false;
    }

    private static List<Terrain> CollectApproachTerrains(
        Transform cell,
        YQTerrainApproachContractV2 contract,
        Terrain preferredTerrain,
        YQPlayerFollowingSemanticChunkStreamer streamer)
    {
        List<Terrain> results = new List<Terrain>();
        if (preferredTerrain != null)
            results.Add(preferredTerrain);
        if (cell == null || contract == null || streamer == null)
            return results;

        Vector3 start = cell.TransformPoint(contract.localStart);
        Vector3 outward = cell.TransformDirection(contract.localOutward);
        if (outward.sqrMagnitude < 0.01f)
            return results;
        outward.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, outward);
        int sampleCount = Mathf.CeilToInt(
            (contract.maximumRun + 1f) / Mathf.Max(0.5f, contract.sampleSpacing));
        for (int index = 0; index <= sampleCount; index++)
        {
            float distance = Mathf.Min(
                contract.maximumRun + 1f,
                index * Mathf.Max(0.5f, contract.sampleSpacing));
            for (int rail = -1; rail <= 1; rail++)
            {
                Vector3 point = start + outward * distance +
                    lateral * (rail * contract.width * 0.5f);
                if (!streamer.TryGetGeneratedTerrainAt(
                        point,
                        out Terrain terrain) ||
                    terrain == null || results.Contains(terrain))
                {
                    continue;
                }
                results.Add(terrain);
            }
        }
        return results;
    }

    private static IEnumerator WaitForStreamedSiteTerrainRoutine(
        GameObject siteContent,
        Terrain preferredTerrain,
        Action<bool> completed)
    {
        if (siteContent == null || preferredTerrain == null ||
            !YQTerrainSupportComposer.TryGetSolidBoundsAndMaterial(
                siteContent,
                out Bounds bounds,
                out _))
        {
            completed?.Invoke(false);
            yield break;
        }

        YQPlayerFollowingSemanticChunkStreamer streamer =
            YQPlayerFollowingSemanticChunkStreamer.Active ??
            FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (streamer == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        Vector3[] coveragePoints = BuildSiteCoveragePoints(bounds);
        // note: The terrain scheduler owns dependency priority and progress; disposing this nested wait releases its site leases.
        yield return streamer.WaitForSiteTerrainRoutine(siteContent, coveragePoints, completed);
    }

    private static Vector3[] BuildSiteCoveragePoints(Bounds bounds)
    {
        // note: Include edge midpoints as well as corners so a reviewed footprint crossing a tile seam requests every touched collision owner.
        Vector3 center = bounds.center;
        return new[]
        {
            center,
            new Vector3(bounds.min.x, center.y, bounds.min.z),
            new Vector3(bounds.min.x, center.y, bounds.max.z),
            new Vector3(bounds.max.x, center.y, bounds.min.z),
            new Vector3(bounds.max.x, center.y, bounds.max.z),
            new Vector3(center.x, center.y, bounds.min.z),
            new Vector3(center.x, center.y, bounds.max.z),
            new Vector3(bounds.min.x, center.y, center.z),
            new Vector3(bounds.max.x, center.y, center.z)
        };
    }

    private static void CollectStreamedSiteTerrains(
        GameObject siteContent,
        Terrain preferredTerrain,
        List<Terrain> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (preferredTerrain != null)
            results.Add(preferredTerrain);
        if (siteContent == null ||
            !YQTerrainSupportComposer.TryGetSolidBoundsAndMaterial(
                siteContent,
                out Bounds bounds,
                out _))
        {
            return;
        }

        YQPlayerFollowingSemanticChunkStreamer streamer =
            YQPlayerFollowingSemanticChunkStreamer.Active ??
            FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (streamer == null)
            return;

        Vector3[] coveragePoints = BuildSiteCoveragePoints(bounds);
        for (int index = 0; index < coveragePoints.Length; index++)
        {
            if (!streamer.TryGetGeneratedTerrainAt(
                    coveragePoints[index],
                    out Terrain terrain) ||
                terrain == null || results.Contains(terrain))
            {
                continue;
            }

            results.Add(terrain);
        }
    }

    internal static bool ContainsGeneratedTerrainPoint(Vector3 origin, Vector3 size, Vector3 point)
    {
        // note: Sampling outside a heightmap clamps to an edge; that can falsely certify a floating site, so require finite in-bounds coordinates.
        return IsFiniteVector(origin) && IsFiniteVector(size) && IsFiniteVector(point) &&
            size.x > 0f && size.z > 0f &&
            point.x >= origin.x && point.x <= origin.x + size.x &&
            point.z >= origin.z && point.z <= origin.z + size.z;
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
        AsyncInstantiateOperation<GameObject> operation = null;
        try
        {
            // note: Repair malformed vendor collider source data cooperatively before cloning; this keeps dense towns on Unity's asynchronous path instead of forcing one blocking Instantiate frame.
            yield return PreparePrefabCollidersForAsyncCloneRoutine(
                prefab,
                colliderSnapshots);

            operation = UnityEngine.Object.InstantiateAsync(
                prefab,
                parent);

            // note: Async clone work is lower priority than the frame that animates and types the loading presentation.
            operation.priority = -1;
            yield return operation;
        }
        finally
        {
            // note: Coroutine cancellation and watchdog timeouts dispose this iterator, so temporary source-collider edits are restored even when asynchronous cloning never reaches normal completion.
            RestorePrefabColliderSource(colliderSnapshots);
        }

        if (operation != null && operation.Result != null &&
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
                true,
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
                true,
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
        bool allowBudgetedLeafClone,
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

        GameObject instance = null;
        if (allowBudgetedLeafClone && sourceChild.childCount == 0)
        {
            try
            {
                // note: Simple leaf roots clone synchronously inside the caller's strict time slice, avoiding one mandatory frame of asynchronous latency for each of hundreds of tiny hut props.
                instance = UnityEngine.Object.Instantiate(
                    sourceChild.gameObject,
                    parent);
            }
            finally
            {
                RestorePrefabColliderSource(snapshots);
            }
        }
        else
        {
            AsyncInstantiateOperation<GameObject> operation = null;
            try
            {
                operation = UnityEngine.Object.InstantiateAsync(
                    sourceChild.gameObject,
                    parent);
                // note: Complex authored roots remain on Unity's background-capable path so a house or district hierarchy cannot force a synchronous frame peak.
                operation.priority = -1;
                yield return operation;
                instance = operation.Result != null &&
                    operation.Result.Length > 0
                        ? operation.Result[0]
                        : null;
            }
            finally
            {
                RestorePrefabColliderSource(snapshots);
            }
        }
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

    private static bool TryReadStreamedChildCount(
        Transform current,
        out int childCount)
    {
        childCount = 0;
        if (current == null)
            return false;

        try
        {
            childCount = current.childCount;
            return true;
        }
        catch (MissingReferenceException)
        {
            // note: Unity can invalidate an authored transform between coroutine slices; treat that branch as already removed.
            return false;
        }
    }

    private static bool TryReadStreamedChild(
        Transform current,
        int childIndex,
        out Transform child)
    {
        child = null;
        if (current == null)
            return false;

        try
        {
            child = current.GetChild(childIndex);
            return child != null;
        }
        catch (MissingReferenceException)
        {
            // note: A concurrent authored cleanup may remove a child after the count snapshot; skip that missing branch without failing the whole site.
            return false;
        }
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
        bool hasCellVisualBounds = false;
        Bounds cellVisualBounds = default;
        List<Collider> unresolvedColliderCandidates = new List<Collider>();
        Dictionary<Transform, Bounds> branchVisualBounds =
            new Dictionary<Transform, Bounds>();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (!TryReadStreamedChildCount(current, out int childCount))
                continue;

            for (int childIndex = 0; childIndex < childCount; childIndex++)
            {
                if (TryReadStreamedChild(current, childIndex, out Transform child))
                    pending.Push(child);
            }

            Renderer[] localRenderers = current.GetComponents<Renderer>();
            bool hasLocalVisualBounds = TryCombineVisibleRendererBounds(
                localRenderers,
                out Bounds localVisualBounds);
            if (hasLocalVisualBounds)
            {
                AccumulateBranchVisualBounds(
                    branchVisualBounds,
                    current,
                    root.transform,
                    localVisualBounds);
                if (!hasCellVisualBounds)
                {
                    cellVisualBounds = localVisualBounds;
                    hasCellVisualBounds = true;
                }
                else
                {
                    cellVisualBounds.Encapsulate(localVisualBounds);
                }
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
                bool oversizedLocalCollider =
                    !(collider is MeshCollider) && hasLocalVisualBounds &&
                    IsColliderVisuallyOversized(bounds, localVisualBounds);
                if (!explicitBarrier && !smallTraversalClutter &&
                    !lowDecorativeRock && !oversizedLocalCollider)
                {
                    if (!(collider is MeshCollider) && !hasLocalVisualBounds)
                        unresolvedColliderCandidates.Add(collider);
                    continue;
                }

                if (oversizedLocalCollider && collider is BoxCollider box &&
                    TryTightenBoxColliderToVisibleBounds(box, localVisualBounds))
                {
                    // note: A visibly oversized authored box is tightened to its renderer instead of deleting collision from a real wall, prop, or building piece.
                    disabledColliders++;
                    continue;
                }

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

        if (hasCellVisualBounds)
        {
            for (int index = 0; index < unresolvedColliderCandidates.Count; index++)
            {
                Collider collider = unresolvedColliderCandidates[index];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Bounds comparisonBounds = cellVisualBounds;
                if (TryResolveNearestBranchVisualBounds(
                        collider.transform,
                        root.transform,
                        branchVisualBounds,
                        out Bounds nearestBranchBounds))
                {
                    comparisonBounds = nearestBranchBounds;
                }
                if (!IsColliderVisuallyOversized(
                        collider.bounds,
                        comparisonBounds))
                {
                    continue;
                }

                if (collider is BoxCollider box &&
                    TryTightenBoxColliderToVisibleBounds(box, comparisonBounds))
                {
                    // note: Renderer-less structural boxes bind to the nearest visible hierarchy branch instead of inheriting the complete settlement footprint.
                    disabledColliders++;
                    continue;
                }

                // note: Renderer-less primitive collision detached from its nearest visible hierarchy branch is source-scene obstruction, not publishable player collision.
                collider.enabled = false;
                disabledColliders++;
            }
        }

        completed?.Invoke(disabledColliders, disabledProbes);
    }

    internal static bool IsColliderVisuallyOversized(
        Bounds colliderBounds,
        Bounds visibleBounds)
    {
        if (!IsFiniteVector(colliderBounds.center) ||
            !IsFiniteVector(colliderBounds.size) ||
            !IsFiniteVector(visibleBounds.center) ||
            !IsFiniteVector(visibleBounds.size) ||
            visibleBounds.size.x <= 0.01f ||
            visibleBounds.size.y <= 0.01f ||
            visibleBounds.size.z <= 0.01f)
        {
            return false;
        }

        const float allowedOverhang = 0.45f;
        bool excessiveX =
            colliderBounds.size.x > Mathf.Max(
                visibleBounds.size.x * 1.65f,
                visibleBounds.size.x + 0.9f) &&
            (colliderBounds.min.x < visibleBounds.min.x - allowedOverhang ||
             colliderBounds.max.x > visibleBounds.max.x + allowedOverhang);
        bool excessiveY =
            colliderBounds.size.y > Mathf.Max(
                visibleBounds.size.y * 1.85f,
                visibleBounds.size.y + 1.2f) &&
            (colliderBounds.min.y < visibleBounds.min.y - allowedOverhang ||
             colliderBounds.max.y > visibleBounds.max.y + allowedOverhang);
        bool excessiveZ =
            colliderBounds.size.z > Mathf.Max(
                visibleBounds.size.z * 1.65f,
                visibleBounds.size.z + 0.9f) &&
            (colliderBounds.min.z < visibleBounds.min.z - allowedOverhang ||
             colliderBounds.max.z > visibleBounds.max.z + allowedOverhang);
        float separationX = Mathf.Max(
            0f,
            Mathf.Max(
                visibleBounds.min.x - colliderBounds.max.x,
                colliderBounds.min.x - visibleBounds.max.x));
        float separationY = Mathf.Max(
            0f,
            Mathf.Max(
                visibleBounds.min.y - colliderBounds.max.y,
                colliderBounds.min.y - visibleBounds.max.y));
        float separationZ = Mathf.Max(
            0f,
            Mathf.Max(
                visibleBounds.min.z - colliderBounds.max.z,
                colliderBounds.min.z - visibleBounds.max.z));
        bool detached = separationX * separationX +
            separationY * separationY + separationZ * separationZ >
            allowedOverhang * allowedOverhang;

        // note: Intentional simplified collision may modestly exceed art bounds; gross overhang or a fully detached envelope is classified as an invisible-wall defect.
        return excessiveX || excessiveY || excessiveZ || detached;
    }

    private static void AccumulateBranchVisualBounds(
        Dictionary<Transform, Bounds> branchBounds,
        Transform visualOwner,
        Transform root,
        Bounds visualBounds)
    {
        Transform current = visualOwner;
        while (current != null)
        {
            if (branchBounds.TryGetValue(current, out Bounds existing))
            {
                existing.Encapsulate(visualBounds);
                branchBounds[current] = existing;
            }
            else
            {
                branchBounds[current] = visualBounds;
            }

            if (current == root)
                break;
            current = current.parent;
        }
    }

    private static bool TryResolveNearestBranchVisualBounds(
        Transform colliderOwner,
        Transform root,
        IReadOnlyDictionary<Transform, Bounds> branchBounds,
        out Bounds bounds)
    {
        bounds = default;
        if (colliderOwner == null || root == null || branchBounds == null)
            return false;

        Transform current = colliderOwner;
        while (current != null)
        {
            if (branchBounds.TryGetValue(current, out bounds))
                return true;
            if (current == root)
                break;
            current = current.parent;
        }

        return false;
    }

    private static bool TryCombineVisibleRendererBounds(
        Renderer[] renderers,
        out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        if (renderers == null)
            return false;

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
        }

        return initialized;
    }

    private static bool TryTightenBoxColliderToVisibleBounds(
        BoxCollider collider,
        Bounds visibleBounds)
    {
        if (collider == null || collider.transform == null ||
            !IsFiniteVector(visibleBounds.center) ||
            !IsFiniteVector(visibleBounds.size))
        {
            return false;
        }

        Transform owner = collider.transform;
        Vector3 localMin = new Vector3(float.PositiveInfinity,
            float.PositiveInfinity, float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity,
            float.NegativeInfinity, float.NegativeInfinity);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 worldCorner = new Vector3(
                (corner & 1) == 0 ? visibleBounds.min.x : visibleBounds.max.x,
                (corner & 2) == 0 ? visibleBounds.min.y : visibleBounds.max.y,
                (corner & 4) == 0 ? visibleBounds.min.z : visibleBounds.max.z);
            Vector3 localCorner = owner.InverseTransformPoint(worldCorner);
            localMin = Vector3.Min(localMin, localCorner);
            localMax = Vector3.Max(localMax, localCorner);
        }

        Vector3 size = localMax - localMin;
        if (!IsFiniteVector(size) || size.x <= 0.01f ||
            size.y <= 0.01f || size.z <= 0.01f)
        {
            return false;
        }

        collider.center = (localMin + localMax) * 0.5f;
        collider.size = size + Vector3.one * 0.04f;
        return true;
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

        if (contentRoot.transform.childCount > 1)
        {
            // note: The reviewed origin slice is intentionally assembled from several small cells; ground each cell independently instead of applying the legacy monolithic-hut support scan to only the first child.
            int groundedCells = 0;
            yield return AlignCompiledCellsToTerrainRoutine(
                contentRoot,
                terrain,
                true,
                count => groundedCells = count);
            completed?.Invoke(groundedCells, 0);
            yield break;
        }

        Transform cell = contentRoot.transform.GetChild(0);
        Bounds structuralBounds = default;
        bool foundStructure = false;
        float frameStartedAt = Time.realtimeSinceStartup;

        Stack<Transform> structuralPending = new Stack<Transform>();
        Stack<Transform> discoveryPending = new Stack<Transform>();
        discoveryPending.Push(cell);

        while (discoveryPending.Count > 0)
        {
            Transform candidate = discoveryPending.Pop();
            if (candidate == null)
                continue;

            if (candidate != cell &&
                IsWitchHouseStructuralRoot(candidate.name))
            {
                // note: Async prefab cloning can retain an extra wrapper level, so Vey's structural roots are discovered throughout the hidden cell rather than assumed to be direct children.
                structuralPending.Push(candidate);
                continue;
            }

            for (int childIndex = 0;
                 childIndex < candidate.childCount;
                 childIndex++)
            {
                discoveryPending.Push(
                    candidate.GetChild(childIndex));
            }

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
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
            // note: A reviewed hut is never published floating merely because a vendor renamed or rewrapped its pieces; the filtered whole-cell support geometry is the bounded final authority.
            if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                    cell.gameObject,
                    out structuralBounds,
                    out _))
            {
                cell.gameObject.SetActive(false);
                Debug.LogError(
                    "[YQCompiledWorldSiteInstance] WITCH HOUSE REJECTED: " +
                    "no stable structural support geometry was found.");
                completed?.Invoke(0, 0);
                yield break;
            }
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
        Vector3 horizontalCorrection = new Vector3(
            desiredAnchor.x - structuralBounds.center.x,
            0f,
            desiredAnchor.z - structuralBounds.center.z);
        cell.position += horizontalCorrection;
        structuralBounds.center += horizontalCorrection;
        float terrainHeight;
        if (!YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                terrain,
                structuralBounds,
                out terrainHeight,
                out _,
                out _))
        {
            terrainHeight = YQGeneratedWorldTerrain.SampleWorldHeight(
                terrain,
                desiredAnchor);
        }
        // note: Vey's full structural footprint, after horizontal recentering, chooses the support height; the imported source pivot can no longer leave the hut hovering or buried.
        float verticalDelta = terrainHeight - 0.015f - structuralBounds.min.y;
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

    private static string SummarizeRendererNames(GameObject root)
    {
        if (root == null)
            return "<null>";

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return "<none>";

        string summary = string.Empty;
        int limit = Mathf.Min(renderers.Length, 12);
        for (int index = 0; index < limit; index++)
        {
            if (index > 0)
                summary += ", ";
            summary += renderers[index] != null
                ? renderers[index].name
                : "<missing>";
        }
        if (renderers.Length > limit)
            summary += ", ... (" + renderers.Length + " total)";
        return summary;
    }

    private static IEnumerator AlignCompiledCellsToTerrainRoutine(
        GameObject contentRoot,
        Terrain terrain,
        bool independentlyPlacedCells,
        Action<int> completed,
        IReadOnlyDictionary<GameObject, YQReviewedCellFunctionContractV2> groundingContracts = null)
    {
        if (contentRoot == null || terrain == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        if (!independentlyPlacedCells)
        {
            // note: A single complete reviewed cell can use its explicit terrain contact contract without inventing an independent multi-cell layout.
            if (contentRoot.transform.childCount == 1 && groundingContracts != null)
            {
                Transform onlyCell = contentRoot.transform.GetChild(0);
                if (groundingContracts.TryGetValue(onlyCell.gameObject, out var contract))
                {
                    string groundingFailure = string.Empty;
                    bool valid = TryResolveReviewedLandingDelta(onlyCell, terrain, contract, out float delta);
                    if (!valid)
                        groundingFailure = DescribeReviewedLandingFailure(onlyCell, terrain, contract);
                    // note: Keep the diagnostic on the owning site instance so the settlement heartbeat reports the contract boundary that rejected publication.
                    YQCompiledWorldSiteInstance owner = contentRoot.transform.parent != null
                        ? contentRoot.transform.parent.GetComponent<YQCompiledWorldSiteInstance>()
                        : null;
                    if (!valid && owner != null)
                        owner.loadFailure = "reviewed landing grounding failed: " + groundingFailure;
                    if (valid) onlyCell.position += Vector3.up * delta;
                    completed?.Invoke(valid ? 1 : 0);
                    yield break;
                }
            }
            // note: Preserve every authored relative transform; a roof-only chunk is not a foundation and must never be snapped to the terrain on its own.
            bool resolved = false;
            float correction = 0f;
            yield return TryResolveCellGroundingDeltaRoutine(contentRoot, terrain,
                (success, delta) => { resolved = success; correction = delta; });
            if (!resolved)
            {
                // note: Keep generic geometry failures observable when no approved landing contract was mapped to the selected cell.
                YQCompiledWorldSiteInstance owner = contentRoot.transform.parent != null
                    ? contentRoot.transform.parent.GetComponent<YQCompiledWorldSiteInstance>()
                    : null;
                if (owner != null)
                    owner.loadFailure = "generic cell grounding failed: no compatible terrain support envelope";
            }
            if (resolved)
                contentRoot.transform.position += Vector3.up * correction;
            completed?.Invoke(resolved ? contentRoot.transform.childCount : 0);
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
                bool ownsContact = groundingContracts != null && groundingContracts.TryGetValue(cell.gameObject, out var ignored);
                if (ownsContact)
                {
                    // note: An explicit reviewed landing owns this cell's datum; failed contact evidence must not silently fall back to renderer minima.
                    hasGroundingDelta = TryResolveReviewedLandingDelta(cell, terrain, groundingContracts[cell.gameObject], out verticalDelta);
                }
                else
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

    private static Dictionary<GameObject, YQReviewedCellFunctionContractV2> BuildGroundingContracts(
        YQReviewedSemanticSiteManifest source, List<KeyValuePair<string, GameObject>> cells)
    {
        // note: Only source-matched approved door/approach evidence can replace geometric compatibility grounding. Pending authoring candidates never gain runtime authority here.
        var result = new Dictionary<GameObject, YQReviewedCellFunctionContractV2>();
        foreach (var zone in source.Zones)
            foreach (var contract in zone?.cellContractsV2 ?? new List<YQReviewedCellFunctionContractV2>())
            {
                if (contract == null || contract.reviewState != YQSemanticSiteReviewState.Approved ||
                    contract.sourceSignature != source.SourceSignature) continue;
                bool hasLanding = false;
                foreach (var door in contract.doorBindings ?? new List<YQCellDoorBindingV2>())
                    hasLanding |= door != null && door.reviewState == YQSemanticSiteReviewState.Approved &&
                        door.terrainApproach != null && door.terrainApproach.reviewState == YQSemanticSiteReviewState.Approved;
                if (!hasLanding) continue;
                foreach (var cell in cells)
                    if (cell.Value != null && string.Equals(cell.Key, contract.cellId, StringComparison.OrdinalIgnoreCase))
                        result[cell.Value] = contract;
            }
        return result;
    }

    internal static bool TryResolveReviewedLandingDelta(Transform cell, Terrain terrain,
        YQReviewedCellFunctionContractV2 contract, out float delta)
    {
        delta = 0f;
        if (cell == null || terrain == null || terrain.terrainData == null || contract?.doorBindings == null) return false;
        bool found = false;
        foreach (var door in contract.doorBindings)
        {
            var approach = door?.terrainApproach;
            if (door == null || door.reviewState != YQSemanticSiteReviewState.Approved || approach == null ||
                approach.reviewState != YQSemanticSiteReviewState.Approved) continue;
            if (!approach.authoredRouteVerified || !IsFiniteVector(approach.localStart) ||
                !YQCellDoorBindingsV2.TryResolveUniquePath(cell, approach.supportPath, out _)) return false;
            Vector3 contact = cell.TransformPoint(approach.localStart);
            // note: Ground against the reviewed soil datum, preserving the small intentional step onto the visible landing.
            if (!IsFinite(approach.walkingSurfaceAboveTerrain) || approach.walkingSurfaceAboveTerrain < 0f ||
                approach.walkingSurfaceAboveTerrain > .15f) return false;
            contact.y -= approach.walkingSurfaceAboveTerrain;
            // note: Use the same hole-aware, transform-validated terrain query as entrance certification before translating the assembly.
            if (!TrySamplePublishedTerrain(terrain, contact, out float soilHeight)) return false;
            float required = soilHeight - contact.y;
            if (!IsFinite(required) || Mathf.Abs(required) > MaximumExteriorFoundationCorrection ||
                (found && Mathf.Abs(required - delta) > 0.03f)) return false;
            delta = required;
            found = true;
        }
        // note: The existing full-width terrain approach validator still checks support, slope and handoff after this rigid translation.
        // note: Entrance translation must also satisfy reviewed foundation contacts on the final terrain before it can be applied.
        if (found && !YQFoundationTerrainContacts.TryValidate(cell, terrain, contract, delta, out string foundationFailure))
        {
            // note: Reconcile a small final-terrain grading drift by translating the whole reviewed cell within its existing door landing contract.
            // note: Keep the solver's interval asymmetric because the shared validator permits extra burial but only a very small under-embed.
            const float runtimeTerrainOverEmbedTolerance = 0.90f;
            const float runtimeTerrainUnderEmbedTolerance = 0.03f;
            float minimumAdjustedDelta = float.NegativeInfinity;
            float maximumAdjustedDelta = float.PositiveInfinity;
            bool sampledContact = false;
            foreach (var contact in contract.independentAssembly?.terrainContacts ?? new List<YQFoundationTerrainContact>())
            {
                if (contact == null || !YQCellDoorBindingsV2.TryResolveUniquePath(cell, contact.supportPath, out _)) continue;
                Vector3 bottom = cell.TransformPoint(contact.localBottom) + UnityEngine.Vector3.up * delta;
                if (!TrySamplePublishedTerrain(terrain, bottom, out float soil)) continue;
                sampledContact = true;
                float embed = soil - bottom.y;
                minimumAdjustedDelta = Mathf.Max(minimumAdjustedDelta,
                    delta + embed - (contact.maximumEmbedDepth + runtimeTerrainOverEmbedTolerance));
                maximumAdjustedDelta = Mathf.Min(maximumAdjustedDelta,
                    delta + embed - (contact.minimumEmbedDepth - runtimeTerrainUnderEmbedTolerance));
            }
            // note: Every post constrains one shared rigid translation; intersect their valid ranges instead of cumulatively applying the same terrain drift once per post.
            // note: Stay inside the interval so adding the correction to world-space post heights cannot round an exact boundary into a rejection.
            float intervalMargin = Mathf.Min(0.001f, Mathf.Max(0f, maximumAdjustedDelta - minimumAdjustedDelta) * 0.5f);
            float adjustedDelta = minimumAdjustedDelta <= maximumAdjustedDelta
                ? Mathf.Clamp(delta, minimumAdjustedDelta + intervalMargin, maximumAdjustedDelta - intervalMargin)
                : delta;
            // note: Bound the actual assembly movement, not its distance from the rejected door-only snap; the reviewed approach is constructed and revalidated after grounding.
            if (sampledContact && IsFinite(adjustedDelta) && Mathf.Abs(adjustedDelta) <= MaximumExteriorFoundationCorrection &&
                YQFoundationTerrainContacts.TryValidate(cell, terrain, contract, adjustedDelta, out _))
            {
                // note: Keep the correction bounded so reviewed assemblies cannot be teleported to unrelated terrain.
                delta = adjustedDelta;
                Debug.LogWarning("[YQCompiledWorldSiteInstance] Foundation contact reconciled to final terrain: delta=" + delta.ToString("F3"));
            }
            else
            {
                // note: Report the shared correction interval when reviewed posts cannot agree, distinguishing terrain-range incompatibility from a missing sample or path.
                Debug.LogError("[YQCompiledWorldSiteInstance] Foundation terrain contact rejected: " + foundationFailure +
                    " sharedDeltaRange=" + minimumAdjustedDelta.ToString("F3") + ".." + maximumAdjustedDelta.ToString("F3") +
                    " sampled=" + sampledContact + " requestedDelta=" + delta.ToString("F3"));
                return false;
            }
        }
        return found;
    }

    private static string DescribeReviewedLandingFailure(
        Transform cell,
        Terrain terrain,
        YQReviewedCellFunctionContractV2 contract)
    {
        // note: Mirror only the reviewed landing preconditions to identify the first failed evidence boundary; this helper never changes acceptance behavior.
        if (cell == null || terrain == null || terrain.terrainData == null || contract?.doorBindings == null)
            return "missing cell, terrain, terrain data, or door bindings";
        bool found = false;
        float requestedDelta = 0f;
        foreach (var door in contract.doorBindings)
        {
            var approach = door?.terrainApproach;
            if (door == null || door.reviewState != YQSemanticSiteReviewState.Approved || approach == null ||
                approach.reviewState != YQSemanticSiteReviewState.Approved)
                continue;
            if (!approach.authoredRouteVerified || !IsFiniteVector(approach.localStart))
                return "approved landing lacks finite authored route evidence";
            if (!YQCellDoorBindingsV2.TryResolveUniquePath(cell, approach.supportPath, out _))
                return "landing support path missing: " + approach.supportPath;
            if (!IsFinite(approach.walkingSurfaceAboveTerrain) || approach.walkingSurfaceAboveTerrain < 0f ||
                approach.walkingSurfaceAboveTerrain > .15f)
                return "walking surface offset outside reviewed range";
            Vector3 contact = cell.TransformPoint(approach.localStart);
            contact.y -= approach.walkingSurfaceAboveTerrain;
            if (!TrySamplePublishedTerrain(terrain, contact, out float soilHeight))
                return "terrain sample unavailable at reviewed landing";
            float required = soilHeight - contact.y;
            if (!IsFinite(required) || Mathf.Abs(required) > MaximumExteriorFoundationCorrection)
                return "landing correction outside bounded range: " + required.ToString("F3");
            if (found && Mathf.Abs(required - requestedDelta) > .03f)
                return "approved landings disagree on rigid correction";
            requestedDelta = required;
            found = true;
        }
        if (!found)
            return "no approved terrain approach binding";
        if (!YQFoundationTerrainContacts.TryValidate(cell, terrain, contract, requestedDelta, out string foundationFailure))
            return "foundation contact rejected: " + foundationFailure;
        return "landing solver rejected without a matching precondition failure";
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
        List<float> terrainHeightSamples = new List<float>(9);
        // note: Retain source identities only during grounding so rejected contacts can be diagnosed from the actual run.
        var contactSources = new List<KeyValuePair<Renderer, float>>();

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
            if (!TrySampleCompiledFoundationTerrainContact(
                    terrain,
                    bounds,
                    terrainHeightSamples,
                    out float terrainContact))
            {
                continue;
            }

            float requiredDelta =
                terrainContact - 0.015f - bounds.min.y;
            if (!IsFinite(requiredDelta) || !IsFinite(weight) || weight <= 0f)
                continue;

            // note: Each substantial low renderer contributes the correction required at its own footprint, so one aggregate terrain median cannot hide a floating secondary building.
            samples.Add(new Vector2(requiredDelta, weight));
            contactSources.Add(new KeyValuePair<Renderer, float>(renderer, requiredDelta));

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StreamingFrameBudgetSeconds)
            {
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        if (!TryResolveCompiledFoundationCorrection(
                samples,
                out float candidate,
                out float supportRatio))
        {
            var rejectedContacts = new List<string>();
            foreach (var contact in contactSources)
            {
                if (candidate - contact.Value <= MaximumCompiledFoundationAirGap &&
                    contact.Value - candidate <= MaximumCompiledFoundationPenetration) continue;
                if (rejectedContacts.Count >= 12) break;
                rejectedContacts.Add(contact.Key.name + " requiredDelta=" + contact.Value.ToString("F3"));
            }
            Debug.LogError(
                "[WORLDGEN ERROR] Compiled cell has irreconcilable foundation contacts. " +
                "Cell=" + cell.name + ", samples=" + samples.Count +
                ", supportedWeight=" + supportRatio.ToString("P0") +
                ", bestCorrection=" + candidate.ToString("F3") +
                ". Unsupported sources: " + string.Join("; ", rejectedContacts));
            completed?.Invoke(false, 0f);
            yield break;
        }

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

    internal static bool TryResolveCompiledFoundationCorrection(
        List<Vector2> correctionSamples,
        out float correction,
        out float supportRatio)
    {
        correction = 0f;
        supportRatio = 0f;
        if (correctionSamples == null || correctionSamples.Count == 0)
            return false;

        float totalWeight = 0f;
        for (int index = 0; index < correctionSamples.Count; index++)
        {
            Vector2 sample = correctionSamples[index];
            if (!IsFinite(sample.x) || !IsFinite(sample.y) || sample.y <= 0f)
                return false;
            totalWeight += sample.y;
        }
        if (!IsFinite(totalWeight) || totalWeight <= 0f)
            return false;

        correctionSamples.Sort((left, right) => left.x.CompareTo(right.x));
        float targetWeight = totalWeight * 0.5f;
        float accumulatedWeight = 0f;
        correction = correctionSamples[0].x;
        for (int index = 0; index < correctionSamples.Count; index++)
        {
            accumulatedWeight += correctionSamples[index].y;
            if (accumulatedWeight < targetWeight)
                continue;
            correction = correctionSamples[index].x;
            break;
        }

        // note: The contact limits are asymmetric. A weighted median minimizes distance,
        // but can reject a feasible rigid placement; find the heaviest compatible interval.
        float medianCorrection = correction;
        float windowWeight = 0f;
        float bestWeight = -1f;
        float bestCorrection = medianCorrection;
        int leftIndex = 0;
        for (int rightIndex = 0; rightIndex < correctionSamples.Count; rightIndex++)
        {
            windowWeight += correctionSamples[rightIndex].y;
            while (leftIndex < rightIndex &&
                   correctionSamples[rightIndex].x - correctionSamples[leftIndex].x >
                   MaximumCompiledFoundationAirGap + MaximumCompiledFoundationPenetration)
            {
                windowWeight -= correctionSamples[leftIndex++].y;
            }

            float minimum = correctionSamples[rightIndex].x - MaximumCompiledFoundationPenetration;
            float maximum = correctionSamples[leftIndex].x + MaximumCompiledFoundationAirGap;
            // note: Stay just inside the feasible interval to avoid boundary rounding changing support.
            float margin = Mathf.Min(0.0001f, Mathf.Max(0f, maximum - minimum) * 0.5f);
            float candidate = Mathf.Clamp(medianCorrection, minimum + margin, maximum - margin);
            if (windowWeight > bestWeight || (windowWeight == bestWeight &&
                Mathf.Abs(candidate - medianCorrection) < Mathf.Abs(bestCorrection - medianCorrection)))
            {
                bestWeight = windowWeight;
                bestCorrection = candidate;
            }
        }
        correction = bestCorrection;

        float supportedWeight = 0f;
        for (int index = 0; index < correctionSamples.Count; index++)
        {
            Vector2 sample = correctionSamples[index];
            // note: A sample is terrain height minus the original foundation bottom; raising beyond that correction creates air, while raising less leaves embedment.
            float airGap = correction - sample.x;
            float penetration = sample.x - correction;
            if (airGap <= MaximumCompiledFoundationAirGap &&
                penetration <= MaximumCompiledFoundationPenetration)
            {
                supportedWeight += sample.y;
            }
        }

        supportRatio = supportedWeight / totalWeight;
        // note: Keep the existing support threshold and contact limits; incompatible secondary structures still invalidate the cell.
        return IsFinite(correction) && IsFinite(supportRatio) &&
               supportRatio >= MinimumCompiledFoundationSupportRatio;
    }

    private static bool TrySampleCompiledFoundationTerrainContact(
        Terrain terrain,
        Bounds bounds,
        List<float> heights,
        out float contactHeight)
    {
        contactHeight = 0f;
        if (terrain == null || terrain.terrainData == null || heights == null ||
            !IsFiniteVector(bounds.center) || !IsFiniteVector(bounds.size))
        {
            return false;
        }

        heights.Clear();
        float sampleX = Mathf.Clamp(bounds.extents.x * 0.72f, 0f, 24f);
        float sampleZ = Mathf.Clamp(bounds.extents.z * 0.72f, 0f, 24f);
        Vector3 center = bounds.center;
        AddCompiledFoundationTerrainHeight(terrain, center, heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x + sampleX, center.y, center.z), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x - sampleX, center.y, center.z), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x, center.y, center.z + sampleZ), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x, center.y, center.z - sampleZ), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x + sampleX, center.y, center.z + sampleZ), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x + sampleX, center.y, center.z - sampleZ), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x - sampleX, center.y, center.z + sampleZ), heights);
        AddCompiledFoundationTerrainHeight(terrain,
            new Vector3(center.x - sampleX, center.y, center.z - sampleZ), heights);
        if (heights.Count == 0)
            return false;

        heights.Sort();
        int contactIndex = Mathf.Clamp(
            Mathf.CeilToInt((heights.Count - 1) * 0.625f),
            0,
            heights.Count - 1);
        contactHeight = heights[contactIndex];
        return IsFinite(contactHeight);
    }

    private static void AddCompiledFoundationTerrainHeight(
        Terrain terrain,
        Vector3 point,
        List<float> heights)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (point.x < origin.x || point.x > origin.x + size.x ||
            point.z < origin.z || point.z > origin.z + size.z)
        {
            return;
        }

        // note: The caller reuses one nine-value buffer across the cell, avoiding per-renderer arrays while retaining complete footprint contact sampling.
        heights.Add(YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point));
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
            "vfx", "decal", "cart");
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

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null || terrainData.size.x <= 0f || terrainData.size.z <= 0f)
            return false;

        // note: Clip the support stamp to this tile before sampling; a footprint crossing a seam must receive an independent support solution on each collision owner.
        Vector3 terrainOrigin = terrain.transform.position;
        float clippedMinimumX = Mathf.Max(bounds.min.x, terrainOrigin.x);
        float clippedMaximumX = Mathf.Min(bounds.max.x, terrainOrigin.x + terrainData.size.x);
        float clippedMinimumZ = Mathf.Max(bounds.min.z, terrainOrigin.z);
        float clippedMaximumZ = Mathf.Min(bounds.max.z, terrainOrigin.z + terrainData.size.z);
        if (clippedMaximumX <= clippedMinimumX || clippedMaximumZ <= clippedMinimumZ)
            return false;
        Bounds clippedBounds = new Bounds(
            new Vector3(
                (clippedMinimumX + clippedMaximumX) * 0.5f,
                bounds.center.y,
                (clippedMinimumZ + clippedMaximumZ) * 0.5f),
            new Vector3(
                clippedMaximumX - clippedMinimumX,
                bounds.size.y,
                clippedMaximumZ - clippedMinimumZ));

        float targetHeight = clippedBounds.min.y + SupportSurfaceOffset;

        // note: Raise only to the highest measured footprint point, making the certified support plane continuous without cutting into the canonical terrain outside the site footprint.
        if (YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                terrain,
                clippedBounds,
                out _,
                out _,
                out float maximumFootprintHeight))
        {
            targetHeight = Mathf.Max(
                targetHeight,
                maximumFootprintHeight + SupportSurfaceOffset);
        }

        float currentTerrainHeight = terrain.SampleHeight(clippedBounds.center) +
            terrain.transform.position.y;

        if (float.IsNaN(targetHeight) || float.IsInfinity(targetHeight) ||
            targetHeight <= currentTerrainHeight + 0.06f)
        {
            return false;
        }

        // note: A reviewed assembly keeps its authored transform; this compact stamp describes only the terrain support that must rise beneath its footprint.
        stamp = new YQTerrainSupportStamp
        {
            center = new Vector2(clippedBounds.center.x, clippedBounds.center.z),
            flatHalfExtents = new Vector2(
                Mathf.Clamp(
                    clippedBounds.extents.x,
                    MinimumFlatHalfExtent,
                    MaximumAssemblyHalfExtent),
                Mathf.Clamp(
                    clippedBounds.extents.z,
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

    internal static bool TryGetSolidBoundsAndMaterial(
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
