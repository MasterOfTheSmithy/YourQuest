using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[Serializable]
public class OllamaRequest
{
    public string model;
    public string prompt;
    public bool stream = false;
}

[Serializable]
public class OllamaResponse
{
    public string response;
    public bool done;
}

public class LLMClient : MonoBehaviour
{
    public static LLMClient Instance { get; private set; }

    [Header("Ollama Settings")]
    public string model = "mistral:7b-instruct-q4_K_M";
    public string apiUrl = "http://127.0.0.1:11434";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void GenerateSkill(string prompt, Action<string> onResponse)
    {
        StartCoroutine(SendCoroutine(prompt, onResponse));
    }

    private IEnumerator SendCoroutine(string prompt, Action<string> onResponse)
    {
        var requestObj = new OllamaRequest
        {
            model = string.IsNullOrEmpty(model) ? "mistral:7b-instruct-q4_K_M" : model.Trim(),
            prompt = prompt,
            stream = false
        };

        string json = JsonConvert.SerializeObject(requestObj);
        Debug.Log("[LLMClient] Sending:\n" + json);

        using var request = new UnityWebRequest($"{apiUrl}/api/generate", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[LLMClient] Request failed:\n" + request.error);
            onResponse?.Invoke(null);
            yield break;
        }

        try
        {
            // ?? THIS IS THE FIX
            var ollama = JsonConvert.DeserializeObject<OllamaResponse>(
                request.downloadHandler.text
            );

            if (ollama == null || string.IsNullOrEmpty(ollama.response))
            {
                Debug.LogError("[LLMClient] Ollama response empty");
                onResponse?.Invoke(null);
                yield break;
            }

            Debug.Log("[LLMClient] Raw LLM Skill JSON:\n" + ollama.response);
            onResponse?.Invoke(ollama.response);
        }
        catch (Exception e)
        {
            Debug.LogError("[LLMClient] Failed to parse Ollama wrapper:\n" + e);
            onResponse?.Invoke(null);
        }
    }
}
