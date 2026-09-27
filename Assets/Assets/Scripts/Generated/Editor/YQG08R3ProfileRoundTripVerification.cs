#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// note: This named editor workflow proves a disposable ordinary journey survives a real PlaySafe reload without changing any existing profile.
[InitializeOnLoad]
internal static class YQG08R3ProfileRoundTripVerification
{
    private const string ScenePath = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private const string NewJourneyRequest = "Temp/YQ_G08_R3_NEW_JOURNEY.request";
    private const string BeginNewJourneyMarker = "Temp/YQ_G08_R3_BEGIN_NEW_JOURNEY.request";
    private const string RestartContinueMarker = "Temp/YQ_G08_R3_RESTART_CONTINUE.request";
    private const string VerifyContinueMarker = "Temp/YQ_G08_R3_VERIFY_CONTINUE.request";
    private const string OrdinaryContinueMarker = "Temp/YQ_STARTUP_CONTINUE.request";
    private const string BaselinePath = "Logs/G08_R3_ProfileRoundTrip_Prepare.json";
    private const string ReceiptPath = "Logs/G08_R3_ProfileRoundTrip.md";
    private const string QuestionnaireCapturePath = "Logs/G08_R3_Questionnaire_PlayerView.png";
    private const string NewJourneyCapturePath = "Logs/G08_R3_NewJourney_PlayerView.png";
    private const string ContinueCapturePath = "Logs/G08_R3_Continue_PlayerView.png";
    private const float StartupActionDelaySeconds = 0.65f;
    private const float NewJourneyTimeoutSeconds = 1500f;
    private const float ContinueTimeoutSeconds = 1200f;

    private static bool _routineRunning;
    private static SnapshotEvidence _workingEvidence;

    [Serializable]
    private sealed class SnapshotEvidence
    {
        public string utc;
        public string scenePath;
        public string profileId;
        public string profileName;
        public string worldId;
        public string worldSeed;
        public string activeAuthority;
        public string authorityReason;
        public string spatialContentHash;
        public string semanticAuthorityFingerprint;
        public string terrainChecksum;
        public string terrainPlanFingerprint;
        public string auxiliaryDocumentChecksum;
        public string loadedAuxiliaryDocumentChecksum;
        public string profileCommitId;
        public int profileRevision;
        public bool terrainSnapshotPresent;
        public bool terrainSnapshotCommitted;
        public bool terrainSnapshotLoaded;
        public int questionnaireListenerCount;
        public int activeListenerCount;
        public bool gameplayReleased;
        public bool hudVisible;
        public bool inputReady;
        public bool cameraActive;
        public List<string> siteRows = new List<string>();
        public List<string> routeRows = new List<string>();
        public List<string> waterRows = new List<string>();
    }

    static YQG08R3ProfileRoundTripVerification()
    {
        // note: The editor update consumes only explicit one-shot requests and never starts or stops Play Mode without a named verification request.
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("YourQuest/Verification/G08-R3 Profile Terrain Round Trip")]
    private static void RequestProfileTerrainRoundTrip()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(NewJourneyRequest, DateTime.UtcNow.ToString("O"));
        Debug.Log("[G08-R3] Disposable profile terrain round-trip requested.");
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (File.Exists(NewJourneyRequest))
            {
                DeleteMarker(NewJourneyRequest);
                if (!TryEnsurePlaySafeScene(out string sceneFailure))
                {
                    WriteFailure("Preparation was not started because " + sceneFailure);
                    return;
                }

                File.WriteAllText(BeginNewJourneyMarker, DateTime.UtcNow.ToString("O"));
                EditorApplication.isPlaying = true;
                return;
            }

            if (File.Exists(RestartContinueMarker))
            {
                DeleteMarker(RestartContinueMarker);
                if (!TryEnsurePlaySafeScene(out string sceneFailure))
                {
                    WriteFailure("The reload verification could not resume because " + sceneFailure);
                    return;
                }

                File.WriteAllText(VerifyContinueMarker, DateTime.UtcNow.ToString("O"));
                File.WriteAllText(OrdinaryContinueMarker, DateTime.UtcNow.ToString("O"));
                EditorApplication.isPlaying = true;
                return;
            }

            return;
        }

        if (_routineRunning)
            return;

        if (File.Exists(BeginNewJourneyMarker))
        {
            YQProductionBaselineDiagnostics host = YQProductionBaselineDiagnostics.Instance;
            if (host == null)
                return;
            DeleteMarker(BeginNewJourneyMarker);
            _routineRunning = true;
            host.StartCoroutine(CreateDisposableJourneyAndSave(host));
        }
        else if (File.Exists(VerifyContinueMarker))
        {
            YQProductionBaselineDiagnostics host = YQProductionBaselineDiagnostics.Instance;
            if (host == null)
                return;
            DeleteMarker(VerifyContinueMarker);
            _routineRunning = true;
            host.StartCoroutine(VerifyContinueAfterReload(host));
        }
    }

    private static bool TryEnsurePlaySafeScene(out string failure)
    {
        var activeScene = EditorSceneManager.GetActiveScene();
        if (string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
        {
            failure = string.Empty;
            return true;
        }

        // note: Load PlaySafe only over Unity's clean untitled scene; never replace another scene or discard unsaved editor work.
        if (!string.IsNullOrWhiteSpace(activeScene.path) || activeScene.isDirty)
        {
            failure = "the active scene is " + (string.IsNullOrWhiteSpace(activeScene.path) ? "untitled and has unsaved changes" : activeScene.path) +
                "; switch to the production PlaySafe scene and retry.";
            return false;
        }
        if (!File.Exists(ScenePath))
        {
            failure = "the production PlaySafe scene asset is missing at " + ScenePath + ".";
            return false;
        }

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        catch (Exception exception)
        {
            failure = "the production PlaySafe scene could not be opened: " + exception.Message;
            return false;
        }

        activeScene = EditorSceneManager.GetActiveScene();
        failure = string.Empty;
        return string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerator CreateDisposableJourneyAndSave(MonoBehaviour host)
    {
        bool restoreOriginLlm = false;
        bool originLlmChanged = false;
        try
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            YQTitleScreenUI title = null;
            YQProfileSaveSystem profiles = null;
            while (Time.realtimeSinceStartup < deadline && (title == null || profiles == null))
            {
                title = YQTitleScreenUI.Instance;
                profiles = YQProfileSaveSystem.Instance;
                yield return null;
            }

            if (title == null || profiles == null)
            {
                WriteFailure("PlaySafe did not expose the title and profile owners within 45 seconds.");
                yield break;
            }

            YQProfileSaveSystem.ProfileEntry activeEntry = profiles.FindProfile(profiles.ActiveProfileId);
            bool resumeDisposable = activeEntry != null &&
                !string.IsNullOrWhiteSpace(activeEntry.displayName) &&
                activeEntry.displayName.StartsWith("G08-R3 Disposable ", StringComparison.Ordinal);
            title.OpenAtStartup();
            yield return new WaitForSecondsRealtime(StartupActionDelaySeconds);

            string profileId;
            if (resumeDisposable)
            {
                profileId = activeEntry.profileId;
                if (!title.ContinueSelectedForDevelopmentVerification())
                {
                    WriteFailure("The existing title Continue handler could not resume the named disposable profile.",
                        new SnapshotEvidence { profileId = profileId, profileName = activeEntry.displayName });
                    yield break;
                }
            }
            else
            {
                string displayName = "G08-R3 Disposable " + DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
                if (!SetPrivateField(title, "_newName", displayName) || !InvokePrivate(title, "CreateCharacter"))
                {
                    WriteFailure("The existing title New Journey handler could not be invoked.");
                    yield break;
                }
                profileId = profiles.ActiveProfileId;
            }

            if (string.IsNullOrWhiteSpace(profileId) ||
                string.Equals(profileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase))
            {
                WriteFailure("New Journey did not activate a valid disposable profile; active profile=" + profileId + ".");
                yield break;
            }
            _workingEvidence = new SnapshotEvidence
            {
                profileId = profileId,
                profileName = profiles.FindProfile(profileId)?.displayName
            };

            deadline = Time.realtimeSinceStartup + 90f;
            YQOriginQuestionnaireUI questionnaire = null;
            while (Time.realtimeSinceStartup < deadline &&
                   (questionnaire == null || !YQTitleScreenUI.CanOpenOriginQuestionnaire))
            {
                questionnaire = UnityEngine.Object.FindFirstObjectByType<YQOriginQuestionnaireUI>();
                yield return null;
            }

            if (questionnaire == null || !YQTitleScreenUI.CanOpenOriginQuestionnaire ||
                !questionnaire.OpenIfNeededAfterTitle())
            {
                WriteFailure("The disposable New Journey did not reach the ordinary Goddess questionnaire boundary.");
                yield break;
            }

            yield return null;
            Directory.CreateDirectory("Logs");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(QuestionnaireCapturePath));
            int questionnaireListeners = ActiveListenerCount();
            if (!InvokePrivate(questionnaire, "BeginQuestions", "Casual", 10))
            {
                WriteFailure("The ordinary questionnaire could not begin its Casual question flow.");
                yield break;
            }

            object input = GetPrivateField(questionnaire, "_input");
            PropertyInfo inputText = input != null ? input.GetType().GetProperty("text") : null;
            if (inputText == null)
            {
                WriteFailure("The ordinary questionnaire text property was unavailable.");
                yield break;
            }

            YQOriginGenerationService origin = YQOriginGenerationService.Instance;
            if (origin != null)
            {
                restoreOriginLlm = origin.enableLlmOriginGeneration;
                origin.enableLlmOriginGeneration = false;
                originLlmChanged = true;
            }

            for (int answerIndex = 0; answerIndex < 10; answerIndex++)
            {
                inputText.SetValue(input, "I keep people safe, build a lasting home, and follow the old road with care.");
                if (!InvokePrivate(questionnaire, "SubmitAnswer"))
                {
                    WriteFailure("The ordinary questionnaire rejected its scripted answer at step " + (answerIndex + 1) + ".");
                    yield break;
                }
                if (answerIndex < 9)
                    yield return null;
            }

            deadline = Time.realtimeSinceStartup + NewJourneyTimeoutSeconds;
            while (!questionnaire.StartupPhaseResolved && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!questionnaire.StartupPhaseResolved)
            {
                WriteFailure("The new profile origin/world generation did not resolve within the verification window.");
                yield break;
            }

            if (originLlmChanged && origin != null)
            {
                origin.enableLlmOriginGeneration = restoreOriginLlm;
                originLlmChanged = false;
            }

            deadline = Time.realtimeSinceStartup + NewJourneyTimeoutSeconds;
            YQGeneratedWorldRuntimeBuilder builder = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                builder = YQGeneratedWorldRuntimeBuilder.Instance;
                if (YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
                    builder != null && builder.HasMaterializedCurrentWorld && !builder.IsMaterializationInProgress &&
                    !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
                    break;
                yield return null;
            }

            if (builder == null || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
                !builder.HasMaterializedCurrentWorld || builder.IsMaterializationInProgress)
            {
                WriteFailure("The disposable world did not reach the materialized, released gameplay boundary.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(NewJourneyCapturePath));
            SnapshotEvidence evidence = CaptureEvidence(profiles, builder, ActiveListenerCount());
            evidence.questionnaireListenerCount = questionnaireListeners;
            string terrainJson = GetPrivateField(builder, "_profileTerrainSnapshotJson") as string;
            YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord terrain = ParseTerrain(terrainJson);
            evidence.terrainSnapshotPresent = IsValidOwnedTerrain(terrain, evidence);

            if (!profiles.SaveActiveProfile())
            {
                WriteFailure("The disposable profile save failed: " + profiles.LastFailure);
                yield break;
            }

            evidence.profileCommitId = profiles.ActiveCommitId;
            evidence.profileRevision = profiles.ActiveRevision;
            _workingEvidence = evidence;
            evidence.terrainSnapshotCommitted = HasCommittedTerrainDocument(profiles.LastTransactionReceipt, out string auxiliaryChecksum);
            evidence.auxiliaryDocumentChecksum = auxiliaryChecksum;
            if (!evidence.terrainSnapshotPresent || !evidence.terrainSnapshotCommitted)
            {
                WriteFailure("The generated terrain snapshot was not published with the active profile revision.", evidence);
                yield break;
            }

            WriteBaseline(evidence);
            File.WriteAllText(RestartContinueMarker, DateTime.UtcNow.ToString("O"));
            _workingEvidence = null;
            _routineRunning = false;
            EditorApplication.isPlaying = false;
        }
        finally
        {
            _workingEvidence = null;
            if (originLlmChanged && YQOriginGenerationService.Instance != null)
                YQOriginGenerationService.Instance.enableLlmOriginGeneration = restoreOriginLlm;
            if (EditorApplication.isPlaying && _routineRunning &&
                !File.Exists(RestartContinueMarker))
            {
                // note: Failed preparation exits Play Mode so the next session cannot accidentally continue a partial test journey.
                _routineRunning = false;
                EditorApplication.isPlaying = false;
            }
        }
    }

    private static IEnumerator VerifyContinueAfterReload(MonoBehaviour host)
    {
        SnapshotEvidence before = null;
        try
        {
            if (File.Exists(BaselinePath))
            {
                before = JsonUtility.FromJson<SnapshotEvidence>(File.ReadAllText(BaselinePath));
            }
            if (before == null || string.IsNullOrWhiteSpace(before.profileId))
            {
                WriteFailure("The disposable New Journey baseline receipt was missing or unreadable.");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + ContinueTimeoutSeconds;
            YQProfileSaveSystem profiles = null;
            YQTitleScreenUI title = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                profiles = YQProfileSaveSystem.Instance;
                title = YQTitleScreenUI.Instance;
                if (profiles != null && title != null &&
                    YQTitleScreenUI.StartupFlowComplete &&
                    string.Equals(profiles.ActiveProfileId, before.profileId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(profiles.LastLoadedProfileId, before.profileId, StringComparison.OrdinalIgnoreCase))
                    break;
                yield return null;
            }

            if (profiles == null || title == null ||
                !string.Equals(profiles.ActiveProfileId, before.profileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(profiles.LastLoadedProfileId, before.profileId, StringComparison.OrdinalIgnoreCase))
            {
                WriteFailure("Ordinary Continue did not load the named disposable profile within the verification window.", before);
                yield break;
            }

            deadline = Time.realtimeSinceStartup + ContinueTimeoutSeconds;
            YQGeneratedWorldRuntimeBuilder builder = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                builder = YQGeneratedWorldRuntimeBuilder.Instance;
                if (YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
                    builder != null && builder.HasMaterializedCurrentWorld && !builder.IsMaterializationInProgress &&
                    !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
                    break;
                yield return null;
            }

            if (builder == null || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
                !builder.HasMaterializedCurrentWorld || builder.IsMaterializationInProgress)
            {
                WriteFailure("Continue did not reach the materialized, released gameplay boundary.", before);
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(ContinueCapturePath));
            SnapshotEvidence after = CaptureEvidence(profiles, builder, ActiveListenerCount());
            string loadedTerrainJson = string.Empty;
            profiles.TryGetLoadedAuxiliaryDocument(YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId, out loadedTerrainJson);
            YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord terrain = ParseTerrain(loadedTerrainJson);
            after.terrainSnapshotPresent = IsValidOwnedTerrain(terrain, after);
            after.terrainChecksum = terrain != null ? terrain.heightmapChecksum : string.Empty;
            after.terrainPlanFingerprint = terrain != null ? terrain.planFingerprint : string.Empty;
            after.terrainSnapshotLoaded = after.terrainSnapshotPresent;
            after.loadedAuxiliaryDocumentChecksum = YQStateContract.Sha256Hex(loadedTerrainJson);
            after.terrainSnapshotCommitted = after.terrainSnapshotLoaded &&
                !string.IsNullOrWhiteSpace(before.auxiliaryDocumentChecksum) &&
                string.Equals(before.auxiliaryDocumentChecksum,
                    after.loadedAuxiliaryDocumentChecksum, StringComparison.OrdinalIgnoreCase);
            after.auxiliaryDocumentChecksum = before.auxiliaryDocumentChecksum;
            bool identityMatches = string.Equals(before.profileId, after.profileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(before.worldId, after.worldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(before.worldSeed, after.worldSeed, StringComparison.Ordinal) &&
                string.Equals(before.spatialContentHash, after.spatialContentHash, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(before.semanticAuthorityFingerprint, after.semanticAuthorityFingerprint, StringComparison.OrdinalIgnoreCase);
            bool terrainMatches = after.terrainSnapshotPresent && builder.LastProfileTerrainRestoreSucceeded &&
                string.Equals(before.terrainChecksum, after.terrainChecksum, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(before.terrainPlanFingerprint, after.terrainPlanFingerprint, StringComparison.OrdinalIgnoreCase);
            bool graphMatches = string.Equals(Signature(before.siteRows, before.routeRows, before.waterRows),
                Signature(after.siteRows, after.routeRows, after.waterRows), StringComparison.OrdinalIgnoreCase);
            bool presentationReady = after.gameplayReleased && after.hudVisible && after.inputReady &&
                after.cameraActive && after.activeListenerCount == 1;
            WriteFinalReport(before, after, builder, identityMatches, terrainMatches, graphMatches, presentationReady);
            _routineRunning = false;
            EditorApplication.isPlaying = false;
        }
        finally
        {
            if (EditorApplication.isPlaying && _routineRunning)
            {
                _routineRunning = false;
                EditorApplication.isPlaying = false;
            }
        }
    }

    private static SnapshotEvidence CaptureEvidence(
        YQProfileSaveSystem profiles,
        YQGeneratedWorldRuntimeBuilder builder,
        int listenerCount)
    {
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        GeneratedWorldPlanRecord plan = world != null ? world.generatedWorldPlan : null;
        PlayerState player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        SnapshotEvidence evidence = new SnapshotEvidence
        {
            utc = DateTime.UtcNow.ToString("O"),
            scenePath = ScenePath,
            profileId = profiles != null ? profiles.ActiveProfileId : string.Empty,
            profileName = profiles != null ? profiles.FindProfile(profiles.ActiveProfileId)?.displayName : string.Empty,
            profileCommitId = profiles != null ? profiles.ActiveCommitId : string.Empty,
            profileRevision = profiles != null ? profiles.ActiveRevision : 0,
            worldId = world != null && world.worldIdentity != null ? world.worldIdentity.worldId : string.Empty,
            worldSeed = plan != null ? plan.worldSeed : string.Empty,
            activeAuthority = string.Empty,
            authorityReason = string.Empty,
            spatialContentHash = plan != null && plan.spatialPlanV2 != null ? plan.spatialPlanV2.contentHash : string.Empty,
            semanticAuthorityFingerprint = plan != null && plan.semanticAuthority != null ? plan.semanticAuthority.sourceSpatialFingerprint : string.Empty,
            activeListenerCount = listenerCount,
            gameplayReleased = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
            inputReady = YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.CanProcessMovementInput,
            cameraActive = YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.playerCamera != null &&
                YQInvestorPlayerMotor.ActiveMotor.playerCamera.isActiveAndEnabled
        };
        YourQuestTutorialHud hud = UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>();
        evidence.hudVisible = hud != null && hud.IsPresentationVisible;

        if (plan != null && YQSpatialPlanVersionRouter.TryResolveActive(plan, out YQSpatialPlanAuthority authority, out string reason))
        {
            evidence.activeAuthority = authority.ToString();
            evidence.authorityReason = reason;
        }
        else if (plan != null)
        {
            YQSpatialPlanVersionRouter.TryResolveActive(plan, out YQSpatialPlanAuthority rejectedAuthority, out string rejectionReason);
            evidence.activeAuthority = rejectedAuthority.ToString();
            evidence.authorityReason = rejectionReason;
        }

        if (plan != null && plan.semanticAuthority != null)
        {
            if (plan.semanticAuthority.siteReservations != null)
            {
                for (int index = 0; index < plan.semanticAuthority.siteReservations.Count; index++)
                {
                    GeneratedSemanticSiteReservationRecord site = plan.semanticAuthority.siteReservations[index];
                    if (site == null) continue;
                    string members = site.memberCellIds != null ? string.Join(",", site.memberCellIds) : string.Empty;
                    string routes = site.routeIds != null ? string.Join(",", site.routeIds) : string.Empty;
                    string entrances = string.Empty;
                    if (site.entrances != null)
                    {
                        List<string> entranceRows = new List<string>();
                        for (int entranceIndex = 0; entranceIndex < site.entrances.Count; entranceIndex++)
                        {
                            GeneratedSemanticEntranceRecord entrance = site.entrances[entranceIndex];
                            if (entrance != null)
                                entranceRows.Add(entrance.entranceId + "@" + F(entrance.worldX) + "," + F(entrance.worldZ) + ">" + entrance.permittedRouteId);
                        }
                        entrances = string.Join(",", entranceRows);
                    }
                    evidence.siteRows.Add(site.siteId + "|" + site.siteKind + "|" + F(site.worldX) + "," + F(site.worldZ) +
                        "|owner=" + site.ownerCellId + "|members=" + members + "|entrances=" + entrances +
                        "|routes=" + routes + "|accepted=" + site.accepted + "|layout=" + site.layoutBindingId +
                        "|runtimeRoot=" + YQCompiledWorldSiteInstance.HasSite(site.siteId) +
                        "|loaded=" + YQCompiledWorldSiteInstance.IsSiteLoaded(site.siteId));
                }
            }
            if (plan.semanticAuthority.routeGraph != null)
            {
                for (int index = 0; index < plan.semanticAuthority.routeGraph.Count; index++)
                {
                    GeneratedSemanticRouteGraphRecord route = plan.semanticAuthority.routeGraph[index];
                    if (route != null)
                        evidence.routeRows.Add(route.routeId + "|" + route.fromSiteId + ">" + route.toSiteId +
                            "|class=" + route.routeClass + "|accepted=" + route.accepted + "|points=" +
                            (route.points != null ? route.points.Count.ToString() : "0") + "|crossings=" +
                            (route.crossings != null ? route.crossings.Count.ToString() : "0"));
                }
            }
            if (plan.semanticAuthority.waterNetworks != null)
            {
                for (int index = 0; index < plan.semanticAuthority.waterNetworks.Count; index++)
                {
                    GeneratedSemanticWaterNetworkRecord water = plan.semanticAuthority.waterNetworks[index];
                    if (water != null)
                        evidence.waterRows.Add(water.waterId + "|kind=" + water.kind + "|source=" + water.sourceId +
                            "|downstream=" + water.downstreamWaterId + "|sink=" + water.sinkId +
                            "|accepted=" + water.accepted + "|points=" +
                            (water.points != null ? water.points.Count.ToString() : "0") + "|crossings=" +
                            (water.crossings != null ? water.crossings.Count.ToString() : "0"));
                }
            }
        }

        evidence.siteRows.Sort(StringComparer.Ordinal);
        evidence.routeRows.Sort(StringComparer.Ordinal);
        evidence.waterRows.Sort(StringComparer.Ordinal);
        return evidence;
    }

    private static bool IsValidOwnedTerrain(
        YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord terrain,
        SnapshotEvidence evidence)
    {
        if (terrain == null)
            return false;
        evidence.terrainChecksum = terrain.heightmapChecksum ?? string.Empty;
        evidence.terrainPlanFingerprint = terrain.planFingerprint ?? string.Empty;
        return terrain.schemaVersion == 1 && terrain.hasTerrain &&
            string.Equals(terrain.ownerProfileId, evidence.profileId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(terrain.worldId, evidence.worldId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(terrain.worldSeed, evidence.worldSeed, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(terrain.heightmapChecksum) &&
            !string.IsNullOrWhiteSpace(terrain.planFingerprint);
    }

    private static bool HasCommittedTerrainDocument(YQProfileTransactionReceipt receipt, out string checksum)
    {
        checksum = string.Empty;
        if (receipt == null || receipt.auxiliaryDocuments == null)
            return false;
        for (int index = 0; index < receipt.auxiliaryDocuments.Count; index++)
        {
            YQProfileAuxiliaryDocumentRecord document = receipt.auxiliaryDocuments[index];
            if (document == null || !string.Equals(document.documentId,
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId, StringComparison.OrdinalIgnoreCase))
                continue;
            checksum = document.checksum ?? string.Empty;
            return !string.IsNullOrWhiteSpace(checksum);
        }
        return false;
    }

    private static YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord ParseTerrain(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(json); }
        catch (ArgumentException) { return null; }
    }

    private static int ActiveListenerCount()
    {
        int count = 0;
        AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int index = 0; index < listeners.Length; index++)
            if (listeners[index] != null && listeners[index].isActiveAndEnabled)
                count++;
        return count;
    }

    private static bool InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target != null
            ? target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            : null;
        if (method == null) return false;
        method.Invoke(target, arguments);
        return true;
    }

    private static bool SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target != null
            ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            : null;
        if (field == null) return false;
        field.SetValue(target, value);
        return true;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target != null
            ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            : null;
        return field != null ? field.GetValue(target) : null;
    }

    private static string Signature(List<string> sites, List<string> routes, List<string> waters)
    {
        StringBuilder value = new StringBuilder();
        if (sites != null)
        {
            for (int index = 0; index < sites.Count; index++)
                value.Append("site|").Append(StripRuntimeResidency(sites[index])).Append('\n');
        }
        AppendRows(value, "route", routes);
        AppendRows(value, "water", waters);
        return YQStateContract.Sha256Hex(value.ToString());
    }

    private static string StripRuntimeResidency(string row)
    {
        const string runtimeResidencyMarker = "|runtimeRoot=";
        int markerIndex = row != null ? row.IndexOf(runtimeResidencyMarker, StringComparison.Ordinal) : -1;
        return markerIndex >= 0 ? row.Substring(0, markerIndex) : row ?? string.Empty;
    }

    private static void AppendRows(StringBuilder target, string label, List<string> rows)
    {
        if (rows == null) return;
        for (int index = 0; index < rows.Count; index++)
            target.Append(label).Append('|').Append(rows[index]).Append('\n');
    }

    private static string F(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void WriteBaseline(SnapshotEvidence evidence)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(BaselinePath, JsonUtility.ToJson(evidence, true));
        Debug.Log("[G08-R3] Disposable New Journey snapshot published profile=" + evidence.profileId +
                  " world=" + evidence.worldId + " seed=" + evidence.worldSeed +
                  " terrain=" + evidence.terrainChecksum + " sites=" + evidence.siteRows.Count +
                  " routes=" + evidence.routeRows.Count + " water=" + evidence.waterRows.Count);
    }

    private static void WriteFinalReport(
        SnapshotEvidence before,
        SnapshotEvidence after,
        YQGeneratedWorldRuntimeBuilder builder,
        bool identityMatches,
        bool terrainMatches,
        bool graphMatches,
        bool presentationReady)
    {
        bool pass = identityMatches && terrainMatches && graphMatches && presentationReady &&
            after.terrainSnapshotCommitted && before.activeListenerCount == 1;
        StringBuilder report = new StringBuilder();
        report.AppendLine("# G08-R3 disposable profile terrain round trip");
        report.AppendLine();
        report.AppendLine("Result: **" + (pass ? "PASS" : "FAIL") + "**");
        report.AppendLine("Evidence level: PlaySafe runtime, fresh named profile, real title New Journey and Continue handoffs across a Play Mode restart.");
        report.AppendLine("Captured UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Scene: " + after.scenePath);
        report.AppendLine("Profile: " + after.profileName + " (`" + after.profileId + "`)");
        report.AppendLine();
        report.AppendLine("| Check | Result | Evidence |");
        report.AppendLine("|---|---|---|");
        report.AppendLine("| Profile/world/seed identity | " + Status(identityMatches) + " | world=" + after.worldId + ", seed=" + after.worldSeed + " |");
        report.AppendLine("| Accepted spatial authority | " + after.activeAuthority + " | hash=" + after.spatialContentHash + " |");
        report.AppendLine("| Terrain loaded from paired profile commit | " + Status(after.terrainSnapshotLoaded && after.terrainSnapshotCommitted) + " | revision=" + after.profileRevision + ", committed checksum=" + before.auxiliaryDocumentChecksum + ", loaded checksum=" + after.loadedAuxiliaryDocumentChecksum + " |");
        report.AppendLine("| Terrain restored after Play Mode restart | " + Status(terrainMatches) + " | checksum=" + after.terrainChecksum + ", restore=" + builder.LastProfileTerrainRestoreSucceeded + ", reason=" + builder.LastProfileTerrainRestoreFailureReason + " |");
        report.AppendLine("| Accepted feature graph stable | " + Status(graphMatches) + " | sites=" + after.siteRows.Count + ", routes=" + after.routeRows.Count + ", water networks=" + after.waterRows.Count + " |");
        report.AppendLine("| New Journey questionnaire listener | " + Status(before.questionnaireListenerCount == 1) + " | active listeners=" + before.questionnaireListenerCount + " |");
        report.AppendLine("| Continue gameplay presentation | " + Status(presentationReady) + " | released=" + after.gameplayReleased + ", HUD=" + after.hudVisible + ", input=" + after.inputReady + ", camera=" + after.cameraActive + ", listeners=" + after.activeListenerCount + " |");
        report.AppendLine();
        report.AppendLine("## Player-view captures");
        report.AppendLine();
        report.AppendLine("- Questionnaire: `" + QuestionnaireCapturePath + "`");
        report.AppendLine("- New Journey: `" + NewJourneyCapturePath + "`");
        report.AppendLine("- Continue after restart: `" + ContinueCapturePath + "`");
        report.AppendLine();
        report.AppendLine("## Persisted feature IDs");
        report.AppendLine();
        report.AppendLine("The rows below are read from the active persisted semantic authority after ordinary Continue. `owner` and `members` are the accepted cell ownership contract; `runtimeRoot`/`loaded` show this session's physical registration state.");
        report.AppendLine();
        report.AppendLine("### Sites");
        report.AppendLine();
        for (int index = 0; index < after.siteRows.Count; index++) report.AppendLine("- " + after.siteRows[index]);
        report.AppendLine();
        report.AppendLine("### Routes");
        report.AppendLine();
        for (int index = 0; index < after.routeRows.Count; index++) report.AppendLine("- " + after.routeRows[index]);
        report.AppendLine();
        report.AppendLine("### Water networks");
        report.AppendLine();
        for (int index = 0; index < after.waterRows.Count; index++) report.AppendLine("- " + after.waterRows[index]);
        report.AppendLine();
        report.AppendLine("## Before/after replay signatures");
        report.AppendLine();
        report.AppendLine("- Profile commit: " + before.profileCommitId + " (revision " + before.profileRevision + ") → " + after.profileCommitId + " (revision " + after.profileRevision + ").");
        report.AppendLine("- Spatial hash: " + before.spatialContentHash + " → " + after.spatialContentHash + ".");
        report.AppendLine("- Semantic fingerprint: " + before.semanticAuthorityFingerprint + " → " + after.semanticAuthorityFingerprint + ".");
        report.AppendLine("- Terrain checksum: " + before.terrainChecksum + " → " + after.terrainChecksum + ".");
        report.AppendLine();
        report.AppendLine("This receipt certifies profile persistence and startup presentation only. Site traversal, hydrology appearance, ecology, performance, unload/revisit and the complete G08 physical itinerary remain separate acceptance rows until captured in ordinary player-view movement.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReceiptPath, report.ToString());
        Debug.Log("[G08-R3] Profile round-trip " + (pass ? "PASS" : "FAIL") + " report written to " + ReceiptPath);
    }

    private static void WriteFailure(string reason, SnapshotEvidence evidence = null)
    {
        Directory.CreateDirectory("Logs");
        StringBuilder report = new StringBuilder();
        report.AppendLine("# G08-R3 disposable profile terrain round trip");
        report.AppendLine();
        report.AppendLine("Result: **FAIL / INCOMPLETE**");
        report.AppendLine("Captured UTC: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("Reason: " + reason);
        if (evidence != null)
        {
            report.AppendLine("Profile: " + evidence.profileId + " (`" + evidence.profileName + "`)");
            report.AppendLine("World: " + evidence.worldId + " seed=" + evidence.worldSeed);
            report.AppendLine("Terrain: present=" + evidence.terrainSnapshotPresent + " checksum=" + evidence.terrainChecksum +
                " committed=" + evidence.terrainSnapshotCommitted + " auxiliary=" + evidence.auxiliaryDocumentChecksum);
            report.AppendLine("Sites/routes/water: " + evidence.siteRows.Count + "/" + evidence.routeRows.Count + "/" + evidence.waterRows.Count);
        }
        else if (_workingEvidence != null)
        {
            report.AppendLine("Profile: " + _workingEvidence.profileId + " (`" + _workingEvidence.profileName + "`)");
        }
        File.WriteAllText(ReceiptPath, report.ToString());
        DeleteMarker(BeginNewJourneyMarker);
        DeleteMarker(VerifyContinueMarker);
        DeleteMarker(RestartContinueMarker);
        DeleteMarker(OrdinaryContinueMarker);
        _workingEvidence = null;
        Debug.LogError("[G08-R3] " + reason + " Report written to " + ReceiptPath);
    }

    private static string Status(bool value)
    {
        return value ? "PASS" : "FAIL";
    }

    private static void DeleteMarker(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
#endif
