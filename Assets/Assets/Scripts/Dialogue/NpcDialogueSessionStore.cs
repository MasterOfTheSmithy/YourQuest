// Assets/Assets/Scripts/Dialogue/NpcDialogueSessionStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class NpcDialogueSessionStore
{
    private const string FolderName = "NpcDialogueSessions";

    private static string RootDir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, FolderName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string SessionPath(string npcEntityId)
    {
        string safeId = string.IsNullOrWhiteSpace(npcEntityId) ? "npc_unknown" : npcEntityId.Trim();
        return Path.Combine(RootDir, safeId + "_session.json");
    }

    public static bool TryLoad(string npcEntityId, out NpcDialogueSession session)
    {
        session = null;
        try
        {
            bool loaded = JsonFileStore.TryLoad(SessionPath(npcEntityId), out session) && session != null;
            if (!loaded)
                return false;

            session.npcEntityId = string.IsNullOrWhiteSpace(session.npcEntityId) ? npcEntityId : session.npcEntityId.Trim();
            session.maxTurns = Mathf.Clamp(session.maxTurns <= 0 ? 160 : session.maxTurns, 4, 256);
            session.recentTurns ??= new List<DialogueTurn>(16);
            TrimTurns(session);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NpcDialogueSessionStore] TryLoad failed: {npcEntityId}\n{ex.Message}");
            session = null;
            return false;
        }
    }

    public static bool TrySave(string npcEntityId, NpcDialogueSession session)
    {
        try
        {
            if (session == null)
                return false;

            session.npcEntityId = string.IsNullOrWhiteSpace(session.npcEntityId) ? npcEntityId : session.npcEntityId.Trim();
            session.maxTurns = Mathf.Clamp(session.maxTurns <= 0 ? 160 : session.maxTurns, 4, 256);
            session.recentTurns ??= new List<DialogueTurn>(16);
            TrimTurns(session);
            return JsonFileStore.TrySave(SessionPath(npcEntityId), session);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NpcDialogueSessionStore] TrySave failed: {npcEntityId}\n{ex.Message}");
            return false;
        }
    }

    private static void TrimTurns(NpcDialogueSession session)
    {
        if (session == null || session.recentTurns == null)
            return;

        for (int i = session.recentTurns.Count - 1; i >= 0; i--)
        {
            DialogueTurn turn = session.recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                session.recentTurns.RemoveAt(i);
        }

        int overflow = session.recentTurns.Count - session.maxTurns;
        if (overflow > 0)
            session.recentTurns.RemoveRange(0, overflow);
    }
}
