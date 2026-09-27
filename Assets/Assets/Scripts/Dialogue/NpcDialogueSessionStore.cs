// Assets/Assets/Scripts/Dialogue/NpcDialogueSessionStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;

public static class NpcDialogueSessionStore
{
    private const string FolderName = "NpcDialogueSessions";


    private static string SessionPath(string npcEntityId)
    {
        return BuildScopedStoragePath(Application.persistentDataPath,
            PlayerStateManager.Instance?.state?.playerId, FolderName, npcEntityId, "_session.json");
    }

    public static string BuildScopedStoragePath(string root, string playerId, string folder, string npcId, string suffix)
    {
        // note: Shared legacy transcripts have no provable profile owner; leave them intact instead of importing another character's memories.
        if (string.IsNullOrWhiteSpace(playerId))
            throw new InvalidOperationException("Dialogue storage requires an active player identity.");
        if (folder != "NpcDialogueSessions" && folder != "NpcDialogue")
            throw new ArgumentException("Unknown dialogue storage category.", nameof(folder));
        if (suffix != "_session.json" && suffix != "_mem.json")
            throw new ArgumentException("Unknown dialogue document suffix.", nameof(suffix));
        return Path.Combine(root, "Profiles", StorageKey(playerId), folder, StorageKey(npcId) + suffix);
    }

    private static string StorageKey(string identity)
    {
        string normalized = string.IsNullOrWhiteSpace(identity) ? "npc_unknown" : identity.Trim().ToLowerInvariant();
        // note: Ordinary project IDs retain readable paths; hash other IDs so separators cannot escape the owning profile.
        bool plain = normalized.Length <= 96;
        for (int i = 0; plain && i < normalized.Length; i++)
            plain = (normalized[i] >= 'a' && normalized[i] <= 'z') ||
                    (normalized[i] >= '0' && normalized[i] <= '9') || normalized[i] == '_' || normalized[i] == '-';
        if (plain && !normalized.StartsWith("encoded_", StringComparison.Ordinal))
            return normalized;
        using (SHA256 hash = SHA256.Create())
            return "encoded_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", "").ToLowerInvariant();
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
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath(npcEntityId)));
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
