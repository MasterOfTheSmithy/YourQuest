using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

// note: Invoke the actual managed recovery methods without a native scene, microphone, model or persistent store.
public static class MemoryRecoveryChecks
{
    static readonly List<object> Results = new List<object>();
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(string id, bool pass)
    {
        Results.Add(new { id, pass });
        if (!pass) throw new Exception(id);
    }
    static void Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
    static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);

    public static int Main(string[] args)
    {
        var agent = (NpcDialogueAgent)RuntimeHelpers.GetUninitializedObject(typeof(NpcDialogueAgent));
        Invoke(agent, "ReportReplyUnavailable", "Insufficient memory", true);
        Check("structured_memory_failure", agent.LastReplyGraphicsMemoryUnavailable && agent.LastReplyStatus == "Insufficient memory");
        Invoke(agent, "ReportReplyUnavailable", "Save unavailable", false);
        Check("different_failure_clears_memory_reason", !agent.LastReplyGraphicsMemoryUnavailable && agent.LastReplyStatus == "Save unavailable");

        var ui = (YQInvestorDialogueUI)RuntimeHelpers.GetUninitializedObject(typeof(YQInvestorDialogueUI));
        var player = new PlayerState { playerId = "detached-memory-recovery" };
        Invoke(ui, "SynchronizeVoiceProfile", player);
        int epoch = Read<int>(ui, "_liveEpoch");
        Invoke(ui, "PauseLiveVoiceForMemory");
        Check("failure_latches_without_muting_preference", Read<bool>(ui, "_voiceInferenceBlocked") && !ui.voiceMuted);
        Check("speculative_capture_epoch_retired", Read<int>(ui, "_liveEpoch") > epoch && !Read<bool>(ui, "_liveWaiting"));
        Invoke(ui, "RetireLiveVoice");
        Invoke(ui, "OnApplicationFocus", false);
        Invoke(ui, "OnApplicationFocus", true);
        Invoke(ui, "SynchronizeVoiceProfile", player);
        Check("focus_retire_and_same_profile_do_not_retry", Read<bool>(ui, "_voiceInferenceBlocked"));
        Invoke(ui, "ToggleVoiceInput");
        Check("explicit_retry_resumes_unmuted_capture", !Read<bool>(ui, "_voiceInferenceBlocked") && !ui.voiceMuted);
        Invoke(ui, "ToggleVoiceInput");
        Check("ordinary_toggle_still_mutes", ui.voiceMuted);
        Invoke(ui, "PauseLiveVoiceForMemory");
        Check("late_failure_preserves_user_mute", ui.voiceMuted && Read<bool>(ui, "_voiceInferenceBlocked"));
        Invoke(ui, "ToggleVoiceInput");
        Check("explicit_unmute_clears_latch", !ui.voiceMuted && !Read<bool>(ui, "_voiceInferenceBlocked"));
        Invoke(ui, "PauseLiveVoiceForMemory");
        Invoke(ui, "SynchronizeVoiceProfile", new PlayerState { playerId = "other-detached-profile" });
        Check("profile_switch_clears_old_failure", !Read<bool>(ui, "_voiceInferenceBlocked"));
        ui.SetVoiceMuted(true);
        Invoke(ui, "SynchronizeVoiceProfile", player);
        Check("profile_switch_preserves_user_mute", ui.voiceMuted);

        // note: The UI repair must not relax the existing full-GPU admission or approved memory reserve.
        var fit = typeof(LlamaCppServerProcess).GetMethod("CanFitFullGpu", BindingFlags.Static | BindingFlags.NonPublic);
        long bytes = 2834975040L;
        Check("observed_memory_rejected", !(bool)fit.Invoke(null, new object[] { 3083, 1536, bytes }));
        Check("admission_minimum_rejected_below", !(bool)fit.Invoke(null, new object[] { 4495, 1536, bytes }));
        Check("admission_minimum_passes", (bool)fit.Invoke(null, new object[] { 4496, 1536, bytes }));
        Check("reserve_cannot_be_lowered", !(bool)fit.Invoke(null, new object[] { 3083, 0, bytes }));
        File.WriteAllText(Path.Combine(args[0], "checks.json"), JsonConvert.SerializeObject(Results, Formatting.Indented));
        Console.WriteLine("PASS " + Results.Count + " managed recovery/admission checks; no native microphone or gameplay claim.");
        return 0;
    }
}
