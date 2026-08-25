using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQSemanticExtractionProfileBuilder
{
    public const string CatalogPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQSemanticExtractionProfileCatalog.asset";

    private const string ReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQSemanticExtractionProfileReport.md";

    private static readonly Dictionary<string, YQSemanticExtractionProfile>
        AuthoredProfiles = BuildAuthoredProfiles();

    private static readonly Dictionary<string, string> ProfileAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "asian_dynasty_environment", "asian_dynasty" },
            { "bio_horror_sci_fi_environment", "bio_horror_scifi" },
            { "cave_of_hidden_tomb", "cave_hidden_tomb" },
            { "gladitor_arena", "gladiator_arena" },
            { "house_ona_hill", "house_on_a_hill" },
            { "persepolis_empire_environment", "persepolis_empire" },
            { "the_messenger_mountain", "messenger_mountain" },
            { "victorian_mansion_environment", "victorian_mansion" }
        };

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Semantic Authoring/Build or Refresh All Extraction Profiles")]
    public static void BuildOrRefreshAllExtractionProfiles()
    {
        SyncProfiles(true);
    }

    public static YQSemanticExtractionProfileCatalog SyncProfiles(bool logResult)
    {
        YQAuthoredSiteSourceCatalog sourceCatalog =
            YQAuthoredSiteSourceDiscovery.SyncCatalog(false);
        List<YQSemanticExtractionProfile> profiles =
            new List<YQSemanticExtractionProfile>();
        IReadOnlyList<YQAuthoredSiteSourceRecord> sources =
            sourceCatalog.Records;

        for (int index = 0; index < sources.Count; index++)
        {
            YQAuthoredSiteSourceRecord source = sources[index];

            if (!IsBuildableEnvironment(source))
                continue;

            string authoredProfileKey = ResolveAuthoredProfileKey(source.kitId);

            if (AuthoredProfiles.TryGetValue(
                    authoredProfileKey,
                    out YQSemanticExtractionProfile authored))
            {
                YQSemanticExtractionProfile resolved = Clone(authored);
                // note: Catalog identity follows the discovered source while semanticStyleKey preserves the stable Goddess-facing palette key.
                resolved.kitId = source.kitId;
                resolved.displayName = source.displayName;
                profiles.Add(resolved);
            }
            else
            {
                profiles.Add(InferQuarantinedProfile(source));
            }
        }

        profiles = profiles
            .OrderBy(profile => profile.kitId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        YQSemanticExtractionProfileCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQSemanticExtractionProfileCatalog>(
                CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<
                YQSemanticExtractionProfileCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.Configure(profiles);
        EditorUtility.SetDirty(catalog);
        WriteReport(profiles);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            int reviewed = profiles.Count(profile => profile.authoredOverride);
            int quarantined = profiles.Count - reviewed;
            Debug.Log(
                "[YQSemanticExtractionProfileBuilder] SEMANTIC PROFILES READY\n" +
                "Buildable environments: " + profiles.Count + "\n" +
                "Authored profiles: " + reviewed + "\n" +
                "Quarantined inferred profiles: " + quarantined + "\n" +
                "Catalog: " + CatalogPath + "\n" +
                "Report: " + ReportPath);
        }

        return catalog;
    }

    public static bool ApplyCurrentAuthoredReviewPolicy(
        YQSemanticExtractionProfile profile)
    {
        if (profile == null)
            return false;

        string authoredProfileKey = ResolveAuthoredProfileKey(profile.kitId);

        if (!AuthoredProfiles.TryGetValue(
                authoredProfileKey,
                out YQSemanticExtractionProfile authored))
        {
            return false;
        }

        bool changed =
            profile.structureUsagePolicy != authored.structureUsagePolicy ||
            profile.maximumEnterableStructures !=
            authored.maximumEnterableStructures;

        if (changed)
        {
            // note: Review-time policy refresh updates only lightweight curated metadata and never rescans source packs or rebuilds geometry.
            profile.structureUsagePolicy = authored.structureUsagePolicy;
            profile.maximumEnterableStructures =
                authored.maximumEnterableStructures;
        }

        return changed;
    }

    private static bool IsBuildableEnvironment(
        YQAuthoredSiteSourceRecord source)
    {
        return source != null &&
               !string.IsNullOrWhiteSpace(source.selectedScenePath) &&
               !string.IsNullOrWhiteSpace(source.generatedPrefabPath) &&
               source.siteKind != YQAuthoredSiteKind.Unknown &&
               (source.state == YQAuthoredSiteSourceState.CandidateBuilt ||
                source.state == YQAuthoredSiteSourceState.Approved);
    }

    private static YQSemanticExtractionProfile InferQuarantinedProfile(
        YQAuthoredSiteSourceRecord source)
    {
        YQSemanticExtractionTopology topology =
            DefaultTopology(source.siteKind);
        return Profile(
            source.kitId,
            source.displayName,
            source.siteKind,
            topology,
            2,
            8,
            DefaultSpan(topology),
            DefaultLayerHeight(topology),
            DefaultLinkDistance(topology),
            false,
            RequiredOutputs(topology));
    }

    private static Dictionary<string, YQSemanticExtractionProfile>
        BuildAuthoredProfiles()
    {
        Dictionary<string, YQSemanticExtractionProfile> result =
            new Dictionary<string, YQSemanticExtractionProfile>(
                StringComparer.OrdinalIgnoreCase);

        YQSemanticExtractionProfile gothicCathedral = Profile("gothic_cathedral", "Gothic Cathedral", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 3, 7, 72f, 10f, 16f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus));
        // note: Visual review proved that only the primary cathedral is furnished; every ancillary building is an exterior-only shell.
        gothicCathedral.structureUsagePolicy =
            YQWorldStructureUsagePolicy.SingleFurnishedPrimaryWithExteriorShells;
        gothicCathedral.maximumEnterableStructures = 1;
        Add(result, gothicCathedral);
        Add(result, Profile("hallowed_depths", "Hallowed Depths", YQAuthoredSiteKind.Dungeon, YQSemanticExtractionTopology.DungeonRooms, 4, 10, 48f, 7f, 12f, true, RequiredOutputs(YQSemanticExtractionTopology.DungeonRooms)));
        Add(result, Profile("haunted_village", "Haunted Village", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 5, 10, 84f, 9f, 18f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("cave_hidden_tomb", "Cave Of Hidden Tomb", YQAuthoredSiteKind.Dungeon, YQSemanticExtractionTopology.DungeonRooms, 5, 12, 52f, 8f, 14f, true, RequiredOutputs(YQSemanticExtractionTopology.DungeonRooms)));
        Add(result, Profile("cyberpunk_city", "Cyberpunk City", YQAuthoredSiteKind.SciFiSite, YQSemanticExtractionTopology.SciFiSectors, 6, 12, 88f, 10f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.SciFiSectors)));
        Add(result, Profile("gladiator_arena", "Gladiator Arena", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 3, 6, 82f, 10f, 18f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus)));
        Add(result, Profile("messenger_mountain", "The Messenger Mountain", YQAuthoredSiteKind.Wilderness, YQSemanticExtractionTopology.WildernessRegions, 5, 12, 150f, 18f, 28f, true, RequiredOutputs(YQSemanticExtractionTopology.WildernessRegions)));
        Add(result, Profile("horror_hospital", "Horror Hospital", YQAuthoredSiteKind.Interior, YQSemanticExtractionTopology.InteriorRooms, 6, 14, 42f, 4.5f, 10f, true, RequiredOutputs(YQSemanticExtractionTopology.InteriorRooms)));
        Add(result, Profile("house_on_a_hill", "House On A Hill", YQAuthoredSiteKind.Interior, YQSemanticExtractionTopology.InteriorRooms, 2, 6, 34f, 4.5f, 9f, true, RequiredOutputs(YQSemanticExtractionTopology.InteriorRooms)));
        Add(result, Profile("medieval_kingdom", "Medieval Kingdom", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 8, 16, 120f, 10f, 24f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("military_camp", "Military Camp", YQAuthoredSiteKind.Camp, YQSemanticExtractionTopology.CampZones, 5, 10, 92f, 8f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.CampZones)));
        Add(result, Profile("modular_viking_village", "Modular Viking Village", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 4, 9, 82f, 9f, 18f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("mountain_temple", "Mountain Temple", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 4, 9, 90f, 14f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus)));
        Add(result, Profile("mystic_dungeon", "Mystic Dungeon", YQAuthoredSiteKind.Dungeon, YQSemanticExtractionTopology.DungeonRooms, 6, 14, 54f, 8f, 14f, true, RequiredOutputs(YQSemanticExtractionTopology.DungeonRooms)));
        Add(result, Profile("native_american_village", "Native American Village", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 4, 9, 90f, 8f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("olympus_temple", "Olympus Temple", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 3, 7, 76f, 12f, 18f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus)));
        Add(result, Profile("pirate_island", "Pirate Island", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 4, 9, 96f, 10f, 22f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("rural_town", "Rural Town", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 3, 8, 80f, 8f, 18f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("the_sewers", "The Sewers", YQAuthoredSiteKind.Dungeon, YQSemanticExtractionTopology.DungeonRooms, 5, 12, 48f, 7f, 12f, true, RequiredOutputs(YQSemanticExtractionTopology.DungeonRooms)));
        Add(result, Profile("town_smith", "Town Smith", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 5, 11, 86f, 9f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("villa_forge", "Villa Forge", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 2, 6, 58f, 8f, 14f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus)));
        Add(result, Profile("witch_house", "Witch House", YQAuthoredSiteKind.Interior, YQSemanticExtractionTopology.InteriorRooms, 2, 5, 30f, 4.5f, 8f, true, RequiredOutputs(YQSemanticExtractionTopology.InteriorRooms)));
        Add(result, Profile("ancient_desert_ruins", "Ancient Desert Ruins", YQAuthoredSiteKind.Landmark, YQSemanticExtractionTopology.LandmarkCampus, 6, 12, 120f, 12f, 24f, true, RequiredOutputs(YQSemanticExtractionTopology.LandmarkCampus)));
        Add(result, Profile("asian_dynasty", "Asian Dynasty", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 4, 9, 88f, 10f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("bio_horror_scifi", "Bio Horror Sci-Fi", YQAuthoredSiteKind.SciFiSite, YQSemanticExtractionTopology.SciFiSectors, 2, 6, 46f, 6f, 12f, true, RequiredOutputs(YQSemanticExtractionTopology.SciFiSectors)));
        Add(result, Profile("container_district", "Container District", YQAuthoredSiteKind.SciFiSite, YQSemanticExtractionTopology.SciFiSectors, 5, 11, 92f, 10f, 20f, true, RequiredOutputs(YQSemanticExtractionTopology.SciFiSectors)));
        // note: Eleven metres joins the modular pieces of one Viking structure without chaining neighbouring houses into one vote.
        Add(result, Profile("medieval_viking_village", "Medieval Viking Village", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 4, 8, 82f, 9f, 11f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("nordic_village", "Nordic Village", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 8, 16, 130f, 10f, 26f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("persepolis_empire", "Persepolis Empire", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 6, 13, 112f, 10f, 24f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));
        Add(result, Profile("sci_fi_engineers_room", "Sci-Fi Engineers Room", YQAuthoredSiteKind.SciFiSite, YQSemanticExtractionTopology.SciFiSectors, 1, 4, 30f, 4.5f, 8f, true, RequiredOutputs(YQSemanticExtractionTopology.SciFiSectors)));
        Add(result, Profile("victorian_mansion", "Victorian Mansion", YQAuthoredSiteKind.Interior, YQSemanticExtractionTopology.InteriorRooms, 3, 8, 38f, 4.5f, 9f, true, RequiredOutputs(YQSemanticExtractionTopology.InteriorRooms)));
        Add(result, Profile("western_desert_town", "Western Desert Town", YQAuthoredSiteKind.Settlement, YQSemanticExtractionTopology.SettlementDistricts, 6, 12, 108f, 9f, 22f, true, RequiredOutputs(YQSemanticExtractionTopology.SettlementDistricts)));

        return result;
    }

    private static void Add(
        Dictionary<string, YQSemanticExtractionProfile> destination,
        YQSemanticExtractionProfile profile)
    {
        destination[profile.kitId] = profile;
    }

    private static YQSemanticExtractionProfile Profile(
        string kitId,
        string displayName,
        YQAuthoredSiteKind siteKind,
        YQSemanticExtractionTopology topology,
        int minimumAssemblies,
        int maximumAssemblies,
        float targetHorizontalSpan,
        float verticalLayerHeight,
        float cohesiveLinkDistance,
        bool authoredOverride,
        IEnumerable<string> requiredOutputs)
    {
        return new YQSemanticExtractionProfile
        {
            kitId = kitId,
            semanticStyleKey = kitId,
            displayName = displayName,
            siteKind = siteKind,
            topology = topology,
            minimumAssemblies = minimumAssemblies,
            maximumAssemblies = maximumAssemblies,
            targetHorizontalSpan = targetHorizontalSpan,
            verticalLayerHeight = verticalLayerHeight,
            cohesiveLinkDistance = cohesiveLinkDistance,
            authoredOverride = authoredOverride,
            requiresManualProfileReview = !authoredOverride,
            requiredSemanticOutputs = new List<string>(requiredOutputs)
        };
    }

    private static YQSemanticExtractionProfile Clone(
        YQSemanticExtractionProfile source)
    {
        YQSemanticExtractionProfile clone = Profile(
            source.kitId,
            source.displayName,
            source.siteKind,
            source.topology,
            source.minimumAssemblies,
            source.maximumAssemblies,
            source.targetHorizontalSpan,
            source.verticalLayerHeight,
            source.cohesiveLinkDistance,
            source.authoredOverride,
            source.requiredSemanticOutputs);
        clone.semanticStyleKey = source.semanticStyleKey;
        clone.structureUsagePolicy = source.structureUsagePolicy;
        clone.maximumEnterableStructures =
            source.maximumEnterableStructures;
        return clone;
    }

    private static string ResolveAuthoredProfileKey(string sourceKitId)
    {
        if (ProfileAliases.TryGetValue(
                sourceKitId ?? string.Empty,
                out string authoredProfileKey))
        {
            return authoredProfileKey;
        }

        return sourceKitId ?? string.Empty;
    }

    private static YQSemanticExtractionTopology DefaultTopology(
        YQAuthoredSiteKind siteKind)
    {
        switch (siteKind)
        {
            case YQAuthoredSiteKind.Settlement:
                return YQSemanticExtractionTopology.SettlementDistricts;
            case YQAuthoredSiteKind.Dungeon:
                return YQSemanticExtractionTopology.DungeonRooms;
            case YQAuthoredSiteKind.Interior:
                return YQSemanticExtractionTopology.InteriorRooms;
            case YQAuthoredSiteKind.Landmark:
                return YQSemanticExtractionTopology.LandmarkCampus;
            case YQAuthoredSiteKind.Camp:
                return YQSemanticExtractionTopology.CampZones;
            case YQAuthoredSiteKind.Wilderness:
                return YQSemanticExtractionTopology.WildernessRegions;
            case YQAuthoredSiteKind.SciFiSite:
                return YQSemanticExtractionTopology.SciFiSectors;
            default:
                return YQSemanticExtractionTopology.Unknown;
        }
    }

    private static float DefaultSpan(YQSemanticExtractionTopology topology)
    {
        return topology == YQSemanticExtractionTopology.WildernessRegions
            ? 140f
            : topology == YQSemanticExtractionTopology.DungeonRooms ||
              topology == YQSemanticExtractionTopology.InteriorRooms
                ? 44f
                : 84f;
    }

    private static float DefaultLayerHeight(
        YQSemanticExtractionTopology topology)
    {
        return topology == YQSemanticExtractionTopology.InteriorRooms
            ? 4.5f
            : topology == YQSemanticExtractionTopology.WildernessRegions
                ? 16f
                : 9f;
    }

    private static float DefaultLinkDistance(
        YQSemanticExtractionTopology topology)
    {
        return topology == YQSemanticExtractionTopology.WildernessRegions
            ? 26f
            : topology == YQSemanticExtractionTopology.DungeonRooms ||
              topology == YQSemanticExtractionTopology.InteriorRooms
                ? 10f
                : 18f;
    }

    private static IEnumerable<string> RequiredOutputs(
        YQSemanticExtractionTopology topology)
    {
        switch (topology)
        {
            case YQSemanticExtractionTopology.SettlementDistricts:
                return new[]
                {
                    "residential", "service", "civic", "circulation", "poi"
                };
            case YQSemanticExtractionTopology.DungeonRooms:
                return new[]
                {
                    "entrance", "chamber", "corridor", "encounter", "poi"
                };
            case YQSemanticExtractionTopology.InteriorRooms:
                return new[]
                {
                    "entrance", "room", "circulation", "service", "poi"
                };
            case YQSemanticExtractionTopology.LandmarkCampus:
                return new[]
                {
                    "approach", "core", "perimeter", "support", "poi"
                };
            case YQSemanticExtractionTopology.CampZones:
                return new[]
                {
                    "entrance", "command", "service", "habitation", "defense"
                };
            case YQSemanticExtractionTopology.WildernessRegions:
                return new[]
                {
                    "route", "vista", "encounter", "landmark", "transition"
                };
            case YQSemanticExtractionTopology.SciFiSectors:
                return new[]
                {
                    "entrance", "habitation", "service", "industry", "circulation", "poi"
                };
            default:
                return new[] { "unknown" };
        }
    }

    private static void WriteReport(
        List<YQSemanticExtractionProfile> profiles)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Semantic Extraction Profiles");
        report.AppendLine();
        report.AppendLine("| Pack | Topology | Assemblies | Span | Profile | Required outputs |");
        report.AppendLine("|---|---|---:|---:|---|---|");

        for (int index = 0; index < profiles.Count; index++)
        {
            YQSemanticExtractionProfile profile = profiles[index];
            report.AppendLine(
                "| " + profile.displayName + " | " + profile.topology +
                " | " + profile.minimumAssemblies + "-" +
                profile.maximumAssemblies + " | " +
                profile.targetHorizontalSpan.ToString("0.#") + "m | " +
                (profile.authoredOverride ? "Authored" : "Quarantined inferred") +
                " | " + string.Join(", ", profile.requiredSemanticOutputs) +
                " |");
        }

        report.AppendLine();
        report.AppendLine(
            "Profiles are extraction contracts, not release approval. Every emitted assembly must still pass visual, connectivity, collision, and prefab-integrity review.");
        File.WriteAllText(ReportPath, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }
}
