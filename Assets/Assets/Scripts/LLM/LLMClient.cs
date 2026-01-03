// Assets/Assets/Scripts/LLM/LLMClient.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class OllamaRequest
{
    public string model;
    public string prompt;
    public bool stream = false;

    // Optional Ollama options payload (num_predict, temperature, etc.)
    public Dictionary<string, object> options;
}

public class LLMClient : MonoBehaviour
{
    public static LLMClient Instance { get; private set; }

    [Header("Ollama Settings")]
    public string model = "mistral:7b-instruct-q4_K_M";
    public string apiUrl = "http://127.0.0.1:11434";

    [Header("Generation Options")]
    [Tooltip("Max tokens to predict (Ollama: num_predict).")]
    public int numPredict = 300;

    [Header("Debug")]
    public bool logRequestJson = true;
    public bool logRawModelText = true;

    /// <summary>
    /// True while processing the queue (or mid-request).
    /// </summary>
    public bool IsBusy => _processing || _queue.Count > 0;

    private struct QueuedRequest
    {
        public string prompt;
        public Action<string> onResponse;
        public string debugTag;
    }

    private readonly Queue<QueuedRequest> _queue = new Queue<QueuedRequest>();

    // IMPORTANT: must be set BEFORE starting coroutine to prevent double-start in same frame.
    private bool _processing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Backwards-compatible entry point.
    /// (You named it GenerateSkill earlier; keep it so other code doesn't break.)
    /// </summary>
    public void GenerateSkill(string prompt, Action<string> onResponse)
    {
        Enqueue(prompt, onResponse, debugTag: "GenerateSkill");
    }

    /// <summary>
    /// Compatibility wrapper (older systems call SendOnce).
    /// Uses the queue, so it will not fail just because we are busy.
    /// </summary>
    public void SendOnce(string prompt, Action<string> onResponse, string debugTag = null)
    {
        Enqueue(prompt, onResponse, string.IsNullOrWhiteSpace(debugTag) ? "SendOnce" : debugTag);
    }

    /// <summary>
    /// Queue a request instead of rejecting when busy.
    /// </summary>
    public void Enqueue(string prompt, Action<string> onResponse, string debugTag = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SafeInvoke(onResponse, null);
            return;
        }

        _queue.Enqueue(new QueuedRequest
        {
            prompt = prompt,
            onResponse = onResponse,
            debugTag = debugTag
        });

        // Critical fix: set flag BEFORE starting coroutine to avoid double-start race.
        if (!_processing)
        {
            _processing = true;
            StartCoroutine(ProcessQueueCoroutine());
        }
    }

    private IEnumerator ProcessQueueCoroutine()
    {
        while (_queue.Count > 0)
        {
            var req = _queue.Dequeue();
            yield return SendOnceCoroutine(req.prompt, req.onResponse, req.debugTag);
        }

        _processing = false;
    }

    private IEnumerator SendOnceCoroutine(string prompt, Action<string> onResponse, string debugTag)
    {
        string url = apiUrl.TrimEnd('/') + "/api/generate";

        var payload = new OllamaRequest
        {
            model = model,
            prompt = prompt,
            stream = false,
            options = new Dictionary<string, object>
            {
                { "num_predict", numPredict }
            }
        };

        string json = JsonConvert.SerializeObject(payload);

        if (logRequestJson)
            Debug.Log($"[LLMClient] Request JSON{(string.IsNullOrWhiteSpace(debugTag) ? "" : $" ({debugTag})")}:\n{json}");

        using var www = new UnityWebRequest(url, "POST");
        byte[] body = Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(body);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[LLMClient] Request failed: {www.error}\n{www.downloadHandler?.text}");
            SafeInvoke(onResponse, null);
            yield break;
        }

        string raw = www.downloadHandler.text;

        // Ollama returns JSON like: { "response": "...", ... }
        // Some builds may return just plain text depending on endpoint;
        // handle both safely.
        string modelText = ExtractOllamaResponseText(raw);

        if (logRawModelText)
            Debug.Log("[LLMClient] Raw model text:\n" + (modelText ?? "<null>"));

        SafeInvoke(onResponse, modelText);
    }

    private void SafeInvoke(Action<string> cb, string value)
    {
        if (cb == null) return;

        try { cb.Invoke(value); }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLMClient] onResponse callback threw:\n" + ex);
        }
    }

    private string ExtractOllamaResponseText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        try
        {
            // Typical Ollama response: { "model": "...", "response": "TEXT", "done": true, ... }
            var jo = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
            if (jo != null && jo.TryGetValue("response", out var respObj))
                return respObj?.ToString();
        }
        catch
        {
            // Not JSON -> treat as already text
        }

        return raw;
    }
}
