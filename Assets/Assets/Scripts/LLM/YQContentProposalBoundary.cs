using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Serializable]
public sealed class YQAcceptedProposal
{
    public string contentId;
    public YQStableEntityKind kind;
    public string source;
    public string schemaVersion;
    public string promptHash;
    public string generationHash;
    public string normalizedPayloadJson;
}

public static class YQContentProposalBoundary
{
    public static bool TryPrepare(
        string rawJson,
        string source,
        string prompt,
        string schemaVersion,
        Func<JObject, string> schemaValidator,
        Func<JObject, JObject> normalizer,
        Func<JObject, bool> curator,
        out YQAcceptedProposal proposal,
        out string error)
    {
        proposal = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            error = "Proposal payload was empty.";
            return false;
        }

        JObject parsed;
        try
        {
            parsed = JObject.Parse(ExtractJsonObject(rawJson));
        }
        catch (Exception exception)
        {
            error = "Proposal JSON was malformed: " + exception.Message;
            return false;
        }

        string schemaError = schemaValidator != null ? schemaValidator(parsed) : string.Empty;
        if (!string.IsNullOrWhiteSpace(schemaError))
        {
            error = "Proposal schema rejected the payload: " + schemaError;
            return false;
        }

        JObject normalized = (JObject)parsed.DeepClone();
        try
        {
            if (normalizer != null)
                normalized = normalizer(normalized);
        }
        catch (Exception exception)
        {
            error = "Proposal normalization failed: " + exception.Message;
            return false;
        }

        if (normalized == null)
        {
            error = "Proposal normalization returned no payload.";
            return false;
        }

        bool curated;
        try
        {
            curated = curator == null || curator(normalized);
        }
        catch (Exception exception)
        {
            error = "Proposal curation failed: " + exception.Message;
            return false;
        }

        if (!curated)
        {
            error = "Proposal failed the domain curation gate.";
            return false;
        }

        string canonicalJson = normalized.ToString(Formatting.None);
        // note: Hash provenance is derived before any state mutation so the accepted record can be replayed and audited after reload.
        proposal = new YQAcceptedProposal
        {
            source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim(),
            schemaVersion = string.IsNullOrWhiteSpace(schemaVersion) ? "unspecified" : schemaVersion.Trim(),
            promptHash = YQStateContract.Sha256Hex(prompt ?? string.Empty),
            generationHash = YQStateContract.Sha256Hex(rawJson),
            normalizedPayloadJson = canonicalJson
        };
        return true;
    }

    public static string ValidateRequiredProperties(JObject payload, params string[] requiredProperties)
    {
        if (payload == null)
            return "Payload object was null.";
        if (requiredProperties == null)
            return string.Empty;

        for (int index = 0; index < requiredProperties.Length; index++)
        {
            string property = requiredProperties[index];
            if (string.IsNullOrWhiteSpace(property) || payload[property] == null)
                return "Missing required property '" + property + "'.";
        }
        return string.Empty;
    }

    public static bool TryCommit(
        PlayerState state,
        string contentId,
        YQStableEntityKind kind,
        YQAcceptedProposal proposal,
        long expectedRevision,
        out YQMutationReceipt receipt)
    {
        return TryCommitInternal(state, null, contentId, kind, proposal, expectedRevision, out receipt);
    }

    public static bool TryCommit(
        WorldState state,
        string contentId,
        YQStableEntityKind kind,
        YQAcceptedProposal proposal,
        long expectedRevision,
        out YQMutationReceipt receipt)
    {
        return TryCommitInternal(null, state, contentId, kind, proposal, expectedRevision, out receipt);
    }

    private static bool TryCommitInternal(
        PlayerState player,
        WorldState world,
        string contentId,
        YQStableEntityKind kind,
        YQAcceptedProposal proposal,
        long expectedRevision,
        out YQMutationReceipt receipt)
    {
        receipt = new YQMutationReceipt { commitKey = contentId ?? string.Empty };
        if (proposal == null || string.IsNullOrWhiteSpace(contentId) || string.IsNullOrWhiteSpace(proposal.normalizedPayloadJson))
        {
            receipt.message = "Accepted proposal and content ID are required.";
            return false;
        }

        if (!TryParseCanonicalObject(proposal.normalizedPayloadJson, out _, out string parseError))
        {
            receipt.message = "Accepted normalized payload is not a JSON object: " + parseError;
            return false;
        }

        proposal.contentId = contentId.Trim();
        proposal.kind = kind;
        YQAcceptedContentReference reference = new YQAcceptedContentReference
        {
            contentId = proposal.contentId,
            kind = kind,
            immutableSource = proposal.source,
            contentHash = YQStateContract.Sha256Hex(proposal.normalizedPayloadJson),
            schemaVersion = proposal.schemaVersion,
            promptHash = proposal.promptHash,
            generationHash = proposal.generationHash,
            normalizedPayloadJson = proposal.normalizedPayloadJson,
            acceptedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        if (player != null)
        {
            player.EnsureCollections();
            if (!player.TryApplyMutationCommit(proposal.contentId, expectedRevision, out receipt))
                return false;
            reference.acceptedRevision = player.stateRevision;
            player.acceptedContent.Add(reference);
            player.Touch();
            receipt.stateRevision = player.stateRevision;
            return true;
        }

        if (world != null)
        {
            world.EnsureCollections();
            if (!world.TryApplyMutationCommit(proposal.contentId, expectedRevision, out receipt))
                return false;
            reference.acceptedRevision = world.stateRevision;
            world.acceptedContent.Add(reference);
            world.TouchNow();
            receipt.stateRevision = world.stateRevision;
            return true;
        }

        receipt.message = "No canonical content owner was provided.";
        return false;
    }

    private static bool TryParseCanonicalObject(string json, out JObject parsed, out string error)
    {
        parsed = null;
        error = string.Empty;
        try
        {
            parsed = JObject.Parse(json);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        string candidate = raw.Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineEnd = candidate.IndexOf('\n');
            candidate = firstLineEnd >= 0 ? candidate.Substring(firstLineEnd + 1) : string.Empty;
            int closingFence = candidate.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                candidate = candidate.Substring(0, closingFence);
        }

        int objectStart = candidate.IndexOf('{');
        int objectEnd = candidate.LastIndexOf('}');
        if (objectStart < 0 || objectEnd <= objectStart)
            return candidate;
        return candidate.Substring(objectStart, objectEnd - objectStart + 1);
    }
}
