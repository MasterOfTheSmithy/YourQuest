using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: This opt-in Editor probe tests native speech without creating NPCs or bypassing world/save validation.
public sealed class YQSpeechProofWindow : EditorWindow
{
    private YQSpeechProofProbe _probe;

    [MenuItem("YourQuest/Voice/Run focused speech proof")]
    private static void Open() => GetWindow<YQSpeechProofWindow>("Local speech proof").Show();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unity microphone and playback proof", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Start in Play Mode. This check uses the real local speech component and default microphone. It does not certify NPC gameplay or game/LLM headroom.", MessageType.Info);
        EditorGUILayout.LabelField("Editor audio muted", EditorUtility.audioMasterMute.ToString());
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _probe != null))
            if (GUILayout.Button("Start speech check"))
            {
                var host = new GameObject("Transient local speech proof") { hideFlags = HideFlags.DontSave };
                _probe = host.AddComponent<YQSpeechProofProbe>();
                _probe.Begin();
            }
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _probe != null))
            if (GUILayout.Button("Play voice only"))
            {
                var host = new GameObject("Transient local speech proof") { hideFlags = HideFlags.DontSave };
                _probe = host.AddComponent<YQSpeechProofProbe>();
                _probe.Begin(false);
            }
        if (_probe == null) return;
        EditorGUILayout.LabelField("Stage", _probe.Stage);
        EditorGUILayout.LabelField("Voice", _probe.Speech != null ? _probe.Speech.Status : "Stopped");
        EditorGUILayout.LabelField("Playback measured", _probe.Result.playbackNonzero.ToString());
        EditorGUILayout.LabelField("Microphone cancel checked", _probe.Result.microphoneCancelPassed.ToString());
        EditorGUILayout.LabelField("Recognized test phrase:");
        EditorGUILayout.TextArea(_probe.Result.transcript ?? "", GUILayout.MinHeight(45));
        if (GUILayout.Button("Stop and save current evidence")) _probe.Finish("Stopped by tester");
    }

    private void OnInspectorUpdate() => Repaint();
    private void OnDisable()
    {
        if (_probe == null) return;
        _probe.Finish("Proof window closed");
        Destroy(_probe.gameObject);
    }
}

public sealed class YQSpeechProofProbe : MonoBehaviour
{
    [Serializable]
    public sealed class Receipt
    {
        public string utc, status, evidence = "ASSISTED_UNITY_SPEECH_ONLY", transcript, error;
        public bool prepared, playbackNonzero, microphoneCancelPassed, microphoneStarted, completedPhrase, listenerPaused, editorMuted;
        public float listenerVolume;
        public int activeListeners;
        public float prepareSeconds, firstOutputSeconds, playbackSeconds;
        public string gameplay = "BLOCKED_BY_SAVED_WORLD_PREFLIGHT", headroom = "GAME_AND_LLM_NOT_VERIFIED";
    }
    public readonly Receipt Result = new Receipt();
    public YQLocalSpeech Speech { get; private set; }
    public string Stage { get; private set; } = "Not started";
    private readonly float[] _output = new float[1024];
    private float _speakBegan;
    private bool _finished, _measureOutput;
    private bool _microphoneTest;

    public void Begin(bool microphone = true)
    {
        _microphoneTest = microphone;
        Speech = gameObject.AddComponent<YQLocalSpeech>();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Stage = "Preparing CPU speech models";
        float began = Time.realtimeSinceStartup;
        Speech.Prepare();
        while (!Speech.IsReady && Time.realtimeSinceStartup - began < 70f) yield return null;
        if (!Speech.IsReady) { Finish("FAILED", Speech.Status); yield break; }
        Result.prepared = true;
        Result.prepareSeconds = Time.realtimeSinceStartup - began;

        Stage = "Playing a short proof line";
        Result.listenerPaused = AudioListener.pause;
        Result.editorMuted = EditorUtility.audioMasterMute;
        Result.listenerVolume = AudioListener.volume;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.isActiveAndEnabled) Result.activeListeners++;
        _speakBegan = Time.realtimeSinceStartup;
        _measureOutput = true;
        Speech.Speak("The local voice route is working. Speak naturally after this line.", "alba");
        while (Speech.IsSpeaking && Time.realtimeSinceStartup - _speakBegan < 65f) yield return null;
        _measureOutput = false;
        Result.playbackSeconds = Time.realtimeSinceStartup - _speakBegan;
        if (!Result.playbackNonzero) { Finish("FAILED", "No nonzero Unity AudioSource output observed: " + Speech.Status); yield break; }
        if (!_microphoneTest) { Finish("UNITY_AUDIO_SAMPLES_PASS_HUMAN_AUDIBILITY_PENDING"); yield break; }

        // note: Retiring a real microphone session must synchronously prevent its eventual transcript callback.
        Stage = "Checking microphone cancellation";
        bool unexpectedCallback = false;
        Speech.BeginDictation(_ => unexpectedCallback = true, Array.Empty<string>());
        began = Time.realtimeSinceStartup;
        while (!Microphone.IsRecording(null) && Time.realtimeSinceStartup - began < 15f) yield return null;
        if (!Microphone.IsRecording(null)) { Finish("FAILED", Speech.Status); yield break; }
        Speech.Cancel();
        yield return new WaitForSecondsRealtime(.5f);
        Result.microphoneCancelPassed = !Microphone.IsRecording(null) && !Speech.IsCapturing && !unexpectedCallback;
        if (!Result.microphoneCancelPassed) { Finish("FAILED", "Microphone cancellation failed"); yield break; }

        Stage = "Speak one test phrase now, then pause";
        Speech.BeginDictation(text => { Result.transcript = text; Result.completedPhrase = true; }, Array.Empty<string>());
        began = Time.realtimeSinceStartup;
        while (!Result.completedPhrase && Time.realtimeSinceStartup - began < 150f)
        {
            Result.microphoneStarted |= Microphone.IsRecording(null);
            if (!Speech.IsCapturing && !Speech.IsTranscribing && !Result.completedPhrase)
                Speech.BeginDictation(text => { Result.transcript = text; Result.completedPhrase = true; }, Array.Empty<string>());
            yield return null;
        }
        Finish(Result.completedPhrase ? "ASSISTED_UNITY_SPEECH_PASS" : "MICROPHONE_STARTED_HUMAN_PHRASE_PENDING");
    }

    private void Update()
    {
        if (!_measureOutput || Result.playbackNonzero) return;
        var source = GetComponent<AudioSource>();
        if (source == null || !source.isPlaying) return;
        source.GetOutputData(_output, 0);
        for (int i = 0; i < _output.Length; i++)
            if (Mathf.Abs(_output[i]) > .00001f)
            { Result.playbackNonzero = true; Result.firstOutputSeconds = Time.realtimeSinceStartup - _speakBegan; break; }
    }

    public void Finish(string status, string error = null)
    {
        if (_finished) return;
        _finished = true;
        StopAllCoroutines();
        if (Speech != null) Speech.enabled = false;
        Stage = Result.status = status;
        Result.error = error;
        Result.utc = DateTime.UtcNow.ToString("O");
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "outputs/LocalSpeech/unity-proof");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, _microphoneTest ? "receipt.json" : "audio-retest.json"), JsonUtility.ToJson(Result, true));
        Debug.Log("[YQSpeechProof] " + status + (error == null ? "" : ": " + error));
    }

    private void OnDisable() { if (!_finished) Finish("Play Mode ended"); }
}
