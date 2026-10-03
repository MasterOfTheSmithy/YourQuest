using System;
using UnityEngine;

// note: Approved in-place travel samples are presentation/movement configuration, never an additional actor or persistent-state owner.
[Serializable]
public sealed class YQDotMotionProfile
{
    public float startDuration, loopDuration, stopDuration;
    [Newtonsoft.Json.JsonIgnore] public Vector3 localForward;
    public float[] startDistance = Array.Empty<float>(), loopDistance = Array.Empty<float>(), stopDistance = Array.Empty<float>();

    public bool IsValid => Valid(startDistance, startDuration) && Valid(loopDistance, loopDuration) && Valid(stopDistance, stopDuration);
    private static bool Valid(float[] samples, float duration)
    {
        if (samples == null || samples.Length < 2 || !float.IsFinite(duration) || duration <= 0f || Mathf.Abs(samples[0]) > .0001f) return false;
        float previous = 0f;
        foreach (float value in samples) { if (!float.IsFinite(value) || value < previous - .00001f) return false; previous = value; }
        return previous > 0f;
    }

    public static float Sample(float[] samples, float duration, float time)
    {
        // note: Linear interpolation of delivered source frames preserves acceleration/deceleration at other frame rates and the exact total travel.
        float position = Mathf.Clamp01(time / duration) * (samples.Length - 1);
        int lower = Mathf.Min(Mathf.FloorToInt(position), samples.Length - 2);
        return Mathf.Lerp(samples[lower], samples[lower + 1], position - lower);
    }
}

public sealed class YQDotGaitSampler
{
    private readonly YQDotMotionProfile _profile;
    private int _phase;
    private double _time;
    private static readonly string[] States = { "DotIdle", "DotStart", "DotWalk", "DotStop" };
    public YQDotGaitSampler(YQDotMotionProfile profile)
    { if (profile == null || !profile.IsValid) throw new ArgumentException("Invalid approved gait profile."); _profile = profile; }
    public bool IsActive => _phase != 0;
    public string StateName => States[_phase];
    public float NormalizedTime => _phase == 0 ? 0f : Mathf.Clamp01((float)(_time / Duration));
    private float Duration => _phase == 1 ? _profile.startDuration : _phase == 2 ? _profile.loopDuration : _profile.stopDuration;
    private float[] Samples => _phase == 1 ? _profile.startDistance : _phase == 2 ? _profile.loopDistance : _profile.stopDistance;
    public float StoppingDistance => _phase == 2 && _time <= .000001f ? _profile.stopDistance[_profile.stopDistance.Length - 1] : _phase == 0 ? _profile.startDistance[_profile.startDistance.Length - 1] + _profile.stopDistance[_profile.stopDistance.Length - 1] :
        Samples[Samples.Length - 1] - YQDotMotionProfile.Sample(Samples, Duration, (float)_time) + (_phase == 3 ? 0f : _profile.stopDistance[_profile.stopDistance.Length - 1]);
    // note: Beginning another loop needs room for both that complete loop and its stop; stopping at a boundary needs only the stop curve.
    public float ContinuationDistance => _phase == 2 && _time <= .000001f ? _profile.loopDistance[_profile.loopDistance.Length - 1] + _profile.stopDistance[_profile.stopDistance.Length - 1] : StoppingDistance;
    public void Reset() { _phase = 0; _time = 0f; }

    public float Advance(bool wantsToMove, float deltaTime)
    {
        if (!float.IsFinite(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (_phase == 0) { if (!wantsToMove) return 0f; _phase = 1; _time = 0f; }
        if (_phase == 2 && !wantsToMove && _time <= .000001f) { _phase = 3; _time = 0f; }
        float distance = 0f; double remaining = Mathf.Min(deltaTime, 1f);
        // note: Finish an authored start/loop boundary before stopping; phase changes use the matching endpoint poses without inventing a new root-motion stream.
        for (int boundary = 0; boundary < 8 && remaining > 0f && _phase != 0; boundary++)
        {
            float duration = Duration; double consumed = Math.Min(remaining, duration - _time);
            // note: Accumulate phase time in double precision so high-frame-rate sampling does not drift across loop/stop boundaries.
            distance += YQDotMotionProfile.Sample(Samples, duration, (float)(_time + consumed)) - YQDotMotionProfile.Sample(Samples, duration, (float)_time);
            _time += consumed; remaining -= consumed;
            if (_time >= duration - .000001f)
            { _phase = _phase == 3 ? 0 : wantsToMove ? 2 : 3; _time = 0f; }
        }
        return Mathf.Max(0f, distance);
    }
}
