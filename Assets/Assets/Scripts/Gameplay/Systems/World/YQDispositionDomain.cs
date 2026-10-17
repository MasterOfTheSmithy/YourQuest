using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// note: Pure validation/projection over canonical WorldState fields; this helper owns no state, save path, scheduler, or model decision.
public static class YQDispositionDomain
{
    public const string ContextSchema = "disposition_policy_v1";
    public const string IncidentType = "disposition_incident_v1";
    public const string ReportType = "disposition_report_v1";
    public const string OutcomeType = "disposition_verified_outcome_v1";
    public const string DeliveryType = "disposition_report_delivery_v1";
    public const string Prefix = "disposition:v1:";
    public const float InsultWeight = .1f, DeedWeight = .2f; // note: Initial code-owned tuning, independent of generated numbers.
    private static readonly string[] PolicyFields = { "version", "contextId", "profileId", "worldId", "locationId", "populationId", "populationNpcIds", "governingFactionId", "governingNpcIds", "aidLaw", "harmLaw", "populationAid", "populationHarm", "governingAid", "governingHarm" };
    private static readonly string[] IncidentFields = { "version", "eventId", "profileId", "worldId", "actorId", "kind", "targetNpcId", "locationId", "occurredUnix", "contextId", "contextHash", "receiptId", "span", "observers" };
    private static readonly string[] ObserverFields = { "npcId", "channel", "receiptId" };
    private static readonly string[] ReportFields = { "version", "eventId", "profileId", "worldId", "incidentId", "reporterNpcId", "recipientNpcIds", "sourceKnowledgeId", "deliveryReceiptId", "occurredUnix" };
    private static readonly string[] OutcomeFields = { "version", "producer", "receiptId", "profileId", "worldId", "actorId", "kind", "targetNpcId", "locationId", "contextId", "contextHash", "occurredUnix", "completed", "consented", "coerced", "targetHostile", "provoked", "observers" };
    private static readonly string[] DeliveryFields = { "version", "producer", "receiptId", "profileId", "worldId", "incidentId", "reporterNpcId", "recipientNpcIds", "sourceKnowledgeId", "occurredUnix", "delivered" };

    public sealed class Admission
    {
        internal readonly PlayerState player;
        internal readonly WorldState world;
        internal readonly string json, type;
        internal readonly Vector3 position;
        private Admission(PlayerState player, WorldState world, JObject payload, string type, Vector3 position)
        { this.player = player; this.world = world; this.json = Canonical(payload); this.type = type; this.position = position; }
        public string EventId => Parse(json).Value<string>("eventId");
        // note: Only the exact receiving agent creates direct-speech admission; arbitrary JSON has no public incident admission route.
        internal static Admission Insult(PlayerState player, WorldState world, string npcId, string eventId, string span, long unix)
        {
            var payload = new JObject { ["version"] = 1, ["eventId"] = eventId, ["profileId"] = player.playerId,
                ["worldId"] = world.worldIdentity.worldId, ["actorId"] = player.playerId, ["kind"] = "direct_targeted_insult",
                ["targetNpcId"] = npcId, ["locationId"] = FindNpc(world, npcId).locationId ?? "", ["occurredUnix"] = unix,
                ["contextId"] = "", ["contextHash"] = "", ["receiptId"] = eventId, ["span"] = span,
                ["observers"] = new JArray(new JObject { ["npcId"] = npcId, ["channel"] = "direct_recipient_heard", ["receiptId"] = eventId }) };
            return new Admission(player, world, payload, IncidentType, player.logicalPosition);
        }
        internal static Admission Captured(PlayerState player, WorldState world, JObject payload, string type, Vector3 position) => new Admission(player, world, payload, type, position);
    }

    public sealed class Result
    {
        public string eventId;
        public bool alreadyApplied;
        public float moralityDelta, lawfulnessDelta, populationDelta, governingDelta, personalDelta;
    }

    public static bool IsReservedFlag(string key) => key != null && key.StartsWith("disposition:", StringComparison.OrdinalIgnoreCase);
    public static string Key(string axis, params string[] ids) => Prefix + axis + ":" + string.Join(":", ids.Select(Hex));
    private static string Hex(string value) => BitConverter.ToString(Encoding.UTF8.GetBytes(value ?? "")).Replace("-", "").ToLowerInvariant();
    private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && value == value.Trim() && !value.Any(char.IsControl);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Bounded(float current, float delta)
    { if (!Finite(current) || !Finite(delta) || current < -1f || current > 1f) throw new InvalidOperationException("Disposition score is invalid."); return Math.Max(-1f, Math.Min(1f, current + delta)); }
    private static JObject Parse(string json) => JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    private static string Canonical(JObject payload)
    { return Sort(payload).ToString(Formatting.None); }
    private static JToken Sort(JToken token)
    { if (token is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Sort(p.Value)))); if (token is JArray array) return new JArray(array.Select(Sort)); return token.DeepClone(); }
    private static void Closed(JObject payload, params string[] fields)
    { if (payload == null || payload.Count != fields.Length || fields.Any(f => payload[f] == null) || payload.Properties().Any(p => !fields.Contains(p.Name))) throw new InvalidOperationException("Closed disposition schema mismatch."); }
    private static string Text(JObject payload, string field, bool empty = false)
    { if (payload[field]?.Type != JTokenType.String) throw new InvalidOperationException("String required: " + field); string value = payload.Value<string>(field); if (!empty && !Id(value)) throw new InvalidOperationException("Stable identity required: " + field); return value; }
    private static long Unix(JObject payload)
    { if (payload["occurredUnix"]?.Type != JTokenType.Integer || payload.Value<long>("occurredUnix") <= 0) throw new InvalidOperationException("Event time required."); return payload.Value<long>("occurredUnix"); }
    private static void Version(JObject payload)
    { if (payload["version"]?.Type != JTokenType.Integer || payload.Value<int>("version") != 1) throw new InvalidOperationException("Unsupported disposition version."); }
    private static void Scope(PlayerState player, WorldState world, JObject payload)
    { Version(payload); if (player == null || world == null || world.schemaVersion != 8 || player.schemaVersion != 8 || Text(payload, "profileId") != player.playerId || Text(payload, "worldId") != world.worldIdentity?.worldId || world.worldIdentity.ownerProfileId != player.playerId) throw new InvalidOperationException("Disposition profile/world mismatch."); }
    private static WorldState.NpcRecord FindNpc(WorldState world, string id)
    { var matches = world?.npcs?.Where(n => n != null && n.npcId == id).ToArray(); return matches?.Length == 1 ? matches[0] : null; }
    private static void Npc(WorldState world, string id)
    { if (!Id(id) || FindNpc(world, id) == null) throw new InvalidOperationException("Unknown or ambiguous canonical NPC."); }
    private static string[] Npcs(WorldState world, JObject payload, string field, bool allowEmpty = false)
    { if (!(payload[field] is JArray array) || array.Count > 64 || (!allowEmpty && array.Count == 0) || array.Any(t => t.Type != JTokenType.String)) throw new InvalidOperationException("Bounded NPC array required."); var ids = array.Values<string>().ToArray(); if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) throw new InvalidOperationException("Duplicate NPC identity."); foreach (var id in ids) Npc(world, id); return ids; }
    private static void Menu(JObject payload, string field, params string[] values)
    { if (!values.Contains(Text(payload, field))) throw new InvalidOperationException("Unsupported policy: " + field); }

    public static bool TryPreparePolicy(PlayerState player, WorldState world, string raw, string source, string prompt, out YQAcceptedProposal proposal, out string failure)
    {
        // note: Supported policy codes are accepted content; neither faction order nor generated ruler/law prose supplies mechanical context.
        proposal = null; failure = "Policy source/prompt required.";
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(prompt)) return false;
        try { Parse(raw); } catch (Exception error) { failure = "Strict policy JSON required: " + error.Message; return false; }
        return YQContentProposalBoundary.TryPrepare(raw, source, prompt, ContextSchema, p => ValidatePolicy(player, world, p), p => (JObject)Sort(p), p => true, out proposal, out failure);
    }
    public static string ValidatePolicy(PlayerState player, WorldState world, JObject payload)
    {
        try {
            Closed(payload, PolicyFields); Scope(player, world, payload); Text(payload, "contextId"); Text(payload, "populationId");
            string location = Text(payload, "locationId");
            if (world.locations == null || world.locations.Count(l => l != null && l.locationId == location) != 1) throw new InvalidOperationException("Canonical jurisdiction location required.");
            string faction = Text(payload, "governingFactionId");
            if (world.factions == null || world.factions.Count(f => f != null && f.factionId == faction) != 1) throw new InvalidOperationException("Explicit existing governor faction required.");
            string[] population = Npcs(world, payload, "populationNpcIds"); foreach (string npc in Npcs(world, payload, "governingNpcIds")) if (FindNpc(world, npc).factionId != faction) throw new InvalidOperationException("Governor recipient is outside accepted faction.");
            if (world.acceptedContent != null) foreach (var reference in world.acceptedContent.Where(r => r?.schemaVersion == ContextSchema)) {
                JObject old = Parse(reference.normalizedPayloadJson);
                if (old.Value<string>("populationId") == payload.Value<string>("populationId") && !((JArray)old["populationNpcIds"]).Values<string>().OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(population.OrderBy(v => v, StringComparer.Ordinal)))
                    throw new InvalidOperationException("An accepted population identity cannot change membership.");
            }
            Menu(payload, "aidLaw", "permitted", "forbidden"); Menu(payload, "harmLaw", "permitted", "forbidden");
            foreach (string field in new[] { "populationAid", "populationHarm", "governingAid", "governingHarm" }) Menu(payload, field, "approve", "disapprove", "neutral");
            return "";
        } catch (Exception error) { return error.Message; }
    }
    private static JObject Context(PlayerState player, WorldState world, string id, string hash)
    {
        var matches = world.acceptedContent?.Where(r => r != null && r.contentId == id && r.schemaVersion == ContextSchema).ToArray();
        if (matches?.Length != 1 || matches[0].kind != YQStableEntityKind.Content || matches[0].contentHash != hash || YQStateContract.Sha256Hex(matches[0].normalizedPayloadJson) != hash) throw new InvalidOperationException("Accepted immutable policy/hash required.");
        JObject policy = Parse(matches[0].normalizedPayloadJson); string error = ValidatePolicy(player, world, policy);
        if (error != "" || policy.Value<string>("contextId") != id) throw new InvalidOperationException("Accepted policy no longer resolves: " + error); return policy;
    }
    public static bool IsGovernedFaction(WorldState world, string factionId) => world?.acceptedContent != null && world.acceptedContent.Any(r => {
        if (r?.schemaVersion != ContextSchema) return false; try { return string.Equals(Parse(r.normalizedPayloadJson).Value<string>("governingFactionId"), factionId, StringComparison.OrdinalIgnoreCase); } catch { return true; } });

    public static JArray CapturePublicPolicies(WorldState world)
    {
        var publicFacts = new JArray(); if (world?.acceptedContent == null) return publicFacts;
        foreach (var reference in world.acceptedContent) {
            if (publicFacts.Count >= 4) break;
            try {
                if (reference?.schemaVersion != ContextSchema || reference.kind != YQStableEntityKind.Content || YQStateContract.Sha256Hex(reference.normalizedPayloadJson) != reference.contentHash) continue;
                JObject policy = Parse(reference.normalizedPayloadJson); Closed(policy, PolicyFields); Version(policy);
                if (policy.Value<string>("worldId") != world.worldIdentity?.worldId || policy.Value<string>("profileId") != world.worldIdentity?.ownerProfileId) continue;
                var fact = new JObject(); foreach (string field in new[] { "contextId", "locationId", "populationId", "governingFactionId", "aidLaw", "harmLaw", "populationAid", "populationHarm", "governingAid", "governingHarm" }) fact[field] = policy[field].DeepClone();
                publicFacts.Add(fact);
            } catch { /* note: Malformed content supplies no public policy fact. */ }
        }
        return publicFacts;
    }
    public static bool CanKnowGoverningStanding(WorldState world, string npcId, string factionId)
    {
        if (FindNpc(world, npcId) == null || world.eventLog == null) return false;
        foreach (var edge in world.eventLog) {
            if (edge == null || (edge.eventType != IncidentType && edge.eventType != ReportType)) continue;
            try { var payload = Parse(edge.context); string id = edge.eventType == IncidentType ? edge.eventId : payload.Value<string>("incidentId");
                if (!Knowledge(world, id, npcId, edge.eventId)) continue;
                var incident = Parse(RequireEvent(world, id, IncidentType).context);
                var policy = world.acceptedContent.SingleOrDefault(r => r != null && r.contentId == incident.Value<string>("contextId") && r.schemaVersion == ContextSchema);
                if (policy != null && Parse(policy.normalizedPayloadJson).Value<string>("governingFactionId") == factionId) return true;
            } catch { /* note: Invalid lineage never grants an audience. */ }
        }
        return false;
    }

    public static bool TryClassifyDirectInsult(string utterance, out string exactSpan)
    {
        // note: Numeric consequences still require unambiguous whole directed speech. Semantic interpretation handles richer conversation without turning reported/negated/affectionate words into penalties.
        exactSpan = null; if (string.IsNullOrWhiteSpace(utterance) || utterance.Length > 160) return false;
        const string noun = @"(?:idiot|fool|coward|cunt|bastard|asshole|arsehole|jerk|moron)";
        const string quality = @"(?:(?:a|an) (?:complete |utter |pathetic |worthless )?" + noun + @"|(?:(?:utterly|completely) )?(?:pathetic|worthless|disgusting|useless))";
        var match = Regex.Match(utterance, @"\A\s*(?:you(?: are|'re|’re) " + quality + @"|you (?:complete |utter |pathetic |worthless )?" + noun + @"|(?:fuck|screw|f[ *]+ck) you(?:[, ]+(?:you )?" + noun + @")?)[.!]*\s*\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false; exactSpan = utterance.Trim().TrimEnd('.', '!'); return true;
    }
    internal static bool TryCaptureDeliveredInsult(PlayerState player, WorldState world, string npcId, string utterance, string eventId, out Admission admission)
    {
        admission = null; if (!TryClassifyDirectInsult(utterance, out string span) || player == null || world == null || FindNpc(world, npcId) == null || world.worldIdentity?.ownerProfileId != player.playerId || !Id(eventId)) return false;
        admission = Admission.Insult(player, world, npcId, eventId, span, DateTimeOffset.UtcNow.ToUnixTimeSeconds()); return true;
    }
    public static bool TryCaptureVerifiedOutcome(PlayerState player, WorldState world, string receiptId, out Admission admission, out string failure)
    {
        // note: No inspected current producer supplies this contract. A future actual completed domain producer must emit it, never a transcript or generic verb counter.
        admission = null; failure = "Verified completed domain outcome is unavailable.";
        try {
            var source = RequireEvent(world, receiptId, OutcomeType); JObject receipt = Parse(source.context); Closed(receipt, OutcomeFields); Scope(player, world, receipt); Unix(receipt);
            if (Text(receipt, "producer") != "YQDispositionOutcomeProducer" || Text(receipt, "receiptId") != source.eventId || source.mutationCommitKey != Key("outcome", source.eventId) || !(world.appliedMutationCommitKeys?.Contains(source.mutationCommitKey) ?? false) || source.actorId != player.playerId || Text(receipt, "actorId") != player.playerId) throw new InvalidOperationException("Exact domain-owned outcome receipt required.");
            string kind = Text(receipt, "kind"); Npc(world, Text(receipt, "targetNpcId"));
            if (source.occurredUnix != Unix(receipt) || source.logicalLocationId != receipt.Value<string>("locationId") || source.targetIds == null || source.targetIds.Count != 1 || source.targetIds[0] != receipt.Value<string>("targetNpcId")) throw new InvalidOperationException("Outcome envelope identity/time/target mismatch.");
            foreach (string field in new[] { "completed", "consented", "coerced", "targetHostile", "provoked" }) if (receipt[field]?.Type != JTokenType.Boolean) throw new InvalidOperationException("Typed outcome prerequisite required.");
            if (!receipt.Value<bool>("completed") || (kind == "aid_consented_no_coercion" ? !receipt.Value<bool>("consented") || receipt.Value<bool>("coerced") : kind != "harm_unprovoked_nonhostile" || receipt.Value<bool>("targetHostile") || receipt.Value<bool>("provoked"))) throw new InvalidOperationException("Unsupported or uncertain ethical outcome.");
            JObject context = Context(player, world, Text(receipt, "contextId"), Text(receipt, "contextHash")); if (Text(receipt, "locationId") != context.Value<string>("locationId")) throw new InvalidOperationException("Outcome jurisdiction mismatch.");
            ValidateObservers(world, receipt["observers"], receiptId, false);
            var payload = new JObject { ["version"] = 1, ["eventId"] = "incident:" + receiptId, ["profileId"] = player.playerId, ["worldId"] = world.worldIdentity.worldId,
                ["actorId"] = player.playerId, ["kind"] = kind, ["targetNpcId"] = receipt["targetNpcId"].DeepClone(), ["locationId"] = receipt["locationId"].DeepClone(), ["occurredUnix"] = receipt["occurredUnix"].DeepClone(),
                ["contextId"] = receipt["contextId"].DeepClone(), ["contextHash"] = receipt["contextHash"].DeepClone(), ["receiptId"] = receiptId, ["span"] = "", ["observers"] = receipt["observers"].DeepClone() };
            admission = Admission.Captured(player, world, payload, IncidentType, source.logicalPosition); failure = ""; return true;
        } catch (Exception error) { failure = error.Message; return false; }
    }
    public static bool TryCaptureDeliveredReport(PlayerState player, WorldState world, string receiptId, out Admission admission, out string failure)
    {
        admission = null; failure = "Delivered NPC report receipt is unavailable.";
        try {
            var source = RequireEvent(world, receiptId, DeliveryType); var receipt = Parse(source.context); Closed(receipt, DeliveryFields); Scope(player, world, receipt); Unix(receipt);
            if (Text(receipt, "producer") != "YQDispositionReportDelivery" || Text(receipt, "receiptId") != source.eventId || source.mutationCommitKey != Key("delivery", source.eventId) || !(world.appliedMutationCommitKeys?.Contains(source.mutationCommitKey) ?? false) || receipt["delivered"]?.Type != JTokenType.Boolean || !receipt.Value<bool>("delivered")) throw new InvalidOperationException("Exact successful report delivery receipt required.");
            Npc(world, Text(receipt, "reporterNpcId")); Npcs(world, receipt, "recipientNpcIds");
            var payload = new JObject { ["version"] = 1, ["eventId"] = "report:" + receiptId, ["profileId"] = player.playerId, ["worldId"] = world.worldIdentity.worldId,
                ["incidentId"] = receipt["incidentId"].DeepClone(), ["reporterNpcId"] = receipt["reporterNpcId"].DeepClone(), ["recipientNpcIds"] = receipt["recipientNpcIds"].DeepClone(), ["sourceKnowledgeId"] = receipt["sourceKnowledgeId"].DeepClone(), ["deliveryReceiptId"] = receiptId, ["occurredUnix"] = receipt["occurredUnix"].DeepClone() };
            if (!Knowledge(world, Text(payload, "incidentId"), Text(payload, "reporterNpcId"), Text(payload, "sourceKnowledgeId"))) throw new InvalidOperationException("Reporter lacks retained observation/report lineage.");
            var knowledge = world.eventLog.Single(e => e != null && e.eventId == payload.Value<string>("sourceKnowledgeId"));
            if (source.occurredUnix != Unix(payload) || knowledge.occurredUnix > Unix(payload)) throw new InvalidOperationException("Report delivery chronology mismatch.");
            if (source.actorId != payload.Value<string>("reporterNpcId") || source.targetIds == null || !source.targetIds.SequenceEqual(((JArray)payload["recipientNpcIds"]).Values<string>())) throw new InvalidOperationException("Report envelope delivery targets disagree.");
            admission = Admission.Captured(player, world, payload, ReportType, source.logicalPosition); failure = ""; return true;
        } catch (Exception error) { failure = error.Message; return false; }
    }
    private static YQEventEnvelope RequireEvent(WorldState world, string id, string type)
    { var matches = world?.eventLog?.Where(e => e != null && e.eventId == id && e.eventType == type).ToArray(); if (matches?.Length != 1) throw new InvalidOperationException("Unique canonical source receipt required."); return matches[0]; }
    private static string[] ValidateObservers(WorldState world, JToken token, string receiptId, bool direct)
    {
        if (!(token is JArray array) || array.Count > 64) throw new InvalidOperationException("Bounded observation receipts required."); var ids = new List<string>();
        foreach (var value in array) { if (!(value is JObject observer)) throw new InvalidOperationException("Observation object required."); Closed(observer, ObserverFields); string id = Text(observer, "npcId"); Npc(world, id); if (ids.Contains(id)) throw new InvalidOperationException("Duplicate observer."); ids.Add(id); Menu(observer, "channel", "direct_recipient_heard", "visual_witness", "auditory_witness"); if (Text(observer, "receiptId") != receiptId || (direct && observer.Value<string>("channel") != "direct_recipient_heard")) throw new InvalidOperationException("Event-time producer observation proof required."); }
        return ids.ToArray();
    }
    private static bool Knowledge(WorldState world, string incidentId, string npcId, string edgeId)
    {
        return Knowledge(world, incidentId, npcId, edgeId, new HashSet<string>(StringComparer.Ordinal));
    }
    private static bool Knowledge(WorldState world, string incidentId, string npcId, string edgeId, HashSet<string> lineage)
    {
        // note: Reloaded report edges must still terminate at a real captured observation; malformed/cyclic/over-budget lineage supplies no knowledge.
        try {
            if (lineage.Count >= 64 || !lineage.Add(edgeId)) return false;
            var edge = world.eventLog?.SingleOrDefault(e => e != null && e.eventId == edgeId); if (edge == null) return false; var p = Parse(edge.context); Version(p); Unix(p);
            if (p.Value<string>("profileId") != world.worldIdentity?.ownerProfileId || p.Value<string>("worldId") != world.worldIdentity?.worldId || p.Value<string>("eventId") != edgeId || edge.occurredUnix != Unix(p)) return false;
            if (edge.eventType == IncidentType && edgeId == incidentId) {
                Closed(p, IncidentFields);
                if (edge.actorId != p.Value<string>("actorId") || edge.mutationCommitKey != Key("incident", edgeId) || !world.appliedMutationCommitKeys.Contains(edge.mutationCommitKey)) return false;
                return ValidateObservers(world, p["observers"], Text(p, "receiptId"), p.Value<string>("kind") == "direct_targeted_insult").Contains(npcId);
            }
            if (edge.eventType != ReportType) return false; Closed(p, ReportFields);
            if (p.Value<string>("incidentId") != incidentId || edge.actorId != p.Value<string>("reporterNpcId") || edge.mutationCommitKey != Key("report", edgeId) || !world.appliedMutationCommitKeys.Contains(edge.mutationCommitKey) || !Npcs(world, p, "recipientNpcIds").Contains(npcId)) return false;
            var delivery = RequireEvent(world, Text(p, "deliveryReceiptId"), DeliveryType); var proof = Parse(delivery.context); Closed(proof, DeliveryFields); Version(proof);
            if (proof.Value<string>("producer") != "YQDispositionReportDelivery" || proof["delivered"]?.Type != JTokenType.Boolean || !proof.Value<bool>("delivered") || delivery.mutationCommitKey != Key("delivery", delivery.eventId) || !world.appliedMutationCommitKeys.Contains(delivery.mutationCommitKey) || delivery.actorId != edge.actorId || delivery.occurredUnix != edge.occurredUnix || proof.Value<string>("receiptId") != delivery.eventId || proof.Value<string>("profileId") != p.Value<string>("profileId") || proof.Value<string>("worldId") != p.Value<string>("worldId") || proof.Value<string>("incidentId") != incidentId || proof.Value<string>("reporterNpcId") != edge.actorId || proof.Value<string>("sourceKnowledgeId") != p.Value<string>("sourceKnowledgeId") || !JToken.DeepEquals(proof["recipientNpcIds"], p["recipientNpcIds"])) return false;
            var parent = world.eventLog.Single(e => e != null && e.eventId == p.Value<string>("sourceKnowledgeId"));
            return parent.occurredUnix <= edge.occurredUnix && Knowledge(world, incidentId, p.Value<string>("reporterNpcId"), parent.eventId, lineage);
        } catch { return false; }
    }
    private static readonly JsonSerializerSettings SnapshotSettings = new JsonSerializerSettings {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
    public static bool TryPrepare(PlayerState player, WorldState sourceWorld, Admission admission, out WorldState preparedWorld, out Result result, out string failure)
    {
        // note: Preparation clones the world; retained admission belongs to exact live owners but never mutates them during preparation.
        result = null; preparedWorld = null; failure = "Admission required.";
        try {
            // note: Even a rejected numeric/lineage check cannot partially mutate the caller's live world.
            WorldState world = JsonConvert.DeserializeObject<WorldState>(JsonConvert.SerializeObject(sourceWorld, SnapshotSettings), SnapshotSettings);
            if (admission == null || admission.player.playerId != player?.playerId || admission.world.worldIdentity?.worldId != world?.worldIdentity?.worldId) return false;
            if (!Finite(admission.position.x) || !Finite(admission.position.y) || !Finite(admission.position.z)) throw new InvalidOperationException("Finite event-time position required.");
            if (world.globalFlags != null && world.globalFlags.Any(f => IsReservedFlag(f.Key) && (!Finite(f.Value) || f.Value < -1f || f.Value > 1f))) throw new InvalidOperationException("Existing disposition score is invalid.");
            JObject payload = Parse(admission.json); Scope(player, world, payload); Unix(payload); string eventId = Text(payload, "eventId"); result = new Result { eventId = eventId };
            var existing = world.eventLog?.Where(e => e != null && e.eventId == eventId).ToArray();
            if (existing?.Length > 0) { if (existing.Length != 1 || existing[0].eventType != admission.type || existing[0].context != admission.json || !existing[0].logicalPosition.Equals(admission.position) || !(world.appliedMutationCommitKeys?.Contains(existing[0].mutationCommitKey) ?? false)) throw new InvalidOperationException("Conflicting incident/report identity."); result.alreadyApplied = true; preparedWorld = world; failure = ""; return true; }
            world.EnsureCollections(); JObject policy = null, incident = payload; string[] aware; string target = "";
            if (admission.type == IncidentType) {
                Closed(payload, IncidentFields); if (Text(payload, "actorId") != player.playerId) throw new InvalidOperationException("Actor mismatch."); target = Text(payload, "targetNpcId"); Npc(world, target); string kind = Text(payload, "kind");
                bool insult = kind == "direct_targeted_insult"; aware = ValidateObservers(world, payload["observers"], Text(payload, "receiptId"), insult);
                if (insult) { if (!TryClassifyDirectInsult(Text(payload, "span"), out _) || aware.Length != 1 || aware[0] != target || Text(payload, "contextId", true) != "" || Text(payload, "contextHash", true) != "" || Text(payload, "receiptId") != eventId || Text(payload, "locationId", true) != (FindNpc(world, target).locationId ?? "")) throw new InvalidOperationException("Exact delivered direct insult required."); result.personalDelta = -InsultWeight; }
                else {
                    if (!TryCaptureVerifiedOutcome(player, world, Text(payload, "receiptId"), out var recaptured, out string why) || recaptured.json != admission.json || !recaptured.position.Equals(admission.position)) throw new InvalidOperationException("Outcome source changed: " + why);
                    policy = Context(player, world, Text(payload, "contextId"), Text(payload, "contextHash")); bool aid = kind == "aid_consented_no_coercion";
                    result.moralityDelta = aid ? DeedWeight : -DeedWeight; result.lawfulnessDelta = policy.Value<string>(aid ? "aidLaw" : "harmLaw") == "forbidden" ? -DeedWeight : 0f;
                    AddScore(world, Key("morality", player.playerId), result.moralityDelta); AddScore(world, Key("lawfulness", policy.Value<string>("locationId"), player.playerId), result.lawfulnessDelta);
                    if (aware.Contains(target)) result.personalDelta = aid ? DeedWeight : -DeedWeight;
                }
                if (result.personalDelta != 0f) { var npc = FindNpc(world, target); npc.affinityToPlayer = Bounded(npc.affinityToPlayer, result.personalDelta); npc.updatedUnix = Math.Max(npc.updatedUnix, Unix(payload)); }
            } else if (admission.type == ReportType) {
                Closed(payload, ReportFields); if (!TryCaptureDeliveredReport(player, world, Text(payload, "deliveryReceiptId"), out var recaptured, out string why) || recaptured.json != admission.json || !recaptured.position.Equals(admission.position)) throw new InvalidOperationException("Report source changed: " + why);
                incident = Parse(RequireEvent(world, Text(payload, "incidentId"), IncidentType).context); if (Unix(payload) < Unix(incident)) throw new InvalidOperationException("Report precedes incident."); aware = Npcs(world, payload, "recipientNpcIds");
                if (incident.Value<string>("contextId") != "") policy = Context(player, world, Text(incident, "contextId"), Text(incident, "contextHash"));
            } else throw new InvalidOperationException("Unsupported admission type.");
            if (policy != null) ApplyAwareness(player, world, incident, policy, aware, result);
            string key = Key(admission.type == IncidentType ? "incident" : "report", eventId); if (world.appliedMutationCommitKeys.Contains(key)) throw new InvalidOperationException("Consequence key has no matching event.");
            world.appliedMutationCommitKeys.Add(key); world.Touch(Math.Max(world.lastUpdatedUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds())); world.eventLog.Add(new YQEventEnvelope { eventId = eventId, eventType = admission.type, actorId = admission.type == IncidentType ? player.playerId : payload.Value<string>("reporterNpcId"),
                targetIds = aware.ToList(), logicalLocationId = incident.Value<string>("locationId"), logicalPosition = admission.position, context = admission.json, outcome = JsonConvert.SerializeObject(result), mutationCommitKey = key, occurredUnix = Unix(payload), stateRevision = world.stateRevision });
            preparedWorld = world; failure = ""; return true;
        } catch (Exception error) { result = null; failure = error.Message; return false; }
    }
    private static void AddScore(WorldState world, string key, float delta)
    { if (delta == 0f) return; world.globalFlags.TryGetValue(key, out float current); world.globalFlags[key] = Bounded(current, delta); }
    public static JObject CapturePlayerStanding(PlayerState player, WorldState world, string contextId = null)
    {
        // note: Player-facing values are independent axes. Unknown polity context stays unknown; this view is not fed to unscoped NPC prompts.
        var view = new JObject { ["available"] = false, ["contextKnown"] = false };
        try {
            if (player == null || world?.worldIdentity?.ownerProfileId != player.playerId) return view;
            float Score(string key) { float value = world.globalFlags != null && world.globalFlags.TryGetValue(key, out float found) ? found : 0f; Bounded(value, 0f); return value; }
            view["objectiveMorality"] = Score(Key("morality", player.playerId));
            if (!string.IsNullOrWhiteSpace(contextId)) {
                var reference = world.acceptedContent.Single(r => r != null && r.contentId == contextId && r.schemaVersion == ContextSchema);
                var policy = Context(player, world, contextId, reference.contentHash);
                string factionId = policy.Value<string>("governingFactionId"); var faction = world.factions.Single(f => f != null && f.factionId == factionId);
                float governing = world.factionAttitudes.TryGetValue(factionId, out float value) ? value : faction.attitudeToPlayer; Bounded(governing, 0f);
                if (governing != faction.attitudeToPlayer) throw new InvalidOperationException("Governing standing projections disagree.");
                view["jurisdictionId"] = policy["locationId"].DeepClone(); view["populationId"] = policy["populationId"].DeepClone(); view["governingFactionId"] = factionId;
                view["jurisdictionLawfulness"] = Score(Key("lawfulness", policy.Value<string>("locationId"), player.playerId));
                view["populationStanding"] = Score(Key("population", policy.Value<string>("populationId"), player.playerId)); view["governingFactionStanding"] = governing; view["contextKnown"] = true;
            }
            view["available"] = true; return view;
        } catch { return new JObject { ["available"] = false, ["contextKnown"] = false }; }
    }
    private static float PolicyDelta(string value) => value == "approve" ? DeedWeight : value == "disapprove" ? -DeedWeight : 0f;
    private static void ApplyAwareness(PlayerState player, WorldState world, JObject incident, JObject policy, string[] aware, Result result)
    {
        bool aid = incident.Value<string>("kind") == "aid_consented_no_coercion"; string id = incident.Value<string>("eventId");
        string populationKey = Key("population_effect", id, policy.Value<string>("populationId"));
        if (Npcs(world, policy, "populationNpcIds").Any(aware.Contains) && !world.appliedMutationCommitKeys.Contains(populationKey)) {
            result.populationDelta = PolicyDelta(policy.Value<string>(aid ? "populationAid" : "populationHarm")); AddScore(world, Key("population", policy.Value<string>("populationId"), player.playerId), result.populationDelta); world.appliedMutationCommitKeys.Add(populationKey);
        }
        string factionId = policy.Value<string>("governingFactionId"), governingKey = Key("governing_effect", id, factionId);
        if (Npcs(world, policy, "governingNpcIds").Any(aware.Contains) && !world.appliedMutationCommitKeys.Contains(governingKey)) {
            result.governingDelta = PolicyDelta(policy.Value<string>(aid ? "governingAid" : "governingHarm")); var faction = world.factions.Single(f => f != null && f.factionId == factionId);
            float current = world.factionAttitudes.TryGetValue(factionId, out float existing) ? existing : faction.attitudeToPlayer;
            if (!Finite(faction.attitudeToPlayer) || (world.factionAttitudes.ContainsKey(factionId) && existing != faction.attitudeToPlayer)) throw new InvalidOperationException("Faction standing projections disagree.");
            float next = Bounded(current, result.governingDelta); world.factionAttitudes[factionId] = next; faction.attitudeToPlayer = next; faction.updatedUnix = Math.Max(faction.updatedUnix, Unix(incident)); world.appliedMutationCommitKeys.Add(governingKey);
        }
    }
    public static JArray CaptureNpcKnowledge(WorldState world, string npcId, int maximum = 6)
    {
        // note: Only explicit witnessed/delivered edges expose an incident. Group membership, score values and transcript claims never create knowledge.
        var facts = new JArray(); if (FindNpc(world, npcId) == null || world.eventLog == null) return facts;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = world.eventLog.Count - 1; index >= 0 && facts.Count < Math.Max(0, Math.Min(6, maximum)); index--) {
            var edge = world.eventLog[index]; if (edge == null || (edge.eventType != IncidentType && edge.eventType != ReportType)) continue;
            try { var p = Parse(edge.context); string id = edge.eventType == IncidentType ? edge.eventId : p.Value<string>("incidentId"); if (!Knowledge(world, id, npcId, edge.eventId) || !seen.Add(id)) continue;
                var original = Parse(RequireEvent(world, id, IncidentType).context); facts.Add(new JObject { ["kind"] = original["kind"].DeepClone(), ["knowledge"] = Knowledge(world, id, npcId, id) ? "witnessed" : "reported",
                    ["actorId"] = original["actorId"].DeepClone(), ["targetNpcId"] = original["targetNpcId"].DeepClone(), ["exactPlayerSpan"] = original["span"].DeepClone(), ["occurredUnix"] = original["occurredUnix"].DeepClone() });
            } catch { /* note: Malformed legacy/unrecognized event data supplies no NPC knowledge. */ }
        }
        return facts;
    }
}

// note: Typed bounded projection retains WorldState and existing NPC/faction objects; allocations and target binding occur before pointer publication.
public sealed class YQPreparedDispositionWorld
{
    private readonly WorldState prepared;
    private readonly WorldState.NpcRecord[] npcTargets, npcSources;
    private readonly WorldState.FactionRecord[] factionTargets, factionSources;
    public YQPreparedDispositionWorld(WorldState target, WorldState prepared)
    {
        this.prepared = prepared;
        npcSources = prepared.npcs.Where(n => n != null).ToArray(); npcTargets = npcSources.Select(n => target.npcs.Single(t => t != null && t.npcId == n.npcId)).ToArray();
        factionSources = prepared.factions.Where(f => f != null).ToArray(); factionTargets = factionSources.Select(f => target.factions.Single(t => t != null && t.factionId == f.factionId)).ToArray();
    }
    public void Apply(WorldState target)
    {
        target.globalFlags = prepared.globalFlags; target.factionAttitudes = prepared.factionAttitudes;
        target.acceptedContent = prepared.acceptedContent; target.eventLog = prepared.eventLog; target.appliedMutationCommitKeys = prepared.appliedMutationCommitKeys;
        for (int index = 0; index < npcTargets.Length; index++) { npcTargets[index].affinityToPlayer = npcSources[index].affinityToPlayer; npcTargets[index].updatedUnix = npcSources[index].updatedUnix; }
        for (int index = 0; index < factionTargets.Length; index++) { factionTargets[index].attitudeToPlayer = factionSources[index].attitudeToPlayer; factionTargets[index].updatedUnix = factionSources[index].updatedUnix; }
        target.stateRevision = prepared.stateRevision; target.lastUpdatedUnix = prepared.lastUpdatedUnix;
    }
}
