using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQProfileDrivenSemanticBenchmark
{
    private const string BenchmarkKitId = "medieval_viking_village";

    private const string OutputRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticBenchmark/MedievalVikingVillage";

    private const string ReviewScenePath =
        OutputRoot + "/YQ_ProfileDrivenVikingSemanticReview.unity";

    private const string ReportPath =
        OutputRoot + "/YQ_ProfileDrivenVikingSemanticReport.md";

    private static readonly Vector2[] GoldenControlAnchors =
    {
        new Vector2(-55f, 5f),
        new Vector2(-20f, 5f),
        new Vector2(-30f, -32f),
        new Vector2(10f, -25f)
    };

    private static readonly Color[] ZoneColors =
    {
        new Color(0.15f, 0.85f, 1f, 0.9f),
        new Color(1f, 0.72f, 0.15f, 0.9f),
        new Color(0.95f, 0.25f, 0.35f, 0.9f),
        new Color(0.35f, 1f, 0.38f, 0.9f)
    };

    private sealed class InstanceRecord
    {
        public Vector3 position;
        public string name = string.Empty;
        public string cohesiveFamily = string.Empty;
        public bool structural;
        public bool circulation;
        public HashSet<string> semanticTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        public int groupIndex;
    }

    private sealed class CohesiveAnchor
    {
        public Vector3 position;
        public string family = string.Empty;
        public int memberCount;
    }

    private sealed class GroupResult
    {
        public Vector3 center;
        public Bounds bounds;
        public int instanceCount;
        public int structuralCount;
        public HashSet<string> semanticTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        public float nearestGoldenDistance;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Rejected Prototypes/Run Viking Blind Semantic Clustering Benchmark")]
    public static void RunVikingProfileDrivenBenchmark()
    {
        EnsureFolderPath(OutputRoot);
        YQAuthoredSiteSourceCatalog sourceCatalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(
                YQAuthoredSiteSourceDiscovery.CatalogPath);
        YQSemanticExtractionProfileCatalog profileCatalog =
            YQSemanticExtractionProfileBuilder.SyncProfiles(false);

        if (sourceCatalog == null || profileCatalog == null)
            throw new InvalidOperationException(
                "Authored source or semantic profile catalog is unavailable.");

        YQAuthoredSiteSourceRecord source = sourceCatalog.Records.FirstOrDefault(
            record => record != null && string.Equals(
                record.kitId,
                BenchmarkKitId,
                StringComparison.OrdinalIgnoreCase));
        YQSemanticExtractionProfile profile =
            profileCatalog.Find(BenchmarkKitId);

        if (source == null || profile == null)
            throw new InvalidOperationException(
                "The Medieval Viking benchmark source/profile is missing.");

        GameObject sourcePrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(source.generatedPrefabPath);

        if (sourcePrefab == null)
            throw new InvalidOperationException(
                "The generated Viking authored-site candidate is missing.");

        GameObject loadedRoot =
            PrefabUtility.LoadPrefabContents(source.generatedPrefabPath);

        try
        {
            List<InstanceRecord> instances = CollectInstances(loadedRoot);
            int targetGroupCount = profile.minimumAssemblies;
            List<Vector3> centers = DiscoverCenters(
                instances,
                targetGroupCount,
                profile);
            AssignInstances(instances, centers, profile);
            List<GroupResult> groups = BuildGroupResults(
                instances,
                centers);
            List<string> errors = ValidateBenchmark(
                instances,
                groups,
                profile);
            BuildReviewScene(sourcePrefab, groups);
            WriteReport(instances, groups, profile, errors);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[YQProfileDrivenSemanticBenchmark] VIKING SEMANTIC BENCHMARK " +
                (errors.Count == 0 ? "PASSED" : "REJECTED") + "\n" +
                "Authored instances: " + instances.Count + "\n" +
                "Discovered semantic districts: " + groups.Count + "\n" +
                "Validation errors: " + errors.Count + "\n" +
                "Review scene: " + ReviewScenePath + "\n" +
                "Report: " + ReportPath + "\n" +
                "Release eligible: 0 (benchmark evidence only)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(loadedRoot);
        }
    }

    private static List<InstanceRecord> CollectInstances(GameObject root)
    {
        List<InstanceRecord> result = new List<InstanceRecord>();

        for (int index = 0; index < root.transform.childCount; index++)
        {
            GameObject child = root.transform.GetChild(index).gameObject;

            if (child.name.Equals("Sockets", StringComparison.OrdinalIgnoreCase))
                continue;

            string text = Normalize(child.name + " " +
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child));
            HashSet<string> tags = ResolveSemanticTags(text);
            string cohesiveFamily =
                ResolveCohesiveFamily(Normalize(child.name));
            result.Add(
                new InstanceRecord
                {
                    position = child.transform.localPosition,
                    name = child.name,
                    cohesiveFamily = cohesiveFamily,
                    structural = IsStructural(text),
                    circulation = tags.Contains("circulation"),
                    semanticTags = tags
                });
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                "The Viking authored candidate contains no semantic instances.");

        return result;
    }

    private static List<Vector3> DiscoverCenters(
        List<InstanceRecord> instances,
        int groupCount,
        YQSemanticExtractionProfile profile)
    {
        List<CohesiveAnchor> anchors = BuildCohesiveAnchors(
            instances,
            profile.cohesiveLinkDistance);

        if (anchors.Count < groupCount)
        {
            anchors = instances
                .Where(instance => instance.structural)
                .Select(instance => new CohesiveAnchor
                {
                    position = instance.position,
                    family = instance.name,
                    memberCount = 1
                })
                .ToList();
        }

        float neighborhoodRadius = Mathf.Max(
            profile.cohesiveLinkDistance * 2f,
            profile.targetHorizontalSpan / Mathf.Max(4f, groupCount));
        Dictionary<CohesiveAnchor, int> localDensities =
            CalculateLocalDensities(
                anchors,
                neighborhoodRadius,
                profile);
        int minimumDensity = Mathf.Max(
            2,
            Mathf.CeilToInt(
                anchors.Count / (float)(groupCount * 10)));
        List<CohesiveAnchor> candidates = anchors
            .Where(anchor => localDensities[anchor] >= minimumDensity)
            .ToList();

        if (candidates.Count < groupCount)
            candidates = anchors;

        List<CohesiveAnchor> rankedCandidates = candidates
            .OrderByDescending(anchor => localDensities[anchor])
            .ThenBy(anchor => anchor.family, StringComparer.Ordinal)
            .ThenBy(anchor => anchor.position.x)
            .ThenBy(anchor => anchor.position.z)
            .ToList();
        List<Vector3> centers = new List<Vector3>();
        float minimumSeparation = Mathf.Max(
            neighborhoodRadius,
            profile.targetHorizontalSpan /
            Mathf.Sqrt(Mathf.Max(2f, groupCount * 2f)));
        float minimumSeparationSquared =
            minimumSeparation * minimumSeparation;

        // note: Non-maximum suppression selects the center of each dense authored neighborhood rather than a peripheral point made attractive only by distance.
        for (int index = 0;
             index < rankedCandidates.Count && centers.Count < groupCount;
             index++)
        {
            CohesiveAnchor candidate = rankedCandidates[index];

            if (centers.Count == 0 || centers.All(center =>
                    HorizontalDistanceSquared(
                        candidate.position,
                        center,
                        profile) >= minimumSeparationSquared))
            {
                centers.Add(candidate.position);
            }
        }

        while (centers.Count < groupCount)
        {
            CohesiveAnchor next = candidates
                .Where(anchor => !centers.Contains(anchor.position))
                .OrderByDescending(anchor =>
                    centers.Min(center => HorizontalDistanceSquared(
                        anchor.position,
                        center,
                        profile)) *
                    Mathf.Sqrt(localDensities[anchor]))
                .ThenBy(anchor => anchor.family, StringComparer.Ordinal)
                .ThenBy(anchor => anchor.position.x)
                .ThenBy(anchor => anchor.position.z)
                .First();
            centers.Add(next.position);
        }

        // note: Coverage centers remain on dense authored neighborhoods instead of drifting toward the largest district and erasing smaller authored quarters.
        return centers;
    }

    private static Dictionary<CohesiveAnchor, int> CalculateLocalDensities(
        List<CohesiveAnchor> anchors,
        float neighborhoodRadius,
        YQSemanticExtractionProfile profile)
    {
        Dictionary<CohesiveAnchor, int> result =
            new Dictionary<CohesiveAnchor, int>();
        float radiusSquared = neighborhoodRadius * neighborhoodRadius;

        for (int anchorIndex = 0;
             anchorIndex < anchors.Count;
             anchorIndex++)
        {
            CohesiveAnchor anchor = anchors[anchorIndex];
            int neighbors = 0;

            for (int candidateIndex = 0;
                 candidateIndex < anchors.Count;
                 candidateIndex++)
            {
                if (HorizontalDistanceSquared(
                        anchor.position,
                        anchors[candidateIndex].position,
                        profile) <= radiusSquared)
                {
                    neighbors++;
                }
            }

            result.Add(anchor, neighbors);
        }

        return result;
    }

    private static List<CohesiveAnchor> BuildCohesiveAnchors(
        List<InstanceRecord> instances,
        float linkDistance)
    {
        List<CohesiveAnchor> result = new List<CohesiveAnchor>();
        IEnumerable<IGrouping<string, InstanceRecord>> families = instances
            .Where(instance => !string.IsNullOrWhiteSpace(
                instance.cohesiveFamily))
            .GroupBy(
                instance => instance.cohesiveFamily,
                StringComparer.OrdinalIgnoreCase);
        float maximumDistanceSquared = linkDistance * linkDistance;

        foreach (IGrouping<string, InstanceRecord> family in families)
        {
            HashSet<InstanceRecord> remaining =
                new HashSet<InstanceRecord>(family);

            while (remaining.Count > 0)
            {
                InstanceRecord seed = remaining.First();
                remaining.Remove(seed);
                Queue<InstanceRecord> frontier = new Queue<InstanceRecord>();
                List<InstanceRecord> members = new List<InstanceRecord>();
                frontier.Enqueue(seed);

                while (frontier.Count > 0)
                {
                    InstanceRecord current = frontier.Dequeue();
                    members.Add(current);
                    List<InstanceRecord> linked = remaining
                        .Where(candidate =>
                            (candidate.position - current.position)
                                .sqrMagnitude <= maximumDistanceSquared)
                        .ToList();

                    for (int index = 0; index < linked.Count; index++)
                    {
                        remaining.Remove(linked[index]);
                        frontier.Enqueue(linked[index]);
                    }
                }

                Vector3 centroid = Vector3.zero;

                for (int index = 0; index < members.Count; index++)
                    centroid += members[index].position;

                result.Add(
                    new CohesiveAnchor
                    {
                        position = centroid / members.Count,
                        family = family.Key,
                        memberCount = members.Count
                    });
            }
        }

        return result;
    }

    private static int FindNearestCenter(
        Vector3 position,
        List<Vector3> centers,
        YQSemanticExtractionProfile profile)
    {
        int nearest = 0;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < centers.Count; index++)
        {
            float distance = HorizontalDistanceSquared(
                position,
                centers[index],
                profile);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = index;
            }
        }

        return nearest;
    }

    private static void AssignInstances(
        List<InstanceRecord> instances,
        List<Vector3> centers,
        YQSemanticExtractionProfile profile)
    {
        for (int instanceIndex = 0;
             instanceIndex < instances.Count;
             instanceIndex++)
        {
            InstanceRecord instance = instances[instanceIndex];
            instance.groupIndex = FindNearestCenter(
                instance.position,
                centers,
                profile);
        }
    }

    private static float HorizontalDistanceSquared(
        Vector3 a,
        Vector3 b,
        YQSemanticExtractionProfile profile)
    {
        float verticalWeight = profile.topology ==
            YQSemanticExtractionTopology.InteriorRooms
                ? profile.targetHorizontalSpan /
                  Mathf.Max(1f, profile.verticalLayerHeight)
                : 0.2f;
        float dx = a.x - b.x;
        float dy = (a.y - b.y) * verticalWeight;
        float dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static List<GroupResult> BuildGroupResults(
        List<InstanceRecord> instances,
        List<Vector3> centers)
    {
        List<GroupResult> groups = new List<GroupResult>();

        for (int groupIndex = 0;
             groupIndex < centers.Count;
             groupIndex++)
        {
            List<InstanceRecord> members = instances
                .Where(instance => instance.groupIndex == groupIndex)
                .ToList();
            Bounds bounds = new Bounds(members[0].position, Vector3.one);
            HashSet<string> tags = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < members.Count; index++)
            {
                bounds.Encapsulate(members[index].position);
                tags.UnionWith(members[index].semanticTags);
            }

            // note: The density peak is the stable authored district origin; every assigned structure and prop remains represented by the bounds and counts.
            Vector3 center = centers[groupIndex];
            bounds.Expand(new Vector3(4f, 4f, 4f));
            float nearestGolden = GoldenControlAnchors.Min(anchor =>
                Vector2.Distance(
                    new Vector2(center.x, center.z),
                    anchor));
            groups.Add(
                new GroupResult
                {
                    center = center,
                    bounds = bounds,
                    instanceCount = members.Count,
                    structuralCount = members.Count(member => member.structural),
                    semanticTags = tags,
                    nearestGoldenDistance = nearestGolden
                });
        }

        return groups
            .OrderBy(group => group.center.x)
            .ThenBy(group => group.center.z)
            .ToList();
    }

    private static List<string> ValidateBenchmark(
        List<InstanceRecord> instances,
        List<GroupResult> groups,
        YQSemanticExtractionProfile profile)
    {
        List<string> errors = new List<string>();

        if (groups.Count < profile.minimumAssemblies ||
            groups.Count > profile.maximumAssemblies)
        {
            errors.Add("Discovered group count violates the authored profile.");
        }

        if (groups.Sum(group => group.instanceCount) != instances.Count)
            errors.Add("Not every authored instance was assigned exactly once.");

        for (int index = 0; index < groups.Count; index++)
        {
            GroupResult group = groups[index];

            if (group.instanceCount < 50)
                errors.Add("Semantic district " + index + " is under-populated.");
            if (group.structuralCount == 0)
                errors.Add("Semantic district " + index + " has no structure.");
            if (group.nearestGoldenDistance > 28f)
                errors.Add("Semantic district " + index +
                    " does not correspond to an authored golden district.");
        }

        for (int anchorIndex = 0;
             anchorIndex < GoldenControlAnchors.Length;
             anchorIndex++)
        {
            float nearest = groups.Min(group => Vector2.Distance(
                new Vector2(group.center.x, group.center.z),
                GoldenControlAnchors[anchorIndex]));

            if (nearest > 28f)
                errors.Add("Golden control district " + anchorIndex +
                    " was not independently rediscovered.");
        }

        HashSet<string> allTags = new HashSet<string>(
            groups.SelectMany(group => group.semanticTags),
            StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < profile.requiredSemanticOutputs.Count;
             index++)
        {
            string required = profile.requiredSemanticOutputs[index];

            if (!allTags.Contains(required))
                errors.Add("Required semantic evidence is missing: " + required + ".");
        }

        return errors;
    }

    private static HashSet<string> ResolveSemanticTags(string text)
    {
        HashSet<string> tags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (ContainsAny(text, "house", "home", "hut", "bed", "stable"))
            tags.Add("residential");
        if (ContainsAny(text, "smith", "forge", "market", "shop", "mill", "work", "turbine", "stable"))
            tags.Add("service");
        if (ContainsAny(text, "hall", "tower", "gate", "well", "statue", "temple", "meeting"))
            tags.Add("civic");
        if (ContainsAny(text, "road", "path", "street", "bridge", "stair", "walk", "deck"))
            tags.Add("circulation");
        if (ContainsAny(text, "house", "tower", "gate", "forge", "well", "turbine", "ship", "temple"))
            tags.Add("poi");

        return tags;
    }

    private static bool IsStructural(string text)
    {
        return ContainsAny(
            text,
            "house", "building", "wall", "roof", "floor", "tower",
            "gate", "stable", "forge", "temple", "bridge", "stair",
            "structure", "turbine", "platform", "deck");
    }

    private static string ResolveCohesiveFamily(string normalizedName)
    {
        string[] tokens = normalizedName.Split(
            new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];

            if (ContainsAny(
                    token,
                    "house", "stable", "tower", "turbine", "structure",
                    "building", "cabin", "hut", "tent", "temple",
                    "forge", "mansion", "hospital", "cathedral"))
            {
                return token;
            }
        }

        return string.Empty;
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        for (int index = 0; index < tokens.Length; index++)
        {
            if (text.Contains(tokens[index]))
                return true;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] characters = value.ToLowerInvariant().ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            if (!char.IsLetterOrDigit(characters[index]))
                characters[index] = ' ';
        }

        return new string(characters);
    }

    private static void BuildReviewScene(
        GameObject sourcePrefab,
        List<GroupResult> groups)
    {
        Scene scene = SceneManager.GetSceneByPath(ReviewScenePath);
        bool createdScene = !scene.IsValid() || !scene.isLoaded;

        if (createdScene)
        {
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
        }

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int index = 0; index < roots.Length; index++)
                UnityEngine.Object.DestroyImmediate(roots[index]);

            GameObject source = PrefabUtility.InstantiatePrefab(
                sourcePrefab,
                scene) as GameObject;

            if (source == null)
                throw new InvalidOperationException(
                    "Could not instantiate the Viking benchmark source.");

            source.name = "Authored Viking Source (Unmodified)";
            GameObject zones = new GameObject("Discovered Semantic Districts");
            SceneManager.MoveGameObjectToScene(zones, scene);

            for (int index = 0; index < groups.Count; index++)
            {
                GameObject zone = new GameObject(
                    "SemanticDistrict_" + index.ToString("00"));
                zone.transform.SetParent(zones.transform, false);
                YQSemanticBenchmarkZoneGizmo gizmo =
                    zone.AddComponent<YQSemanticBenchmarkZoneGizmo>();
                gizmo.Configure(
                    zone.name,
                    groups[index].bounds,
                    ZoneColors[index % ZoneColors.Length]);
            }

            if (!EditorSceneManager.SaveScene(scene, ReviewScenePath))
                throw new InvalidOperationException(
                    "Unity could not save the semantic benchmark review scene.");
        }
        finally
        {
            if (createdScene && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void WriteReport(
        List<InstanceRecord> instances,
        List<GroupResult> groups,
        YQSemanticExtractionProfile profile,
        List<string> errors)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# Profile-Driven Viking Semantic Benchmark");
        report.AppendLine();
        report.AppendLine("Result: " + (errors.Count == 0 ? "PASS" : "REJECTED"));
        report.AppendLine("Authored instances: " + instances.Count);
        report.AppendLine("Discovered districts: " + groups.Count);
        report.AppendLine();
        report.AppendLine("| District | Center | Instances | Structures | Golden distance | Semantics |");
        report.AppendLine("|---|---|---:|---:|---:|---|");

        for (int index = 0; index < groups.Count; index++)
        {
            GroupResult group = groups[index];
            report.AppendLine(
                "| " + index + " | (" + group.center.x.ToString("0.0") +
                ", " + group.center.y.ToString("0.0") + ", " +
                group.center.z.ToString("0.0") + ") | " +
                group.instanceCount + " | " + group.structuralCount + " | " +
                group.nearestGoldenDistance.ToString("0.0") + "m | " +
                string.Join(", ", group.semanticTags.OrderBy(tag => tag)) + " |");
        }

        report.AppendLine();
        report.AppendLine("## Validation");

        if (errors.Count == 0)
            report.AppendLine("- All benchmark gates passed.");
        else
        {
            for (int index = 0; index < errors.Count; index++)
                report.AppendLine("- " + errors[index]);
        }

        report.AppendLine();
        report.AppendLine(
            "Golden anchors are used only for validation after independent clustering; they do not seed or place discovered districts.");
        File.WriteAllText(ReportPath, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void EnsureFolderPath(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
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
