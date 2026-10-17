using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Pure admission rules support the existing LLMClient queue; this class owns no model, state or scheduler.
internal static class YQLlmResidencyPolicy
{
    internal const long MiB = 1024L * 1024L;

    internal static bool ShouldReclaimGameSystemMemory(long availableBytes, long allocatedGameBytes, bool inferenceBusy)
    {
        // note: Unknown host telemetry is not pressure. Protect active inference and trim game caches only when the host has at most 2 GB free and the game owns substantial allocations.
        return !inferenceBusy && availableBytes > 0 && availableBytes <= 2048L * MiB && allocatedGameBytes >= 4096L * MiB;
    }

    internal static bool CanRetireGameCache(float now, float lastUse, float keepWarmSeconds)
    {
        // note: Unknown clocks, recent loads and a reversed clock never prove that a source cache is dormant.
        return !float.IsNaN(now) && !float.IsNaN(lastUse) && !float.IsInfinity(now) && !float.IsInfinity(lastUse) &&
            now >= lastUse && now - lastUse >= Math.Max(1f, keepWarmSeconds);
    }

    internal static float BackgroundGraceDelay(float now, float lastPlayerActivity, float firstQueued,
        float graceSeconds, float maximumWaitSeconds)
    {
        // note: Grace is a scheduling delay, never a deadline or cancellation; already-aged work keeps its place in the queue.
        return Math.Max(0f, Math.Min(Math.Max(0f, graceSeconds) - Math.Max(0f, now - lastPlayerActivity),
            Math.Max(0f, maximumWaitSeconds) - Math.Max(0f, now - firstQueued)));
    }

    internal static bool IsLocalEndpoint(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri) && uri.IsLoopback && uri.Scheme == "http";

    internal static bool SameModel(string left, string right)
    {
        string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty :
            value.Trim().Contains(":") ? value.Trim() : value.Trim() + ":latest";
        return !string.IsNullOrWhiteSpace(left) && string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    internal static string OptionsKey(JObject options)
    {
        // note: Any option change conservatively requires a new placement check after dispatch, including context growth.
        var ordered = new SortedDictionary<string, JToken>(StringComparer.Ordinal);
        foreach (JProperty property in options.Properties()) ordered[property.Name] = property.Value;
        return JsonConvert.SerializeObject(ordered);
    }

    internal static bool ExplicitCpu(JObject options)
        => options?["num_gpu"]?.Type == JTokenType.Integer && options.Value<long>("num_gpu") == 0;

    internal static bool CanRetainCpu(string response, string model, int maximumResidentMb,
        long availableBytes, int reserveMb, long additionalMainBytes)
    {
        // note: Missing/ambiguous residency or memory telemetry is not proof of CPU placement or capacity.
        if (availableBytes <= 0 || maximumResidentMb <= 0 || reserveMb < 0 || additionalMainBytes < 0 ||
            availableBytes / MiB < (long)reserveMb + (additionalMainBytes + MiB - 1) / MiB) return false;
        try
        {
            var models = JObject.Parse(response)["models"] as JArray;
            if (models == null) return false;
            int matches = 0;
            foreach (JToken entry in models)
            {
                if (!(entry is JObject item)) return false;
                if (!SameModel((string)item["name"], model) && !SameModel((string)item["model"], model)) continue;
                if (++matches != 1 || item["size_vram"]?.Type != JTokenType.Integer ||
                    item["size"]?.Type != JTokenType.Integer || item.Value<long>("size_vram") != 0 ||
                    item.Value<long>("size") <= 0 || item.Value<long>("size") > maximumResidentMb * MiB) return false;
            }
            return matches == 1;
        }
        catch (Exception error) when (error is JsonException || error is ArgumentException || error is OverflowException)
        { return false; }
    }

    internal static string PromptScope(string profile, string world, int epoch, long playerRevision, long worldRevision,
        string modelPolicy, string outputContract)
    {
        // note: This is a reuse guard, never cached game state; the complete current prompt is sent on every call.
        if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(world) || epoch < 0) return string.Empty;
        return JsonConvert.SerializeObject(new { profile, world, epoch, playerRevision, worldRevision, modelPolicy, outputContract });
    }

    internal static bool CanReusePrompt(string previousScope, string nextScope)
        => !string.IsNullOrEmpty(nextScope) && string.Equals(previousScope, nextScope, StringComparison.Ordinal);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint length, load;
        public ulong totalPhysical, availablePhysical, totalPageFile, availablePageFile;
        public ulong totalVirtual, availableVirtual, availableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    internal static long AvailableSystemMemoryBytes()
    {
        // note: Windows supplies actual free physical RAM without spawning a blocking probe. Unsupported hosts use serial eviction.
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return 0;
        try
        {
            var status = new MemoryStatus { length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
            return GlobalMemoryStatusEx(ref status) && status.availablePhysical <= long.MaxValue ? (long)status.availablePhysical : 0;
        }
        catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException)
        { return 0; }
    }
}
