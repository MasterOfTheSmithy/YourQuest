// C:\Users\Garri\YourQuest\Assets\Assets\Scripts\Dialogue\NpcDialogueMemoryStore.cs
using System;
using System.IO;
using UnityEngine;

public static class NpcDialogueMemoryStore
{
    private const string FolderName = "NpcDialogue";

    private static string RootDir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, FolderName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string MemPath(string npcEntityId)
    {
        npcEntityId = string.IsNullOrWhiteSpace(npcEntityId) ? "entity_unknown" : npcEntityId.Trim();
        return Path.Combine(RootDir, $"{npcEntityId}_mem.json");
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
            return JsonFileStore.TrySave(MemPath(npcEntityId), mem);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NpcDialogueMemoryStore] TrySave failed: {npcEntityId}\n{ex.Message}");
            return false;
        }
    }
}
