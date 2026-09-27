using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQSiteFunctionContractReviewV2
{
    private const string SourcePath = "Assets/Assets/GeneratedAssets/WorldAssemblies/SemanticProfiles/medieval_viking_village/YQ_MedievalVikingVillage_ReviewedSemanticSite.asset";
    public const string RuntimePath = "Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset";
    public const string ReportPath = "Logs/V2_SettlementFunctionReadiness.md";
    private const string RepairFolder = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates";
    private const string RepairReportPath = "Logs/V2_EntranceRepairCandidates.md";
    private const string VikingPrefabs = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/";

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Populate First Kit Function Candidates")]
    public static void PopulateFirstKitCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before reviewing authored cell contracts.");

        // note: This bounded pass reads the runtime allow-list and one reviewed kit; it never scans or rewrites imported asset packs.
        YQRuntimeWorldSiteCatalog catalog = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>(
            YQRuntimeWorldSiteCatalogBuilder.CatalogPath);
        if (catalog == null)
            throw new InvalidOperationException("Runtime world-site catalog is missing.");
        StringBuilder report = new StringBuilder();
        report.AppendLine("# V2 settlement functional readiness");
        report.AppendLine();
        report.AppendLine("Generated from existing reviewed metadata. This is not a visual approval or a runtime performance test.");
        report.AppendLine();
        report.AppendLine("## Current settlement catalog");
        report.AppendLine();
        int settlements = 0;
        int shellOnly = 0;
        foreach (YQRuntimeWorldSiteRecord site in catalog.Sites)
        {
            if (site == null || site.siteKind != YQAuthoredSiteKind.Settlement)
                continue;
            settlements++;
            if (site.structureUsagePolicy == YQWorldStructureUsagePolicy.ExteriorShellsOnly)
                shellOnly++;
            report.AppendLine("- " + site.kitId + ": " + site.structureUsagePolicy);
        }
        report.AppendLine();
        report.AppendLine("Settlement kits: " + settlements + "; exterior-shell-only: " + shellOnly + ".");
        report.AppendLine();

        YQReviewedSemanticSiteManifest source = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(SourcePath);
        YQReviewedSemanticSiteManifest runtime = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(RuntimePath);
        if (source == null || runtime == null || source.KitId != runtime.KitId ||
            source.SourceSignature != runtime.SourceSignature)
            throw new InvalidOperationException("The first kit's source/runtime manifests are missing or mismatched.");

        report.AppendLine("## First-kit contract drafts");
        report.AppendLine();
        PopulateManifest(source, report, "Reviewed source");
        PopulateManifest(runtime, report, "Runtime metadata copy");
        // note: Measure the shared authored geometry once; source/runtime metadata copies do not need duplicate physics work.
        YQCellPassageReviewV2.AppendReport(runtime, report);
        List<string> selectedZoneIds = new List<string>();
        foreach (YQReviewedSemanticZoneRecord zone in runtime.Zones)
            if (zone != null)
                selectedZoneIds.Add(zone.stableId);
        YQRuntimeWorldSiteRecord runtimeRecord = catalog.FindByKitId(runtime.KitId);
        bool functionReady = YQSiteFunctionContractsV2.TryValidate(runtime, selectedZoneIds,
            new[] { YQAssetFunctionV2.Habitation, YQAssetFunctionV2.Circulation, YQAssetFunctionV2.Service },
            runtimeRecord != null ? runtimeRecord.structureUsagePolicy : YQWorldStructureUsagePolicy.Unspecified,
            out string readinessFailure);
        report.AppendLine("First-kit functional gate: " + (functionReady ? "PASSED" : "NOT READY — " + readinessFailure));
        report.AppendLine();
        report.AppendLine("## Required next review");
        report.AppendLine();
        report.AppendLine("- Replace shell-only habitation with an approved enterable cell or a verified interior transition.");
        report.AppendLine("- Verify entrance and furnishing sockets, standing/head clearance, terrain support and a usable interior floor.");
        report.AppendLine("- Bind service/commerce interaction sockets to actual runtime providers; a sign, workbench mesh or district tag is insufficient.");
        report.AppendLine("- Verify route sockets connect to walkable cell approaches; socket names alone do not establish traversability.");
        report.AppendLine("- Add exact-cell encounter, reward and security evidence before a hostile site can pass.");
        report.AppendLine("- Review materials, foliage LOD transitions and grounding visually before approving or activating V2.");
        report.AppendLine();
        report.AppendLine("No contract was promoted, no structure-usage policy was relaxed, and V2 remains in shadow mode.");

        // note: Only the two existing project-owned manifests receive additive candidate metadata; no scene, prefab, vendor asset or runtime authority is changed.
        AssetDatabase.SaveAssetIfDirty(source);
        AssetDatabase.SaveAssetIfDirty(runtime);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("[YQSiteFunctionReviewV2] Candidate metadata populated. " + settlements +
            " settlement kits, " + shellOnly + " shell-only. Report: " + ReportPath);
    }

    private static void PopulateManifest(YQReviewedSemanticSiteManifest manifest, StringBuilder report, string label)
    {
        int added = YQSiteFunctionContractsV2.PopulateCandidates(manifest);
        bool changed = added > 0;
        int verifiedSocketPaths = 0;
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone == null || zone.cellContractsV2 == null)
                continue;
            foreach (YQReviewedCellFunctionContractV2 binding in zone.cellContractsV2)
            {
                if (binding == null)
                    continue;
                // note: Only measured legacy connection transforms are copied. Clearances and support planes remain unset rather than inventing playable geometry from bounds.
                if (manifest.StreamingSite == null && zone.prefab != null &&
                    binding.reviewState == YQSemanticSiteReviewState.Pending &&
                    binding.curation != null && binding.curation.contractVersion == 0)
                {
                    binding.curation.EnsureCollections();
                    foreach (string path in zone.connectionSocketPaths ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            continue;
                        Transform socket = zone.prefab.transform.Find(path);
                        if (socket == null)
                            continue;
                        verifiedSocketPaths++;
                        bool exists = binding.curation.sockets.Exists(item => item != null && item.transformPath == path);
                        if (exists)
                            continue;
                        binding.curation.sockets.Add(new YQAssetSocketRecordV2
                        {
                            socketId = binding.cellId + ":" + path,
                            kind = YQAssetSocketKindV2.Connection,
                            transformPath = path,
                            localPosition = zone.prefab.transform.InverseTransformPoint(socket.position),
                            localRotation = Quaternion.Inverse(zone.prefab.transform.rotation) * socket.rotation
                        });
                        changed = true;
                    }
                    changed |= CollectDoorCandidates(zone, binding, report, label);
                }
                report.AppendLine("- " + label + " / " + binding.cellId + ": " + binding.reviewState +
                    "; proposed function=" + (binding.curation != null ? binding.curation.primaryFunction.ToString() : "missing") + ".");
            }
        }
        report.AppendLine();
        report.AppendLine(label + ": " + added + " new candidates; " + verifiedSocketPaths + " connection paths found in source prefabs.");
        report.AppendLine();
        if (changed)
            EditorUtility.SetDirty(manifest);
    }

    private static bool CollectDoorCandidates(YQReviewedSemanticZoneRecord zone,
        YQReviewedCellFunctionContractV2 binding, StringBuilder report, string label)
    {
        // note: These two exact imported prefab identities are known separate door leaves; no filename substring is accepted as runtime interaction authority.
        const string house2Door = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_House2_Door.prefab";
        const string house4Door = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_House4_Door.prefab";
        binding.doorBindings ??= new List<YQCellDoorBindingV2>();
        bool changed = false;
        int knownFloorModules = 0;
        int doorLeaves = 0;
        int ambiguous = 0;
        Transform[] hierarchy = zone.prefab.GetComponentsInChildren<Transform>(true);
        foreach (Transform current in hierarchy)
        {
            GameObject original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(current.gameObject);
            if (original == null || original.transform.parent != null)
                continue;
            string sourcePath = AssetDatabase.GetAssetPath(original);
            if (sourcePath == "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_House1_Floor.prefab" ||
                sourcePath == "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_House3_Floor.prefab")
                knownFloorModules++;
            if (sourcePath != house2Door && sourcePath != house4Door)
                continue;
            doorLeaves++;
            string path = AnimationUtility.CalculateTransformPath(current, zone.prefab.transform);
            BoxCollider box = current.GetComponent<BoxCollider>();
            if (box == null || box.isTrigger ||
                !YQCellDoorBindingsV2.TryResolveUniquePath(zone.prefab.transform, path, out Transform resolved) || resolved != current)
            {
                ambiguous++;
                report.AppendLine("  - Door leaf needs unique-path/collider repair: `" + path + "`.");
                continue;
            }
            if (!binding.doorBindings.Exists(item => item != null && item.targetPath == path))
            {
                binding.doorBindings.Add(new YQCellDoorBindingV2
                {
                    bindingId = path,
                    targetPath = path,
                    sourcePrefabGuid = AssetDatabase.AssetPathToGUID(sourcePath),
                    sourceLocalPosition = current.localPosition,
                    sourceLocalRotation = current.localRotation,
                    sourceLocalScale = current.localScale,
                    colliderCenter = box.center,
                    colliderSize = box.size,
                    reviewNote = "Measured separate door leaf; swing direction, standing clearance, interior floor and passage still require review. Not approved."
                });
                changed = true;
            }
            report.AppendLine("  - Measured door candidate: `" + path + "`; imported collider=" + box.size.ToString("F3") + ".");
        }
        report.AppendLine("  - " + label + " / " + zone.stableId + ": known door leaves=" + doorLeaves +
            ", ambiguous/missing-collider leaves=" + ambiguous + ", exact imported floor modules=" + knownFloorModules +
            ", existing door providers=" + zone.prefab.GetComponentsInChildren<YQLockpickableDoor>(true).Length +
            ", existing loot providers=" + zone.prefab.GetComponentsInChildren<YQLockpickableLoot>(true).Length +
            ", existing dialogue providers=" + zone.prefab.GetComponentsInChildren<NpcDialogueAgent>(true).Length + ".");
        report.AppendLine("  - Floor-module counts are source evidence, not proof of furnished or traversable interiors.");
        return changed;
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Build Isolated Entrance Repair Candidates")]
    public static void BuildEntranceRepairCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before constructing review candidates.");
        YQReviewedSemanticSiteManifest manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(RuntimePath);
        YQReviewedSemanticZoneRecord central = null, eastern = null;
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone.stableId == "yq_viking_district_central_village") central = zone;
            if (zone.stableId == "yq_viking_district_eastern_works") eastern = zone;
        }
        if (central?.prefab == null || eastern?.prefab == null)
            throw new InvalidOperationException("Exact first-kit source districts are missing.");
        StringBuilder report = new StringBuilder("# Isolated V2 entrance repair candidates\n\n");
        report.AppendLine("Candidate variants only: not registered with the active catalog, not approved as habitation, and not a visual or player-controller test.");
        report.AppendLine("Source modules, materials and colliders are reused. No invisible support plane or vendor-prefab edits are allowed. Existing candidate files are inspected, never overwritten.");
        report.AppendLine();
        // note: These are explicit first-kit authoring recipes, not filename-based runtime generation rules.
        bool centralReady = BuildEntranceCandidate(central, eastern.prefab, report, true);
        bool easternReady = BuildEntranceCandidate(eastern, eastern.prefab, report, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(RepairReportPath, report.ToString());
        // note: Preserve the diagnostic report, but never report a successful unattended run when a candidate failed its geometry gate.
        if (!centralReady || !easternReady)
            throw new InvalidOperationException("Entrance candidate geometry failed; see " + RepairReportPath);
        Debug.Log("[YQEntranceRepairV2] Isolated candidate results: " + RepairReportPath);
    }

    private static bool BuildEntranceCandidate(YQReviewedSemanticZoneRecord zone, GameObject donor,
        StringBuilder report, bool isCentral)
    {
        string candidatePath = RepairFolder + "/" + zone.stableId + "_entrance_candidate.prefab";
        report.AppendLine("## " + zone.stableId + "\n");
        report.AppendLine("Source dependency hash: `" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(zone.prefab)) + "`.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
            GameObject candidate = (GameObject)PrefabUtility.InstantiatePrefab(saved != null ? saved : zone.prefab, preview);
            if (saved == null)
            {
                // note: Keep the source prefab connection. All changes belong to an unregistered variant until reviewed and deliberately promoted.
                Transform additions = new GameObject("YQ_V2_EntranceRepairs").transform;
                additions.SetParent(candidate.transform, false);
                if (isCentral)
                {
                    Transform door = RequirePath(candidate.transform, "SM_House4_Door11");
                    Transform frame = RequirePath(candidate.transform, "SM_House4_BackWall22");
                    // note: Despite its authored instance name, this object references the front-wall prefab with the doorway opening.
                    RequireSource(frame, "SM_House4_FrontWall");
                    RequireSource(door, "SM_House4_Door");
                    // note: Matching wall orientation and hinge offset establish this door's closed pose; its source leaf is approximately 100 degrees open.
                    if ((frame.localPosition - new Vector3(-27.579903f, 6.6f, -5.4767733f)).sqrMagnitude > 0.0001f ||
                        Quaternion.Angle(frame.localRotation, Quaternion.Euler(0, 90, 0)) > 0.1f)
                        throw new InvalidOperationException("Central entrance frame changed; review the recipe before rebuilding.");
                    door.localRotation = frame.localRotation;
                    CopyHouse4Threshold(donor, door, additions);
                }
                else
                {
                    CopyHouse4Threshold(donor, RequirePath(candidate.transform, "SM_House4_Door6"), additions);
                    AddHouse2Floor(RequirePath(candidate.transform, "SM_House2_Door_64"), additions);
                }
            }

            bool safe = true;
            using (YQCellPassageReviewV2 before = new YQCellPassageReviewV2(zone.prefab))
            using (YQCellPassageReviewV2 after = new YQCellPassageReviewV2(candidate))
            {
                foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                    foreach (YQCellDoorBindingV2 binding in contract.doorBindings)
                    {
                        bool repaired = binding.targetPath == "SM_House4_Door11" || binding.targetPath == "SM_House4_Door6" || binding.targetPath == "SM_House2_Door_64";
                        Quaternion? portal = binding.targetPath == "SM_House4_Door11" ? Quaternion.Euler(0, 90, 0) : (Quaternion?)null;
                        YQCellPassageReviewV2.Result prior = before.Measure(binding.targetPath, portal);
                        YQCellPassageReviewV2.Result current = after.Measure(binding.targetPath);
                        if ((repaired || prior.Clear) && !current.Clear) safe = false;
                        report.AppendLine("- `" + binding.targetPath + "` before: " + prior.Describe());
                        report.AppendLine("  - Candidate: " + current.Describe());
                    }
            }
            if (safe && saved == null)
            {
                if (!AssetDatabase.IsValidFolder(RepairFolder))
                    AssetDatabase.CreateFolder("Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage", "V2EntranceCandidates");
                // note: Save only after every repaired entrance passes and previously clear entrances remain clear.
                if (PrefabUtility.SaveAsPrefabAsset(candidate, candidatePath) == null)
                    throw new InvalidOperationException("Could not save isolated entrance candidate.");
            }
            report.AppendLine();
            report.AppendLine(safe ? "Collision checks passed. Candidate: `" + candidatePath + "`." :
                "Candidate failed collision checks. No new prefab was saved; authored geometry requires further repair.");
            report.AppendLine("Remaining: landing foundations/terrain contact, approach stairs/routes beyond the doorway probe, complete room flooring/furnishing, hinge swing and rendered materials. No function contract is approved.");
            report.AppendLine();
            return safe;
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void CopyHouse4Threshold(GameObject donor, Transform targetDoor, Transform additions)
    {
        Transform donorDoor = RequirePath(donor.transform, "SM_House4_Door5");
        RequireSource(targetDoor, "SM_House4_Door");
        RequireSource(donorDoor, "SM_House4_Door");
        // note: Reuse a measured same-family floor and landing relationship, including authored scale, instead of stretching a generic plane across the settlement.
        CopyHouse4Module(donor, targetDoor, additions, "SM_House1_Floor10", "SM_House1_Floor", "");
        // note: The second panel shares its donor's position but has negative Z scale; it covers the other half of the room.
        CopyHouse4Module(donor, targetDoor, additions, "SM_House1_Floor11", "SM_House1_Floor", "_Mirrored");
        CopyHouse4Module(donor, targetDoor, additions, "SM_WoodenUpPathway_PathwaySection6", "SM_WoodenUpPathway_PathwaySection", "");
    }

    private static bool CopyHouse4Module(GameObject donor, Transform targetDoor, Transform additions,
        string modulePath, string asset, string suffix)
    {
        Transform donorDoor = RequirePath(donor.transform, "SM_House4_Door5");
        Transform module = RequirePath(donor.transform, modulePath);
        RequireSource(targetDoor, "SM_House4_Door");
        RequireSource(donorDoor, "SM_House4_Door");
        GameObject source = RequireSource(module, asset);
        string name = targetDoor.name + "_" + asset + suffix;
        Vector3 position = targetDoor.TransformPoint(donorDoor.InverseTransformPoint(module.position));
        Quaternion rotation = targetDoor.rotation * Quaternion.Inverse(donorDoor.rotation) * module.rotation;
        Transform existing = null;
        foreach (Transform child in additions)
        {
            if (child.name != name) continue;
            if (existing != null) throw new InvalidOperationException("Ambiguous candidate repair module: " + name);
            existing = child;
        }
        if (existing != null)
        {
            // note: Additive upgrades do not reset hand-edited geometry. A changed module must be reviewed instead of silently overwritten.
            RequireSource(existing, asset);
            if ((existing.position - position).sqrMagnitude > 0.0001f ||
                Quaternion.Angle(existing.rotation, rotation) > 0.1f ||
                (existing.localScale - module.localScale).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("Candidate module was edited; preserving it: " + name);
            return false;
        }
        GameObject copy = (GameObject)PrefabUtility.InstantiatePrefab(source, additions.gameObject.scene);
        copy.name = name;
        copy.transform.SetParent(additions, false);
        copy.transform.SetPositionAndRotation(position, rotation);
        copy.transform.localScale = module.localScale;
        return true;
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Complete Candidate Room Floors")]
    public static void CompleteCandidateRoomFloors()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Room-floor authoring requires Edit Mode.");
        const string reportPath = "Logs/V2_RoomFloorReadiness.md";
        YQReviewedSemanticSiteManifest manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(RuntimePath);
        YQReviewedSemanticZoneRecord central = null, eastern = null;
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone.stableId == "yq_viking_district_central_village") central = zone;
            if (zone.stableId == "yq_viking_district_eastern_works") eastern = zone;
        }
        if (central?.prefab == null || eastern?.prefab == null)
            throw new InvalidOperationException("Required first-kit source districts are missing.");
        StringBuilder report = new StringBuilder("# V2 candidate room-floor readiness\n\n");
        report.AppendLine("Bounded collision-only review: five footprint rays, a 1.8m standing capsule, and swept four-neighbour connectivity across explicit room extents. Not visual approval or a player-controller test.");
        report.AppendLine("These first-kit room extents come from the selected house wall layouts. They are not general runtime placement rules. Terrain is absent from this review; no implicit floor or ground is supplied.");
        report.AppendLine();
        int additions = 0;
        bool centralReady = CompleteRoomCandidate(central, eastern.prefab, report, true, ref additions);
        bool easternReady = CompleteRoomCandidate(eastern, eastern.prefab, report, false, ref additions);
        report.AppendLine("Added mirrored imported floor panels: " + additions + ". Source prefabs, GUIDs, active catalog and function approvals are unchanged.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(reportPath, report.ToString());
        if (!centralReady || !easternReady)
            throw new InvalidOperationException("Candidate room-floor checks failed; see " + reportPath);

        // note: Exercise the preservation path in the same editor run without replacing the useful before/after report.
        string centralPath = RepairFolder + "/" + central.stableId + "_entrance_candidate.prefab";
        string easternPath = RepairFolder + "/" + eastern.stableId + "_entrance_candidate.prefab";
        string centralSnapshot = File.ReadAllText(centralPath), easternSnapshot = File.ReadAllText(easternPath);
        int repeatedAdditions = 0;
        StringBuilder repeatedReport = new StringBuilder();
        bool centralRepeat = CompleteRoomCandidate(central, eastern.prefab, repeatedReport, true, ref repeatedAdditions);
        bool easternRepeat = CompleteRoomCandidate(eastern, eastern.prefab, repeatedReport, false, ref repeatedAdditions);
        if (!centralRepeat || !easternRepeat || repeatedAdditions != 0 ||
            centralSnapshot != File.ReadAllText(centralPath) || easternSnapshot != File.ReadAllText(easternPath))
            throw new InvalidOperationException("Repeated room-floor completion changed a saved candidate.");
        Debug.Log("[YQRoomFloorReviewV2] Added " + additions + " mirrored panels; saved candidates unchanged on repeat. Report: " + reportPath);
    }

    private static bool CompleteRoomCandidate(YQReviewedSemanticZoneRecord zone, GameObject donor,
        StringBuilder report, bool isCentral, ref int additions)
    {
        string path = RepairFolder + "/" + zone.stableId + "_entrance_candidate.prefab";
        GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (saved == null || PrefabUtility.GetPrefabAssetType(saved) != PrefabAssetType.Variant)
            throw new InvalidOperationException("A source-linked entrance candidate is required: " + path);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            string house4Door = isCentral ? "SM_House4_Door11" : "SM_House4_Door6";
            Transform repairRoot = RequirePath(contents.transform, "YQ_V2_EntranceRepairs");
            string furniturePath = contents.transform.Find("YQ_V2_InteriorFurnishings") != null ? "YQ_V2_InteriorFurnishings" : null;
            report.AppendLine("## " + zone.stableId + "\n");
            using (YQCellPassageReviewV2 before = new YQCellPassageReviewV2(contents))
                report.AppendLine("- Larger house before: " + MeasureHouseRoom(before, house4Door, false, furniturePath).Describe());
            // note: Upgrade only our isolated candidate and only the missing counterpart. Loading prefab contents preserves the existing variant's base and GUID.
            bool changed = CopyHouse4Module(donor, RequirePath(contents.transform, house4Door), repairRoot,
                "SM_House1_Floor11", "SM_House1_Floor", "_Mirrored");
            bool ready;
            using (YQCellPassageReviewV2 after = new YQCellPassageReviewV2(contents))
            {
                YQCellPassageReviewV2.FloorRegionResult room = MeasureHouseRoom(after, house4Door, false, furniturePath);
                report.AppendLine("- Larger house after: " + room.Describe());
                ready = room.Ready;
                if (!isCentral)
                {
                    YQCellPassageReviewV2.FloorRegionResult small = MeasureHouseRoom(after, "SM_House2_Door_64", true);
                    report.AppendLine("- Smaller house: " + small.Describe());
                    ready &= small.Ready;
                }
                int passages = 0;
                foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                    foreach (YQCellDoorBindingV2 binding in contract.doorBindings)
                    {
                        if (!after.Measure(binding.targetPath).Clear) ready = false;
                        else passages++;
                    }
                report.AppendLine("- Clear entrance probes retained: " + passages + ".");
                ReportOuterApproach(after, house4Door, false, report);
                if (!isCentral) ReportOuterApproach(after, "SM_House2_Door_64", true, report);
            }
            if (ready && changed)
            {
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidOperationException("Could not save candidate room-floor completion.");
                additions++;
            }
            report.AppendLine(ready ? "Room-floor checks passed; no habitation approval granted." : "Room-floor checks failed; this candidate was not saved.");
            report.AppendLine("Remaining: complete exterior approach at actual terrain elevation, foundation contact, furnished/interactable room contracts, swing clearance and visual review. Outer-approach measurements are not an approval gate while terrain is absent.");
            report.AppendLine();
            return ready;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static YQCellPassageReviewV2.FloorRegionResult MeasureHouseRoom(YQCellPassageReviewV2 review,
        string door, bool small, string furniturePath = null)
    {
        // note: Rect X/Y represent door-local X/Z. Insets reserve the standing capsule's clearance from the measured wall lines.
        Rect area = small ? new Rect(-3.2f, -2f, 2.6f, 2.9f) : new Rect(-6.3f, -3.3f, 5.6f, 3.3f);
        float floorY = small ? -0.23335f : -0.16f;
        Vector3 entry = new Vector3(small ? -0.6f : -0.7f, floorY, -0.603f);
        // note: Once furnished, support must still be measured on structural floor while furniture participates in standing and traversal checks.
        return review.MeasureFloorRegion(door, area, floorY, entry, 0.45f, furniturePath);
    }

    private static void ReportOuterApproach(YQCellPassageReviewV2 review, string door, bool small, StringBuilder report)
    {
        // note: This probes authored support 1.2–4m outside the door at entrance grade, not a fabricated flat world beneath the house.
        float floorY = small ? -0.23335f : -0.16f;
        YQCellPassageReviewV2.FloorRegionResult approach = review.MeasureFloorRegion(door,
            new Rect(1.2f, -0.703f, 2.8f, 0.2f), floorY, new Vector3(1.2f, floorY, -0.603f));
        report.AppendLine("- `" + door + "` outer approach at entrance grade (terrain not loaded): " + approach.Describe());
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Review Candidate Door Swings")]
    public static void ReviewCandidateDoorSwings()
    {
        // note: Inspect only the two repaired first-kit candidates; collision findings never promote draft contracts or mutate imported assets.
        YQReviewedSemanticSiteManifest manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(RuntimePath);
        if (manifest == null || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Candidate swing review requires the runtime manifest and Edit Mode.");
        StringBuilder report = new StringBuilder("# V2 candidate door swing clearance\n\n");
        report.AppendLine("Source-linked candidates only. Angular envelopes check closed and intermediate leaf geometry against copied authored collision, with 5mm vertical contact tolerance. Envelope-only warnings receive a bounded 0.125-degree refinement after the two-degree pass. This is conservative collision evidence, not rendered approval, furnishing readiness or a terrain-access check.");
        int checkedDoors = 0, clearDoors = 0;
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone == null || (zone.stableId != "yq_viking_district_central_village" &&
                zone.stableId != "yq_viking_district_eastern_works")) continue;
            string path = RepairFolder + "/" + zone.stableId + "_entrance_candidate.prefab";
            GameObject candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (candidate == null) throw new InvalidOperationException("Missing repaired candidate: " + path);
            report.AppendLine("\n## " + zone.stableId + "\n");
            report.AppendLine("Dependency hash: `" + AssetDatabase.GetAssetDependencyHash(path) + "`.\n");
            using (var review = new YQCellPassageReviewV2(candidate))
                foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                    foreach (YQCellDoorBindingV2 binding in contract.doorBindings)
                    {
                        if (binding == null) continue;
                        var positive = review.MeasureSwing(binding.targetPath, 86);
                        var negative = review.MeasureSwing(binding.targetPath, -86);
                        checkedDoors++;
                        if (positive.Clear || negative.Clear) clearDoors++;
                        report.AppendLine("- `" + binding.targetPath + "` +86: " + positive.Describe());
                        report.AppendLine("  - -86: " + negative.Describe());
                    }
        }
        if (checkedDoors == 0) throw new InvalidOperationException("No candidate door bindings were reviewed.");
        report.AppendLine("\nDoors with at least one clear direction: " + clearDoors + "/" + checkedDoors + ".");
        report.AppendLine("No binding angles, review states, prefabs or active catalog entries were changed. Inspect reported blockers before choosing a hinge direction; an empty passage does not imply a clear leaf swing.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/V2_CandidateDoorSwingReadiness.md", report.ToString());
        Debug.Log("[YQDoorSwingReviewV2] Clear direction for " + clearDoors + "/" + checkedDoors + " candidate doors. Report: Logs/V2_CandidateDoorSwingReadiness.md");
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Repair Small House Candidate Door Fit")]
    public static void RepairSmallHouseCandidateWallCollision()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Candidate collision authoring requires Edit Mode.");
        const string zoneId = "yq_viking_district_eastern_works";
        string path = RepairFolder + "/" + zoneId + "_entrance_candidate.prefab";
        const string reportPath = "Logs/V2_SmallHouseCollisionRepair.md";
        var manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(RuntimePath);
        YQReviewedSemanticZoneRecord zone = null;
        foreach (var item in manifest.Zones) if (item != null && item.stableId == zoneId) zone = item;
        if (zone == null || PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(path)) != PrefabAssetType.Variant)
            throw new InvalidOperationException("The source-linked eastern repair candidate is required.");
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        StringBuilder report = new StringBuilder("# Small-house candidate door fit repair\n\n");
        string[] doors = { "SM_House2_Door_64", "SM_House2_Door2" };
        string[] walls = { "SM_House2_FrontWall_61", "SM_House2_FrontWall2" };
        try
        {
            using (var before = new YQCellPassageReviewV2(contents))
                for (int index = 0; index < doors.Length; index++)
                {
                    Transform door = RequirePath(contents.transform, doors[index]);
                    Transform wall = RequirePath(contents.transform, walls[index]);
                    RequireSource(door, "SM_House2_Door");
                    RequireSource(wall, "SM_House2_FrontWall");
                    report.AppendLine("- `" + doors[index] + "` hinge in wall coordinates: " + wall.InverseTransformPoint(door.position).ToString("F5") +
                        "; relative rotation: " + (Quaternion.Inverse(wall.rotation) * door.rotation).eulerAngles.ToString("F3"));
                    report.AppendLine("  - Before +86: " + before.MeasureSwing(doors[index], 86).Describe());
                    report.AppendLine("  - Before -86: " + before.MeasureSwing(doors[index], -86).Describe());
                }
            // note: Seat only these two leaves on the measured outside frame face. Keep the original cheap wall colliders, leaf dimensions, materials and elevation.
            int changed = 0;
            for (int index = 0; index < walls.Length; index++)
            {
                Transform wall = RequirePath(contents.transform, walls[index]);
                Transform door = RequirePath(contents.transform, doors[index]);
                float movement = SeatSmallHouseLeafOnFrame(door, wall);
                if (movement > 0f) changed++;
                report.AppendLine("- `" + doors[index] + "` outward movement in wall units: " + movement.ToString("F5") +
                    "; fitted hinge: " + wall.InverseTransformPoint(door.position).ToString("F5"));
            }
            bool ready = true;
            using (var after = new YQCellPassageReviewV2(contents))
            {
                foreach (string doorPath in doors)
                {
                    var positive = after.MeasureSwing(doorPath, 86);
                    var negative = after.MeasureSwing(doorPath, -86);
                    ready &= positive.Clear || negative.Clear;
                    report.AppendLine("- `" + doorPath + "` fitted +86: " + positive.Describe());
                    report.AppendLine("  - Fitted -86: " + negative.Describe());
                }
                // note: A swing-only improvement cannot sacrifice any previously clear entrance or either completed interior floor.
                foreach (var contract in zone.cellContractsV2)
                    foreach (var binding in contract.doorBindings)
                        if (binding != null && !after.Measure(binding.targetPath).Clear) ready = false;
                string furniturePath = contents.transform.Find("YQ_V2_InteriorFurnishings") != null ? "YQ_V2_InteriorFurnishings" : null;
                var largeRoom = MeasureHouseRoom(after, "SM_House4_Door6", false, furniturePath);
                var smallRoom = MeasureHouseRoom(after, "SM_House2_Door_64", true);
                ready &= largeRoom.Ready && smallRoom.Ready;
                report.AppendLine("\nLarger room: " + largeRoom.Describe());
                report.AppendLine("Smaller room: " + smallRoom.Describe());
            }
            if (ready && changed > 0 && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                throw new InvalidOperationException("Could not save the verified candidate collision repair.");
            report.AppendLine(ready ? "\nCandidate geometry gates passed. Fitted door leaves: " + changed + "." :
                "\nGeometry gate failed. No candidate changes were saved; outward seating did not establish a safe repair.");
            report.AppendLine("No imported prefab, material, mesh, live catalog or approval state was changed. This does not approve terrain access, furnishings, interactions or rendered appearance.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, report.ToString());
            if (!ready) throw new InvalidOperationException("Small-house collision repair rejected; see " + reportPath);
            Debug.Log("[YQSmallHouseCollisionRepair] Candidate passed; fitted door leaves=" + changed + ". Report: " + reportPath);
        }
        catch (Exception exception)
        {
            // note: Preserve diagnostics even when a source/geometry guard rejects the unsaved staging copy.
            report.AppendLine("\nAborted: " + exception.Message);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(reportPath, report.ToString());
            throw;
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static float SeatSmallHouseLeafOnFrame(Transform door, Transform wall)
    {
        RequireSource(wall, "SM_House2_FrontWall");
        RequireSource(door, "SM_House2_Door");
        if (Quaternion.Angle(door.rotation, wall.rotation) > 0.1f)
            throw new InvalidOperationException("Small-house leaf is not in its frame-aligned closed pose.");
        var group = wall.GetComponent<LODGroup>();
        LOD[] lods = group != null ? group.GetLODs() : Array.Empty<LOD>();
        MeshFilter filter = lods.Length > 0 && lods[0].renderers.Length == 1 && lods[0].renderers[0] != null
            ? lods[0].renderers[0].GetComponent<MeshFilter>() : null;
        BoxCollider leaf = door.GetComponent<BoxCollider>();
        if (filter == null || filter.sharedMesh == null || leaf == null || !leaf.enabled || leaf.isTrigger)
            throw new InvalidOperationException("Required wall geometry or leaf collision is missing.");
        BoxCollider[] boxes = wall.GetComponents<BoxCollider>();
        if (boxes.Length != 3) throw new InvalidOperationException("Expected the source wall's three authored collision boxes.");
        float front = float.NegativeInfinity;
        foreach (BoxCollider box in boxes)
        {
            if (!box.enabled || box.isTrigger) throw new InvalidOperationException("Wall collision was edited; preserve it for review.");
            front = Mathf.Max(front, box.center.x + box.size.x * 0.5f);
        }
        // note: Bounds reads do not cook a mesh. Including the original visible frame avoids seating the leaf inside detail that protrudes beyond its collision boxes.
        Bounds bounds = filter.sharedMesh.bounds;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            front = Mathf.Max(front, wall.InverseTransformPoint(filter.transform.TransformPoint(point)).x);
        }
        Vector3 center = wall.InverseTransformPoint(door.TransformPoint(leaf.center));
        float halfThickness = Mathf.Abs(wall.InverseTransformVector(door.TransformVector(Vector3.right * leaf.size.x * 0.5f)).x);
        float movement = front + halfThickness + 0.006f - center.x;
        if (float.IsNaN(movement) || float.IsInfinity(movement) || movement < -0.0001f || movement > 0.25f)
            throw new InvalidOperationException("Door seating exceeds the bounded 25cm correction or conflicts with an existing edit: " + movement);
        if (movement <= 0.0001f) return 0f;
        door.position += wall.TransformVector(Vector3.right * movement);
        return movement;
    }

    public static void RunSmallHouseCollisionBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0)
            {
                RepairSmallHouseCandidateWallCollision();
                // note: Exercise the saved prefab on a second run; verified door fits must be repeatable without accumulating transform drift or serialization edits.
                string candidatePath = RepairFolder + "/yq_viking_district_eastern_works_entrance_candidate.prefab";
                const string reportPath = "Logs/V2_SmallHouseCollisionRepair.md";
                string candidateSnapshot = File.ReadAllText(candidatePath);
                string firstReport = File.ReadAllText(reportPath);
                RepairSmallHouseCandidateWallCollision();
                if (candidateSnapshot != File.ReadAllText(candidatePath))
                    throw new InvalidOperationException("Repeated small-house fitting changed the saved candidate.");
                File.WriteAllText(reportPath, firstReport + "\nRepeat verification: saved candidate is byte-for-byte unchanged.\n");
                ReviewCandidateDoorSwings();
            }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void RunDoorSwingBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) ReviewCandidateDoorSwings();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RunRoomFloorBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) CompleteCandidateRoomFloors();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void AddHouse2Floor(Transform door, Transform additions)
    {
        RequireSource(door, "SM_House2_Door");
        GameObject module = AssetDatabase.LoadAssetAtPath<GameObject>(VikingPrefabs + "SM_StrongholdWallBase_FloorPiece.prefab");
        BoxCollider bounds = module != null ? module.GetComponent<BoxCollider>() : null;
        if (bounds == null || bounds.isTrigger || bounds.size.x <= 0 || bounds.size.z <= 0)
            throw new InvalidOperationException("The existing stone floor module lacks usable authored collision.");
        // note: Four mildly fitted visible stone tiles fill this 3.9m room footprint; a separate landing continues through the threshold.
        // note: The floor sits 3cm below the existing approach stone to avoid coplanar overlap; no existing support is removed.
        float floorY = -0.23335f;
        for (int x = 0; x < 2; x++)
            for (int z = 0; z < 2; z++)
                PlaceFloorTile(module, bounds, door, additions, "Room_" + x + "_" + z,
                    new Vector3(-0.975f - x * 1.95f, floorY, -0.55f - 0.975f + z * 1.95f), 1.96f, 1.96f);
        PlaceFloorTile(module, bounds, door, additions, "Threshold", new Vector3(0.75f, floorY, -0.603f), 1.8f, 1.6f);
    }

    private static void PlaceFloorTile(GameObject module, BoxCollider bounds, Transform door, Transform parent,
        string id, Vector3 topCenter, float width, float depth)
    {
        GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(module, parent.gameObject.scene);
        tile.name = door.name + "_" + id;
        tile.transform.SetParent(parent, false);
        Vector3 scale = new Vector3(width / bounds.size.x, 1, depth / bounds.size.z);
        Vector3 localTop = bounds.center + Vector3.up * bounds.size.y * 0.5f;
        tile.transform.localScale = scale;
        tile.transform.SetPositionAndRotation(door.TransformPoint(topCenter - Vector3.Scale(localTop, scale)), door.rotation);
    }

    private static Transform RequirePath(Transform root, string path)
    {
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, path, out Transform target))
            throw new InvalidOperationException("Missing or ambiguous authoring target: " + path);
        return target;
    }

    private static GameObject RequireSource(Transform instance, string asset)
    {
        GameObject original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance.gameObject);
        if (original == null || AssetDatabase.GetAssetPath(original) != VikingPrefabs + asset + ".prefab")
            throw new InvalidOperationException("Unexpected source module for " + instance.name + ".");
        return original;
    }

    public static void RunEntranceRepairBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) BuildEntranceRepairCandidates();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RunBatch()
    {
        try
        {
            // note: Test first; failed contracts must not mutate the reviewed metadata during an unattended run.
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0)
                PopulateFirstKitCandidates();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
