#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// note: This is the single editor entry point for the deterministic production-slice checks and live runtime snapshot.
public static class YQProductionBaselineRegression
{
    // note: The menu action is intentionally one command so beta verification does not depend on remembering individual test classes.
    [MenuItem("YourQuest/Beta Baseline/Run Production Regression")]
    public static void RunProductionRegression()
    {
        List<string> failures = RunPureRegression();
        List<YQBaselineResultRecord> records = new List<YQBaselineResultRecord>
        {
            CreatePureRecord(failures)
        };
        if (Application.isPlaying)
        {
            YQProductionBaselineDiagnostics diagnostics = YQProductionBaselineDiagnostics.Instance;
            if (diagnostics == null)
            {
                failures.Add("Runtime diagnostics owner is missing.");
                records.Add(CreateRuntimeUnavailableRecord("Runtime diagnostics owner is missing."));
            }
            else
            {
                // note: A failed readiness check still emits all independent records so one blocked boundary cannot hide traversal or reload evidence.
                records.AddRange(diagnostics.CaptureResultRecords());
                if (!diagnostics.TryValidateRuntime(out string runtimeFailure))
                    failures.Add("Live runtime: " + runtimeFailure);
            }
        }
        else
        {
            Debug.Log("[YQProductionBaselineRegression] Live runtime checks are deferred until Play Mode.");
            records.Add(CreateRuntimeUnavailableRecord("Unity is not in Play Mode; no live runtime check was executed."));
        }

        // note: Every receipt is logged independently so skipped runtime checks cannot be promoted to an aggregate PASS.
        for (int index = 0; index < records.Count; index++)
            Debug.Log("[YQProductionBaselineReceipt] " + records[index].ToLogLine());

        if (failures.Count == 0)
        {
            Debug.Log("[YQProductionBaselineRegression] PASS: pure contracts complete; runtime result records above remain authoritative.");
            return;
        }

        for (int index = 0; index < failures.Count; index++)
            Debug.LogError("[YQProductionBaselineRegression] FAIL: " + failures[index]);
    }

    // note: Pure editor fixtures get their own receipt and cannot make a skipped runtime boundary appear green.
    private static YQBaselineResultRecord CreatePureRecord(List<string> failures)
    {
        bool passed = failures == null || failures.Count == 0;
        return new YQBaselineResultRecord
        {
            checkId = "pure.contracts",
            category = "pure-contracts",
            status = passed ? YQBaselineResultStatus.PASS : YQBaselineResultStatus.FAIL,
            evidence = YQBaselineEvidenceLevel.EDITOR_TOOL_VERIFIED,
            expected = "Deterministic production-slice contracts pass.",
            actual = passed ? "all registered pure checks passed" : "one or more pure checks failed",
            reproduction = "YourQuest > Beta Baseline > Run Production Regression",
            owner = "G01 / owning downstream goal"
        };
    }

    // note: Missing Play Mode is an explicit not-yet-testable result, not a regression PASS.
    private static YQBaselineResultRecord CreateRuntimeUnavailableRecord(string actual)
    {
        return new YQBaselineResultRecord
        {
            checkId = "live.runtime",
            category = "live-snapshots",
            status = YQBaselineResultStatus.NOT_YET_TESTABLE,
            evidence = YQBaselineEvidenceLevel.NOT_VERIFIED,
            expected = "The enabled PlaySafe runtime path is exercised.",
            actual = actual,
            reproduction = "Enter Play Mode in Assets/Assets/Scenes/YourQuest_PlaySafe.unity and rerun.",
            owner = "G01"
        };
    }

    // note: Pure checks reuse the established production-slice fixtures instead of creating a second test architecture.
    public static List<string> RunPureRegression()
    {
        List<string> failures = new List<string>();
        Run("parcel earthworks", YQProductionSliceRegressionTests.TestParcelEarthworks, failures);
        Run("origin ownership", YQProductionSliceRegressionTests.TestOriginOwnership, failures);
        Run("origin prompt budget", YQProductionSliceRegressionTests.TestOriginPromptBudget, failures);
        Run("Goddess prose", YQProductionSliceRegressionTests.TestGoddessProsePreservation, failures);
        Run("quest progress and persistence", YQProductionSliceRegressionTests.TestQuestProgressAndPersistence, failures);
        Run("quest marker contract", YQProductionSliceRegressionTests.TestQuestMarkerContract, failures);
        Run("accepted-origin bootstrap", YQProductionSliceRegressionTests.TestBootstrapPreservesAcceptedOrigin, failures);
        Run("dialogue profile isolation", YQProductionSliceRegressionTests.TestDialogueIsolation, failures);
        Run("profile preflight", YQProductionSliceRegressionTests.TestProfilePreflight, failures);
        Run("canonical state contracts", YQProductionSliceRegressionTests.TestCanonicalStateContracts, failures);
        Run("profile commit recovery", YQProductionSliceRegressionTests.TestProfileCommitRecovery, failures);
        Run("mutation and event contracts", YQProductionSliceRegressionTests.TestMutationAndEventContracts, failures);
        Run("service lifecycle contracts", YQProductionSliceRegressionTests.TestServiceLifecycleContracts, failures);
        Run("LLM proposal boundary", YQProductionSliceRegressionTests.TestLlmProposalBoundary, failures);

        // note: These values prove the report and fixture share one declared production contract before any scene is loaded.
        if (YQWorldGenerationArchitecture.AllowsLegacyRuntimeBuilder)
            failures.Add("Legacy scatter materialization is active.");
        if (string.IsNullOrWhiteSpace(YQBetaDevelopmentFixture.CanonicalWorldSeed))
            failures.Add("Canonical fixture seed is empty.");
        return failures;
    }

    private static bool profileIsolationRunning;
    private static bool profileReloadPreparationRunning;

    // note: This preparation selects the persisted comparison profile through the existing save owner before an actual Play Mode restart.
    public static void PrepareProfileBForReload()
    {
        if (profileReloadPreparationRunning)
            return;

        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        YQTitleScreenUI title = YQTitleScreenUI.Instance != null
            ? YQTitleScreenUI.Instance
            : UnityEngine.Object.FindFirstObjectByType<YQTitleScreenUI>();
        YQProfileSaveSystem.ProfileEntry comparison = null;
        if (system != null)
        {
            for (int index = system.Profiles.Count - 1; index >= 0; index--)
            {
                YQProfileSaveSystem.ProfileEntry candidate = system.Profiles[index];
                if (candidate != null && !string.Equals(candidate.profileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    comparison = candidate;
                    break;
                }
            }
        }

        if (system == null || title == null || comparison == null || !system.LoadProfile(comparison.profileId))
        {
            LogProfileReloadResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The persisted comparison profile could not be selected before restart.");
            return;
        }

        profileReloadPreparationRunning = true;
        title.OpenAtStartup();
        YQProductionBaselineDiagnostics host = YQProductionBaselineDiagnostics.Instance;
        if (host == null)
        {
            profileReloadPreparationRunning = false;
            LogProfileReloadResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The runtime diagnostics owner is unavailable before restart.");
            return;
        }
        host.StartCoroutine(ProfileBReloadPreparationRoutine(title, comparison.profileId));
    }

    // note: The preparation waits for the normal title fade so the saved comparison profile is the active authority when Play Mode is stopped.
    private static IEnumerator ProfileBReloadPreparationRoutine(YQTitleScreenUI title, string profileId)
    {
        yield return new WaitForSecondsRealtime(0.60f);
        bool completed = InvokePrivate(title, "CompleteStartupFlow", "Prepared comparison profile for reload.");
        profileReloadPreparationRunning = false;
        LogProfileReloadResult(completed ? YQBaselineResultStatus.PASS : YQBaselineResultStatus.NOT_YET_TESTABLE,
            completed ? "Prepared active profile B=" + profileId + " for Play Mode restart." : "The existing title completion action is unavailable before restart.");
    }

    // note: This receipt verifies the comparison profile after the editor has stopped and restarted the real PlaySafe session.
    public static void VerifyProfileBReload()
    {
        YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
        PlayerState player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        bool profileB = system != null && !string.IsNullOrWhiteSpace(system.ActiveProfileId) &&
            !string.Equals(system.ActiveProfileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase);
        bool origin = player != null && GeneratedRpgContentService.HasCompletedOrigin(player);
        bool plan = world != null && world.generatedWorldPlan != null && !string.IsNullOrWhiteSpace(world.generatedWorldPlan.worldSeed);
        string playerId = player != null ? player.playerId : string.Empty;
        string worldId = world != null && world.worldIdentity != null ? world.worldIdentity.worldId : string.Empty;
        string seed = world != null && world.generatedWorldPlan != null ? world.generatedWorldPlan.worldSeed : string.Empty;
        int playerObjects = GameObject.FindGameObjectsWithTag("Player").Length;
        int playerMotors = UnityEngine.Object.FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        bool passed = profileB && origin && plan && !string.IsNullOrWhiteSpace(playerId) && !string.IsNullOrWhiteSpace(worldId) && playerObjects == 1 && playerMotors == 1;
        LogProfileReloadResult(passed ? YQBaselineResultStatus.PASS : YQBaselineResultStatus.FAIL,
            "active=" + (system != null ? system.ActiveProfileId : string.Empty) + " player=" + playerId + " world=" + worldId + " seed=" + seed +
            " origin=" + origin + " plan=" + plan + " playerObjects=" + playerObjects + " playerMotors=" + playerMotors +
            " loads=" + (system != null ? system.SuccessfulLoadCount.ToString() : "0") + " saves=" + (system != null ? system.SuccessfulSaveCount.ToString() : "0"));
    }

    // note: This development-only workflow exercises the existing title and origin owners for the second profile without creating a parallel save or gameplay architecture.
    public static void RunProfileBOriginRegression()
    {
        if (profileIsolationRunning)
            return;

        YQProductionBaselineDiagnostics host = YQProductionBaselineDiagnostics.Instance;
        if (host == null)
        {
            LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The runtime diagnostics owner is unavailable.");
            return;
        }

        profileIsolationRunning = true;
        host.StartCoroutine(ProfileBOriginRegressionRoutine());
    }

    // note: The coroutine gives the visible title transition and questionnaire callbacks their normal frame boundaries before comparing the two loaded authorities.
    private static IEnumerator ProfileBOriginRegressionRoutine()
    {
        try
        {
            YQProfileSaveSystem system = YQProfileSaveSystem.Instance;
            YQTitleScreenUI title = YQTitleScreenUI.Instance != null
                ? YQTitleScreenUI.Instance
                : UnityEngine.Object.FindFirstObjectByType<YQTitleScreenUI>();
            if (system == null || title == null)
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The existing profile or title owner is unavailable.");
                yield break;
            }

            YQProfileSaveSystem.ProfileEntry comparison = null;
            for (int index = system.Profiles.Count - 1; index >= 0; index--)
            {
                YQProfileSaveSystem.ProfileEntry candidate = system.Profiles[index];
                if (candidate != null && !string.Equals(candidate.profileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    comparison = candidate;
                    break;
                }
            }

            if (comparison == null || !system.LoadProfile(comparison.profileId))
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "No persisted comparison profile could be loaded through the existing profile owner.");
                yield break;
            }

            // note: Reopen the real title gate around profile B so the origin questionnaire is entered through its normal startup ownership boundary.
            title.OpenAtStartup();
            yield return new WaitForSecondsRealtime(0.60f);
            if (!InvokePrivate(title, "CompleteStartupFlow", "Loaded comparison profile."))
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The existing title completion action is unavailable.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.70f);

            YQOriginQuestionnaireUI questionnaire = UnityEngine.Object.FindFirstObjectByType<YQOriginQuestionnaireUI>();
            if (questionnaire == null || !questionnaire.OpenIfNeededAfterTitle())
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "Profile B did not expose the existing origin questionnaire boundary.");
                yield break;
            }

            yield return null;
            Type viewType = typeof(YQOriginQuestionnaireUI);
            if (!InvokePrivate(questionnaire, "BeginQuestions", "Casual", 1))
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The existing origin question action is unavailable.");
                yield break;
            }

            object input = GetPrivateField(questionnaire, "_input");
            PropertyInfo inputText = input != null ? input.GetType().GetProperty("text") : null;
            if (inputText == null)
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The existing origin input control is unavailable.");
                yield break;
            }

            // note: Force only the existing deterministic fallback for this verification answer, then restore the normal origin-generation setting immediately.
            YQOriginGenerationService origin = YQOriginGenerationService.Instance;
            bool restoreLlm = origin != null && origin.enableLlmOriginGeneration;
            if (origin != null)
                origin.enableLlmOriginGeneration = false;
            inputText.SetValue(input, "I protect the people who share my road and measure what I build.");
            bool submitted = InvokePrivate(questionnaire, "SubmitAnswer");
            if (origin != null)
                origin.enableLlmOriginGeneration = restoreLlm;
            if (!submitted)
            {
                LogProfileIsolationResult(YQBaselineResultStatus.NOT_YET_TESTABLE, "The existing origin submit action is unavailable.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 6f;
            while (!questionnaire.StartupPhaseResolved && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerState bPlayer = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
            WorldState bWorld = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
            bool bOrigin = bPlayer != null && GeneratedRpgContentService.HasCompletedOrigin(bPlayer);
            bool bPlan = bWorld != null && bWorld.generatedWorldPlan != null && !string.IsNullOrWhiteSpace(bWorld.generatedWorldPlan.worldSeed);
            if (!bOrigin || !bPlan || !system.SaveActiveProfile())
            {
                LogProfileIsolationResult(YQBaselineResultStatus.FAIL, "Profile B did not publish an accepted origin and plan: origin=" + bOrigin + " plan=" + bPlan + " failure=" + system.LastFailure);
                yield break;
            }

            string bPlayerId = bPlayer.playerId;
            string bWorldId = bWorld.worldIdentity != null ? bWorld.worldIdentity.worldId : string.Empty;
            string bSeed = bWorld.generatedWorldPlan.worldSeed;

            // note: Return through the same canonical development title handoff used by the beta fixture, preserving profile B's committed revision for the comparison.
            title.OpenAtStartup();
            yield return new WaitForSecondsRealtime(0.60f);
            if (!title.CompleteCanonicalDevelopmentStartup())
            {
                LogProfileIsolationResult(YQBaselineResultStatus.FAIL, "Canonical profile could not be selected after profile B was saved.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.70f);

            PlayerState aPlayer = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
            WorldState aWorld = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
            string aPlayerId = aPlayer != null ? aPlayer.playerId : string.Empty;
            string aWorldId = aWorld != null && aWorld.worldIdentity != null ? aWorld.worldIdentity.worldId : string.Empty;
            string aSeed = aWorld != null && aWorld.generatedWorldPlan != null ? aWorld.generatedWorldPlan.worldSeed : string.Empty;
            int playerObjects = GameObject.FindGameObjectsWithTag("Player").Length;
            int playerMotors = UnityEngine.Object.FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

            bool isolated = !string.IsNullOrWhiteSpace(bPlayerId) && !string.Equals(bPlayerId, aPlayerId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(bWorldId) && !string.Equals(bWorldId, aWorldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(system.ActiveProfileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(aPlayerId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(aSeed, YQBetaDevelopmentFixture.CanonicalWorldSeed, StringComparison.OrdinalIgnoreCase) &&
                playerObjects == 1 && playerMotors == 1;
            string actual = "A=" + aPlayerId + "/" + aWorldId + "/" + aSeed +
                " B=" + bPlayerId + "/" + bWorldId + "/" + bSeed +
                " active=" + system.ActiveProfileId + " playerObjects=" + playerObjects + " playerMotors=" + playerMotors +
                " loads=" + system.SuccessfulLoadCount + " saves=" + system.SuccessfulSaveCount;
            LogProfileIsolationResult(isolated ? YQBaselineResultStatus.PASS : YQBaselineResultStatus.FAIL, actual);
        }
        finally
        {
            profileIsolationRunning = false;
        }
    }

    // note: Reflection is limited to existing private UI actions so the development verifier cannot become a second public gameplay API.
    private static bool InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target != null ? target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic) : null;
        if (method == null)
            return false;
        method.Invoke(target, arguments);
        return true;
    }

    // note: Read only the existing questionnaire input reference needed to submit one deterministic verification answer.
    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target != null ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic) : null;
        return field != null ? field.GetValue(target) : null;
    }

    // note: Profile isolation keeps its own structured receipt so an unavailable questionnaire cannot be reported as a broader runtime PASS.
    private static void LogProfileIsolationResult(YQBaselineResultStatus status, string actual)
    {
        YQBaselineResultRecord record = new YQBaselineResultRecord
        {
            checkId = "live.profile-isolation",
            category = "profile-isolation",
            status = status,
            evidence = YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            expected = "Two isolated profiles retain distinct player/world identity and accepted origin/plan while one authoritative player remains.",
            actual = actual,
            reproduction = "PlaySafe title flow; run the G02 profile-isolation development check.",
            owner = "G02"
        };
        Debug.Log("[YQProductionBaselineReceipt] " + record.ToLogLine());
    }

    // note: Reload receipts use the same structured baseline record and remain distinct from the broader production regression summary.
    private static void LogProfileReloadResult(YQBaselineResultStatus status, string actual)
    {
        YQBaselineResultRecord record = new YQBaselineResultRecord
        {
            checkId = "live.profile-reload-b",
            category = "profile-reload",
            status = status,
            evidence = YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            expected = "Profile B retains accepted origin/plan after Play Mode restart with one authoritative player.",
            actual = actual,
            reproduction = "PlaySafe title flow; prepare profile B, stop/start Play Mode, then run the G02 reload development check.",
            owner = "G02"
        };
        Debug.Log("[YQProductionBaselineReceipt] " + record.ToLogLine());
    }

    // note: Each fixture returns null on success and a precise reason on failure, preserving the existing test vocabulary.
    private static void Run(string label, Func<string> test, List<string> failures)
    {
        try
        {
            string failure = test != null ? test() : "Test delegate was missing.";
            if (!string.IsNullOrWhiteSpace(failure))
                failures.Add(label + ": " + failure);
        }
        catch (Exception exception)
        {
            failures.Add(label + ": threw " + exception.GetType().Name + ": " + exception.Message);
        }
    }
}
#endif
