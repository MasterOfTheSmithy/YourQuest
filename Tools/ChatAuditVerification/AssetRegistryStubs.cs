// Portable test boundary only. These simulate Resources requests and object references, not Unity native loading or lifecycle timing.
using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace UnityEngine
{
    public class Object { public string name = string.Empty; }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            var value = new T();
            typeof(T).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(value, null);
            return value;
        }
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType value) { } }
    public class Component : Object { public T[] GetComponents<T>() => Array.Empty<T>(); }
    public class MonoBehaviour : Component { }
    public class Transform : Component { public Transform Find(string path) => null; }
    public class GameObject : Object
    {
        public Transform transform = new Transform();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.Empty<T>();
    }
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public class Material : Object { public Texture mainTexture; public Texture GetTexture(string key) => null; }
    public class Renderer : Component { public Material sharedMaterial; public Material[] sharedMaterials; }
    public struct LOD { public Renderer[] renderers; public float screenRelativeTransitionHeight; }
    public class LODGroup : Component { public LOD[] GetLODs() => Array.Empty<LOD>(); public void SetLODs(LOD[] values) { } }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Abs(float value) => Math.Abs(value);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
    public static class Time { public static float realtimeSinceStartup; }
    public class TextAsset : Object { public string text; public TextAsset(string value) { text = value; } }
    public static class JsonUtility { public static T FromJson<T>(string value) => JsonConvert.DeserializeObject<T>(value); }
    public class AsyncOperation : Object
    {
        public bool isDone;
        public event Action<AsyncOperation> completed;
        public void DispatchCompleted() { isDone = true; completed?.Invoke(this); }
    }
    public class ResourceRequest : AsyncOperation { public Object asset; public string path; }
    public static class Resources
    {
        public static readonly Dictionary<string, Object> Assets = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
        public static readonly List<string> SyncLoads = new List<string>();
        public static readonly List<ResourceRequest> Requests = new List<ResourceRequest>();
        public static T Load<T>(string path) where T : Object
        {
            SyncLoads.Add(path);
            return Assets.TryGetValue(path, out Object value) ? value as T : null;
        }
        public static T[] LoadAll<T>(string path) where T : Object => Array.Empty<T>();
        public static ResourceRequest LoadAsync<T>(string path) where T : Object
        {
            var request = new ResourceRequest { path = path };
            Requests.Add(request);
            return request;
        }
        public static void Complete(ResourceRequest request, bool dispatch = true)
        {
            Assets.TryGetValue(request.path, out Object value);
            request.asset = value;
            request.isDone = true;
            if (dispatch) request.DispatchCompleted();
        }
        public static void Reset() { Assets.Clear(); SyncLoads.Clear(); Requests.Clear(); Debug.Messages.Clear(); Time.realtimeSinceStartup = 0; }
    }
    public static class Debug
    {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(string text) => Messages.Add(text);
        public static void LogWarning(string text, Object context = null) => Messages.Add("warning:" + text);
        public static void LogError(string text) => Messages.Add("error:" + text);
    }
}

public static class YQRuntimeUrpMaterialRepair
{
    public static void RepairMaterialHierarchy(UnityEngine.GameObject instance) { }
    public static bool IsMaterialSlotRequired(UnityEngine.Renderer renderer, int slot) => true;
    public static bool IsRuntimeMaterialUsable(UnityEngine.Material material) => material != null;
}

public static class YQLlmResidencyPolicy
{
    // The production policy remains separately owned; the fixture exercises the registry at an unquestionably cold timestamp.
    public static bool CanRetireGameCache(float now, float used, float keepWarm) => now - used >= keepWarm;
}
