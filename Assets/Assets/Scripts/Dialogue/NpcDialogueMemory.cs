// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\NpcDialogueModels.cs
using System;
using System.Collections.Generic;

[Serializable]
public sealed class NpcDialogueMemory
{
    // Structured, durable memory (avoid storing full transcripts forever).
    public float relationship = 0f; // clamp -1..+1
    public float trust = 0f;        // clamp 0..1

    public List<string> knownFacts = new List<string>();   // small set of ids/flags
    public List<string> hooks = new List<string>();        // e.g. "awaiting_payment"
}

[Serializable]
public sealed class DialogueTurn
{
    public string speaker; // "player" | "npc"
    public string text;
}

[Serializable]
public sealed class NpcDialogueSession
{
    public string npcEntityId;
    public List<DialogueTurn> recentTurns = new List<DialogueTurn>(16);

    // Keep the transcript bounded to avoid runaway prompt growth.
    public int maxTurns = 12;
}
