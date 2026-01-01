using System.IO;
using UnityEngine;

public static class JsonFileStore
{
    public static bool TryLoad<T>(string path, out T data) where T : class
    {
        data = null;

        try
        {
            if (!File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<T>(json);
            return data != null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[JsonFileStore] Load failed: {path}\n{e}");
            return false;
        }
    }

    public static bool TrySave<T>(string path, T data) where T : class
    {
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[JsonFileStore] Save failed: {path}\n{e}");
            return false;
        }
    }
}
