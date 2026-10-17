using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// note: This adapter supplies bounded producers to existing content/inventory/social owners; it owns no save or mutable gameplay state.
public static class YQSocialAidDomain
{
    public const string RequestSchema = "npc_voluntary_aid_v1";
    private static readonly string[] RequestFields = { "version", "requestId", "profileId", "worldId", "npcId", "locationId", "contextId", "contextHash", "purpose", "templateId", "itemContractHash", "quantity", "requestLine" };
    internal sealed class AidDelivery
    {
        internal readonly PlayerState player; internal readonly WorldState world;
        internal readonly string requestId, requestHash, itemId; internal readonly long playerRevision, inventoryRevision, occurredUnix;
        internal readonly Vector3 position; internal readonly Func<bool> current;
        internal AidDelivery(PlayerState p, WorldState w, string id, string hash, string item, long revision, long inventory, Vector3 pos, Func<bool> guard)
        { player = p; world = w; requestId = id; requestHash = hash; itemId = item; playerRevision = revision; inventoryRevision = inventory; position = pos; current = guard; occurredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
    }
    internal sealed class ReportDelivery
    {
        internal readonly PlayerState player; internal readonly WorldState world;
        internal readonly string receiptId, incidentId, reporterId, recipientId, knowledgeId;
        internal readonly long occurredUnix; internal readonly Vector3 position; internal readonly Func<bool> current;
        internal ReportDelivery(PlayerState p, WorldState w, string incident, string reporter, string recipient, string knowledge, Vector3 pos, Func<bool> guard)
        {
            player = p; world = w; incidentId = incident; reporterId = reporter; recipientId = recipient; knowledgeId = knowledge; position = pos; current = guard;
            // note: Retelling the same retained incident to the same recipient is one delivery across turns/reloads; a new guard cannot create another knowledge edge.
            receiptId = "npc-report:" + YQStateContract.Sha256Hex(YQDispositionDomain.Key("report_delivery", p.playerId, w.worldIdentity.worldId, incident, reporter, recipient, knowledge));
            occurredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
    public static string RequestId(WorldState world, string npcId) => "aid-request:" + YQStateContract.Sha256Hex(YQDispositionDomain.Key("aid_request", world.worldIdentity.worldId, npcId));
    public static string PolicyId(WorldState world, string locationId) => "social-policy:" + YQStateContract.Sha256Hex(YQDispositionDomain.Key("social_policy", world.worldIdentity.worldId, locationId));
    public static string OutcomeId(string requestId) => "aid-outcome:" + YQStateContract.Sha256Hex(requestId);
    public static string IncidentId(string requestId) => "incident:" + OutcomeId(requestId);
    internal static JObject Parse(string raw) => JObject.Parse(raw, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    internal static JToken Sort(JToken value) => value is JObject obj ? new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Sort(p.Value)))) :
        value is JArray array ? new JArray(array.Select(Sort)) : value.DeepClone();
    internal static void Closed(JObject payload, params string[] fields)
    { if (payload == null || payload.Count != fields.Length || fields.Any(f => payload[f] == null) || payload.Properties().Any(p => !fields.Contains(p.Name))) throw new InvalidOperationException("Closed aid schema required."); }
    internal static bool Stable(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 160 && value == value.Trim() && !value.Any(char.IsControl);
    internal static string Text(JObject p, string field)
    { if (p[field]?.Type != JTokenType.String || !Stable(p.Value<string>(field))) throw new InvalidOperationException("Stable string required: " + field); return p.Value<string>(field); }
    internal static void Scope(PlayerState p, WorldState w)
    { if (p?.schemaVersion != 8 || w?.schemaVersion != 8 || w.worldIdentity?.ownerProfileId != p.playerId || !Stable(w.worldIdentity.worldId)) throw new InvalidOperationException("Current schema-8 paired owners required."); }
    public static bool IsSupportedSupply(InventoryItemRecord item) => item != null && Stable(item.itemId) && Stable(item.templateId) && item.IsConsumable && !item.IsEquippable &&
        !float.IsNaN(item.moveSpeedBonus) && !float.IsInfinity(item.moveSpeedBonus) &&
        item.quantity >= 1 && item.quantity <= YQContainerInventory.MaxStack && (item.stackable || item.quantity == 1) &&
        item.healAmount >= 0 && item.restoreManaAmount >= 0 && item.restoreStaminaAmount >= 0 &&
        (item.healAmount > 0 || item.restoreManaAmount > 0 || item.restoreStaminaAmount > 0);
    public static string ItemContractHash(InventoryItemRecord item)
    {
        // note: Accepted exact mechanics/presentation bindings cannot be refreshed under a matching template name. Only instance, quantity and creation time differ.
        JObject data = JObject.FromObject(item); data.Remove("itemId"); data.Remove("quantity"); data.Remove("generatedAtUnixString");
        return YQStateContract.Sha256Hex(Sort(data).ToString(Formatting.None));
    }
    internal static WorldState.NpcRecord Npc(WorldState w, string id)
    { if (!Stable(id)) throw new InvalidOperationException("Canonical NPC required."); var matches = w.npcs?.Where(n => n?.npcId == id).ToArray(); if (matches?.Length != 1) throw new InvalidOperationException("Unique canonical NPC required."); return matches[0]; }
    public static bool IsEligibleRecipient(WorldState w, PlayerState p, string id)
    {
        try {
            Scope(p, w); Npc(w, id);
            var plans = w.generatedWorldPlan?.generatedNpcs?.Where(n => n?.npcId == id).ToArray();
            var inventory = YQContainerInventory.Find(w, id);
            return plans?.Length == 1 && !plans[0].hostile && inventory != null && inventory.generated && !inventory.dead && !inventory.locked && inventory.profileId == p.playerId &&
                inventory.ownerId == id && inventory.context?.entityId == id && inventory.context.sourceType == YQContainerType.Npc && inventory.access == YQContainerAccess.OwnerOnly && inventory.restock == YQContainerRestock.Never;
        } catch { return false; }
    }
    public static JObject Policy(PlayerState p, WorldState w, string id)
    {
        Scope(p, w); var matches = w.acceptedContent?.Where(r => r != null && r.contentId == id).ToArray();
        if (matches?.Length != 1 || matches[0].kind != YQStableEntityKind.Content || matches[0].schemaVersion != YQDispositionDomain.ContextSchema ||
            matches[0].contentHash != YQStateContract.Sha256Hex(matches[0].normalizedPayloadJson)) throw new InvalidOperationException("Accepted exact policy required.");
        JObject policy = Parse(matches[0].normalizedPayloadJson); string failure = YQDispositionDomain.ValidatePolicy(p, w, policy);
        if (failure != "" || policy.Value<string>("contextId") != id) throw new InvalidOperationException("Policy invalid: " + failure); return policy;
    }
    public static string ValidateRequest(PlayerState p, WorldState w, JObject r, bool requireHeldSupply)
    {
        try {
            Scope(p, w); Closed(r, RequestFields);
            if (r["version"]?.Type != JTokenType.Integer || r.Value<int>("version") != 1 || Text(r, "profileId") != p.playerId || Text(r, "worldId") != w.worldIdentity.worldId ||
                Text(r, "requestId") != RequestId(w, Text(r, "npcId")) || Text(r, "purpose") != "voluntary_restorative_supplies") throw new InvalidOperationException("Unsupported aid identity/purpose.");
            var npc = Npc(w, Text(r, "npcId")); if (!IsEligibleRecipient(w, p, npc.npcId) || Text(r, "locationId") != npc.locationId) throw new InvalidOperationException("Living nonhostile local NPC inventory required.");
            JObject policy = Policy(p, w, Text(r, "contextId"));
            var reference = w.acceptedContent.Single(a => a.contentId == r.Value<string>("contextId"));
            if (Text(r, "contextHash") != reference.contentHash || Text(r, "locationId") != policy.Value<string>("locationId")) throw new InvalidOperationException("Request policy/hash mismatch.");
            Text(r, "templateId"); Text(r, "itemContractHash");
            if (r["quantity"]?.Type != JTokenType.Integer || r.Value<int>("quantity") < 1 || r.Value<int>("quantity") > 3) throw new InvalidOperationException("Request quantity outside code-owned 1..3 bound.");
            if (r["requestLine"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(r.Value<string>("requestLine")) || r.Value<string>("requestLine").Length > 220 || r.Value<string>("requestLine").Any(char.IsControl)) throw new InvalidOperationException("Bounded complete request line required.");
            if (requireHeldSupply && !(p.inventoryItems?.Any(i => IsSupportedSupply(i) && i.templateId == r.Value<string>("templateId") && ItemContractHash(i) == r.Value<string>("itemContractHash") && i.quantity >= r.Value<int>("quantity")) ?? false)) throw new InvalidOperationException("Requested approved supply is not available.");
            return "";
        } catch (Exception error) { return error.Message; }
    }
    public static bool TryPrepareRequest(PlayerState p, WorldState w, string raw, string source, string prompt, out YQAcceptedProposal proposal, out string failure)
    {
        proposal = null; failure = "Generation provenance required.";
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(prompt)) return false;
        try { Parse(raw); } catch (Exception error) { failure = error.Message; return false; }
        return YQContentProposalBoundary.TryPrepare(raw, source, prompt, RequestSchema, root => {
            try { Closed(root, "decision", "payload"); if (root["decision"]?.Type != JTokenType.String || root.Value<string>("decision") != "aid" || !(root["payload"] is JObject payload)) return "No supported aid request."; return ValidateRequest(p, w, payload, true); }
            catch (Exception error) { return error.Message; }
        }, root => (JObject)Sort(root["payload"]), _ => true, out proposal, out failure);
    }
    public static JObject Request(PlayerState p, WorldState w, string id, string expectedHash = null)
    {
        var matches = w.acceptedContent?.Where(r => r?.contentId == id).ToArray();
        if (matches?.Length != 1 || matches[0].schemaVersion != RequestSchema || matches[0].kind != YQStableEntityKind.Content || matches[0].contentHash != YQStateContract.Sha256Hex(matches[0].normalizedPayloadJson) ||
            (expectedHash != null && matches[0].contentHash != expectedHash)) throw new InvalidOperationException("Unique immutable accepted aid request required.");
        JObject request = Parse(matches[0].normalizedPayloadJson); string error = ValidateRequest(p, w, request, false);
        if (error != "") throw new InvalidOperationException(error); return request;
    }
    internal static bool PrepareRequest(PlayerState p, WorldState w, string raw, string source, string prompt, out WorldState prepared, out YQDispositionDomain.Result result, out string failure)
    {
        prepared = null; result = null;
        if (!TryPrepareRequest(p, w, raw, source, prompt, out var proposal, out failure)) return false;
        string id = Parse(proposal.normalizedPayloadJson).Value<string>("requestId");
        var existing = w.acceptedContent.Where(r => r?.contentId == id).ToArray();
        if (existing.Length > 0) {
            if (existing.Length != 1 || existing[0].schemaVersion != RequestSchema || existing[0].kind != YQStableEntityKind.Content || existing[0].normalizedPayloadJson != proposal.normalizedPayloadJson || existing[0].contentHash != YQStateContract.Sha256Hex(proposal.normalizedPayloadJson)) { failure = "Accepted aid request cannot refresh terms."; return false; }
            prepared = w; return true;
        }
        if (!YQContentProposalBoundary.TryCommit(w, id, YQStableEntityKind.Content, proposal, w.stateRevision, out var receipt)) { failure = receipt.message; return false; }
        prepared = w; return true;
    }
    internal static bool PrepareAid(PlayerState p, WorldState w, AidDelivery token, out WorldState prepared, out YQDispositionDomain.Result result, out string failure)
    {
        prepared = null; result = null; failure = "Aid delivery unavailable.";
        try {
            if (token == null || !Finite(token.position)) throw new InvalidOperationException(failure);
            Scope(p, w); JObject request = Request(p, w, token.requestId, token.requestHash); string id = OutcomeId(token.requestId);
            // note: Retry resolves the exact durable completion before inventory checks; successful replay never debits a second item.
            if (w.eventLog.Any(e => e?.eventId == id)) {
                var old = w.eventLog.Single(e => e?.eventId == id); JObject proof = Parse(old.outcome);
                ValidateCompletionProof(proof, request, token.requestHash);
                if (!YQDispositionDomain.TryCaptureVerifiedOutcome(p, w, id, out var existing, out failure)) return false;
                return YQDispositionDomain.TryPrepare(p, w, existing, out prepared, out result, out failure);
            }
            if (!YQContainerInventory.TryPrepareConsentedNpcAid(w, p, request.Value<string>("npcId"), token.itemId, request.Value<string>("templateId"), request.Value<string>("itemContractHash"), request.Value<int>("quantity"), token.playerRevision, token.inventoryRevision, token.requestId, token.occurredUnix, out JObject transfer, out failure)) return false;
            transfer["producer"] = "YQSocialAidDomain"; transfer["requestId"] = token.requestId; transfer["requestHash"] = token.requestHash;
            ValidateCompletionProof(transfer, request, token.requestHash);
            JObject context = new JObject { ["version"] = 1, ["producer"] = "YQDispositionOutcomeProducer", ["receiptId"] = id, ["profileId"] = p.playerId, ["worldId"] = w.worldIdentity.worldId, ["actorId"] = p.playerId,
                ["kind"] = "aid_consented_no_coercion", ["targetNpcId"] = request["npcId"].DeepClone(), ["locationId"] = request["locationId"].DeepClone(), ["contextId"] = request["contextId"].DeepClone(), ["contextHash"] = request["contextHash"].DeepClone(),
                ["occurredUnix"] = token.occurredUnix, ["completed"] = true, ["consented"] = true, ["coerced"] = false, ["targetHostile"] = false, ["provoked"] = false,
                ["observers"] = new JArray(new JObject { ["npcId"] = request["npcId"].DeepClone(), ["channel"] = "visual_witness", ["receiptId"] = id }) };
            string key = YQDispositionDomain.Key("outcome", id); if (w.appliedMutationCommitKeys.Contains(key)) throw new InvalidOperationException("Orphan aid key rejected.");
            w.appliedMutationCommitKeys.Add(key); w.eventLog.Add(new YQEventEnvelope { eventId = id, eventType = YQDispositionDomain.OutcomeType, actorId = p.playerId, targetIds = new List<string> { request.Value<string>("npcId") }, logicalLocationId = request.Value<string>("locationId"), logicalPosition = token.position, context = Sort(context).ToString(Formatting.None), outcome = Sort(transfer).ToString(Formatting.None), mutationCommitKey = key, occurredUnix = token.occurredUnix, stateRevision = w.stateRevision });
            if (!YQDispositionDomain.TryCaptureVerifiedOutcome(p, w, id, out var admission, out failure)) return false;
            return YQDispositionDomain.TryPrepare(p, w, admission, out prepared, out result, out failure);
        } catch (Exception error) { failure = error.Message; result = null; return false; }
    }
    private static void ValidateCompletionProof(JObject proof, JObject request, string requestHash)
    {
        // note: Reload/retry checks the exact typed transfer receipt, not only a producer label or a matching event ID.
        Closed(proof, "npcId", "itemId", "templateId", "itemContractHash", "quantity", "donorQuantityBefore", "donorQuantityAfter", "recipientQuantityBefore", "recipientQuantityAfter", "playerRevisionBefore", "playerRevisionAfter", "recipientRevisionBefore", "recipientRevisionAfter", "producer", "requestId", "requestHash");
        if (Text(proof, "producer") != "YQSocialAidDomain" || Text(proof, "requestId") != request.Value<string>("requestId") || Text(proof, "requestHash") != requestHash ||
            Text(proof, "npcId") != request.Value<string>("npcId") || Text(proof, "templateId") != request.Value<string>("templateId") || Text(proof, "itemContractHash") != request.Value<string>("itemContractHash")) throw new InvalidOperationException("Conflicting aid completion terms.");
        Text(proof, "itemId");
        foreach (string field in new[] { "quantity", "donorQuantityBefore", "donorQuantityAfter", "recipientQuantityBefore", "recipientQuantityAfter", "playerRevisionBefore", "playerRevisionAfter", "recipientRevisionBefore", "recipientRevisionAfter" })
            if (proof[field]?.Type != JTokenType.Integer || proof.Value<long>(field) < 0) throw new InvalidOperationException("Typed nonnegative transfer values required.");
        long count = proof.Value<long>("quantity");
        if (count != request.Value<int>("quantity") || proof.Value<long>("donorQuantityBefore") - proof.Value<long>("donorQuantityAfter") != count ||
            proof.Value<long>("recipientQuantityAfter") - proof.Value<long>("recipientQuantityBefore") != count ||
            proof.Value<long>("playerRevisionAfter") - proof.Value<long>("playerRevisionBefore") != 1 || proof.Value<long>("recipientRevisionAfter") - proof.Value<long>("recipientRevisionBefore") != 1)
            throw new InvalidOperationException("Completion receipt has no exact transfer/revision witness.");
    }
    internal static bool PrepareReport(PlayerState p, WorldState w, ReportDelivery token, out WorldState prepared, out YQDispositionDomain.Result result, out string failure)
    {
        prepared = null; result = null; failure = "Report delivery unavailable.";
        try {
            Scope(p, w); if (token == null || !Finite(token.position) || token.reporterId == token.recipientId) throw new InvalidOperationException(failure);
            Npc(w, token.reporterId); Npc(w, token.recipientId);
            if (!w.eventLog.Any(e => e?.eventId == token.receiptId)) {
                var context = new JObject { ["version"] = 1, ["producer"] = "YQDispositionReportDelivery", ["receiptId"] = token.receiptId, ["profileId"] = p.playerId, ["worldId"] = w.worldIdentity.worldId,
                    ["incidentId"] = token.incidentId, ["reporterNpcId"] = token.reporterId, ["recipientNpcIds"] = new JArray(token.recipientId), ["sourceKnowledgeId"] = token.knowledgeId, ["occurredUnix"] = token.occurredUnix, ["delivered"] = true };
                string key = YQDispositionDomain.Key("delivery", token.receiptId); if (w.appliedMutationCommitKeys.Contains(key)) throw new InvalidOperationException("Orphan delivery key rejected.");
                w.appliedMutationCommitKeys.Add(key); w.eventLog.Add(new YQEventEnvelope { eventId = token.receiptId, eventType = YQDispositionDomain.DeliveryType, actorId = token.reporterId, targetIds = new List<string> { token.recipientId }, logicalPosition = token.position,
                    logicalLocationId = Npc(w, token.reporterId).locationId, context = Sort(context).ToString(Formatting.None), outcome = "{\"channel\":\"active_npc_hearing\"}", mutationCommitKey = key, occurredUnix = token.occurredUnix, stateRevision = w.stateRevision });
            }
            if (!YQDispositionDomain.TryCaptureDeliveredReport(p, w, token.receiptId, out var admission, out failure)) return false;
            return YQDispositionDomain.TryPrepare(p, w, admission, out prepared, out result, out failure);
        } catch (Exception error) { failure = error.Message; result = null; return false; }
    }
    internal static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
}

// note: This union extends the existing social paired transaction only for the item-transfer producer; full document equality guards unlisted mutations.
internal sealed class YQPreparedSocialInventory
{
    private readonly List<InventoryItemRecord> inventory;
    private readonly Dictionary<string, YQContainerRecord> containers;
    private readonly long revision, updated;
    internal YQPreparedSocialInventory(PlayerState p, WorldState w) { inventory = p.inventoryItems; containers = w.containers; revision = p.stateRevision; updated = p.lastUpdatedUnix; }
    internal void ApplyPlayer(PlayerState p) { p.inventoryItems = inventory; p.stateRevision = revision; p.lastUpdatedUnix = updated; }
    internal void ApplyWorld(WorldState w) { w.containers = containers; }
}
