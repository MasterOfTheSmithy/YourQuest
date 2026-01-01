using System.Collections.Generic;
using UnityEngine;

public enum MovementPattern
{
    Travel,
    Erratic,
    Retreat,
    Pursuit,
    Circling,
    Idle
}

/// <summary>
/// Cheap movement classification from position samples.
/// You can feed it from your player controller or let it sample a target transform.
/// </summary>
public class MovementPatternClassifier : MonoBehaviour
{
    [Header("Sampling")]
    public Transform playerTransform;
    public float sampleRateHz = 6f;
    public float windowSeconds = 5f;

    [Header("Thresholds")]
    public float minMoveSpeed = 0.2f;
    public float reversalDotThreshold = -0.25f;

    private struct Sample { public Vector3 pos; public float t; }
    private readonly Queue<Sample> samples = new Queue<Sample>();

    private float nextSampleTime;

    public MovementPattern CurrentPattern { get; private set; } = MovementPattern.Idle;
    public float AvgSpeed { get; private set; }
    public float Turniness { get; private set; } // 0..1

    private void Update()
    {
        if (playerTransform == null) return;

        if (Time.time >= nextSampleTime)
        {
            nextSampleTime = Time.time + (1f / Mathf.Max(0.1f, sampleRateHz));
            AddSample(playerTransform.position);
            Compute();
        }
    }

    public void AddSample(Vector3 pos)
    {
        samples.Enqueue(new Sample { pos = pos, t = Time.time });
        Trim();
    }

    private void Trim()
    {
        float cutoff = Time.time - windowSeconds;
        while (samples.Count > 0 && samples.Peek().t < cutoff)
            samples.Dequeue();
    }

    private void Compute()
    {
        if (samples.Count < 4)
        {
            CurrentPattern = MovementPattern.Idle;
            AvgSpeed = 0f;
            Turniness = 0f;
            return;
        }

        var arr = samples.ToArray();

        float distSum = 0f;
        float timeSum = arr[arr.Length - 1].t - arr[0].t;
        int dirChanges = 0;
        int considered = 0;

        for (int i = 1; i < arr.Length; i++)
        {
            distSum += Vector3.Distance(arr[i - 1].pos, arr[i].pos);
        }

        AvgSpeed = timeSum > 0.01f ? distSum / timeSum : 0f;

        // Direction reversals = erraticness
        for (int i = 2; i < arr.Length; i++)
        {
            Vector3 v1 = arr[i - 1].pos - arr[i - 2].pos;
            Vector3 v2 = arr[i].pos - arr[i - 1].pos;
            if (v1.sqrMagnitude < 0.0004f || v2.sqrMagnitude < 0.0004f) continue;

            considered++;
            float dot = Vector3.Dot(v1.normalized, v2.normalized);
            if (dot < reversalDotThreshold) dirChanges++;
        }

        Turniness = considered > 0 ? Mathf.Clamp01(dirChanges / (float)considered) : 0f;

        if (AvgSpeed < minMoveSpeed)
        {
            CurrentPattern = MovementPattern.Idle;
            return;
        }

        // Simple classification:
        if (Turniness > 0.45f) CurrentPattern = MovementPattern.Erratic;
        else CurrentPattern = MovementPattern.Travel;
    }
}
