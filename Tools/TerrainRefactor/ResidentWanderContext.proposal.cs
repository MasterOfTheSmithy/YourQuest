    private static readonly object ResidentWanderSurfaceIssuer = new object();

    // note: This transient receipt borrows this provider's existing floor selector; it owns no actor, navigation or saved state.
    internal sealed class ResidentWanderSurfaceContext
    {
        private readonly YQCompiledWorldSiteInstance owner;
        private readonly EntityInfo entity;
        private readonly ResidentPositionBinding binding;
        private readonly Transform content, parent;
        private readonly WorldState world;
        private readonly WorldStateManager worldOwner;
        private readonly GeneratedWorldPlanRecord plan;
        private readonly int siteEpoch, requestEpoch;
        private readonly string npcId, siteId, kitId, compositionSeed;

        private ResidentWanderSurfaceContext(YQCompiledWorldSiteInstance owner, EntityInfo entity, ResidentPositionBinding binding, Transform content)
        {
            this.owner = owner; this.entity = entity; this.binding = binding; this.content = content;
            parent = entity.transform.parent; worldOwner = WorldStateManager.Instance; world = worldOwner?.State; plan = world?.generatedWorldPlan;
            siteEpoch = owner.streamExecutionEpoch; requestEpoch = YQServiceLifecycle.RequestEpoch;
            npcId = entity.entityId; siteId = owner.settlementId; kitId = owner.expectedKitId; compositionSeed = owner.semanticSliceSeed;
        }

        internal static ResidentWanderSurfaceContext Capture(YQCompiledWorldSiteInstance owner, EntityInfo entity,
            Transform content, object issuer)
        {
            if (!ReferenceEquals(issuer, ResidentWanderSurfaceIssuer)) return null;
            foreach (var binding in owner.residentPositionBindings)
                if (binding != null && binding.entity == entity)
                    return new ResidentWanderSurfaceContext(owner, entity, binding, content);
            return null;
        }

        private bool IsActorWorldCurrent()
        {
            return owner != null && entity != null && entity.enabled && parent != null && world != null &&
                ReferenceEquals(WorldStateManager.Instance, worldOwner) && ReferenceEquals(worldOwner.State, world) && ReferenceEquals(world.generatedWorldPlan, plan) &&
                YQServiceLifecycle.IsCurrent(requestEpoch) &&
                entity.transform.parent == parent && entity.entityId == npcId && binding.entity == entity &&
                owner.residentPositionBindings.Contains(binding);
        }

        private bool IsCurrent()
        {
            return IsActorWorldCurrent() && content != null && owner.streamExecutionEpoch == siteEpoch &&
                owner.transform.Find("CompiledSiteContent") == content && content.gameObject.activeInHierarchy &&
                TryGetContinuationGeometryContext(siteId, kitId, compositionSeed, out var current) && current == owner;
        }

        internal bool CanRenewFrom(YQCompiledWorldSiteInstance provider)
        {
            // note: Only this same published owner may renew retained actors after a geometry reload; changed profile/world/identity/registration never transfers the receipt.
            return ReferenceEquals(provider, owner) && IsActorWorldCurrent() && owner.IsLoaded &&
                TryGetContinuationGeometryContext(siteId, kitId, compositionSeed, out var current) && current == owner;
        }

        internal bool TryProjectInitial(Vector3 candidate, out Vector3 floor)
        {
            // note: A freshly bound inactive staged cast may query complete geometry without falsely granting public population readiness.
            floor = candidate;
            return IsCurrent() && (owner.IsLoaded || owner.continuationPopulationInFlight && !entity.gameObject.activeInHierarchy) &&
                owner.TryProjectToSurface(candidate, out floor, entity.transform);
        }

        internal bool TryProject(Vector3 candidate, out Vector3 floor)
        {
            floor = candidate;
            return IsCurrent() && owner.IsLoaded && entity.gameObject.activeInHierarchy &&
                owner.TryProjectToSurface(candidate, out floor, entity.transform);
        }
    }

    internal static bool TryCaptureResidentWanderSurface(string locationId, EntityInfo entity, out ResidentWanderSurfaceContext context)
    {
        context = null;
        if (entity == null || string.IsNullOrWhiteSpace(entity.entityId) ||
            !Instances.TryGetValue(locationId ?? string.Empty, out var owner) || owner == null ||
            !TryGetContinuationGeometryContext(locationId, owner.expectedKitId, owner.semanticSliceSeed, out var current) || current != owner)
            return false;
        Transform content = owner.transform.Find("CompiledSiteContent");
        if (content == null || !content.gameObject.activeInHierarchy || WorldStateManager.Instance?.State?.generatedWorldPlan == null)
            return false;
        context = ResidentWanderSurfaceContext.Capture(owner, entity, content, ResidentWanderSurfaceIssuer);
        return context != null;
    }

    private IEnumerator RenewRegisteredResidentWanderSurfacesRoutine()
    {
        // note: Legacy reviewed residents can outlive the provider's content child. Renew only their existing movement contract after the new geometry is published, without creating actors or changing identities/seeds.
        int epoch = streamExecutionEpoch;
        WorldState world = WorldStateManager.Instance?.State;
        int renewed = 0;
        for (int index = 0; index < residentPositionBindings.Count; index++)
        {
            if (streamExecutionEpoch != epoch || !ReferenceEquals(WorldStateManager.Instance?.State, world) || !IsLoaded)
                throw new InvalidOperationException("Reviewed resident movement renewal lost its published owner.");
            EntityInfo entity = residentPositionBindings[index]?.entity;
            var wander = entity != null ? entity.GetComponent<YQGeneratedNpcWander>() : null;
            if (wander == null || !wander.UsesReviewedSurface) continue;
            wander.RenewReviewedSurface(this, settlementId, entity);
            if (++renewed % ResidentRelocationLimitPerFrame == 0) yield return null;
        }
    }

