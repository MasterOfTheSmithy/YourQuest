#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// note: These menu commands provide the exact bounded beta-fixture operations without exposing them to release players.
public static class YQProductionBaselineMenu
{
    // note: Reset is deliberately available only in Play Mode because the profile owner lives in the runtime save system.
    [MenuItem("YourQuest/Beta Baseline/Reset Canonical Dev Profile")]
    public static void ResetCanonicalDevProfile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[YQProductionBaseline] Enter Play Mode before resetting the canonical dev profile.");
            return;
        }

        YQProfileSaveSystem profile = YQProfileSaveSystem.Instance;
        if (profile == null || !profile.EnsureCanonicalDevelopmentProfile(true))
        {
            Debug.LogError("[YQProductionBaseline] Canonical dev profile reset failed.");
            return;
        }

        Debug.Log("[YQProductionBaseline] Canonical dev profile ready: id=" +
            YQBetaDevelopmentFixture.CanonicalProfileId +
            " seed=" + YQBetaDevelopmentFixture.CanonicalWorldSeed);
    }

    // note: Creation without reset preserves the fixture save while making the profile available on a clean machine.
    [MenuItem("YourQuest/Beta Baseline/Ensure Canonical Dev Profile")]
    public static void EnsureCanonicalDevProfile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[YQProductionBaseline] Enter Play Mode before ensuring the canonical dev profile.");
            return;
        }

        YQProfileSaveSystem profile = YQProfileSaveSystem.Instance;
        if (profile == null || !profile.EnsureCanonicalDevelopmentProfile(false))
        {
            Debug.LogError("[YQProductionBaseline] Canonical dev profile creation failed.");
            return;
        }

        Debug.Log("[YQProductionBaseline] Canonical dev profile active: id=" +
            YQBetaDevelopmentFixture.CanonicalProfileId +
            " seed=" + YQBetaDevelopmentFixture.CanonicalWorldSeed);
    }

    // note: This command records the current runtime authority without mutating saves or rebuilding the world.
    [MenuItem("YourQuest/Beta Baseline/Log Runtime Diagnostics")]
    public static void LogRuntimeDiagnostics()
    {
        YQProductionBaselineDiagnostics diagnostics = YQProductionBaselineDiagnostics.Instance;
        if (diagnostics == null)
        {
            Debug.LogError("[YQProductionBaseline] Runtime diagnostics owner is missing.");
            return;
        }

        diagnostics.CaptureAndReport(true);
    }
}
#endif
