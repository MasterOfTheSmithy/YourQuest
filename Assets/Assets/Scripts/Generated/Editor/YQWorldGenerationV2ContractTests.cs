using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class YQWorldGenerationV2ContractTests
{
    private const string PendingRequestPath =
        "Assets/Assets/EditorBuildRequests/RunWorldGenerationV2ContractTests.request";

    [InitializeOnLoadMethod]
    private static void QueueRequestedRun()
    {
        if (!File.Exists(Path.GetFullPath(PendingRequestPath)))
            return;

        // note: A file request runs this pure contract suite in headless Unity without taking control of the editor desktop.
        EditorApplication.delayCall += RunRequestedTests;
    }

    private static void RunRequestedTests()
    {
        AssetDatabase.DeleteAsset(PendingRequestPath);
        RunFromMenu();
    }

    [MenuItem("YourQuest/AAA World Generation/Run V2 Contract Tests")]
    public static void RunFromMenu()
    {
        int failures = RunTests(out int tested);
        Debug.Log(
            "[YQWorldGenV2Tests] Tested " + tested +
            " V2 planning and asset-intelligence contracts; failures=" +
            failures + ".");
    }

    public static void RunBatch()
    {
        int failures = RunTests(out int tested);
        Debug.Log(
            "[YQWorldGenV2Tests] Tested " + tested +
            " V2 planning and asset-intelligence contracts; failures=" +
            failures + ".");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    public static int RunTests(out int tested)
    {
        int failures = 0;
        tested = 0;

        // note: Production regressions cover onboarding, persistence and presentation before world geometry contracts.
        failures += Run("origin callback ownership and duplicate acceptance", YQProductionSliceRegressionTests.TestOriginOwnership, ref tested);
        failures += Run("bounded questionnaire evidence", YQProductionSliceRegressionTests.TestOriginPromptBudget, ref tested);
        failures += Run("Goddess concise generated prose preservation", YQProductionSliceRegressionTests.TestGoddessProsePreservation, ref tested);
        failures += Run("partial quest progress, target IDs and save round-trip", YQProductionSliceRegressionTests.TestQuestProgressAndPersistence, ref tested);
        failures += Run("quest marker follows structured current objective", YQProductionSliceRegressionTests.TestQuestMarkerContract, ref tested);
        failures += Run("real bootstrap preserves accepted quests and restores missing origin", YQProductionSliceRegressionTests.TestBootstrapPreservesAcceptedOrigin, ref tested);
        failures += Run("profile-owned safe dialogue paths", YQProductionSliceRegressionTests.TestDialogueIsolation, ref tested);
        failures += Run("read-only profile corruption preflight and recovery", YQProductionSliceRegressionTests.TestProfilePreflight, ref tested);

        // note: Each test uses synthetic reviewed records, keeping this foundational suite independent of scenes and third-party prefab imports.
        failures += Run(
            "V1/V2 authority boundary",
            TestVersionBoundary,
            ref tested);
        failures += Run(
            "asset hard constraints",
            TestHardConstraints,
            ref tested);
        failures += Run(
            "ordering-independent deterministic selection",
            TestDeterministicSelection,
            ref tested);
        failures += Run(
            "legacy weighted picker survives catalog reorder",
            TestLegacyWeightedPickerOrdering,
            ref tested);
        failures += Run(
            "genre-neutral functional binding",
            TestGenreNeutralBinding,
            ref tested);

        failures += YQSpatialBlueprintV2Tests.RunAll(
            out int blueprintTests);
        tested += blueprintTests;
        // note: Storage retries and activation must preserve exact collision and consumption ownership.
        failures += Run("reviewed storage preserves authored collision and instance ownership", YQCellLootBindingTestsV2.TestBinding, ref tested);
        failures += Run("reviewed storage rejects stale geometry, drafts and invalid access", YQCellLootBindingTestsV2.TestRejections, ref tested);
        failures += Run("storage functions require a matching reviewed runtime provider", YQCellLootBindingTestsV2.TestFunctionEvidence, ref tested);

        // note: Collision-only fixtures verify that doorway evidence rejects absent floors and obstructing walls without entering Play Mode.
        failures += Run("isolated doorway support and obstruction evidence",
            YQCellPassageReviewV2.TestCollisionEvidence, ref tested);
        // note: Door clearance includes the complete bounded swing envelope, not just an empty passage with its leaf disabled.
        failures += Run("door swing clearance, direction and source preservation",
            YQCellPassageReviewV2.TestDoorSwingEvidence, ref tested);
        // note: Full-room evidence must reject unsupported footprints and standing areas disconnected by between-sample obstacles.
        failures += Run("bounded room floor coverage and continuous connectivity",
            YQCellPassageReviewV2.TestRoomFloorEvidence, ref tested);
        // note: A terrain plan is not built ground, and a clamped height outside a tile is not valid support.
        failures += Run("terrain approach bounds and construction requirements", YQTerrainApproachV2Tests.TestPlanningLimits, ref tested);
        failures += Run("native terrain holes, coverage and final connection gate", YQTerrainApproachV2Tests.TestActualTerrainGate, ref tested);
        // note: Structural stairs need swept, bidirectional evidence rather than flat-floor casts or endpoint-only checks.
        failures += Run("stepped approach support and bidirectional clearance", YQCellPassageReviewV2.TestSteppedRouteEvidence, ref tested);
        // note: Local preview grading must survive the actual world-grid spacing and preserve rejected/previously accepted heightfields.
        failures += Run("bounded coarse-grid terrain construction preview", YQTerrainApproachConstructionReviewV2.TestBoundedConstruction, ref tested);
        // note: Existing blueprint reservations protect continuous footprints, including off-grid features and explicit owner boundaries.
        failures += Run("terrain repair reservation geometry and ownership", YQTerrainRepairProtectionV2.TestProtectionPolicies, ref tested);
        // note: Saved-world review must use the correct player snapshot and one feasible cell height, not independent per-door translations.
        failures += Run("saved placement snapshot and common datum", YQSavedWorldPlacementReviewV2.TestReviewInputsAndDatum, ref tested);

        return failures;
    }

    public static string RunLegacyWeightedPickerOrderingCheck()
    {
        // note: Expose the focused reorder check for the beta binding receipt without duplicating its synthetic reviewed fixtures.
        return TestLegacyWeightedPickerOrdering();
    }

    private static int Run(
        string name,
        Func<string> test,
        ref int tested)
    {
        tested++;

        try
        {
            string failure = test();
            if (string.IsNullOrWhiteSpace(failure))
                return 0;

            Debug.LogError(
                "[YQWorldGenV2Tests] " + name + ": " + failure);
            return 1;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError(
                "[YQWorldGenV2Tests] " + name +
                " threw an exception.");
            return 1;
        }
    }

    private static string TestVersionBoundary()
    {
        GeneratedWorldPlanRecord plan = new GeneratedWorldPlanRecord
        {
            worldSeed = "v2_boundary_seed"
        };
        plan.EnsureCollections();
        plan.spatialPlan.semanticFingerprint = "semantic_fingerprint_1";

        if (!YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.PersistedV1,
                out YQSpatialPlanAuthority v1Authority,
                out string v1Reason) ||
            v1Authority != YQSpatialPlanAuthority.PersistedV1)
        {
            return "An ordinary V1 save lost authority: " + v1Reason;
        }

        if (!YQSpatialBlueprintCompilerV2.TryCompile(
                plan,
                out GeneratedSpatialWorldPlanV2Record acceptedV2,
                out string compileFailure))
        {
            return "A valid semantic plan could not compile V2: " +
                   compileFailure;
        }

        plan.spatialPlanV2 = acceptedV2;

        if (!YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Shadow,
                out YQSpatialPlanAuthority shadowAuthority,
                out string shadowReason) ||
            shadowAuthority != YQSpatialPlanAuthority.PersistedV1)
        {
            return "Shadow compilation displaced V1 authority: " +
                   shadowReason;
        }

        if (!YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Authoritative,
                out YQSpatialPlanAuthority v2Authority,
                out string v2Reason) ||
            v2Authority != YQSpatialPlanAuthority.AcceptedV2)
        {
            return "A complete accepted V2 artifact was rejected: " +
                   v2Reason;
        }

        if (!YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Preferred,
                out YQSpatialPlanAuthority preferredAuthority,
                out string preferredReason) ||
            preferredAuthority != YQSpatialPlanAuthority.AcceptedV2)
        {
            return "Preferred activation did not select a valid V2 artifact: " +
                   preferredReason;
        }

        plan.spatialPlanV2.validatedContentHash = "changed_after_gate";

        if (YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Authoritative,
                out _,
                out _))
        {
            return "An artifact changed after validation was accepted.";
        }

        if (YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Preferred,
                out preferredAuthority,
                out preferredReason) ||
            preferredAuthority != YQSpatialPlanAuthority.None)
        {
            return "Preferred activation silently downgraded a canonical V2 rejection: " +
                   preferredReason;
        }

        // note: A plan with no canonical V2 artifact remains eligible for the explicit legacy compatibility path.
        plan.spatialPlanV2 = null;
        if (!YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.V2Preferred,
                out preferredAuthority,
                out preferredReason) ||
            preferredAuthority != YQSpatialPlanAuthority.PersistedV1)
        {
            return "A non-canonical compatibility fixture lost its V1 fallback: " + preferredReason;
        }

        GeneratedSpatialWorldPlanV2Record legacy =
            new GeneratedSpatialWorldPlanV2Record
            {
                schemaVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                generationVersion =
                    GeneratedSpatialWorldPlanV2Record.LegacyGenerationVersion,
                validationVersion =
                    GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion,
                acceptanceState =
                    GeneratedSpatialPlanAcceptanceState.Accepted
            };
        if (!YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(legacy) ||
            YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(acceptedV2))
        {
            return "The safe shadow-era replacement boundary is not version-exact.";
        }

        GeneratedSpatialWorldPlanV2Record previousAccepted =
            new GeneratedSpatialWorldPlanV2Record
            {
                schemaVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                generationVersion =
                    GeneratedSpatialWorldPlanV2Record.PreviousAcceptedGenerationVersion,
                validationVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                acceptanceState =
                    GeneratedSpatialPlanAcceptanceState.Accepted
            };
        // note: A previously accepted topology must enter the explicit migration path so repaired physical continuation is not stranded behind an old save.
        if (!YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(previousAccepted))
        {
            return "The prior accepted topology cannot enter the explicit migration path.";
        }

        GeneratedSpatialWorldPlanV2Record olderAccepted =
            new GeneratedSpatialWorldPlanV2Record
            {
                schemaVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                generationVersion =
                    GeneratedSpatialWorldPlanV2Record.OlderAcceptedGenerationVersion,
                validationVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                acceptanceState =
                    GeneratedSpatialPlanAcceptanceState.Accepted
            };
        // note: The immediately older accepted topology remains explicitly migratable; protected saves never fall through to an unreviewed V1 presentation.
        if (!YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(olderAccepted))
        {
            return "The older accepted topology cannot enter the explicit migration path.";
        }

        GeneratedSpatialWorldPlanV2Record legacyAccepted =
            new GeneratedSpatialWorldPlanV2Record
            {
                schemaVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                generationVersion =
                    GeneratedSpatialWorldPlanV2Record.LegacyAcceptedGenerationVersion,
                validationVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                acceptanceState =
                    GeneratedSpatialPlanAcceptanceState.Accepted
            };
        // note: Topology 3 remains explicitly migratable for saves produced before the prior continuation repair.
        if (!YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(legacyAccepted))
        {
            return "The legacy accepted topology cannot enter the explicit migration path.";
        }

        GeneratedSpatialWorldPlanV2Record oldestAccepted =
            new GeneratedSpatialWorldPlanV2Record
            {
                schemaVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                generationVersion =
                    GeneratedSpatialWorldPlanV2Record.OldestAcceptedGenerationVersion,
                validationVersion =
                    GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                acceptanceState =
                    GeneratedSpatialPlanAcceptanceState.Accepted
            };
        // note: The topology-2 save family remains readable while still entering the current transactional rebuild path.
        if (!YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(oldestAccepted))
        {
            return "The oldest accepted topology cannot enter the explicit migration path.";
        }

        // note: Production now prefers a fully prepared V2 world and reserves whole-world V1 fallback for non-canonical compatibility fixtures.
        if (YQWorldGenerationArchitecture.ActiveSpatialPlanningMode !=
            YQSpatialPlanningMode.V2Preferred)
        {
            return "The live architecture is not in transactional V2-preferred mode.";
        }

        return string.Empty;
    }

    private static string TestHardConstraints()
    {
        YQAssetKitManifest kit = BuildKit(
            "manual_timber_kit",
            YQTechnologyBandV2.Manual,
            YQConstructionFamilyV2.Timber);
        YQSpatialAssetRecord valid = BuildHabitationAsset(
            "asset_valid_home",
            kit.kitId);
        YQAssetPlacementContextV2 context =
            BuildHabitationContext(YQTechnologyBandV2.Manual);

        YQAssetConstraintEvaluationV2 evaluation =
            YQAssetConstraintEvaluatorV2.Evaluate(kit, valid, context);

        if (!evaluation.Accepted)
            return "A valid reviewed structure failed: " + Join(evaluation);

        YQSpatialAssetRecord unsupported = BuildHabitationAsset(
            "asset_no_support",
            kit.kitId);
        unsupported.curationV2.supportPolygon.Clear();
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            unsupported,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.InvalidSupportPolygon))
        {
            return "A structure without support geometry was accepted.";
        }

        YQSpatialAssetRecord nonFiniteSupport = BuildHabitationAsset(
            "asset_nan_support",
            kit.kitId);
        nonFiniteSupport.curationV2.supportPolygon[0] =
            new Vector2(float.NaN, -5f);
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            nonFiniteSupport,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.InvalidSupportPolygon))
        {
            return "Non-finite support geometry bypassed the placement gate.";
        }

        YQSpatialAssetRecord doorless = BuildHabitationAsset(
            "asset_no_entrance",
            kit.kitId);
        doorless.curationV2.sockets.Clear();
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            doorless,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.MissingSocket))
        {
            return "An enterable structure without an entrance socket was accepted.";
        }

        YQSpatialAssetRecord unreviewed = BuildHabitationAsset(
            "asset_unreviewed",
            kit.kitId);
        unreviewed.releaseEligible = false;
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            unreviewed,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.AssetNotReleaseEligible))
        {
            return "An unreleased intake record entered V2 selection.";
        }

        YQAssetKitManifest incompleteStyle = BuildKit(
            "incomplete_style_kit",
            YQTechnologyBandV2.Manual,
            YQConstructionFamilyV2.Timber);
        incompleteStyle.styleV2.technologyBands.Clear();
        YQSpatialAssetRecord incompleteStyleAsset = BuildHabitationAsset(
            "asset_incomplete_style",
            incompleteStyle.kitId);
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            incompleteStyle,
            incompleteStyleAsset,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.InvalidStyleAxes))
        {
            return "A reviewed kit without explicit style axes entered selection.";
        }

        YQSpatialAssetRecord mismatchedKit = BuildHabitationAsset(
            "asset_wrong_kit",
            "some_other_kit");
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            mismatchedKit,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.AssetKitMismatch))
        {
            return "An asset was selected through a kit that does not own it.";
        }

        YQSpatialAssetRecord invalidPortal = BuildHabitationAsset(
            "asset_invalid_portal",
            kit.kitId);
        invalidPortal.curationV2.affordances.Add(
            YQAssetAffordanceV2.Openable);
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            invalidPortal,
            context);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.InvalidAffordanceContract))
        {
            return "An openable asset without a portal role/socket passed review.";
        }

        YQAssetPlacementContextV2 steep =
            BuildHabitationContext(YQTechnologyBandV2.Manual);
        steep.terrainSlopeDegrees = 22f;
        evaluation = YQAssetConstraintEvaluatorV2.Evaluate(
            kit,
            valid,
            steep);

        if (!evaluation.Failures.Contains(
                YQAssetConstraintFailureV2.SlopeExceeded))
        {
            return "A structure exceeded its reviewed slope contract.";
        }

        return string.Empty;
    }

    private static string TestDeterministicSelection()
    {
        YQAssetKitManifest kit = BuildKit(
            "deterministic_kit",
            YQTechnologyBandV2.Manual,
            YQConstructionFamilyV2.Timber);
        YQSpatialAssetRecord first = BuildHabitationAsset(
            "asset_home_a",
            kit.kitId);
        YQSpatialAssetRecord second = BuildHabitationAsset(
            "asset_home_b",
            kit.kitId);
        YQAssetPlacementContextV2 context =
            BuildHabitationContext(YQTechnologyBandV2.Manual);
        context.worldSeed = "stable_selector_seed";
        context.ownerId = "settlement_17";
        context.slotId = "parcel_04";

        List<YQAssetSelectionCandidateV2> forward =
            new List<YQAssetSelectionCandidateV2>
            {
                Wrap(kit, first),
                Wrap(kit, second)
            };
        List<YQAssetSelectionCandidateV2> reverse =
            new List<YQAssetSelectionCandidateV2>
            {
                Wrap(kit, second),
                Wrap(kit, first)
            };

        if (!YQDeterministicAssetSelectorV2.TrySelect(
                forward,
                context,
                out YQSpatialAssetRecord selectedForward,
                out string forwardFailure))
        {
            return "Forward selection failed: " + forwardFailure;
        }

        if (!YQDeterministicAssetSelectorV2.TrySelect(
                reverse,
                context,
                out YQSpatialAssetRecord selectedReverse,
                out string reverseFailure))
        {
            return "Reverse selection failed: " + reverseFailure;
        }

        if (!string.Equals(
                selectedForward.stableAssetId,
                selectedReverse.stableAssetId,
                StringComparison.Ordinal))
        {
            return "Catalog enumeration order changed the selected stable asset.";
        }

        return string.Empty;
    }

    private static string TestLegacyWeightedPickerOrdering()
    {
        // note: Reordered legacy palette fixtures prove the weighted bridge is stable before any runtime catalog is appended.
        GeneratedRegionAssetPaletteRecord forwardPalette =
            new GeneratedRegionAssetPaletteRecord();
        forwardPalette.settlementBuilding.Add(new GeneratedAssetReferenceRecord
        {
            assetKey = "stable_house_a",
            assetPath = "Assets/Tests/HouseA.prefab",
            slotTag = YQWorldAssetCatalog.SlotSettlementBuilding,
            runtimeEligible = true,
            weight = 1
        });
        forwardPalette.settlementBuilding.Add(new GeneratedAssetReferenceRecord
        {
            assetKey = "stable_house_b",
            assetPath = "Assets/Tests/HouseB.prefab",
            slotTag = YQWorldAssetCatalog.SlotSettlementBuilding,
            runtimeEligible = true,
            weight = 1
        });

        GeneratedRegionAssetPaletteRecord reversePalette =
            new GeneratedRegionAssetPaletteRecord();
        reversePalette.settlementBuilding.Add(forwardPalette.settlementBuilding[1]);
        reversePalette.settlementBuilding.Add(forwardPalette.settlementBuilding[0]);

        GeneratedAssetReferenceRecord forward = YQWorldAssetCatalog.PickAssetForSlot(
            forwardPalette,
            YQWorldAssetCatalog.SlotSettlementBuilding,
            "stable-reorder-seed");
        GeneratedAssetReferenceRecord reverse = YQWorldAssetCatalog.PickAssetForSlot(
            reversePalette,
            YQWorldAssetCatalog.SlotSettlementBuilding,
            "stable-reorder-seed");

        if (forward == null || reverse == null)
            return "Synthetic reviewed building candidates were not selectable.";
        if (!string.Equals(forward.assetKey, reverse.assetKey, StringComparison.Ordinal))
            return "Legacy weighted selection changed when the palette list was reordered.";

        return string.Empty;
    }

    private static string TestGenreNeutralBinding()
    {
        YQAssetKitManifest manualKit = BuildKit(
            "manual_habitation_kit",
            YQTechnologyBandV2.Manual,
            YQConstructionFamilyV2.Timber);
        YQAssetKitManifest advancedKit = BuildKit(
            "advanced_habitation_kit",
            YQTechnologyBandV2.Advanced,
            YQConstructionFamilyV2.Composite);
        YQSpatialAssetRecord manualAsset = BuildHabitationAsset(
            "asset_manual_habitation",
            manualKit.kitId);
        YQSpatialAssetRecord advancedAsset = BuildHabitationAsset(
            "asset_advanced_habitation",
            advancedKit.kitId);
        List<YQAssetSelectionCandidateV2> candidates =
            new List<YQAssetSelectionCandidateV2>
            {
                Wrap(manualKit, manualAsset),
                Wrap(advancedKit, advancedAsset)
            };

        YQAssetPlacementContextV2 manualContext =
            BuildHabitationContext(YQTechnologyBandV2.Manual);
        manualContext.worldSeed = "genre_neutral_seed";
        manualContext.ownerId = "site_a";
        manualContext.slotId = "habitation_1";

        if (!YQDeterministicAssetSelectorV2.TrySelect(
                candidates,
                manualContext,
                out YQSpatialAssetRecord selectedManual,
                out string manualFailure) ||
            selectedManual != manualAsset)
        {
            return "Manual style did not bind the shared habitation intent: " +
                   manualFailure;
        }

        YQAssetPlacementContextV2 advancedContext =
            BuildHabitationContext(YQTechnologyBandV2.Advanced);
        advancedContext.worldSeed = manualContext.worldSeed;
        advancedContext.ownerId = manualContext.ownerId;
        advancedContext.slotId = manualContext.slotId;

        if (!YQDeterministicAssetSelectorV2.TrySelect(
                candidates,
                advancedContext,
                out YQSpatialAssetRecord selectedAdvanced,
                out string advancedFailure) ||
            selectedAdvanced != advancedAsset)
        {
            return "Advanced style did not bind the shared habitation intent: " +
                   advancedFailure;
        }

        if (manualContext.requiredFunction != advancedContext.requiredFunction ||
            manualContext.requiredRole != advancedContext.requiredRole)
        {
            return "Style selection altered the mechanical site contract.";
        }

        return string.Empty;
    }

    private static YQAssetKitManifest BuildKit(
        string kitId,
        YQTechnologyBandV2 technology,
        YQConstructionFamilyV2 construction)
    {
        YQAssetKitManifest kit = new YQAssetKitManifest
        {
            kitId = kitId,
            displayName = kitId,
            releaseEligible = true
        };
        kit.EnsureCollections();
        kit.styleV2.contractVersion =
            YQKitStyleContractV2.SupportedContractVersion;
        kit.styleV2.technologyBands.Add(technology);
        kit.styleV2.constructionFamilies.Add(construction);
        kit.styleV2.environments.Add(YQAssetEnvironmentV2.Exterior);
        kit.styleV2.conditions.Add(YQConditionBandV2.Maintained);
        return kit;
    }

    private static YQSpatialAssetRecord BuildHabitationAsset(
        string stableId,
        string kitId)
    {
        YQSpatialAssetRecord asset = new YQSpatialAssetRecord
        {
            stableAssetId = stableId,
            sourceGuid = stableId + "_guid",
            assetPath = "Assets/Reviewed/" + stableId + ".prefab",
            kitId = kitId,
            semanticRole = "reviewed_structure",
            compositionScale = YQSpatialCompositionScale.CompleteBuilding,
            disposition = YQAssetIntakeDisposition.Candidate,
            releaseEligible = true,
            localBoundsSize = new Vector3(8f, 6f, 10f),
            clearanceSize = new Vector3(9f, 7f, 11f),
            footprintX = 8f,
            footprintZ = 10f,
            height = 6f,
            frontDirection = Vector3.forward,
            frontDirectionAuthored = true,
            spatialMetadataAuthored = true,
            allowedSlopeDegrees = 12f,
            foundationProfile = "reviewed_cut_fill_foundation",
            roadRelationship = "frontage_required",
            navigationProfile = "walkable_structure",
            hasRenderer = true,
            rendererCount = 3,
            materialSlotCount = 3,
            hasCollider = true,
            colliderCount = 2,
            lodGroupCount = 1,
            estimatedRendererCost = 3
        };
        asset.EnsureCollections();
        asset.curationV2.contractVersion =
            YQAssetCurationContractV2.SupportedContractVersion;
        asset.curationV2.primaryRole = YQAssetRoleV2.CompleteStructure;
        asset.curationV2.primaryFunction = YQAssetFunctionV2.Habitation;
        asset.curationV2.environments.Add(YQAssetEnvironmentV2.Exterior);
        asset.curationV2.affordances.Add(YQAssetAffordanceV2.Entrance);
        asset.curationV2.familyId = "habitation_family";
        asset.curationV2.variantGroupId = stableId;
        asset.curationV2.supportMode = YQAssetSupportModeV2.Foundation;
        asset.curationV2.canonicalScale = Vector3.one;
        asset.curationV2.maximumSupportRelief = 1.5f;
        asset.curationV2.supportPolygon.Add(new Vector2(-4f, -5f));
        asset.curationV2.supportPolygon.Add(new Vector2(4f, -5f));
        asset.curationV2.supportPolygon.Add(new Vector2(4f, 5f));
        asset.curationV2.supportPolygon.Add(new Vector2(-4f, 5f));
        asset.curationV2.sockets.Add(new YQAssetSocketRecordV2
        {
            socketId = "entrance_main",
            kind = YQAssetSocketKindV2.Entrance,
            transformPath = "Sockets/Entrance_Main",
            localPosition = new Vector3(0f, 0f, 5f),
            localRotation = Quaternion.identity,
            clearanceSize = new Vector3(2f, 3f, 2f),
            compatibilityKey = "pedestrian_entry"
        });
        return asset;
    }

    private static YQAssetPlacementContextV2 BuildHabitationContext(
        YQTechnologyBandV2 technology)
    {
        YQAssetPlacementContextV2 context =
            new YQAssetPlacementContextV2
            {
                requiredRole = YQAssetRoleV2.CompleteStructure,
                requiredFunction = YQAssetFunctionV2.Habitation,
                requiredEnvironment = YQAssetEnvironmentV2.Exterior,
                technologyBand = technology,
                requiredSupportMode = YQAssetSupportModeV2.Foundation,
                terrainSlopeDegrees = 5f,
                maximumFootprint = new Vector2(14f, 16f),
                requireTerrainSupport = true,
                requireEntrance = true,
                requireCollider = true,
                requireNavigation = true,
                requireValidMaterials = true
            };
        context.EnsureCollections();
        return context;
    }

    private static YQAssetSelectionCandidateV2 Wrap(
        YQAssetKitManifest kit,
        YQSpatialAssetRecord asset)
    {
        return new YQAssetSelectionCandidateV2
        {
            kit = kit,
            asset = asset
        };
    }

    private static string Join(YQAssetConstraintEvaluationV2 evaluation)
    {
        return evaluation == null
            ? "<null evaluation>"
            : string.Join(", ", evaluation.Failures);
    }
}
