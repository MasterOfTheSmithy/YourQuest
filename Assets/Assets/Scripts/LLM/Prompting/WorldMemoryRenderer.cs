using System.Text;

public static class WorldMemoryRenderer
{
    public static string Render(WorldState s, int maxFlags = 12, int maxFactions = 6, int maxLocations = 6, int maxNpcs = 6)
    {
        if (s == null) return "WORLD_SNAPSHOT: <null>";

        var sb = new StringBuilder();

        sb.AppendLine("WORLD_SNAPSHOT");
        sb.AppendLine($"World: {s.worldName}  Region: {s.currentRegionId}");
        sb.AppendLine();

        sb.AppendLine("CANON_LEDGER");
        sb.AppendLine(string.IsNullOrWhiteSpace(s.canonLedger) ? "<none>" : s.canonLedger.Trim());
        sb.AppendLine();

        sb.AppendLine("GLOBAL_FLAGS (top)");
        int added = 0;
        if (s.globalFlags != null && s.globalFlags.Count > 0)
        {
            foreach (var kv in s.globalFlags)
            {
                sb.AppendLine($"- {kv.Key} = {kv.Value:0.00}");
                added++;
                if (added >= maxFlags) break;
            }
        }
        if (added == 0) sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("FACTIONS (notable)");
        int fCount = 0;
        if (s.factions != null)
        {
            for (int i = 0; i < s.factions.Count && fCount < maxFactions; i++)
            {
                var f = s.factions[i];
                if (f == null) continue;
                sb.AppendLine($"- {f.name} ({f.factionId}) | AttitudeToPlayer {f.attitudeToPlayer:0.00} | Status {f.status}");
                fCount++;
            }
        }
        if (fCount == 0) sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("LOCATIONS (region-relevant)");
        int lCount = 0;
        if (s.locations != null)
        {
            for (int i = 0; i < s.locations.Count && lCount < maxLocations; i++)
            {
                var l = s.locations[i];
                if (l == null) continue;
                if (!string.IsNullOrWhiteSpace(s.currentRegionId) && l.regionId != s.currentRegionId) continue;

                sb.AppendLine($"- {l.name} ({l.locationId}) | State {l.state} | Importance {l.importance:0.00}");
                lCount++;
            }
        }
        if (lCount == 0) sb.AppendLine("<none>");
        sb.AppendLine();

        sb.AppendLine("NPCS (region-relevant)");
        int nCount = 0;
        if (s.npcs != null)
        {
            for (int i = 0; i < s.npcs.Count && nCount < maxNpcs; i++)
            {
                var n = s.npcs[i];
                if (n == null) continue;
                if (!string.IsNullOrWhiteSpace(s.currentRegionId) && !string.IsNullOrWhiteSpace(n.locationId))
                {
                    // if you later store npc->location->region, you can filter better
                }

                sb.AppendLine($"- {n.name} ({n.npcId}) | Faction {n.factionId} | Affinity {n.affinityToPlayer:0.00} | Status {n.status}");
                nCount++;
            }
        }
        if (nCount == 0) sb.AppendLine("<none>");

        return sb.ToString();
    }
}
