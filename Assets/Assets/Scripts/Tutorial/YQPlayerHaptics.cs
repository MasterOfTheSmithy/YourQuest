using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class YQPlayerHaptics : MonoBehaviour
{
    [Range(0f, 1f)] public float intensity = 1f;
    [Range(0f, 1f)] public float cameraFeedbackIntensity = .65f;
    private Gamepad device;
    private float low, high, until;
    private YQInvestorVitals vitals;

    private void Awake()
    {
        // note: These are local presentation preferences, never character/save mechanics.
        intensity = PlayerPrefs.GetFloat("YQ.Haptics.Intensity", intensity);
        cameraFeedbackIntensity = PlayerPrefs.GetFloat("YQ.CameraFeedback.Intensity", cameraFeedbackIntensity);
        vitals = GetComponent<YQInvestorVitals>();
        device = Gamepad.current;
    }

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("YQ.Haptics.Intensity", intensity);
        if (intensity <= 0f) StopFeedback();
    }

    public void SetCameraIntensity(float value)
    {
        cameraFeedbackIntensity = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("YQ.CameraFeedback.Intensity", cameraFeedbackIntensity);
    }

    public void Pulse(float lowMotor, float highMotor, float seconds)
    {
        if (!isActiveAndEnabled || intensity <= 0f || RuntimeModalUiBlocker.IsBlocked || (vitals != null && vitals.IsDead)) return;
        // note: Overlapping feedback is bounded and mixed by maximum strength, without coroutines or queued vibration.
        low = Mathf.Max(low, Mathf.Clamp01(lowMotor));
        high = Mathf.Max(high, Mathf.Clamp01(highMotor));
        until = Mathf.Max(until, Time.unscaledTime + Mathf.Clamp(seconds, .01f, .35f));
    }

    private void Update()
    {
        if (vitals == null) vitals = GetComponent<YQInvestorVitals>();
        if (device != Gamepad.current) { StopFeedback(); device = Gamepad.current; }
        if (Time.unscaledTime >= until || Time.timeScale <= 0f || RuntimeModalUiBlocker.IsBlocked ||
            !Application.isFocused || (vitals != null && vitals.IsDead)) { StopFeedback(); return; }
        device?.SetMotorSpeeds(low * intensity, high * intensity);
    }

    public void StopFeedback()
    {
        if (device != null && device.added) device.SetMotorSpeeds(0f, 0f);
        low = high = until = 0f;
    }
    private void OnDisable() => StopFeedback();
    private void OnDestroy() => StopFeedback();
    private void OnApplicationFocus(bool focused) { if (!focused) StopFeedback(); }
}
