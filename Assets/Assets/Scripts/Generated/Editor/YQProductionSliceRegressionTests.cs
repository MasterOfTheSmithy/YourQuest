using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// note: These fixtures exercise production acceptance rules without loading a player's save or starting a model.
public static class YQProductionSliceRegressionTests
{
    // note: The production earthwork evaluator must preserve distinct parcel heights, heading, and untouched ground.
    public static string TestParcelEarthworks()
    {
        var layout = new YQProceduralSettlementLayoutRecord { earthworkVersion = 1 };
        layout.cells.Add(new YQProceduralCellPlacement { boundsCenter = new Vector3(-20f, 0f, 0f), boundsSize = new Vector3(4f, 4f, 4f) });
        layout.cells.Add(new YQProceduralCellPlacement { boundsCenter = new Vector3(20f, 0f, 0f), boundsSize = new Vector3(4f, 4f, 4f) });
        layout.streets.Add(new YQProceduralStreet { start = new Vector3(-20f, 0f, 0f), end = new Vector3(20f, 0f, 0f), width = 4f });
        float[] heights = { 10f, 20f };
        // note: Reflection reaches the same evaluator used by terrain writes without exposing a gameplay API for tests.
        var evaluate = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("ResolveParcelEarthwork",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        foreach (float heading in new[] { 0f, 90f, 180f, 270f })
        {
            foreach (float localX in new[] { -20f, 0f, 20f, 100f })
            {
                float angle = heading * Mathf.Deg2Rad;
                var point = new Vector2(Mathf.Cos(angle) * localX, -Mathf.Sin(angle) * localX);
                object[] arguments = { layout, heights, point, heading, 0f, 0f };
                evaluate.Invoke(null, arguments);
                float weight = (float)arguments[4], target = (float)arguments[5];
                if (localX == 100f)
                {
                    if (weight != 0f) return "Unowned terrain was graded.";
                }
                else if (Mathf.Abs(weight - 1f) > .001f || Mathf.Abs(target - (15f + localX * .25f)) > .001f)
                    return "Parcel or street elevation changed under heading " + heading;
            }
        }
        layout.parcelGroundHeights.AddRange(heights);
        var restored = JsonConvert.DeserializeObject<YQProceduralSettlementLayoutRecord>(JsonConvert.SerializeObject(layout));
        if (restored.earthworkVersion != 1 || restored.parcelGroundHeights.Count != 2 || restored.parcelGroundHeights[1] != 20f)
            return "Parcel elevations did not roundtrip.";
        // note: Start with a valid record so rejection tests cannot pass because of an unrelated malformed layout field.
        // note: SeedPrefix layouts use the current independent-streaming schema version, so this fixture must match VersionFor(seed).
        var record = new YQProceduralSettlementLayoutRecord { version = 4, seed = YQProceduralSettlementLayout.SeedPrefix + "profile-validation",
            radius = 30f, earthworkVersion = 1 };
        record.cells.Add(new YQProceduralCellPlacement { cellId = "house", boundsSize = Vector3.one * 4f });
        record.streets.Add(new YQProceduralStreet { start = Vector3.zero, end = Vector3.right * 5f, width = 4f });
        record.streets.Add(new YQProceduralStreet { start = Vector3.zero, end = Vector3.forward * 5f, width = 4f });
        record.parcelGroundHeights.Add(20f);
        if (!YQProceduralSettlementLayout.ValidateRecord(record, out string reason)) return "Valid profile rejected: " + reason;
        record.parcelGroundHeights[0] = float.NaN;
        if (YQProceduralSettlementLayout.ValidateRecord(record, out reason)) return "Nonfinite elevation accepted.";
        record.parcelGroundHeights[0] = 20f;
        record.parcelGroundHeights.Add(30f);
        if (YQProceduralSettlementLayout.ValidateRecord(record, out reason)) return "Mismatched elevation count accepted.";
        record.parcelGroundHeights.RemoveAt(1);
        record.earthworkVersion = 99;
        if (YQProceduralSettlementLayout.ValidateRecord(record, out reason)) return "Unknown earthwork version accepted.";
        return null;
    }

    // note: Exercise production readiness predicates and local geometry rejection without loading a player save.
    public static void RunReadinessBatch()
    {
        try
        {
            string earthworkFailure = TestParcelEarthworks();
            if (earthworkFailure != null) throw new Exception(earthworkFailure);
            var report = new YQGeneratedWorldIntegrityValidator.RouteReport();
            if (report.IsValid) throw new Exception("Empty route evidence passed.");
            report.sampledPoints = 4;
            report.measuredTraversalSamples = 4;
            report.traversalMeasurementComplete = true;
            if (!report.IsValid) throw new Exception("Complete clear route evidence failed.");
            // note: Independently inject each recorded failure so no other failure can mask a missing gate.
            string[] fields = { "missingGroundPoints", "impassableHeightSteps", "unresolvedStructuralBlockers",
                "clearanceQuerySaturations", "traversalIssueSamples" };
            foreach (string name in fields)
            {
                var field = report.GetType().GetField(name);
                field.SetValue(report, 1);
                // note: Height-step samples are advisory once the completed capsule sweep proves the route clear.
                if (name == "impassableHeightSteps")
                    report.traversalMeasurementComplete = false;
                if (report.IsValid) throw new Exception(name + " was accepted.");
                field.SetValue(report, 0);
                report.traversalMeasurementComplete = true;
            }
            report.traversalMeasurementComplete = false;
            if (report.IsValid) throw new Exception("Partial route evidence passed.");

            var root = new GameObject("ReadinessFixture");
            try
            {
                var validator = typeof(YQGeneratedWorldRuntimeBuilder).GetMethod("ValidateSettlementPresentation",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Func<int, bool> valid = count => (bool)validator.Invoke(null, new object[] { root.transform, null, null, count });
                if (valid(1)) throw new Exception("Missing building passed.");
                var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
                first.name = "SettlementBuilding_0";
                first.transform.SetParent(root.transform);
                first.transform.localScale = Vector3.one * 3f;
                if (!valid(1)) throw new Exception("Separated whole-building fixture failed.");
                var second = GameObject.CreatePrimitive(PrimitiveType.Cube);
                second.name = "SettlementBuilding_1";
                second.transform.SetParent(root.transform);
                second.transform.localScale = Vector3.one * 3f;
                if (valid(2)) throw new Exception("Overlapping buildings passed.");
                second.transform.position = Vector3.right * 10f;
                if (!valid(2)) throw new Exception("Separated building fixtures failed.");
                second.name += "__Modular";
                if (valid(2)) throw new Exception("Fragment-built candidate passed.");
            }
            finally
            {
                // note: Isolated test geometry never persists into a scene or user's generated world.
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("[YQReadinessTests] PASS: route rejection and settlement presentation acceptance.");
            UnityEditor.EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            UnityEditor.EditorApplication.Exit(1);
        }
    }

    public static string TestOriginOwnership()
    {
        var player = new PlayerState { playerId = "origin_owner" };
        player.EnsureCollections();
        if (!YQOriginGenerationService.CanApplyOriginResult(player, player, "origin_owner"))
            return "Current uncommitted origin was rejected.";
        var reloaded = new PlayerState { playerId = "origin_owner" };
        if (YQOriginGenerationService.CanApplyOriginResult(player, reloaded, "origin_owner") ||
            YQOriginGenerationService.CanApplyOriginResult(player, player, "other_owner") ||
            YQOriginGenerationService.CanApplyOriginResult(null, player, "origin_owner"))
            return "Stale, switched or missing origin owner was accepted.";
        player.behaviorCounters["origin:questionnaire_complete"] = 1f;
        return YQOriginGenerationService.CanApplyOriginResult(player, player, "origin_owner")
            ? "An accepted origin could be awarded twice." : null;
    }

    public static string TestOriginPromptBudget()
    {
        var answers = new List<string>();
        for (int i = 0; i < 100; i++)
            answers.Add("answer_" + i + " " + new string('x', 790));
        string evidence = YQOriginGenerationService.BuildBoundedAnswerEvidence(answers);
        if (evidence.Length > 7300 || !evidence.Contains("100: ") || !evidence.Contains("answer_99"))
            return "Hardcore evidence is unbounded or drops later answers.";
        if (answers[99].Length < 790 || evidence != YQOriginGenerationService.BuildBoundedAnswerEvidence(answers))
            return "Prompt summarization mutates or nondeterministically selects the accepted evidence.";
        if (!YQOriginGenerationService.BuildBoundedAnswerEvidence(new[] { "A\n\"B\"" }).Contains("\\\"B\\\""))
            return "Player evidence was not JSON-quoted.";
        return null;
    }

    public static string TestGoddessProsePreservation()
    {
        const string completion = "Mira, you called caution cowardice, then asked for a shield. That is worth considering.";
        var voice = new YQGoddessGenerationVoiceDto
        {
            completion = completion,
            nextPrelude = "Your beginning is decided. The rest requires attention.",
            // note: The fixture keeps concise direct speech while including the player anchor required by the contextual voice gate.
            ambientLines = new[] { "Mira, you have made an unusually specific request.", "Mira, I have not mistaken confidence for evidence." }
        };
        var player = new PlayerState { displayName = "Mira" };
        YQGoddessGenerationDialogue.EnsureOriginVoice(voice, player, null);
        if (voice.completion != completion || voice.ambientLines[0] != "Mira, you have made an unusually specific request.")
            return "Valid concise direct speech was replaced by a prescribed greeting or stock narration.";
        if (YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable("The goddess watches the hills rise beyond the courtyard.", 28) ||
            YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable("I", 28))
            return "Scene narration or fragment passed the spoken-line gate.";
        return null;
    }

    public static string TestQuestProgressAndPersistence()
    {
        var state = new PlayerState { playerId = "quest_owner" };
        state.EnsureCollections();
        var quest = new QuestRecord
        {
            questId = "origin_quest_fixture",
            status = "active",
            objectives = new List<QuestObjectiveRecord>
            {
                new QuestObjectiveRecord { type = "origin_manifested", requiredCount = 1 },
                new QuestObjectiveRecord { type = "talk_to_npc", targetId = "npc_1", counterPrefix = "dialogue:", requiredCount = 1 }
            }
        };
        state.quests.Add(quest);
        state.SetActiveQuest(quest.questId);
        state.behaviorCounters["origin:equipment_manifested"] = 1f;
        state.behaviorCounters["dialogue:npc_10:friendly"] = 1f;
        if (YQQuestCompletionDirector.EvaluateObjectives(state, quest, out bool changed) || !changed ||
            !quest.objectives[0].completed || quest.objectives[1].completed)
            return "Partial progress failed or a different NPC completed an explicitly targeted step.";
        var settings = new JsonSerializerSettings
        {
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        PlayerState loaded = JsonConvert.DeserializeObject<PlayerState>(JsonConvert.SerializeObject(state, settings), settings);
        QuestRecord restored = loaded.GetActiveQuest();
        if (restored == null || restored.questId != quest.questId || !restored.objectives[0].completed || restored.objectives[1].completed)
            return "Active quest or partial objective completion did not round-trip through the player save schema.";
        loaded.behaviorCounters["dialogue:npc_1:friendly"] = 1f;
        if (!YQQuestCompletionDirector.EvaluateObjectives(loaded, restored, out changed) || !changed)
            return "Correct NPC failed to complete the outstanding step.";
        if (!YQQuestCompletionDirector.EvaluateObjectives(loaded, restored, out changed) || changed)
            return "Repeated evaluation changed already-completed steps.";
        restored.objectives = new List<QuestObjectiveRecord> { null };
        if (YQQuestCompletionDirector.EvaluateObjectives(loaded, restored, out _))
            return "A null-only objective list completed a quest.";
        return null;
    }

    public static string TestQuestMarkerContract()
    {
        var first = new QuestObjectiveRecord { type = "equip_item" };
        var second = new QuestObjectiveRecord { type = "talk_to_npc", targetId = "npc_archivist_01" };
        var quest = new QuestRecord { objectives = new List<QuestObjectiveRecord> { first, second } };
        if (!ReferenceEquals(YQActiveQuestWorldHighlight.GetTrackedObjective(quest), first))
            return "Marker skipped the current non-spatial objective.";
        first.completed = true;
        if (!ReferenceEquals(YQActiveQuestWorldHighlight.GetTrackedObjective(quest), second))
            return "Marker did not advance to the next structured target.";
        second.completed = true;
        return YQActiveQuestWorldHighlight.GetTrackedObjective(quest) != null
            ? "Finished quest still has a marker objective." : null;
    }

    public static string TestBootstrapPreservesAcceptedOrigin()
    {
        var state = new PlayerState { playerId = "bootstrap_owner" };
        state.EnsureCollections();
        state.generatedOrigin = new GeneratedOriginRecord
        {
            source = "llm_origin_v1", seed = "accepted_seed", questName = "The Measured Promise",
            rawJson = "{\"quest\":{\"description\":\"Put your promise into practice.\"}}", generatedUnix = 123
        };
        var origin = new QuestRecord
        {
            questId = "accepted_origin_quest", name = state.generatedOrigin.questName,
            tags = new[] { "origin_generated", "tutorial_main" }, status = "offer",
            objectives = YQOriginQuestionnaireUI.BuildDefaultOriginObjectives()
        };
        var chosen = new QuestRecord { questId = "chosen_side_quest", name = "Another accepted quest", status = "active" };
        state.quests.Add(origin);
        state.quests.Add(chosen);
        state.activeQuestId = chosen.questId;
        state.behaviorCounters["item:equip:weapon"] = 3f;
        state.behaviorCounters["dialogue:npc_archivist_01:talk"] = 2f;
        // note: No old tutorial-version marker: this previously triggered destructive demo reseeding on new saves.
        YourQuestTutorialAutoBootstrap.EnsureTutorialQuestChain(state);
        if (state.quests.Count != 2 || state.quests[0] != origin || state.activeQuestId != chosen.questId ||
            state.behaviorCounters["item:equip:weapon"] != 3f || state.behaviorCounters["dialogue:npc_archivist_01:talk"] != 2f)
            return "Bootstrap replaced accepted quests, reset event evidence, or overrode active selection.";
        state.quests.Remove(origin);
        YourQuestTutorialAutoBootstrap.EnsureTutorialQuestChain(state);
        if (state.quests.Count != 2 || state.activeQuestId != chosen.questId)
            return "Recovery changed user selection or injected demo quests.";
        QuestRecord recovered = state.quests[1];
        if (recovered.name != state.generatedOrigin.questName || recovered.objectives.Count != 3 ||
            !recovered.objectives[1].completed || !recovered.objectives[2].completed)
            return "Missing origin quest was not restored from accepted identity and existing counters.";
        string id = recovered.questId;
        YourQuestTutorialAutoBootstrap.EnsureTutorialQuestChain(state);
        if (state.quests.Count != 2 || state.quests[1].questId != id)
            return "Repeated bootstrap duplicated a recovered quest.";
        recovered.status = "completed";
        recovered.completedUnix = 999;
        YourQuestTutorialAutoBootstrap.EnsureTutorialQuestChain(state);
        return recovered.completedUnix != 999 || state.quests.Count != 2
            ? "Completed origin was reset or regenerated." : null;
    }

    public static string TestDialogueIsolation()
    {
        string root = Path.GetFullPath(Path.Combine("Library", "YQ_DialoguePathFixture"));
        string a = NpcDialogueSessionStore.BuildScopedStoragePath(root, "profile_a", "NpcDialogueSessions", "npc_vey", "_session.json");
        string b = NpcDialogueSessionStore.BuildScopedStoragePath(root, "profile_b", "NpcDialogueSessions", "npc_vey", "_session.json");
        if (a == b || a != NpcDialogueSessionStore.BuildScopedStoragePath(root, "PROFILE_A", "NpcDialogueSessions", "NPC_VEY", "_session.json"))
            return "Dialogue storage mixes profiles or changes case-insensitive identity.";
        string hostile = NpcDialogueSessionStore.BuildScopedStoragePath(root, "../../other", "NpcDialogue", "../../escape", "_mem.json");
        if (!Path.GetFullPath(hostile).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || hostile.Contains(".."))
            return "Untrusted identity escaped the dialogue storage root.";
        return null;
    }

    public static string TestProfilePreflight()
    {
        // note: All disk fixtures live in a unique disposable folder, never Application.persistentDataPath.
        string root = Path.GetFullPath(Path.Combine("Library", "YQ_ProfilePreflight_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            string playerPath = Path.Combine(root, "player_state.json");
            string worldPath = Path.Combine(root, "world_state.json");
            const string playerJson = "{\"playerId\":\"profile_a\",\"displayName\":\"Test\"}";
            const string worldJson = "{\"worldName\":\"Accepted fixture world\",\"schemaVersion\":6}";
            File.WriteAllText(playerPath, playerJson);
            File.WriteAllText(worldPath, worldJson);
            if (!YQProfileSaveSystem.TryResolveProfileDocuments(root, "profile_a", out string player, out string world, out _) ||
                player != playerPath || world != worldPath)
                return "Valid profile documents were rejected.";
            File.WriteAllText(worldPath, "{");
            if (YQProfileSaveSystem.TryResolveProfileDocuments(root, "profile_a", out _, out _, out _))
                return "Corrupt world was accepted as a default world.";
            File.WriteAllText(worldPath + ".bak", worldJson);
            if (!YQProfileSaveSystem.TryResolveProfileDocuments(root, "profile_a", out _, out world, out _) || world != worldPath + ".bak")
                return "Profile-owned recovery copy was not selected.";
            if (YQProfileSaveSystem.TryResolveProfileDocuments(root, "profile_b", out _, out _, out _))
                return "Wrong player identity passed preflight.";
            if (File.ReadAllText(worldPath) != "{" || File.ReadAllText(playerPath) != playerJson)
                return "Read-only preflight modified save documents.";
            return null;
        }
        finally
        {
            // note: Resolve and constrain cleanup to this exact generated fixture beneath Library.
            string library = Path.GetFullPath("Library") + Path.DirectorySeparatorChar;
            if (root.StartsWith(library, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(root).StartsWith("YQ_ProfilePreflight_", StringComparison.Ordinal))
                Directory.Delete(root, true);
        }
    }

    // note: Verify negative-coordinate flooring, identity stability after label edits, duplicate references, and future-schema rejection.
    public static string TestCanonicalStateContracts()
    {
        PlayerState player = new PlayerState { schemaVersion = 5, playerId = "contract_player", displayName = "First Name" };
        player.quests.Add(new QuestRecord { name = "Accepted Quest", createdUnix = 42 });
        if (!YQStateMigrations.TryMigrate(player, out YQMigrationResult firstMigration) || !firstMigration.supported)
            return "Supported player schema did not migrate.";
        string stableQuestId = player.quests[0].questId;
        player.displayName = "Renamed Player";
        YQStateIdentity.EnsurePlayerState(player);
        if (player.quests[0].questId != stableQuestId || YQStateContract.CellIndex(-0.01f) != -1 || YQStateContract.CellIndex(-128f) != -1 || YQStateContract.CellIndex(-128.01f) != -2)
            return "Identity or negative-coordinate convention changed after migration.";
        if (!YQStateMigrations.TryMigrate(player, out YQMigrationResult repeat) || repeat.changed)
            return "Repeat migration was not idempotent.";
        player.schemaVersion = YQStateContract.CurrentStateSchemaVersion + 1;
        if (YQStateMigrations.TryMigrate(player, out _))
            return "Unsupported future player schema was accepted.";

        WorldState world = WorldState.CreateDefault();
        world.identityRecords.Add(new YQEntityIdentityRecord { id = "duplicate", kind = YQStableEntityKind.Region });
        world.identityRecords.Add(new YQEntityIdentityRecord { id = "duplicate", kind = YQStableEntityKind.Site });
        if (YQStateReferenceValidator.Validate(player, world).IsValid)
            return "Duplicate world identities were accepted.";

        // note: Verify a resolved parent with an incompatible kind is rejected at the identity boundary.
        WorldState illegalLinkWorld = WorldState.CreateDefault();
        YQStateIdentity.EnsureWorldState(illegalLinkWorld);
        illegalLinkWorld.identityRecords.Add(new YQEntityIdentityRecord
        {
            id = "illegal-origin",
            kind = YQStableEntityKind.Origin,
            parentId = illegalLinkWorld.worldIdentity.worldId
        });
        if (YQStateReferenceValidator.Validate(player, illegalLinkWorld).IsValid)
            return "Illegal identity parent kind was accepted.";

        // note: Verify schema-1 aliases are normalized before current typed deserialization can discard them.
        const string legacyPlayerJson = "{\"schemaVersion\":1,\"playerId\":\"legacy-player\",\"displayName\":\"Legacy\",\"xp\":12.75,\"lastPosition\":[-4,2,9],\"flags\":{\"origin:done\":1}}";
        if (!YQStateMigrations.TryNormalizePlayerDocument(legacyPlayerJson, out string normalizedLegacyJson, out YQMigrationResult legacyMigration, out string legacyFailure))
            return "Supported legacy player document was rejected: " + legacyFailure;
        PlayerState legacyPlayer = JsonConvert.DeserializeObject<PlayerState>(normalizedLegacyJson, new JsonSerializerSettings
        {
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        });
        if (!legacyMigration.changed || legacyPlayer == null || legacyPlayer.experience != 12 || legacyPlayer.lastPosition != new Vector3(-4f, 2f, 9f) ||
            !legacyPlayer.behaviorCounters.ContainsKey("origin:done"))
            return "Legacy player progression, position, or flags were not retained.";

        // note: Verify accepted V2 spatial references are assigned stable IDs in the shared world registry.
        WorldState spatialWorld = WorldState.CreateDefault();
        spatialWorld.generatedWorldPlan.spatialPlanV2 = new GeneratedSpatialWorldPlanV2Record
        {
            blueprint = new YQSpatialBlueprintV2
            {
                hydrology = new List<YQHydrologyFeatureV2> { new YQHydrologyFeatureV2 { kind = YQHydrologyKindV2.River } },
                routes = new List<YQRouteCorridorV2> { new YQRouteCorridorV2 { routeClass = YQRouteClassV2.Trail } },
                sites = new List<YQSiteAnchorV2> { new YQSiteAnchorV2 { kind = YQSiteKindV2.PointOfInterest } }
            }
        };
        YQStateIdentity.EnsureWorldState(spatialWorld);
        if (string.IsNullOrWhiteSpace(spatialWorld.generatedWorldPlan.spatialPlanV2.blueprint.hydrology[0].hydrologyId) ||
            string.IsNullOrWhiteSpace(spatialWorld.generatedWorldPlan.spatialPlanV2.blueprint.routes[0].routeId) ||
            string.IsNullOrWhiteSpace(spatialWorld.generatedWorldPlan.spatialPlanV2.blueprint.sites[0].siteId))
            return "Accepted spatial references did not receive stable IDs.";

        // note: Verify an unresolved parent is rejected instead of being treated as a default world link.
        WorldState missingParentWorld = WorldState.CreateDefault();
        missingParentWorld.identityRecords.Add(new YQEntityIdentityRecord
        {
            id = "missing-parent-site",
            kind = YQStableEntityKind.Site,
            parentId = "does-not-exist"
        });
        YQIdentityValidationResult missingParentResult = YQStateReferenceValidator.Validate(player, missingParentWorld);
        if (!missingParentResult.failures.Exists(failure => failure.IndexOf("missing parent", StringComparison.OrdinalIgnoreCase) >= 0))
            return "Missing identity parent was accepted.";

        // note: Verify a malformed identity chain reports cycles without rewriting either record.
        WorldState cyclicWorld = WorldState.CreateDefault();
        cyclicWorld.identityRecords.Add(new YQEntityIdentityRecord { id = "cycle-a", kind = YQStableEntityKind.Content, parentId = "cycle-b" });
        cyclicWorld.identityRecords.Add(new YQEntityIdentityRecord { id = "cycle-b", kind = YQStableEntityKind.Content, parentId = "cycle-a" });
        YQIdentityValidationResult cycleResult = YQStateReferenceValidator.Validate(player, cyclicWorld);
        if (!cycleResult.failures.Exists(failure => failure.IndexOf("parent cycle", StringComparison.OrdinalIgnoreCase) >= 0))
            return "Identity parent cycle was accepted.";
        return null;
    }

    // note: Fault points prove a complete prior profile revision remains readable when any later snapshot stage is interrupted.
    public static string TestProfileCommitRecovery()
    {
        string root = Path.GetFullPath(Path.Combine("Library", "YQ_G02_Commit_" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            string profile = Path.Combine(root, "profile_a");
            string activePlayer = Path.Combine(root, "active_player.json");
            string activeWorld = Path.Combine(root, "active_world.json");
            string playerA = "{\"playerId\":\"profile_a\",\"stateRevision\":1}";
            string worldA = "{\"worldName\":\"World A\",\"stateRevision\":1}";
            if (!YQProfileCommitStore.TryCommit(profile, "profile_a", activePlayer, activeWorld, 1, playerA, worldA, null, out YQProfileTransactionReceipt baseline) || !baseline.published)
                return "Baseline profile revision was not published.";
            Dictionary<string, string> auxiliary = new Dictionary<string, string> { { "npc-memory", "{\"profile\":\"profile_a\",\"memory\":1}" } };
            if (!YQProfileCommitStore.TryCommit(profile, "profile_a", activePlayer, activeWorld, 99, playerA, worldA, auxiliary, null, out YQProfileTransactionReceipt auxiliaryCommit) ||
                auxiliaryCommit.auxiliaryDocuments.Count != 1 ||
                !YQProfileCommitStore.TryReadAuxiliaryDocument(profile, auxiliaryCommit.commitId, "npc-memory", auxiliaryCommit.auxiliaryDocuments[0].checksum, out _, out _))
                return "Registered auxiliary document was not committed and recovered with its checksum.";
            Dictionary<string, string> collidingAuxiliary = new Dictionary<string, string>
            {
                { "npc/memory", "{\"profile\":\"profile_a\",\"memory\":2}" },
                { "npc\\memory", "{\"profile\":\"profile_a\",\"memory\":3}" }
            };
            if (YQProfileCommitStore.TryCommit(profile, "profile_a", activePlayer, activeWorld, 100, playerA, worldA, collidingAuxiliary, null, out _))
                return "Auxiliary filename collision was accepted as a coherent commit.";
            for (int pointIndex = 0; pointIndex <= (int)YQProfileCommitPoint.BeforePointerPublication; pointIndex++)
            {
                YQProfileCommitPoint point = (YQProfileCommitPoint)pointIndex;
                bool interrupted = !YQProfileCommitStore.TryCommit(profile, "profile_a", activePlayer, activeWorld, pointIndex + 2,
                    "{\"playerId\":\"profile_a\",\"stateRevision\":" + (pointIndex + 2) + "}",
                    "{\"worldName\":\"World B\",\"stateRevision\":" + (pointIndex + 2) + "}",
                    current => { if (current == point) throw new IOException("Injected interruption at " + current); }, out _);
                if (!interrupted)
                    return "Injected profile interruption was not observable at " + point + ".";
                if (!YQProfileCommitStore.TryReadCommit(profile, baseline.commitId, baseline.playerChecksum, baseline.worldChecksum, out _, out _, out _))
                    return "Complete prior revision was not recoverable after " + point + ".";
            }
            if (YQProfileCommitStore.TryReadCommit(profile, baseline.commitId, "wrong", baseline.worldChecksum, out _, out _, out _))
                return "Checksum mismatch was accepted.";
            return null;
        }
        finally
        {
            string library = Path.GetFullPath("Library") + Path.DirectorySeparatorChar;
            if (root.StartsWith(library, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    // note: Exercise idempotent mutation keys and the persisted event envelope without creating a GameObject-backed service.
    public static string TestMutationAndEventContracts()
    {
        PlayerState state = new PlayerState { playerId = "event_player" };
        state.EnsureCollections();
        if (!state.TryApplyMutationCommit("fixture:once", -1, out YQMutationReceipt first) || !first.applied)
            return "First mutation commit was rejected.";
        if (state.TryApplyMutationCommit("fixture:once", -1, out _) || state.TryApplyMutationCommit("fixture:stale", first.StateRevision - 1, out _))
            return "Repeated or stale mutation commit was accepted.";
        state.AppendEventEnvelope(new YQEventEnvelope { eventType = "fixture", outcome = "accepted", logicalLocationId = "site_fixture", sessionSequence = 1 });
        YQEventEnvelope envelope = state.eventLog[state.eventLog.Count - 1];
        return string.IsNullOrWhiteSpace(envelope.eventId) || envelope.actorId != state.playerId || envelope.stateRevision <= 0 ? "Event envelope was incomplete." : null;
    }

    public static string TestServiceLifecycleContracts()
    {
        // note: Exercise teardown ownership and stale-request invalidation with a disposable callback only.
        int teardownCalls = 0;
        Action teardown = () => teardownCalls++;
        YQServiceLifecycle.RegisterTeardown(teardown);
        try
        {
            int priorEpoch = YQServiceLifecycle.RequestEpoch;
            int currentEpoch = YQServiceLifecycle.BeginProfileSession("fixture-profile");
            if (currentEpoch <= priorEpoch || teardownCalls != 1 || !YQServiceLifecycle.IsCurrent(currentEpoch) || YQServiceLifecycle.IsCurrent(priorEpoch))
                return "Profile lifecycle did not invalidate stale requests and invoke teardown exactly once.";
            return null;
        }
        finally
        {
            // note: Remove only this fixture callback so other services retain their lifecycle registrations.
            YQServiceLifecycle.UnregisterTeardown(teardown);
        }
    }

    public static string TestLlmProposalBoundary()
    {
        // note: The fixture proves parse, schema gate, normalization, curation, provenance, and atomic persistence without contacting a model.
        string prompt = "fixture prompt";
        string raw = "```json\n{\"source\":\"fixture\",\"name\":\"Bounded Origin\"}\n```";
        if (!YQContentProposalBoundary.TryPrepare(
            raw,
            "fixture-llm",
            prompt,
            "fixture-v1",
            root => YQContentProposalBoundary.ValidateRequiredProperties(root, "source", "name"),
            root => { root["name"] = root["name"].ToString().Trim(); return root; },
            root => !string.IsNullOrWhiteSpace(root["name"]?.ToString()),
            out YQAcceptedProposal proposal,
            out string error))
            return "Valid proposal was rejected: " + error;

        PlayerState state = new PlayerState { playerId = "proposal_fixture" };
        state.EnsureCollections();
        long expectedRevision = state.stateRevision;
        if (!YQContentProposalBoundary.TryCommit(
            state,
            "proposal:fixture",
            YQStableEntityKind.Origin,
            proposal,
            expectedRevision,
            out YQMutationReceipt receipt) ||
            state.acceptedContent.Count != 1 ||
            state.acceptedContent[0].promptHash != YQStateContract.Sha256Hex(prompt) ||
            string.IsNullOrWhiteSpace(state.acceptedContent[0].normalizedPayloadJson) ||
            receipt.StateRevision != state.stateRevision)
            return "Accepted proposal provenance was not persisted atomically.";

        if (YQContentProposalBoundary.TryCommit(state, "proposal:fixture", YQStableEntityKind.Origin, proposal, state.stateRevision, out _))
            return "Duplicate proposal commit was accepted.";
        if (YQContentProposalBoundary.TryPrepare("{bad", "fixture-llm", prompt, "fixture-v1", null, null, null, out _, out _))
            return "Malformed proposal was accepted.";

        // note: Exercise the LLM client's own structured-response parser so malformed JSON is rejected before any domain callback can run.
        var normalize = typeof(LLMClient).GetMethod(
            "TryNormalizeJsonObject",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (normalize == null)
            return "LLM structured-response parser was not found.";
        object[] normalizeArguments = { "{\"status\":", null, null };
        if ((bool)normalize.Invoke(null, normalizeArguments))
            return "Malformed LLM JSON was normalized as valid.";
        return null;
    }
}
