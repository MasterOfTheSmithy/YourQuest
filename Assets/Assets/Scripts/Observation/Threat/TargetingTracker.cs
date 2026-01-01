using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks "who is targeting whom" as explicit edges.
/// This is the single best signal to improve relevance.
/// </summary>
public class TargetingTracker : MonoBehaviour
{
    public static TargetingTracker Instance { get; private set; }

    [Header("Decay / TTL")]
    [Tooltip("If an edge isn't refreshed, it expires after this many seconds.")]
    public float edgeTtlSeconds = 6f;

    [Header("Player")]
    [Tooltip("Stable player id for targeting edges. Keep it constant.")]
    public string playerEntityId = "player";

    private class Edge
    {
        public string fromId;
        public string toId;
        public float lastSeenTime;
    }

    private readonly Dictionary<string, Edge> incomingToPlayer = new Dictionary<string, Edge>(); // fromId -> edge
    private string playerOutgoingTargetId = null;
    private float playerOutgoingLastSeen = -999f;

    public string PlayerEntityId => playerEntityId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        ExpireEdges();
    }

    private void ExpireEdges()
    {
        float now = Time.time;

        // Incoming edges to player
        var toRemove = new List<string>();
        foreach (var kv in incomingToPlayer)
        {
            if (now - kv.Value.lastSeenTime > edgeTtlSeconds)
                toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
            incomingToPlayer.Remove(toRemove[i]);

        // Outgoing edge from player
        if (!string.IsNullOrWhiteSpace(playerOutgoingTargetId))
        {
            if (now - playerOutgoingLastSeen > edgeTtlSeconds)
                playerOutgoingTargetId = null;
        }
    }

    /// <summary>
    /// Set/refresh a targeting edge. Call repeatedly while targeting stays true.
    /// </summary>
    public void SetTargeting(EntityInfo from, string toEntityId, bool targeting)
    {
        if (from == null || string.IsNullOrWhiteSpace(from.entityId) || string.IsNullOrWhiteSpace(toEntityId))
            return;

        string fromId = from.entityId.Trim();
        string toId = toEntityId.Trim();

        if (!targeting)
        {
            // Remove if exists
            if (toId == playerEntityId && incomingToPlayer.ContainsKey(fromId))
                incomingToPlayer.Remove(fromId);

            // If it was player's outgoing, clear
            if (fromId == playerEntityId && playerOutgoingTargetId == toId)
                playerOutgoingTargetId = null;

            return;
        }

        // Add/refresh
        if (toId == playerEntityId)
        {
            incomingToPlayer[fromId] = new Edge { fromId = fromId, toId = toId, lastSeenTime = Time.time };
        }
    }

    /// <summary>
    /// Player targets a specific entity (lock-on/aim).
    /// Refresh this while aiming to keep it alive.
    /// </summary>
    public void SetPlayerOutgoingTarget(EntityInfo target)
    {
        if (target == null || string.IsNullOrWhiteSpace(target.entityId)) return;
        playerOutgoingTargetId = target.entityId.Trim();
        playerOutgoingLastSeen = Time.time;
    }

    public List<string> GetIncomingTargetersToPlayer()
    {
        var list = new List<string>(incomingToPlayer.Count);
        foreach (var kv in incomingToPlayer)
            list.Add(kv.Key);
        return list;
    }

    public string GetPlayerOutgoingTargetId()
    {
        return playerOutgoingTargetId;
    }
}
