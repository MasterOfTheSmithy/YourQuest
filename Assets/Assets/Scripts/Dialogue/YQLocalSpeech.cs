using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public sealed class YQLocalSpeech : MonoBehaviour
{
    public static YQLocalSpeech Instance { get; private set; }
    private void Awake()
    {
        // note: Retrieval shares the existing owned voice worker, including its cleanup and CPU budget.
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    [Serializable] private sealed class MemoryRequest { public string query; public string[] candidates; public int limit = 4; }
    [Serializable] public sealed class MemoryResult { public int[] indices; public float[] scores; }
    private UnityWebRequest _memoryRequest;
    private bool _retrieving;
    private int _memorySequence;
    [Serializable] private sealed class Handshake { public int protocol, port, pid; }
    [Serializable] private sealed class Health { public bool busy; }
    [Serializable] private sealed class StartRequest { public string session; public string[] terms; }
    [Serializable] private sealed class ChunkRequest { public string session, pcm; public int rate; }
    [Serializable] private sealed class FinishRequest { public string session; }
    [Serializable] private sealed class SpeakRequest { public string text, voice; public float speed = 1f; }
    [Serializable] private sealed class Transcript { public string text; public bool complete; }
    [Serializable] private sealed class WorkerError { public string error; }
    private sealed class PcmDownload : DownloadHandlerScript
    {
        private readonly YQSpeechPcmBuffer _buffer;
        public PcmDownload(YQSpeechPcmBuffer buffer) : base(new byte[32768]) { _buffer = buffer; }
        protected override bool ReceiveData(byte[] data, int length) => _buffer.Receive(data, length);
    }

    private Process _launcher, _worker;
    private readonly ConcurrentQueue<string> _handshakes = new ConcurrentQueue<string>();
    private Coroutine _prepareRoutine, _captureRoutine, _speakRoutine;
    private UnityWebRequest _request;
    private readonly HashSet<UnityWebRequest> _controlRequests = new HashSet<UnityWebRequest>();
    private AudioSource _audio;
    private AudioClip _playback, _microphone;
    private string _token, _url;
    private string _lastWorkerDiagnostic;
    private int _sequence, _controls;
    private bool _prepared, _stopCapture;
    public bool IsCapturing { get; private set; }
    public bool IsTranscribing { get; private set; }
    public bool IsReady => _prepared && WorkerAlive();
    public bool IsSpeaking => _speakRoutine != null;
    public string Status { get; private set; } = "Voice not prepared";
    // note: One immutable final identity reaches the hearing boundary; partial transcripts never receive a delivery identity.
    public string LastFinalTranscriptId { get; private set; }
    public event Action<string> StatusChanged;

    public void Prepare()
    {
        if (!IsReady && _prepareRoutine == null) _prepareRoutine = StartCoroutine(PrepareWorker());
    }

    private IEnumerator PrepareWorker()
    {
        SetStatus("Preparing voice… You can still type.");
        try
        {
            bool connected = false;
            yield return ConnectWorker(value => connected = value);
            if (!connected) { SetStatus("Voice unavailable. Use typing."); yield break; }
            bool idle = false;
            yield return WaitForIdle(value => idle = value);
            if (!idle) yield break;
            string response = null;
            yield return Post("/prepare", "{}", value => response = value, 65);
            _prepared = response != null;
            if (_prepared) SetStatus("Voice ready • F2 to mute");
        }
        finally { _prepareRoutine = null; }
    }

    private IEnumerator ConnectWorker(Action<bool> connected)
    {
        // note: Memory can start the same worker without loading voice models or opening a microphone.
        if (!StartWorker()) { connected(false); yield break; }
        float deadline = Time.realtimeSinceStartup + 10f;
        while (_url == null && Time.realtimeSinceStartup < deadline && WorkerAlive())
        {
            if (_handshakes.TryDequeue(out string line))
            {
                Handshake h = Parse<Handshake>(line);
                if (h != null && h.protocol == 1 && h.port > 0 && h.port <= 65535 && h.pid > 0)
                {
                    _url = "http://127.0.0.1:" + h.port;
                    // note: The venv launcher may have a child interpreter; retain the exact owned worker handle for cleanup.
                    _worker = Process.GetProcessById(h.pid);
                    IntPtr ownedHandle = _worker.Handle;
                }
            }
            yield return null;
        }
        if (_url == null)
        {
            Debug.LogWarning("Local voice worker did not become ready: " + _lastWorkerDiagnostic);
            ShutdownWorker(); connected(false); yield break;
        }
        connected(true);
    }

    private bool StartWorker()
    {
        if (WorkerAlive()) return true;
        ShutdownWorker();
        string root = Path.GetDirectoryName(Application.dataPath);
        string bundle = Application.isEditor ? root : Path.Combine(Application.streamingAssetsPath, "LocalSpeech");
        string python = Environment.GetEnvironmentVariable("YOURQUEST_SPEECH_PYTHON") ??
                        Path.Combine(bundle, Application.isEditor ? "outputs/InitialModelStack/venv/Scripts/python.exe" : "venv/Scripts/python.exe");
        string script = Environment.GetEnvironmentVariable("YOURQUEST_SPEECH_WORKER") ??
                        Path.Combine(bundle, Application.isEditor ? "Tools/LocalSpeech/server.py" : "server.py");
        string cache = Environment.GetEnvironmentVariable("YOURQUEST_SPEECH_CACHE") ??
                       Path.Combine(bundle, Application.isEditor ? "outputs/InitialModelStack/cache" : "cache");
        if (!File.Exists(python) || !File.Exists(script))
        {
            SetStatus("Voice unavailable. Use typing.");
            return false;
        }
        try
        {
            _token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var start = new ProcessStartInfo(python, "-u \"" + script + "\" --cache \"" + cache + "\" --parent-pid " + Process.GetCurrentProcess().Id)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(script)
            };
            start.EnvironmentVariables["YOURQUEST_SPEECH_TOKEN"] = _token;
            _launcher = new Process { StartInfo = start };
            _launcher.OutputDataReceived += (_, args) => { if (args.Data != null && args.Data.StartsWith("{", StringComparison.Ordinal)) _handshakes.Enqueue(args.Data); };
            _launcher.ErrorDataReceived += (_, args) =>
            {
                // note: Drain SDK diagnostics and retain the last bounded error without calling Unity from a process thread.
                if (args.Data != null) _lastWorkerDiagnostic = args.Data.Length > 240 ? args.Data.Substring(0, 240) : args.Data;
            };
            _launcher.Start();
            _launcher.BeginOutputReadLine();
            _launcher.BeginErrorReadLine();
            return true;
        }
        catch (Exception e) { Debug.LogWarning("Local voice could not start: " + e.Message); SetStatus("Voice unavailable. Use typing."); ShutdownWorker(); return false; }
    }

    public IEnumerator RetrieveMemories(string query, string[] candidates, Action<MemoryResult> result, Func<bool> stillCurrent)
    {
        // note: Memory is optional context; unavailable/busy inference preserves ordinary recent-dialogue generation.
        if (_retrieving || !isActiveAndEnabled) { result(null); yield break; }
        _retrieving = true;
        int memorySequence = ++_memorySequence;
        try
        {
            bool connected = false;
            yield return ConnectWorker(value => connected = value);
            if (memorySequence != _memorySequence || (stillCurrent != null && !stillCurrent())) yield break;
            if (!connected || IsCapturing || IsTranscribing || IsSpeaking) { result(null); yield break; }
            using (var request = NewRequest("/retrieve", JsonUtility.ToJson(new MemoryRequest { query = query, candidates = candidates })))
            {
                _memoryRequest = request;
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (ReferenceEquals(_memoryRequest, request)) _memoryRequest = null;
                if (stillCurrent != null && !stillCurrent()) yield break;
                if (memorySequence != _memorySequence) { result(null); yield break; }
                result(request.result == UnityWebRequest.Result.Success ? Parse<MemoryResult>(request.downloadHandler.text) : null);
            }
        }
        finally { if (memorySequence == _memorySequence) { _memoryRequest = null; _retrieving = false; } }
    }

    public bool BeginDictation(Action<string> finalText, IReadOnlyList<string> terms, bool live = true)
    {
        if (!IsReady || IsCapturing || IsTranscribing) return false;
        Cancel();
        _stopCapture = false;
        IsCapturing = true;
        _captureRoutine = StartCoroutine(Capture(_sequence, finalText, terms, live));
        return true;
    }

    public void EndDictation() { _stopCapture = true; }

    private IEnumerator Capture(int sequence, Action<string> finalText, IReadOnlyList<string> terms, bool live)
    {
        string session = Guid.NewGuid().ToString("N");
        bool completed = false;
        try
        {
            bool idle = false;
            yield return WaitForIdle(value => idle = value);
            if (!idle || _stopCapture || sequence != _sequence) yield break;
            var list = new List<string>();
            if (terms != null) for (int i = 0; i < terms.Count && list.Count < 32; i++)
                if (!string.IsNullOrWhiteSpace(terms[i]) && terms[i].Length <= 100) list.Add(terms[i]);
            string response = null;
            yield return Post("/asr/start", JsonUtility.ToJson(new StartRequest { session = session, terms = list.ToArray() }), value => response = value);
            if (response == null || _stopCapture || sequence != _sequence) yield break;
            if (!StartMicrophone()) yield break;
            SetStatus("Listening • Speak naturally • F2 to mute");
            float began = Time.realtimeSinceStartup;
            int read = 0, stoppedAt = -1;
            while (sequence == _sequence)
            {
                int position = stoppedAt >= 0 ? stoppedAt : Microphone.GetPosition(null);
                if (position < 0) { SetStatus("Microphone unavailable. Use typing."); yield break; }
                if ((_stopCapture || Time.realtimeSinceStartup - began >= 30f) && stoppedAt < 0)
                {
                    stoppedAt = position;
                    Microphone.End(null);
                    IsCapturing = false;
                    IsTranscribing = true;
                    SetStatus("Understanding…");
                }
                int available = position - read;
                if (available > 0)
                {
                    int frames = Math.Min(available, _microphone.frequency / 2);
                    float[] interleaved = new float[frames * _microphone.channels];
                    if (!_microphone.GetData(interleaved, read)) { SetStatus("Microphone unavailable. Use typing."); yield break; }
                    // note: Capture remains in Unity; only mono PCM crosses the local authenticated route.
                    float[] mono = new float[frames];
                    for (int i = 0; i < frames; i++)
                    {
                        float sum = 0f;
                        for (int c = 0; c < _microphone.channels; c++) sum += interleaved[i * _microphone.channels + c];
                        mono[i] = sum / _microphone.channels;
                    }
                    byte[] pcm = new byte[frames * 4];
                    Buffer.BlockCopy(mono, 0, pcm, 0, pcm.Length);
                    response = null;
                    yield return Post("/asr/chunk", JsonUtility.ToJson(new ChunkRequest { session = session, rate = _microphone.frequency, pcm = Convert.ToBase64String(pcm) }), value => response = value);
                    if (response == null) yield break;
                    read += frames;
                    Transcript phrase = live ? Parse<Transcript>(response) : null;
                    if (phrase != null && phrase.complete && !string.IsNullOrWhiteSpace(phrase.text) && sequence == _sequence)
                    {
                        // note: VAD completion stops the microphone before handing the phrase to the existing dialogue authority.
                        completed = true;
                        StopMicrophone();
                        IsCapturing = IsTranscribing = false;
                        _captureRoutine = null;
                        StartCoroutine(ControlCancel());
                        SetStatus("Thinking…");
                        LastFinalTranscriptId = session;
                        finalText?.Invoke(phrase.text.Trim());
                        yield break;
                    }
                }
                else if (stoppedAt >= 0) break;
                else if (!Microphone.IsRecording(null) && Time.realtimeSinceStartup - began > 2f)
                { SetStatus("Microphone unavailable. Use typing."); yield break; }
                else yield return new WaitForSecondsRealtime(.1f);
            }
            if (sequence != _sequence) yield break;
            // note: A live capture's duration cap is a restart boundary, never permission to submit unfinished speech.
            if (live) { SetStatus("Listening restarts…"); yield break; }
            response = null;
            yield return Post("/asr/finish", JsonUtility.ToJson(new FinishRequest { session = session }), value => response = value);
            Transcript transcript = response != null ? Parse<Transcript>(response) : null;
            if (sequence != _sequence || transcript == null) yield break;
            completed = true;
            IsCapturing = IsTranscribing = false;
            _captureRoutine = null;
            SetStatus(string.IsNullOrWhiteSpace(transcript.text) ? "Nothing heard" : "Voice ready • F2 to mute");
            if (!string.IsNullOrWhiteSpace(transcript.text)) { LastFinalTranscriptId = session; finalText?.Invoke(transcript.text.Trim()); }
        }
        finally
        {
            StopMicrophone();
            IsCapturing = IsTranscribing = false;
            _captureRoutine = null;
            if (!completed && _url != null) StartCoroutine(ControlCancel());
        }
    }

    private bool StartMicrophone()
    {
        try
        {
            if (Microphone.devices.Length == 0) { SetStatus("No microphone found. Use typing."); return false; }
            _microphone = Microphone.Start(null, false, 31, 16000);
            return _microphone != null;
        }
        catch (Exception e) { Debug.LogWarning("Microphone capture could not start: " + e.Message); SetStatus("Microphone unavailable. Use typing."); return false; }
    }

    public void Speak(string text, string voice, string delivery = "measured", Func<bool> stillCurrent = null)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(text) || (stillCurrent != null && !stillCurrent())) return;
        Cancel();
        // note: Freeze casting and verified pacing before inference. Retiring an owner stops audio without undoing an already heard social event.
        _speakRoutine = StartCoroutine(PlaySpeech(_sequence, text, voice, YQDialogueSocialRules.Speed(delivery), stillCurrent));
    }

    private IEnumerator PlaySpeech(int sequence, string text, string voice, float speed, Func<bool> stillCurrent)
    {
        bool Current() => sequence == _sequence && (stillCurrent == null || stillCurrent());
        try
        {
            bool idle = false;
            yield return WaitForIdle(value => idle = value);
            if (!idle || !Current()) yield break;
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
                _audio.loop = true;
                // note: Dialogue/title modals pause world audio; the owned reply must still play while their text is visible.
                _audio.ignoreListenerPause = true;
            }
            var buffer = new YQSpeechPcmBuffer();
            _playback = AudioClip.Create("NPC voice", YQSpeechPcmBuffer.SampleRate * 2, 1, YQSpeechPcmBuffer.SampleRate, true, buffer.Read);
            _audio.clip = _playback;
            // note: Streaming preserves the accepted line and speaker; there is no pitch shifting or speech-derived mutation.
            using (UnityWebRequest request = NewRequest("/speak", JsonUtility.ToJson(new SpeakRequest { text = text, voice = voice, speed = speed })))
            {
                request.downloadHandler = new PcmDownload(buffer);
                request.timeout = 60;
                _request = request;
                request.SendWebRequest();
                bool playing = false;
                SetStatus("Preparing reply audio…");
                while (!request.isDone && Current())
                {
                    // note: Buffering covers measured chunk jitter rather than starting at the first tiny packet.
                    if (!playing && buffer.BufferedSamples >= YQSpeechPcmBuffer.SampleRate * .75f)
                    { _audio.Play(); playing = true; SetStatus("Speaking… Listening paused"); }
                    yield return null;
                }
                _request = null;
                if (!Current()) yield break;
                if (request.result != UnityWebRequest.Result.Success || !buffer.Complete())
                { SetStatus("Reply audio unavailable. The text is still here."); yield break; }
                if (!playing && buffer.BufferedSamples > 0) _audio.Play();
                while (buffer.BufferedSamples > 0 && Current()) yield return null;
                // note: The streaming callback can prefetch a clip cycle; drain that queued audio before releasing playback.
                float drainUntil = Time.realtimeSinceStartup + _playback.length + .1f;
                while (Time.realtimeSinceStartup < drainUntil && Current()) yield return null;
                if (Current()) SetStatus("Voice ready • F2 to mute");
            }
        }
        finally { StopPlayback(); _speakRoutine = null; }
    }

    private IEnumerator WaitForIdle(Action<bool> result)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (_controls > 0 && Time.realtimeSinceStartup < deadline) yield return null;
        while (Time.realtimeSinceStartup < deadline && WorkerAlive() && _url != null)
        {
            using (var request = UnityWebRequest.Get(_url + "/health"))
            {
                request.SetRequestHeader("Authorization", "Bearer " + _token);
                request.timeout = 3;
                _request = request;
                yield return request.SendWebRequest();
                _request = null;
                Health health = request.result == UnityWebRequest.Result.Success ? Parse<Health>(request.downloadHandler.text) : null;
                if (health != null && !health.busy) { result(true); yield break; }
            }
            yield return new WaitForSecondsRealtime(.05f);
        }
        SetStatus("Voice unavailable. Use typing.");
        result(false);
    }

    private IEnumerator Post(string route, string json, Action<string> response, int timeout = 12)
    {
        using (var request = NewRequest(route, json))
        {
            request.timeout = timeout;
            _request = request;
            yield return request.SendWebRequest();
            _request = null;
            if (request.result == UnityWebRequest.Result.Success) response(request.downloadHandler.text);
            else
            {
                WorkerError error = Parse<WorkerError>(request.downloadHandler.text);
                Debug.LogWarning("Local voice " + route + " failed (" + request.responseCode + "): " + (error?.error ?? request.error));
                SetStatus("Voice unavailable. Use typing.");
            }
        }
    }

    private UnityWebRequest NewRequest(string route, string json)
    {
        var request = new UnityWebRequest(_url + route, "POST")
        { uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)), downloadHandler = new DownloadHandlerBuffer() };
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + _token);
        return request;
    }

    private IEnumerator ControlCancel()
    {
        _controls++;
        var request = NewRequest("/cancel", "{}");
        _controlRequests.Add(request);
        try { request.timeout = 3; yield return request.SendWebRequest(); }
        finally { if (_controlRequests.Remove(request)) request.Dispose(); _controls = Math.Max(0, _controls - 1); }
    }

    private void AbortControls()
    {
        // note: Retired coroutine control requests also own native transports; release them before killing the worker.
        foreach (var request in _controlRequests) { request.Abort(); request.Dispose(); }
        _controlRequests.Clear();
        _controls = 0;
    }

    public void Cancel()
    {
        // note: Retiring a turn synchronously stops capture/playback before any late network callback can run.
        _sequence++;
        _memorySequence++;
        if (_memoryRequest != null) { _memoryRequest.Abort(); _memoryRequest = null; }
        _retrieving = false;
        _stopCapture = true;
        if (_prepareRoutine == null) AbortRequest();
        if (_captureRoutine != null) StopCoroutine(_captureRoutine);
        if (_speakRoutine != null) StopCoroutine(_speakRoutine);
        _captureRoutine = _speakRoutine = null;
        IsCapturing = IsTranscribing = false;
        StopMicrophone();
        StopPlayback();
        if (_url != null && isActiveAndEnabled) StartCoroutine(ControlCancel());
    }

    private void StopMicrophone()
    {
        if (_microphone == null) return;
        Microphone.End(null);
        Destroy(_microphone);
        _microphone = null;
    }
    private void StopPlayback()
    {
        if (_audio != null) { _audio.Stop(); _audio.clip = null; }
        if (_playback != null) Destroy(_playback);
        _playback = null;
    }
    private bool WorkerAlive() { try { return _launcher != null && !_launcher.HasExited; } catch (InvalidOperationException) { return false; } }
    private static T Parse<T>(string json) where T : class { try { return JsonUtility.FromJson<T>(json); } catch (ArgumentException) { return null; } }
    private void SetStatus(string text) { Status = text; StatusChanged?.Invoke(text); }
    private void AbortRequest()
    {
        // note: Coroutine retirement may skip iterator disposal, so release the owned transport explicitly.
        if (_request == null) return;
        _request.Abort();
        _request.Dispose();
        _request = null;
    }
    private void ShutdownWorker()
    {
        _prepared = false;
        _url = null;
        while (_handshakes.TryDequeue(out _)) { }
        // note: Kill only retained handles belonging to this component's interpreter and launcher, never an external server.
        foreach (Process owned in new[] { _worker, _launcher })
        {
            if (owned == null) continue;
            try { if (!owned.HasExited) owned.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception e) { Debug.LogWarning("Owned voice worker cleanup failed: " + e.Message); }
            owned.Dispose();
        }
        _worker = _launcher = null;
    }
    private void OnDisable() { Cancel(); AbortRequest(); StopAllCoroutines(); AbortControls(); _prepareRoutine = null; ShutdownWorker(); }
    private void OnApplicationQuit() { Cancel(); AbortRequest(); StopAllCoroutines(); AbortControls(); ShutdownWorker(); }
}
