#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Read an already-selected local Frame Debugger draw; this diagnostic never enables, selects, pauses, renders or alters its display state.
public static class YQSelectedFrameDrawNativeCapture
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const int ArrayLimit = 64, StringLimit = 8192;

    [MenuItem("YourQuest/Terrain Refactor/Capture Selected Frame Draw (Read Only)")]
    public static void Capture()
    {
        var profiles = YQProfileSaveSystem.Instance;
        string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
        var player = YQInvestorPlayerMotor.ActiveMotor;
        if (!EditorApplication.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            YQDeveloperTestSession.Active || player == null || !player.IsAuthoritative || name == null ||
            !name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal))
            throw new InvalidOperationException("Selected draw capture requires a released named disposable terrain player with no protected lease.");

        Type utility = typeof(Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerUtility");
        Type dataType = typeof(Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerInternal.FrameDebuggerEventData");
        Type debugger = typeof(Camera).Assembly.GetType("UnityEngine.FrameDebugger");
        var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["unityVersion"] = Application.unityVersion,
            ["profile"] = profiles.ActiveProfileId, ["worldSeed"] = WorldStateManager.Instance?.State?.generatedWorldPlan?.worldSeed,
            ["evidence"] = "READ_ONLY_SELECTED_NATIVE_FRAME_EVENT; NOT_PIXEL_ROOT_CAUSE_OR_VISUAL_PASS",
            ["cameraAssociation"] = "UNAVAILABLE_NATIVE_EVENT_HAS_NO_CAMERA_ID; ROOT_MUST_IDENTIFY_PLAYER_CAMERA_EVENT_IN_DEBUGGER",
            ["materialAssociation"] = "UNAVAILABLE_NATIVE_EVENT_HAS_NO_MATERIAL_ID; LIVE_RENDERER_SLOTS_ARE_CANDIDATES_ONLY",
            ["gpuVertexOrPixelEvidence"] = "NOT_CAPTURED", ["renderStateChanged"] = false };
        if (utility == null || dataType == null || debugger == null)
        { receipt["status"] = "UNAVAILABLE_API_TYPES"; Save(receipt); return; }
        MethodInfo getData = utility.GetMethod("GetFrameEventData", Static, null, new[] { typeof(int), dataType }, null);
        MethodInfo getObject = utility.GetMethod("GetFrameEventObject", Static, null, new[] { typeof(int) }, null);
        MethodInfo getName = utility.GetMethod("GetFrameEventInfoName", Static, null, new[] { typeof(int) }, null);
        if (getData == null || getObject == null || getName == null)
        { receipt["status"] = "UNAVAILABLE_API_METHODS"; Save(receipt); return; }
        bool paused = EditorApplication.isPaused;
        bool enabled = ReadBool(debugger, "enabled"), local = CallBool(debugger, "IsLocalEnabled");
        receipt["paused"] = paused; receipt["debuggerEnabled"] = enabled; receipt["debuggerLocal"] = local;
        if (!enabled || !local)
        { receipt["status"] = "UNAVAILABLE_REQUIRES_ALREADY_ENABLED_LOCAL_DEBUGGER"; Save(receipt); return; }
        int count = ReadInt(utility, "count"), limit = ReadInt(utility, "limit"), hash = ReadInt(utility, "eventsHash");
        receipt["eventCount"] = count; receipt["selectedLimit"] = limit; receipt["frameEventsHash"] = hash;
        if (limit <= 0 || limit > count)
        { receipt["status"] = "UNAVAILABLE_SELECTED_EVENT"; Save(receipt); return; }

        // note: Unity's native getter fills only the currently-selected event. A mismatched/stale index must never be promoted into draw evidence.
        int index = limit - 1; object data = Activator.CreateInstance(dataType, true);
        if (!(bool)getData.Invoke(null, new[] { (object)index, data }))
        { receipt["status"] = "UNAVAILABLE_STALE_SELECTED_EVENT_DATA"; Save(receipt); return; }
        receipt["eventIndex"] = index; receipt["eventName"] = Limit(getName.Invoke(null, new object[] { index }) as string);
        var eventObject = getObject.Invoke(null, new object[] { index }) as UnityEngine.Object;
        receipt["eventObject"] = ObjectRecord(eventObject);
        receipt["nativeDrawFields"] = Value(data, 0);
        int componentId = (int)dataType.GetField("m_ComponentInstanceID", Fields).GetValue(data);
        var component = EditorUtility.EntityIdToObject((EntityId)componentId) as Component;
        Renderer renderer = component as Renderer;
        if (renderer == null && eventObject is Renderer eventRenderer) renderer = eventRenderer;
        if (renderer != null)
        {
            var slots = new JArray(); Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < Mathf.Min(materials.Length, 32); i++)
            {
                Material material = materials[i];
                slots.Add(new JObject { ["slot"] = i, ["material"] = ObjectRecord(material),
                    ["shader"] = ObjectRecord(material != null ? material.shader : null) });
            }
            receipt["renderer"] = new JObject { ["object"] = ObjectRecord(renderer), ["hierarchy"] = Hierarchy(renderer.transform),
                ["enabled"] = renderer.enabled, ["active"] = renderer.gameObject.activeInHierarchy, ["liveMaterialCandidates"] = slots,
                ["omittedSlots"] = Mathf.Max(0, materials.Length - 32) };
            if (renderer is BillboardRenderer billboard)
                receipt["nativeBillboardMaterialCandidate"] = ObjectRecord(billboard.billboard != null ? billboard.billboard.material : null);
        }
        Camera camera = Camera.main;
        if (camera != null) receipt["currentMainCameraContextOnly"] = new JObject {
            ["object"] = ObjectRecord(camera), ["hierarchy"] = Hierarchy(camera.transform),
            ["position"] = Value(camera.transform.position, 0), ["forward"] = Value(camera.transform.forward, 0) };
        // note: The operation has no state setters to undo. Discard certification if external UI/context changed during these synchronous reads.
        bool stable = EditorApplication.isPlaying && EditorApplication.isPaused == paused && ReadBool(debugger, "enabled") == enabled &&
            CallBool(debugger, "IsLocalEnabled") == local && ReadInt(utility, "limit") == limit && ReadInt(utility, "eventsHash") == hash &&
            profiles.ActiveProfileId == (string)receipt["profile"] && player.IsAuthoritative;
        receipt["status"] = stable ? "SELECTED_EVENT_CAPTURED_CAMERA_AND_MATERIAL_ID_UNAVAILABLE" : "CONTEXT_CHANGED_UNVERIFIED";
        Save(receipt);
    }

    private static bool ReadBool(Type type, string property) => (bool)(type.GetProperty(property, Static)?.GetValue(null) ??
        throw new MissingMemberException(type.FullName, property));
    private static int ReadInt(Type type, string property) => (int)(type.GetProperty(property, Static)?.GetValue(null) ??
        throw new MissingMemberException(type.FullName, property));
    private static bool CallBool(Type type, string method) => (bool)(type.GetMethod(method, Static)?.Invoke(null, null) ??
        throw new MissingMemberException(type.FullName, method));
    private static string Limit(string text) => text == null ? null : text.Substring(0, Math.Min(text.Length, StringLimit));
    private static JObject ObjectRecord(UnityEngine.Object value) => value == null ? null : new JObject {
        ["instanceId"] = value.GetInstanceID(), ["type"] = value.GetType().FullName, ["name"] = Limit(value.name),
        ["assetPath"] = AssetDatabase.GetAssetPath(value) };
    private static string Hierarchy(Transform value)
    {
        string path = value.name;
        for (int depth = 0; value.parent != null && depth < 16; depth++) { value = value.parent; path = value.name + "/" + path; }
        return Limit(path);
    }
    // note: Bound every native array/string and stop at value-field depth; no GPU readback, native mesh mutation or recursive object graph traversal occurs.
    private static JToken Value(object value, int depth)
    {
        if (value == null) return JValue.CreateNull();
        if (value is UnityEngine.Object asset) return ObjectRecord(asset);
        if (value is string text) return text.Length <= StringLimit ? new JValue(text) :
            new JObject { ["text"] = Limit(text), ["omittedCharacters"] = text.Length - StringLimit };
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum) return type.IsEnum ? new JValue(value.ToString()) : JToken.FromObject(value);
        if (depth > 4) return new JObject { ["unavailable"] = "DEPTH_BOUND", ["type"] = type.FullName };
        if (value is Array array)
        {
            // note: Native shader arrays interleave named property headers with hundreds of unnamed payload rows. Preserve named headers before applying the output cap, without losing raw/omitted counts or scanning unbounded arrays.
            FieldInfo propertyName = type.GetElementType()?.GetField("m_Name", Fields);
            if (propertyName != null && propertyName.FieldType == typeof(string))
                return NamedPropertyArray(array, propertyName, depth);
            var items = new JArray(); for (int i = 0; i < Math.Min(array.Length, ArrayLimit); i++) items.Add(Value(array.GetValue(i), depth + 1));
            return new JObject { ["items"] = items, ["count"] = array.Length, ["omitted"] = Math.Max(0, array.Length - ArrayLimit) };
        }
        var result = new JObject(); FieldInfo[] fields = type.GetFields(Fields);
        for (int i = 0; i < Math.Min(fields.Length, 96); i++) result[fields[i].Name] = Value(fields[i].GetValue(value), depth + 1);
        if (fields.Length > 96) result["omittedFields"] = fields.Length - 96;
        return result;
    }
    private static JObject NamedPropertyArray(Array array, FieldInfo nameField, int depth)
    {
        const int MaximumInspectedProperties = 4096;
        int inspected = Math.Min(array.Length, MaximumInspectedProperties), unnamed = 0, named = 0;
        var items = new JArray(); var nativeIndices = new JArray();
        for (int index = 0; index < inspected; index++)
        {
            object entry = array.GetValue(index);
            string name = entry != null ? nameField.GetValue(entry) as string : null;
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "<noninit>", StringComparison.Ordinal))
            { unnamed++; continue; }
            named++;
            if (items.Count >= ArrayLimit) continue;
            items.Add(Value(entry, depth + 1)); nativeIndices.Add(index);
        }
        // note: Unnamed native payload is omitted deliberately, not interpreted as zero or associated with a neighbouring material/light. Retained source indices keep that limitation explicit.
        return new JObject { ["items"] = items, ["nativeIndices"] = nativeIndices, ["count"] = array.Length,
            ["omitted"] = array.Length - items.Count, ["selection"] = "NAMED_PROPERTY_HEADERS_ONLY; UNNAMED_ARRAY_PAYLOAD_NOT_CAPTURED",
            ["inspected"] = inspected, ["namedInspected"] = named, ["omittedNamed"] = named - items.Count,
            ["omittedUnnamed"] = unnamed, ["uninspected"] = array.Length - inspected, ["namedLimit"] = ArrayLimit };
    }

    private static void Save(JObject receipt)
    {
        const string folder = "outputs/TerrainRefactor_20261014_StreamingRetest/TreeMaterials/FrameDraw/native";
        Directory.CreateDirectory(folder);
        string path = folder + "/selected-draw-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream)) writer.Write(receipt.ToString());
        Debug.Log("[YQSelectedFrameDraw] " + (string)receipt["status"] + " receipt=" + path);
    }
}
#endif
