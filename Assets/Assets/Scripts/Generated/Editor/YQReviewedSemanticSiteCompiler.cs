using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQReviewedSemanticSiteCompiler
{
    private const string VikingKitId = "medieval_viking_village";

    private const string VikingDistrictFolder =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/Districts";

    private const string OutputFolder =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles/medieval_viking_village";

    private const string ManifestPath =
        OutputFolder + "/YQ_MedievalVikingVillage_ReviewedSemanticSite.asset";

    private const string ReportPath =
        OutputFolder + "/YQ_MedievalVikingVillage_ReviewedSemanticSiteReport.md";

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Semantic Authoring/Compile Reviewed Viking Semantic Districts")]
    public static void CompileReviewedVikingSemanticDistricts()
    {
        EnsureFolderPath(OutputFolder);
        YQSemanticExtractionProfileCatalog profileCatalog =
            YQSemanticExtractionProfileBuilder.SyncProfiles(false);
        YQSemanticExtractionProfile profile =
            profileCatalog != null
                ? profileCatalog.Find(VikingKitId)
                : null;

        if (profile == null)
            throw new InvalidOperationException(
                "The reviewed Viking semantic profile is unavailable.");

        string[] prefabPaths = AssetDatabase
            .FindAssets("t:Prefab", new[] { VikingDistrictFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.StartsWith(
                VikingDistrictFolder + "/",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        List<string> errors = new List<string>();
        List<YQReviewedSemanticZoneRecord> zones =
            new List<YQReviewedSemanticZoneRecord>();
        HashSet<string> allTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> stableIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < prefabPaths.Length; index++)
        {
            string prefabPath = prefabPaths[index];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            YQWorldAssemblyDescriptor assembly = prefab != null
                ? prefab.GetComponent<YQWorldAssemblyDescriptor>()
                : null;
            YQWorldDistrictDescriptor district = prefab != null
                ? prefab.GetComponent<YQWorldDistrictDescriptor>()
                : null;

            if (prefab == null || assembly == null || district == null)
            {
                errors.Add(Path.GetFileName(prefabPath) +
                    " is missing its reviewed district contract.");
                continue;
            }

            if (assembly.AssemblyKind != YQWorldAssemblyKind.District ||
                assembly.ReviewState != YQWorldAssemblyReviewState.ApprovedGolden ||
                !assembly.ReleaseEligible)
            {
                errors.Add(assembly.StableAssemblyId +
                    " has not been promoted through visual review.");
            }

            if (!stableIds.Add(assembly.StableAssemblyId))
                errors.Add("Duplicate reviewed district ID: " +
                    assembly.StableAssemblyId + ".");

            if (district.SourceInstanceCount <= 0 ||
                district.AuthoredBuildingCount <= 0)
            {
                errors.Add(assembly.StableAssemblyId +
                    " has incomplete authored instance evidence.");
            }

            HashSet<string> tags = new HashSet<string>(
                assembly.SemanticTags,
                StringComparer.OrdinalIgnoreCase);
            AddFunctionTags(district.DistrictFunction, tags);

            if (district.ConnectionSocketPaths.Count > 0)
                tags.Add("circulation");

            allTags.UnionWith(tags);
            zones.Add(new YQReviewedSemanticZoneRecord
            {
                stableId = assembly.StableAssemblyId,
                displayName = assembly.SourceFamily,
                districtFunction = district.DistrictFunction,
                prefab = prefab,
                authoredSourceOrigin = district.AuthoredSourceOrigin,
                localBoundsCenter = district.LocalBoundsCenter,
                localBoundsSize = district.LocalBoundsSize,
                sourceInstanceCount = district.SourceInstanceCount,
                authoredBuildingCount = district.AuthoredBuildingCount,
                authoredDressingCount = district.AuthoredDressingCount,
                semanticTags = tags.OrderBy(tag => tag).ToList(),
                connectionSocketPaths = district.ConnectionSocketPaths.ToList()
            });
        }

        if (zones.Count < profile.minimumAssemblies ||
            zones.Count > profile.maximumAssemblies)
        {
            errors.Add("Reviewed district count violates the semantic profile.");
        }

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            string required = profile.requiredSemanticOutputs[index];

            if (!allTags.Contains(required))
                errors.Add("Required semantic output is absent: " + required + ".");
        }

        int sourceInstanceCount = zones.Sum(zone => zone.sourceInstanceCount);

        if (sourceInstanceCount != 1051)
        {
            errors.Add("Reviewed districts preserve " + sourceInstanceCount +
                " source instances; the approved Viking extraction preserves 1051.");
        }

        YQReviewedSemanticSiteManifest manifest =
            AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(
                ManifestPath);

        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<
                YQReviewedSemanticSiteManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
        }

        // note: A rejected compile remains persisted as non-release evidence and can never enter runtime selection.
        manifest.Configure(
            VikingKitId,
            profile.semanticStyleKey,
            sourceInstanceCount,
            zones,
            errors.Count == 0);
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();
        WriteReport(zones, profile, errors);
        AssetDatabase.Refresh();

        Debug.Log(
            "[YQReviewedSemanticSiteCompiler] REVIEWED VIKING SEMANTIC SITE " +
            (errors.Count == 0 ? "READY" : "REJECTED") + "\n" +
            "Reviewed districts: " + zones.Count + "\n" +
            "Authored instances preserved: " + sourceInstanceCount + "\n" +
            "Validation errors: " + errors.Count + "\n" +
            "Manifest: " + ManifestPath + "\n" +
            "Report: " + ReportPath + "\n" +
            "Release eligible: " + (errors.Count == 0 ? "1" : "0"));
    }

    private static void AddFunctionTags(
        YQDistrictFunction function,
        HashSet<string> tags)
    {
        // note: Stable district functions become semantic selectors; prose never controls geometry or spawn behavior.
        switch (function)
        {
            case YQDistrictFunction.Residential:
                tags.Add("residential");
                tags.Add("poi");
                break;
            case YQDistrictFunction.MixedUse:
                tags.Add("residential");
                tags.Add("service");
                tags.Add("civic");
                tags.Add("poi");
                break;
            case YQDistrictFunction.Service:
                tags.Add("service");
                tags.Add("poi");
                break;
            case YQDistrictFunction.Defensive:
                tags.Add("civic");
                tags.Add("poi");
                break;
        }
    }

    private static void WriteReport(
        List<YQReviewedSemanticZoneRecord> zones,
        YQSemanticExtractionProfile profile,
        List<string> errors)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("# Reviewed Viking Semantic Site");
        text.AppendLine();
        text.AppendLine("Result: " +
            (errors.Count == 0 ? "READY" : "REJECTED"));
        text.AppendLine("Spatial authority: visually reviewed authored districts");
        text.AppendLine("Semantic profile: " + profile.semanticStyleKey);
        text.AppendLine();
        text.AppendLine("| District | Function | Instances | Buildings | Dressing | Origin | Tags |");
        text.AppendLine("|---|---|---:|---:|---:|---|---|");

        for (int index = 0; index < zones.Count; index++)
        {
            YQReviewedSemanticZoneRecord zone = zones[index];
            text.AppendLine("| " + zone.stableId + " | " +
                zone.districtFunction + " | " + zone.sourceInstanceCount +
                " | " + zone.authoredBuildingCount + " | " +
                zone.authoredDressingCount + " | " +
                Format(zone.authoredSourceOrigin) + " | " +
                string.Join(", ", zone.semanticTags) + " |");
        }

        text.AppendLine();
        text.AppendLine("## Validation");

        if (errors.Count == 0)
            text.AppendLine("- All reviewed spatial and semantic contracts passed.");
        else
            errors.ForEach(error => text.AppendLine("- " + error));

        text.AppendLine();
        text.AppendLine(
            "The LLM may select this site and its districts by semantic intent; approved prefab references and authored spatial composition remain deterministic runtime authority.");
        File.WriteAllText(ReportPath, text.ToString());
    }

    private static string Format(Vector3 value)
    {
        return "(" + value.x.ToString("0.0") + ", " +
            value.y.ToString("0.0") + ", " +
            value.z.ToString("0.0") + ")";
    }

    private static void EnsureFolderPath(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);

            current = next;
        }
    }
}
