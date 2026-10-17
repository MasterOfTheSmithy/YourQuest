// Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using FrontierTiming = YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics;

public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }

    [Header("Persistence")]
    public string saveFileName = "world_state.json";

    public WorldState State { get; private set; } = WorldState.CreateDefault();
    public WorldState state => State;
    public WorldState GetWorldState() => State;

    // note: Keep Unity vector values on the explicit x/y/z converter so persistence never walks Vector3's derived normalized property.
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        // note: Compact JSON keeps the transactional world snapshot identical in schema while reducing synchronous serialization and atomic-write time.
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters =
        {
            new Vector3JsonConverter(),
            new Vector2JsonConverter(),
            new QuaternionJsonConverter()
        }
    };

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    // note: Preserve the previous complete world document before replacing generated canon.
    private string BackupSavePath => SavePath + ".bak";
    public string LastLoadStatus { get; private set; } = "not_loaded";
    public float MaximumSaveNormalizeSeconds { get; private set; }
    public float MaximumSaveSerializeSeconds { get; private set; }
    public float MaximumSaveWriteSeconds { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadOrCreate();
    }

    public void LoadOrCreate()
    {
        // note: Preserve the world document identity while development commands hold a rollback buffer.
        if (YQDeveloperConsoleGate.BlocksPersistence) return;
        bool hadPersistentState = File.Exists(SavePath) || File.Exists(BackupSavePath);
        string backupFailure = string.Empty;
        bool loaded = TryLoadState(SavePath, out WorldState loadedState, out string primaryFailure);
        if (!loaded && TryLoadState(BackupSavePath, out loadedState, out backupFailure))
        {
            // note: Keep the last accepted world plan when the primary write was interrupted.
            Debug.LogWarning("[WorldStateManager] Primary save was unreadable; recovered the last known-good backup.");
            loaded = true;
            LastLoadStatus = "recovered_backup";
        }

        if (loaded)
        {
            State = loadedState ?? WorldState.CreateDefault();
            if (LastLoadStatus == "not_loaded") LastLoadStatus = "loaded";
        }
        else
        {
            // note: Unsupported future schemas fail closed and never overwrite the only unreadable world save.
            State = WorldState.CreateDefault();
            LastLoadStatus = hadPersistentState && ((primaryFailure ?? string.Empty).IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (backupFailure ?? string.Empty).IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0)
                ? "unsupported_version" : "created_default";
        }

        NormalizeState();

        if (!hadPersistentState && !File.Exists(SavePath))
            Save();
    }

    public void Save()
    {
        // note: Preserve existing Unity events and callers while transactional owners can observe persistence failure.
        TrySave(out _);
    }

    public bool TrySave(out string failure)
    {
        // note: NPC test edits must not escape through an unrelated world save caller.
        if (YQDeveloperConsoleGate.BlocksPersistence) { failure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        failure = string.Empty;
        try
        {
            long saveStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            NormalizeState();
            State.TouchNow();
            long normalizedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            // note: Compact JSON preserves the same persisted schema while reducing serialization and atomic-write time for large procedural world saves.
            string json = JsonConvert.SerializeObject(State, JsonSettings);
            long serializedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            WriteAtomically(json);
            long writtenAt = System.Diagnostics.Stopwatch.GetTimestamp();
            // note: Retain stage high-water marks so a measured hitch can be assigned to normalization, serialization, or atomic I/O.
            float secondsPerTick = 1f / System.Diagnostics.Stopwatch.Frequency;
            MaximumSaveNormalizeSeconds = Mathf.Max(MaximumSaveNormalizeSeconds, (normalizedAt - saveStarted) * secondsPerTick);
            MaximumSaveSerializeSeconds = Mathf.Max(MaximumSaveSerializeSeconds, (serializedAt - normalizedAt) * secondsPerTick);
            MaximumSaveWriteSeconds = Mathf.Max(MaximumSaveWriteSeconds, (writtenAt - serializedAt) * secondsPerTick);
            return true;
        }
        catch (Exception e)
        {
            failure = e.Message;
            Debug.LogWarning("[WorldStateManager] Save failed:\n" + e);
            return false;
        }
    }

    public bool TryPrepareSnapshot(out string json, out string failure)
    {
        json = string.Empty;
        failure = string.Empty;
        try
        {
            NormalizeState();
            json = JsonConvert.SerializeObject(State, JsonSettings);
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    // note: The profile transaction owns this detached snapshot; no candidate is installed on the live world before its manifest pointer commits.
    internal sealed class PreparedContinuationSnapshot
    {
        internal readonly WorldStateManager owner;
        internal readonly WorldState source;
        internal readonly GeneratedWorldPlanRecord plan;
        internal readonly GeneratedSpatialWorldPlanV2Record artifact;
        internal readonly GeneratedSpatialContinuationV2Record priorContinuation;
        internal readonly List<YQEntityIdentityRecord> identities;
        internal readonly string sourceJson;
        internal readonly string worldJson;
        internal readonly GeneratedSpatialContinuationV2Record acceptedContinuation;
        internal readonly List<YQEntityIdentityRecord> appendedIdentities;
        internal readonly long revision;
        internal readonly long updatedUnix;

        internal PreparedContinuationSnapshot(WorldStateManager owner, WorldState source, WorldState detached,
            string sourceJson, string worldJson, List<YQEntityIdentityRecord> appendedIdentities)
        {
            this.owner = owner; this.source = source; plan = source.generatedWorldPlan;
            artifact = plan.spatialPlanV2; priorContinuation = artifact.acceptedContinuation;
            identities = source.identityRecords; this.sourceJson = sourceJson; this.worldJson = worldJson;
            acceptedContinuation = detached.generatedWorldPlan.spatialPlanV2.acceptedContinuation;
            this.appendedIdentities = appendedIdentities; revision = detached.stateRevision; updatedUnix = detached.lastUpdatedUnix;
        }
    }

    private static readonly object ContinuationBaseCloneIssuer = new object();

    // note: This is private preparation data, never a world authority or a persisted acceptance record.
    internal sealed class PreparedContinuationBaseWorld
    {
        private readonly WorldStateManager owner;
        private readonly WorldState source;
        private readonly GeneratedWorldPlanRecord plan;
        private readonly GeneratedSpatialWorldPlanV2Record artifact;
        private readonly GeneratedSpatialContinuationV2Record prior;
        private readonly List<YQEntityIdentityRecord> identities;
        private readonly YQWorldIdentityRecord identity;
        private readonly string profileId, worldId, sourceJson;
        private readonly int epoch;
        private readonly object[] codec;
        private WorldState detached;
        private bool consumed;

        private PreparedContinuationBaseWorld(WorldStateManager owner, WorldState source,
            string json, WorldState detached, object[] codec)
        {
            this.owner = owner; this.source = source; this.detached = detached; this.codec = codec;
            sourceJson = json; plan = source.generatedWorldPlan; artifact = plan.spatialPlanV2;
            prior = artifact.acceptedContinuation; identities = source.identityRecords; identity = source.worldIdentity;
            profileId = identity?.ownerProfileId; worldId = identity?.worldId; epoch = YQServiceLifecycle.RequestEpoch;
        }

        internal static PreparedContinuationBaseWorld Issue(WorldStateManager owner, WorldState source,
            string json, WorldState detached, object[] codec, object issuer)
        {
            if (!ReferenceEquals(issuer, ContinuationBaseCloneIssuer)) throw new InvalidOperationException("Base world preparation requires its canonical manager.");
            return new PreparedContinuationBaseWorld(owner, source, json, detached, codec);
        }

        internal bool TryTake(WorldStateManager expectedOwner, WorldState expectedWorld, out string json, out WorldState value)
        {
            json = null; value = null;
            if (consumed) return false;
            consumed = true;
            try
            {
                // note: Reuse requires actual complete source equality and exact lifetime owners; deep drift falls back to fresh synchronous capture.
                if (!ReferenceEquals(owner, expectedOwner) || !ReferenceEquals(Instance, owner) || !owner.isActiveAndEnabled ||
                    !ReferenceEquals(owner.State, source) || !ReferenceEquals(expectedWorld, source) || !YQServiceLifecycle.IsCurrent(epoch) ||
                    !ReferenceEquals(source.generatedWorldPlan, plan) || !ReferenceEquals(plan.spatialPlanV2, artifact) ||
                    !ReferenceEquals(artifact.acceptedContinuation, prior) || !ReferenceEquals(source.identityRecords, identities) ||
                    !ReferenceEquals(source.worldIdentity, identity) || identity?.ownerProfileId != profileId || identity?.worldId != worldId ||
                    !SamePlainContinuationCodec(codec) || !HasPlainContinuationWorldData(source) ||
                    !YQProfileCommitStore.JsonEqualsCaptured(source, sourceJson, JsonSettings)) return false;
                json = sourceJson; value = detached;
                return value != null;
            }
            catch { return false; }
            finally { detached = null; }
        }

        internal void Retire() { consumed = true; detached = null; }
    }

    internal bool TryCaptureContinuationBaseWorld(WorldState expectedWorld, out PreparedContinuationBaseWorld preparation)
    {
        preparation = null;
        using var timing = new Unity.Profiling.ProfilerMarker("YQ.Frontier.WorldBaseClone").Auto();
        try
        {
            if (!ReferenceEquals(Instance, this) || !isActiveAndEnabled || !ReferenceEquals(State, expectedWorld) || expectedWorld == null ||
                expectedWorld.schemaVersion != YQStateContract.CurrentStateSchemaVersion || expectedWorld.generatedWorldPlan?.spatialPlanV2 == null ||
                expectedWorld.identityRecords == null || expectedWorld.stateRevision < 0 || expectedWorld.stateRevision == long.MaxValue ||
                !TryCapturePlainContinuationCodec(out object[] codec) || !HasPlainContinuationWorldData(expectedWorld)) return false;
            // note: Move only the original private base-copy block; registration, fresh pairing, compiler proof, timestamps and live aliases remain in the final factory.
            string json = JsonConvert.SerializeObject(expectedWorld, JsonSettings);
            WorldState detached = JsonConvert.DeserializeObject<WorldState>(json, JsonSettings);
            if (!SamePlainContinuationCodec(codec) || !YQProfileCommitStore.JsonEqualsCaptured(detached, json, JsonSettings)) return false;
            preparation = PreparedContinuationBaseWorld.Issue(this, expectedWorld, json, detached, codec, ContinuationBaseCloneIssuer);
            return true;
        }
        catch
        {
            // note: Optional preparation grants no acceptance; the original final factory retries current inputs and reports its unchanged rejection/error.
            return false;
        }
    }

    private static bool TryCapturePlainContinuationCodec(out object[] snapshot)
    {
        snapshot = null;
        // note: A global/default/custom codec may run application callbacks. Leave that environment entirely on the original fresh final path.
        if (JsonConvert.DefaultSettings != null || JsonSettings.ContractResolver != null || JsonSettings.Error != null || JsonSettings.TraceWriter != null ||
            JsonSettings.ReferenceResolverProvider != null || JsonSettings.EqualityComparer != null || JsonSettings.SerializationBinder != null ||
            JsonSettings.Context.Context != null || JsonSettings.Culture != System.Globalization.CultureInfo.InvariantCulture || JsonSettings.Converters.Count != 3 ||
            JsonSettings.Converters[0].GetType() != typeof(Vector3JsonConverter) || JsonSettings.Converters[1].GetType() != typeof(Vector2JsonConverter) ||
            JsonSettings.Converters[2].GetType() != typeof(QuaternionJsonConverter)) return false;
        var properties = typeof(JsonSerializerSettings).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        snapshot = new object[properties.Length];
        for (int index = 0; index < properties.Length; index++)
            snapshot[index] = properties[index].Name == "Converters" ? (object)JsonSettings.Converters.Count : properties[index].GetValue(JsonSettings);
        return true;
    }

    private static bool SamePlainContinuationCodec(object[] snapshot)
    {
        if (snapshot == null || !TryCapturePlainContinuationCodec(out object[] current) || snapshot.Length != current.Length) return false;
        for (int index = 0; index < current.Length; index++) if (!Equals(current[index], snapshot[index])) return false;
        return true;
    }

    private sealed class ContinuationDataReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }

    private static bool HasPlainContinuationWorldData(WorldState world)
    {
        var pending = new Stack<KeyValuePair<object, Type>>();
        var seen = new HashSet<object>(new ContinuationDataReferenceComparer());
        var fields = new Dictionary<Type, System.Reflection.FieldInfo[]>();
        var resolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
        pending.Push(new KeyValuePair<object, Type>(world, typeof(WorldState)));
        while (pending.Count > 0)
        {
            var entry = pending.Pop(); object value = entry.Key;
            if (value == null) continue;
            Type type = value.GetType(), declared = Nullable.GetUnderlyingType(entry.Value) ?? entry.Value;
            if (type != declared) return false;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid) ||
                type == typeof(Vector3) || type == typeof(Vector2) || type == typeof(Quaternion)) continue;
            if (!seen.Add(value)) continue;
            if (type.IsArray)
            {
                if (((Array)value).Rank != 1) return false;
                foreach (object item in (Array)value) pending.Push(new KeyValuePair<object, Type>(item, type.GetElementType()));
                continue;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type[] arguments = type.GetGenericArguments();
                foreach (System.Collections.DictionaryEntry item in (System.Collections.IDictionary)value)
                { pending.Push(new KeyValuePair<object, Type>(item.Key, arguments[0])); pending.Push(new KeyValuePair<object, Type>(item.Value, arguments[1])); }
                continue;
            }
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(HashSet<>)))
            {
                foreach (object item in (System.Collections.IEnumerable)value) pending.Push(new KeyValuePair<object, Type>(item, type.GetGenericArguments()[0]));
                continue;
            }
            if (!fields.TryGetValue(type, out var members))
            {
                // note: Canonical DTO contracts/constructors were audited as plain data. Reject runtime subtypes, hooks, getters and foreign records before any early serializer invocation.
                if (type.Assembly != typeof(WorldState).Assembly || type.BaseType != typeof(object) || type.TypeInitializer != null ||
                    type.GetMethod(nameof(GetHashCode), Type.EmptyTypes)?.DeclaringType != typeof(object) ||
                    type.GetMethod(nameof(Equals), new[] { typeof(object) })?.DeclaringType != typeof(object)) return false;
                var contract = resolver.ResolveContract(type) as Newtonsoft.Json.Serialization.JsonObjectContract;
                if (contract == null || contract.Converter != null || contract.ExtensionDataGetter != null || contract.ExtensionDataSetter != null ||
                    contract.OnSerializingCallbacks.Count + contract.OnSerializedCallbacks.Count + contract.OnDeserializingCallbacks.Count +
                    contract.OnDeserializedCallbacks.Count + contract.OnErrorCallbacks.Count != 0) return false;
                var collected = new List<System.Reflection.FieldInfo>();
                foreach (var property in contract.Properties)
                {
                    if (property.Ignored) continue;
                    var field = property.DeclaringType.GetField(property.UnderlyingName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (field == null || property.ShouldSerialize != null || property.GetIsSpecified != null || property.SetIsSpecified != null ||
                        property.Converter != null && property.Converter.GetType() != typeof(Vector3JsonConverter) && property.Converter.GetType() != typeof(Vector2JsonConverter) &&
                        property.Converter.GetType() != typeof(QuaternionJsonConverter)) return false;
                    collected.Add(field);
                }
                members = collected.ToArray(); fields.Add(type, members);
            }
            foreach (var field in members) pending.Push(new KeyValuePair<object, Type>(field.GetValue(value), field.FieldType));
        }
        return true;
    }

    internal bool TryPrepareContinuationSnapshot(WorldState expectedWorld, PlayerState pairedPlayer,
        GeneratedSpatialContinuationV2Record continuation, out PreparedContinuationSnapshot snapshot, out string failure,
        YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction = null,
        PreparedContinuationBaseWorld baseWorld = null)
    {
        // note: Measure this existing owner without changing its callbacks or rejection gates.
        using var publicationTiming = FrontierTiming.Begin(FrontierTiming.Operation.WorldPrepare);
        snapshot = null;
        failure = string.Empty;
        try
        {
            if (!ReferenceEquals(State, expectedWorld) || expectedWorld == null || pairedPlayer == null ||
                expectedWorld.schemaVersion != YQStateContract.CurrentStateSchemaVersion ||
                expectedWorld.generatedWorldPlan?.spatialPlanV2 == null || expectedWorld.identityRecords == null ||
                continuation == null || continuation.state != YQSpatialContinuationStateV2.Accepted ||
                expectedWorld.stateRevision < 0 || expectedWorld.stateRevision == long.MaxValue)
            {
                failure = "Continuation snapshot requires the current canonical world and an accepted construction envelope.";
                return false;
            }
            string sourceJson;
            WorldState detached;
            // note: Consume only a private base copy of byte-identical current inputs. Changed or unsupported preparation retries the original synchronous factory.
            if (baseWorld == null || !baseWorld.TryTake(this, expectedWorld, out sourceJson, out detached))
            {
                sourceJson = JsonConvert.SerializeObject(expectedWorld, JsonSettings);
                detached = JsonConvert.DeserializeObject<WorldState>(sourceJson, JsonSettings);
                if (!YQProfileCommitStore.JsonEqualsCaptured(detached, sourceJson, JsonSettings))
                {
                    failure = "The world snapshot requires normalization before continuation staging; existing document fields were preserved.";
                    return false;
                }
            }
            // note: Only the accepted continuation and its appended identities change; normalization or migration of the base document is forbidden here.
            detached.generatedWorldPlan.spatialPlanV2.acceptedContinuation =
                JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(JsonConvert.SerializeObject(continuation, JsonSettings), JsonSettings);
            YQSpatialContinuationStateReferences.RegisterAccepted(detached);
            YQIdentityValidationResult validation = YQStateReferenceValidator.Validate(pairedPlayer, detached);
            if (!validation.IsValid)
            {
                failure = "Continuation reference validation failed: " + string.Join("; ", validation.failures);
                return false;
            }
            // note: Construction already compiled this precise candidate. Reuse only its private deep-input proof; absent or changed inputs still receive full compiler admission.
            if (!(construction?.HasPreparedCandidate(detached.generatedWorldPlan) ?? false) &&
                !YQSpatialMaterializationCompilerV2.TryPrepare(detached.generatedWorldPlan, out _, out failure))
                return false;
            int originalCount = expectedWorld.identityRecords.Count;
            if (detached.identityRecords.Count < originalCount)
            {
                failure = "Continuation staging removed existing identity metadata.";
                return false;
            }
            for (int index = 0; index < originalCount; index++)
            {
                string expectedIdentityJson = JsonConvert.SerializeObject(expectedWorld.identityRecords[index], JsonSettings);
                if (!YQProfileCommitStore.JsonEqualsCaptured(detached.identityRecords[index],
                        expectedIdentityJson, JsonSettings))
                {
                    failure = "Continuation staging changed existing identity metadata.";
                    return false;
                }
            }
            var appended = detached.identityRecords.GetRange(originalCount, detached.identityRecords.Count - originalCount);
            detached.Touch(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            // note: Publication appends locations without replacing already live payload objects used by streamed providers and casts.
            if (!TryPreserveContinuationLocationReferences(expectedWorld.generatedWorldPlan.spatialPlanV2.acceptedContinuation,
                    detached.generatedWorldPlan.spatialPlanV2.acceptedContinuation, out failure)) return false;
            snapshot = new PreparedContinuationSnapshot(this, expectedWorld, detached, sourceJson,
                JsonConvert.SerializeObject(detached, JsonSettings), appended);
            if (TryValidateContinuationSnapshot(snapshot, out failure)) return true;
            snapshot = null;
            return false;
        }
        catch (Exception exception)
        {
            snapshot = null;
            failure = "Continuation snapshot failed: " + exception.Message;
            return false;
        }
    }

    internal bool TryValidateContinuationSnapshot(PreparedContinuationSnapshot snapshot, out string failure)
    {
        // note: Measure this existing owner without changing its callbacks or rejection gates.
        using var publicationTiming = FrontierTiming.Begin(FrontierTiming.Operation.WorldEquality);
        failure = string.Empty;
        if (snapshot == null || !ReferenceEquals(snapshot.owner, this) || !ReferenceEquals(State, snapshot.source) ||
            !ReferenceEquals(State.generatedWorldPlan, snapshot.plan) || !ReferenceEquals(snapshot.plan.spatialPlanV2, snapshot.artifact) ||
            !ReferenceEquals(snapshot.artifact.acceptedContinuation, snapshot.priorContinuation) ||
            !ReferenceEquals(State.identityRecords, snapshot.identities) ||
            !YQProfileCommitStore.JsonEqualsCaptured(State, snapshot.sourceJson, JsonSettings))
        {
            failure = "The world document, base artifact, continuation, or identity metadata changed after staging.";
            return false;
        }
        return true;
    }

    private static bool TryPreserveContinuationLocationReferences(GeneratedSpatialContinuationV2Record previous,
        GeneratedSpatialContinuationV2Record next, out string failure)
    {
        failure = string.Empty;
        var oldLocations = previous?.locations;
        if (oldLocations == null) return true;
        var nextLocations = next?.locations;
        if (nextLocations == null || nextLocations.Count < oldLocations.Count)
        { failure = "Continuation publication must preserve the accepted location prefix."; return false; }
        // note: Complete body comparison precedes reference replacement, so a rejected candidate remains wholly detached.
        for (int index = 0; index < oldLocations.Count; index++)
        {
            string expectedLocationJson = JsonConvert.SerializeObject(oldLocations[index], JsonSettings);
            if (!YQProfileCommitStore.JsonEqualsCaptured(nextLocations[index],
                    expectedLocationJson, JsonSettings))
            { failure = "Continuation publication changed a previously accepted location."; return false; }
        }
        for (int index = 0; index < oldLocations.Count; index++) nextLocations[index] = oldLocations[index];
        return true;
    }

    internal void ReserveContinuationPublication(PreparedContinuationSnapshot snapshot)
    {
        // note: Allocate before disk publication so the subsequent in-place authority update has no callbacks or acceptance work.
        int required = checked(snapshot.identities.Count + snapshot.appendedIdentities.Count);
        if (snapshot.identities.Capacity < required) snapshot.identities.Capacity = required;
    }

    internal void ApplyPublishedContinuation(PreparedContinuationSnapshot snapshot)
    {
        // note: Retain all canonical document/list references and concurrent gameplay collections; only the committed continuation delta becomes authoritative.
        snapshot.artifact.acceptedContinuation = snapshot.acceptedContinuation;
        snapshot.identities.AddRange(snapshot.appendedIdentities);
        snapshot.source.stateRevision = snapshot.revision;
        snapshot.source.lastUpdatedUnix = snapshot.updatedUnix;
    }

    private bool TryLoadState(string path, out WorldState loaded, out string failure)
    {
        loaded = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            if (TryDecodeLoadedDocument(File.ReadAllText(path), out loaded, out failure)) return true;
            Debug.LogError("[WorldStateManager] Load rejected: " + failure);
            return false;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            Debug.LogWarning("[WorldStateManager] Save file could not be read: " + Path.GetFileName(path) + "\n" + exception);
            return false;
        }
    }

    internal static bool TryPrepareLoadedDocument(string json, out WorldState loaded, out string failure)
    {
        if (!TryDecodeLoadedDocument(json, out loaded, out failure)) return false;
        // note: Normalize once on the detached worker document, before its paired owner permits installation.
        try { NormalizeLoadedState(loaded); return true; }
        catch (Exception exception) { failure = exception.Message; loaded = null; return false; }
    }

    private static bool TryDecodeLoadedDocument(string json, out WorldState loaded, out string failure)
    {
        // note: Only detached document values enter this migration/normalization kernel; no singleton, filesystem write, or Unity object is touched by a profile's decoding worker.
        loaded = null; failure = string.Empty;
        try
        {
            // note: Keep deserialization local until the complete persisted world has passed JSON parsing.
            loaded = JsonConvert.DeserializeObject<WorldState>(json, JsonSettings);
            YQMigrationResult migration = null;
            if (loaded == null || !YQStateMigrations.TryMigrate(loaded, out migration))
            {
                failure = loaded == null ? "World record was null." : migration.message;
                return false;
            }
            return true;
        }
        catch (Exception exception) { failure = exception.Message; return false; }
    }

    internal void InstallPreparedProfileDocument(WorldState prepared)
    {
        // note: The paired profile owner has already validated identity, schema, complete documents and the old-player writer barrier before this main-thread handoff.
        if (prepared == null || prepared.schemaVersion != YQStateContract.CurrentStateSchemaVersion)
            throw new InvalidOperationException("Prepared profile document is unavailable or unsupported.");
        State = prepared;
        LastLoadStatus = "loaded";
    }

    private void WriteAtomically(string json)
    {
        // note: Keep filesystem commit logic independently testable without creating another world-state manager or touching a profile.
        WriteWorldDocument(SavePath, json);
    }

    internal static void WriteWorldDocument(string path, string json)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, json);

        try
        {
            if (File.Exists(path))
            {
                // note: The previous accepted world is retained as a backup before committing the new one.
                File.Replace(temporaryPath, path, path + ".bak", true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // note: An unsupported atomic commit is failure, not permission to overwrite the last accepted save non-atomically.
            throw new IOException("This filesystem does not support atomic world-save replacement; the accepted save was preserved.");
        }
    }

    private void NormalizeState()
    {
        State ??= WorldState.CreateDefault();
        NormalizeLoadedState(State);
    }

    private static void NormalizeLoadedState(WorldState State)
    {
        if (State == null)
            State = WorldState.CreateDefault();

        State.EnsureCollections();
        State.worldName = string.IsNullOrWhiteSpace(State.worldName) ? "YourQuest" : State.worldName.Trim();
        if (string.IsNullOrWhiteSpace(State.currentRegionId))
            State.currentRegionId = "region_unknown";
        if (string.IsNullOrWhiteSpace(State.currentRegionName))
            State.currentRegionName = "Unknown";
        State.tension = Mathf.Max(0f, State.tension);
        YQStateIdentity.EnsureWorldState(State);
    }

    public void AddCanonLine(string line)
    {
        NormalizeState();
        if (string.IsNullOrWhiteSpace(line))
            return;
        State.AppendCanon(line.Trim());
        State.TouchNow();
    }

    public void SetCurrentRegion(string regionId, string regionName = null)
    {
        NormalizeState();
        State.currentRegionId = string.IsNullOrWhiteSpace(regionId) ? "region_unknown" : regionId;
        if (regionName != null)
            State.currentRegionName = string.IsNullOrWhiteSpace(regionName) ? "Unknown" : regionName;
        State.TouchNow();
    }

    public void ReplaceState(WorldState newState)
    {
        State = newState ?? WorldState.CreateDefault();
        NormalizeState();
    }

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
    public bool DevelopmentSetNpcAffinity(string npcId, float affinity, out string result)
    {
        // note: Persisted NPC affinity is owned by this document; NPCs have no independent player-style stat/inventory state.
        WorldState.NpcRecord npc = State?.npcs?.Find(n => n != null && string.Equals(n.npcId, npcId, StringComparison.OrdinalIgnoreCase));
        if (npc == null || float.IsNaN(affinity) || float.IsInfinity(affinity) || affinity < -1 || affinity > 1)
        { result = "Known NPC ID and affinity -1..1 required."; return false; }
        float before = npc.affinityToPlayer;
        npc.affinityToPlayer = affinity;
        npc.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); State.TouchNow();
        result = "SESSION NPC " + npc.npcId + " affinityToPlayer " + before + " -> " + affinity + " normalized affinity.";
        return true;
    }
#endif

    public void SetTension(float t)
    {
        NormalizeState();
        State.tension = Mathf.Max(0f, t);
        State.ApplyFlagDelta("tension", "set", State.tension);
    }

    private void OnApplicationQuit()
    {
        // note: The profile owner performs the paired commit; this fallback protects standalone world-state scenes.
        if (YQProfileSaveSystem.Instance == null)
            TrySave(out _);
    }

    private void OnDestroy()
    {
        // note: Clear the singleton so scene reloads cannot route mutations to a destroyed world state owner.
        if (Instance == this)
            Instance = null;
    }
}
