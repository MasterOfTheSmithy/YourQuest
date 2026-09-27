// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\NpcDialogueMemoryStore.cs
using System;
using System.IO;
using UnityEngine;

public static class NpcDialogueMemoryStore
{
    private const string FolderName = "NpcDialogue";


    private static string MemPath(string npcEntityId)
    {
        // note: Long-term memory and transcripts share the same profile ownership and safe ID rules.
        return NpcDialogueSessionStore.BuildScopedStoragePath(Application.persistentDataPath,
            PlayerStateManager.Instance?.state?.playerId, FolderName, npcEntityId, "_mem.json");
    }

    public static bool TryLoad(string npcEntityId, out NpcDialogueMemory mem)
    {
        mem = null;
        try
        {
            return JsonFileStore.TryLoad(MemPath(npcEntityId), out mem) && mem != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NpcDialogueMemoryStore] TryLoad failed: {npcEntityId}\n{ex.Message}");
            mem = null;
            return false;
        }
    }

    public static bool TrySave(string npcEntityId, NpcDialogueMemory mem)
    {
        try
        {
            if (mem == null) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(MemPath(npcEntityId)));
            return JsonFileStore.TrySave(MemPath(npcEntityId), mem);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NpcDialogueMemoryStore] TrySave failed: {npcEntityId}\n{ex.Message}");
            return false;
        }
    }
}
