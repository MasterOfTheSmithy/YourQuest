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

