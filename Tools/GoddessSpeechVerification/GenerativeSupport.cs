
// note: These explicit adapters isolate pure presentation logic. They do not certify Unity UI, scheduler transport or physical paired commits.
public class PlayerState
{
    public string playerId = "fixture-player";
    public long stateRevision = 17;
    public int schemaVersion = 8;
    public string activeQuestId = "fixture-quest";
    public System.Collections.Generic.List<string> goddessVoiceMemory = new System.Collections.Generic.List<string>();
    public System.Collections.Generic.HashSet<string> goddessDeliveredSpeechHashes = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
    public void EnsureCollections()
    {
        goddessVoiceMemory ??= new System.Collections.Generic.List<string>();
        goddessDeliveredSpeechHashes ??= new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
    }
}
public class WorldState
{
    public string lastLLMRationale = string.Empty;
    public long stateRevision = 23;
    public WorldIdentity worldIdentity = new WorldIdentity();
    public class WorldIdentity { public string worldId = "fixture-world", ownerProfileId = "fixture-player"; }
}
public class PlayerStateManager { public static PlayerStateManager Instance = new PlayerStateManager(); public PlayerState state = new PlayerState(); }
public class WorldStateManager { public static WorldStateManager Instance = new WorldStateManager(); public WorldState State = new WorldState(); }
public class YQProfileSaveSystem
{
    public static YQProfileSaveSystem Instance = new YQProfileSaveSystem();
    public string ActiveProfileId = "fixture-player";
    public bool succeeds = true;
    public int commits;
    public bool SaveActiveProfile() { if (!succeeds) return false; commits++; return true; }
}
public static class YQServiceLifecycle
{
    public static int RequestEpoch = 7;
    public static bool IsCurrent(int epoch) => epoch == RequestEpoch;
}
public static class YQGeneratedWorldRuntimeBuilder
{
    public static bool IsInitialGenerationGameplayLocked;
}
public class LLMClient
{
    // note: Detached NPC checks exercise the production callback chain with explicitly enabled fixture review, not runtime qualification flags.
    public static LLMClient Instance;
    public bool DialogueSemanticRepairEnabled = true;
    public string QualifiedDialogueVerifierDigest = "fixture-reviewer";
    public bool GoddessSpeechPlanEnabled = true, GoddessSemanticRepairEnabled = true, GoddessSpeechPlanCpuOnly = true;
    public string QualifiedGoddessSpeechPlanDigest = "fixture-generator", QualifiedGoddessVerifierDigest = "fixture-reviewer";
    public bool IsExclusiveSequenceActive;
    public string ExclusiveSequenceOwner = string.Empty;
    public readonly System.Collections.Generic.Queue<Pending> pending = new System.Collections.Generic.Queue<Pending>();
    public class Pending { public YQLlmRequest request; public Action<YQLlmRequestResult> callback; public string key; }
    public YQRepairEpisode CreateRepairEpisode(YQLlmRequest request, string task, double seconds) =>
        new YQRepairEpisode(request.profileId, request.worldId, YQServiceLifecycle.RequestEpoch, request.ownerId,
            request.bindPlayerStateRevision ? request.playerStateRevision : -1,
            request.bindWorldStateRevision ? request.worldStateRevision : -1, task, seconds);
    public long Submit(YQLlmRequest request, Action<YQLlmRequestResult> callback)
    {
        bool admitted = request.repairEpisode.TryAdmit(request.repairVerification);
        if (!admitted || !request.repairEpisode.TryBeginCall(request.repairVerification, request.prompt, request.parentRequestKey, out string key))
            throw new InvalidOperationException("Detached adapter exceeded the production episode budget.");
        pending.Enqueue(new Pending { request = request, callback = callback, key = key }); return pending.Count;
    }
    public void Complete(string raw, bool wrongBinding = false, bool failed = false)
    {
        Pending child = pending.Dequeue(); YQLlmRequest r = child.request;
        child.callback(new YQLlmRequestResult(1, r.debugTag, r.category, !failed,
            failed ? YQLlmTerminalOutcome.Failed : YQLlmTerminalOutcome.AcceptedResponse, raw, failed ? "fixture transport failure" : null,
            1, 0, 0, default, r.profileId, r.worldId, r.generationEpoch, r.ownerId, r.playerStateRevision, r.worldStateRevision,
            r.repairEpisode.key, wrongBinding ? "different-physical-request" : child.key));
    }
}
namespace UnityEngine
{
    public class DisallowMultipleComponent : Attribute { }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) { } }
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
        public object gameObject;
        protected static void Destroy(object value) { }
        protected static T FindFirstObjectByType<T>() where T : class => NpcFixtureScene.Director as T;
    }
    public static class Debug { public static void Log(object value) => Console.Error.WriteLine(value); }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class RangeAttribute : Attribute { public RangeAttribute(float low, float high) { } }
    public class CreateAssetMenuAttribute : Attribute { public string menuName; }
    public class TextAsset { public string text; }
    public static class Resources
    {
        public static T Load<T>(string name) where T : class => new TextAsset {
            text = System.IO.File.ReadAllText("Assets/Assets/Resources/GoddessAgentProfile.txt") } as T;
    }
    public static class Mathf
    {
        public static int Clamp(int value, int low, int high) => Math.Clamp(value, low, high);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static int CeilToInt(float value) => (int)Math.Ceiling(value);
        public static int RoundToInt(float value) => (int)Math.Round(value);
    }
}

// note: These adapters supply only fixture context and transcript storage; the reply generation, review, repair and finalization code is linked unchanged.
public static class NpcFixtureScene { public static YQInvestorDirector Director = new YQInvestorDirector(); }
public sealed class YQInvestorDirector { public string CurrentObjective = string.Empty; }
public sealed class SituationSnapshotBuilder { public string snapshot = "{}"; public string BuildSnapshot() => snapshot; }
public sealed class NpcDialogueAgent
{
    public bool isActiveAndEnabled = true;
    public string NpcId = "fixture-npc", persona = string.Empty;
    public readonly System.Collections.Generic.List<string> npcLines = new System.Collections.Generic.List<string>();
    public readonly System.Collections.Generic.List<string> playerLines = new System.Collections.Generic.List<string>();
    public string BuildPersonaBlock() => persona;
    public System.Collections.Generic.List<string> GetRecentNpcLines(int count) => npcLines.GetRange(Math.Max(0, npcLines.Count-count), Math.Min(count,npcLines.Count));
    public System.Collections.Generic.List<string> GetRecentPlayerLines(int count) => playerLines.GetRange(Math.Max(0, playerLines.Count-count), Math.Min(count,playerLines.Count));
    public string RenderRecentDialogue(int count)
    {
        var lines = new System.Collections.Generic.List<string>();
        for (int i = Math.Max(0, npcLines.Count-count); i < npcLines.Count; i++)
        { lines.Add("Player: " + playerLines[i]); lines.Add("NPC: " + npcLines[i]); }
        return string.Join("\n", lines);
    }
}
