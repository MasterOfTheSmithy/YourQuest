using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// This executable compiles the complete production registry source with the explicit Unity API stubs in AssetRegistryStubs.cs.
internal static class AssetRegistryTests
{
    private const string A = "Assets/Fixture/Pack/A.prefab", B = "Assets/Fixture/Pack/B.prefab";
    private const string LeafA = "YQWorldAssetLeaves/a", LeafB = "YQWorldAssetLeaves/b";
    private const string Clip = "YQWorldAssetDependencies/clip", Controller = "YQWorldAssetDependencies/controller";
    private static int cases;

    private static void Main(string[] args)
    {
        CheckPublishedMembership(args[0]);
        CheckPublishedFallbackAdmission(args[0], args[1]);
        CheckRequestOrderAndLeaseLifetime();
        CheckCancellationAndCompletionDispatch();
        CheckSessionEpoch();
        CheckResetDuringPreload();
        CheckMissingAndStaleMetadata();
        Console.WriteLine("PASS registry contract cases=" + cases + "; actual registry source, simulated Unity API; no Unity compile/native timing claim.");
    }

    private static YQRuntimeWorldAssetRegistry Fixture(bool metadata = true, bool stale = false)
    {
        YQRuntimeWorldAssetRegistry.ClearCachedInstance();
        Resources.Reset();
        var registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
        registry.SetLazyResourceShards(true);
        registry.SetGranularResourceShards(true);
        Resources.Assets["YQRuntimeWorldAssetRegistry"] = registry;
        Resources.Assets["YQWorldAssetManifest"] = new TextAsset(JsonConvert.SerializeObject(new
        {
            version = 1, assets = new[] { new { path = A, resource = LeafA, entrySha256 = "a" }, new { path = B, resource = LeafB, entrySha256 = "b" } }
        }));
        if (metadata) Resources.Assets["YQWorldPrefabDependencies"] = new TextAsset(JsonConvert.SerializeObject(new
        {
            version = 1, prefabs = new[] {
                new { path = A, entrySha256 = stale ? "outdated" : "a", resources = new[] { Clip, Controller } },
                new { path = B, entrySha256 = "b", resources = new[] { Clip } } }
        }));
        foreach (var pair in new[] { (A, LeafA), (B, LeafB) })
        {
            var leaf = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
            leaf.SetEntries(new List<YQRuntimeWorldAssetEntry> { new YQRuntimeWorldAssetEntry { assetPath = pair.Item1, prefab = new GameObject() } });
            Resources.Assets[pair.Item2] = leaf;
        }
        Resources.Assets[Clip] = new YQRuntimePrefabDependency { asset = new UnityEngine.Object(), sourceIdentity = "clip" };
        Resources.Assets[Controller] = new YQRuntimePrefabDependency { asset = new UnityEngine.Object(), sourceIdentity = "controller" };
        return YQRuntimeWorldAssetRegistry.Instance;
    }

    private static void CheckPublishedMembership(string repository)
    {
        var registry = Fixture(false);
        string text = File.ReadAllText(Path.Combine(repository, "Assets/Assets/Resources/YQWorldAssetManifest.json"));
        Resources.Assets["YQWorldAssetManifest"] = new TextAsset(text);
        JArray rows = (JArray)JObject.Parse(text)["assets"];
        foreach (JObject row in rows)
        {
            string path = (string)row["path"];
            Require(registry.ContainsPrefabPath(path) == path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase), "manifest membership " + path);
        }
        Require(!registry.ContainsPrefabPath("Assets/YughuesFreeBushes2018/Prefabs/P_Bush02.prefab"), "missing bush must not bind by suffix");
        Require(!registry.ContainsPrefabPath("Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_GrassMesh.prefab"), "missing grass must not bind by pack");
        Require(!registry.ContainsPrefabPath("Assets/Fixture/Pack/Unknown.prefab"), "invented path rejected");
        Require(Resources.Requests.Count == 0 && Resources.SyncLoads.All(path => path == "YQRuntimeWorldAssetRegistry" || path == "YQWorldAssetManifest"),
            "membership must not load source assets");
        Console.WriteLine("PASS all " + rows.Count + " published binding paths; no prefab loads during membership queries.");
        cases++;
    }

    private static void CheckRequestOrderAndLeaseLifetime()
    {
        var registry = Fixture();
        using var leaseA = registry.AcquireAssetResidency(new[] { A });
        using var leaseB = registry.AcquireAssetResidency(new[] { B });
        using var first = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
        using var second = new Driver(registry.PreloadPrefabDependenciesRoutine(B));
        first.Step(); second.Step();
        Require(Resources.Requests.Count == 1 && Resources.Requests[0].path == Clip, "overlapping demand shares one clip request");
        first.Step(); second.Step(); first.Step();
        Require(Resources.Requests.Count == 1 && Field<IDictionary>(registry, "_loadedDependencies").Count == 0,
            "early manual resumes wait for the native request instead of reading an unfinished asset");
        CompleteAll(first, second);
        Require(registry.HasPreparedPrefabDependencies(A) && registry.HasPreparedPrefabDependencies(B), "exact prepared dependencies available");
        Require(Resources.Requests.FindIndex(r => r.path == Clip) < Resources.Requests.FindIndex(r => r.path == Controller) &&
            Resources.Requests.FindIndex(r => r.path == Controller) < Resources.Requests.FindIndex(r => r.path == LeafA), "clip then controller then prefab");
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 0, "live demand survives cold reclamation");
        leaseA.Dispose();
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 2, "A-only controller and leaf retire");
        Require(registry.ResolvePreloadedPrefab(A) == null && registry.ResolvePreloadedPrefab(B) != null && registry.HasPreparedPrefabDependencies(B), "B retains shared clip and its own leaf");
        leaseB.Dispose(); leaseB.Dispose();
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 2, "last lease releases remaining cache");
        Require(Field<IDictionary>(registry, "_resourcePins").Count == 0, "no lease leak or negative pin");
        cases++;
    }

    private static void CheckPublishedFallbackAdmission(string repository, string fixturePath)
    {
        var registry = Fixture(false);
        Resources.Assets["YQWorldAssetManifest"] = new TextAsset(File.ReadAllText(Path.Combine(repository,
            "Assets/Assets/Resources/YQWorldAssetManifest.json")));
        List<YQSpatialAssetRecord> records = JsonConvert.DeserializeObject<List<YQSpatialAssetRecord>>(File.ReadAllText(fixturePath));
        YQWorldAssetCatalog.SetFixture(new YQWorldAssetIntakeCatalog { SpatialAssets = records });
        var accepted = new GeneratedAssetReferenceRecord { assetKey = "accepted-sentinel", assetPath = "Assets/Accepted/Existing.prefab", weight = 917 };
        List<GeneratedAssetReferenceRecord> selected = YQEnvironmentFallbackFixture.Apply(accepted);
        Require(selected.Count == 1 && ReferenceEquals(selected[0], accepted) && accepted.weight == 917,
            "all eight unapproved proposals are rejected without rewriting accepted references");
        int bound = 0;
        foreach (YQSpatialAssetRecord record in records)
        {
            // A curation update alone cannot invent a missing player binding; exercise each published proposal independently.
            record.releaseEligible = true;
            record.disposition = YQAssetIntakeDisposition.Candidate;
            record.spatialMetadataAuthored = true;
            record.curationV2 = new YQAssetCurationContractV2 { contractVersion = 2 };
            selected = YQEnvironmentFallbackFixture.Apply(accepted);
            bool present = registry.ContainsPrefabPath(record.assetPath);
            Require(selected.Any(item => item.assetPath == record.assetPath) == present, "approval and exact binding both required: " + record.assetPath);
            if (present) bound++;
            record.releaseEligible = false;
        }
        Require(bound == 3, "published fallback binding fixture remains three present and five absent");
        Require(Resources.Requests.Count == 0, "fallback admission never loads candidate prefab objects");
        Console.WriteLine("PASS actual fallback/approval method bodies: eight unapproved proposals rejected; five remain rejected after approval when binding absent; accepted reference retained.");
        cases++;
    }

    private static void CheckCancellationAndCompletionDispatch()
    {
        var registry = Fixture();
        var lease = registry.AcquireAssetResidency(new[] { A });
        var consumer = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
        consumer.Step();
        ResourceRequest request = Resources.Requests.Single();
        consumer.Dispose(); lease.Dispose();
        Require(Field<int>(registry, "_activePreloads") == 0, "cancellation releases preload scope");
        Resources.Complete(request);
        Require(Field<IDictionary>(registry, "_pendingPreloads").Count == 0, "orphaned native completion retires its request");
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 1, "canceled descriptor becomes reclaimable");

        registry = Fixture();
        using var driver = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
        driver.Step();
        Resources.Complete(Resources.Requests.Single(), false);
        driver.Step();
        Require(Resources.Requests.Count == 2 && Resources.Requests[1].path == Controller, "isDone resumes before completion event without a missing dependency");
        CompleteAll(driver);
        Require(registry.HasPreparedPrefabDependencies(A), "late completion callback does not prevent prepared result");
        cases++;
    }

    private static void CheckSessionEpoch()
    {
        var registry = Fixture();
        var old = registry.AcquireAssetResidency(new[] { A });
        registry.SetGranularResourceShards(true);
        using var current = registry.AcquireAssetResidency(new[] { A });
        old.Dispose();
        Require(Field<IDictionary>(registry, "_resourcePins").Count == 3, "old epoch lease cannot unpin current resources");
        using var driver = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
        CompleteAll(driver);
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 0, "new session retains its own demand");
        current.Dispose();
        Require(YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(1000) == 3, "current epoch release remains effective");
        cases++;
    }

    private static void CheckMissingAndStaleMetadata()
    {
        foreach (bool stale in new[] { false, true })
        {
            var registry = Fixture(stale, stale);
            using var driver = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
            CompleteAll(driver);
            Require(Resources.Requests.Count == 1 && Resources.Requests[0].path == LeafA, "legacy path still loads asynchronously without dependency discovery");
            Require(registry.ResolvePreloadedPrefab(A) != null && !registry.HasPreparedPrefabDependencies(A), "missing/stale metadata cannot claim qualified warming");
            Require(Debug.Messages.Any(line => line.Contains("missing/stale")), "metadata prerequisite is explicit");
            Require(!Resources.SyncLoads.Contains(LeafA), "legacy compatibility must not introduce a synchronous leaf load");
        }
        var failed = Fixture();
        Resources.Assets.Remove(Clip);
        using var failureDriver = new Driver(failed.PreloadPrefabDependenciesRoutine(A));
        CompleteAll(failureDriver);
        Require(!failed.HasPreparedPrefabDependencies(A) && Debug.Messages.Any(line => line.StartsWith("error:")), "failed descriptor cannot certify warming");
        var malformed = Fixture(false);
        Resources.Assets["YQWorldPrefabDependencies"] = new TextAsset("{invalid json");
        using var malformedLease = malformed.AcquireAssetResidency(new[] { A });
        using var malformedDriver = new Driver(malformed.PreloadPrefabDependenciesRoutine(A));
        CompleteAll(malformedDriver);
        Require(malformed.ResolvePreloadedPrefab(A) != null && !malformed.HasPreparedPrefabDependencies(A) &&
            Debug.Messages.Any(line => line.Contains("could not be read")), "malformed optional metadata remains explicitly unqualified and loadable");
        cases++;
    }

    private static void CheckResetDuringPreload()
    {
        foreach (bool disableLazy in new[] { false, true })
        {
            var registry = Fixture();
            using var oldDriver = new Driver(registry.PreloadPrefabDependenciesRoutine(A));
            oldDriver.Step();
            ResourceRequest old = Resources.Requests.Single();
            if (disableLazy) registry.SetLazyResourceShards(false);
            else registry.SetGranularResourceShards(true);
            oldDriver.Step();
            Require(oldDriver.Done && Resources.Requests.Count == 1, "epoch reset prevents the old iterator from issuing subsequent requests");
            Resources.Complete(old);
            Require(Field<IDictionary>(registry, "_pendingPreloads").Count == 0 &&
                Field<IDictionary>(registry, "_loadedDependencies").Count == 0 && Field<int>(registry, "_activePreloads") == 0,
                "late old completion cannot populate or decrement the new session");
        }
        cases++;
    }

    private static void CompleteAll(params Driver[] drivers)
    {
        for (int pass = 0; pass < 100 && drivers.Any(driver => !driver.Done); pass++)
        {
            foreach (Driver driver in drivers) driver.Step();
            foreach (ResourceRequest request in Resources.Requests.Where(item => !item.isDone).ToArray()) Resources.Complete(request);
        }
        Require(drivers.All(driver => driver.Done), "requests finish within bounded test steps");
    }

    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    private static void Require(bool condition, string description) { if (!condition) throw new Exception(description); }

    private sealed class Driver : IDisposable
    {
        private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private ResourceRequest wait;
        public bool Done => stack.Count == 0;
        public Driver(IEnumerator iterator) { stack.Push(iterator); }
        public void Step()
        {
            if (wait != null && !wait.isDone) return;
            wait = null;
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                if (top.Current is ResourceRequest request) { wait = request; return; }
                return;
            }
        }
        public void Dispose() { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
}
