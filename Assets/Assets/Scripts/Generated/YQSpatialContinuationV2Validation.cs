using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class YQSpatialContinuationBasicValidationResultV2
{
    public readonly List<string> errors = new List<string>();
    public bool IsStructurallyValid => errors.Count == 0;

    internal void Add(string subject, string failure)
    {
        // note: Report the rejected contract without changing state, normalizing records, or issuing physical acceptance.
        errors.Add((subject ?? "continuation") + ": " + failure);
    }
}

public static class YQSpatialContinuationValidatorV2
{
    public static YQSpatialContinuationBasicValidationResultV2 ValidateBasic(
        GeneratedSpatialWorldPlanV2Record parent)
    {
        return ValidateBasic(parent, parent?.acceptedContinuation);
    }

    internal static string BranchIdentity(GeneratedSpatialContinuationV2Record continuation)
    {
        // note: Unity's inline field JSON may represent null variant branches like default records. Preserve the saved branch presence alongside that fast full-field snapshot; POI/settlement/camp and optional context semantics cannot alias.
        if (continuation == null) return "null";
        if (continuation.locations == null) return "null-locations";
        var shape = new StringBuilder(continuation.locations.Count * 8);
        foreach (var location in continuation.locations)
        {
            if (location == null) { shape.Append('n'); continue; }
            shape.Append(location.settlement == null ? '0' : '1');
            shape.Append(location.encampment == null ? '0' : '1');
            shape.Append(location.pointOfInterest == null ? '0' : '1');
            shape.Append(location.physicalContext == null ? '0' : '1');
            shape.Append(location.compositionLayout == null ? '0' : '1');
            shape.Append(location.settlement?.proceduralLayout == null ? '0' : '1');
            shape.Append(location.encampment?.proceduralLayout == null ? '0' : '1');
            shape.Append('|');
        }
        return shape.ToString();
    }

    private static readonly object ProofLock = new object();
    private static string latestProof, previousProof;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProofs()
    {
        lock (ProofLock) { latestProof = previousProof = null; }
    }

    public static YQSpatialContinuationBasicValidationResultV2 ValidateBasic(
        GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
    {
        // note: Reuse only a complete successful structural/hash gate for byte-identical parent and continuation fields. Return a fresh result so callers cannot mutate the retained proof.
        if (continuation == null) return new YQSpatialContinuationBasicValidationResultV2();
        string source;
        try { source = BasicProofSource(parent, continuation); }
        catch (Exception) { return ValidateUncached(parent, continuation); }
        lock (ProofLock)
        {
            if (string.Equals(latestProof, source, StringComparison.Ordinal)) return new YQSpatialContinuationBasicValidationResultV2();
            if (string.Equals(previousProof, source, StringComparison.Ordinal))
            {
                var old = latestProof; latestProof = previousProof; previousProof = old;
                return new YQSpatialContinuationBasicValidationResultV2();
            }
        }
        var result = ValidateUncached(parent, continuation);
        if (result.IsStructurallyValid && string.Equals(source, BasicProofSource(parent, continuation), StringComparison.Ordinal))
            lock (ProofLock) { previousProof = latestProof; latestProof = source; }
        return result;
    }


    private static string BasicProofSource(GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
    {
        // note: Preserve the original snapshot and branch evaluation order. Only plain callback-free data may share a proof across the unrelated sibling envelope on its parent.
        string parentJson = JsonUtility.ToJson(parent), continuationJson = JsonUtility.ToJson(continuation);
        string parentBranches = BranchIdentity(parent?.acceptedContinuation), continuationBranches = BranchIdentity(continuation);
        if (!CallbackFreeProofGraph(parent, continuation))
            return parentJson + "|" + continuationJson + "|" + parentBranches + "|" + continuationBranches;
        return ParentGeometryProofJson(parentJson) + "|" + continuationJson + "|" + continuationBranches;
    }

    private sealed class ProofReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object first, object last) => ReferenceEquals(first, last);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    private static bool CallbackFreeProofGraph(GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
        => CallbackFreeProofRoots(parent, typeof(GeneratedSpatialWorldPlanV2Record), continuation, typeof(GeneratedSpatialContinuationV2Record));

    private static bool CallbackFreeProofRoots(object parent, Type parentType, object continuation, Type continuationType)
    {
        // note: This invocation-local guard refuses callbacks, runtime subtypes, foreign objects and oversized graphs; it does not issue acceptance or retain another proof cache.
        var pending = new Stack<KeyValuePair<object, Type>>();
        var visited = new HashSet<object>(new ProofReferenceComparer());
        var fields = new Dictionary<Type, System.Reflection.FieldInfo[]>();
        pending.Push(new KeyValuePair<object, Type>(parent, parentType));
        pending.Push(new KeyValuePair<object, Type>(continuation, continuationType));
        bool Leaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) ||
            type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Quaternion) || type == typeof(Color);
        while (pending.Count > 0)
        {
            var entry = pending.Pop(); object value = entry.Key;
            if (value == null) continue;
            Type type = value.GetType();
            if (type != entry.Value || value is ISerializationCallbackReceiver) return false;
            if (Leaf(type)) continue;
            if (visited.Count >= 65536) return false;
            if (!visited.Add(value)) continue;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type element = type.GetGenericArguments()[0];
                if (!Leaf(element)) foreach (object item in (System.Collections.IList)value)
                {
                    if (pending.Count >= 65536) return false;
                    pending.Push(new KeyValuePair<object, Type>(item, element));
                }
                continue;
            }
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                if (!Leaf(element)) foreach (object item in (Array)value)
                {
                    if (pending.Count >= 65536) return false;
                    pending.Push(new KeyValuePair<object, Type>(item, element));
                }
                continue;
            }
            // note: Private serialized base fields are not returned by this field walk; conservatively retain the original proof identity for every inherited record type.
            if (!type.IsValueType && type.BaseType != typeof(object)) return false;
            if (type.Assembly != typeof(GeneratedSpatialWorldPlanV2Record).Assembly ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) || !type.IsSerializable) return false;
            if (!fields.TryGetValue(type, out var members))
            {
                members = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                fields.Add(type, members);
            }
            foreach (var field in members)
            {
                if (field.IsNotSerialized || !field.IsPublic &&
                    !field.IsDefined(typeof(SerializeField), false) && !field.IsDefined(typeof(SerializeReference), false)) continue;
                if (!Leaf(field.FieldType))
                {
                    if (pending.Count >= 65536) return false;
                    pending.Push(new KeyValuePair<object, Type>(field.GetValue(value), field.FieldType));
                }
            }
        }
        return true;
    }

    private static string ParentGeometryProofJson(string source)
    {
        // note: Remove only the top-level sibling envelope value from an already captured Unity field document. All parent metadata/geometry and the explicit candidate document remain exact proof inputs.
        const string key = "\"acceptedContinuation\"";
        int depth = 0; bool quoted = false, escaped = false;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"')
            {
                if (depth == 1 && i + key.Length <= source.Length && string.CompareOrdinal(source, i, key, 0, key.Length) == 0)
                {
                    int start = i + key.Length;
                    while (start < source.Length && char.IsWhiteSpace(source[start])) start++;
                    if (start < source.Length && source[start++] == ':')
                    {
                        while (start < source.Length && char.IsWhiteSpace(source[start])) start++;
                        int nested = 0; bool text = false, slash = false;
                        for (int end = start; end < source.Length; end++)
                        {
                            char item = source[end];
                            if (text) { if (slash) slash = false; else if (item == '\\') slash = true; else if (item == '"') text = false; continue; }
                            if (item == '"') { text = true; continue; }
                            if (nested == 0 && (item == ',' || item == '}')) return source.Substring(0, start) + "null" + source.Substring(end);
                            if (item == '{' || item == '[') nested++;
                            else if (item == '}' || item == ']') nested--;
                        }
                    }
                }
                quoted = true;
            }
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }
        return source;
    }


    private static YQSpatialContinuationBasicValidationResultV2 ValidateUncached(
        GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation)
    {
        var result = new YQSpatialContinuationBasicValidationResultV2();
        // note: Null is the old-save contract, not a request to invent or accept frontier content.
        if (continuation == null)
            return result;

        try
        {
            ValidateBasicCore(parent, continuation, result);
        }
        catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                                          exception is InvalidOperationException || exception is OverflowException)
        {
            // note: Malformed scalar documents fail this read-only gate with a reason instead of escaping into a future publication caller.
            result.Add("continuation", "malformed serialized contract: " + exception.Message);
        }
        return result;
    }

    private static void ValidateBasicCore(GeneratedSpatialWorldPlanV2Record parent,
        GeneratedSpatialContinuationV2Record continuation, YQSpatialContinuationBasicValidationResultV2 result)
    {

        if (parent == null || parent.blueprint == null ||
            parent.acceptanceState != GeneratedSpatialPlanAcceptanceState.Accepted ||
            parent.schemaVersion != GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion ||
            string.IsNullOrWhiteSpace(parent.worldSeed) || string.IsNullOrWhiteSpace(parent.contentHash) ||
            parent.contentHash != parent.validatedContentHash || parent.validationErrors == null ||
            parent.validationErrors.Count != 0)
        {
            result.Add("continuation", "accepted parent identity is missing or unsupported");
            return;
        }
        if (continuation.schemaVersion != GeneratedSpatialContinuationV2Record.SupportedSchemaVersion ||
            continuation.generationVersion != GeneratedSpatialContinuationV2Record.SupportedGenerationVersion ||
            continuation.validationVersion != GeneratedSpatialContinuationV2Record.SupportedValidationVersion ||
            continuation.hashAlgorithm != GeneratedSpatialContinuationV2Record.SupportedHashAlgorithm)
            result.Add("continuation", "unsupported version or hash algorithm");
        if (continuation.worldSeed != parent.worldSeed ||
            continuation.parentSpatialContentHash != parent.contentHash ||
            continuation.parentMemberFootprintHash != YQSpatialBlueprintHasherV2.ComputeMemberFootprintHashReadOnly(parent) ||
            parent.contentHash != YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(parent))
            result.Add("continuation", "world or accepted parent geometry identity mismatch");
        bool accepted = continuation.state == YQSpatialContinuationStateV2.Accepted;
        if (!IsSupportedState(continuation.state) || continuation.revision < 0 || accepted && continuation.revision == 0)
            result.Add("continuation", "unsupported state or revision");
        if (continuation.locations == null || accepted && continuation.locations.Count == 0 ||
            continuation.validationErrors == null || accepted && continuation.validationErrors.Count != 0)
        {
            result.Add("continuation", "location collection or validation status is incomplete");
            return;
        }

        // note: Case-insensitive ID uniqueness matches existing runtime lookups; no base or sibling identity may be shadowed.
        var siteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var semanticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var npcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (parent.blueprint.sites != null)
            foreach (YQSiteAnchorV2 site in parent.blueprint.sites)
                if (site != null)
                {
                    siteIds.Add(site.siteId ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(site.sourceSemanticId)) semanticIds.Add(site.sourceSemanticId);
                }
        foreach (GeneratedSpatialContinuationLocationV2Record location in continuation.locations)
        {
            ValidateLocation(location, accepted, result);
            if (location == null) continue;
            AddIdentity(location.contentId, contentIds, "content", result);
            AddIdentity(location.anchor?.siteId, siteIds, "site", result);
            AddIdentity(location.anchor?.sourceSemanticId, semanticIds, "semantic", result);
            if (location.population != null)
                foreach (GeneratedNpcPlanRecord npc in location.population)
                    AddIdentity(npc?.npcId, npcIds, "population", result);
        }
        ValidateHash("continuation", continuation.contentHash, continuation.validatedContentHash,
            accepted, () => YQSpatialContinuationHasherV2.ComputeContentHash(continuation), result);
    }

    private static void ValidateLocation(GeneratedSpatialContinuationLocationV2Record location,
        bool acceptedEnvelope, YQSpatialContinuationBasicValidationResultV2 result)
    {
        if (location == null) { result.Add("location", "missing record"); return; }
        string id = location.anchor?.siteId ?? location.contentId;
        // note: A continued hostile site has one geometry authority on compositionLayout; the optional base-only field must remain absent.
        if (location.encampment?.proceduralLayout != null)
            result.Add(id, "continued hostile layout must use compositionLayout only");
        bool accepted = location.state == YQSpatialContinuationStateV2.Accepted;
        if (location.schemaVersion != GeneratedSpatialContinuationLocationV2Record.SupportedSchemaVersion ||
            !IsSupportedState(location.state) || acceptedEnvelope && !accepted ||
            location.revision < 0 || accepted && location.revision == 0 ||
            location.source != YQSpatialContinuationSourceV2.LlmProposal &&
            location.source != YQSpatialContinuationSourceV2.ExplicitFallback)
            result.Add(id, "unsupported version, provisional state, source, or revision");
        if (string.IsNullOrWhiteSpace(location.deterministicSeed) ||
            string.IsNullOrWhiteSpace(location.sourceContentId) || string.IsNullOrWhiteSpace(location.sourceContentHash) ||
            location.validationErrors == null || accepted && location.validationErrors.Count != 0)
            result.Add(id, "content provenance or validation status is incomplete");

        YQSiteAnchorV2 anchor = location.anchor;
        if (anchor == null) { result.Add(id, "canonical anchor is missing"); return; }
        if (!Finite(anchor.x) || !Finite(anchor.z) || !Finite(anchor.preferredHeadingDegrees) ||
            !Positive(anchor.reservedRadius) || !Positive(anchor.terrainSearchRadius) ||
            !Finite(anchor.maximumSlopeDegrees) || anchor.maximumSlopeDegrees < 0f || anchor.maximumSlopeDegrees > 90f ||
            !Normalized(anchor.minimumRouteAccess) || !Normalized(anchor.minimumWaterAccess) ||
            !Enum.IsDefined(typeof(YQSitePlacementModeV2), anchor.placementMode) || anchor.placementMode == YQSitePlacementModeV2.Unknown ||
            string.IsNullOrWhiteSpace(anchor.parentRegionId))
            result.Add(id, "invalid anchor geometry, access values, or placement contract");
        ValidateMembers(anchor, result);
        ValidatePhysicalContext(location, result);
        ValidateFunctions(anchor, result);
        ValidateEntrances(location, result);

        int payloads = (location.settlement != null ? 1 : 0) + (location.encampment != null ? 1 : 0) +
            (location.pointOfInterest != null ? 1 : 0);
        string semanticId = location.settlement?.settlementId ?? location.encampment?.encampmentId ?? location.pointOfInterest?.poiId;
        string regionId = location.settlement?.regionId ?? location.encampment?.regionId ?? location.pointOfInterest?.regionId;
        bool typeMatches = anchor.kind == YQSiteKindV2.Settlement && location.settlement != null ||
            anchor.kind == YQSiteKindV2.HostileSite && location.encampment != null ||
            (anchor.kind == YQSiteKindV2.PointOfInterest || anchor.kind == YQSiteKindV2.NaturalFeature) && location.pointOfInterest != null;
        if (payloads != 1 || !typeMatches || string.IsNullOrWhiteSpace(semanticId) ||
            anchor.sourceSemanticId != semanticId || anchor.parentRegionId != regionId)
            result.Add(id, "typed semantic identity, kind, or region does not match the anchor");
        ValidateBindingAndLayout(location, accepted, result);
        ValidatePopulation(location, result);
        ValidateProofClaims(location, accepted, result);
        ValidateHash(id, location.contentHash, location.validatedContentHash, accepted,
            () => YQSpatialContinuationHasherV2.ComputeLocationContentHash(location), result);
    }

    private static void ValidateMembers(YQSiteAnchorV2 anchor, YQSpatialContinuationBasicValidationResultV2 result)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (anchor.memberFootprint == null) { result.Add(anchor.siteId, "member collection is missing"); return; }
        // note: Reject oversized extensions before any derived cell enumeration; the eight-sector construction budget does not change legacy opening footprints.
        if (anchor.memberFootprint.Count > 7) { result.Add(anchor.siteId, "continuation exceeds eight physical sectors"); return; }
        long cells = FootprintCellCount(anchor.x, anchor.z, anchor.reservedRadius);
        foreach (YQSiteMemberFootprintV2 member in anchor.memberFootprint)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.memberId) || !ids.Add(member.memberId) ||
                member.memberId == anchor.siteId || !Finite(member.x) || !Finite(member.z) || !Positive(member.reservedRadius) || member.sectorIndex < 0)
                result.Add(anchor.siteId, "invalid or duplicate member footprint");
            if (member != null) cells += FootprintCellCount(member.x, member.z, member.reservedRadius);
        }
        if (cells > 4096) result.Add(anchor.siteId, "continuation footprint exceeds 4096 reserved cells");
    }

    private static long FootprintCellCount(float x, float z, float radius)
    {
        if (!Finite(x) || !Finite(z) || !Positive(radius) || Math.Abs(x) > 1048576f || Math.Abs(z) > 1048576f)
            return 4097;
        // note: Closed boundaries include both touching cells, matching the reservation owner's boundary convention without allocating a grid.
        double minX = Math.Ceiling(((double)x - radius + 512d) / 128d) - 1d;
        double minZ = Math.Ceiling(((double)z - radius + 512d) / 128d) - 1d;
        double maxX = Math.Floor(((double)x + radius + 512d) / 128d);
        double maxZ = Math.Floor(((double)z + radius + 512d) / 128d);
        double count = (maxX - minX + 1d) * (maxZ - minZ + 1d);
        return count > 4096d ? 4097 : (long)count;
    }

    public static YQSpatialContinuationBasicValidationResultV2 ValidatePhysicalContextOnly(GeneratedSpatialContinuationLocationV2Record location)
    {
        var result = new YQSpatialContinuationBasicValidationResultV2();
        if (location?.anchor == null) { result.Add("location", "missing physical anchor"); return result; }
        if (!Finite(location.anchor.maximumSlopeDegrees) || location.anchor.maximumSlopeDegrees < 0f || location.anchor.maximumSlopeDegrees > 90f)
            result.Add(location.anchor.siteId, "invalid physical slope contract");
        ValidateMembers(location.anchor, result);
        ValidatePhysicalContext(location, result);
        return result;
    }

    private static void ValidatePhysicalContext(GeneratedSpatialContinuationLocationV2Record location,
        YQSpatialContinuationBasicValidationResultV2 result)
    {
        var context = location.physicalContext;
        if (context == null) return;
        string id = location.anchor.siteId;
        if (context.version != GeneratedSpatialContinuationPhysicalContextV2Record.SupportedVersion ||
            context.terrainPads == null || context.routes == null || context.terrainPads.Count > 8 || context.routes.Count > 16)
        { result.Add(id, "unsupported or oversized physical context"); return; }
        var expected = new HashSet<string>(StringComparer.Ordinal) { id };
        if (location.anchor.memberFootprint != null)
            foreach (var member in location.anchor.memberFootprint) if (member != null) expected.Add(member.memberId);
        // note: Numeric preflight and pad indexing are bounded to the supported physical sector sizes; larger future layouts need an explicit sampling contract.
        if (location.anchor.reservedRadius > 256f || location.anchor.memberFootprint?.Count > 7 ||
            location.anchor.memberFootprint != null && location.anchor.memberFootprint.Exists(member => member != null && member.reservedRadius > 256f))
            result.Add(id, "physical context sector exceeds its 256 metre sampling bound");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pad in context.terrainPads)
            if (pad == null || string.IsNullOrWhiteSpace(pad.sectorId) || !expected.Contains(pad.sectorId) || !seen.Add(pad.sectorId) ||
                !Normalized(pad.elevationNormalized) || !Positive(pad.shoulderWidth) || pad.shoulderWidth > 128f)
                result.Add(id, "invalid or duplicate sector terrain pad");
        if (location.anchor.requiresTerrainConformance && seen.Count != expected.Count)
            result.Add(id, "physical context must cover every terrain-conforming sector");
        var routeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int points = 0;
        foreach (var route in context.routes)
        {
            if (route == null || string.IsNullOrWhiteSpace(route.routeId) || !routeIds.Add(route.routeId) ||
                route.parentRegionId != location.anchor.parentRegionId || !Enum.IsDefined(typeof(YQRouteClassV2), route.routeClass) ||
                route.routeClass == YQRouteClassV2.Unknown || !Positive(route.width) || route.width > 32f ||
                !Finite(route.shoulderWidth) || route.shoulderWidth < 0f || route.shoulderWidth > 128f ||
                !Positive(route.maximumGradeDegrees) || route.maximumGradeDegrees > 28f ||
                route.controlPoints == null || route.controlPoints.Count < 2 || route.controlPoints.Count > 64 ||
                route.crossings == null || route.crossings.Count != 0)
            { result.Add(id, "unsupported route geometry or crossing contract"); continue; }
            points += route.controlPoints.Count;
            foreach (var point in route.controlPoints)
                if (point == null || !Finite(point.x) || !Finite(point.z) || Math.Abs(point.x) > 1048576f || Math.Abs(point.z) > 1048576f ||
                    !Normalized(point.normalizedElevation) || !Finite(point.width) || point.width < 0f ||
                    point.width > 0f && Math.Abs(point.width - route.width) > .001f) result.Add(id, "invalid route control point");
        }
        if (points > 256) result.Add(id, "physical context exceeds 256 route points");
    }

    private static void ValidateFunctions(YQSiteAnchorV2 anchor, YQSpatialContinuationBasicValidationResultV2 result)
    {
        var functions = new HashSet<YQAssetFunctionV2>();
        if (anchor.requiredFunctions == null || anchor.requiredFunctions.Count == 0)
        { result.Add(anchor.siteId, "typed required functions are missing"); return; }
        foreach (YQAssetFunctionV2 function in anchor.requiredFunctions)
            if (function == YQAssetFunctionV2.None || !Enum.IsDefined(typeof(YQAssetFunctionV2), function) || !functions.Add(function))
                result.Add(anchor.siteId, "unknown or duplicate required function");
        // note: This checks requested semantics only; it makes no claim that a released physical provider supplies these functions.
        if (anchor.kind == YQSiteKindV2.Settlement &&
            (!functions.Contains(YQAssetFunctionV2.Habitation) || !functions.Contains(YQAssetFunctionV2.Service) ||
             !functions.Contains(YQAssetFunctionV2.Circulation)) ||
            anchor.kind == YQSiteKindV2.HostileSite &&
            (!functions.Contains(YQAssetFunctionV2.Encounter) || !functions.Contains(YQAssetFunctionV2.Reward) ||
             !functions.Contains(YQAssetFunctionV2.Security)))
            result.Add(anchor.siteId, "site kind is missing its existing typed function requirements");
    }

    private static void ValidateEntrances(GeneratedSpatialContinuationLocationV2Record location,
        YQSpatialContinuationBasicValidationResultV2 result)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (location.entrances == null || location.entrances.Count == 0)
        { result.Add(location.anchor.siteId, "entrance contract is missing"); return; }
        foreach (GeneratedSemanticEntranceRecord entrance in location.entrances)
            if (entrance == null || string.IsNullOrWhiteSpace(entrance.entranceId) || !ids.Add(entrance.entranceId) ||
                !Finite(entrance.worldX) || !Finite(entrance.worldZ) || !Finite(entrance.headingDegrees))
                result.Add(location.anchor.siteId, "invalid or duplicate entrance");
    }

    private static void ValidateBindingAndLayout(GeneratedSpatialContinuationLocationV2Record location,
        bool accepted, YQSpatialContinuationBasicValidationResultV2 result)
    {
        string id = location.anchor.siteId;
        string kitId = location.settlement?.runtimeSiteKitId ?? location.encampment?.runtimeSiteKitId ?? location.poiRuntimeSiteKitId;
        string bindingVersion = location.settlement?.runtimeSiteBindingVersion ?? location.encampment?.runtimeSiteBindingVersion ??
            location.poiRuntimeSiteBindingVersion;
        if ((location.settlement != null || location.encampment != null) &&
            (!string.IsNullOrEmpty(location.poiRuntimeSiteKitId) || !string.IsNullOrEmpty(location.poiRuntimeSiteBindingVersion)) ||
            location.settlement != null && location.compositionLayout != null)
            result.Add(id, "competing binding or settlement layout owners");
        if (accepted && (string.IsNullOrWhiteSpace(kitId) || string.IsNullOrWhiteSpace(bindingVersion) ||
            string.IsNullOrWhiteSpace(location.compositionSeed) || string.IsNullOrWhiteSpace(location.compositionGeometrySignature) ||
            location.selectedSourceCellIds == null || location.selectedSourceCellIds.Count == 0))
            result.Add(id, "accepted binding or exact composition identity is incomplete");
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (location.selectedSourceCellIds != null)
            foreach (string sourceId in location.selectedSourceCellIds)
                if (string.IsNullOrWhiteSpace(sourceId) || !selected.Add(sourceId)) result.Add(id, "invalid or duplicate selected source cell");
        YQProceduralSettlementLayoutRecord layout = location.settlement?.proceduralLayout ?? location.compositionLayout;
        if (layout == null) return;
        if (!YQProceduralSettlementLayout.ValidateRecord(layout, out string failure))
        { result.Add(id, failure); return; }
        if (layout.kitId != kitId || layout.seed != location.compositionSeed ||
            YQProceduralSettlementLayout.GeometrySignature(layout) != location.compositionGeometrySignature)
            result.Add(id, "saved layout does not match its binding and composition identity");
    }

    private static void ValidatePopulation(GeneratedSpatialContinuationLocationV2Record location,
        YQSpatialContinuationBasicValidationResultV2 result)
    {
        if (location.population == null) { result.Add(location.anchor.siteId, "population collection is missing"); return; }
        foreach (GeneratedNpcPlanRecord npc in location.population)
            if (npc == null || npc.regionId != location.anchor.parentRegionId ||
                location.settlement != null && (npc.settlementId != location.settlement.settlementId || !string.IsNullOrEmpty(npc.encampmentId)) ||
                location.encampment != null && (npc.encampmentId != location.encampment.encampmentId || !string.IsNullOrEmpty(npc.settlementId)) ||
                location.pointOfInterest != null && (!string.IsNullOrEmpty(npc.settlementId) || !string.IsNullOrEmpty(npc.encampmentId)))
                result.Add(location.anchor.siteId, "population parent identity is inconsistent");
    }

    private static void ValidateProofClaims(GeneratedSpatialContinuationLocationV2Record location,
        bool accepted, YQSpatialContinuationBasicValidationResultV2 result)
    {
        if (location.proofClaims == null) { result.Add(location.anchor.siteId, "proof collection is missing"); return; }
        if (location.proofClaims.Count == 0 && !accepted) return;
        var kinds = new HashSet<YQSpatialContinuationProofKindV2>();
        string payloadHash = YQSpatialContinuationHasherV2.ComputeLocationPayloadHash(location);
        foreach (GeneratedSpatialContinuationProofV2Record proof in location.proofClaims)
        {
            if (proof == null || proof.schemaVersion != GeneratedSpatialContinuationProofV2Record.SupportedSchemaVersion ||
                proof.kind == YQSpatialContinuationProofKindV2.None || !Enum.IsDefined(typeof(YQSpatialContinuationProofKindV2), proof.kind) ||
                !kinds.Add(proof.kind) || !Enum.IsDefined(typeof(YQSpatialContinuationProofOutcomeV2), proof.outcome) ||
                proof.subjectSiteId != location.anchor.siteId || proof.subjectPayloadHash != payloadHash ||
                string.IsNullOrWhiteSpace(proof.ownerValidationVersion) || string.IsNullOrWhiteSpace(proof.evidenceId) ||
                string.IsNullOrWhiteSpace(proof.evidenceHash) || accepted && proof.outcome != YQSpatialContinuationProofOutcomeV2.Passed)
                result.Add(location.anchor.siteId, "proof claim identity, version, or outcome is inconsistent");
        }
        // note: Complete typed claims are necessary for an accepted record, but only the owning validators and publication transaction can verify those claims.
        if (accepted)
            for (int kind = (int)YQSpatialContinuationProofKindV2.ContentProposal;
                 kind <= (int)YQSpatialContinuationProofKindV2.PopulationAndInteractions; kind++)
                if (!kinds.Contains((YQSpatialContinuationProofKindV2)kind))
                    result.Add(location.anchor.siteId, "accepted record is missing a required owner proof claim");
    }

    private static void ValidateHash(string id, string claimed, string validated, bool accepted,
        Func<string> compute, YQSpatialContinuationBasicValidationResultV2 result)
    {
        if (string.IsNullOrEmpty(claimed) && !accepted)
        {
            if (!string.IsNullOrEmpty(validated)) result.Add(id, "validated hash has no content hash");
            return;
        }
        if (string.IsNullOrWhiteSpace(claimed) || claimed != compute() || accepted && claimed != validated ||
            !string.IsNullOrEmpty(validated) && validated != claimed)
            result.Add(id, "content or validated checksum mismatch");
    }

    private static void AddIdentity(string id, HashSet<string> identities, string kind,
        YQSpatialContinuationBasicValidationResultV2 result)
    {
        if (string.IsNullOrWhiteSpace(id) || !identities.Add(id)) result.Add(kind, "missing or duplicate identity: " + id);
    }

    private static bool IsSupportedState(YQSpatialContinuationStateV2 state) =>
        state == YQSpatialContinuationStateV2.Staged || state == YQSpatialContinuationStateV2.Accepted;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Positive(float value) => Finite(value) && value > 0f;
    private static bool Normalized(float value) => Finite(value) && value >= 0f && value <= 1f;
}

public static class YQSpatialContinuationHasherV2
{
    public static string ComputeLocationPayloadHash(GeneratedSpatialContinuationLocationV2Record location)
    {
        if (location == null) return string.Empty;
        JObject token = LocationToken(location);
        // note: Owner claims bind to payload geometry/content without creating a checksum cycle through their own subject hash.
        token.Remove("state");
        token.Remove("proofClaims");
        return Hash(token);
    }

    public static string ComputeLocationContentHash(GeneratedSpatialContinuationLocationV2Record location)
    {
        return location == null ? string.Empty : Hash(LocationToken(location));
    }

    public static string ComputeContentHash(GeneratedSpatialContinuationV2Record continuation)
    {
        if (continuation == null) return string.Empty;
        JObject token = FieldToken(continuation);
        RemoveHashMetadata(token);
        var locations = new List<GeneratedSpatialContinuationLocationV2Record>(
            continuation.locations ?? new List<GeneratedSpatialContinuationLocationV2Record>());
        locations.Sort((left, right) => string.CompareOrdinal(left?.anchor?.siteId, right?.anchor?.siteId));
        var records = new JArray();
        foreach (GeneratedSpatialContinuationLocationV2Record location in locations)
        {
            if (location == null) { records.Add(JValue.CreateNull()); continue; }
            JObject record = LocationToken(location);
            // note: The envelope binds each entry's independently validated checksum as well as its complete payload and proof claims.
            record["contentHash"] = location.contentHash;
            record["validatedContentHash"] = location.validatedContentHash;
            records.Add(record);
        }
        token["locations"] = records;
        return Hash(token);
    }

    private static JObject LocationToken(GeneratedSpatialContinuationLocationV2Record location)
    {
        JObject token = FieldToken(location);
        RemoveHashMetadata(token);
        // note: Adding an optional context must not change an older location checksum when no context was persisted.
        if (location.physicalContext == null) token.Remove("physicalContext");
        else if (token["physicalContext"] is JObject context)
        {
            SortUnorderedRecords(context, "terrainPads", "sectorId");
            SortUnorderedRecords(context, "routes", "routeId");
        }
        // note: Unity serializes newly added optional class fields; omit an absent base layout to preserve pre-extension hostile continuation checksums.
        if (location.encampment?.proceduralLayout == null && token["encampment"] is JObject encampment)
            encampment.Remove("proceduralLayout");
        if (token["anchor"] is JObject anchor)
            SortUnorderedRecords(anchor, "memberFootprint", "memberId");
        return token;
    }

    private static JObject FieldToken(object record)
    {
        // note: Unity field serialization excludes Vector3.normalized and other computed properties; Json.NET only parses the resulting scalar document.
        return JObject.Parse(JsonUtility.ToJson(record));
    }

    private static void RemoveHashMetadata(JObject token)
    {
        token.Remove("contentHash");
        token.Remove("validatedContentHash");
        token.Remove("validationErrors");
    }

    private static void SortUnorderedRecords(JObject parent, string property, string identity)
    {
        if (!(parent[property] is JArray array)) return;
        var records = new List<JToken>();
        foreach (JToken record in array) records.Add(record);
        records.Sort((left, right) => string.CompareOrdinal(
            left is JObject leftObject ? (string)leftObject[identity] : null,
            right is JObject rightObject ? (string)rightObject[identity] : null));
        parent[property] = new JArray(records);
    }

    private static void WriteCanonical(JsonWriter writer, JToken token)
    {
        // note: Render the same ordinal object order directly; array order and primitive Json.NET formatting retain the accepted checksum bytes without a second token tree.
        if (token is JObject obj)
        {
            var properties = new List<JProperty>(obj.Properties());
            properties.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            writer.WriteStartObject();
            foreach (JProperty property in properties)
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
            return;
        }
        if (token is JArray array)
        {
            writer.WriteStartArray();
            foreach (JToken value in array) WriteCanonical(writer, value);
            writer.WriteEndArray();
            return;
        }
        token.WriteTo(writer);
    }

    private static string Hash(JToken token)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            // note: BOM-free UTF8 with the same replacement fallback feeds SHA256 incrementally, avoiding the canonical string and its whole-document byte copy.
            using (var stream = new System.Security.Cryptography.CryptoStream(System.IO.Stream.Null, algorithm,
                System.Security.Cryptography.CryptoStreamMode.Write))
            {
                using (var text = new System.IO.StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                using (var writer = new JsonTextWriter(text) { Formatting = Formatting.None, CloseOutput = false })
                {
                    WriteCanonical(writer, token);
                    writer.Flush();
                }
                stream.FlushFinalBlock();
                var result = new StringBuilder(algorithm.Hash.Length * 2);
                foreach (byte value in algorithm.Hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }

}
