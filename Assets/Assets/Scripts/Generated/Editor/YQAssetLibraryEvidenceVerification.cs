using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: These Edit Mode fixtures use disposable objects, text/metadata extraction and pure projection; they never load or publish assets.
public static class YQAssetLibraryEvidenceVerification
{
    [Serializable]
    private sealed class Check
    {
        public string name;
        public string verdict;
        public string expected;
        public string actual;
    }

    [Serializable]
    private sealed class Receipt
    {
        public string schemaVersion = "yq_asset_library_documentary_contract_receipt_v3";
        public string verifierVersion = "current-intake-3-historical-separation";
        public string utc;
        public string evidenceLevel = "EDIT_MODE_DOCUMENTARY_CONTRACT";
        public bool runtimeVerified;
        public string unityVersion;
        public string runtimeMvid;
        public string editorMvid;
        public string runtimeAssemblySha256;
        public string editorAssemblySha256;
        public string verdict;
        public string candidatePath;
        public string candidateSha256;
        public int candidateAssets;
        public int candidateBindings;
        public int candidateSourceVersions;
        public int candidateConflicts;
        public List<string> sourceSnapshotIdentities = new List<string>();
        public List<Check> checks = new List<Check>();
    }

    [MenuItem("YourQuest/Verification/Run Asset Library Documentary Contracts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Asset library documentary contracts require stable Edit Mode.");

        Receipt receipt = new Receipt
        {
            utc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            runtimeMvid = typeof(YQWorldAssetIntakeCatalog).Assembly.ManifestModule.ModuleVersionId.ToString(),
            editorMvid = typeof(YQAssetLibraryEvidenceVerification).Assembly.ManifestModule.ModuleVersionId.ToString(),
            runtimeAssemblySha256 = Hash(typeof(YQWorldAssetIntakeCatalog).Assembly.Location),
            editorAssemblySha256 = Hash(typeof(YQAssetLibraryEvidenceVerification).Assembly.Location)
        };
        foreach (string source in new[]
        {
            "Assets/Assets/Scripts/Generated/YQWorldAssetIntakeCatalog.cs",
            "Assets/Assets/Scripts/Generated/YQDiscoveredWorldAssetCatalog.cs",
            "Assets/Assets/Scripts/Generated/Editor/YQRuntimeWorldAssetRegistryBuilder.cs",
            "Assets/Assets/Scripts/Generated/Editor/YQAssetLibraryEvidenceVerification.cs"
        })
            receipt.sourceSnapshotIdentities.Add(source + "|sha256=" + Hash(source));

        RunCheck(receipt, "legacy v3 round-trip and numeric contracts", () => VerifyLegacyRoundTrip(receipt));
        RunCheck(receipt, "documentary retention, normalization and export", () => VerifyDocumentaryRecords(receipt));
        RunCheck(receipt, "existing master historical tuple preservation", () => VerifyExistingMaster(receipt));
        RunCheck(receipt, "copy-only coverage merge, identities and rescan retention", () => VerifyCoverageMerge(receipt));
        RunCheck(receipt, "declared export roots follow exact versions and parts", () => VerifyDeclaredExportRoots(receipt));
        RunCheck(receipt, "current intake observations retain nullable truth and exact identities", () => VerifyCurrentIntakeMerge(receipt));
        YQWorldAssetIntakeBuilder.GenerationInventoryDocument candidate = null;
        RunCheck(receipt, "actual source readers and candidate preservation", () => candidate = VerifyActualCoverage(receipt));
        receipt.verdict = receipt.checks.Exists(check => check.verdict == "FAIL") ? "FAIL" : "PASS";
        string directory = Path.Combine("outputs/G08_EnvironmentCohesion_20261003",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_asset_library_documentary_contracts");
        Directory.CreateDirectory(directory);
        if (candidate != null)
        {
            // note: The review candidate is investigation output only. No AssetDatabase import or canonical publication is invoked.
            receipt.candidatePath = Path.Combine(directory, "WorldAssetInventoryCandidate.json").Replace('\\', '/');
            File.WriteAllText(receipt.candidatePath, JsonConvert.SerializeObject(candidate, Formatting.Indented));
            receipt.candidateSha256 = Hash(receipt.candidatePath);
            receipt.candidateAssets = candidate.assets.Count;
            receipt.candidateBindings = candidate.domainBindings.Count;
            receipt.candidateSourceVersions = candidate.sourceVersions.Count;
            receipt.candidateConflicts = candidate.coverageConflicts.Count;
        }
        File.WriteAllText(Path.Combine(directory, "Receipt.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
        Debug.Log("[YQAssetLibraryDocumentaryContracts] " + receipt.verdict + "; checks=" + receipt.checks.Count + "; " + directory);
    }

    private static void VerifyLegacyRoundTrip(Receipt receipt)
    {
        const string legacyJson = @"{
          ""schemaVersion"":""world_asset_intake_v3"",""scanScope"":""fixture_old_v3"",""generatedUtc"":""2026-09-13T00:00:00Z"",
          ""kits"":[{""kitId"":""fixture_kit"",""sourceRoot"":""Assets/FixtureLibrary"",""releaseEligible"":false}],
          ""spatialAssets"":[{""stableAssetId"":""asset_fixture_guid"",""sourceGuid"":""fixture_guid"",""sourceAssetKey"":""accepted_old_key"",
            ""assetPath"":""Assets/FixtureLibrary/Old.prefab"",""kitId"":""fixture_kit"",""semanticRole"":""vegetation"",
            ""compositionScale"":3,""disposition"":0,""releaseEligible"":false,""spatialMetadataAuthored"":false}],
          ""materials"":[{""stableAssetId"":""material_fixture_guid"",""sourceGuid"":""fixture_material_guid"",
            ""assetPath"":""Assets/FixtureLibrary/Old.mat"",""kitId"":""fixture_kit"",""compatibilityState"":2,""releaseEligible"":false}]
        }";
        YQWorldAssetIntakeCatalog first = CreateFixture();
        YQWorldAssetIntakeCatalog restored = CreateFixture();
        try
        {
            JsonUtility.FromJsonOverwrite(legacyJson, first);
            first.EnsureCollections();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(first), restored);
            restored.EnsureCollections();
            Add(receipt, "all supplied old v3 fields survive round-trip", "True",
                ContainsExpectedFields(JObject.Parse(JsonUtility.ToJson(restored)), JObject.Parse(legacyJson)).ToString());
            Add(receipt, "outer intake schema remains v3", "world_asset_intake_v3", restored.SchemaVersion);
            Add(receipt, "missing documentary collections stay empty", "0|0|0|0",
                restored.LibraryEvidence.Count + "|" + restored.Kits[0].libraryEvidenceIds.Count + "|" +
                restored.SpatialAssets[0].libraryEvidenceIds.Count + "|" + restored.Materials[0].libraryEvidenceIds.Count);
            Add(receipt, "legacy enum numbers remain unchanged", "0|1|6|0|1|6|10",
                (int)YQAssetIntakeDisposition.NeedsSpatialReview + "|" + (int)YQAssetIntakeDisposition.Candidate + "|" +
                (int)YQAssetIntakeDisposition.Quarantined + "|" + (int)YQMaterialCompatibilityState.Unknown + "|" +
                (int)YQMaterialCompatibilityState.VerifiedUrp + "|" + (int)YQMaterialCompatibilityState.VerifiedUrpAdapter + "|" +
                (int)YQSpatialCompositionScale.CharacterOrCreature);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(restored);
        }
    }

    private static void VerifyDocumentaryRecords(Receipt receipt)
    {
        YQWorldAssetIntakeCatalog catalog = CreateFixture();
        YQWorldAssetIntakeCatalog restored = CreateFixture();
        try
        {
            // note: Synthetic held/withdrawn facts exercise shape and retention; they do not author production approval.
            YQAssetLibraryEvidenceRecord unknown = new YQAssetLibraryEvidenceRecord { evidenceId = "unknown" };
            YQAssetLibraryEvidenceRecord held = Evidence("held", YQAssetLibraryApprovalState.Held, "delivery", "payload_held");
            held.deliveryPermission = YQAssetLibraryDeliveryPermission.Denied;
            held.supersedesEvidenceIds = new List<string> { "prior_visual_review", "prior_technical_review" };
            YQAssetLibraryEvidenceRecord withdrawn = Evidence("withdrawn", YQAssetLibraryApprovalState.Withdrawn, "delivery", "payload_withdrawn");
            withdrawn.deliveryPermission = YQAssetLibraryDeliveryPermission.Denied;
            YQAssetLibraryEvidenceRecord narrow = Evidence("narrow", YQAssetLibraryApprovalState.Approved,
                "scalp_alpha_correction_only", "payload_narrow");
            narrow.deliveryPermission = YQAssetLibraryDeliveryPermission.Allowed;
            narrow.provenanceEvidenceRefs = new List<string> { "source_z", "source_a" };
            narrow.consumerEvidence.Add(new YQAssetLibraryConsumerEvidenceRecord
            {
                contractVersion = 1, consumerId = "item_pool", bindingKey = "accepted_old_key",
                evidenceLevel = YQAssetLibraryConsumerEvidenceLevel.StaticReference, reason = "fixture static reference only"
            });
            narrow.consumerEvidence.Add(new YQAssetLibraryConsumerEvidenceRecord { consumerId = "unknown_consumer" });
            List<YQAssetLibraryEvidenceRecord> evidence = new List<YQAssetLibraryEvidenceRecord> { withdrawn, unknown, narrow, held };
            YQAssetKitManifest kit = new YQAssetKitManifest { kitId = "fixture_kit", sourceRoot = "Assets/FixtureLibrary",
                releaseEligible = false, libraryEvidenceIds = new List<string> { "withdrawn", "unknown" } };
            YQSpatialAssetRecord spatial = Spatial("fixture_guid", "Assets/FixtureLibrary/Old.prefab", false);
            spatial.libraryEvidenceIds.Add("held");
            YQMaterialAssetRecord material = new YQMaterialAssetRecord { stableAssetId = "material_fixture_guid",
                sourceGuid = "fixture_material_guid", assetPath = "Assets/FixtureLibrary/Old.mat", kitId = kit.kitId,
                compatibilityState = YQMaterialCompatibilityState.NeedsReview, releaseEligible = false,
                libraryEvidenceIds = new List<string> { "narrow" } };
            catalog.SetRecords("fixture", "fixture_utc", new List<YQAssetKitManifest> { kit },
                new List<YQSpatialAssetRecord> { spatial }, new List<YQMaterialAssetRecord> { material }, evidence);
            string evidenceBefore = JsonConvert.SerializeObject(catalog.LibraryEvidence);
            YQAssetKitManifest refreshedKit = new YQAssetKitManifest { kitId = kit.kitId, sourceRoot = kit.sourceRoot };
            YQSpatialAssetRecord refreshedSpatial = Spatial(spatial.sourceGuid, "Assets/FixtureLibrary/Moved.prefab", false);
            YQMaterialAssetRecord refreshedMaterial = new YQMaterialAssetRecord { stableAssetId = material.stableAssetId,
                sourceGuid = material.sourceGuid, assetPath = "Assets/FixtureLibrary/Moved.mat", kitId = kit.kitId,
                compatibilityState = YQMaterialCompatibilityState.NeedsReview };
            catalog.SetRecords("fixture_rescan", "fixture_new_utc", new List<YQAssetKitManifest> { refreshedKit },
                new List<YQSpatialAssetRecord> { refreshedSpatial }, new List<YQMaterialAssetRecord> { refreshedMaterial });
            Add(receipt, "old SetRecords retains all documentary facts", evidenceBefore, JsonConvert.SerializeObject(catalog.LibraryEvidence));
            Add(receipt, "false/unreviewed records retain links across GUID-preserving moves", "withdrawn,unknown|held|narrow",
                string.Join(",", refreshedKit.libraryEvidenceIds) + "|" + string.Join(",", refreshedSpatial.libraryEvidenceIds) + "|" +
                string.Join(",", refreshedMaterial.libraryEvidenceIds));
            Add(receipt, "documentary retention preserves technical states", "False|False|NeedsSpatialReview|False|NeedsReview",
                refreshedKit.releaseEligible + "|" + refreshedSpatial.releaseEligible + "|" + refreshedSpatial.disposition + "|" +
                refreshedMaterial.releaseEligible + "|" + refreshedMaterial.compatibilityState);
            Add(receipt, "missing approval and consumer evidence are unknown", "0|Unknown|Unknown|0|Unknown",
                unknown.contractVersion + "|" + unknown.approvalState + "|" + unknown.deliveryPermission + "|" +
                narrow.consumerEvidence[1].contractVersion + "|" + narrow.consumerEvidence[1].evidenceLevel);

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(catalog), restored);
            restored.EnsureCollections();
            Add(receipt, "held/withdrawn/narrow facts survive Unity JSON round-trip",
                JObject.Parse(JsonUtility.ToJson(catalog))["libraryEvidence"].ToString(Formatting.None),
                JObject.Parse(JsonUtility.ToJson(restored))["libraryEvidence"].ToString(Formatting.None));
            YQAssetLibraryEvidenceRecord nullCollections = new YQAssetLibraryEvidenceRecord
            {
                provenanceEvidenceRefs = null, licenseEvidenceRefs = null, supersedesEvidenceIds = null,
                supersedesVersionIds = null, consumerEvidence = null
            };
            nullCollections.EnsureCollections();
            Add(receipt, "collection normalization does not infer documentary state", "0|Unknown|Unknown|0|0|0|0|0",
                nullCollections.contractVersion + "|" + nullCollections.approvalState + "|" + nullCollections.deliveryPermission + "|" +
                nullCollections.provenanceEvidenceRefs.Count + "|" + nullCollections.licenseEvidenceRefs.Count + "|" +
                nullCollections.supersedesEvidenceIds.Count + "|" + nullCollections.supersedesVersionIds.Count + "|" +
                nullCollections.consumerEvidence.Count);

            YQSpatialAssetRecord ready = Spatial("fixture_ready_guid", "Assets/FixtureLibrary/Art/Props/Approved.prefab", true);
            List<YQSpatialAssetRecord> spatialRecords = new List<YQSpatialAssetRecord> { ready, refreshedSpatial };
            var coverage = new YQWorldAssetIntakeBuilder.GenerationInventoryAsset
            {
                stableAssetId = "source_fixture_coverage", sourceGuid = "fixture_coverage_guid",
                assetPath = "Assets/FixtureLibrary/Art/Props/Approved.glb", assetType = "source_model",
                sourceRoot = kit.sourceRoot, runtimeEligible = false, finalState = "pending_source_asset_review",
                finalStateReason = "unknown documentary coverage", classificationStatus = "unknown",
                reviewDisposition = "unknown", reviewPolicyVersion = "documentary-unknown",
                reviewPolicyScope = YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LibraryCoverage,
                libraryEvidenceIds = new List<string> { "withdrawn", "held" }
            };
            List<YQWorldAssetIntakeBuilder.GenerationInventoryAsset> sources =
                new List<YQWorldAssetIntakeBuilder.GenerationInventoryAsset>
                {
                    coverage,
                    new YQWorldAssetIntakeBuilder.GenerationInventoryAsset
                    {
                        sourceGuid = "fixture_legacy_source", assetPath = "Assets/FixtureLibrary/OtherSource.glb",
                        assetType = "source_model", sourceRoot = kit.sourceRoot, finalState = "pending_source_asset_review",
                        finalStateReason = "historical pending source", classificationStatus = "pending"
                    }
                };
            string inputsBefore = SnapshotInputs(catalog.LibraryEvidence, spatialRecords, sources);
            var document = YQWorldAssetIntakeBuilder.BuildGenerationInventoryDocument(new List<string> { kit.sourceRoot },
                new List<YQAssetKitManifest> { refreshedKit }, spatialRecords, new List<YQMaterialAssetRecord> { refreshedMaterial },
                sources, catalog.LibraryEvidence, "fixed_fixture_utc");
        Add(receipt, "expanded export has an explicit version", "yq_world_generation_asset_inventory_v7|1",
                document.schemaVersion + "|" + document.libraryContractVersion);
            var coverageExport = document.assets.Find(row => row.sourceGuid == coverage.sourceGuid);
            Add(receipt, "coverage unknown bypasses legacy exclusion and filename representation", "pending_source_asset_review|unknown|unknown|documentary-unknown|False",
                coverageExport.finalState + "|" + coverageExport.classificationStatus + "|" + coverageExport.reviewDisposition + "|" +
                coverageExport.reviewPolicyVersion + "|" + coverageExport.runtimeEligible);
            var legacySourceExport = document.assets.Find(row => row.sourceGuid == "fixture_legacy_source");
            Add(receipt, "legacy pending policy behavior remains unchanged", "intentionally_excluded_source_without_runtime_binding|intentional_exclusion|world-kit-policy-v1",
                legacySourceExport.finalState + "|" + legacySourceExport.reviewDisposition + "|" + legacySourceExport.reviewPolicyVersion);
            var spatialExport = document.assets.Find(row => row.sourceGuid == refreshedSpatial.sourceGuid);
            Add(receipt, "export preserves stable identity and documentary link", refreshedSpatial.stableAssetId + "|" +
                refreshedSpatial.sourceGuid + "|accepted_old_key|held", spatialExport.stableAssetId + "|" + spatialExport.sourceGuid + "|" +
                spatialExport.sourceAssetKey + "|" + string.Join(",", spatialExport.libraryEvidenceIds));
            Add(receipt, "documentary approval does not change world readiness", "True|False",
                YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(ready) + "|" +
                YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(refreshedSpatial));
            Add(receipt, "pure export leaves input records unchanged", inputsBefore,
                SnapshotInputs(catalog.LibraryEvidence, spatialRecords, sources));
            List<YQAssetLibraryEvidenceRecord> reversedEvidence = new List<YQAssetLibraryEvidenceRecord>(catalog.LibraryEvidence);
            reversedEvidence.Reverse();
            spatialRecords.Reverse();
            var reversed = YQWorldAssetIntakeBuilder.BuildGenerationInventoryDocument(new List<string> { kit.sourceRoot },
                new List<YQAssetKitManifest> { refreshedKit }, spatialRecords, new List<YQMaterialAssetRecord> { refreshedMaterial },
                sources, reversedEvidence, "fixed_fixture_utc");
            Add(receipt, "documentary export survives reordered inputs", JsonConvert.SerializeObject(document), JsonConvert.SerializeObject(reversed));
            Add(receipt, "scoped approval remains narrow in export", "scalp_alpha_correction_only|payload_narrow|StaticReference",
                document.libraryEvidence.Find(row => row.evidenceId == "narrow").approvalScope + "|" +
                document.libraryEvidence.Find(row => row.evidenceId == "narrow").payloadSha256 + "|" +
                document.libraryEvidence.Find(row => row.evidenceId == "narrow").consumerEvidence.Find(row => row.consumerId == "item_pool").evidenceLevel);

            YQSpatialAssetRecord replacement = Spatial("different_guid", refreshedSpatial.assetPath, false);
            catalog.SetRecords("fixture_replacement", "fixture_new_utc", new List<YQAssetKitManifest> { refreshedKit },
                new List<YQSpatialAssetRecord> { replacement }, new List<YQMaterialAssetRecord> { refreshedMaterial });
            Add(receipt, "same path with a different GUID does not inherit evidence", "0", replacement.libraryEvidenceIds.Count.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(restored);
        }
    }

    private static void VerifyExistingMaster(Receipt receipt)
    {
        // note: Read the accepted master as text and project copies; this check neither rescans nor rewrites its production file.
        const string path = "Assets/Assets/GeneratedAssets/WorldIntake/YQWorldGenerationAssetInventory.json";
        receipt.sourceSnapshotIdentities.Add(path + "|sha256=" + Hash(path));
        JObject original = JObject.Parse(File.ReadAllText(path));
        var sourceRows = original["assets"].ToObject<List<YQWorldAssetIntakeBuilder.GenerationInventoryAsset>>();
        var document = YQWorldAssetIntakeBuilder.BuildGenerationInventoryDocument(
            original["approvedRoots"].ToObject<List<string>>(), new List<YQAssetKitManifest>(), new List<YQSpatialAssetRecord>(),
            new List<YQMaterialAssetRecord>(), sourceRows, null, "fixed_fixture_utc");
        JObject exported = JObject.FromObject(document);
        Dictionary<string, JObject> byPath = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject row in exported["assets"]) byPath.Add((string)row["assetPath"], row);
        string[] tupleFields = { "sourceGuid", "assetPath", "assetType", "assetFamily", "sourceRoot", "registryState", "slotTag",
            "runtimeEligible", "finalState", "finalStateReason", "representedByAssetPath", "intakeDisposition", "technicalIssues",
            "classificationStatus", "placementContextStatus", "paletteAssignmentStatus", "technicalValidationStatus", "reviewDisposition", "reviewPolicyVersion" };
        int changed = 0;
        string firstChange = null;
        foreach (JObject row in original["assets"])
        {
            if (!byPath.TryGetValue((string)row["assetPath"], out JObject projected))
            {
                changed++;
                firstChange ??= (string)row["assetPath"] + " missing";
                continue;
            }
            foreach (string field in tupleFields)
                if (!JToken.DeepEquals(row[field], projected[field]))
                {
                    changed++;
                    firstChange ??= (string)row["assetPath"] + "|" + field;
                    break;
                }
        }
        Add(receipt, "all existing master classification/reason/policy tuples survive projection", "0",
            changed == 0 ? "0" : changed + "|first=" + firstChange);
        Add(receipt, "existing master summary counts survive projection", original["counts"].ToString(Formatting.None),
            exported["counts"].ToString(Formatting.None));
        Add(receipt, "production master remains unchanged after read-only projection", receipt.sourceSnapshotIdentities[receipt.sourceSnapshotIdentities.Count - 1],
            path + "|sha256=" + Hash(path));
    }

    private static void VerifyCoverageMerge(Receipt receipt)
    {
        // note: Synthetic identities exercise overlapping observations and documentary retention without production files or objects.
        string baselineHash = new string('a', 64), installedHash = new string('b', 64), otherHash = new string('c', 64);
        var historical = new YQWorldAssetIntakeBuilder.GenerationInventoryAsset
        {
            stableAssetId = "accepted_stable_id", sourceGuid = "historical_guid", sourceAssetKey = "accepted_old_key",
            assetPath = "Assets/FixtureLibrary/Old.prefab", assetType = "prefab", assetFamily = "accepted_family",
            sourceRoot = "Assets/FixtureLibrary", registryState = "generation_ready", slotTag = "vegetation", runtimeEligible = true,
            finalState = "generation_ready", finalStateReason = "accepted historical reason", intakeDisposition = "Candidate",
            technicalIssues = new List<string>(), classificationStatus = "reviewed", placementContextStatus = "reviewed",
            paletteAssignmentStatus = "assigned", technicalValidationStatus = "compatible", reviewDisposition = "approved_for_generation",
            reviewPolicyVersion = "world-kit-policy-v1", libraryEvidenceIds = new List<string> { "held" }
        };
        var master = new YQWorldAssetIntakeBuilder.GenerationInventoryDocument { generatedUtc = "fixed_fixture_utc" };
        master.assets.Add(historical);
        master.libraryEvidence.Add(new YQAssetLibraryEvidenceRecord { evidenceId = "unknown" });
        master.libraryEvidence.Add(Evidence("held", YQAssetLibraryApprovalState.Held, "delivery", "held_payload"));
        master.libraryEvidence[1].deliveryPermission = YQAssetLibraryDeliveryPermission.Denied;
        master.libraryEvidence.Add(Evidence("withdrawn", YQAssetLibraryApprovalState.Withdrawn, "delivery", "withdrawn_payload"));
        master.libraryEvidence[2].deliveryPermission = YQAssetLibraryDeliveryPermission.Denied;
        master.libraryEvidence.Add(Evidence("narrow", YQAssetLibraryApprovalState.Approved, "scalp_alpha_correction_only", "narrow_payload"));
        var inputs = new YQWorldAssetIntakeBuilder.LibraryCoverageInputs { master = master };
        var main = CoverageSource("main_guid", "Assets/FixtureDot/main.glb", baselineHash, installedHash);
        main.sourceAliases.Add("SourceAssets/DOT/Fixture/main.glb");
        main.libraryEvidenceIds.AddRange(new[] { "withdrawn", "held", "unknown", "narrow" });
        main.sourceDeclarationRefs.Add("fixture_layout#main");
        inputs.sources.Add(main);
        var overlapping = CoverageSource("main_guid", main.assetPath, null, null);
        overlapping.sourceAvailability = "observed";
        overlapping.sourceDeclarationRefs.Add("fixture_metadata#main");
        inputs.sources.Add(overlapping);
        inputs.sources.Add(CoverageSource("lod_guid", "Assets/FixtureDot/main_LOD1.glb", otherHash, otherHash));
        inputs.sources.Add(CoverageSource(historical.sourceGuid, "Assets/FixtureLibrary/Moved.prefab", null, null));
        inputs.roots.AddRange(new[] { "Assets/FixtureDot", "Assets/FixtureDot" });
        var binding = new YQWorldAssetIntakeBuilder.GenerationInventoryDomainBinding
        {
            bindingId = "dot-equipment:fixture_item", domain = "equipment", assetId = "fixture_item", bindingKey = "accepted_binding_key",
            registryGuid = historical.sourceGuid, domainEligible = true, eligibilityReason = "existing technical flag only",
            catalogEvidenceRef = "fixture_catalog", sources = new List<YQWorldAssetIntakeBuilder.GenerationInventorySourceRelation>
            {
                new YQWorldAssetIntakeBuilder.GenerationInventorySourceRelation { declaredPath = main.sourceAliases[0], declaredSha256 = baselineHash, role = "declared_primary_source" },
                new YQWorldAssetIntakeBuilder.GenerationInventorySourceRelation { declaredPath = "Assets/FixtureDot/main_LOD1.glb", declaredSha256 = otherHash, role = "declared_lod1" }
            }
        };
        inputs.bindings.Add(binding);
        inputs.bindings.Add(new YQWorldAssetIntakeBuilder.GenerationInventoryDomainBinding
        {
            bindingId = "dot-creature:unknown_fixture", domain = "creature", assetId = "unknown_fixture", domainEligible = null,
            sources = new List<YQWorldAssetIntakeBuilder.GenerationInventorySourceRelation>
            {
                new YQWorldAssetIntakeBuilder.GenerationInventorySourceRelation { declaredPath = main.assetPath, declaredSha256 = otherHash, role = "declared_primary_source" }
            }
        });
        string before = JsonConvert.SerializeObject(inputs), historicalBefore = HistoricalTuples(master.assets);
        var merged = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(master, inputs);
        Add(receipt, "pure merge leaves master, observations and evidence unchanged", before, JsonConvert.SerializeObject(inputs));
        Add(receipt, "legacy tuple is unchanged after additive coverage", historicalBefore,
            HistoricalTuples(merged.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit)));
        Add(receipt, "overlapping roots and declarations do not duplicate files or promote LODs", "3|1|2|1|2",
            merged.counts.total + "|" + merged.counts.generationReady + "|" + merged.counts.pendingReview + "|" +
            merged.legacyCounts.total + "|" + merged.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LibraryCoverage && !row.runtimeEligible).Count);
        var mergedMain = merged.assets.Find(row => row.sourceGuid == "main_guid");
        Add(receipt, "corrective baseline and installed source versions remain separate", baselineHash + "|" + installedHash + "|True|4",
            mergedMain.declaredSourceSha256 + "|" + mergedMain.installedSourceSha256 + "|" +
            (mergedMain.baselineSourceVersionId != mergedMain.sourceVersionId) + "|" + mergedMain.libraryEvidenceIds.Count);
        var mergedBinding = merged.domainBindings.Find(row => row.bindingId == binding.bindingId);
        var primary = mergedBinding.sources.Find(row => row.role == "declared_primary_source");
        Add(receipt, "binding relation joins explicit alias and original hash without losing installed correction", "guid:main_guid@sha256:" + baselineHash + "|guid:main_guid@sha256:" + installedHash + "|joined_declared_source",
            primary.sourceVersionId + "|" + primary.installedSourceVersionId + "|" + primary.joinState);
        Add(receipt, "technical domain flag and unknown creature eligibility do not admit world assets", "True|True|False|False",
            mergedBinding.domainEligible + "|" + (merged.domainBindings.Find(row => row.domain == "creature").domainEligible == null) + "|" +
            mergedMain.runtimeEligible + "|" + merged.assets.Find(row => row.sourceGuid == "lod_guid").runtimeEligible);
        Add(receipt, "hash conflict remains an explicit unresolved relation", "declared_hash_mismatch_or_unknown",
            merged.domainBindings.Find(row => row.domain == "creature").sources[0].joinState);
        Add(receipt, "historical GUID, ID, key and path survive source aliases", "accepted_stable_id|historical_guid|accepted_old_key|Assets/FixtureLibrary/Old.prefab|True",
            merged.assets.Find(row => row.sourceGuid == historical.sourceGuid).stableAssetId + "|" +
            merged.assets.Find(row => row.sourceGuid == historical.sourceGuid).sourceGuid + "|" +
            merged.assets.Find(row => row.sourceGuid == historical.sourceGuid).sourceAssetKey + "|" +
            merged.assets.Find(row => row.sourceGuid == historical.sourceGuid).assetPath + "|" +
            merged.assets.Find(row => row.sourceGuid == historical.sourceGuid).sourceAliases.Contains("Assets/FixtureLibrary/Moved.prefab"));
        Add(receipt, "unknown held withdrawn and narrow approval scope remain documentary facts", EvidenceStates(master.libraryEvidence), EvidenceStates(merged.libraryEvidence));
        var reordered = JsonConvert.DeserializeObject<YQWorldAssetIntakeBuilder.LibraryCoverageInputs>(before);
        reordered.sources.Reverse(); reordered.bindings.Reverse(); reordered.roots.Reverse(); reordered.master.libraryEvidence.Reverse();
        foreach (var row in reordered.sources) { row.sourceAliases.Reverse(); row.libraryEvidenceIds.Reverse(); row.sourceDeclarationRefs.Reverse(); }
        foreach (var row in reordered.bindings) row.sources.Reverse();
        Add(receipt, "coverage output is deterministic across reordered source and evidence inputs", JsonConvert.SerializeObject(merged),
            JsonConvert.SerializeObject(YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(reordered.master, reordered)));
        Add(receipt, "repeating the same coverage merge is idempotent", JsonConvert.SerializeObject(merged),
            JsonConvert.SerializeObject(YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(merged, inputs)));

        // note: Simulate the old source-only terminal classification without calling its scanner or publication method.
        var future = new YQWorldAssetIntakeBuilder.GenerationInventoryDocument { generatedUtc = "next_fixture_utc" };
        future.assets.Add(JsonConvert.DeserializeObject<YQWorldAssetIntakeBuilder.GenerationInventoryAsset>(JsonConvert.SerializeObject(historical)));
        var sourceOnly = CoverageSource("main_guid", main.assetPath, null, null);
        sourceOnly.reviewPolicyScope = YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit;
        sourceOnly.registryState = "source_asset_only";
        sourceOnly.finalState = "intentionally_excluded_source_without_runtime_binding";
        sourceOnly.reviewDisposition = "intentional_exclusion";
        sourceOnly.reviewPolicyVersion = "world-kit-policy-v1";
        future.assets.Add(sourceOnly);
        var retained = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(future,
            new YQWorldAssetIntakeBuilder.LibraryCoverageInputs { master = merged });
        Add(receipt, "future old caller retains omitted documentary rows and binding history", "3|2|4|2",
            retained.assets.Count + "|" + retained.domainBindings.Count + "|" + retained.libraryEvidence.Count + "|" +
            retained.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LibraryCoverage).Count);
        Add(receipt, "future legacy source scan cannot terminally exclude a coverage unknown", "pending_library_source_review|unknown|LibraryCoverage|False",
            retained.assets.Find(row => row.sourceGuid == "main_guid").finalState + "|" + retained.assets.Find(row => row.sourceGuid == "main_guid").reviewDisposition + "|" +
            retained.assets.Find(row => row.sourceGuid == "main_guid").reviewPolicyScope + "|" + retained.assets.Find(row => row.sourceGuid == "main_guid").runtimeEligible);
    }

    private static YQWorldAssetIntakeBuilder.GenerationInventoryAsset CoverageSource(string guid, string path, string baselineHash, string installedHash)
    {
        string id = "guid:" + guid;
        return new YQWorldAssetIntakeBuilder.GenerationInventoryAsset
        {
            stableAssetId = "source_" + guid, sourceGuid = guid, assetPath = path, assetType = path.EndsWith(".prefab") ? "prefab" : "source_model",
            librarySourceId = id, declaredSourceSha256 = baselineHash, installedSourceSha256 = installedHash,
            baselineSourceVersionId = baselineHash == null ? null : id + "@sha256:" + baselineHash,
            sourceVersionId = installedHash == null ? null : id + "@sha256:" + installedHash, sourceAvailability = "declared",
            declaredSourceKind = baselineHash == null ? null : "Models/Assemblies", registryState = "library_source_accounting_only",
            reviewPolicyScope = YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LibraryCoverage, finalState = "pending_library_source_review",
            finalStateReason = "documentary unknown", reviewDisposition = "unknown", reviewPolicyVersion = "library-coverage-v1",
            classificationStatus = "unknown", placementContextStatus = "unknown", paletteAssignmentStatus = "unknown", technicalValidationStatus = "unknown"
        };
    }

    private static void VerifyDeclaredExportRoots(Receipt receipt)
    {
        // note: Revised main models may retain library contracts; an explicit matching parts payload supplies that contract's package root.
        string original = new string('a', 64), revised = new string('b', 64), parts = new string('c', 64);
        JObject contract = JObject.Parse("{exports:{main:{path:'Family/Main.glb'},parts:{path:'Family/Parts.glb'},lod1:{path:'Family/Lod.glb'}}}");
        contract["exports"]["main"]["sha256"] = original;
        contract["exports"]["parts"]["sha256"] = parts;
        contract["exports"]["lod1"]["sha256"] = new string('d', 64);
        JObject row = new JObject { ["sourcePath"] = "Assets/Revised/Main.glb", ["partsSourcePath"] = "Assets/Library/Parts.glb",
            ["sourceContractJson"] = contract.ToString(Formatting.None) };
        var layout = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["Assets/Revised/Main.glb"] = new JObject { ["path"] = "Assets/Revised/Main.glb", ["origin"] = "Source/Revised/Family/Main.glb", ["sha256"] = revised },
            ["Assets/Library/Parts.glb"] = new JObject { ["path"] = "Assets/Library/Parts.glb", ["origin"] = "Source/Library/Family/Parts.glb", ["sha256"] = parts }
        };
        var binding = new YQWorldAssetIntakeBuilder.GenerationInventoryDomainBinding();
        YQWorldAssetIntakeBuilder.AddEquipmentLodRelations(binding, row, layout);
        Add(receipt, "revised main does not redirect a retained library export contract", "Source/Library/Family/Lod.glb",
            binding.sources.Find(source => source.role == "declared_lod1")?.declaredPath);
        row["sourcePath"] = "Assets/Library/Parts.glb"; row.Remove("partsSourcePath");
        binding = new YQWorldAssetIntakeBuilder.GenerationInventoryDomainBinding();
        YQWorldAssetIntakeBuilder.AddEquipmentLodRelations(binding, row, layout);
        Add(receipt, "parts-only records retain exact declared sibling source relations", "Source/Library/Family/Lod.glb",
            binding.sources.Find(source => source.role == "declared_lod1")?.declaredPath);
        row["sourcePath"] = "Assets/Revised/Main.glb";
        binding = new YQWorldAssetIntakeBuilder.GenerationInventoryDomainBinding();
        YQWorldAssetIntakeBuilder.AddEquipmentLodRelations(binding, row, layout);
        Add(receipt, "path similarity without a matching export hash remains unresolved", "Family/Lod.glb",
            binding.sources.Find(source => source.role == "unresolved_relative_lod1")?.declaredPath);
    }

    private static void VerifyCurrentIntakeMerge(Receipt receipt)
    {
        // note: Synthetic source records isolate current eligibility from old null IDs, exclusions, evidence states and transport.
        const string path = "Assets/FixtureLibrary/Current.prefab", guid = "current_guid";
        var historical = new YQWorldAssetIntakeBuilder.GenerationInventoryAsset
        {
            assetPath = path, sourceGuid = guid, assetType = "prefab", slotTag = "lighting",
            registryState = "catalogued_review_or_quarantine", runtimeEligible = false,
            finalState = "intentionally_excluded_unreviewed_context", finalStateReason = "retained historical exclusion",
            intakeDisposition = "NeedsSpatialReview", reviewDisposition = "intentional_exclusion", reviewPolicyVersion = "world-kit-policy-v1"
        };
        var baseline = new YQWorldAssetIntakeBuilder.GenerationInventoryDocument { schemaVersion = "yq_world_generation_asset_inventory_v6" };
        baseline.assets.Add(historical);
        baseline.libraryEvidence.Add(Evidence("held", YQAssetLibraryApprovalState.Held, "delivery", "held_payload"));
        baseline.libraryEvidence.Add(Evidence("withdrawn", YQAssetLibraryApprovalState.Withdrawn, "delivery", "withdrawn_payload"));
        var rows = new List<JObject>
        {
            IntakeObservation(guid, path, true, 2),
            IntakeObservation("false_guid", "Assets/FixtureLibrary/False.prefab", false, 0),
            IntakeObservation("missing_guid", "Assets/FixtureLibrary/Missing.prefab", null, null),
            IntakeObservation("unsupported_guid", "Assets/FixtureLibrary/Unsupported.prefab", true, 99),
            IntakeObservation("wrong_guid", "Assets/FixtureLibrary/Wrong.prefab", true, 2),
            IntakeObservation(guid, "Assets/FixtureLibrary/Moved.prefab", true, 2)
        };
        string rowsBefore = JsonConvert.SerializeObject(rows), tuples = HistoricalTuples(baseline.assets);
        var inputs = new YQWorldAssetIntakeBuilder.LibraryCoverageInputs { currentIntakeCatalogSnapshotId = "fixture_intake_snapshot_1" };
        inputs.bindings.AddRange(YQWorldAssetIntakeBuilder.BuildLibraryCurrentIntakeBindings(rows, inputs.currentIntakeCatalogSnapshotId));
        inputs.sources.Add(CoverageSource("false_guid", "Assets/FixtureLibrary/False.prefab", null, null));
        inputs.sources.Add(CoverageSource("missing_guid", "Assets/FixtureLibrary/Missing.prefab", null, null));
        inputs.sources.Add(CoverageSource("unsupported_guid", "Assets/FixtureLibrary/Unsupported.prefab", null, null));
        inputs.sources.Add(CoverageSource("other_guid", "Assets/FixtureLibrary/Wrong.prefab", null, null));
        inputs.bindings[0].currentIntakeContract.curationEvidenceIds.Add("technical_receipt");
        inputs.evidence.Add(new YQAssetLibraryEvidenceRecord { contractVersion = 1, evidenceId = "technical_receipt",
            subjectKind = YQAssetLibrarySubjectKind.Asset, subjectSourceGuid = guid,
            approvalScope = "technical_spatial_curation", approvalState = YQAssetLibraryApprovalState.Unknown });
        string inputsBefore = JsonConvert.SerializeObject(inputs);
        var merged = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(baseline, inputs);
        var current = merged.domainBindings.Find(row => row.bindingKey == path);
        Add(receipt, "current projection and merge do not mutate input rows or observations", rowsBefore + "|" + inputsBefore,
            JsonConvert.SerializeObject(rows) + "|" + JsonConvert.SerializeObject(inputs));
        Add(receipt, "historical exclusion and null IDs coexist with true current curation", tuples + "|True|True|True",
            HistoricalTuples(merged.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit)) +
            "|" + current.domainEligible + "|" + (merged.assets[0].stableAssetId == null) + "|" + (merged.assets[0].sourceAssetKey == null));
        Add(receipt, "current false, missing and unsupported remain distinct", "False|True|False|False|True",
            merged.domainBindings.Find(row => row.bindingKey.EndsWith("/False.prefab")).domainEligible + "|" +
            (merged.domainBindings.Find(row => row.bindingKey.EndsWith("/Missing.prefab")).domainEligible == null) + "|" +
            merged.domainBindings.Find(row => row.bindingKey.EndsWith("/Unsupported.prefab")).domainEligible + "|" +
            merged.domainBindings.Find(row => row.bindingKey.EndsWith("/False.prefab")).currentIntakeContract.releaseEligible + "|" +
            (merged.domainBindings.Find(row => row.bindingKey.EndsWith("/Missing.prefab")).currentIntakeContract.releaseEligible == null));
        Add(receipt, "wrong GUID and moved path cannot join intake source identity", "2",
            merged.domainBindings.FindAll(row => row.bindingKey.EndsWith("/Wrong.prefab") || row.bindingKey.EndsWith("/Moved.prefab"))
                .FindAll(row => row.domainEligible == null && row.resolvedAssetPath == null).Count.ToString());
        Add(receipt, "missing serialized transport does not erase current technical eligibility", "True|True|exact_intake_guid_and_path",
            current.domainEligible + "|" + (current.registryGuid == null) + "|" + current.sources[0].joinMethod);
        Add(receipt, "technical curation stays unknown approval beside held and withdrawn", "Unknown|2",
            merged.libraryEvidence.Find(row => row.evidenceId == "technical_receipt").approvalState + "|" +
            merged.libraryEvidence.FindAll(row => row.approvalState == YQAssetLibraryApprovalState.Held ||
                row.approvalState == YQAssetLibraryApprovalState.Withdrawn).Count);
        var repeat = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(merged, inputs);
        Add(receipt, "current observation repeat is idempotent", JsonConvert.SerializeObject(merged), JsonConvert.SerializeObject(repeat));
        var reversed = JsonConvert.DeserializeObject<YQWorldAssetIntakeBuilder.LibraryCoverageInputs>(inputsBefore);
        reversed.bindings.Reverse(); reversed.sources.Reverse(); reversed.evidence.Reverse();
        Add(receipt, "current observations are deterministic under reordered inputs", JsonConvert.SerializeObject(merged),
            JsonConvert.SerializeObject(YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(baseline, reversed)));
        var next = new YQWorldAssetIntakeBuilder.LibraryCoverageInputs { currentIntakeCatalogSnapshotId = "fixture_intake_snapshot_2" };
        next.bindings.AddRange(YQWorldAssetIntakeBuilder.BuildLibraryCurrentIntakeBindings(
            new[] { IntakeObservation(guid, path, false, 0) }, next.currentIntakeCatalogSnapshotId));
        var history = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(merged, next);
        Add(receipt, "changed current snapshot retains old true and current false separately", "fixture_intake_snapshot_2|1|1",
            history.currentIntakeCatalogSnapshotId + "|" + history.domainBindings.FindAll(row => row.bindingKey == path && row.domainEligible == true).Count +
            "|" + history.domainBindings.FindAll(row => row.bindingKey == path && row.domainEligible == false).Count);
        var rescanned = new YQWorldAssetIntakeBuilder.GenerationInventoryDocument();
        var changedHistorical = JsonConvert.DeserializeObject<YQWorldAssetIntakeBuilder.GenerationInventoryAsset>(JsonConvert.SerializeObject(historical));
        changedHistorical.runtimeEligible = true; changedHistorical.finalState = "generation_ready"; changedHistorical.stableAssetId = "new_current_id";
        rescanned.assets.Add(changedHistorical);
        var retained = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(rescanned,
            new YQWorldAssetIntakeBuilder.LibraryCoverageInputs { master = history });
        Add(receipt, "later rescan retains protected historical tuple and observation history", tuples + "|2|fixture_intake_snapshot_2",
            HistoricalTuples(retained.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit)) +
            "|" + retained.domainBindings.FindAll(row => row.bindingKey == path).Count + "|" + retained.currentIntakeCatalogSnapshotId);
        var oldExport = JsonConvert.DeserializeObject<YQWorldAssetIntakeBuilder.GenerationInventoryDocument>(
            "{\"schemaVersion\":\"yq_world_generation_asset_inventory_v6\",\"domainBindings\":[{\"domain\":\"equipment\"}]}");
        Add(receipt, "old v6 export has unknown optional current intake fields", "True|True|yq_world_generation_asset_inventory_v7",
            (oldExport.currentIntakeCatalogSnapshotId == null) + "|" + (oldExport.domainBindings[0].currentIntakeContract == null) + "|" + merged.schemaVersion);
    }

    private static JObject IntakeObservation(string guid, string path, bool? release, int? version)
    {
        return new JObject
        {
            ["stableAssetId"] = "asset_" + guid, ["sourceGuid"] = guid, ["sourceAssetKey"] = "key_" + guid,
            ["assetPath"] = path, ["kitId"] = "fixture_kit", ["semanticRole"] = "wall_deco",
            ["disposition"] = 1, ["releaseEligible"] = release, ["spatialMetadataAuthored"] = release,
            ["curationV2"] = version.HasValue ? new JObject { ["contractVersion"] = version } : null
        };
    }

    private static YQWorldAssetIntakeBuilder.GenerationInventoryDocument VerifyActualCoverage(Receipt receipt)
    {
        // note: Extract current approved authorities directly. The fixture candidate goes only to timestamped outputs after all reads finish.
        const string masterPath = "Assets/Assets/GeneratedAssets/WorldIntake/YQWorldGenerationAssetInventory.json";
        string masterBefore = Hash(masterPath);
        var inputs = YQWorldAssetIntakeBuilder.ReadLibraryCoverageInputs(Directory.GetCurrentDirectory());
        string historicalBefore = HistoricalTuples(inputs.master.assets.FindAll(row =>
            row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit));
        var candidate = YQWorldAssetIntakeBuilder.MergeGenerationInventoryCoverage(inputs.master, inputs);
        foreach (var snapshot in inputs.snapshots) receipt.sourceSnapshotIdentities.Add(snapshot.path + "|sha256=" + snapshot.sha256 + "|basis=" + snapshot.identityBasis);
        Add(receipt, "expanded candidate preserves every canonical historical tuple", historicalBefore,
            HistoricalTuples(candidate.assets.FindAll(row => row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LegacyWorldKit)));
        Add(receipt, "all canonical historical summary counts survive coverage expansion", JsonConvert.SerializeObject(inputs.master.legacyCounts ?? inputs.master.counts), JsonConvert.SerializeObject(candidate.legacyCounts));
        Add(receipt, "world generation admission is unchanged by source ingestion", inputs.master.counts.generationReady.ToString(), candidate.counts.generationReady.ToString());
        Add(receipt, "all 17 explicit roots are observed completely without skipped links or caps", "17|0",
            inputs.observations.Count + "|" + inputs.observations.FindAll(row => !row.complete || row.availability != "present").Count);
        foreach (string domain in new[] { "equipment", "creature" })
        {
            string catalog = "Assets/Assets/Resources/Player/YQDot" + (domain == "equipment" ? "Equipment" : "Creature") + "Catalog.asset";
            int declared = Regex.Matches(File.ReadAllText(catalog), @"(?m)^  - assetId:").Count;
            var bindings = inputs.bindings.FindAll(row => row.domain == domain);
            Add(receipt, domain + " reader preserves every declared catalog entry", declared.ToString(), bindings.Count.ToString());
            Add(receipt, domain + " bindings resolve existing registry GUIDs without loading prefabs", "0",
                bindings.FindAll(row => string.IsNullOrWhiteSpace(row.registryGuid) || string.IsNullOrWhiteSpace(row.resolvedAssetPath)).Count.ToString());
        }
        int eligibleEquipment = Regex.Matches(File.ReadAllText("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset"), @"(?m)^    generationEligible: 1\r?$").Count;
        int falseEquipment = Regex.Matches(File.ReadAllText("Assets/Assets/Resources/Player/YQDotEquipmentCatalog.asset"), @"(?m)^    generationEligible: 0\r?$").Count;
        Add(receipt, "equipment true/false technical eligibility is copied literally", eligibleEquipment + "|" + falseEquipment,
            inputs.bindings.FindAll(row => row.domain == "equipment" && row.domainEligible == true).Count + "|" +
            inputs.bindings.FindAll(row => row.domain == "equipment" && row.domainEligible == false).Count);
        Add(receipt, "creature eligibility remains unknown without a catalog flag", "0",
            inputs.bindings.FindAll(row => row.domain == "creature" && row.domainEligible != null).Count.ToString());
        Add(receipt, "new source rows are unknown and never runtime eligible", "0", candidate.assets.FindAll(row =>
            row.reviewPolicyScope == YQWorldAssetIntakeBuilder.GenerationInventoryReviewPolicyScope.LibraryCoverage &&
            (row.runtimeEligible || row.reviewDisposition != "unknown" || row.finalState != "pending_library_source_review")).Count.ToString());
        var packages = candidate.libraryEvidence.FindAll(row => row?.subjectKind == YQAssetLibrarySubjectKind.Package &&
            row.evidenceId != null && row.evidenceId.StartsWith("tracker-package:", StringComparison.Ordinal));
        Add(receipt, "exact tracker facts remain two held and two withdrawn delivery-denied packages", "2|2|4",
            packages.FindAll(row => row.approvalState == YQAssetLibraryApprovalState.Held).Count + "|" +
            packages.FindAll(row => row.approvalState == YQAssetLibraryApprovalState.Withdrawn).Count + "|" +
            packages.FindAll(row => row.deliveryPermission == YQAssetLibraryDeliveryPermission.Denied && row.approvalScope == "delivery").Count);
        Add(receipt, "tracker package versions never acquire inferred source member links", "0",
            candidate.assets.FindAll(row => row.libraryEvidenceIds.Exists(id => id.StartsWith("tracker-package:", StringComparison.Ordinal))).Count.ToString());
        Add(receipt, "current source document bytes match supplied provenance hashes", "0",
            inputs.conflicts.FindAll(row => row.kind == "provenance_hash_mismatch").Count.ToString());
        Add(receipt, "canonical master file is unchanged by extraction and pure merge", masterBefore, Hash(masterPath));
        VerifyActualCurrentIntake(receipt, inputs, candidate);
        return candidate;
    }

    private static void VerifyActualCurrentIntake(Receipt receipt, YQWorldAssetIntakeBuilder.LibraryCoverageInputs inputs,
        YQWorldAssetIntakeBuilder.GenerationInventoryDocument candidate)
    {
        // note: Exact paths are fixture expectations from the bounded investigation, not approval policy or report-derived authority.
        const string viking = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";
        const string nordic = "Assets/BefourStudios/NordicVillage/Art/Prefabs/";
        const string chests = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/";
        string[] paths = { viking + "SM_House1_Floor.prefab", viking + "SM_StableWooden_Floor.prefab",
            viking + "SM_House2_Roof.prefab", viking + "SM_House2_Door.prefab",
            nordic + "SM_ThatchRoof01.prefab", nordic + "SM_LogRoofGable01.prefab", nordic + "SM_RoofGableTall01.prefab",
            nordic + "SM_Door01.prefab", nordic + "SM_Door02.prefab", nordic + "SM_WallTorch.prefab", nordic + "SM_Shield.prefab",
            chests + "ChestSimpleSmall.prefab", chests + "ChestSimpleMedium.prefab", chests + "ChestOrnateMedium.prefab" };
        int ready = 0, identities = 0, nullIds = 0, historicalExcluded = 0, historicalQuarantined = 0, unknown = 0, transport = 0, joined = 0;
        List<string> missingTransport = new List<string>();
        foreach (string path in paths)
        {
            var current = candidate.domainBindings.Find(row => row.domain == "world_intake" && row.bindingKey == path &&
                row.currentIntakeContract.catalogSnapshotId == candidate.currentIntakeCatalogSnapshotId);
            var old = inputs.master.assets.Find(row => row.assetPath == path);
            if (current == null || old == null) throw new InvalidDataException("Expected current intake identity unavailable: " + path);
            if (current.domainEligible == true && current.currentIntakeContract.releaseEligible == true &&
                current.currentIntakeContract.spatialMetadataAuthored == true && current.currentIntakeContract.dispositionValue == 1 &&
                current.currentIntakeContract.curationContractVersion == 2) ready++;
            if (old.sourceGuid == current.currentIntakeContract.sourceGuid &&
                current.assetId == "asset_" + old.sourceGuid && !string.IsNullOrWhiteSpace(current.currentIntakeContract.sourceAssetKey)) identities++;
            if (old.stableAssetId == null && old.sourceAssetKey == null) nullIds++;
            if (old.finalState == "intentionally_excluded_unreviewed_context") historicalExcluded++;
            if (old.finalState == "quarantined_technical_defect") historicalQuarantined++;
            if (current.sources.Exists(row => row.joinMethod == "exact_intake_guid_and_path")) joined++;
            if (current.registryGuid == null) missingTransport.Add(path); else if (current.registryGuid == old.sourceGuid) transport++;
            foreach (string id in current.currentIntakeContract.curationEvidenceIds)
            {
                var evidence = candidate.libraryEvidence.Find(row => row?.evidenceId == id);
                if (evidence != null && evidence.subjectSourceGuid == old.sourceGuid &&
                    evidence.approvalState == YQAssetLibraryApprovalState.Unknown &&
                    evidence.deliveryPermission == YQAssetLibraryDeliveryPermission.Unknown) unknown++;
            }
        }
        Add(receipt, "14 current technical rows coexist with historical exclusions and quarantines", "14|11|3", ready + "|" + historicalExcluded + "|" + historicalQuarantined);
        Add(receipt, "14 actual intake GUID/path joins retain current IDs and historical nulls", "14|14|14", joined + "|" + identities + "|" + nullIds);
        Add(receipt, "14 actual curation receipts remain unknown independent art approval", "14", unknown.ToString());
        missingTransport.Sort(StringComparer.Ordinal);
        var expectedMissing = new List<string> { nordic + "SM_Door01.prefab", nordic + "SM_Door02.prefab", chests + "ChestSimpleMedium.prefab" };
        expectedMissing.Sort(StringComparer.Ordinal);
        Add(receipt, "three missing serialized transports remain separate from technical curation", "11|" + string.Join("|", expectedMissing),
            transport + "|" + string.Join("|", missingTransport));
        var torch = candidate.domainBindings.Find(row => row.domain == "world_intake" && row.bindingKey == nordic + "SM_WallTorch.prefab" &&
            row.currentIntakeContract.catalogSnapshotId == candidate.currentIntakeCatalogSnapshotId);
        Add(receipt, "current torch wall role does not rewrite historical lighting role", "wall_deco|lighting",
            torch.currentIntakeContract.semanticRole + "|" + candidate.assets.Find(row => row.assetPath == torch.bindingKey).slotTag);
        Add(receipt, "actual current observations preserve all 28199 historical rows and 2878 admission count", "28199|2878",
            candidate.legacyCounts.total + "|" + candidate.legacyCounts.generationReady);
        Add(receipt, "current intake selector names the exact canonical serialized byte snapshot", "serialized-intake:Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset@sha256:" +
            Hash("Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset"), candidate.currentIntakeCatalogSnapshotId);
        Add(receipt, "new intake consumers never claim ordinary runtime proof", "0",
            candidate.domainBindings.FindAll(row => row.domain == "world_intake")
                .FindAll(row => row.consumerEvidence.Exists(item => item.evidenceLevel == YQAssetLibraryConsumerEvidenceLevel.OrdinaryRuntime)).Count.ToString());
    }

    private static string EvidenceStates(List<YQAssetLibraryEvidenceRecord> evidence)
    {
        List<string> states = new List<string>();
        foreach (var row in evidence) states.Add(row.evidenceId + "|" + row.contractVersion + "|" + row.approvalState + "|" +
            row.approvalScope + "|" + row.deliveryPermission + "|" + row.sourceVersionId + "|" + row.payloadSha256);
        states.Sort(StringComparer.Ordinal);
        return string.Join("\n", states);
    }

    private static string HistoricalTuples(List<YQWorldAssetIntakeBuilder.GenerationInventoryAsset> assets)
    {
        // note: Compare the accepted review/admission projection, excluding only newly added documentary fields.
        string[] fields = { "stableAssetId", "sourceGuid", "sourceAssetKey", "assetPath", "assetType", "assetFamily", "sourceRoot", "registryState", "slotTag",
            "runtimeEligible", "finalState", "finalStateReason", "representedByAssetPath", "intakeDisposition", "technicalIssues", "classificationStatus",
            "placementContextStatus", "paletteAssignmentStatus", "technicalValidationStatus", "reviewDisposition", "reviewPolicyVersion", "reviewPolicyScope" };
        List<string> tuples = new List<string>();
        foreach (var asset in assets)
        {
            JObject source = JObject.FromObject(asset), tuple = new JObject();
            foreach (string field in fields) tuple[field] = source[field];
            tuples.Add(tuple.ToString(Formatting.None));
        }
        tuples.Sort(StringComparer.Ordinal);
        return YQWorldAssetIntakeBuilder.LibraryHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", tuples)));
    }

    private static YQWorldAssetIntakeCatalog CreateFixture()
    {
        YQWorldAssetIntakeCatalog catalog = ScriptableObject.CreateInstance<YQWorldAssetIntakeCatalog>();
        catalog.hideFlags = HideFlags.HideAndDontSave;
        return catalog;
    }

    private static YQSpatialAssetRecord Spatial(string guid, string path, bool ready)
    {
        return new YQSpatialAssetRecord
        {
            stableAssetId = "asset_" + guid, sourceGuid = guid, sourceAssetKey = "accepted_old_key", assetPath = path,
            kitId = "fixture_kit", semanticRole = "vegetation", releaseEligible = ready, spatialMetadataAuthored = ready,
            disposition = ready ? YQAssetIntakeDisposition.Candidate : YQAssetIntakeDisposition.NeedsSpatialReview,
            curationV2 = new YQAssetCurationContractV2 { contractVersion = ready ? 2 : 0 }
        };
    }

    private static YQAssetLibraryEvidenceRecord Evidence(string id, YQAssetLibraryApprovalState state, string scope, string payload)
    {
        return new YQAssetLibraryEvidenceRecord
        {
            contractVersion = 1, evidenceId = id, subjectKind = YQAssetLibrarySubjectKind.Package,
            packageId = "synthetic_fixture_package", packageVersion = "fixture_v1", sourceVersionId = "fixture_version_" + id,
            approvalState = state, approvalScope = scope, payloadSha256 = payload, evidenceRef = "synthetic_fixture_evidence"
        };
    }

    private static bool ContainsExpectedFields(JToken actual, JToken expected)
    {
        if (expected is JObject expectedObject)
        {
            if (!(actual is JObject actualObject)) return false;
            foreach (JProperty property in expectedObject.Properties())
                if (!ContainsExpectedFields(actualObject[property.Name], property.Value)) return false;
            return true;
        }
        if (expected is JArray expectedArray)
        {
            if (!(actual is JArray actualArray) || actualArray.Count != expectedArray.Count) return false;
            for (int i = 0; i < expectedArray.Count; i++)
                if (!ContainsExpectedFields(actualArray[i], expectedArray[i])) return false;
            return true;
        }
        return JToken.DeepEquals(actual, expected);
    }

    private static string SnapshotInputs(IReadOnlyList<YQAssetLibraryEvidenceRecord> evidence,
        List<YQSpatialAssetRecord> spatial, List<YQWorldAssetIntakeBuilder.GenerationInventoryAsset> sources)
    {
        // note: Unity's field serializer captures complete spatial records without following Vector3.normalized properties recursively.
        List<string> spatialSnapshots = new List<string>();
        for (int i = 0; i < spatial.Count; i++) spatialSnapshots.Add(JsonUtility.ToJson(spatial[i]));
        return JsonConvert.SerializeObject(evidence) + "\n" + JsonConvert.SerializeObject(spatialSnapshots) + "\n" +
            JsonConvert.SerializeObject(sources);
    }

    private static void RunCheck(Receipt receipt, string name, Action action)
    {
        try { action(); }
        catch (Exception exception) { Add(receipt, name, "no exception", exception.ToString()); }
    }

    private static void Add(Receipt receipt, string name, string expected, string actual)
    {
        receipt.checks.Add(new Check { name = name, expected = expected, actual = actual,
            verdict = string.Equals(expected, actual, StringComparison.Ordinal) ? "PASS" : "FAIL" });
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
