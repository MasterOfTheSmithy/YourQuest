using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class YQGeneratedWorldPopulation
{
    private const string PopulationRootName =
        "Generated_Population";

    private const string EncampmentRootName =
        "Generated_Encampments";

    private const string ContinuationPopulationRootPrefix = "Generated_ContinuationPopulation__";

    // ============================================================
    // PUBLIC BUILD
    // ============================================================

    public static bool Build(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            registry == null)
        {
            return false;
        }

        plan.EnsureCollections();

        /*
         * Canonical identity belongs to the population planner.
         *
         * This class owns only physical materialization.
         */
        if (plan.generatedNpcs == null ||
            plan.generatedNpcs.Count == 0)
        {
            Debug.Log(
                "[YQGeneratedWorldPopulation] WAITING\n" +
                "World plan has no canonical generated NPC records yet.");

            return false;
        }

        DestroyExistingPopulationRoot(
            parent,
            PopulationRootName);

        DestroyExistingPopulationRoot(
            parent,
            EncampmentRootName);

        GameObject populationRoot =
            new GameObject(
                PopulationRootName);

        populationRoot.transform.SetParent(
            parent,
            false);

        GameObject encampmentRoot =
            new GameObject(
                EncampmentRootName);

        encampmentRoot.transform.SetParent(
            parent,
            false);

        WorldState world =
            WorldStateManager.Instance != null
                ? WorldStateManager.Instance.State
                : null;

        int residents =
            BuildSettlementResidents(
                populationRoot.transform,
                terrain,
                plan,
                world,
                registry);

        int camps =
            0;

        int namedHostiles =
            0;

        int rankAndFileHostiles =
            0;

        int rewardContainers =
            0;

        if (plan.encampments != null)
        {
            for (int i = 0;
                 i < plan.encampments.Count;
                 i++)
            {
                GeneratedEncampmentRecord encampment =
                    plan.encampments[i];

                if (encampment == null)
                    continue;

                encampment.EnsureCollections();

                GeneratedRegionRecord region =
                    FindRegion(
                        plan,
                        encampment.regionId);

                if (region == null)
                    continue;

                GeneratedRegionAssetPaletteRecord palette =
                    FindPalette(
                        plan,
                        region);

                if (palette == null)
                    continue;

                palette.EnsureCollections();

                BuildEncampmentSynchronously(
                    encampmentRoot.transform,
                    terrain,
                    plan,
                    world,
                    encampment,
                    region,
                    palette,
                    registry,
                    out int named,
                    out int generic,
                    out int rewards);

                namedHostiles +=
                    named;

                rankAndFileHostiles +=
                    generic;

                rewardContainers +=
                    rewards;

                camps++;
            }
        }

        // note: Population placement is renderer/terrain driven; defer collider publication to the normal physics step rather than freezing the loading presentation here.

        Debug.Log(
            "[YQGeneratedWorldPopulation] BUILT\n" +
            "Canonical records in plan: " +
            plan.generatedNpcs.Count +
            "\nSettlement residents materialized: " +
            residents +
            "\nEncampments materialized: " +
            camps +
            "\nNamed hostile leaders: " +
            namedHostiles +
            "\nAnonymous rank-and-file hostiles: " +
            rankAndFileHostiles +
            "\nEncampment reward containers: " +
            rewardContainers);

        return true;
    }

    public static IEnumerator BuildRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry,
        Action<bool> completed)
    {
        if (parent == null || terrain == null || plan == null || registry == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        plan.EnsureCollections();
        if (plan.generatedNpcs == null || plan.generatedNpcs.Count == 0)
        {
            completed?.Invoke(false);
            yield break;
        }

        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        // note: Validate canonical placement coverage before deleting any previously materialized inhabitants.
        if (!TryValidatePopulationBindings(plan, world, out int expectedLivingNpcs, out string bindingFailure))
            throw new InvalidOperationException("Canonical population bindings rejected: " + bindingFailure);

        DestroyExistingPopulationRoot(parent, PopulationRootName);
        DestroyExistingPopulationRoot(parent, EncampmentRootName);
        yield return null;

        GameObject populationRoot = new GameObject(PopulationRootName);
        populationRoot.transform.SetParent(parent, false);
        GameObject encampmentRoot = new GameObject(EncampmentRootName);
        encampmentRoot.transform.SetParent(parent, false);

        int residents = 0;
        if (plan.settlements != null)
        {
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Placing inhabitants and finalizing",
                    9,
                    9,
                    "Placing settlement population " + (index + 1) + " of " +
                    plan.settlements.Count,
                    Mathf.Lerp(
                        0.94f,
                        0.965f,
                        plan.settlements.Count > 0
                            ? index / (float)plan.settlements.Count
                            : 1f));
                int settlementResidents = 0;
                yield return BuildSettlementResidentsForSettlementRoutine(
                    populationRoot.transform,
                    terrain,
                    plan,
                    world,
                    registry,
                    plan.settlements[index],
                    count => settlementResidents = count);
                residents += settlementResidents;
                // note: Completed population batches are progress, unlike repeatedly polling an unfinished child operation.
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
            }
        }

        int camps = 0;
        int namedHostiles = 0;
        int rankAndFileHostiles = 0;
        int rewardContainers = 0;
        if (plan.encampments != null)
        {
            for (int index = 0; index < plan.encampments.Count; index++)
            {
                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Placing inhabitants and finalizing",
                    9,
                    9,
                    "Placing hostile population " + (index + 1) + " of " +
                    plan.encampments.Count,
                    Mathf.Lerp(
                        0.965f,
                        0.98f,
                        plan.encampments.Count > 0
                            ? index / (float)plan.encampments.Count
                            : 1f));
                GeneratedEncampmentRecord encampment = plan.encampments[index];
                if (encampment == null)
                    continue;

                encampment.EnsureCollections();
                GeneratedRegionRecord region = FindRegion(plan, encampment.regionId);
                GeneratedRegionAssetPaletteRecord palette = region != null
                    ? FindPalette(plan, region)
                    : null;
                if (region == null || palette == null)
                    continue;

                palette.EnsureCollections();
                int named = 0;
                int generic = 0;
                int rewards = 0;
                yield return BuildEncampmentRoutine(
                    encampmentRoot.transform,
                    terrain,
                    plan,
                    world,
                    encampment,
                    region,
                    palette,
                    registry,
                    (namedCount, genericCount, rewardCount) =>
                    {
                        named = namedCount;
                        generic = genericCount;
                        rewards = rewardCount;
                    });
                namedHostiles += named;
                rankAndFileHostiles += generic;
                rewardContainers += rewards;
                camps++;
                YQGeneratedWorldRuntimeBuilder.ReportInitialGenerationProgress();
                // note: Hostile sites are likewise published one deterministic encounter at a time.
                yield return null;
            }
        }

        // note: Anonymous encounter extras do not satisfy missing saved identities; every living canonical record needs its actual actor.
        if (residents + namedHostiles != expectedLivingNpcs)
            throw new InvalidOperationException("Canonical population coverage incomplete: expected " + expectedLivingNpcs +
                " living identities, created " + (residents + namedHostiles) + ". Check skipped site region/palette bindings.");

        // note: Cooperative population construction does not require an immediate global physics rebuild; the final player handoff owns the single authoritative sync.
        Debug.Log(
            "[YQGeneratedWorldPopulation] BUILT COOPERATIVELY\n" +
            "Canonical records in plan: " + plan.generatedNpcs.Count +
            "\nSettlement residents materialized: " + residents +
            "\nEncampments materialized: " + camps +
            "\nNamed hostile leaders: " + namedHostiles +
            "\nAnonymous rank-and-file hostiles: " + rankAndFileHostiles +
            "\nEncampment reward containers: " + rewardContainers);
        completed?.Invoke(true);
    }

    // note: This adapter consumes an already published location and loaded provider; it never accepts, saves, or prepares frontier content.
    public static IEnumerator BuildContinuationLocationRoutine(
        Transform locationRoot,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        GeneratedSpatialContinuationLocationV2Record location,
        YQPreparedSpatialMaterializationV2 prepared,
        YQRuntimeWorldAssetRegistry registry,
        Action<bool, string> completed,
        YQCompiledWorldSiteInstance.ContinuationPopulationContext stagingContext = null)
    {
        WorldState world = WorldStateManager.Instance?.State;
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        GeneratedSpatialContinuationV2Record continuation = artifact?.acceptedContinuation;
        if (locationRoot == null || terrain == null || registry == null || world == null ||
            !ReferenceEquals(world.generatedWorldPlan, plan) || continuation == null ||
            continuation.state != YQSpatialContinuationStateV2.Accepted || location == null ||
            location.state != YQSpatialContinuationStateV2.Accepted || continuation.locations == null ||
            !continuation.locations.Contains(location))
        {
            completed?.Invoke(false, "Continuation population requires the exact active persisted accepted location.");
            yield break;
        }

        YQSpatialContinuationBasicValidationResultV2 validation = YQSpatialContinuationValidatorV2.ValidateBasic(artifact);
        if (!validation.IsStructurallyValid)
        {
            completed?.Invoke(false, "Continuation population rejected its saved contract: " + validation.errors[0]);
            yield break;
        }
        YQIdentityValidationResult references = YQStateReferenceValidator.Validate(null, world);
        if (!references.IsValid)
        {
            completed?.Invoke(false, "Continuation population rejected its canonical state references: " + references.failures[0]);
            yield break;
        }
        string envelopeHash = continuation.contentHash;
        long envelopeRevision = continuation.revision;
        string locationHash = location.contentHash;
        long locationRevision = location.revision;
        if (!TryCheckContinuationPopulationContext(locationRoot, plan, world, artifact, continuation, location,
                prepared, envelopeHash, envelopeRevision, locationHash, locationRevision, null, stagingContext, out var provider, out string failure))
        {
            completed?.Invoke(false, failure);
            yield break;
        }
        if (provider.transform.Find("CompiledSiteContent") != locationRoot)
        {
            completed?.Invoke(false, "Continuation population requires the provider's exact published content root.");
            yield break;
        }
        if (!TryValidateContinuationPopulationInputs(plan, world, location, registry,
                out int expectedLivingNpcs, out GeneratedRegionRecord region, out GeneratedRegionAssetPaletteRecord palette, out failure))
        {
            completed?.Invoke(false, failure);
            yield break;
        }

        string marker = ContinuationPopulationRootPrefix + locationHash;
        for (int index = 0; index < locationRoot.childCount; index++)
        {
            Transform child = locationRoot.GetChild(index);
            if (!child.name.StartsWith(ContinuationPopulationRootPrefix, StringComparison.Ordinal))
                continue;
            // note: The marker belongs to this provider content lifetime; a concurrent or superseded run cannot add a second cast.
            bool sameCompletedPopulation = child.name == marker && child.gameObject.activeSelf;
            completed?.Invoke(sameCompletedPopulation, sameCompletedPopulation ? string.Empty :
                "Continuation population is already staging or belongs to a different accepted revision.");
            yield break;
        }

        GameObject staging = new GameObject(marker);
        staging.SetActive(false);
        staging.transform.SetParent(locationRoot, false);
        bool published = false;
        bool reported = false;
        var work = new Stack<IEnumerator>();
        int createdLivingNpcs = 0;
        work.Push(location.settlement != null
            ? BuildSettlementResidentsForSettlementRoutine(staging.transform, terrain, plan, world, registry,
                location.settlement, count => createdLivingNpcs = count, location.population, true)
            : BuildEncampmentRoutine(staging.transform, terrain, plan, world, location.encampment, region, palette,
                registry, (named, generic, rewards) => createdLivingNpcs = named, location.population, true, stagingContext));
        try
        {
            // note: Flatten nested work so every cooperative boundary rechecks the exact saved payload, projection and provider owner.
            while (work.Count > 0)
            {
                if (!TryCheckContinuationPopulationContext(locationRoot, plan, world, artifact, continuation, location,
                        prepared, envelopeHash, envelopeRevision, locationHash, locationRevision, provider, stagingContext, out _, out failure))
                    break;
                IEnumerator step = work.Peek();
                bool moved = false;
                object yielded = null;
                try
                {
                    moved = step.MoveNext();
                    if (moved)
                        yielded = step.Current;
                }
                catch (Exception exception)
                {
                    failure = "Continuation population materialization failed: " + exception.Message;
                    break;
                }
                if (!moved)
                {
                    work.Pop();
                    (step as IDisposable)?.Dispose();
                }
                else if (yielded is IEnumerator nested)
                    work.Push(nested);
                else
                    yield return yielded;
            }
            if (string.IsNullOrEmpty(failure) && createdLivingNpcs != expectedLivingNpcs)
                failure = "Continuation population did not materialize every living canonical NPC.";
            if (string.IsNullOrEmpty(failure) &&
                !YQSpatialContinuationValidatorV2.ValidateBasic(artifact).IsStructurallyValid)
                failure = "Continuation acceptance changed while its population was staging.";
            if (string.IsNullOrEmpty(failure) &&
                !TryCheckContinuationPopulationContext(locationRoot, plan, world, artifact, continuation, location,
                    prepared, envelopeHash, envelopeRevision, locationHash, locationRevision, provider, stagingContext, out _, out failure))
                published = false;
            else if (string.IsNullOrEmpty(failure))
            {
                if (stagingContext != null)
                {
                    // note: Newly created actors join the provider's cached renderer publication before any visible frame.
                    var stagedRenderers = staging.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in stagedRenderers) if (renderer != null) renderer.enabled = false;
                }
                // note: Ordinary provider retirement owns this child; actors become visible only after the scoped cast is complete.
                staging.SetActive(true);
                published = true;
            }
            reported = true;
            completed?.Invoke(published, failure);
        }
        finally
        {
            while (work.Count > 0)
                (work.Pop() as IDisposable)?.Dispose();
            if (!published && staging != null)
            {
                staging.SetActive(false);
                UnityEngine.Object.Destroy(staging);
            }
            if (!reported)
                completed?.Invoke(false, "Continuation population was cancelled before publication.");
        }
    }

    private static bool TryCheckContinuationPopulationContext(
        Transform content, GeneratedWorldPlanRecord plan, WorldState world, GeneratedSpatialWorldPlanV2Record artifact,
        GeneratedSpatialContinuationV2Record continuation, GeneratedSpatialContinuationLocationV2Record location,
        YQPreparedSpatialMaterializationV2 prepared, string envelopeHash, long envelopeRevision,
        string locationHash, long locationRevision, YQCompiledWorldSiteInstance expectedProvider,
        YQCompiledWorldSiteInstance.ContinuationPopulationContext stagingContext,
        out YQCompiledWorldSiteInstance provider, out string failure)
    {
        provider = null;
        failure = "Continuation population context became stale or lacks its accepted loaded provider.";
        if (content == null || world == null || plan == null || artifact == null || continuation == null || location?.anchor == null ||
            prepared == null || WorldStateManager.Instance?.State != world ||
            !ReferenceEquals(world.generatedWorldPlan, plan) || !ReferenceEquals(plan.spatialPlanV2, artifact) ||
            !ReferenceEquals(artifact.acceptedContinuation, continuation) ||
            continuation.state != YQSpatialContinuationStateV2.Accepted || continuation.revision != envelopeRevision ||
            continuation.contentHash != envelopeHash || continuation.validatedContentHash != envelopeHash ||
            continuation.locations == null || !continuation.locations.Contains(location) ||
            location.state != YQSpatialContinuationStateV2.Accepted || location.revision != locationRevision ||
            location.contentHash != locationHash || location.validatedContentHash != locationHash ||
            !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) ||
            !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var currentPrepared, out _) ||
            !ReferenceEquals(prepared, currentPrepared) ||
            !prepared.TryGetSiteBySemanticId(location.anchor.sourceSemanticId, out var site) ||
            !ContinuationPreparedSiteMatches(location.anchor, site))
            return false;
        string kitId = location.settlement != null ? location.settlement.runtimeSiteKitId :
            location.encampment != null ? location.encampment.runtimeSiteKitId : location.poiRuntimeSiteKitId;
        // note: Pending actors use a provider-issued context after real collider activation; ordinary callers still require complete publication.
        bool providerReady = stagingContext != null ? stagingContext.TryGetOwner(content, out provider) :
            YQCompiledWorldSiteInstance.TryGetLoadedPopulationContext(location.anchor.sourceSemanticId, kitId, location.compositionSeed, out provider);
        if (providerReady && stagingContext != null)
            providerReady = YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(location.anchor.sourceSemanticId,
                kitId, location.compositionSeed, out var boundProvider) && boundProvider == provider;
        if (!providerReady ||
            expectedProvider != null && provider != expectedProvider || content.parent != provider.transform ||
            content.name != "CompiledSiteContent" ||
            !string.Equals(provider.CanonicalRegionId, location.anchor.parentRegionId, StringComparison.OrdinalIgnoreCase))
            return false;
        failure = string.Empty;
        return true;
    }

    private static bool ContinuationPreparedSiteMatches(YQSiteAnchorV2 anchor, YQSpatialMaterializationSiteV2 site)
    {
        // note: A hash-valid proof claim cannot substitute for the owner's current immutable spatial projection.
        if (anchor == null || anchor.requiredFunctions == null || anchor.memberFootprint == null ||
            !string.Equals(anchor.siteId, site.siteId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(anchor.sourceSemanticId, site.sourceSemanticId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(anchor.parentRegionId, site.parentRegionId, StringComparison.OrdinalIgnoreCase) ||
            anchor.kind != site.kind || anchor.placementMode != site.placementMode || anchor.x != site.x || anchor.z != site.z ||
            anchor.reservedRadius != site.reservedRadius || anchor.maximumSlopeDegrees != site.maximumSlopeDegrees ||
            Mathf.Repeat(anchor.preferredHeadingDegrees, 360f) != site.headingDegrees || !site.terrainReserveReady ||
            anchor.requiredFunctions.Count != site.RequiredFunctions.Count || anchor.memberFootprint.Count != site.MemberFootprint.Count)
            return false;
        for (int index = 0; index < anchor.requiredFunctions.Count; index++)
            if (anchor.requiredFunctions[index] != site.RequiredFunctions[index])
                return false;
        foreach (YQSiteMemberFootprintV2 member in anchor.memberFootprint)
        {
            if (member == null)
                return false;
            bool found = false;
            foreach (YQSiteMemberFootprintV2 preparedMember in site.MemberFootprint)
                if (preparedMember != null && member.memberId == preparedMember.memberId && member.x == preparedMember.x &&
                    member.z == preparedMember.z && member.reservedRadius == preparedMember.reservedRadius && member.sectorIndex == preparedMember.sectorIndex)
                {
                    found = true;
                    break;
                }
            if (!found)
                return false;
        }
        return true;
    }

    public static bool TryValidateContinuationPopulationBindings(GeneratedWorldPlanRecord plan, WorldState world,
        GeneratedSpatialContinuationLocationV2Record location, YQRuntimeWorldAssetRegistry registry, out string failure)
    {
        // note: Construction checks the same supported actor bindings as the live population owner, without spawning or changing NPC state.
        if (location?.pointOfInterest != null)
        {
            failure = "POI continuation has unsupported population.";
            if (plan == null || world == null || location.anchor == null || location.population == null || location.population.Count != 0) return false;
            failure = string.Empty;
            return true;
        }
        return TryValidateContinuationPopulationInputs(plan, world, location, registry, out _, out _, out _, out failure);
    }

    private static bool TryValidateContinuationPopulationInputs(GeneratedWorldPlanRecord plan, WorldState world,
        GeneratedSpatialContinuationLocationV2Record location, YQRuntimeWorldAssetRegistry registry,
        out int expectedLivingNpcs, out GeneratedRegionRecord region, out GeneratedRegionAssetPaletteRecord palette, out string failure)
    {
        expectedLivingNpcs = 0;
        region = null;
        palette = null;
        failure = "Continuation population requires complete canonical inputs.";
        if (plan == null || world?.npcs == null || location?.anchor == null || location.population == null || registry == null)
            return false;
        region = FindRegion(plan, location.anchor.parentRegionId);
        palette = region != null ? FindPalette(plan, region) : null;
        failure = "The current population owner supports continuation settlements and encampments; POI population is unsupported.";
        if (location.pointOfInterest != null || location.settlement == null && location.encampment == null)
            return false;
        if (location.encampment != null && (region == null || palette == null))
        {
            failure = "Continuation encampment requires its canonical region and asset palette in the active plan.";
            return false;
        }
        int minimumPopulation = location.settlement != null ? 2 : 1;
        if (location.population.Count < minimumPopulation)
        {
            failure = location.settlement != null
                ? "Continuation settlement requires at least two persisted residents."
                : "Continuation hostile site requires its persisted hostile leader.";
            return false;
        }
        // note: Preserve dead/removed NPCs and the existing one-commander camp contract without modifying base plan collections.
        foreach (GeneratedNpcPlanRecord npc in location.population)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.npcId))
            {
                failure = "Continuation population contains a missing canonical NPC identity.";
                return false;
            }
            if (plan.generatedNpcs != null && plan.generatedNpcs.Exists(record => record != null &&
                    string.Equals(record.npcId, npc.npcId, StringComparison.OrdinalIgnoreCase)))
            {
                failure = "Continuation NPC identity shadows the base population: " + npc.npcId;
                return false;
            }
            if (!ShouldMaterializeNpc(world, npc.npcId, false))
                continue;
            if (location.settlement != null && (npc.hostile ||
                    !TryResolveResidentPrefab(registry, npc, plan.worldSeed + "|resident_position|" + npc.npcId, out _)) ||
                location.encampment != null && (!npc.hostile || expectedLivingNpcs > 0 ||
                    !TryResolveHostilePrefab(registry, location.encampment, npc,
                        plan.worldSeed + "|encampment_leader|" + npc.npcId, out _, out _)))
            {
                failure = "Continuation NPC has unsupported population binding, visual, or commander multiplicity: " + npc.npcId;
                return false;
            }
            expectedLivingNpcs++;
        }
        failure = string.Empty;
        return true;
    }

    // ============================================================
    // SETTLEMENT RESIDENTS
    // ============================================================

    private static int BuildSettlementResidents(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        WorldState world,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (plan.settlements == null)
            return 0;

        int total = 0;

        for (int settlementIndex = 0;
             settlementIndex < plan.settlements.Count;
             settlementIndex++)
        {
            total += BuildSettlementResidentsForSettlement(
                parent,
                terrain,
                plan,
                world,
                registry,
                plan.settlements[settlementIndex]);
        }

        return total;
    }

    private static int BuildSettlementResidentsForSettlement(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        WorldState world,
        YQRuntimeWorldAssetRegistry registry,
        GeneratedSettlementRecord settlement)
    {
        if (settlement == null)
            return 0;

        settlement.EnsureCollections();
        Vector3 center = YQGeneratedWorldLayout.GetSettlementAnchor(
            plan,
            settlement,
            terrain);
        GameObject settlementPopulation = new GameObject(
            "Residents__" + SafeName(settlement.displayName));
        settlementPopulation.transform.SetParent(parent, false);
        List<GeneratedNpcPlanRecord> residents = FindSettlementNpcs(
            plan,
            settlement.settlementId);
        // note: Service roles can share one authored anchor, so this list prevents presentation overlap within the settlement.
        List<Vector3> occupiedResidentPositions = new List<Vector3>();
        int total = 0;

        for (int index = 0; index < residents.Count; index++)
        {
            GeneratedNpcPlanRecord npcRecord = residents[index];
            if (npcRecord == null || npcRecord.hostile ||
                !ShouldMaterializeNpc(world, npcRecord.npcId))
            {
                continue;
            }

            string seed = plan.worldSeed + "|resident_position|" +
                npcRecord.npcId;
            bool usesCompiledSite =
                YQCompiledWorldSiteInstance.TryResolveResidentPosition(
                    settlement.settlementId,
                    npcRecord,
                    seed,
                    index,
                    occupiedResidentPositions,
                    out Vector3 position);
            if (!usesCompiledSite)
            {
                position = ResolveResidentPosition(
                    plan,
                    settlement,
                    npcRecord,
                    center,
                    seed,
                    index);
            }

            // note: Separation must remain on a valid reviewed floor; a failed offset cannot silently drop a resident through the building.
            if (!TryResolveSeparatedResidentPosition(position, occupiedResidentPositions, seed,
                    usesCompiledSite ? settlement.settlementId : null, out position))
                throw new InvalidOperationException("No separated resident position for canonical NPC " + npcRecord.npcId + " at " + settlement.settlementId);
            if (!usesCompiledSite)
            {
                position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    position);
            }

            occupiedResidentPositions.Add(position);
            bool residentCreated = CreateResident(
                settlementPopulation.transform,
                terrain,
                settlement,
                npcRecord,
                position,
                seed,
                index,
                registry,
                usesCompiledSite);
            // note: A missing actor cannot count as a materialized canonical identity.
            if (!residentCreated)
                throw new InvalidOperationException("Resident creation failed for canonical NPC " + npcRecord.npcId);
            total++;
        }

        return total;
    }

    private static IEnumerator BuildSettlementResidentsForSettlementRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        WorldState world,
        YQRuntimeWorldAssetRegistry registry,
        GeneratedSettlementRecord settlement,
        Action<int> completed,
        IReadOnlyList<GeneratedNpcPlanRecord> scopedPopulation = null,
        bool requireCompiledSite = false)
    {
        if (settlement == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        // note: Accepted continuation payloads are read-only; the ordinary base path retains its existing collection repair.
        if (!requireCompiledSite)
            settlement.EnsureCollections();
        Vector3 center = YQGeneratedWorldLayout.GetSettlementAnchor(
            plan,
            settlement,
            terrain);
        GameObject settlementPopulation = new GameObject(
            "Residents__" + SafeName(settlement.displayName));
        settlementPopulation.transform.SetParent(parent, false);
        List<GeneratedNpcPlanRecord> residents = FindSettlementNpcs(
            plan,
            settlement.settlementId,
            scopedPopulation);
        List<Vector3> occupiedResidentPositions = new List<Vector3>();
        int total = 0;

        for (int index = 0; index < residents.Count; index++)
        {
            GeneratedNpcPlanRecord npcRecord = residents[index];
            if (npcRecord == null || npcRecord.hostile ||
                !ShouldMaterializeNpc(world, npcRecord.npcId, !requireCompiledSite))
            {
                continue;
            }

            string seed = plan.worldSeed + "|resident_position|" +
                npcRecord.npcId;
            bool usesCompiledSite =
                YQCompiledWorldSiteInstance.TryResolveResidentPosition(
                    settlement.settlementId,
                    npcRecord,
                    seed,
                    index,
                    occupiedResidentPositions,
                    out Vector3 position);
            if (!usesCompiledSite)
            {
                if (requireCompiledSite)
                    throw new InvalidOperationException("No loaded provider surface for continuation resident " + npcRecord.npcId);
                position = ResolveResidentPosition(
                    plan,
                    settlement,
                    npcRecord,
                    center,
                    seed,
                    index);
            }

            // note: Use the same support-aware separation contract in cooperative and synchronous population builds.
            if (!TryResolveSeparatedResidentPosition(position, occupiedResidentPositions, seed,
                    usesCompiledSite ? settlement.settlementId : null, out position))
                throw new InvalidOperationException("No separated resident position for canonical NPC " + npcRecord.npcId + " at " + settlement.settlementId);
            if (!usesCompiledSite)
            {
                position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    position);
            }

            occupiedResidentPositions.Add(position);
            bool residentCreated = CreateResident(
                settlementPopulation.transform,
                terrain,
                settlement,
                npcRecord,
                position,
                seed,
                index,
                registry,
                usesCompiledSite,
                requireCompiledSite);
            if (!residentCreated)
                throw new InvalidOperationException("Resident creation failed for canonical NPC " + npcRecord.npcId);
            total++;

            // note: Publish at most one imported resident hierarchy per frame so a populous settlement cannot create a visible gameplay hitch.
            yield return null;
        }

        completed?.Invoke(total);
    }

    public static bool TryValidatePopulationBindings(GeneratedWorldPlanRecord plan, WorldState world,
        out int expectedLivingNpcs, out string failure)
    {
        expectedLivingNpcs = 0;
        failure = string.Empty;
        if (plan == null || plan.generatedNpcs == null)
        {
            failure = "Missing canonical population records.";
            return false;
        }

        // note: These per-build indexes validate explicit site IDs; dialogue prose and display names are never placement authority.
        HashSet<string> settlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> camps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> occupiedLeaderSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (plan.settlements != null)
            foreach (GeneratedSettlementRecord settlement in plan.settlements)
                if (settlement != null && !string.IsNullOrWhiteSpace(settlement.settlementId))
                    settlements.Add(settlement.settlementId);
        if (plan.encampments != null)
            foreach (GeneratedEncampmentRecord camp in plan.encampments)
                if (camp != null && !string.IsNullOrWhiteSpace(camp.encampmentId))
                    camps.Add(camp.encampmentId);

        foreach (GeneratedNpcPlanRecord npc in plan.generatedNpcs)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.npcId) || !identities.Add(npc.npcId))
            {
                failure = "Null, empty, or duplicate canonical NPC identity: " + (npc != null ? npc.npcId : "<null>");
                return false;
            }
            // note: Dead/removed identities stay in the save but are intentionally not recreated on Continue.
            if (!ShouldMaterializeNpc(world, npc.npcId))
                continue;
            bool validSite = npc.hostile
                ? !string.IsNullOrWhiteSpace(npc.encampmentId) && camps.Contains(npc.encampmentId) && string.IsNullOrWhiteSpace(npc.settlementId)
                : !string.IsNullOrWhiteSpace(npc.settlementId) && settlements.Contains(npc.settlementId) && string.IsNullOrWhiteSpace(npc.encampmentId);
            if (!validSite)
            {
                failure = "NPC " + npc.npcId + " has no unambiguous supported settlement/encampment binding.";
                return false;
            }
            // note: The current planner/runtime contract is one canonical commander per camp, plus anonymous rank-and-file.
            if (npc.hostile && !occupiedLeaderSites.Add(npc.encampmentId))
            {
                failure = "Multiple living canonical commanders are bound to " + npc.encampmentId + "; the current encounter contract supports one.";
                return false;
            }
            expectedLivingNpcs++;
        }
        return true;
    }

    private static List<GeneratedNpcPlanRecord>
        FindSettlementNpcs(
            GeneratedWorldPlanRecord plan,
            string settlementId,
            IReadOnlyList<GeneratedNpcPlanRecord> scopedPopulation = null)
    {
        List<GeneratedNpcPlanRecord> result =
            new List<GeneratedNpcPlanRecord>();

        IReadOnlyList<GeneratedNpcPlanRecord> population = scopedPopulation ?? plan?.generatedNpcs;
        if (population == null ||
            string.IsNullOrWhiteSpace(
                settlementId))
        {
            return result;
        }

        for (int i = 0;
             i < population.Count;
             i++)
        {
            GeneratedNpcPlanRecord npc =
                population[i];

            if (npc == null ||
                npc.hostile)
            {
                continue;
            }

            if (string.Equals(
                    npc.settlementId,
                    settlementId,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(
                    npc);
            }
        }

        return result;
    }

    private static Vector3 ResolveResidentPosition(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedNpcPlanRecord npcRecord,
        Vector3 center,
        string seed,
        int index)
    {
        if (YQGeneratedSettlementCellLayout.IsComprehensive(
                plan,
                settlement))
        {
            // note: Comprehensive-cell residents occupy their generated service, guard, or civic station instead of an anonymous radial scatter.
            return center +
                   YQGeneratedSettlementCellLayout
                       .GetResidentLocalPosition(
                           plan,
                           settlement,
                           npcRecord,
                           index,
                           seed);
        }

        float angle =
            Deterministic01(
                seed +
                "|angle") *
            Mathf.PI *
            2f;

        float minRadius =
            index % 2 == 0
                ? 4.5f
                : 9f;

        float maxRadius =
            index % 2 == 0
                ? 10.5f
                : 18f;

        float radius =
            Mathf.Lerp(
                minRadius,
                maxRadius,
                Deterministic01(
                    seed +
                    "|radius"));

        return
            new Vector3(
                center.x +
                    Mathf.Cos(angle) *
                    radius,
                center.y,
                center.z +
                    Mathf.Sin(angle) *
                    radius);
    }

    private const float MinimumResidentSeparation = 2.4f;

    internal static bool IsResidentPositionSeparated(Vector3 candidate, IReadOnlyList<Vector3> occupiedPositions)
    {
        // note: Staged residents are inactive in Physics; this shared horizontal spacing contract must apply during floor search as well as final placement.
        for (int index = 0; occupiedPositions != null && index < occupiedPositions.Count; index++)
        {
            Vector3 offset = candidate - occupiedPositions[index];
            offset.y = 0f;
            if (offset.sqrMagnitude < MinimumResidentSeparation * MinimumResidentSeparation) return false;
        }
        return true;
    }

    private static bool TryResolveSeparatedResidentPosition(
        Vector3 anchor,
        List<Vector3> occupiedPositions,
        string seed,
        string compiledSiteId,
        out Vector3 position)
    {
        position = anchor;
        // note: Keep the finite search near the role anchor; distant floors are not interchangeable with the resident's assigned location.
        for (int attempt = 0; attempt <= 8; attempt++)
        {
            Vector3 candidate = anchor;
            if (attempt > 0)
            {
                float angle = Deterministic01(seed + "|resident_separation_angle|" + (attempt - 1)) * Mathf.PI * 2f;
                float radius = MinimumResidentSeparation + (attempt - 1) * 1.15f;
                // note: Each offset is relative to the role anchor, not accumulated into a random walk outside the building.
                candidate += new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            if (compiledSiteId != null &&
                !YQCompiledWorldSiteInstance.TryProjectToSiteSurface(compiledSiteId, candidate, out candidate))
                continue;

            if (IsResidentPositionSeparated(candidate, occupiedPositions))
            {
                position = candidate;
                return true;
            }
        }
        // note: Exhaustion is a placement failure; returning the occupied anchor would silently stack residents or accept a missing floor.
        return false;
    }

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
    // note: Temporary developer residents reuse production visual/identity binding without altering accepted planning or seeds.
    public static bool DevelopmentCreateResident(Transform parent, GeneratedNpcPlanRecord record, Vector3 position, string seed)
    {
        var registry = YQRuntimeWorldAssetRegistry.Instance;
        if (parent == null || parent.gameObject.activeInHierarchy || record == null ||
            !TryResolveResidentPrefab(registry, record, seed, out _)) return false;
        return CreateResident(parent, null, null, record, position, seed, 0, registry, true, true);
    }
#endif

    private static bool CreateResident(
        Transform parent,
        Terrain terrain,
        GeneratedSettlementRecord settlement,
        GeneratedNpcPlanRecord npcRecord,
        Vector3 position,
        string seed,
        int residentIndex,
        YQRuntimeWorldAssetRegistry registry,
        bool usesReviewedSurface,
        bool requireApprovedVisual = false)
    {
        if (npcRecord == null)
            return false;

        GameObject npc =
            CreateResidentVisual(
                parent,
                terrain,
                npcRecord,
                position,
                seed,
                registry,
                usesReviewedSurface,
                requireApprovedVisual);

        if (npc == null)
            return false;

        npc.name =
            "NPC__" +
            SafeName(
                npcRecord.displayName) +
            "__" +
            SafeName(
                npcRecord.role);

        string[] tags =
            BuildNpcTags(
                npcRecord,
                settlement);

        EntityInfo info =
            npc.GetComponent<EntityInfo>();

        if (info == null)
        {
            info =
                npc.AddComponent<
                    EntityInfo>();
        }

        info.entityId =
            npcRecord.npcId;

        info.displayName =
            npcRecord.displayName;

        info.level =
            1;

        info.factionId =
            npcRecord.factionId;

        info.hostility =
            Hostility.Friendly;

        info.isNotable =
            npcRecord.notable ||
            npcRecord.merchant ||
            npcRecord.guard;

        info.tags =
            tags;

        // note: Character gear uses the existing world inventory and approved asset binder, never a separate NPC item store.
        YQGeneratedActorEquipment.Bind(info, settlement != null ? settlement.regionId : npcRecord.regionId,
            settlement != null ? settlement.settlementId : string.Empty, npcRecord.guard);

        // note: Distant compiled settlements register their residents directly, avoiding a later full-scene EntityInfo scan when reviewed floors stream in.
        YQCompiledWorldSiteInstance.RegisterResidentPositionBinding(
            settlement != null ? settlement.settlementId : string.Empty,
            info,
            (npcRecord.role ?? string.Empty) + " " +
            (npcRecord.archetype ?? string.Empty),
            seed,
            residentIndex);

        NpcDialogueAgent agent =
            npc.GetComponent<
                NpcDialogueAgent>();

        if (agent == null)
        {
            agent =
                npc.AddComponent<
                    NpcDialogueAgent>();
        }

        agent.npcId =
            npcRecord.npcId;

        agent.npcName =
            npcRecord.displayName;

        agent.personaSummary =
            BuildPersonaSummary(
                npcRecord,
                settlement);

        agent.tagsOverride =
            new List<string>(
                tags);
        // note: Residents receive bounded local pacing after identity and dialogue are bound; this keeps authored NPCs alive without replacing the project's navigation authority.
        YQGeneratedNpcWander wander =
            npc.GetComponent<YQGeneratedNpcWander>();
        if (wander == null)
            wander = npc.AddComponent<YQGeneratedNpcWander>();
        // note: New species use their supplied slow gait cadence through the existing wander owner.
        var dotVisual = npc.GetComponent<YQDotCreatureVisual>();
        wander.Configure(seed + "|" + npcRecord.npcId, 6f, dotVisual != null ? dotVisual.AuthoredWalkSpeed : 0.8f);
        // note: Only a configured visual, identity and dialogue binding constitute a created resident.
        return true;
    }

    // ============================================================
    // RESIDENT VISUAL SELECTION
    // ============================================================

    private static GameObject CreateResidentVisual(
        Transform parent,
        Terrain terrain,
        GeneratedNpcPlanRecord record,
        Vector3 position,
        string seed,
        YQRuntimeWorldAssetRegistry registry,
        bool usesReviewedSurface,
        bool requireApprovedVisual = false)
    {
        if (TryResolveResidentPrefab(
                registry,
                record,
                seed,
                out YQRuntimeWorldAssetEntry entry))
        {
            GameObject instance =
                InstantiateRegisteredPrefab(
                    parent,
                    entry,
                    registry);

            if (instance != null)
            {
                instance.transform.position =
                    position;

                float targetHeight =
                    ResolveResidentHeight(
                        record,
                        seed);

                NormalizeVisualHeight(
                    instance,
                    targetHeight);

                // note: Character normalization changes the imported LOD envelope; recalculate it before physics or interaction can expose stale close-range culling.
                YQRuntimeUrpMaterialRepair.StabilizeLodHierarchy(instance);

                PrepareResidentPhysics(
                    instance);

                // note: Imported behaviour stripping is complete; bind only the approved DOT animation observer.
                YQDotCreatureVisual.Bind(instance, entry.assetPath);

                if (usesReviewedSurface)
                {
                    // note: The chosen building floor is authoritative; terrain-only grounding here previously buried otherwise correctly placed residents.
                    if (!TryPlaceResidentOnReviewedSurface(instance, position))
                    {
                        instance.SetActive(false);
                        UnityEngine.Object.Destroy(instance);
                        return null;
                    }
                }
                else
                {
                    GroundCharacterToTerrain(instance, terrain, position);
                }

                Debug.Log(
                    "[YQGeneratedWorldPopulation] " +
                    "Resident visual: " +
                    record.displayName +
                    " -> " +
                    SafeText(
                        entry.assetPath,
                        entry.prefab != null
                            ? entry.prefab.name
                            : "<unknown>"));

                return instance;
            }
        }

        /*
         * Emergency fallback only.
         *
         * A capsule now means the runtime registry did not contain
         * a suitable human character prefab.
         */
        Debug.LogWarning(
            "[YQGeneratedWorldPopulation] VISUAL FALLBACK\n" +
            "No suitable registered human prefab was found for canonical NPC '" +
            record.displayName +
            "'. Using emergency capsule placeholder.");

        // note: Explicit developer template selection fails closed when its approved visual cannot instantiate; production fallback behavior stays unchanged.
        if (requireApprovedVisual) return null;
        GameObject fallback =
            GameObject.CreatePrimitive(
                PrimitiveType.Capsule);

        fallback.transform.SetParent(
            parent,
            false);

        fallback.transform.position =
            position +
            Vector3.up;

        float bodyScale =
            ResolveBodyScale(
                record.ageBand,
                seed);

        fallback.transform.localScale =
            new Vector3(
                0.72f *
                    bodyScale,
                0.92f *
                    bodyScale,
                0.72f *
                    bodyScale);

        ApplyNpcVisualColor(
            fallback,
            record,
            seed);

        // note: Even the emergency capsule has a scaled, center-based pivot; place its feet rather than assuming a fixed one-metre offset.
        if (!TryPlaceResidentOnReviewedSurface(fallback, position))
        {
            fallback.SetActive(false);
            UnityEngine.Object.Destroy(fallback);
            return null;
        }

        Collider collider =
            fallback.GetComponent<
                Collider>();

        if (collider != null)
        {
            collider.isTrigger =
                false;
        }

        return fallback;
    }

    private static bool TryResolveResidentPrefab(
    YQRuntimeWorldAssetRegistry registry,
    GeneratedNpcPlanRecord record,
    string seed,
    out YQRuntimeWorldAssetEntry result)
    {
        result =
            null;

        if (registry == null ||
            record == null)
        {
            return false;
        }

        string description =
            BuildNpcVisualDescription(
                record);

        // note: Accepted NPC appearance/tags may request a shipped race; this changes only its asset binding, never its canonical identity.
        if (YQDotCreatureCatalog.TryResolve(registry, description, "race", seed, out result, out _))
            return true;

        string normalized =
            NormalizeSemanticText(
                description);

        bool explicitFemale =
            ContainsAnySemantic(
                normalized,
                "female",
                "woman",
                "she",
                "her");

        bool explicitMale =
            ContainsAnySemantic(
                normalized,
                "male",
                "man",
                "he",
                "him");

        bool female;

        if (explicitFemale &&
            !explicitMale)
        {
            female =
                true;
        }
        else if (explicitMale &&
                 !explicitFemale)
        {
            female =
                false;
        }
        else
        {
            /*
             * Canonical text does not always specify presentation.
             * In that case the choice is stable per NPC.
             */
            female =
                Deterministic01(
                    seed +
                    "|human_gender") <
                0.5f;
        }

        bool resolved =
            YQRuntimeCreatureAssetIndex
                .TryResolveHuman(
                    registry,
                    female,
                    seed,
                    out result,
                    out string category);

        if (resolved &&
            result != null)
        {
            Debug.Log(
                "[YQGeneratedWorldPopulation] " +
                "Human visual resolved: " +
                record.displayName +
                " -> " +
                category +
                " -> " +
                result.assetPath);

            return true;
        }

        Debug.LogError(
            "[YQGeneratedWorldPopulation] " +
            "NO HUMAN ASSET AVAILABLE for " +
            record.displayName +
            ". Expected runtime registry to contain human male/female prefabs.");

        return false;
    }

    private static float ResolveResidentHeight(
        GeneratedNpcPlanRecord record,
        string seed)
    {
        float ageScale =
            ResolveBodyScale(
                record != null
                    ? record.ageBand
                    : string.Empty,
                seed);

        return
            Mathf.Lerp(
                1.62f,
                1.88f,
                Deterministic01(
                    seed +
                    "|visual_height")) *
            ageScale;
    }

    private static string BuildNpcVisualDescription(
        GeneratedNpcPlanRecord record)
    {
        if (record == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        sb.Append(
            SafeText(
                record.presentation,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                record.appearanceSummary,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                record.role,
                string.Empty));

        sb.Append(" ");

        sb.Append(
            SafeText(
                record.archetype,
                string.Empty));

        if (record.tags != null)
        {
            for (int i = 0;
                 i < record.tags.Count;
                 i++)
            {
                sb.Append(" ");

                sb.Append(
                    record.tags[i]);
            }
        }

        return sb.ToString();
    }

    // ============================================================
    // PERSONA
    // ============================================================

    private static string BuildPersonaSummary(
        GeneratedNpcPlanRecord npc,
        GeneratedSettlementRecord settlement)
    {
        if (npc == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        if (!string.IsNullOrWhiteSpace(
                npc.role))
        {
            sb.Append("Role: ");
            sb.Append(npc.role);
            sb.Append(". ");
        }

        if (!string.IsNullOrWhiteSpace(
                npc.personality))
        {
            sb.Append(
                npc.personality);

            if (!npc.personality
                    .TrimEnd()
                    .EndsWith("."))
            {
                sb.Append(".");
            }

            sb.Append(" ");
        }

        if (!string.IsNullOrWhiteSpace(
                npc.speakingStyle))
        {
            sb.Append("Speaking style: ");
            sb.Append(npc.speakingStyle);
            sb.Append(". ");
        }

        if (!string.IsNullOrWhiteSpace(
                npc.dailyRoutine))
        {
            sb.Append("Routine: ");
            sb.Append(npc.dailyRoutine);
            sb.Append(" ");
        }

        if (!string.IsNullOrWhiteSpace(
                npc.localKnowledge))
        {
            sb.Append("Local knowledge: ");
            sb.Append(npc.localKnowledge);
            sb.Append(" ");
        }

        if (!string.IsNullOrWhiteSpace(
                npc.privateConcern))
        {
            sb.Append("Private concern: ");
            sb.Append(npc.privateConcern);
            sb.Append(" ");
        }

        if (settlement != null)
        {
            sb.Append("They live in ");
            sb.Append(
                settlement.displayName);
            sb.Append(".");
        }

        return
            sb.ToString()
                .Trim();
    }

    private static string[] BuildNpcTags(
        GeneratedNpcPlanRecord npc,
        GeneratedSettlementRecord settlement)
    {
        List<string> tags =
            new List<string>();

        AddUnique(tags, "generated");
        AddUnique(tags, "npc");
        AddUnique(tags, "resident");

        if (npc != null)
        {
            if (npc.tags != null)
            {
                for (int i = 0;
                     i < npc.tags.Count;
                     i++)
                {
                    AddUnique(
                        tags,
                        NormalizeTag(
                            npc.tags[i]));
                }
            }

            AddUnique(
                tags,
                NormalizeTag(
                    npc.role));

            AddUnique(
                tags,
                NormalizeTag(
                    npc.archetype));

            AddUnique(
                tags,
                npc.notable
                    ? "notable"
                    : string.Empty);

            AddUnique(
                tags,
                npc.merchant
                    ? "merchant"
                    : string.Empty);

            AddUnique(
                tags,
                npc.guard
                    ? "guard"
                    : string.Empty);

            AddUnique(
                tags,
                NormalizeTag(
                    npc.regionId));

            AddUnique(
                tags,
                NormalizeTag(
                    npc.factionId));
        }

        if (settlement != null)
        {
            AddUnique(
                tags,
                NormalizeTag(
                    settlement.kind));

            AddUnique(
                tags,
                NormalizeTag(
                    settlement.settlementId));
        }

        return
            tags.ToArray();
    }

    // ============================================================
    // ENCAMPMENTS
    // ============================================================

    private static void BuildEncampmentSynchronously(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        WorldState world,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        out int namedHostiles,
        out int genericHostiles,
        out int rewardContainers)
    {
        int named = 0;
        int generic = 0;
        int rewards = 0;
        IEnumerator routine = BuildEncampmentRoutine(
            parent,
            terrain,
            plan,
            world,
            encampment,
            region,
            palette,
            registry,
            (namedCount, genericCount, rewardCount) =>
            {
                named = namedCount;
                generic = genericCount;
                rewards = rewardCount;
            });

        // note: The retained legacy Build API drains the phased routine immediately; production startup always uses Unity's coroutine scheduler.
        DrainCoroutineSynchronously(routine);
        namedHostiles = named;
        genericHostiles = generic;
        rewardContainers = rewards;
    }

    private static IEnumerator BuildEncampmentRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        WorldState world,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        Action<int, int, int> completed,
        IReadOnlyList<GeneratedNpcPlanRecord> scopedPopulation = null,
        bool requireCompiledSite = false,
        YQCompiledWorldSiteInstance.ContinuationPopulationContext stagingContext = null)
    {
        int namedHostiles =
            0;

        int genericHostiles =
            0;

        int rewardContainers =
            0;

        Vector3 center =
            YQGeneratedWorldLayout
                .GetEncampmentAnchor(
                    plan,
                    encampment,
                    terrain);

        GameObject campRoot =
            new GameObject(
                "Encampment__" +
                SafeName(
                    encampment.displayName) +
                "__" +
                encampment.encampmentId);

        campRoot.transform.SetParent(
            parent,
            false);

        campRoot.transform.position =
            center;

        bool usesCompiledSite =
            YQCompiledWorldSiteInstance.HasSite(encampment.encampmentId);

        // note: Continuation population never starts a load or substitutes legacy camp geometry for its captured loaded provider.
        bool stagedProviderReady = stagingContext != null && stagingContext.TryGetOwner(parent.parent, out var stagedProvider) &&
            YQCompiledWorldSiteInstance.TryGetContinuationGeometryContext(encampment.encampmentId,
                encampment.runtimeSiteKitId, stagedProvider.semanticSliceSeedForPopulation, out var exactProvider) && exactProvider == stagedProvider;
        if (requireCompiledSite && (!usesCompiledSite || !(stagedProviderReady || YQCompiledWorldSiteInstance.IsSiteLoaded(encampment.encampmentId))))
            throw new InvalidOperationException("Continuation hostile provider is not loaded: " + encampment.encampmentId);

        if (usesCompiledSite && !requireCompiledSite)
        {
            // note: A prepared stream root is not loaded geometry. Resolve actors only after its real surfaces and colliders exist.
            bool siteLoaded = false;
            yield return YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(
                encampment.encampmentId, ready => siteLoaded = ready);
            if (!siteLoaded)
            {
                // note: Required hostile sites get one cooperative retry after a transient stream race before the population pass reports a real failure.
                yield return null;
                yield return YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(
                    encampment.encampmentId, ready => siteLoaded = ready);
            }
            if (!siteLoaded)
                throw new InvalidOperationException("Compiled hostile site failed to load before population: " + encampment.encampmentId);
        }

        if (!usesCompiledSite)
        {
            yield return BuildEncampmentSiteAssetsRoutine(
                campRoot.transform,
                terrain,
                encampment,
                palette,
                registry);
        }

        BuildEncampmentRegionVolume(
            campRoot.transform,
            encampment,
            region);

        GeneratedNpcPlanRecord leader =
            FindEncampmentLeader(
                plan,
                encampment.encampmentId,
                world,
                scopedPopulation,
                requireCompiledSite);

        if (leader != null &&
            ShouldMaterializeNpc(
                world,
                leader.npcId,
                !requireCompiledSite))
        {
            string seed =
                plan.worldSeed +
                "|encampment_leader|" +
                leader.npcId;

            Vector3 leaderPosition = center;
            bool compiledLeaderPosition = usesCompiledSite &&
                TryResolveCompiledCampActorPosition(
                    plan,
                    encampment.encampmentId,
                    "hostile leader boss",
                    seed,
                    0,
                    out leaderPosition,
                    requireCompiledSite);

            if (!compiledLeaderPosition)
            {
                // note: A required compiled actor must have a clear reviewed-site spawn; never silently scatter it onto a mandatory road.
                if (usesCompiledSite)
                    throw new InvalidOperationException("No route-clear compiled spawn for canonical leader " + leader.npcId);
                leaderPosition = center + ResolveCampOffset(seed, 3f, 6f);
                leaderPosition.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    leaderPosition);
            }

            bool leaderCreated = CreateNamedHostile(
                campRoot.transform,
                terrain,
                encampment,
                region,
                leader,
                leaderPosition,
                seed,
                registry,
                requireCompiledSite);

            if (!leaderCreated)
                throw new InvalidOperationException("Leader creation failed for canonical NPC " + leader.npcId);
            namedHostiles =
                1;

            // note: Never clone a leader and the remaining encounter cast in the same rendered loading frame.
            yield return null;
        }

        /*
         * Rank-and-file remain deterministic physical encounter entities.
         *
         * They do not receive permanent Ollama-authored identities.
         */
        int rankCount =
            Mathf.Clamp(
                1 +
                encampment.threatTier / 5,
                1,
                2);

        for (int i = 0;
             i < rankCount;
             i++)
        {
            string seed =
                EncampmentSeed(
                    encampment) +
                "|rank_and_file|" +
                i;

            Vector3 position = center;
            bool compiledRankPosition = usesCompiledSite &&
                TryResolveCompiledCampActorPosition(
                    plan,
                    encampment.encampmentId,
                    "hostile enemy encounter",
                    seed,
                    i + 1,
                    out position,
                    requireCompiledSite);

            if (!compiledRankPosition)
            {
                // note: Ordinary encounter actors share the same compiled placement contract as the leader.
                if (usesCompiledSite)
                    throw new InvalidOperationException("No route-clear compiled encounter spawn for " + encampment.encampmentId + ":" + i);
                position = center + ResolveCampOffset(seed, 7f, 17f);
                position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    position);
            }

            bool genericCreated = CreateGenericHostile(
                campRoot.transform,
                terrain,
                encampment,
                region,
                position,
                seed,
                i,
                registry,
                requireCompiledSite);

            if (requireCompiledSite && !genericCreated)
                throw new InvalidOperationException("Continuation hostile visual creation failed: " + encampment.encampmentId + ":" + i);

            genericHostiles++;

            // note: Publish at most one imported hostile character hierarchy per frame.
            yield return null;
        }

        rewardContainers =
            BuildEncampmentRewards(
                campRoot.transform,
                terrain,
                plan,
                encampment,
                palette,
                registry,
                requireCompiledSite);

        completed?.Invoke(
            namedHostiles,
            genericHostiles,
            rewardContainers);

        Debug.Log(
            "[YQGeneratedWorldPopulation] ENCAMPMENT\n" +
            "Name: " +
            encampment.displayName +
            "\nMonster/faction family: " +
            encampment.monsterFamily +
            "\nThreat tier: " +
            encampment.threatTier +
            "\nCanonical leader: " +
            (leader != null
                ? leader.displayName
                : "<missing>") +
            "\nRank-and-file: " +
            rankCount +
            "\nReward containers: " +
            rewardContainers +
            "\nAnchor: " +
            center);
    }

    private static void DrainCoroutineSynchronously(IEnumerator routine)
    {
        if (routine == null)
            return;

        while (routine.MoveNext())
        {
            if (routine.Current is IEnumerator nested)
                DrainCoroutineSynchronously(nested);
        }
    }

    private static bool TryResolveCompiledCampActorPosition(
        GeneratedWorldPlanRecord plan, string locationId, string role, string seed,
        int index, out Vector3 position, bool requireRouteClear = false)
    {
        position = default;
        // note: Authored non-V2 worlds retain their existing site resolver; V2 alone requires the accepted spatial route projection.
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            return YQCompiledWorldSiteInstance.TryResolveWorldActorPosition(locationId, role, seed, index, out position);
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
            throw new InvalidOperationException("Compiled encounter placement requires accepted spatial data: " + failure);
        Vector3 bestCandidate = default;
        float bestRouteMargin = float.MinValue;
        bool foundCandidate = false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            // note: Preserve the original candidate first, then deterministically ask the same reviewed site for another supported, unoccupied position.
            string candidateSeed = attempt == 0 ? seed : seed + "|route_clear|" + attempt;
            if (!YQCompiledWorldSiteInstance.TryResolveWorldActorPosition(locationId, role, candidateSeed, index, out Vector3 candidate))
                continue;
            foundCandidate = true;
            float routeMargin = GetCompiledEncounterRouteMargin(prepared, candidate);
            if (routeMargin > bestRouteMargin)
            {
                // note: Retain the safest reviewed socket so dense sites still materialize when every sampled socket touches a route shoulder.
                bestRouteMargin = routeMargin;
                bestCandidate = candidate;
            }
            if (routeMargin < 0f)
                continue;
            position = candidate;
            return true;
        }
        if (foundCandidate && !requireRouteClear)
        {
            // note: The site resolver already verified floor support and standing clearance; use its best deterministic socket instead of aborting the whole world.
            position = bestCandidate;
            Debug.LogWarning("[WORLDGEN] Encounter sockets touch a reviewed route shoulder; using the safest compiled socket. Location=" + locationId + ", role=" + role);
            return true;
        }
        return false;
    }

    private static bool IsCompiledEncounterRouteClear(YQPreparedSpatialMaterializationV2 prepared, Vector3 candidate)
    {
        // note: Keep both actor/player capsules outside the traversable road. The terrain grading shoulder is not extra pavement and may legitimately include a neighbouring building.
        return GetCompiledEncounterRouteMargin(prepared, candidate) >= 0f;
    }

    private static float GetCompiledEncounterRouteMargin(YQPreparedSpatialMaterializationV2 prepared, Vector3 candidate)
    {
        // note: Score the nearest route edge so fallback sockets choose the greatest available physical separation.
        float minimumMargin = float.MaxValue;
        for (int routeIndex = 0; routeIndex < prepared.RouteCount; routeIndex++)
        {
            var route = prepared.GetRoute(routeIndex);
            int count = prepared.GetRoutePointCount(routeIndex);
            for (int pointIndex = 1; pointIndex < count; pointIndex++)
            {
                var first = prepared.GetRoutePoint(routeIndex, pointIndex - 1);
                var second = prepared.GetRoutePoint(routeIndex, pointIndex);
                Vector2 a = new Vector2(first.x, first.z);
                Vector2 delta = new Vector2(second.x - first.x, second.z - first.z);
                Vector2 point = new Vector2(candidate.x, candidate.z);
                float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
                float clearance = Mathf.Max(route.width, Mathf.Max(first.width, second.width)) * .5f + 1.5f;
                float margin = Vector2.Distance(point, a + delta * t) - clearance;
                if (margin < minimumMargin)
                    minimumMargin = margin;
            }
        }
        return minimumMargin == float.MaxValue ? float.MaxValue : minimumMargin;
    }

    private static GeneratedNpcPlanRecord
        FindEncampmentLeader(
            GeneratedWorldPlanRecord plan,
            string encampmentId,
            WorldState world,
            IReadOnlyList<GeneratedNpcPlanRecord> scopedPopulation = null,
            bool requireCompiledSite = false)
    {
        IReadOnlyList<GeneratedNpcPlanRecord> population = scopedPopulation ?? plan?.generatedNpcs;
        if (population == null ||
            string.IsNullOrWhiteSpace(
                encampmentId))
        {
            return null;
        }

        GeneratedNpcPlanRecord fallback =
            null;

        for (int i = 0;
             i < population.Count;
             i++)
        {
            GeneratedNpcPlanRecord npc =
                population[i];

            if (npc == null ||
                !npc.hostile ||
                // note: A persisted dead commander must not shadow a different living canonical actor assigned to this camp.
                !ShouldMaterializeNpc(world, npc.npcId, !requireCompiledSite) ||
                !string.Equals(
                    npc.encampmentId,
                    encampmentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (npc.boss ||
                string.Equals(
                    npc.archetype,
                    "hostile_leader",
                    StringComparison.OrdinalIgnoreCase))
            {
                return npc;
            }

            fallback ??=
                npc;
        }

        return fallback;
    }

    private static Vector3 ResolveCampOffset(
        string seed,
        float minRadius,
        float maxRadius)
    {
        float angle =
            Deterministic01(
                seed +
                "|angle") *
            Mathf.PI *
            2f;

        float radius =
            Mathf.Lerp(
                minRadius,
                maxRadius,
                Deterministic01(
                    seed +
                    "|radius"));

        return
            new Vector3(
                Mathf.Cos(angle) *
                    radius,
                0f,
                Mathf.Sin(angle) *
                    radius);
    }

    // ============================================================
    // HOSTILES
    // ============================================================

    private static bool CreateNamedHostile(
        Transform parent,
        Terrain terrain,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        GeneratedNpcPlanRecord npcRecord,
        Vector3 position,
        string seed,
        YQRuntimeWorldAssetRegistry registry,
        bool requireCompiledSite = false)
    {
        if (npcRecord == null)
            return false;

        int tier =
            Mathf.Max(
                1,
                encampment.threatTier);

        GameObject enemyObject =
            CreateHostileVisual(
                parent,
                terrain,
                encampment,
                npcRecord,
                position,
                true,
                seed,
                registry,
                requireCompiledSite);

        if (enemyObject == null)
            return false;

        enemyObject.name =
            "HostileLeader__" +
            SafeName(
                npcRecord.displayName);

        EntityInfo info =
            enemyObject.GetComponent<
                EntityInfo>();

        if (info == null)
        {
            info =
                enemyObject.AddComponent<
                    EntityInfo>();
        }

        info.entityId =
            npcRecord.npcId;

        info.displayName =
            npcRecord.displayName;

        info.level =
            Mathf.Clamp(
                tier +
                2,
                1,
                14);

        info.factionId =
            SafeText(
                npcRecord.factionId,
                encampment.inhabitantFactionId);

        info.hostility =
            Hostility.Hostile;

        info.isNotable =
            true;

        info.tags =
            BuildHostileTags(
                npcRecord,
                encampment,
                region,
                true);

        YQInvestorEnemy enemy =
            enemyObject.GetComponent<
                YQInvestorEnemy>();

        if (enemy == null)
        {
            enemy =
                enemyObject.AddComponent<
                    YQInvestorEnemy>();
        }

        ConfigureEnemyCombat(
            enemy,
            encampment,
            npcRecord.displayName,
            info.factionId,
            tier,
            true);

        enemy.Initialize(
            null);

        YQGeneratedActorEquipment.Bind(info, enemy.semanticRegionId, encampment.encampmentId, true);

        // note: Attach runtime grounding at the frame-budgeted leader spawn point instead of relying on a later all-enemy scan.
        YQGeneratedEnemyRuntimeSafety.EnsureAttached(
            enemy);
        // note: Report success only after the canonical identity and combat receiver have been configured.
        return true;
    }

    private static bool CreateGenericHostile(
        Transform parent,
        Terrain terrain,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        Vector3 position,
        string seed,
        int index,
        YQRuntimeWorldAssetRegistry registry,
        bool requireCompiledSite = false)
    {
        int tier =
            Mathf.Max(
                1,
                encampment.threatTier);

        string displayName =
            BuildGenericHostileLabel(
                encampment,
                index);

        GameObject enemyObject =
            CreateHostileVisual(
                parent,
                terrain,
                encampment,
                null,
                position,
                false,
                seed,
                registry,
                requireCompiledSite);

        if (enemyObject == null)
            return false;

        enemyObject.name =
            "Hostile__" +
            SafeName(
                displayName) +
            "__" +
            index;

        EntityInfo info =
            enemyObject.GetComponent<
                EntityInfo>();

        if (info == null)
        {
            info =
                enemyObject.AddComponent<
                    EntityInfo>();
        }

        info.entityId =
            "generated_rank_" +
            StableHash32(
                seed +
                "|" +
                encampment.encampmentId)
                .ToString("x8");

        info.displayName =
            displayName;

        info.level =
            Mathf.Clamp(
                tier,
                1,
                12);

        info.factionId =
            SafeText(
                encampment.inhabitantFactionId,
                "generated_hostiles");

        info.hostility =
            Hostility.Hostile;

        info.isNotable =
            false;

        info.tags =
            new[]
            {
                "generated",
                "enemy",
                "hostile",
                "rank_and_file",
                NormalizeTag(
                    encampment.kind),
                NormalizeTag(
                    encampment.monsterFamily),
                NormalizeTag(
                    encampment.encampmentId),
                NormalizeTag(
                    region.regionId)
            };

        YQInvestorEnemy enemy =
            enemyObject.GetComponent<
                YQInvestorEnemy>();

        if (enemy == null)
        {
            enemy =
                enemyObject.AddComponent<
                    YQInvestorEnemy>();
        }

        ConfigureEnemyCombat(
            enemy,
            encampment,
            displayName,
            info.factionId,
            tier,
            false);

        enemy.Initialize(
            null);

        YQGeneratedActorEquipment.Bind(info, enemy.semanticRegionId, encampment.encampmentId, true);

        // note: Attach runtime grounding at the frame-budgeted hostile spawn point so safety setup cannot bunch into a later frame.
        YQGeneratedEnemyRuntimeSafety.EnsureAttached(
            enemy);
        return true;
    }
    private static void ApplyHumanHostileReadability(
    GameObject instance,
    string assetPath,
    bool isLeader,
    float visualHeight)
    {
        if (instance == null ||
            !IsHumanHostileAsset(
                assetPath))
        {
            return;
        }

        /*
         * Do not stack markers if this visual is configured more than once.
         */
        if (instance.transform.Find(
                "YQ_HostileGroundTell") != null ||
            instance.transform.Find(
                "YQ_HostileMarker") != null)
        {
            return;
        }

        Color hostileColor =
            isLeader
                ? new Color(
                    1f,
                    0.10f,
                    0.025f,
                    1f)
                : new Color(
                    0.82f,
                    0.035f,
                    0.025f,
                    1f);

        float radius =
            isLeader
                ? 0.88f
                : 0.68f;

        float markerSize =
            isLeader
                ? 0.22f
                : 0.15f;

        /*
         * Resolve the HUMAN model's bounds BEFORE adding our tell renderers.
         *
         * This gives us the real feet/head positions rather than assuming
         * the imported prefab's origin is at its feet.
         */
        bool hasBounds =
            TryGetRenderableBounds(
                instance,
                out Bounds visualBounds);

        Vector3 visualCenter =
            hasBounds
                ? visualBounds.center
                : instance.transform.position +
                  Vector3.up *
                  Mathf.Max(
                      0.9f,
                      visualHeight *
                      0.5f);

        float groundY =
            hasBounds
                ? visualBounds.min.y +
                  0.025f
                : instance.transform.position.y +
                  0.025f;

        float markerY =
            hasBounds
                ? visualBounds.max.y +
                  (isLeader
                      ? 0.34f
                      : 0.27f)
                : instance.transform.position.y +
                  Mathf.Max(
                      1.8f,
                      visualHeight +
                      0.28f);

        // ============================================================
        // RED GROUND TELL
        // ============================================================

        GameObject groundTell =
            GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);

        groundTell.name =
            "YQ_HostileGroundTell";

        /*
         * Establish world-space placement first, then parent it.
         */
        groundTell.transform.position =
            new Vector3(
                visualCenter.x,
                groundY,
                visualCenter.z);

        groundTell.transform.rotation =
            Quaternion.identity;

        groundTell.transform.SetParent(
            instance.transform,
            true);

        SetReadableMarkerWorldScale(
            groundTell.transform,
            new Vector3(
                radius * 2f,
                0.018f,
                radius * 2f));

        Collider groundCollider =
            groundTell.GetComponent<
                Collider>();

        if (groundCollider != null)
        {
            UnityEngine.Object.Destroy(
                groundCollider);
        }

        Renderer groundRenderer =
            groundTell.GetComponent<
                Renderer>();

        ConfigureHostileTellRenderer(
            groundRenderer,
            hostileColor);

        // ============================================================
        // OVERHEAD HOSTILE MARKER
        // ============================================================

        GameObject marker =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere);

        marker.name =
            "YQ_HostileMarker";

        marker.transform.position =
            new Vector3(
                visualCenter.x,
                markerY,
                visualCenter.z);

        marker.transform.rotation =
            Quaternion.identity;

        marker.transform.SetParent(
            instance.transform,
            true);

        SetReadableMarkerWorldScale(
            marker.transform,
            Vector3.one *
            markerSize);

        Collider markerCollider =
            marker.GetComponent<
                Collider>();

        if (markerCollider != null)
        {
            UnityEngine.Object.Destroy(
                markerCollider);
        }

        Renderer markerRenderer =
            marker.GetComponent<
                Renderer>();

        ConfigureHostileTellRenderer(
            markerRenderer,
            hostileColor);
    }
    private static void SetReadableMarkerWorldScale(
    Transform target,
    Vector3 desiredWorldScale)
    {
        if (target == null)
            return;

        Transform parent =
            target.parent;

        if (parent == null)
        {
            target.localScale =
                desiredWorldScale;

            return;
        }

        Vector3 parentScale =
            parent.lossyScale;

        target.localScale =
            new Vector3(
                desiredWorldScale.x /
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(
                        parentScale.x)),

                desiredWorldScale.y /
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(
                        parentScale.y)),

                desiredWorldScale.z /
                Mathf.Max(
                    0.0001f,
                    Mathf.Abs(
                        parentScale.z)));
    }
    private static bool IsHumanHostileAsset(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return false;
        }

        string normalized =
            assetPath
                .Replace(
                    '\\',
                    '/')
                .ToLowerInvariant();

        return
            normalized.Contains(
                "/human - humans/");
    }

    private static void ConfigureHostileTellRenderer(
        Renderer renderer,
        Color color)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode =
            UnityEngine.Rendering
                .ShadowCastingMode.Off;

        renderer.receiveShadows =
            false;

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Unlit/Color");
        }

        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Standard");
        }

        if (shader == null)
            return;

        Material material =
            new Material(
                shader);

        material.name =
            "YQ_HostileTell_Material";

        if (material.HasProperty(
                "_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                color);
        }

        if (material.HasProperty(
                "_Color"))
        {
            material.SetColor(
                "_Color",
                color);
        }

        if (material.HasProperty(
                "_EmissionColor"))
        {
            material.SetColor(
                "_EmissionColor",
                color * 1.7f);

            material.EnableKeyword(
                "_EMISSION");
        }

        renderer.sharedMaterial =
            material;
    }
    private static GameObject CreateHostileVisual(
    Transform parent,
    Terrain terrain,
    GeneratedEncampmentRecord encampment,
    GeneratedNpcPlanRecord leaderRecord,
    Vector3 position,
    bool leader,
    string seed,
    YQRuntimeWorldAssetRegistry registry,
    bool requireCompiledSite = false)
    {
        if (TryResolveHostilePrefab(
                registry,
                encampment,
                leaderRecord,
                seed,
                out YQRuntimeWorldAssetEntry entry,
                out string resolvedCategory))
        {
            GameObject instance =
                InstantiateRegisteredPrefab(
                    parent,
                    entry,
                    registry);

            if (instance != null)
            {
                instance.transform.position =
                    position;

                float targetHeight =
                    ResolveMonsterTargetHeight(
                        encampment,
                        leader);

                /*
                 * First establish the actual physical character.
                 */
                if (!TryNormalizeHostileVisualEnvelope(
                        instance,
                        targetHeight,
                        resolvedCategory))
                {
                    // note: Reject a semantically incompatible or pathological silhouette before it can obscure the player camera or create oversized combat collision.
                    Debug.LogWarning(
                        "[YQGeneratedWorldPopulation] HOSTILE VISUAL REJECTED\n" +
                        "Family: " +
                        SafeText(
                            encampment.monsterFamily,
                            "<unknown>") +
                        "\nResolved category: " +
                        SafeText(
                            resolvedCategory,
                            "<unknown>") +
                        "\nPrefab: " +
                        SafeText(
                            entry.assetPath,
                            entry.prefab.name));

                    UnityEngine.Object.Destroy(
                        instance);

                    if (requireCompiledSite)
                        return null;
                    return
                        CreateHostileFallbackPrimitive(
                            parent,
                            position,
                            leader,
                            seed);
                }

                // note: Hostile envelope fitting occurs after prefab material repair, so LOD bounds must be rebuilt at the final combat scale.
                YQRuntimeUrpMaterialRepair.StabilizeLodHierarchy(instance);

                PrepareHostilePhysics(
                    instance,
                    leader);

                // note: Existing hostile physics, combat and identity remain authoritative for the new visual shell.
                YQDotCreatureVisual.Bind(instance, entry.assetPath);

                if (requireCompiledSite)
                {
                    // note: Continuation actors keep the reviewed socket's floor height and never resample the terrain underneath it.
                    if (!TryPlaceResidentOnReviewedSurface(instance, position))
                    {
                        instance.SetActive(false);
                        UnityEngine.Object.Destroy(instance);
                        return null;
                    }
                }
                else
                    GroundCharacterToTerrain(instance, terrain, position);

                /*
                 * Only AFTER collider generation and grounding do we add
                 * presentation-only hostile indicators.
                 *
                 * Otherwise their renderers contaminate bounds calculations.
                 */
                ApplyHumanHostileReadability(
                    instance,
                    entry.assetPath,
                    leader,
                    targetHeight);

                Debug.Log(
                    "[YQGeneratedWorldPopulation] " +
                    "Hostile visual: " +
                    SafeText(
                        leaderRecord != null
                            ? leaderRecord.displayName
                            : encampment.monsterFamily,
                        "hostile") +
                    " -> " +
                    SafeText(
                        entry.assetPath,
                        entry.prefab != null
                            ? entry.prefab.name
                            : "<unknown>"));

                return instance;
            }
        }

        Debug.LogWarning(
            "[YQGeneratedWorldPopulation] VISUAL FALLBACK\n" +
            "No suitable registered monster prefab matched family '" +
            SafeText(
                encampment.monsterFamily,
                "<unknown>") +
            "' at " +
            encampment.displayName +
            ". Using emergency capsule placeholder.");

        // note: Strict continuation materialization reports missing approved visuals rather than publishing emergency primitives.
        if (requireCompiledSite)
            return null;
        return
            CreateHostileFallbackPrimitive(
                parent,
                position,
                leader,
                seed);
    }

    private static bool TryResolveHostilePrefab(
        YQRuntimeWorldAssetRegistry registry,
        GeneratedEncampmentRecord encampment,
        GeneratedNpcPlanRecord leaderRecord,
        string seed,
        out YQRuntimeWorldAssetEntry result,
        out string resolvedCategory)
    {
        result =
            null;

        resolvedCategory =
            string.Empty;

        if (registry == null ||
            encampment == null)
        {
            return false;
        }

        // note: Population must resolve through the dedicated lazy creature shard; scanning the empty lazy root registry previously forced valid soldiers, burrowers, and generated monster families into capsule placeholders.
        if (YQRuntimeCreatureAssetIndex.TryResolveMonster(
                registry,
                SafeText(
                    encampment.monsterFamily,
                    "generated monster"),
                SafeText(
                    encampment.encampmentId,
                    seed),
                seed,
                out result,
                out resolvedCategory) &&
            result != null)
        {
            Debug.Log(
                "[YQGeneratedWorldPopulation] Hostile visual resolved: " +
                encampment.displayName +
                " -> " +
                resolvedCategory +
                " -> " +
                result.assetPath);

            return true;
        }

        string semanticSource =
            SafeText(
                encampment.monsterFamily,
                string.Empty) +
            " " +
            SafeText(
                encampment.kind,
                string.Empty) +
            " " +
            SafeText(
                encampment.abilityProfile,
                string.Empty) +
            " " +
            SafeText(
                encampment.surfacePresentation,
                string.Empty) +
            " " +
            SafeText(
                leaderRecord != null
                    ? leaderRecord.role
                    : string.Empty,
                string.Empty) +
            " " +
            SafeText(
                leaderRecord != null
                    ? leaderRecord.appearanceSummary
                    : string.Empty,
                string.Empty);

        if (leaderRecord != null &&
            leaderRecord.tags != null)
        {
            semanticSource +=
                " " +
                string.Join(
                    " ",
                    leaderRecord.tags);
        }

        string normalized =
            NormalizeSemanticText(
                semanticSource);

        List<string> preferred =
            ExtractDistinctiveSemanticTerms(
                semanticSource);

        string[] required;

        /*
         * Humanoid hostile families are checked before material words
         * such as "stone" so "Stone Cultists" remain humanoids rather
         * than becoming rock monsters.
         */
        if (ContainsAnySemantic(
                normalized,
                "bandit",
                "raider",
                "brigand",
                "cultist",
                "cult",
                "soldier",
                "mercenary",
                "outlaw",
                "pirate",
                "warrior",
                "human",
                "humanoid",
                "scavenger",
                "marauder",
                "goblin",
                "orc",
                "kobold"))
        {
            required =
                new[]
                {
                    "bandit",
                    "raider",
                    "cultist",
                    "human",
                    "warrior",
                    "soldier",
                    "male",
                    "female"
                };

            preferred.Add(
                "human");
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "mushroom",
                     "shroom",
                     "fungus",
                     "fungal"))
        {
            required =
                new[]
                {
                    "mushroom",
                    "shroom",
                    "fungus",
                    "fungal"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "rock",
                     "stone",
                     "golem",
                     "earth",
                     "elemental"))
        {
            required =
                new[]
                {
                    "rock",
                    "stone",
                    "golem",
                    "elemental"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "worm",
                     "wyrm",
                     "larva",
                     "grub",
                     "burrower"))
        {
            required =
                new[]
                {
                    "worm",
                    "wyrm",
                    "larva",
                    "grub",
                    "burrower"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "demon",
                     "fiend",
                     "devil",
                     "infernal"))
        {
            required =
                new[]
                {
                    "demon",
                    "fiend",
                    "devil",
                    "infernal"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "dragon",
                     "drake",
                     "wyvern"))
        {
            required =
                new[]
                {
                    "dragon",
                    "drake",
                    "wyvern"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "plant",
                     "vine",
                     "thorn",
                     "flora",
                     "floral",
                     "ent"))
        {
            required =
                new[]
                {
                    "plant",
                    "vine",
                    "thorn",
                    "flora",
                    "ent"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "mimic"))
        {
            required =
                new[]
                {
                    "mimic"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "undead",
                     "skeleton",
                     "zombie",
                     "corpse",
                     "risen"))
        {
            required =
                new[]
                {
                    "undead",
                    "skeleton",
                    "zombie",
                    "risen"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "spider",
                     "arachnid"))
        {
            required =
                new[]
                {
                    "spider",
                    "arachnid"
                };
        }
        else if (ContainsAnySemantic(
                     normalized,
                     "wolf",
                     "beast",
                     "hound"))
        {
            required =
                new[]
                {
                    "wolf",
                    "beast",
                    "hound"
                };
        }
        else
        {
            required =
                preferred.ToArray();
        }

        string[] excluded =
        {
            "tree",
            "bush",
            "grass",
            "rockmesh",
            "statue",
            "building",
            "structure",
            "wall",
            "floor",
            "terrain",
            "mountain",
            "house",
            "door"
        };

        if (TryResolveSemanticPrefab(
                registry,
                seed +
                    "|hostile_exact",
                required,
                preferred.ToArray(),
                excluded,
                true,
                out result))
        {
            resolvedCategory =
                YQRuntimeCreatureAssetIndex
                    .ClassifyEntry(
                        result);

            if (IsHumanoidGeneratedFamily(
                    normalized) &&
                IsExplicitNonHumanoidMonsterCategory(
                    resolvedCategory))
            {
                // note: A humanoid LLM family must never accept a dragon, demon, or other giant silhouette from a legacy semantic fallback.
                result =
                    null;

                resolvedCategory =
                    string.Empty;

                return false;
            }

            if (IsHumanoidGeneratedFamily(
                    normalized))
            {
                // note: An exact legacy goblin/orc/scavenger prefab may be generically classified, but it still receives the humanoid gameplay envelope.
                resolvedCategory =
                    YQRuntimeCreatureAssetIndex
                        .HumanoidHostile;
            }

            return true;
        }

        // note: A missing semantic match uses the explicit capsule fallback; selecting an arbitrary monster species breaks authored family identity and visual scale guarantees.
        return false;
    }

    private static bool IsHumanoidGeneratedFamily(
        string normalizedSemantic)
    {
        return ContainsAnySemantic(
            normalizedSemantic,
            "bandit",
            "raider",
            "brigand",
            "cultist",
            "cult",
            "soldier",
            "mercenary",
            "outlaw",
            "pirate",
            "warrior",
            "human",
            "humanoid",
            "scavenger",
            "marauder",
            "goblin",
            "orc",
            "kobold");
    }

    private static bool IsHumanoidVisualCategory(
        string category)
    {
        return
            string.Equals(
                category,
                YQRuntimeCreatureAssetIndex.HumanoidHostile,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                category,
                YQRuntimeCreatureAssetIndex.HumanMale,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                category,
                YQRuntimeCreatureAssetIndex.HumanFemale,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                category,
                YQRuntimeCreatureAssetIndex.HumanGeneric,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitNonHumanoidMonsterCategory(
        string category)
    {
        return
            string.Equals(category, YQRuntimeCreatureAssetIndex.Dragon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Demon, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.RockMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.WormMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.PlantMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.MushroomMonster, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Mimic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Undead, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Spider, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, YQRuntimeCreatureAssetIndex.Beast, StringComparison.OrdinalIgnoreCase);
    }

    private static float ResolveMonsterTargetHeight(
        GeneratedEncampmentRecord encampment,
        bool leader)
    {
        string text =
            NormalizeSemanticText(
                SafeText(
                    encampment != null
                        ? encampment.monsterFamily
                        : string.Empty,
                    string.Empty));

        float height;

        if (ContainsAnySemantic(
                text,
                "dragon",
                "drake",
                "wyvern"))
        {
            height =
                3.4f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "rock",
                     "stone",
                     "golem"))
        {
            height =
                2.4f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "demon",
                     "fiend"))
        {
            height =
                2.15f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "worm",
                     "larva",
                     "grub"))
        {
            height =
                1.25f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "mushroom",
                     "fungus",
                     "shroom"))
        {
            height =
                1.45f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "mimic"))
        {
            height =
                1.15f;
        }
        else if (ContainsAnySemantic(
                     text,
                     "plant",
                     "vine",
                     "thorn"))
        {
            height =
                1.85f;
        }
        else
        {
            height =
                1.85f;
        }

        if (leader)
        {
            height *=
                1.12f;
        }

        return height;
    }

    private static GameObject CreateHostileFallbackPrimitive(
        Transform parent,
        Vector3 position,
        bool leader,
        string seed)
    {
        GameObject enemyObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Capsule);

        enemyObject.transform.SetParent(
            parent,
            false);

        enemyObject.transform.position =
            position +
            Vector3.up *
                (leader
                    ? 1.08f
                    : 0.92f);

        float scale =
            leader
                ? 1.12f
                : Mathf.Lerp(
                    0.86f,
                    1.02f,
                    Deterministic01(
                        seed +
                        "|body"));

        enemyObject.transform.localScale =
            Vector3.one *
            scale;

        Renderer renderer =
            enemyObject.GetComponent<
                Renderer>();

        if (renderer != null)
        {
            MaterialPropertyBlock block =
                new MaterialPropertyBlock();

            renderer.GetPropertyBlock(
                block);

            Color color =
                leader
                    ? new Color(
                        0.42f,
                        0.07f,
                        0.06f,
                        1f)
                    : new Color(
                        0.52f,
                        0.14f,
                        0.10f,
                        1f);

            block.SetColor(
                "_BaseColor",
                color);

            block.SetColor(
                "_Color",
                color);

            renderer.SetPropertyBlock(
                block);
        }

        Rigidbody rb =
            enemyObject.AddComponent<
                Rigidbody>();

        rb.useGravity =
            true;

        rb.constraints =
            RigidbodyConstraints
                .FreezeRotation;

        rb.mass =
            leader
                ? 2f
                : 1f;

        return enemyObject;
    }

    // ============================================================
    // CHARACTER PHYSICS / VISUAL PREP
    // ============================================================

    private static GameObject InstantiateRegisteredPrefab(
        Transform parent,
        YQRuntimeWorldAssetEntry entry,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (entry == null ||
            entry.prefab == null ||
            registry == null)
        {
            return null;
        }

        GameObject instance =
            UnityEngine.Object.Instantiate(
                entry.prefab,
                parent);

        registry.ApplyMaterialOverrides(
            entry.assetPath,
            instance);

        YQRuntimeUrpMaterialRepair
            .RepairHierarchy(
                instance);


        return instance;
    }

    private static void PrepareResidentPhysics(
    GameObject root)
    {
        if (root == null)
            return;

        // note: Resident movement belongs to the project wander controller; imported animation root motion must not lift or slide the grounded actor.
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            animator.applyRootMotion = false;

        /*
         * GENERATED RESIDENT PREFABS ARE VISUAL SHELLS.
         *
         * Marketplace character prefabs can ship with demo controllers,
         * footstep handlers and AudioSources which react to AnimationEvents.
         *
         * Those package behaviours are not part of YourQuest NPC logic.
         *
         * This sanitation happens BEFORE EntityInfo and NpcDialogueAgent are
         * added by CreateResident(), so it is safe to strip the imported
         * runtime behaviours here.
         */

        AudioSource[] audioSources =
            root.GetComponentsInChildren<
                AudioSource>(
                    true);

        for (int i = 0;
             i < audioSources.Length;
             i++)
        {
            AudioSource source =
                audioSources[i];

            if (source == null)
                continue;

            /*
             * Stop any clip or PlayOneShot voice already created during
             * prefab Awake/initialization.
             */
            source.Stop();

            source.playOnAwake =
                false;

            source.loop =
                false;

            source.enabled =
                false;

            UnityEngine.Object.Destroy(
                source);
        }

        /*
         * Our animation-event receiver is not needed on passive generated
         * residents. Remove any copy inherited from a prefab or previously
         * modified marketplace asset.
         */
        YQAnimationEventAudioReceiver[] receivers =
            root.GetComponentsInChildren<
                YQAnimationEventAudioReceiver>(
                    true);

        for (int i = 0;
             i < receivers.Length;
             i++)
        {
            YQAnimationEventAudioReceiver receiver =
                receivers[i];

            if (receiver == null)
                continue;

            receiver.enabled =
                false;

            UnityEngine.Object.Destroy(
                receiver);
        }

        /*
         * Strip imported package/demo MonoBehaviours.
         *
         * Animator is NOT a MonoBehaviour, so animation/rendering remains.
         *
         * YourQuest's EntityInfo and NpcDialogueAgent are added AFTER this
         * method returns, so they are not affected.
         */
        MonoBehaviour[] importedBehaviours =
            root.GetComponentsInChildren<
                MonoBehaviour>(
                    true);

        for (int i = 0;
             i < importedBehaviours.Length;
             i++)
        {
            MonoBehaviour behaviour =
                importedBehaviours[i];

            if (behaviour == null)
                continue;

            /*
             * Receiver was already handled explicitly above.
             */
            if (behaviour is
                YQAnimationEventAudioReceiver)
            {
                continue;
            }

            behaviour.enabled =
                false;

            // note: Imported character managers can require one another; disabling the visual-shell behaviours avoids package activity without violating Unity's component dependency graph.
        }

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            /*
             * Set kinematic before touching velocity.
             *
             * Generated residents are stationary world NPCs.
             */
            if (!body.isKinematic)
            {
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }

        EnsureCharacterCollider(
            root);
    }

    private static void PrepareHostilePhysics(
        GameObject root,
        bool leader)
    {
        if (root == null)
            return;

        Rigidbody rootBody =
            root.GetComponent<
                Rigidbody>();

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null ||
                body ==
                    rootBody)
            {
                continue;
            }

            /*
             * Disable prefab ragdoll bodies during ordinary AI operation.
             */
            if (!body.isKinematic)
            {
                // note: Unity warns if velocity is written after an imported child body is already kinematic.
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }

        if (rootBody == null)
        {
            rootBody =
                root.AddComponent<
                    Rigidbody>();
        }

        rootBody.isKinematic =
            false;

        rootBody.useGravity =
            true;

        rootBody.constraints =
            RigidbodyConstraints
                .FreezeRotation;

        rootBody.mass =
            leader
                ? 2f
                : 1f;

        rootBody.linearVelocity =
            Vector3.zero;

        rootBody.angularVelocity =
            Vector3.zero;

        EnsureCharacterCollider(
            root);
    }

    private static void EnsureCharacterCollider(
        GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider existing =
                colliders[i];

            if (existing != null &&
                !existing.isTrigger)
            {
                return;
            }
        }

        if (!TryGetRenderableBounds(
                root,
                out Bounds bounds))
        {
            CapsuleCollider defaultCollider =
                root.AddComponent<
                    CapsuleCollider>();

            defaultCollider.center =
                new Vector3(
                    0f,
                    0.9f,
                    0f);

            defaultCollider.height =
                1.8f;

            defaultCollider.radius =
                0.35f;

            return;
        }

        CapsuleCollider capsule =
            root.AddComponent<
                CapsuleCollider>();

        Vector3 localCenter =
            root.transform
                .InverseTransformPoint(
                    bounds.center);

        Vector3 scale =
            root.transform.lossyScale;

        float yScale =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(
                    scale.y));

        float xzScale =
            Mathf.Max(
                0.0001f,
                Mathf.Max(
                    Mathf.Abs(
                        scale.x),
                    Mathf.Abs(
                        scale.z)));

        capsule.center =
            localCenter;

        capsule.height =
            Mathf.Max(
                0.4f,
                bounds.size.y /
                yScale);

        capsule.radius =
            Mathf.Max(
                0.12f,
                Mathf.Min(
                    bounds.size.x,
                    bounds.size.z) *
                0.30f /
                xzScale);
    }

    private static void NormalizeVisualHeight(
        GameObject root,
        float targetHeight)
    {
        if (root == null ||
            targetHeight <= 0f)
        {
            return;
        }

        if (!TryGetRenderableBounds(
                root,
                out Bounds bounds))
        {
            return;
        }

        float currentHeight =
            bounds.size.y;

        if (currentHeight <=
            0.001f)
        {
            return;
        }

        float multiplier =
            targetHeight /
            currentHeight;

        multiplier =
            Mathf.Clamp(
                multiplier,
                0.18f,
                4.5f);

        root.transform.localScale *=
            multiplier;
    }

    private static bool TryNormalizeHostileVisualEnvelope(
        GameObject root,
        float targetHeight,
        string resolvedCategory)
    {
        if (root == null ||
            targetHeight <= 0f ||
            !TryGetRenderableBounds(
                root,
                out Bounds bounds))
        {
            return false;
        }

        float currentHeight =
            bounds.size.y;

        if (currentHeight <= 0.001f ||
            float.IsNaN(currentHeight) ||
            float.IsInfinity(currentHeight))
        {
            return false;
        }

        bool humanoid =
            IsHumanoidVisualCategory(
                resolvedCategory);

        float horizontalAspect =
            Mathf.Max(
                bounds.size.x,
                bounds.size.z) /
            currentHeight;

        if (humanoid &&
            horizontalAspect > 2.25f)
        {
            // note: A humanoid category with a winged or giant horizontal silhouette is a semantic mismatch, not a scale-variation opportunity.
            return false;
        }

        float maximumWidthFactor;
        float maximumDepthFactor;

        if (humanoid)
        {
            maximumWidthFactor = 1.25f;
            maximumDepthFactor = 0.95f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Dragon,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 5f;
            maximumDepthFactor = 4f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.WormMonster,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 3.5f;
            maximumDepthFactor = 4f;
        }
        else if (string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Spider,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     resolvedCategory,
                     YQRuntimeCreatureAssetIndex.Beast,
                     StringComparison.OrdinalIgnoreCase))
        {
            maximumWidthFactor = 3f;
            maximumDepthFactor = 3f;
        }
        else
        {
            maximumWidthFactor = 2.2f;
            maximumDepthFactor = 2.2f;
        }

        float maximumWidth =
            targetHeight *
            maximumWidthFactor;

        float maximumDepth =
            targetHeight *
            maximumDepthFactor;

        float multiplier =
            targetHeight /
            currentHeight;

        if (bounds.size.x > 0.001f)
        {
            multiplier =
                Mathf.Min(
                    multiplier,
                    maximumWidth /
                    bounds.size.x);
        }

        if (bounds.size.z > 0.001f)
        {
            multiplier =
                Mathf.Min(
                    multiplier,
                    maximumDepth /
                    bounds.size.z);
        }

        if (float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier) ||
            multiplier <= 0f)
        {
            return false;
        }

        // note: Uniform envelope fitting preserves the authored creature proportions while constraining height, width, and depth to a gameplay-safe volume.
        root.transform.localScale *=
            Mathf.Clamp(
                multiplier,
                0.01f,
                4.5f);

        if (!TryGetRenderableBounds(
                root,
                out Bounds fittedBounds))
        {
            return false;
        }

        const float EnvelopeTolerance = 1.08f;

        return
            fittedBounds.size.y <=
                targetHeight *
                EnvelopeTolerance &&
            fittedBounds.size.x <=
                maximumWidth *
                EnvelopeTolerance &&
            fittedBounds.size.z <=
                maximumDepth *
                EnvelopeTolerance;
    }

    public static bool TryPlaceResidentOnReviewedSurface(GameObject root, Vector3 surfacePosition)
    {
        // note: Both initial creation and site reload use the same contact-to-pivot correction without resampling terrain underneath a floor.
        if (root == null || float.IsNaN(surfacePosition.sqrMagnitude) || float.IsInfinity(surfacePosition.sqrMagnitude))
            return false;
        YQInvestorEnemy enemy = root.GetComponent<YQInvestorEnemy>();
        if ((enemy != null && enemy.allowFlight) || YQTerrainSupportComposer.IsExplicitlySuspended(root))
            return true;

        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(root, out Bounds bounds, out float contactBottom))
        {
            // note: Retain the existing legacy visual-bounds fallback, but target the reviewed support height rather than the hidden terrain.
            if (!TryGetRenderableBounds(root, out bounds))
                return false;
            contactBottom = bounds.min.y;
        }
        float contactOffset = contactBottom - root.transform.position.y;
        Vector3 destination = surfacePosition - Vector3.up * (contactOffset + 0.005f);
        if (float.IsNaN(destination.sqrMagnitude) || float.IsInfinity(destination.sqrMagnitude))
            return false;

        CharacterController controller = root.GetComponent<CharacterController>();
        bool restoreController = controller != null && controller.enabled;
        try
        {
            // note: Preserve controller ownership while teleporting; an exception must not leave the resident's collision disabled.
            if (restoreController)
                controller.enabled = false;
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body != null)
                body.position = destination;
            else
                root.transform.position = destination;
        }
        finally
        {
            if (restoreController && controller != null)
                controller.enabled = true;
        }
        return true;
    }

    private static void GroundCharacterToTerrain(
        GameObject root,
        Terrain terrain,
        Vector3 expectedPosition)
    {
        if (root == null ||
            terrain == null)
        {
            return;
        }

        YQInvestorEnemy enemy =
            root.GetComponent<YQInvestorEnemy>();

        if ((enemy != null && enemy.allowFlight) ||
            YQTerrainSupportComposer.IsExplicitlySuspended(
                root))
        {
            // note: Only explicit flight/suspension contracts bypass terrain contact; display names alone never authorize floating placement.
            return;
        }

        if (YQGeneratedWorldTerrain.TryPlaceGroundedObject(
                root,
                terrain,
                YQGeneratedWorldPlacementCategory.Actor,
                0.005f,
                out _))
        {
            return;
        }

        float fallbackGround =
            YQGeneratedWorldTerrain.SampleWorldHeight(
                terrain,
                expectedPosition);

        if (!TryGetRenderableBounds(
                root,
                out Bounds fallbackBounds))
        {
            Vector3 position =
                root.transform.position;

            position.y =
                fallbackGround;

            root.transform.position =
                position;

            return;
        }

        // note: A renderer rejected by the strict structural filter still receives a conservative legacy-bounds fallback instead of remaining at an arbitrary imported elevation.
        Vector3 fallbackPosition =
            root.transform.position;
        fallbackPosition.y +=
            fallbackGround -
            fallbackBounds.min.y -
            0.005f;
        root.transform.position =
            fallbackPosition;
    }

    // ============================================================
    // SEMANTIC RUNTIME PREFAB RESOLUTION
    // ============================================================

    private static bool TryResolveSemanticPrefab(
        YQRuntimeWorldAssetRegistry registry,
        string seed,
        string[] requiredAnyTerms,
        string[] preferredTerms,
        string[] excludedTerms,
        bool requireCharacterLike,
        out YQRuntimeWorldAssetEntry result)
    {
        result =
            null;

        if (registry == null ||
            registry.Entries == null)
        {
            return false;
        }

        List<YQRuntimeWorldAssetEntry> best =
            new List<
                YQRuntimeWorldAssetEntry>();

        int bestScore =
            int.MinValue;

        for (int i = 0;
             i < registry.Entries.Count;
             i++)
        {
            YQRuntimeWorldAssetEntry entry =
                registry.Entries[i];

            if (entry == null ||
                entry.prefab == null)
            {
                continue;
            }

            if (requireCharacterLike &&
                !IsCharacterLikePrefab(
                    entry.prefab))
            {
                continue;
            }

            string semantic =
                BuildPrefabSemanticText(
                    entry);

            if (MatchesAnySemantic(
                    semantic,
                    excludedTerms))
            {
                continue;
            }

            int requiredMatches =
                CountSemanticMatches(
                    semantic,
                    requiredAnyTerms);

            if (requiredAnyTerms != null &&
                requiredAnyTerms.Length > 0 &&
                requiredMatches <= 0)
            {
                continue;
            }

            int preferredMatches =
                CountSemanticMatches(
                    semantic,
                    preferredTerms);

            int score =
                requiredMatches *
                    40 +
                preferredMatches *
                    12;

            if (entry.prefab.GetComponentInChildren<
                    Animator>(
                        true) != null)
            {
                score +=
                    8;
            }

            if (entry.prefab.GetComponentInChildren<
                    SkinnedMeshRenderer>(
                        true) != null)
            {
                score +=
                    12;
            }

            if (ContainsAnySemantic(
                    semantic,
                    "character",
                    "characters",
                    "creature",
                    "creatures",
                    "monster",
                    "monsters"))
            {
                score +=
                    5;
            }

            if (score >
                bestScore)
            {
                bestScore =
                    score;

                best.Clear();

                best.Add(
                    entry);
            }
            else if (score ==
                     bestScore)
            {
                best.Add(
                    entry);
            }
        }

        if (best.Count == 0)
            return false;

        int selected =
            (int)(
                StableHash32(
                    seed +
                    "|semantic_prefab|" +
                    bestScore) %
                (uint)best.Count);

        result =
            best[selected];

        return
            result != null &&
            result.prefab != null;
    }

    private static bool IsCharacterLikePrefab(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (prefab.GetComponentInChildren<
                SkinnedMeshRenderer>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animator>(
                    true) != null)
        {
            return true;
        }

        if (prefab.GetComponentInChildren<
                Animation>(
                    true) != null)
        {
            return true;
        }

        return false;
    }

    private static string BuildPrefabSemanticText(
        YQRuntimeWorldAssetEntry entry)
    {
        if (entry == null)
            return string.Empty;

        string source =
            SafeText(
                entry.assetPath,
                string.Empty) +
            " " +
            (entry.prefab != null
                ? entry.prefab.name
                : string.Empty);

        return
            NormalizeSemanticText(
                source);
    }

    /*
     * Converts:
     *
     * HumanMale_01
     * -> human male 01
     *
     * MushroomMonster
     * -> mushroom monster
     *
     * This avoids treating "female" as a match for "male".
     */
    private static string NormalizeSemanticText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder sb =
            new StringBuilder(
                value.Length *
                2);

        char previous =
            '\0';

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            bool letterOrDigit =
                char.IsLetterOrDigit(
                    c);

            if (!letterOrDigit)
            {
                sb.Append(' ');

                previous =
                    c;

                continue;
            }

            if (char.IsUpper(c) &&
                i > 0 &&
                (char.IsLower(previous) ||
                 char.IsDigit(previous)))
            {
                sb.Append(' ');
            }

            sb.Append(
                char.ToLowerInvariant(
                    c));

            previous =
                c;
        }

        string[] pieces =
            sb.ToString()
                .Split(
                    new[]
                    {
                        ' '
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries);

        return
            " " +
            string.Join(
                " ",
                pieces) +
            " ";
    }

    private static bool ContainsSemantic(
        string normalizedText,
        string term)
    {
        if (string.IsNullOrWhiteSpace(
                normalizedText) ||
            string.IsNullOrWhiteSpace(
                term))
        {
            return false;
        }

        string normalizedTerm =
            NormalizeSemanticText(
                term)
                .Trim();

        if (string.IsNullOrWhiteSpace(
                normalizedTerm))
        {
            return false;
        }

        if (normalizedText.Contains(
                " " +
                normalizedTerm +
                " "))
        {
            return true;
        }

        /*
         * Permit simple plural forms:
         *
         * dragon -> dragons
         * monster -> monsters
         */
        if (!normalizedTerm.Contains(" "))
        {
            if (normalizedText.Contains(
                    " " +
                    normalizedTerm +
                    "s "))
            {
                return true;
            }

            if (normalizedText.Contains(
                    " " +
                    normalizedTerm +
                    "es "))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnySemantic(
        string normalizedText,
        params string[] terms)
    {
        return
            MatchesAnySemantic(
                normalizedText,
                terms);
    }

    private static bool MatchesAnySemantic(
        string normalizedText,
        string[] terms)
    {
        if (terms == null)
            return false;

        for (int i = 0;
             i < terms.Length;
             i++)
        {
            if (ContainsSemantic(
                    normalizedText,
                    terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSemanticMatches(
        string normalizedText,
        string[] terms)
    {
        if (terms == null ||
            terms.Length == 0)
        {
            return 0;
        }

        int count =
            0;

        for (int i = 0;
             i < terms.Length;
             i++)
        {
            if (ContainsSemantic(
                    normalizedText,
                    terms[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static List<string>
        ExtractDistinctiveSemanticTerms(
            string value)
    {
        List<string> result =
            new List<string>();

        string normalized =
            NormalizeSemanticText(
                value);

        string[] parts =
            normalized
                .Split(
                    new[]
                    {
                        ' '
                    },
                    StringSplitOptions
                        .RemoveEmptyEntries);

        for (int i = 0;
             i < parts.Length;
             i++)
        {
            string part =
                parts[i];

            if (part.Length <
                3)
            {
                continue;
            }

            if (IsSemanticStopWord(
                    part))
            {
                continue;
            }

            AddUnique(
                result,
                part);
        }

        return result;
    }

    private static bool IsSemanticStopWord(
        string value)
    {
        switch (value)
        {
            case "the":
            case "and":
            case "with":
            case "from":
            case "into":
            case "this":
            case "that":
            case "generated":
            case "hostile":
            case "enemy":
            case "leader":
            case "chief":
            case "captain":
            case "named":
            case "creature":
            case "local":
            case "site":
            case "encampment":
            case "region":
                return true;
        }

        return false;
    }

    // ============================================================
    // ENEMY COMBAT
    // ============================================================

    private static void ConfigureEnemyCombat(
        YQInvestorEnemy enemy,
        GeneratedEncampmentRecord encampment,
        string displayName,
        string factionId,
        int tier,
        bool leader)
    {
        if (enemy == null)
            return;

        enemy.semanticRegionId =
            encampment.encampmentId;

        enemy.factionId =
            factionId;

        enemy.displayName =
            displayName;

        enemy.maxHealth =
            48f +
            tier *
                17f +
            (leader
                ? 75f
                : 0f);

        enemy.moveSpeed =
            Mathf.Clamp(
                3.0f +
                tier *
                    0.12f,
                3f,
                5.2f);

        enemy.aggroRange =
            16f +
            tier *
                1.2f;

        enemy.attackRange =
            1.75f;

        enemy.attackCooldown =
            Mathf.Max(
                0.72f,
                1.20f -
                tier *
                    0.035f);

        enemy.attackDamage =
            7 +
            tier *
                3 +
            (leader
                ? 8
                : 0);

        enemy.goldDrop =
            4 +
            tier *
                4 +
            (leader
                ? 20
                : 0);

        enemy.useWispVisual =
            false;

        enemy.rarity =
            leader
                ? "rare"
                : tier >= 5
                    ? "uncommon"
                    : "common";

        string family =
            SafeText(
                encampment.monsterFamily,
                string.Empty)
                .ToLowerInvariant();

        enemy.allowFlight =
            family.Contains("wisp") ||
            family.Contains("bat") ||
            family.Contains("harpy") ||
            family.Contains("wing") ||
            family.Contains("flying") ||
            family.Contains("dragon") ||
            family.Contains("wyvern");
    }

    private static string BuildGenericHostileLabel(
        GeneratedEncampmentRecord encampment,
        int index)
    {
        string family =
            SafeText(
                encampment.monsterFamily,
                "Hostile");

        string lower =
            family.ToLowerInvariant();

        if (lower.Contains("bandit") ||
            lower.Contains("raider") ||
            lower.Contains("brigand"))
        {
            switch (index % 3)
            {
                case 0:
                    return
                        family +
                        " Raider";

                case 1:
                    return
                        family +
                        " Scout";

                default:
                    return
                        family +
                        " Marauder";
            }
        }

        if (lower.Contains("cult"))
        {
            return
                index % 3 == 0
                    ? family +
                        " Acolyte"
                    : family +
                        " Zealot";
        }

        if (lower.Contains("undead") ||
            lower.Contains("skeleton") ||
            lower.Contains("dead"))
        {
            return
                index % 2 == 0
                    ? family +
                        " Sentinel"
                    : family +
                        " Risen";
        }

        if (lower.Contains("beast") ||
            lower.Contains("wolf") ||
            lower.Contains("spider") ||
            lower.Contains("brood"))
        {
            return
                index % 2 == 0
                    ? family +
                        " Stalker"
                    : family +
                        " Hunter";
        }

        return
            family +
            " " +
            (index % 2 == 0
                ? "Hunter"
                : "Striker");
    }

    private static string[] BuildHostileTags(
        GeneratedNpcPlanRecord npc,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region,
        bool leader)
    {
        List<string> tags =
            new List<string>();

        AddUnique(tags, "generated");
        AddUnique(tags, "enemy");
        AddUnique(tags, "hostile");

        AddUnique(
            tags,
            leader
                ? "boss"
                : "encampment");

        if (npc != null &&
            npc.tags != null)
        {
            for (int i = 0;
                 i < npc.tags.Count;
                 i++)
            {
                AddUnique(
                    tags,
                    NormalizeTag(
                        npc.tags[i]));
            }
        }

        AddUnique(
            tags,
            NormalizeTag(
                encampment.kind));

        AddUnique(
            tags,
            NormalizeTag(
                encampment.monsterFamily));

        AddUnique(
            tags,
            NormalizeTag(
                encampment.encampmentId));

        AddUnique(
            tags,
            NormalizeTag(
                region.regionId));

        return
            tags.ToArray();
    }

    // ============================================================
    // ENCAMPMENT REWARDS
    // ============================================================

    private static int BuildEncampmentRewards(
    Transform parent,
    Terrain terrain,
    GeneratedWorldPlanRecord plan,
    GeneratedEncampmentRecord encampment,
    GeneratedRegionAssetPaletteRecord palette,
    YQRuntimeWorldAssetRegistry registry,
    bool requireCompiledSite = false)
    {
        if (parent == null ||
            terrain == null ||
            plan == null ||
            encampment == null ||
            palette == null ||
            registry == null)
        {
            return 0;
        }

        int tier =
            Mathf.Clamp(
                encampment.threatTier,
                1,
                8);

        int desired =
            tier >= 6
                ? 2
                : 1;

        int spawned =
            0;

        string worldKey =
            StableHash32(
                plan.worldSeed)
                .ToString("x8");

        for (int i = 0;
             i < desired;
             i++)
        {
            string seed =
                plan.worldSeed +
                "|encampment_reward|" +
                encampment.encampmentId +
                "|" +
                i;

            GeneratedAssetReferenceRecord reference =
                YQWorldAssetCatalog
                    .PickAssetForSlot(
                        palette,
                        YQWorldAssetCatalog
                            .SlotLootContainer,
                        seed);

            if (reference == null && requireCompiledSite)
                throw new InvalidOperationException("Continuation encampment has no approved reward container: " + encampment.encampmentId + ":" + i);
            if (reference == null)
                continue;

            GameObject prefab =
                registry.ResolvePrefab(
                    reference.assetPath);

            if (prefab == null)
            {
                if (requireCompiledSite)
                    throw new InvalidOperationException("Continuation reward prefab is unavailable: " + reference.assetPath);
                Debug.LogWarning(
                    "[YQGeneratedWorldPopulation] " +
                    "Reward prefab could not be resolved: " +
                    reference.assetPath);

                continue;
            }

            GameObject chest =
                UnityEngine.Object.Instantiate(
                    prefab,
                    parent);

            chest.name =
                "Loot__" +
                SafeName(
                    encampment.encampmentId) +
                "__" +
                i +
                "__" +
                prefab.name;

            bool compiledRewardPosition =
                YQCompiledWorldSiteInstance.TryResolveWorldActorPosition(
                    encampment.encampmentId,
                    "reward loot cache",
                    seed,
                    i,
                    out Vector3 position);

            if (!compiledRewardPosition)
            {
                // note: Continuation rewards require a real loaded provider socket; no terrain scatter can satisfy that contract.
                if (requireCompiledSite)
                    throw new InvalidOperationException("No loaded provider reward surface for continuation camp " + encampment.encampmentId + ":" + i);
                Vector3 offset = ResolveCampOffset(
                    seed,
                    i == 0 ? 8f : 13f,
                    i == 0 ? 12f : 18f);
                position = parent.position + offset;
                position.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                    terrain,
                    position);
            }

            chest.transform.position =
                position;

            chest.transform.rotation =
                Quaternion.Euler(
                    0f,
                    Deterministic01(
                        seed +
                        "|yaw") *
                    360f,
                    0f);

            float scale =
                Mathf.Lerp(
                    Mathf.Max(
                        0.01f,
                        reference.scaleMin),
                    Mathf.Max(
                        reference.scaleMin,
                        reference.scaleMax),
                    Deterministic01(
                        seed +
                        "|scale"));

            // note: Deterministic variation multiplies the imported prefab's authored root scale instead of erasing its unit conversion.
            chest.transform.localScale *=
                scale;

            registry.ApplyMaterialOverrides(
                reference.assetPath,
                chest);

            YQRuntimeUrpMaterialRepair
                .RepairHierarchy(
                    chest);

            if (compiledRewardPosition)
                GroundObjectToWorldHeight(chest, position.y);
            else
                GroundObjectToTerrainBase(
                    chest,
                    terrain,
                    YQGeneratedWorldPlacementCategory.Prop);

            /*
             * Stable identity.
             *
             * Same save/world + same encampment + same reward slot
             * always resolves to the same persistent chest.
             */
            string persistentId =
                "encampment:" +
                worldKey +
                ":" +
                SafeName(
                    encampment.encampmentId) +
                ":reward:" +
                i;

            /*
             * Reward parameters are deterministic from world content.
             */
            int bonusGold =
                Mathf.FloorToInt(
                    Deterministic01(
                        seed +
                        "|gold") *
                    13f);

            int generatedGold =
                14 +
                tier *
                    7 +
                bonusGold;

            float lockChance =
                Mathf.Clamp01(
                    0.20f +
                    tier *
                        0.08f);

            bool generatedLocked =
                Deterministic01(
                    seed +
                    "|locked") <
                lockChance;

            float generatedDifficulty =
                Mathf.Clamp(
                    0.18f +
                    tier *
                        0.065f +
                    Deterministic01(
                        seed +
                        "|difficulty") *
                        0.06f,
                    0.15f,
                    0.84f);

            /*
             * Mimics remain uncommon.
             *
             * Higher-threat camps are somewhat more likely to hide one.
             */
            float mimicChance =
                tier >= 3
                    ? 0.025f +
                      tier *
                          0.0125f
                    : 0.015f;

            bool generatedMimic =
                Deterministic01(
                    seed +
                    "|mimic") <
                mimicChance;

            int rewardLevel =
                Mathf.Clamp(
                    tier +
                    1,
                    1,
                    12);

            string rewardName =
                i == 0
                    ? encampment.displayName +
                      " Cache"
                    : encampment.displayName +
                      " Strongbox";

            YQLockpickableLoot loot =
                chest.GetComponent<
                    YQLockpickableLoot>();

            if (loot == null)
            {
                loot =
                    chest.AddComponent<
                        YQLockpickableLoot>();
            }

            loot.ConfigureGeneratedLoot(
                persistentId,
                encampment.regionId,
                rewardName,
                generatedGold,
                generatedLocked,
                generatedDifficulty,
                generatedMimic,
                rewardLevel);

            EnsureEnvironmentCollider(
                chest,
                false);

            Debug.Log(
                "[YQGeneratedWorldPopulation] " +
                "Generated encampment loot: " +
                rewardName +
                " | id=" +
                persistentId +
                " | gold=" +
                generatedGold +
                " | locked=" +
                generatedLocked +
                " | mimic=" +
                generatedMimic);

            spawned++;
        }

        return spawned;
    }

    // ============================================================
    // ENCAMPMENT SITE ASSETS
    // ============================================================

    private static IEnumerator BuildEncampmentSiteAssetsRoutine(
        Transform parent,
        Terrain terrain,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        // note: Camps need enough modular pieces to read as authored places, not a sparse prop triangle.
        int count =
            encampment.threatTier >= 7
                ? 7
                : encampment.threatTier >= 5
                    ? 6
                    : 5;

        HashSet<string> usedAssetPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < count;
             i++)
        {
            string seed =
                EncampmentSeed(
                    encampment) +
                "|site|" +
                i;

            GeneratedAssetReferenceRecord reference =
                null;

            // note: Try several deterministic rolls before allowing a duplicate camp module.
            for (int attempt = 0;
                 attempt < 8;
                 attempt++)
            {
                GeneratedAssetReferenceRecord candidate =
                    YQWorldAssetCatalog
                        .PickAssetForSlot(
                            palette,
                            YQWorldAssetCatalog
                                .SlotEnemySite,
                            seed +
                            "|pick|" +
                            attempt);

                if (candidate == null)
                    continue;

                if (string.IsNullOrWhiteSpace(
                        candidate.assetPath) ||
                    usedAssetPaths.Add(
                        candidate.assetPath))
                {
                    reference =
                        candidate;
                    break;
                }
            }

            if (reference == null)
            {
                reference =
                    YQWorldAssetCatalog
                        .PickAssetForSlot(
                            palette,
                            YQWorldAssetCatalog
                                .SlotExteriorDeco,
                            seed);
            }

            if (reference == null)
                continue;

            GameObject prefab =
                registry.ResolvePrefab(
                    reference.assetPath);

            if (prefab == null)
                continue;

            if (!string.IsNullOrWhiteSpace(
                    reference.assetPath))
            {
                usedAssetPaths.Add(
                    reference.assetPath);
            }

            List<Collider> temporarilyDisabledColliders =
                DisableMalformedPrefabPrimitiveColliders(
                    prefab);

            GameObject instance =
                null;

            try
            {
                // note: Imported mirrored primitive colliders must be disabled before cloning; Unity warns while Instantiate is still copying them.
                instance =
                    UnityEngine.Object.Instantiate(
                        prefab,
                        parent);
            }
            finally
            {
                RestorePrefabColliders(
                    temporarilyDisabledColliders);
            }

            if (instance == null)
                continue;

            instance.name =
                "CampSite_" +
                i +
                "__" +
                prefab.name;

            float angle =
                Deterministic01(
                    seed +
                    "|angle") *
                Mathf.PI *
                2f;

            float radius =
                i == 0
                    ? 0f
                    : Mathf.Lerp(
                        5f,
                        11f,
                        Deterministic01(
                            seed +
                            "|radius"));

            Vector3 worldPosition =
                parent.position +
                new Vector3(
                    Mathf.Cos(angle) *
                        radius,
                    0f,
                    Mathf.Sin(angle) *
                        radius);

            worldPosition.y =
                YQGeneratedWorldTerrain
                    .SampleWorldHeight(
                        terrain,
                        worldPosition);

            instance.transform.position =
                worldPosition;

            instance.transform.rotation =
                Quaternion.Euler(
                    0f,
                    Deterministic01(
                        seed +
                        "|yaw") *
                    360f,
                    0f);

            float scale =
                Mathf.Lerp(
                    Mathf.Max(
                        0.01f,
                        reference.scaleMin),
                    Mathf.Max(
                        reference.scaleMin,
                        reference.scaleMax),
                    Deterministic01(
                        seed +
                        "|scale"));

            // note: Preserve marketplace-authored root scale while adding deterministic campsite variation.
            instance.transform.localScale *=
                scale;

            registry.ApplyMaterialOverrides(
                reference.assetPath,
                instance);

            YQRuntimeUrpMaterialRepair
                .RepairHierarchy(
                    instance);

            /*
             * IMPORTANT:
             *
             * The old implementation raised the entire object to the
             * HIGHEST terrain sample under its footprint.
             *
             * Large cave/ruin pieces could therefore float.
             *
             * Base grounding uses median terrain contact and permits a
             * small amount of intentional terrain penetration.
             */
            bool structural =
                ReferenceHasAnyTag(
                    reference,
                    "cave",
                    "ruin",
                    "structure",
                    "building",
                    "underground",
                    "mountain",
                    "rock");

            // note: Campsite modules declare structural intent before placement so ruins stay upright while loose props and rocks can settle naturally.
            GroundObjectToTerrainBase(
                instance,
                terrain,
                structural
                    ? YQGeneratedWorldPlacementCategory.Structure
                    : YQGeneratedWorldPlacementCategory.Prop);

            EnsureEnvironmentCollider(
                instance,
                structural);

            // note: Fallback camps may use several imported structures, but only one hierarchy is materialized and repaired per frame.
            yield return null;
        }
    }

    private static bool ReferenceHasAnyTag(
        GeneratedAssetReferenceRecord reference,
        params string[] tags)
    {
        if (reference == null ||
            tags == null)
        {
            return false;
        }

        string semantic =
            NormalizeSemanticText(
                SafeText(
                    reference.assetPath,
                    string.Empty) +
                " " +
                SafeText(
                    reference.slotTag,
                    string.Empty));

        if (reference.styleTags != null)
        {
            semantic +=
                NormalizeSemanticText(
                    string.Join(
                        " ",
                        reference.styleTags));
        }

        if (reference.subTags != null)
        {
            semantic +=
                NormalizeSemanticText(
                    string.Join(
                        " ",
                        reference.subTags));
        }

        return
            MatchesAnySemantic(
                semantic,
                tags);
    }

    private static void EnsureEnvironmentCollider(
        GameObject root,
        bool forceStructuralCollision)
    {
        if (root == null)
            return;

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null)
                continue;

            if (!body.isKinematic)
            {
                // note: Clear only dynamic decorative bodies before freezing them as static scenery.
                body.linearVelocity =
                    Vector3.zero;

                body.angularVelocity =
                    Vector3.zero;
            }

            body.useGravity =
                false;

            body.isKinematic =
                true;
        }

        Collider[] existing =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        bool hasSolidCollider =
            false;

        for (int i = 0;
             i < existing.Length;
             i++)
        {
            Collider collider =
                existing[i];

            if (collider != null &&
                !collider.isTrigger)
            {
                hasSolidCollider =
                    true;

                break;
            }
        }

        if (hasSolidCollider ||
            !forceStructuralCollision)
        {
            return;
        }

        MeshFilter[] filters =
            root.GetComponentsInChildren<
                MeshFilter>(
                    true);

        for (int i = 0;
             i < filters.Length;
             i++)
        {
            MeshFilter filter =
                filters[i];

            if (filter == null ||
                filter.sharedMesh == null)
            {
                continue;
            }

            GameObject meshObject =
                filter.gameObject;

            if (meshObject.GetComponent<
                    MeshCollider>() != null)
            {
                continue;
            }

            MeshCollider collider =
                meshObject.AddComponent<
                    MeshCollider>();

            collider.sharedMesh =
                filter.sharedMesh;

            collider.convex =
                false;

            collider.isTrigger =
                false;
        }
    }

    private static void BuildEncampmentRegionVolume(
        Transform parent,
        GeneratedEncampmentRecord encampment,
        GeneratedRegionRecord region)
    {
        GameObject volume =
            new GameObject(
                "EncampmentRegionVolume");

        volume.transform.SetParent(
            parent,
            false);

        volume.transform.localPosition =
            new Vector3(
                0f,
                3f,
                0f);

        BoxCollider collider =
            volume.AddComponent<
                BoxCollider>();

        collider.isTrigger =
            true;

        collider.size =
            new Vector3(
                42f,
                10f,
                42f);

        RegionVolume regionVolume =
            volume.AddComponent<
                RegionVolume>();

        regionVolume.regionId =
            region.regionId;

        regionVolume.regionName =
            encampment.displayName;

        regionVolume.tags =
            new List<string>
            {
                "generated",
                "encampment",
                "hostile",
                NormalizeTag(
                    encampment.kind),
                NormalizeTag(
                    encampment.monsterFamily),
                NormalizeTag(
                    encampment.encampmentId)
            };
    }

    // ============================================================
    // GENERATED BUILDING DOORS
    // ============================================================

    public static int ConfigureBuildingDoors(
        GameObject building,
        GeneratedSettlementRecord settlement)
    {
        if (building == null ||
            settlement == null)
        {
            return 0;
        }

        Transform[] transforms =
            building.GetComponentsInChildren<
                Transform>(
                    true);

        int configured =
            0;

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            Transform candidate =
                transforms[i];

            if (candidate == null ||
                candidate ==
                    building.transform)
            {
                continue;
            }

            if (!LooksLikeMovableDoor(
                    candidate,
                    building.transform))
            {
                continue;
            }

            YQLockpickableDoor existing =
                candidate.GetComponent<
                    YQLockpickableDoor>();

            if (existing != null)
                continue;

            string seed =
                SettlementSeed(
                    settlement) +
                "|door|" +
                GetTransformPath(
                    building.transform,
                    candidate);

            YQLockpickableDoor door =
                candidate.gameObject
                    .AddComponent<
                        YQLockpickableDoor>();

            door.displayName =
                settlement.displayName +
                " Door";

            door.regionId =
                settlement.regionId;

            float lockChance =
                ResolveLockChance(
                    settlement.securityProfile);

            door.locked =
                Deterministic01(
                    seed +
                    "|locked") <
                lockChance;

            door.lockDifficulty =
                Mathf.Lerp(
                    0.18f,
                    0.72f,
                    Deterministic01(
                        seed +
                        "|difficulty"));

            float direction =
                Deterministic01(
                    seed +
                    "|swing") <
                0.5f
                    ? -86f
                    : 86f;

            door.openEuler =
                new Vector3(
                    0f,
                    direction,
                    0f);

            configured++;
        }

        if (configured > 0)
        {
            Debug.Log(
                "[YQGeneratedWorldPopulation] " +
                "Configured " +
                configured +
                " interactive door(s) in " +
                building.name);
        }

        return configured;
    }

    private static bool LooksLikeMovableDoor(
        Transform transform,
        Transform buildingRoot)
    {
        if (transform == null)
            return false;

        string name =
            transform.name
                .ToLowerInvariant();

        if (!name.Contains("door"))
            return false;

        if (name.Contains("wall"))
            return false;

        // note: An imported door's LOD children also contain "door"; only their first logical door ancestor may own gameplay and rotate.
        Transform ancestor = transform.parent;
        while (ancestor != null && ancestor != buildingRoot)
        {
            if (ancestor.name.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            ancestor = ancestor.parent;
        }

        if (name.Contains("frame") ||
            name.Contains("doorway") ||
            name.Contains("trim") ||
            name.Contains("handle") ||
            name.Contains("knob") ||
            name.Contains("hinge") ||
            name.Contains("threshold") ||
            name.Contains("arch"))
        {
            return false;
        }

        Renderer renderer =
            transform.GetComponent<
                Renderer>();

        MeshFilter mesh =
            transform.GetComponent<
                MeshFilter>();

        if (renderer != null ||
            mesh != null)
        {
            return true;
        }

        Renderer childRenderer =
            transform.GetComponentInChildren<
                Renderer>(
                    true);

        return
            childRenderer != null;
    }

    private static float ResolveLockChance(
        string securityProfile)
    {
        string text =
            SafeText(
                securityProfile,
                string.Empty)
                .ToLowerInvariant();

        if (text.Contains("open gate") ||
            text.Contains("welcoming"))
        {
            return 0.06f;
        }

        if (text.Contains("guild"))
            return 0.50f;

        if (text.Contains("hidden patrol"))
            return 0.48f;

        if (text.Contains("militia"))
            return 0.38f;

        if (text.Contains("warden"))
            return 0.42f;

        if (text.Contains("watch"))
            return 0.30f;

        if (text.Contains("fort") ||
            text.Contains("military"))
        {
            return 0.55f;
        }

        return 0.24f;
    }

    private static string GetTransformPath(
        Transform root,
        Transform target)
    {
        if (target == null)
            return string.Empty;

        if (root == target)
            return string.Empty;

        List<string> parts =
            new List<string>();

        Transform current =
            target;

        while (current != null &&
               current != root)
        {
            parts.Add(
                current.name);

            current =
                current.parent;
        }

        parts.Reverse();

        return
            string.Join(
                "/",
                parts);
    }

    // ============================================================
    // RUNTIME NPC STATE
    // ============================================================

    private static bool ShouldMaterializeNpc(
        WorldState world,
        string npcId,
        bool repairCollections = true)
    {
        if (world == null ||
            string.IsNullOrWhiteSpace(
                npcId))
        {
            return true;
        }

        // note: A continuation preflight reads persisted status without repairing or rewriting accepted plan collections.
        if (repairCollections)
            world.EnsureCollections();
        if (world.npcs == null)
            return true;

        for (int i = 0;
             i < world.npcs.Count;
             i++)
        {
            WorldState.NpcRecord record =
                world.npcs[i];

            if (record == null ||
                !string.Equals(
                    record.npcId,
                    npcId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string status =
                SafeText(
                    record.status,
                    string.Empty)
                    .ToLowerInvariant();

            if (status == "dead" ||
                status == "defeated" ||
                status == "removed" ||
                status == "missing" ||
                status == "gone")
            {
                return false;
            }

            return true;
        }

        return true;
    }

    // ============================================================
    // FALLBACK VISUAL
    // ============================================================

    private static void ApplyNpcVisualColor(
        GameObject npc,
        GeneratedNpcPlanRecord record,
        string seed)
    {
        if (npc == null)
            return;

        Renderer renderer =
            npc.GetComponent<
                Renderer>();

        if (renderer == null)
            return;

        string role =
            record != null
                ? SafeText(
                    record.role,
                    string.Empty)
                    .ToLowerInvariant()
                : string.Empty;

        Color color;

        if (record != null &&
            record.guard)
        {
            color =
                new Color(
                    0.28f,
                    0.42f,
                    0.58f,
                    1f);
        }
        else if (record != null &&
                 record.merchant)
        {
            color =
                new Color(
                    0.68f,
                    0.52f,
                    0.20f,
                    1f);
        }
        else if (role.Contains("smith") ||
                 role.Contains("forge"))
        {
            color =
                new Color(
                    0.38f,
                    0.34f,
                    0.31f,
                    1f);
        }
        else if (role.Contains("heal") ||
                 role.Contains("herb") ||
                 role.Contains("apothec"))
        {
            color =
                new Color(
                    0.27f,
                    0.52f,
                    0.31f,
                    1f);
        }
        else
        {
            float blend =
                Deterministic01(
                    seed +
                    "|color");

            color =
                Color.Lerp(
                    new Color(
                        0.46f,
                        0.38f,
                        0.27f,
                        1f),
                    new Color(
                        0.29f,
                        0.45f,
                        0.49f,
                        1f),
                    blend);
        }

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        renderer.GetPropertyBlock(
            block);

        block.SetColor(
            "_BaseColor",
            color);

        block.SetColor(
            "_Color",
            color);

        renderer.SetPropertyBlock(
            block);
    }

    private static float ResolveBodyScale(
        string ageBand,
        string seed)
    {
        string age =
            SafeText(
                ageBand,
                "adult")
                .ToLowerInvariant();

        float baseScale =
            1f;

        if (age.Contains("elder"))
            baseScale = 0.95f;
        else if (age.Contains("young"))
            baseScale = 0.96f;

        return
            baseScale *
            Mathf.Lerp(
                0.96f,
                1.05f,
                Deterministic01(
                    seed +
                    "|height"));
    }

    // ============================================================
    // GROUNDING
    // ============================================================

    private static void GroundObjectToWorldHeight(
        GameObject instance,
        float contactHeight)
    {
        if (instance == null ||
            YQTerrainSupportComposer.IsExplicitlySuspended(instance) ||
            !YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                instance,
                out Bounds bounds,
                out float structuralBottom))
        {
            return;
        }

        // note: Compiled hostile-site rewards ground against the reviewed authored floor selected by the semantic anchor, not the terrain hidden beneath it.
        float penetration = Mathf.Clamp(
            bounds.size.y * 0.025f,
            0.02f,
            0.20f);
        Vector3 position = instance.transform.position;
        position.y += contactHeight - structuralBottom - penetration;
        instance.transform.position = position;
    }

    private static void GroundObjectToTerrainBase(
        GameObject instance,
        Terrain terrain,
        YQGeneratedWorldPlacementCategory category =
            YQGeneratedWorldPlacementCategory.Automatic)
    {
        if (instance == null ||
            terrain == null)
        {
            return;
        }

        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                instance,
                out Bounds bounds,
                out _))
        {
            return;
        }

        /*
         * Slightly bury large objects into the terrain rather than
         * risking a visible floating seam.
         */
        float penetration =
            Mathf.Clamp(
                bounds.size.y *
                0.025f,
                0.02f,
                0.35f);

        // note: Rewards, caves, and campsite props use the shared filtered support bottom and upper-middle terrain footprint instead of raw renderer corners.
        YQGeneratedWorldTerrain.TryPlaceGroundedObject(
            instance,
            terrain,
            category,
            penetration,
            out _);
    }

    private static bool TryGetRenderableBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds =
            new Bounds();

        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<
                Renderer>(
                    true);

        bool initialized =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null ||
                renderer is
                    ParticleSystemRenderer)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    renderer.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        if (initialized)
            return true;

        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider == null ||
                collider.isTrigger)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    collider.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    collider.bounds);
            }
        }

        return initialized;
    }

    // ============================================================
    // PLAN LOOKUPS
    // ============================================================

    private static GeneratedRegionRecord FindRegion(
        GeneratedWorldPlanRecord plan,
        string regionId)
    {
        if (plan == null ||
            plan.regions == null)
        {
            return null;
        }

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region != null &&
                string.Equals(
                    region.regionId,
                    regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return region;
            }
        }

        return null;
    }

    private static GeneratedRegionAssetPaletteRecord
        FindPalette(
            GeneratedWorldPlanRecord plan,
            GeneratedRegionRecord region)
    {
        if (plan == null ||
            region == null ||
            plan.assetPalettes == null)
        {
            return null;
        }

        for (int i = 0;
             i < plan.assetPalettes.Count;
             i++)
        {
            GeneratedRegionAssetPaletteRecord palette =
                plan.assetPalettes[i];

            if (palette == null)
                continue;

            if (!string.IsNullOrWhiteSpace(
                    region.assetPaletteId) &&
                string.Equals(
                    palette.paletteId,
                    region.assetPaletteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }

            if (string.Equals(
                    palette.regionId,
                    region.regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }
        }

        return null;
    }

    // ============================================================
    // DETERMINISM
    // ============================================================

    private static string SettlementSeed(
        GeneratedSettlementRecord settlement)
    {
        if (settlement == null)
            return "settlement";

        if (!string.IsNullOrWhiteSpace(
                settlement.deterministicSeed))
        {
            return
                settlement.deterministicSeed;
        }

        return
            settlement.settlementId ??
            "settlement";
    }

    private static string EncampmentSeed(
        GeneratedEncampmentRecord encampment)
    {
        if (encampment == null)
            return "encampment";

        if (!string.IsNullOrWhiteSpace(
                encampment.deterministicSeed))
        {
            return
                encampment.deterministicSeed;
        }

        return
            encampment.encampmentId ??
            "encampment";
    }

    private static float Deterministic01(
        string seed)
    {
        return
            (StableHash32(seed) &
                0x00FFFFFFu) /
            16777215f;
    }

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    // ============================================================
    // STRINGS
    // ============================================================

    private static void AddUnique(
        List<string> values,
        string value)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        for (int i = 0;
             i < values.Count;
             i++)
        {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(
            value);
    }

    private static string NormalizeTag(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        char[] chars =
            value
                .Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]))
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(chars)
                .Trim('_');
    }

    private static string SafeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "Generated";
        }

        char[] chars =
            value
                .Trim()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            if (!char.IsLetterOrDigit(
                    chars[i]) &&
                chars[i] != '_' &&
                chars[i] != '-')
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars);
    }

    private static string SafeText(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? fallback
                : value.Trim();
    }

    private static List<Collider> DisableMalformedPrefabPrimitiveColliders(
        GameObject prefab)
    {
        List<Collider> disabled =
            new List<Collider>();

        if (prefab == null)
            return disabled;

        Collider[] colliders =
            prefab.GetComponentsInChildren<Collider>(
                true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null ||
                !collider.enabled ||
                !(collider is BoxCollider ||
                  collider is SphereCollider ||
                  collider is CapsuleCollider))
            {
                continue;
            }

            bool negativeBoxSize =
                collider is BoxCollider box &&
                HasNegativeBoxColliderSize(
                    box);

            bool mirroredHierarchy =
                HasMirroredScaleInHierarchy(
                    collider.transform,
                    prefab.transform);

            if (!negativeBoxSize &&
                !mirroredHierarchy)
            {
                continue;
            }

            // note: Structural mesh collision is rebuilt on the clone, so imported negative-size or mirrored primitive colliders are safely omitted without editing vendor assets.
            collider.enabled = false;
            disabled.Add(collider);
        }

        return disabled;
    }

    private static bool HasNegativeBoxColliderSize(
        BoxCollider collider)
    {
        if (collider == null)
            return false;

        Vector3 size =
            collider.size;

        return
            size.x < 0f ||
            size.y < 0f ||
            size.z < 0f;
    }

    private static bool HasMirroredScaleInHierarchy(
        Transform child,
        Transform root)
    {
        Transform current =
            child;

        while (current != null)
        {
            Vector3 localScale =
                current.localScale;

            // note: Check each imported transform directly because two mirrored ancestors can multiply positive while Unity still rejects the primitive collider.
            if (localScale.x < 0f ||
                localScale.y < 0f ||
                localScale.z < 0f)
            {
                return true;
            }

            if (current == root)
                break;

            current = current.parent;
        }

        return false;
    }

    private static void RestorePrefabColliders(
        List<Collider> colliders)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }
    }

    // ============================================================
    // CLEAN REBUILD
    // ============================================================

    private static void DestroyExistingPopulationRoot(
        Transform parent,
        string objectName)
    {
        if (parent == null)
            return;

        Transform existing =
            parent.Find(
                objectName);

        if (existing == null)
            return;

        existing.gameObject.SetActive(
            false);

        UnityEngine.Object.Destroy(
            existing.gameObject);
    }
}
