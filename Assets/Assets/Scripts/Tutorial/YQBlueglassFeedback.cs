using UnityEngine;
using UnityEngine.InputSystem;

public enum YQBlueglassCue { Hover, Exit, Press, Select, Confirm, Reject, Screen, Scroll }

[DisallowMultipleComponent]
public sealed class YQBlueglassFeedback : MonoBehaviour
{
    private static YQBlueglassFeedback _instance;
    private Gamepad _pad;
    private float _stopAt;
    private float _lastStart = -1f;
    private int _priority;
    public static bool HapticsEnabled
    {
        get => PlayerPrefs.GetInt("YQ.UI.Haptics", 1) != 0;
        set { PlayerPrefs.SetInt("YQ.UI.Haptics", value ? 1 : 0); if (!value) _instance?.StopOwnedPulse(); }
    }
    public static bool ReducedMotion
    {
        get => PlayerPrefs.GetInt("YQ.UI.ReducedMotion", 0) != 0;
        set => PlayerPrefs.SetInt("YQ.UI.ReducedMotion", value ? 1 : 0);
    }

    public static bool ControllerActive
    {
        get
        {
            Gamepad pad = Gamepad.current;
            if (pad == null || !pad.enabled) return false;
            double other = System.Math.Max(Keyboard.current?.lastUpdateTime ?? -1, Mouse.current?.lastUpdateTime ?? -1);
            return pad.lastUpdateTime > other;
        }
    }

    public static void Request(YQBlueglassCue cue, bool controller)
    {
        // note: Mouse/keyboard interactions also receive tactile feedback when a supported controller is connected and haptics are enabled.
        if (Gamepad.current == null || !Gamepad.current.enabled || !HapticsEnabled || !Application.isFocused) return;
        if (_instance == null)
        {
            // note: Exactly one UI-only pulse arbiter; no new gameplay or save owner.
            _instance = new GameObject("YourQuest UI feedback").AddComponent<YQBlueglassFeedback>();
            DontDestroyOnLoad(_instance.gameObject);
        }
        int priority = cue == YQBlueglassCue.Confirm || cue == YQBlueglassCue.Reject ? 2 : 1;
        if (_instance._pad != null && _instance._stopAt > Time.unscaledTime && priority < _instance._priority) return;
        if (priority == 1 && Time.unscaledTime - _instance._lastStart < .035f) return;
        float low = cue == YQBlueglassCue.Confirm ? .24f : cue == YQBlueglassCue.Reject ? .28f : .06f;
        float high = cue == YQBlueglassCue.Reject ? .05f : cue == YQBlueglassCue.Confirm ? .16f : .10f;
        _instance.StopOwnedPulse();
        _instance._pad = Gamepad.current;
        _instance._priority = priority;
        _instance._lastStart = Time.unscaledTime;
        _instance._stopAt = Time.unscaledTime + (priority == 2 ? .065f : .020f);
        _instance._pad.SetMotorSpeeds(low, high);
    }

    private void Update()
    {
        // note: Unscaled expiry works in full pause; disconnected/disabled devices cannot leave an owned pulse running.
        if (_pad != null && (Time.unscaledTime >= _stopAt || !_pad.added || !_pad.enabled)) StopOwnedPulse();
    }
    private void StopOwnedPulse()
    {
        if (_pad != null && _pad.added) _pad.SetMotorSpeeds(0f, 0f);
        _pad = null;
    }
    private void OnApplicationFocus(bool focused) { if (!focused) StopOwnedPulse(); }
    private void OnApplicationPause(bool paused) { if (paused) StopOwnedPulse(); }
    private void OnDisable() => StopOwnedPulse();
    private void OnDestroy() { StopOwnedPulse(); if (_instance == this) _instance = null; }
}
