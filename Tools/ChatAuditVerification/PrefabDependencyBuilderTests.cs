// Managed contract tests for the complete production Editor builder and dependency descriptor.
// The controlled AssetDatabase has no Unity importer, native object graph, or performance model.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class PrefabDependencyBuilderTests
{
    private const string ResourcesRoot = "Assets/Assets/Resources";
    private const string BindingManifest = ResourcesRoot + "/YQWorldAssetManifest.json";
    private const string DependencyManifest = ResourcesRoot + "/YQWorldPrefabDependencies.json";
    private const string RegistryPath = ResourcesRoot + "/YQRuntimeWorldAssetRegistry.asset";
    private const string LogicalPath = "Assets/YourQuest DOT Creatures/cairnback/cairnback_darkness_affinity_set_v1.prefab";
    private const string MovedPath = "Assets/RelocatedCreatures/RockMonsters/Cairnback/darkness.prefab";
    private const string PrefabGuid = "c5741d01345fda5449c0ff9e0d97c9cf";
    private const long PrefabId = 5846611850104292756L;
    private const string ResourceHash = "91d84b5f1c2626c2ef1d7ab772e3eec35f2241c60df6f658c3ab6a756262f99c";
    private const string ExactEntryHash = "7821b2aaca638b2a5c95d72ce0803a59318e63552922a28c1ae8f501f033c359";
    private const string ExactLeafHash = "09232d07b9aa0aa2bc3912f0891adbe2b4f47619cd7cce39ff019daf933132e6";
    private const string BindingOwner = "YourQuest.GranularWorldAssetResources.v1";
    private const string ClipPath = "Assets/FixtureAnimations/Walk.anim";
    private const string ModelPath = "Assets/FixtureAnimations/Body.fbx";
    private const string ControllerPath = "Assets/FixtureAnimations/Locomotion.controller";
    private const string ClipIdentity = "dddddddddddddddddddddddddddddddd:7400000";
    private const string ModelClipIdentity = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb:740000002";
    private const string ControllerIdentity = "11111111111111111111111111111111:9100000";
    private static readonly string[] ExpectedResources = new[] { ModelClipIdentity, ClipIdentity, ControllerIdentity }
        .Select(identity => "YQWorldAssetDependencies/" + Hash(identity)).ToArray();

    private const string LeafHeader = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
        "  m_Script: {fileID: 11500000, guid: cad7c799f2ef4fd4ea812d39a104736a, type: 3}\n" +
        "  m_Name: YQWorldAsset_" + ResourceHash + "\n" +
        "  m_EditorClassIdentifier: Assembly-CSharp::YQRuntimeWorldAssetRegistry\n  entries:\n";
    private const string LeafTail = "  useLazyResourceShards: 0\n  useGranularResourceShards: 0\n  terrainGrassTexture: {fileID: 0}\n";

    // note: These fixture bytes reproduce the committed cairnback row and conserved entry, including its 64-bit root fileID.
    private static string Entry(string path = LogicalPath, string guid = PrefabGuid, string fileId = "5846611850104292756") =>
        "  - assetPath: " + path + "\n    prefab: {fileID: " + fileId + ", guid: " + guid + ", type: 3}\n" +
        "    material: {fileID: 0}\n    materialOverrides: []\n";

    private sealed class Fixture : IDisposable
    {
        private readonly string previousDirectory = Directory.GetCurrentDirectory();
        private readonly string directory = Path.Combine(Path.GetTempPath(), "yourquest-prefab-fixture-" + Guid.NewGuid().ToString("N"));
        public readonly JObject Row;
        public readonly JObject Manifest;
        public readonly GameObject Prefab = new GameObject { name = "cairnback" };
        public string PhysicalPath;
        public string LeafPath => ResourcesRoot + "/" + (string)Row["resource"] + ".asset";
        public JArray Rows => (JArray)Manifest["assets"];

        public Fixture(bool moved = false)
        {
            AssetDatabase.Reset();
            Directory.CreateDirectory(directory);
            Directory.SetCurrentDirectory(directory);
            PhysicalPath = moved ? MovedPath : LogicalPath;
            Put(PhysicalPath, "controlled imported prefab source\n");
            AssetDatabase.Register(PhysicalPath, PrefabGuid, PrefabId, Prefab);
            AssetDatabase.DependencyHashes[PhysicalPath] = "prefab-dependencies-v1";
            Put(RegistryPath, "controlled root registry\n");
            AssetDatabase.MainAssets[RegistryPath] = new YQRuntimeWorldAssetRegistry();

            Put(ClipPath, "controlled animation source\n");
            AssetDatabase.Register(ClipPath, "dddddddddddddddddddddddddddddddd", 7400000L, new AnimationClip { name = "Walk" });
            Put(ControllerPath, "controlled controller source\n");
            AssetDatabase.Register(ControllerPath, "11111111111111111111111111111111", 9100000L,
                new RuntimeAnimatorController { name = "Locomotion" });
            Put(ModelPath, "controlled model source\n");
            AssetDatabase.Register(ModelPath, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 100100000L, new GameObject { name = "Body" });
            AssetDatabase.Register(ModelPath, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 740000002L, new AnimationClip { name = "Idle" });
            AssetDatabase.Register(ModelPath, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", 740000003L,
                new AnimationClip { name = "__preview__Idle" });
            AssetImporter.Importers[ModelPath] = new ModelImporter { importAnimation = true };
            // note: Deliberately unsorted, shared dependency paths exercise actual class ordering, deduplication, and embedded-clip discovery.
            AssetDatabase.Dependencies[PhysicalPath] = new[] { ControllerPath, PhysicalPath, ModelPath, ClipPath, ModelPath };
            Row = new JObject
            {
                ["path"] = LogicalPath,
                ["resource"] = "YQWorldAssetLeaves/" + ResourceHash,
                ["source"] = ResourcesRoot + "/YQWorldAssetShards/YQWorldAssets_yourquest_dot_creatures_cairnback.asset"
            };
            Manifest = new JObject { ["version"] = 1, ["generator"] = BindingOwner, ["assets"] = new JArray(Row) };
            WriteEntry(Entry());
        }

        public void WriteEntry(string entry, bool crlf = false)
        {
            string leaf = LeafHeader + entry + LeafTail;
            Row["entrySha256"] = Hash(entry);
            Row["leafSha256"] = Hash(leaf);
            Put(LeafPath, crlf ? leaf.Replace("\n", "\r\n") : leaf);
            WriteManifest();
        }

        public void WriteManifest() => Put(BindingManifest, Manifest.ToString(Formatting.Indented));

        public void MoveSource(string destination, string dependencyHash)
        {
            string oldPath = PhysicalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Move(oldPath, destination);
            AssetDatabase.GuidPaths[PrefabGuid] = destination;
            AssetDatabase.PathGuids.Remove(oldPath);
            AssetDatabase.PathGuids[destination] = PrefabGuid;
            AssetDatabase.ObjectTypes.Remove((oldPath, PrefabId));
            AssetDatabase.ObjectTypes[(destination, PrefabId)] = typeof(GameObject);
            AssetDatabase.MainAssets.Remove(oldPath);
            AssetDatabase.MainAssets[destination] = Prefab;
            AssetDatabase.MainTypes.Remove(oldPath);
            AssetDatabase.MainTypes[destination] = typeof(GameObject);
            AssetDatabase.ImportedAssets.Remove(oldPath);
            AssetDatabase.ImportedAssets[destination] = new UnityEngine.Object[] { Prefab };
            AssetDatabase.ObjectPaths[Prefab] = destination;
            AssetDatabase.Dependencies[destination] = AssetDatabase.Dependencies[oldPath].Select(path => path == oldPath ? destination : path).ToArray();
            AssetDatabase.Dependencies.Remove(oldPath);
            AssetDatabase.DependencyHashes.Remove(oldPath);
            AssetDatabase.DependencyHashes[destination] = dependencyHash;
            PhysicalPath = destination;
        }

        public void AssertNoPublication()
        {
            Check(AssetDatabase.Writes.Count == 0, "Rejected preparation called an Editor write API: " + string.Join(", ", AssetDatabase.Writes));
            Check(!File.Exists(DependencyManifest), "Rejected preparation published a dependency manifest.");
            Check(!Directory.Exists(ResourcesRoot + "/YQWorldAssetDependencies"), "Rejected preparation created descriptor output.");
            Check(YQRuntimeWorldAssetRegistry.ClearCalls == 0, "Rejected preparation reset the runtime registry.");
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Directory.Delete(directory, true);
        }
    }

    private static void OrdinaryBinding()
    {
        using (var fixture = new Fixture())
        {
            Check(Hash(LogicalPath.ToLowerInvariant()) == ResourceHash, "The real runtime key resource hash changed in this fixture.");
            Check((string)fixture.Row["entrySha256"] == ExactEntryHash, "The exact published entry hash was not reproduced.");
            Check((string)fixture.Row["leafSha256"] == ExactLeafHash, "The exact published leaf hash was not reproduced.");
            string conservedManifest = File.ReadAllText(BindingManifest), conservedLeaf = File.ReadAllText(fixture.LeafPath);
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertPublishedRow();
            Check(AssetDatabase.Writes.Count(value => value.StartsWith("asset: ", StringComparison.Ordinal)) == 3,
                "Expected two exact clips and one controller, with shared and preview objects excluded.");
            Check(AssetDatabase.GuidReads.All(value => value.options == AssetPathToGUIDOptions.OnlyExistingAssets), "Source GUID checks must exclude recently deleted paths.");
            Check(AssetDatabase.TypeReads.Count > 0 && AssetDatabase.TypeReads.All(value => value.fileId == PrefabId), "The prefab fileID was truncated or ignored.");
            Check(!AssetDatabase.ObjectLoads.Contains(fixture.PhysicalPath) && !AssetDatabase.ObjectLoads.Contains(fixture.LeafPath),
                "Source resolution loaded a prefab or leaf object graph.");
            AssertReadOnly(YQRuntimePrefabDependencyBuilder.ValidateCurrent);
            Check(File.ReadAllText(BindingManifest) == conservedManifest && File.ReadAllText(fixture.LeafPath) == conservedLeaf,
                "Building dependencies rewrote conserved runtime bindings.");
        }
    }

    private static void MovedBinding()
    {
        // note: This case intentionally fails the original 94c builder at its File.Exists(logical key) gate.
        foreach (bool historicalPathHasWrongPrefab in new[] { false, true })
        using (var fixture = new Fixture(moved: true))
        {
            if (historicalPathHasWrongPrefab)
            {
                Put(LogicalPath, "unrelated replacement occupying the historical path\n");
                AssetDatabase.Register(LogicalPath, "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", 100100000L, new GameObject { name = "wrong prefab" });
                AssetDatabase.DependencyHashes[LogicalPath] = "wrong-prefab-hash";
                AssetDatabase.Dependencies[LogicalPath] = Array.Empty<string>();
            }
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertPublishedRow();
            AssertReadOnly(YQRuntimePrefabDependencyBuilder.ValidateCurrent);
            Check(AssetDatabase.DependencyReads.Count > 0 && AssetDatabase.DependencyReads.All(path => path == MovedPath),
                "Discovery used the historical key or a name/suffix fallback instead of the conserved GUID.");
            Check(AssetDatabase.HashReads.All(path => path == MovedPath), "Validation hashed the logical path or its unrelated replacement.");
            Check(AssetDatabase.TypeReads.All(value => value.path == MovedPath && value.fileId == PrefabId),
                "Moved source validation did not preserve exact GUID/fileID identity.");
        }
    }

    private static void ConservedTextFormats()
    {
        using (var fixture = new Fixture(moved: true))
        {
            fixture.WriteEntry(Entry(), crlf: true);
            Check((string)fixture.Row["leafSha256"] == ExactLeafHash, "CRLF must retain the generator's LF-normalized hash.");
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertPublishedRow();
        }
        using (var fixture = new Fixture(moved: true))
        {
            fixture.WriteEntry(Entry().Replace("YourQuest DOT Creatures/", "YourQuest DOT\n      Creatures/"), crlf: true);
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertPublishedRow((string)fixture.Row["entrySha256"]);
            AssertReadOnly(YQRuntimePrefabDependencyBuilder.ValidateCurrent);
        }
    }

    private static void MissingBindings()
    {
        Reject(fixture => File.Delete(fixture.LeafPath), "Missing runtime leaf");
        Reject(fixture => AssetDatabase.GuidPaths.Remove(PrefabGuid), "Missing imported prefab source");
        Reject(fixture => File.Delete(fixture.PhysicalPath), "Missing imported prefab source");
        Reject(fixture => AssetDatabase.GuidPaths[PrefabGuid] = "Packages/OutsideProject/cairnback.prefab", "Missing imported prefab source");
    }

    private static void ExactIdentity()
    {
        Reject(fixture => AssetDatabase.PathGuids[fixture.PhysicalPath] = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "GUID/fileID identity mismatch");
        Reject(fixture =>
        {
            AssetDatabase.ObjectTypes.Remove((fixture.PhysicalPath, PrefabId));
            AssetDatabase.ObjectTypes[(fixture.PhysicalPath, PrefabId + 1)] = typeof(GameObject);
        }, "GUID/fileID identity mismatch");
        Reject(fixture => AssetDatabase.ObjectTypes[(fixture.PhysicalPath, PrefabId)] = typeof(Material), "GUID/fileID identity mismatch");
        Reject(fixture => fixture.WriteEntry(Entry(guid: new string('0', 32))), "one persistent prefab GUID/fileID");
        Reject(fixture => fixture.WriteEntry(Entry(fileId: "0")), "one persistent prefab GUID/fileID");
        Reject(fixture => fixture.WriteEntry(Entry(fileId: "9223372036854775808")), "one persistent prefab GUID/fileID");
        Reject(fixture => fixture.WriteEntry(Entry(guid: "not-a-persistent-guid")), "one persistent prefab GUID/fileID");
    }

    private static void CorruptMetadata()
    {
        Reject(fixture => { fixture.Row["resource"] = "YQWorldAssetLeaves/" + new string('0', 64); fixture.WriteManifest(); }, "path/resource/hash");
        Reject(fixture => { fixture.Row["leafSha256"] = new string('0', 64); fixture.WriteManifest(); }, "Modified/stale runtime leaf");
        Reject(fixture => { fixture.Row["entrySha256"] = new string('0', 64); fixture.WriteManifest(); }, "entry hash mismatch");
        Reject(fixture => fixture.WriteEntry(Entry(path: "Assets/Different.prefab")), "exactly its canonical binding");
        Reject(fixture => fixture.WriteEntry(Entry() + Entry()), "exactly its canonical binding");
        string reference = "    prefab: {fileID: 5846611850104292756, guid: " + PrefabGuid + ", type: 3}\n";
        Reject(fixture => fixture.WriteEntry(Entry().Replace(reference, reference + reference)), "one persistent prefab GUID/fileID");
        Reject(fixture => fixture.WriteEntry(Entry().Replace(reference, reference + "    prefab: {fileID: 0}\n")), "one persistent prefab GUID/fileID");
        Reject(fixture => fixture.WriteEntry(Entry().Replace(reference, reference + "    prefab : malformed\n")), "one persistent prefab GUID/fileID");
        Reject(fixture => { fixture.Manifest["version"] = 99; fixture.WriteManifest(); }, "owner/version");
        Reject(fixture => { fixture.Manifest["generator"] = "unowned"; fixture.WriteManifest(); }, "owner/version");
        Reject(fixture => { fixture.Rows.Add(fixture.Row.DeepClone()); fixture.WriteManifest(); }, "Duplicate runtime prefab binding");
        foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JObject(), new JObject { ["path"] = 42 }, new JObject { ["path"] = " " } })
            Reject(fixture => { fixture.Rows.Add(invalid.DeepClone()); fixture.WriteManifest(); }, "invalid asset row/path");
    }

    private static void CompletePreflight()
    {
        using (var fixture = new Fixture())
        {
            fixture.Rows.Add(new JObject
            {
                ["path"] = "Assets/ZZZMissing.prefab", ["resource"] = "YQWorldAssetLeaves/" + Hash("assets/zzzmissing.prefab"),
                ["entrySha256"] = new string('1', 64), ["leafSha256"] = new string('2', 64)
            });
            fixture.WriteManifest();
            Throws(() => YQRuntimePrefabDependencyBuilder.Rebuild(), "Missing runtime leaf");
            fixture.AssertNoPublication();
            Check(AssetDatabase.DependencyReads.Count == 0, "Preparation discovered dependencies before every source/leaf passed preflight.");
        }
        foreach (string state in new[] { "playing", "compiling", "updating" })
        using (var fixture = new Fixture())
        {
            EditorApplication.isPlayingOrWillChangePlaymode = state == "playing";
            EditorApplication.isCompiling = state == "compiling";
            EditorApplication.isUpdating = state == "updating";
            Throws(() => YQRuntimePrefabDependencyBuilder.Rebuild(), "stable Edit mode");
            fixture.AssertNoPublication();
        }
    }

    private static void ChangesDuringDiscovery()
    {
        // note: Each mutation occurs after preflight, from the fake dependency API, and must be rejected before any generated output is written.
        DiscoveryReject(fixture => File.AppendAllText(fixture.LeafPath, "# changed during discovery\n"), "Modified/stale runtime leaf");
        DiscoveryReject(fixture => fixture.MoveSource("Assets/AnotherLocation/moved-again.prefab", "prefab-dependencies-v1"), "changed during preparation");
        DiscoveryReject(fixture => AssetDatabase.DependencyHashes[fixture.PhysicalPath] = "prefab-dependencies-v2", "changed during preparation");
        DiscoveryReject(fixture => { fixture.Manifest["changed"] = true; fixture.WriteManifest(); }, "manifest changed during dependency preparation");
    }

    private static void MoveAfterBuild()
    {
        using (var fixture = new Fixture())
        {
            YQRuntimePrefabDependencyBuilder.Rebuild();
            string canonicalBindings = File.ReadAllText(BindingManifest), leaf = File.ReadAllText(fixture.LeafPath);
            // note: The controlled imported hash changes on this move. Real Unity hash/import behavior remains a local Editor verification requirement.
            fixture.MoveSource(MovedPath, "prefab-dependencies-after-move");
            AssertReadOnly(() => Throws(() => YQRuntimePrefabDependencyBuilder.ValidateCurrent(), "Missing/stale prefab dependency row"));
            int assetsBefore = AssetDatabase.Writes.Count(value => value.StartsWith("asset: ", StringComparison.Ordinal));
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertPublishedRow(sourceHash: "prefab-dependencies-after-move");
            AssertReadOnly(YQRuntimePrefabDependencyBuilder.ValidateCurrent);
            Check(AssetDatabase.Writes.Count(value => value.StartsWith("asset: ", StringComparison.Ordinal)) == assetsBefore,
                "A source move rewrote existing correct dependency descriptors.");
            Check(File.ReadAllText(BindingManifest) == canonicalBindings && File.ReadAllText(fixture.LeafPath) == leaf,
                "Rebuilding after a move changed canonical keys or conserved leaf text.");
        }
    }

    private static void ValidationAndBuildGuard()
    {
        foreach (string corruption in new[] { "missing-clip", "order", "descriptor-identity", "descriptor-target", "stale-hash" })
        using (var fixture = new Fixture())
        {
            YQRuntimePrefabDependencyBuilder.Rebuild();
            JObject metadata = ReadMetadata();
            var resources = (JArray)metadata["prefabs"][0]["resources"];
            string expectedError;
            if (corruption == "missing-clip" || corruption == "order")
            {
                if (corruption == "missing-clip") resources.RemoveAt(0);
                else metadata["prefabs"][0]["resources"] = new JArray(resources.Reverse().Select(item => item.DeepClone()));
                Put(DependencyManifest, metadata.ToString(Formatting.Indented));
                expectedError = "closure/order differs";
            }
            else if (corruption == "stale-hash")
            {
                AssetDatabase.DependencyHashes[fixture.PhysicalPath] = "changed-imported-source";
                expectedError = "Missing/stale prefab dependency row";
            }
            else
            {
                var descriptor = (YQRuntimePrefabDependency)AssetDatabase.MainAssets[ResourcesRoot + "/" + (string)resources[0] + ".asset"];
                if (corruption == "descriptor-identity") descriptor.sourceIdentity = "wrong-persistent-identity";
                else descriptor.asset = null;
                expectedError = "descriptor identity mismatch";
            }
            AssertReadOnly(() => Throws(() => YQRuntimePrefabDependencyBuilder.ValidateCurrent(), expectedError));
            AssertReadOnly(() => Throws(() => new YQRuntimePrefabDependencyBuildGuard().OnPreprocessBuild(new BuildReport()),
                "Run YourQuest > Assets > Build Prefab Dependency Preloads", typeof(BuildFailedException)));
        }
        using (var fixture = new Fixture())
        {
            AssertReadOnly(() => Throws(() => new YQRuntimePrefabDependencyBuildGuard().OnPreprocessBuild(new BuildReport()),
                "metadata is not current", typeof(BuildFailedException)));
            ((YQRuntimeWorldAssetRegistry)AssetDatabase.MainAssets[RegistryPath]).UsesGranularResourceShards = false;
            AssertReadOnly(() => new YQRuntimePrefabDependencyBuildGuard().OnPreprocessBuild(new BuildReport()));
        }
        using (var fixture = new Fixture(moved: true))
        {
            YQRuntimePrefabDependencyBuilder.Rebuild();
            AssertReadOnly(() => new YQRuntimePrefabDependencyBuildGuard().OnPreprocessBuild(new BuildReport()));
            Check(new YQRuntimePrefabDependencyBuildGuard().callbackOrder == 0, "The build guard callback order changed.");
        }
    }

    private static void Reject(Action<Fixture> mutation, string message)
    {
        using (var fixture = new Fixture(moved: true))
        {
            mutation(fixture);
            Throws(() => YQRuntimePrefabDependencyBuilder.Rebuild(), message);
            fixture.AssertNoPublication();
        }
    }

    private static void DiscoveryReject(Action<Fixture> mutation, string message)
    {
        using (var fixture = new Fixture(moved: true))
        {
            bool mutated = false;
            AssetDatabase.DuringDependencyRead = path =>
            {
                if (mutated) return;
                mutated = true;
                mutation(fixture);
            };
            Throws(() => YQRuntimePrefabDependencyBuilder.Rebuild(), message);
            Check(mutated, "The discovery mutation did not execute.");
            fixture.AssertNoPublication();
        }
    }

    private static JObject ReadMetadata() => JObject.Parse(File.ReadAllText(DependencyManifest));

    private static void AssertPublishedRow(string entryHash = ExactEntryHash, string sourceHash = "prefab-dependencies-v1")
    {
        var metadata = ReadMetadata();
        var rows = (JArray)metadata["prefabs"];
        Check((int)metadata["version"] == 1 && (string)metadata["generator"] == "YourQuest.PrefabDependencyPreloads.v1", "Dependency schema/owner changed.");
        Check(rows.Count == 1 && (string)rows[0]["path"] == LogicalPath, "Publication changed the conserved runtime key.");
        Check((string)rows[0]["entrySha256"] == entryHash, "Publication changed the conserved entry hash.");
        Check((string)rows[0]["sourceDependencyHash"] == sourceHash, "Publication did not hash the resolved imported source.");
        Check(((JArray)rows[0]["resources"]).Select(value => (string)value).SequenceEqual(ExpectedResources),
            "Publication omitted, duplicated, or reordered the exact clip/controller closure.");
    }

    private static void AssertReadOnly(Action action)
    {
        string before = SnapshotFiles();
        int writes = AssetDatabase.Writes.Count, resets = YQRuntimeWorldAssetRegistry.ClearCalls;
        action();
        Check(SnapshotFiles() == before && AssetDatabase.Writes.Count == writes && YQRuntimeWorldAssetRegistry.ClearCalls == resets,
            "Validation/build gating changed fixture files, called a write API, or reset runtime caches.");
    }

    private static string SnapshotFiles() => string.Join("\n", Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => path + ":" + Hash(File.ReadAllText(path))));

    private static void Throws(Action action, string message, Type expectedType = null)
    {
        try { action(); }
        catch (Exception error)
        {
            Check((expectedType ?? typeof(InvalidOperationException)).IsInstanceOfType(error), "Unexpected failure type: " + error);
            Check(error.Message.Contains(message, StringComparison.Ordinal), "Expected diagnostic '" + message + "', got: " + error.Message);
            return;
        }
        throw new InvalidOperationException("Expected rejection containing '" + message + "'.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Put(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static string Hash(string value)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static int Main(string[] args)
    {
        var cases = new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            ["ordinary"] = OrdinaryBinding, ["moved"] = MovedBinding, ["formats"] = ConservedTextFormats,
            ["missing"] = MissingBindings, ["identity"] = ExactIdentity, ["corrupt"] = CorruptMetadata,
            ["preflight"] = CompletePreflight, ["discovery"] = ChangesDuringDiscovery,
            ["source_move"] = MoveAfterBuild, ["validation"] = ValidationAndBuildGuard
        };
        if (args.Length > 1 || (args.Length == 1 && !cases.ContainsKey(args[0])))
        {
            Console.Error.WriteLine("Expected an optional case name: " + string.Join(", ", cases.Keys));
            return 2;
        }
        int passed = 0;
        foreach (var test in cases.Where(pair => args.Length == 0 || pair.Key == args[0]))
        {
            try { test.Value(); passed++; Console.WriteLine("PASS prefab dependency builder: " + test.Key); }
            catch (Exception error) { Console.Error.WriteLine("FAIL prefab dependency builder: " + test.Key + "\n" + error); return 1; }
        }
        Console.WriteLine("PASS " + passed + " managed builder groups. Actual production C#; controlled Editor API contracts, not Unity import/native verification.");
        return 0;
    }
}
