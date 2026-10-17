#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class YQReviewedNpcFloorNativeCapture
{
    private const string Menu = "YourQuest/Terrain Refactor/Capture Nearby Reviewed NPC Floors (Read Only)";
    private const string Output = "outputs/TerrainRefactor_20261014_StreamingRetest/NpcFloors/native";
    private const int ResidentLimit = 16, ProviderLimit = 128, BindingLimitPerProvider = 64;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Serializable] private sealed class Capture
    {
        public string evidence = "NATIVE_READ_ONLY_CANONICAL_RESIDENT_BINDING_AND_FLOOR_QUERY; not NPC traversal/animation/placement PASS";
        public string limitation = "Nearest 16 registered active residents among at most 128 registry providers and 64 bindings per provider; omitted counts are explicit. Canonical posed contact getter may bake/dispose a transient Mesh; no actor transforms, controller state, scene/profile/import changes. Floor query uses existing provider filters and ignores this actor's standing-clearance body. Collider rays only verify provenance at the already accepted canonical point, never discover a substitute floor. Coplanar matches are reported without asserting which collider won the private selector. Property/schema reflection reads existing owners only.";
        public string utc, profileId, worldId, worldSeed, spatialContentHash;
        public int frame, registeredProviders, examinedProviders, omittedProviders, examinedBindings, omittedBindings, candidates;
        public float captureSeconds;
        public Vector3 playerPosition, renderOrigin;
        public List<ResidentRecord> residents = new List<ResidentRecord>();
    }
    [Serializable] private sealed class ResidentRecord
    {
        public string npcId, savedName, savedStatus, savedLocationId, actorHierarchy, siteId, roleIntent, seed;
        public string providerHierarchy, providerKitId, providerCompositionSeed, missingReason, contactReason, floorCellHierarchy;
        public int actorInstanceId, providerInstanceId, residentIndex, floorMatches;
        public bool active, canonicalNpcPresent, currentLoadedProvider, hasPosedFeet, humanoidPose, canonicalFloorProjected, floorResolved, floorNormalAmbiguous;
        public float rootY, feetY, posedFeetOffset, floorY, feetMinusFloor, placementEmbedResidual;
        public Vector3 position, logicalPosition, floorPoint, floorNormal;
        public Bounds posedBodyBounds;
        public List<FloorRecord> floorOwners = new List<FloorRecord>();
        public List<CountRecord> floorQuery = new List<CountRecord>();
    }
    [Serializable] private sealed class FloorRecord
    {
        public string hierarchy, type, cellHierarchy, cellDatumReason;
        public int instanceId, triangleIndex;
        public bool enabled, active, sameProvider, reviewedCellDatumFound;
        public Vector3 point, normal, reviewedCellDatumLocal, reviewedCellDatumWorld;
    }
    [Serializable] private sealed class CountRecord { public string name; public int count; }
    private sealed class Candidate
    {
        public EntityInfo entity;
        public YQCompiledWorldSiteInstance provider;
        public object binding;
        public string npcId, siteId;
        public float distance;
    }

    [MenuItem(Menu)]
    public static void CaptureNearby()
    {
        // note: The menu performs one bounded read/write-to-new-output operation, installing no callbacks, test lease, movement, repair, or save.
        if (!TryGetContext(out var player, out string reason) || !TryGetQueryContract(out var registry, out var query, out var diagnosticType, out var hitBuffer, out reason))
        { Debug.LogWarning("[YQNpcFloors] Capture refused: " + reason); return; }
        var profiles = YQProfileSaveSystem.Instance;
        var world = WorldStateManager.Instance.State;
        var state = PlayerStateManager.Instance.state;
        string profileId = profiles.ActiveProfileId;
        float started = Time.realtimeSinceStartup;
        var capture = new Capture
        {
            utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount, profileId = profileId,
            worldId = world.worldIdentity.worldId, worldSeed = Convert.ToString(world.generatedWorldPlan?.worldSeed),
            spatialContentHash = world.generatedWorldPlan?.spatialPlanV2?.contentHash,
            playerPosition = player.transform.position, renderOrigin = state.renderOrigin, registeredProviders = registry.Count
        };
        var nearest = new List<Candidate>(ResidentLimit + 1);
        foreach (DictionaryEntry entry in registry)
        {
            if (capture.examinedProviders >= ProviderLimit) break;
            capture.examinedProviders++;
            var provider = entry.Value as YQCompiledWorldSiteInstance;
            if (provider == null) continue;
            string siteId = Convert.ToString(entry.Key);
            var bindings = ReadField(provider, "residentPositionBindings") as IList;
            if (bindings == null) continue;
            capture.omittedBindings += Mathf.Max(0, bindings.Count - BindingLimitPerProvider);
            for (int index = 0; index < Mathf.Min(bindings.Count, BindingLimitPerProvider); index++)
            {
                capture.examinedBindings++;
                object binding = bindings[index];
                var entity = ReadField(binding, "entity") as EntityInfo;
                if (entity == null || !entity.gameObject.activeInHierarchy || !entity.gameObject.scene.IsValid() || !entity.gameObject.scene.isLoaded) continue;
                string npcId = entity.entityId;
                capture.candidates++;
                var candidate = new Candidate { entity = entity, provider = provider, binding = binding, npcId = npcId, siteId = siteId,
                    distance = (entity.transform.position - player.transform.position).sqrMagnitude };
                int at = nearest.FindIndex(other => candidate.distance < other.distance ||
                    candidate.distance == other.distance && string.CompareOrdinal(candidate.npcId + "|" + candidate.siteId, other.npcId + "|" + other.siteId) < 0);
                if (at < 0) at = nearest.Count;
                nearest.Insert(at, candidate);
                if (nearest.Count > ResidentLimit) nearest.RemoveAt(ResidentLimit);
            }
        }
        capture.omittedProviders = Mathf.Max(0, registry.Count - capture.examinedProviders);
        foreach (var candidate in nearest)
            capture.residents.Add(ReadResident(candidate, world, state.renderOrigin, query, diagnosticType, hitBuffer));
        // note: A replacement profile/world must not receive diagnostics attributed to a retired capture context.
        if (!ReferenceEquals(WorldStateManager.Instance?.State, world) || profiles.ActiveProfileId != profileId ||
            !ReferenceEquals(PlayerStateManager.Instance?.state, state) || !TryGetContext(out _, out reason))
        { Debug.LogWarning("[YQNpcFloors] Capture retired before output: " + reason); return; }
        capture.captureSeconds = Time.realtimeSinceStartup - started;
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Output));
        Directory.CreateDirectory(folder);
        string filename = Path.Combine(folder, "npc-floors-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json");
        using (var stream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(capture, true));
        Debug.Log("[YQNpcFloors] Read-only capture: " + filename + " residents=" + capture.residents.Count);
    }

    private static ResidentRecord ReadResident(Candidate candidate, WorldState world, Vector3 renderOrigin,
        MethodInfo query, Type diagnosticType, FieldInfo hitBuffer)
    {
        var actor = candidate.entity;
        var provider = candidate.provider;
        var record = new ResidentRecord
        {
            npcId = candidate.npcId, siteId = candidate.siteId, actorInstanceId = actor.GetInstanceID(),
            providerInstanceId = provider.GetInstanceID(), actorHierarchy = Hierarchy(actor.transform), providerHierarchy = Hierarchy(provider.transform),
            roleIntent = Convert.ToString(ReadField(candidate.binding, "roleIntent")), seed = Convert.ToString(ReadField(candidate.binding, "seed")),
            residentIndex = Convert.ToInt32(ReadField(candidate.binding, "index")), active = actor.gameObject.activeInHierarchy,
            providerKitId = Convert.ToString(ReadField(provider, "expectedKitId")), providerCompositionSeed = Convert.ToString(ReadField(provider, "semanticSliceSeed")),
            position = actor.transform.position, logicalPosition = actor.transform.position + renderOrigin, rootY = actor.transform.position.y
        };
        if (world.npcs == null) { record.missingReason = "current saved world NPC document is unavailable"; return record; }
        foreach (var saved in world.npcs)
            if (saved != null && string.Equals(saved.npcId, record.npcId, StringComparison.Ordinal))
            { record.canonicalNpcPresent = true; record.savedName = saved.name; record.savedStatus = saved.status; record.savedLocationId = saved.locationId; break; }
        if (!record.canonicalNpcPresent) { record.missingReason = "registered actor identity is absent from current saved world NPC document"; return record; }
        if (!Finite(record.position)) { record.contactReason = "actor position is non-finite"; record.missingReason = record.contactReason; return record; }
        var animator = actor.GetComponentInChildren<Animator>(true);
        record.humanoidPose = animator != null && animator.isHuman;
        try { record.hasPosedFeet = YQGeneratedWorldTerrain.TryGetActorContactGeometry(actor.gameObject, out record.posedBodyBounds, out record.feetY) &&
            !float.IsNaN(record.feetY) && !float.IsInfinity(record.feetY); }
        catch (Exception exception) { record.contactReason = "canonical posed contact getter threw: " + exception.GetType().Name; }
        if (record.hasPosedFeet) record.posedFeetOffset = record.feetY - record.rootY;
        else
        {
            record.feetY = 0f;
            record.posedBodyBounds = default;
            if (string.IsNullOrEmpty(record.contactReason)) record.contactReason = "canonical actor contact geometry unavailable; no pivot/bounds/terrain substitute";
        }
        record.currentLoadedProvider = YQCompiledWorldSiteInstance.TryGetLoadedPopulationContext(record.siteId,
            record.providerKitId, record.providerCompositionSeed, out var registered) && registered == provider;
        if (!record.currentLoadedProvider) { record.missingReason = "exact registered reviewed population provider is not current/active/loaded for its kit and composition"; return record; }
        // note: Invoke the existing owned floor selector with this actor ignored for clearance. Its hit buffer supplies provenance only after the canonical filter has selected a floor.
        object[] arguments = { record.position, Vector3.zero, actor.transform, Activator.CreateInstance(diagnosticType) };
        bool projected;
        try { projected = (bool)query.Invoke(provider, arguments); }
        catch (Exception exception) { record.missingReason = "canonical provider floor query threw: " + (exception.InnerException ?? exception).GetType().Name; return record; }
        foreach (var field in diagnosticType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            record.floorQuery.Add(new CountRecord { name = field.Name, count = (int)field.GetValue(arguments[3]) });
        if (!projected) { record.missingReason = "canonical reviewed floor projection rejected; see owner/normal/height/roof/clearance/saturation counts"; return record; }
        record.floorPoint = (Vector3)arguments[1];
        if (!Finite(record.floorPoint)) { record.missingReason = "canonical provider floor point is non-finite"; return record; }
        record.canonicalFloorProjected = true;
        record.floorY = record.floorPoint.y;
        var matchedColliders = new HashSet<int>();
        var verificationRay = new Ray(record.position + Vector3.up * 120f, Vector3.down);
        foreach (RaycastHit hit in (RaycastHit[])hitBuffer.GetValue(null))
        {
            var collider = hit.collider;
            if (collider == null || !collider.transform.IsChildOf(provider.transform) ||
                collider.transform.IsChildOf(actor.transform) || collider is TerrainCollider ||
                (hit.point - record.floorPoint).sqrMagnitude > .000001f) continue;
            // note: NonAlloc leaves old entries beyond its unpublished hit count. Verify each unique owned collider afresh at the canonical point before attributing it; never trust stale buffer provenance.
            if (!matchedColliders.Add(collider.GetInstanceID()) || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy ||
                !collider.Raycast(verificationRay, out RaycastHit verified, 260f) || !Finite(verified.point) || !Finite(verified.normal) ||
                (verified.point - record.floorPoint).sqrMagnitude > .000001f) continue;
            record.floorMatches++;
            if (record.floorOwners.Count >= 8) continue;
            var floor = new FloorRecord { instanceId = collider.GetInstanceID(), hierarchy = Hierarchy(collider.transform),
                type = collider.GetType().FullName, point = verified.point, normal = verified.normal, triangleIndex = verified.triangleIndex,
                enabled = collider.enabled, active = collider.gameObject.activeInHierarchy, sameProvider = true };
            ReadCellDatum(provider, collider.transform, floor);
            record.floorOwners.Add(floor);
        }
        if (record.floorOwners.Count == 0) { record.missingReason = "canonical query returned no exact reviewed building-floor provenance (own body/terrain is not a resident floor)"; return record; }
        record.floorResolved = true;
        record.floorNormal = record.floorOwners[0].normal;
        record.floorCellHierarchy = record.floorOwners[0].cellHierarchy;
        foreach (var floor in record.floorOwners)
            if ((floor.normal - record.floorNormal).sqrMagnitude > .000001f) record.floorNormalAmbiguous = true;
        if (record.hasPosedFeet)
        {
            record.feetMinusFloor = record.feetY - record.floorY;
            // note: Production reviewed placement embeds feet by .005m. Report the residual without declaring a new tolerance or applying a correction.
            record.placementEmbedResidual = record.feetMinusFloor + .005f;
        }
        return record;
    }

    private static void ReadCellDatum(YQCompiledWorldSiteInstance provider, Transform floor, FloorRecord record)
    {
        var datums = ReadField(provider, "residentSurfaceDatums") as IDictionary;
        for (Transform current = floor; current != null && current != provider.transform; current = current.parent)
            if (datums != null && datums.Contains(current) && datums[current] is Vector3 local)
            {
                record.reviewedCellDatumFound = true;
                record.cellHierarchy = Hierarchy(current);
                record.reviewedCellDatumLocal = local;
                record.reviewedCellDatumWorld = current.TransformPoint(local);
                return;
            }
        record.cellHierarchy = string.Empty;
        record.cellDatumReason = "No exact reviewed cell datum ancestor is registered for this verified collider; canonical provider projection still supplies the actual floor point.";
    }
    private static bool TryGetQueryContract(out IDictionary registry, out MethodInfo query, out Type diagnosticType,
        out FieldInfo hitBuffer, out string failure)
    {
        // note: Reflection is confined to this editor receipt and the exact existing canonical provider query; missing contracts refuse capture rather than guess floors.
        var type = typeof(YQCompiledWorldSiteInstance);
        registry = type.GetField("Instances", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IDictionary;
        query = type.GetMethod("TryProjectResidentSurface", PrivateInstance);
        diagnosticType = type.GetNestedType("ResidentPlacementDiagnostics", BindingFlags.NonPublic);
        hitBuffer = type.GetField("ResidentSurfaceHitBuffer", BindingFlags.Static | BindingFlags.NonPublic);
        failure = "canonical reviewed resident binding/floor-query contract is unavailable";
        if (registry == null || query == null || diagnosticType == null || hitBuffer == null ||
            query.ReturnType != typeof(bool) || query.GetParameters().Length != 4 ||
            query.GetParameters()[0].ParameterType != typeof(Vector3) || query.GetParameters()[1].ParameterType != typeof(Vector3).MakeByRefType() ||
            query.GetParameters()[2].ParameterType != typeof(Transform) || query.GetParameters()[3].ParameterType != diagnosticType.MakeByRefType() ||
            hitBuffer.FieldType != typeof(RaycastHit[]) || !(hitBuffer.GetValue(null) is RaycastHit[]) ||
            type.GetField("residentPositionBindings", PrivateInstance) == null || type.GetField("residentSurfaceDatums", PrivateInstance) == null) return false;
        foreach (var field in diagnosticType.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (field.FieldType != typeof(int)) return false;
        var bindingsType = type.GetField("residentPositionBindings", PrivateInstance).FieldType;
        if (!typeof(IList).IsAssignableFrom(bindingsType) || !bindingsType.IsGenericType ||
            !typeof(IDictionary).IsAssignableFrom(type.GetField("residentSurfaceDatums", PrivateInstance).FieldType)) return false;
        var bindingType = bindingsType.GetGenericArguments()[0];
        if (bindingType.GetField("entity", AllInstance)?.FieldType != typeof(EntityInfo) ||
            bindingType.GetField("index", AllInstance)?.FieldType != typeof(int) ||
            bindingType.GetField("seed", AllInstance)?.FieldType != typeof(string) ||
            bindingType.GetField("roleIntent", AllInstance)?.FieldType != typeof(string)) return false;
        failure = string.Empty;
        return true;
    }
    private static object ReadField(object owner, string name) =>
        owner == null ? null : owner.GetType().GetField(name, AllInstance)?.GetValue(owner);
    private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    private static string Hierarchy(Transform transform)
    {
        var parts = new Stack<string>();
        for (var node = transform; node != null; node = node.parent) parts.Push(node.name + "[" + node.GetSiblingIndex() + "]");
        return string.Join("/", parts);
    }
    private static bool TryGetContext(out YQInvestorPlayerMotor motor, out string reason)
    {
        // note: Reuse the established named disposable-profile/released-game boundary without acquiring or modifying a developer test session.
        motor = YQInvestorPlayerMotor.ActiveMotor;
        var profiles = YQProfileSaveSystem.Instance;
        // note: Unity singleton references also require Unity null comparisons; null-conditional access does not detect destroyed native objects.
        var profile = profiles != null ? profiles.FindProfile(profiles.ActiveProfileId) : null;
        string name = profile != null ? profile.displayName : null;
        var worldManager = WorldStateManager.Instance;
        var playerManager = PlayerStateManager.Instance;
        var world = worldManager != null ? worldManager.State : null;
        bool valid = EditorApplication.isPlaying && !Application.isBatchMode && !EditorApplication.isCompiling &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased && !YQDeveloperTestSession.Active &&
            !YQDeveloperConsoleGate.BlocksPersistence && name != null && name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal) &&
            motor != null && motor.IsAuthoritative && motor.CanProcessMovementInput && motor.gameObject.activeInHierarchy &&
            motor.gameObject.scene.IsValid() && motor.gameObject.scene.isLoaded && playerManager != null && playerManager.state != null &&
            world?.worldIdentity != null && string.Equals(world.worldIdentity.ownerProfileId, profiles.ActiveProfileId, StringComparison.Ordinal);
        reason = valid ? string.Empty : "Requires the released Terrain Apron Playtest profile, its current authoritative player/world, and no developer test lease or persistence barrier.";
        return valid;
    }
}
#endif
