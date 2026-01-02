using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public static class JsonFileStore
{
    // One place to control serialization behavior for the whole project.
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,

        // Prevent Unity structs/classes with recursive properties from nuking saves.
        ReferenceLoopHandling = ReferenceLoopHandling.Error,

        // Converters to serialize Unity types as plain data.
        Converters =
        {
            new Vector3JsonConverter(),
            new Vector2JsonConverter(),
            new QuaternionJsonConverter()
        }
    };

    public static bool TryLoad<T>(string path, out T data) where T : class
    {
        data = null;

        try
        {
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path);
            data = JsonConvert.DeserializeObject<T>(json, Settings);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[JsonFileStore] TryLoad failed: {path}\n{ex.Message}");
            data = null;
            return false;
        }
    }

    public static bool TrySave<T>(string path, T data) where T : class
    {
        try
        {
            if (data == null) return false;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Settings);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[JsonFileStore] TrySave failed: {path}\n{ex.Message}");
            return false;
        }
    }
}
