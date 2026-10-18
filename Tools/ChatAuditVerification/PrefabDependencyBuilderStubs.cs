// Controlled Editor API boundary for the complete production dependency builder.
// These fixtures do not simulate Unity imports, native asset loading, or runtime performance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityEngine
{
    public class Object { public string name = string.Empty; }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
    public class GameObject : Object { }
    public class AnimationClip : Object { }
    public class RuntimeAnimatorController : Object { }
    public class Material : Object { }
    public struct Hash128
    {
        private readonly string value;
        public Hash128(string value) { this.value = value; }
        public override string ToString() => value;
    }
    public static class Application { public static string dataPath => Path.GetFullPath("Assets"); }
    public static class Debug
    {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(string message) => Messages.Add(message);
        public static void LogWarning(string message) => Messages.Add("warning: " + message);
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute { public MenuItem(string path) { } }
    public enum AssetPathToGUIDOptions { IncludeRecentlyDeletedAssets = 0, OnlyExistingAssets = 1 }
    public enum ImportAssetOptions { Default = 0, ForceSynchronousImport = 8 }
    public static class EditorApplication
    {
        public static bool isPlayingOrWillChangePlaymode, isCompiling, isUpdating;
    }
    public class AssetImporter
    {
        public static readonly Dictionary<string, AssetImporter> Importers = new Dictionary<string, AssetImporter>(StringComparer.Ordinal);
        public static AssetImporter GetAtPath(string path) => Importers.TryGetValue(path, out var importer) ? importer : null;
    }
    public class ModelImporter : AssetImporter { public bool importAnimation; }

    public static class AssetDatabase
    {
        public static readonly Dictionary<string, string> GuidPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Dictionary<string, string> PathGuids = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<(string path, long fileId), Type> ObjectTypes = new Dictionary<(string, long), Type>();
        public static readonly Dictionary<string, Type> MainTypes = new Dictionary<string, Type>(StringComparer.Ordinal);
        public static readonly Dictionary<string, UnityEngine.Object[]> ImportedAssets = new Dictionary<string, UnityEngine.Object[]>(StringComparer.Ordinal);
        public static readonly Dictionary<string, UnityEngine.Object> MainAssets = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        public static readonly Dictionary<UnityEngine.Object, (string guid, long fileId)> Identities = new Dictionary<UnityEngine.Object, (string, long)>();
        public static readonly Dictionary<UnityEngine.Object, string> ObjectPaths = new Dictionary<UnityEngine.Object, string>();
        public static readonly Dictionary<string, string[]> Dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> DependencyHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly List<string> DependencyReads = new List<string>();
        public static readonly List<string> HashReads = new List<string>();
        public static readonly List<(string path, long fileId)> TypeReads = new List<(string, long)>();
        public static readonly List<(string path, AssetPathToGUIDOptions options)> GuidReads = new List<(string, AssetPathToGUIDOptions)>();
        public static readonly List<string> ObjectLoads = new List<string>();
        public static readonly List<string> Writes = new List<string>();
        public static Action<string> DuringDependencyRead;

        // note: Registration describes the fake import database; fixture source files are created separately in an isolated temporary project.
        public static void Register(string path, string guid, long fileId, UnityEngine.Object asset)
        {
            GuidPaths[guid] = path;
            PathGuids[path] = guid;
            ObjectTypes[(path, fileId)] = asset.GetType();
            Identities[asset] = (guid, fileId);
            ObjectPaths[asset] = path;
            if (!MainAssets.ContainsKey(path))
            {
                MainAssets[path] = asset;
                MainTypes[path] = asset.GetType();
            }
            var contents = ImportedAssets.TryGetValue(path, out var existing) ? existing.ToList() : new List<UnityEngine.Object>();
            contents.Add(asset);
            ImportedAssets[path] = contents.ToArray();
        }

        public static string GUIDToAssetPath(string guid) => GuidPaths.TryGetValue(guid, out string path) ? path : string.Empty;
        public static string AssetPathToGUID(string path, AssetPathToGUIDOptions options = AssetPathToGUIDOptions.IncludeRecentlyDeletedAssets)
        {
            GuidReads.Add((path, options));
            if (options == AssetPathToGUIDOptions.OnlyExistingAssets && !File.Exists(path)) return string.Empty;
            return PathGuids.TryGetValue(path, out string guid) ? guid : string.Empty;
        }
        public static Type GetTypeFromPathAndFileID(string path, long fileId)
        {
            TypeReads.Add((path, fileId));
            return ObjectTypes.TryGetValue((path, fileId), out Type type) ? type : null;
        }
        public static Type GetMainAssetTypeAtPath(string path) => MainTypes.TryGetValue(path, out Type type) ? type : null;
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path)
        {
            ObjectLoads.Add(path);
            return ImportedAssets.TryGetValue(path, out var assets) ? assets : Array.Empty<UnityEngine.Object>();
        }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
        {
            ObjectLoads.Add(path);
            return MainAssets.TryGetValue(path, out var asset) ? asset as T : null;
        }
        public static bool TryGetGUIDAndLocalFileIdentifier(UnityEngine.Object asset, out string guid, out long localId)
        {
            if (asset != null && Identities.TryGetValue(asset, out var identity))
            { guid = identity.guid; localId = identity.fileId; return true; }
            guid = string.Empty; localId = 0; return false;
        }
        public static string GetAssetPath(UnityEngine.Object asset) => ObjectPaths.TryGetValue(asset, out string path) ? path : string.Empty;
        public static string[] GetDependencies(string path, bool recursive)
        {
            if (!recursive) throw new InvalidOperationException("The fixture requires transitive dependency discovery.");
            DependencyReads.Add(path);
            var dependencies = Dependencies.TryGetValue(path, out var declared) ? declared.ToArray() : new[] { path };
            DuringDependencyRead?.Invoke(path);
            return dependencies;
        }
        public static UnityEngine.Hash128 GetAssetDependencyHash(string path)
        {
            HashReads.Add(path);
            return new UnityEngine.Hash128(DependencyHashes.TryGetValue(path, out string hash) ? hash : "missing-dependency-hash");
        }
        public static bool IsValidFolder(string path) => Directory.Exists(path);
        public static string CreateFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            Writes.Add("folder: " + path);
            Directory.CreateDirectory(path);
            return path;
        }
        public static void CreateAsset(UnityEngine.Object asset, string path)
        {
            Writes.Add("asset: " + path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "controlled descriptor asset\n");
            MainAssets[path] = asset;
            MainTypes[path] = asset.GetType();
            ObjectPaths[asset] = path;
        }
        public static void ImportAsset(string path, ImportAssetOptions options) => Writes.Add("import: " + path);

        public static void Reset()
        {
            GuidPaths.Clear(); PathGuids.Clear(); ObjectTypes.Clear(); MainTypes.Clear(); ImportedAssets.Clear(); MainAssets.Clear();
            Identities.Clear(); ObjectPaths.Clear(); Dependencies.Clear(); DependencyHashes.Clear(); DependencyReads.Clear(); HashReads.Clear();
            TypeReads.Clear(); GuidReads.Clear(); ObjectLoads.Clear(); Writes.Clear(); DuringDependencyRead = null;
            AssetImporter.Importers.Clear(); UnityEngine.Debug.Messages.Clear();
            EditorApplication.isPlayingOrWillChangePlaymode = EditorApplication.isCompiling = EditorApplication.isUpdating = false;
            YQRuntimeWorldAssetRegistry.ClearCalls = 0;
        }
    }
}

namespace UnityEditor.Build
{
    public interface IOrderedCallback { int callbackOrder { get; } }
    public interface IPreprocessBuildWithReport : IOrderedCallback { void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report); }
    public class BuildFailedException : Exception { public BuildFailedException(string message) : base(message) { } }
}
namespace UnityEditor.Build.Reporting { public sealed class BuildReport { } }

public sealed class YQRuntimeWorldAssetRegistry : UnityEngine.ScriptableObject
{
    public bool UsesGranularResourceShards = true;
    public static int ClearCalls;
    public static void ClearCachedInstance() { ClearCalls++; }
    public static string NormalizePath(string path)
    {
        string value = (path ?? string.Empty).Trim().Replace('\\', '/');
        while (value.Contains("//")) value = value.Replace("//", "/");
        return value;
    }
}
