using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// note: Edit-only fixtures exercise the existing site binder without touching accepted profiles, catalogs or scene objects.
public static class YQEnvironmentBindingVerification
{
    [Serializable]
    private sealed class Check
    {
        public string name;
        public string verdict;
        public string expected;
        public string actual;
    }

    [Serializable]
    private sealed class Receipt
    {
        public string utc;
        public string evidenceLevel = "EDIT_MODE_SYNTHETIC_CONTRACT";
        public string unityVersion;
        public string runtimeMvid;
        public string editorMvid;
        public string runtimeAssemblySha256;
        public string editorAssemblySha256;
        public string siteBinderSourceSha256;
        public string bindingVersion;
        public string verdict;
        public List<Check> checks = new List<Check>();
    }

    [MenuItem("YourQuest/Verification/Run Environment Binding Contracts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Environment binding contracts require stable Edit Mode.");

        Receipt receipt = new Receipt
        {
            utc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            runtimeMvid = typeof(YQCompiledWorldSiteBindingService).Assembly.ManifestModule.ModuleVersionId.ToString(),
            editorMvid = typeof(YQEnvironmentBindingVerification).Assembly.ManifestModule.ModuleVersionId.ToString(),
            runtimeAssemblySha256 = Hash(typeof(YQCompiledWorldSiteBindingService).Assembly.Location),
            editorAssemblySha256 = Hash(typeof(YQEnvironmentBindingVerification).Assembly.Location),
            siteBinderSourceSha256 = Hash("Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs"),
            bindingVersion = YQCompiledWorldSiteBindingService.BindingVersion
        };
        YQRuntimeWorldSiteCatalog fixture = ScriptableObject.CreateInstance<YQRuntimeWorldSiteCatalog>();
        FieldInfo cache = typeof(YQCompiledWorldSiteBindingService).GetField("catalog", BindingFlags.NonPublic | BindingFlags.Static);
        object originalCache = cache.GetValue(null);
        try
        {
            // note: Replace only the binder's transient cache for the duration of these synthetic calls, then restore it even after a failure.
            YQRuntimeWorldSiteRecord first = Site("fixture_a");
            YQRuntimeWorldSiteRecord second = Site("fixture_b");
            fixture.Configure(new[] { first, second });
            cache.SetValue(null, fixture);
            string forwardCatalog = CompatibleIds(fixture);
            fixture.Configure(new[] { second, first });
            Add(receipt, "compatible site enumeration survives catalog reorder", forwardCatalog, CompatibleIds(fixture));

            fixture.Configure(new[] { first, second });
            for (int seed = 0; seed < 4; seed++)
            {
                string forward = ResolvePlan("ec_binding_seed_" + seed, false);
                string reverse = ResolvePlan("ec_binding_seed_" + seed, true);
                Add(receipt, "settlement binding query order seed " + seed, forward, reverse);
                fixture.Configure(new[] { second, first });
                Add(receipt, "settlement binding catalog order seed " + seed, forward, ResolvePlan("ec_binding_seed_" + seed, false));
                fixture.Configure(new[] { first, second });
                Add(receipt, "settlement binding shuffled plan and save round-trip seed " + seed, forward,
                    ResolvePlan("ec_binding_seed_" + seed, true, true));
            }

            GeneratedWorldPlanRecord accepted = Plan("ec_accepted_binding");
            GeneratedSettlementRecord pinned = accepted.settlements[0];
            pinned.runtimeSiteKitId = second.kitId;
            pinned.runtimeSiteSemanticStyle = second.semanticStyleKey;
            pinned.runtimeSiteBindingVersion = "reviewed-site-binding-4-semantic-slices";
            string before = HashText(JsonUtility.ToJson(pinned));
            fixture.Configure(new[] { Site("fixture_added"), first, second });
            bool resolved = YQCompiledWorldSiteBindingService.TryResolveSettlementSite(accepted, pinned, null, null,
                out YQRuntimeWorldSiteRecord selected, out bool changed);
            Add(receipt, "accepted version four binding survives additions and reorder", "True|False|fixture_b|" + before,
                resolved + "|" + changed + "|" + selected?.kitId + "|" + HashText(JsonUtility.ToJson(pinned)));
            Add(receipt, "same accepted kit preserves its committed version", pinned.runtimeSiteBindingVersion,
                YQCompiledWorldSiteBindingService.VersionForCommittedKit(pinned.runtimeSiteKitId, pinned.runtimeSiteBindingVersion, pinned.runtimeSiteKitId));
            Add(receipt, "changed kit receives current binding version", YQCompiledWorldSiteBindingService.BindingVersion,
                YQCompiledWorldSiteBindingService.VersionForCommittedKit(pinned.runtimeSiteKitId, pinned.runtimeSiteBindingVersion, first.kitId));

            GeneratedWorldPlanRecord probePlan = Plan("ec_no_commit_probe");
            string probeBefore = HashText(JsonUtility.ToJson(probePlan));
            YQCompiledWorldSiteBindingService.TryResolveSettlementSite(probePlan, probePlan.settlements[1], null, null,
                out _, out _, persistBinding: false);
            Add(receipt, "selection-only query preserves plan", probeBefore, HashText(JsonUtility.ToJson(probePlan)));

            bool excludedResolved = YQCompiledWorldSiteBindingService.TryResolveSettlementSite(probePlan, probePlan.settlements[0], null, null,
                out YQRuntimeWorldSiteRecord excluded, out bool excludedChanged,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "fixture_a", "fixture_b", "fixture_added" });
            Add(receipt, "exhausted eligibility remains a rejection", "False|True|False", excludedResolved + "|" + (excluded == null) + "|" + excludedChanged);

            // note: Mixed domains share one feature-order policy; ordinary campsite art never gains habitation or service capability.
            YQRuntimeWorldSiteRecord campA = Site("camp_a");
            YQRuntimeWorldSiteRecord campB = Site("camp_b");
            foreach (YQRuntimeWorldSiteRecord camp in new[] { campA, campB })
            {
                camp.siteKind = YQAuthoredSiteKind.Camp;
                camp.reviewedFunctionsV2 = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Encounter, YQAssetFunctionV2.Reward, YQAssetFunctionV2.Security };
            }
            fixture.Configure(new[] { first, campA, second, campB });
            for (int seed = 0; seed < 4; seed++)
                Add(receipt, "mixed settlement/encampment query order seed " + seed,
                    ResolveMixed("ec_mixed_" + seed, false), ResolveMixed("ec_mixed_" + seed, true));
        }
        catch (Exception exception)
        {
            Add(receipt, "fixture execution", "no exception", exception.ToString());
        }
        finally
        {
            cache.SetValue(null, originalCache);
            UnityEngine.Object.DestroyImmediate(fixture);
        }

        receipt.verdict = receipt.checks.Exists(check => check.verdict == "FAIL") ? "FAIL" : "PASS";
        string directory = Path.Combine("outputs/G08_EnvironmentCohesion_20261003", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_binding_contracts");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Receipt.json"), JsonUtility.ToJson(receipt, true));
        Debug.Log("[YQEnvironmentBindingContracts] " + receipt.verdict + "; checks=" + receipt.checks.Count + "; " + directory);
    }

    private static void Add(Receipt receipt, string name, string expected, string actual)
    {
        receipt.checks.Add(new Check { name = name, expected = expected, actual = actual,
            verdict = string.Equals(expected, actual, StringComparison.Ordinal) ? "PASS" : "FAIL" });
    }

    private static YQRuntimeWorldSiteRecord Site(string id)
    {
        return new YQRuntimeWorldSiteRecord { kitId = id, semanticStyleKey = "medieval", siteKind = YQAuthoredSiteKind.Settlement,
            presentationMode = YQWorldSitePresentationMode.SeamlessExterior, spatiallyValidated = true, seamlessPlacementEligible = true,
            reviewedFunctionsV2 = new List<YQAssetFunctionV2> { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Service, YQAssetFunctionV2.Circulation } };
    }

    private static GeneratedWorldPlanRecord Plan(string seed)
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord { worldSeed = seed };
        plan.EnsureCollections();
        for (int index = 0; index < 4; index++)
            plan.settlements.Add(new GeneratedSettlementRecord { settlementId = "settlement_" + index, deterministicSeed = "shared_fixture_seed", kind = "village" });
        return plan;
    }

    private static string ResolvePlan(string seed, bool reverse, bool shuffledRoundTrip = false)
    {
        GeneratedWorldPlanRecord plan = Plan(seed);
        if (shuffledRoundTrip)
        {
            plan.settlements.Reverse();
            plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(plan));
        }
        for (int step = 0; step < plan.settlements.Count; step++)
        {
            int index = reverse ? plan.settlements.Count - 1 - step : step;
            if (!YQCompiledWorldSiteBindingService.TryResolveSettlementSite(plan, plan.settlements[index], null, null, out _, out _))
                throw new InvalidOperationException("Synthetic eligible settlement was not resolved: " + index);
        }
        List<string> bindings = plan.settlements.ConvertAll(record => record.settlementId + "=" + record.runtimeSiteKitId);
        bindings.Sort(StringComparer.Ordinal);
        return string.Join("|", bindings);
    }

    private static string ResolveMixed(string seed, bool reverse)
    {
        GeneratedWorldPlanRecord plan = Plan(seed);
        for (int index = 0; index < 4; index++)
            plan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "encampment_" + index, deterministicSeed = "shared_fixture_seed", kind = "camp" });
        for (int step = 0; step < 8; step++)
        {
            int index = reverse ? 7 - step : step;
            bool resolved = index < 4
                ? YQCompiledWorldSiteBindingService.TryResolveSettlementSite(plan, plan.settlements[index], null, null, out _, out _)
                : YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(plan, plan.encampments[index - 4], null, null, out _, out _);
            if (!resolved) throw new InvalidOperationException("Synthetic mixed site was not resolved: " + index);
        }
        List<string> bindings = plan.settlements.ConvertAll(record => record.settlementId + "=" + record.runtimeSiteKitId);
        bindings.AddRange(plan.encampments.ConvertAll(record => record.encampmentId + "=" + record.runtimeSiteKitId));
        bindings.Sort(StringComparer.Ordinal);
        return string.Join("|", bindings);
    }

    private static string CompatibleIds(YQRuntimeWorldSiteCatalog fixture)
    {
        List<string> ids = new List<string>();
        foreach (YQRuntimeWorldSiteRecord site in fixture.FindCompatibleSites(new YQRuntimeWorldSiteQuery()))
            ids.Add(site.kitId);
        return string.Join("|", ids);
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string HashText(string value)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
    }
}
