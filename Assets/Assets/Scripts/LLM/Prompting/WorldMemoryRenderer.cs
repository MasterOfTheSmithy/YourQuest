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

        AppendGeneratedWorldPlan(sb, s);
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

    private static void AppendGeneratedWorldPlan(StringBuilder sb, WorldState s, int maxRegions = 5, int maxSettlements = 5, int maxEncampments = 6, int maxPalettes = 5)
    {
        GeneratedWorldPlanRecord plan = s != null ? s.generatedWorldPlan : null;
        if (plan == null)
        {
            sb.AppendLine("GENERATED_WORLD_PLAN");
            sb.AppendLine("<none>");
            return;
        }

        plan.EnsureCollections();
        if (string.IsNullOrWhiteSpace(plan.worldSeed) || plan.regions.Count == 0)
        {
            sb.AppendLine("GENERATED_WORLD_PLAN");
            sb.AppendLine("<none>");
            return;
        }

        sb.AppendLine("GENERATED_WORLD_PLAN (compact)");
        sb.AppendLine($"Seed: {plan.worldSeed} | Source: {plan.source} | PromptPolicy: {plan.promptBudgetPolicy}");
        sb.AppendLine($"Summary: {plan.summary}");
        sb.AppendLine($"Budget: {plan.targetPlayableHoursMin}-{plan.targetPlayableHoursMax}h | Regions {plan.regions.Count} | Settlements {plan.settlements.Count} | Encampments {plan.encampments.Count} | AssetPalettes {plan.assetPalettes.Count}");

        sb.AppendLine("Generated Regions");
        int regions = 0;
        for (int i = 0; i < plan.regions.Count && regions < maxRegions; i++)
        {
            GeneratedRegionRecord region = plan.regions[i];
            if (region == null)
                continue;
            sb.AppendLine($"- {region.displayName} ({region.regionId}) | Tier {region.dangerTier} | Pressure {region.playerPressure}");
            regions++;
        }
        if (regions == 0)
            sb.AppendLine("- <none>");

        sb.AppendLine("Generated Asset Palettes");
        int palettes = 0;
        for (int i = 0; i < plan.assetPalettes.Count && palettes < maxPalettes; i++)
        {
            GeneratedRegionAssetPaletteRecord palette = plan.assetPalettes[i];
            if (palette == null)
                continue;

            sb.AppendLine($"- {palette.styleKey} ({palette.paletteId}) | Region {palette.regionId} | Floor {palette.floor.Count} Wall {palette.wall.Count} Path {palette.path.Count} Deco {palette.floorDeco.Count + palette.wallDeco.Count}");
            palettes++;
        }
        if (palettes == 0)
            sb.AppendLine("- <none>");

        sb.AppendLine("Generated Settlements");
        int settlements = 0;
        for (int i = 0; i < plan.settlements.Count && settlements < maxSettlements; i++)
        {
            GeneratedSettlementRecord settlement = plan.settlements[i];
            if (settlement == null)
                continue;
            sb.AppendLine($"- {settlement.displayName} ({settlement.settlementId}) | {settlement.kind} pop~{settlement.approxPopulation} | Region {settlement.regionId}");
            settlements++;
        }
        if (settlements == 0)
            sb.AppendLine("- <none>");

        sb.AppendLine("Generated Enemy Sites");
        int encampments = 0;
        for (int i = 0; i < plan.encampments.Count && encampments < maxEncampments; i++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[i];
            if (encampment == null)
                continue;
            sb.AppendLine($"- {encampment.displayName} ({encampment.encampmentId}) | {encampment.kind} T{encampment.threatTier} | {encampment.abilityProfile}");
            encampments++;
        }
        if (encampments == 0)
            sb.AppendLine("- <none>");
    }
}
