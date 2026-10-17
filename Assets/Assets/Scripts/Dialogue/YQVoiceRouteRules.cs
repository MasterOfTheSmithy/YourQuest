using System;

public static class YQVoiceRouteRules
{
    public static bool CanHear(float distanceSquared, float radius, bool active)
    {
        // note: Intelligent listeners are explicitly supplied by active dialogue agents, never inferred from generated prose.
        return active && !float.IsNaN(distanceSquared) && !float.IsInfinity(distanceSquared) &&
            !float.IsNaN(radius) && !float.IsInfinity(radius) && distanceSquared >= 0f && radius > 0f && distanceSquared <= radius * radius;
    }

    public static bool Prefer(float distanceSquared, string identity, float bestDistanceSquared, string bestIdentity)
    {
        // note: Equal-distance listeners resolve by stable NPC identity rather than scene enumeration order.
        return distanceSquared < bestDistanceSquared ||
            (distanceSquared == bestDistanceSquared && string.Compare(identity, bestIdentity, StringComparison.Ordinal) < 0);
    }
}
