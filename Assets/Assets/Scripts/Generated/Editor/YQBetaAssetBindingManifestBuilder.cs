#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQBetaAssetBindingManifestBuilder
{
    public const string ManifestPath =
        "Assets/Assets/Resources/YQBetaAssetBindingManifest.asset";

    private const string CoverageReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQBetaAssetBindingCoverageReport.md";

    private const string PrimaryFamilyKey = "medieval_viking_village";

    [MenuItem("YourQuest/World Generation/Build Approved Beta Binding Manifest")]
    public static void BuildFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[YQBetaAssetBinding] Build requires stable Edit mode.");
            return;
        }

        Build(false);
    }

    public static void BuildBatch()
    {
        bool success = Build(true);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static bool Build(bool logResult)
    {
        YQWorldAssetIntakeCatalog intake = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(
            YQWorldAssetIntakeBuilder.IntakeCatalogPath);
        YQRuntimeWorldSiteCatalog siteCatalog = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldSiteCatalog>(
            "Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset");

        if (intake == null || siteCatalog == null)
        {
            Debug.LogError("[YQBetaAssetBinding] Intake or reviewed site catalog is missing.");
            return false;
        }

        YQBetaAssetBindingManifest manifest = AssetDatabase.LoadAssetAtPath<YQBetaAssetBindingManifest>(ManifestPath);
        if (manifest == null)
        {
            EnsureFolder("Assets/Assets/Resources");
            manifest = ScriptableObject.CreateInstance<YQBetaAssetBindingManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
        }

        List<YQBetaAssetCapabilityRecord> capabilities = BuildWorldCapabilities(siteCatalog);
        List<YQCharacterAppearanceCapabilityRecord> appearance = BuildAppearanceCapabilities();
        List<YQBetaMechanicCapabilityRecord> mechanics = BuildMechanicCapabilities();
        List<YQBetaAssetMigrationFixture> fixtures = BuildMigrationFixtures();

        string sourceIdentity = YQWorldAssetIntakeCatalog.CurrentSchemaVersion + "+" +
            siteCatalog.SchemaVersion + "+" + YQBetaAssetBindingManifest.CurrentBindingVersion;
        manifest.Configure(
            PrimaryFamilyKey,
            sourceIdentity,
            DateTime.UtcNow.ToString("o"),
            capabilities,
            appearance,
            mechanics,
            fixtures);
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssetIfDirty(manifest);

        WriteCoverageReport(manifest, intake, siteCatalog);

        if (logResult)
        {
            Debug.Log("[YQBetaAssetBinding] Published " + capabilities.Count +
                " asset capabilities, " + appearance.Count +
                " appearance records and " + mechanics.Count + " mechanic/effect records.");
        }

        return true;
    }

    private static List<YQBetaAssetCapabilityRecord> BuildWorldCapabilities(
        YQRuntimeWorldSiteCatalog siteCatalog)
    {
        List<YQBetaAssetCapabilityRecord> result = new List<YQBetaAssetCapabilityRecord>();
        AddReviewedSiteCapability(result, siteCatalog, PrimaryFamilyKey,
            YQBetaAssetCapabilityKind.PrimaryWorldFamily,
            "origin,settlement,service,encounter,landmark");
        AddReviewedSiteCapability(result, siteCatalog, "hallowed_depths",
            YQBetaAssetCapabilityKind.Transition,
            "transition,interior,subterranean,encounter");

        string malePath = ResolveFirstExistingPrefab(new[]
        {
            // note: The v4 anchor is the production creature-index path; later vendor revisions remain fallback-only until independently reviewed.
            "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4).prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab"
        });
        AddPathCapability(result, "actor:human_male", "human_male",
            YQBetaAssetCapabilityKind.Actor, malePath,
            "actor,human,humanoid,rigged");

        string femalePath = ResolveFirstExistingPrefab(new[]
        {
            "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Female (v4.1.1).prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Female (v4).prefab"
        });
        AddPathCapability(result, "actor:human_female", "human_female",
            YQBetaAssetCapabilityKind.Actor, femalePath,
            "actor,human,humanoid,rigged");

        string swordPath = ResolveFirstExistingPrefab(new[]
        {
            "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword003/Prefab/Sword003.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword002/Prefab/Sword002.prefab"
        });
        AddPathCapability(result, "equipment:weapon_melee", "weapon_melee",
            YQBetaAssetCapabilityKind.Equipment, swordPath,
            "equipment,weapon,melee,first_person_safe");

        AddRuntimeAlternative(result, "water:surface", "runtime_water_surface",
            YQBetaAssetCapabilityKind.Water,
            "water,surface,terrain_handoff",
            "Supported project-owned water surface runtime alternative; no arbitrary vendor path is exposed.");
        AddRuntimeAlternative(result, "effect:procedural_families", "runtime_vfx_families",
            YQBetaAssetCapabilityKind.Effect,
            "effect,melee,spell,shield,heal",
            "Supported project-owned effect family runtime alternative; visual families remain explicit semantic keys.");

        result.Sort((left, right) => string.Compare(left.capabilityId, right.capabilityId, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static void AddReviewedSiteCapability(
        List<YQBetaAssetCapabilityRecord> result,
        YQRuntimeWorldSiteCatalog siteCatalog,
        string kitId,
        YQBetaAssetCapabilityKind kind,
        string tags)
    {
        YQRuntimeWorldSiteRecord site = siteCatalog.FindByKitId(kitId);
        YQBetaAssetCapabilityRecord record = new YQBetaAssetCapabilityRecord
        {
            capabilityId = "site:" + kitId,
            semanticFamilyId = kitId,
            sourceCatalogId = "YQRuntimeWorldSiteCatalog",
            kind = kind,
            releaseEligible = site != null,
            runtimeVerified = site != null && site.spatiallyValidated &&
                !string.IsNullOrWhiteSpace(site.runtimeManifestResourceKey),
            stableAssetKeys = new List<string> { kitId },
            supportedTags = SplitTags(tags)
        };
        if (site != null)
        {
            // note: Copy reviewed site geometry and placement contracts into the published capability instead of re-deriving them at runtime.
            record.compatibility = BuildSiteCompatibility(site, SplitTags(tags));
            record.assetPaths.Add(site.runtimeManifestResourceKey);
            record.notes.Add("Reviewed site manifest owns geometry, cells, sockets and physical placement evidence.");
            if (!site.seamlessPlacementEligible)
                record.notes.Add("Site remains a bounded transition/interior ingredient; seamless exterior placement is not claimed.");
        }
        else
        {
            record.compatibility = BuildUnavailableCompatibility(SplitTags(tags));
            record.notes.Add("Missing reviewed site candidate; capability is explicitly unavailable.");
        }
        result.Add(record);
    }

    private static void AddPathCapability(
        List<YQBetaAssetCapabilityRecord> result,
        string capabilityId,
        string familyId,
        YQBetaAssetCapabilityKind kind,
        string assetPath,
        string tags)
    {
        YQBetaAssetCapabilityRecord record = new YQBetaAssetCapabilityRecord
        {
            capabilityId = capabilityId,
            semanticFamilyId = familyId,
            sourceCatalogId = "YQRuntimeWorldAssetRegistry",
            kind = kind,
            releaseEligible = !string.IsNullOrWhiteSpace(assetPath),
            runtimeVerified = !string.IsNullOrWhiteSpace(assetPath),
            supportedTags = SplitTags(tags)
        };
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            // note: Derive physical prefab metadata from the approved imported object while the editor has authoritative asset access.
            record.compatibility = BuildPrefabCompatibility(assetPath, familyId, SplitTags(tags));
            record.assetPaths.Add(assetPath);
            record.stableAssetKeys.Add(assetPath.ToLowerInvariant().Replace('/', '_').Replace(' ', '_'));
            if (record.compatibility.shaderMaterialProfile.StartsWith("missing_material_slots", StringComparison.Ordinal))
                record.notes.Add("Source material gaps are repaired by the approved runtime URP material binder before presentation.");
            if (record.compatibility.colliderCount == 0)
                record.notes.Add("Source collider absence is handled by the approved actor/equipment physics binder before interaction.");
        }
        else
        {
            record.compatibility = BuildUnavailableCompatibility(SplitTags(tags));
            record.notes.Add("No approved prefab was found in the current imported set; consumer must use an explicit fallback or remain unavailable.");
        }
        result.Add(record);
    }

    private static void AddRuntimeAlternative(
        List<YQBetaAssetCapabilityRecord> result,
        string capabilityId,
        string familyId,
        YQBetaAssetCapabilityKind kind,
        string tags,
        string note)
    {
        result.Add(new YQBetaAssetCapabilityRecord
        {
            capabilityId = capabilityId,
            semanticFamilyId = familyId,
            sourceCatalogId = "project_owned_runtime",
            kind = kind,
            releaseEligible = true,
            runtimeVerified = true,
            usesRuntimeAlternative = true,
            compatibility = BuildRuntimeAlternativeCompatibility(familyId, SplitTags(tags)),
            stableAssetKeys = new List<string> { familyId },
            supportedTags = SplitTags(tags),
            notes = new List<string> { note }
        });
    }

    private static List<YQCharacterAppearanceCapabilityRecord> BuildAppearanceCapabilities()
    {
        List<YQCharacterAppearanceCapabilityRecord> result = new List<YQCharacterAppearanceCapabilityRecord>();
        AddAppearance(result, "appearance:human_male", "human_male", "Human_v4_Humanoid", "human_head", "human_urp", true);
        AddAppearance(result, "appearance:human_female", "human_female", "Human_v4_Humanoid", "human_head", "human_urp", true);
        AddAppearance(result, "appearance:humanoid_hostile", "humanoid_hostile", "Human_v4_Humanoid", "supported_hostile_head", "supported_hostile_material", false);
        return result;
    }

    private static void AddAppearance(
        List<YQCharacterAppearanceCapabilityRecord> result,
        string id,
        string family,
        string rig,
        string head,
        string material,
        bool verified)
    {
        result.Add(new YQCharacterAppearanceCapabilityRecord
        {
            capabilityId = id,
            actorFamilyId = family,
            rigId = rig,
            headFamilyId = head,
            materialFamilyId = material,
            releaseEligible = verified,
            runtimeVerified = verified,
            supportedMorphs = new List<string>(),
            notes = new List<string> { "No unimplemented morph promises; empty morph list means the beta binding uses the supported authored shape only." }
        });
    }

    private static List<YQBetaMechanicCapabilityRecord> BuildMechanicCapabilities()
    {
        return new List<YQBetaMechanicCapabilityRecord>
        {
            Mechanic("mechanic:melee_swing", "melee", "physical", "melee", true),
            Mechanic("mechanic:spell_pulse", "cast", "arcane", "magic", true),
            Mechanic("effect:shield", "cast", "shield", "magic", true),
            Mechanic("effect:heal", "cast", "heal", "magic", true),
            Mechanic("effect:projectile", "cast", "projectile", "magic", true)
        };
    }

    private static YQBetaMechanicCapabilityRecord Mechanic(
        string id, string animation, string effect, string audio, bool available)
    {
        return new YQBetaMechanicCapabilityRecord
        {
            capabilityId = id,
            mechanicKey = id.Substring(id.IndexOf(':') + 1),
            animationIntent = animation,
            effectFamily = effect,
            audioFamily = audio,
            implementationAvailable = available,
            notes = new List<string> { "Capability is a supported semantic family; it does not grant arbitrary generated behavior." }
        };
    }

    private static YQBetaAssetCompatibilityMetadata BuildSiteCompatibility(
        YQRuntimeWorldSiteRecord site,
        List<string> semanticCompatibility)
    {
        // note: Site metadata preserves the reviewed footprint and datum as the authoritative physical compatibility contract.
        return new YQBetaAssetCompatibilityMetadata
        {
            recommendedScale = Vector3.one,
            footprintSize = site.authoredFootprintSize,
            groundDatum = "authored_foundation_y=" + site.authoredFoundationY.ToString("0.###"),
            pivotDatum = "authored_footprint_center=" + site.authoredFootprintCenter,
            colliderCount = site.activeInstanceCount,
            entranceProfile = "reviewed_site_sockets",
            interiorProfile = site.siteKind == YQAuthoredSiteKind.Dungeon ||
                              site.siteKind == YQAuthoredSiteKind.Interior
                ? "reviewed_enterable_interior"
                : "not_enterable",
            navigationProfile = "reviewed_site_navigation",
            cameraClearanceProfile = "reviewed_streaming_camera_clearance",
            shaderMaterialProfile = "reviewed_runtime_materials",
            semanticCompatibility = semanticCompatibility ?? new List<string>()
        };
    }

    private static YQBetaAssetCompatibilityMetadata BuildPrefabCompatibility(
        string assetPath,
        string familyId,
        List<string> semanticCompatibility)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        Renderer[] renderers = prefab != null
            ? prefab.GetComponentsInChildren<Renderer>(true)
            : Array.Empty<Renderer>();
        Collider[] colliders = prefab != null
            ? prefab.GetComponentsInChildren<Collider>(true)
            : Array.Empty<Collider>();
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        int nonNullMaterialSlots = 0;
        int missingMaterialSlots = 0;
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
                continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (materials[materialIndex] == null)
                    missingMaterialSlots++;
                else
                    nonNullMaterialSlots++;
            }
        }

        return new YQBetaAssetCompatibilityMetadata
        {
            recommendedScale = Vector3.one,
            footprintSize = hasBounds ? bounds.size : Vector3.zero,
            groundDatum = hasBounds ? "renderer_bounds_min_y=" + bounds.min.y.ToString("0.###") : "not_available",
            pivotDatum = "prefab_root_local_origin",
            colliderCount = colliders.Length,
            entranceProfile = familyId == "weapon_melee" ? "not_applicable" : "actor_entry_not_applicable",
            interiorProfile = familyId == "weapon_melee" ? "equipment_not_enterable" : "actor_not_enterable",
            navigationProfile = familyId == "weapon_melee" ? "static_equipment_collision" : "humanoid_agent_reviewed",
            cameraClearanceProfile = familyId == "weapon_melee" ? "first_person_safe" : "humanoid_camera_anchor",
            shaderMaterialProfile = missingMaterialSlots == 0
                ? "bound_material_slots=" + nonNullMaterialSlots + ";urp_review_required"
                : "missing_material_slots=" + missingMaterialSlots,
            semanticCompatibility = semanticCompatibility ?? new List<string>()
        };
    }

    private static YQBetaAssetCompatibilityMetadata BuildRuntimeAlternativeCompatibility(
        string familyId,
        List<string> semanticCompatibility)
    {
        return new YQBetaAssetCompatibilityMetadata
        {
            recommendedScale = Vector3.one,
            footprintSize = Vector3.zero,
            groundDatum = "runtime_owned",
            pivotDatum = "runtime_owned",
            colliderCount = 0,
            entranceProfile = familyId == "runtime_water_surface" ? "terrain_handoff" : "not_applicable",
            interiorProfile = "not_enterable",
            navigationProfile = familyId == "runtime_water_surface" ? "terrain_boundary" : "effect_only",
            cameraClearanceProfile = "runtime_owned",
            shaderMaterialProfile = "project_owned_runtime_binding",
            semanticCompatibility = semanticCompatibility ?? new List<string>()
        };
    }

    private static YQBetaAssetCompatibilityMetadata BuildUnavailableCompatibility(
        List<string> semanticCompatibility)
    {
        return new YQBetaAssetCompatibilityMetadata
        {
            groundDatum = "not_available",
            pivotDatum = "not_available",
            entranceProfile = "not_available",
            interiorProfile = "not_available",
            navigationProfile = "not_available",
            cameraClearanceProfile = "not_available",
            shaderMaterialProfile = "not_available",
            semanticCompatibility = semanticCompatibility ?? new List<string>()
        };
    }

    private static List<YQBetaAssetMigrationFixture> BuildMigrationFixtures()
    {
        return new List<YQBetaAssetMigrationFixture>
        {
            new YQBetaAssetMigrationFixture
            {
                fromVersion = "runtime-world-sites-1.0.0",
                toVersion = YQBetaAssetBindingManifest.CurrentSchemaVersion,
                stableBindingKey = "site:" + PrimaryFamilyKey,
                expectedResolution = PrimaryFamilyKey
            },
            new YQBetaAssetMigrationFixture
            {
                fromVersion = "reviewed-site-binding-3",
                toVersion = YQBetaAssetBindingManifest.CurrentBindingVersion,
                stableBindingKey = "site:" + PrimaryFamilyKey,
                expectedResolution = PrimaryFamilyKey
            }
        };
    }

    private static void WriteCoverageReport(
        YQBetaAssetBindingManifest manifest,
        YQWorldAssetIntakeCatalog intake,
        YQRuntimeWorldSiteCatalog siteCatalog)
    {
        EnsureFolder("Assets/Assets/GeneratedAssets/WorldIntake");
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Approved Beta Asset Binding Coverage");
        report.AppendLine();
        report.AppendLine("- result: " + (manifest != null ? "PUBLISHED" : "BLOCKED"));
        report.AppendLine("- schema: " + (manifest != null ? manifest.SchemaVersion : "missing"));
        report.AppendLine("- binding: " + (manifest != null ? manifest.BindingVersion : "missing"));
        report.AppendLine("- primary family: " + (manifest != null ? manifest.PrimaryFamilyKey : "missing"));
        report.AppendLine("- intake schema: " + intake.SchemaVersion);
        report.AppendLine("- reviewed site catalog: " + siteCatalog.SchemaVersion);
        report.AppendLine("- intake prefab records: " + intake.SpatialAssets.Count);
        report.AppendLine("- reviewed site candidates: " + siteCatalog.Sites.Count);
        HashSet<string> referencedIntakePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int reachableApprovedCapabilities = 0;
        for (int index = 0; index < manifest.Capabilities.Count; index++)
        {
            YQBetaAssetCapabilityRecord capability = manifest.Capabilities[index];
            if (capability == null || !capability.releaseEligible || !capability.runtimeVerified)
                continue;
            reachableApprovedCapabilities++;
            for (int pathIndex = 0; pathIndex < capability.assetPaths.Count; pathIndex++)
            {
                string path = capability.assetPaths[pathIndex] ?? string.Empty;
                if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    referencedIntakePaths.Add(path.Replace('\\', '/'));
            }
        }
        int matchedIntakeRecords = 0;
        for (int index = 0; index < intake.SpatialAssets.Count; index++)
        {
            YQSpatialAssetRecord record = intake.SpatialAssets[index];
            if (record != null && referencedIntakePaths.Contains((record.assetPath ?? string.Empty).Replace('\\', '/')))
                matchedIntakeRecords++;
        }
        List<string> missingBetaFunctions = FindMissingBetaFunctions(manifest);
        report.AppendLine("- reachable approved beta capabilities: " + reachableApprovedCapabilities + "/" + manifest.Capabilities.Count);
        report.AppendLine("- selected-family intake records reachable through published bindings: " + matchedIntakeRecords);
        report.AppendLine("- unreferenced intake entries outside bounded beta family: " + Math.Max(0, intake.SpatialAssets.Count - matchedIntakeRecords));
        report.AppendLine("- missing beta functions: " + (missingBetaFunctions.Count == 0 ? "none" : string.Join(", ", missingBetaFunctions)));
        report.AppendLine();
        report.AppendLine("| Capability | Kind | Release | Runtime verified | Scale | Footprint | Ground/pivot datum | Collider | Entrance | Interior | Navigation | Camera clearance | Shader/material | Semantic compatibility | Binding | Notes |");
        report.AppendLine("|---|---|---:|---:|---|---|---|---:|---|---|---|---|---|---|---|---|");
        foreach (YQBetaAssetCapabilityRecord capability in manifest.Capabilities)
        {
            report.Append("|").Append(capability.capabilityId).Append("|")
                .Append(capability.kind).Append("|").Append(capability.releaseEligible ? "PASS" : "NOT AVAILABLE").Append("|")
                .Append(capability.runtimeVerified ? "PASS" : "NOT VERIFIED").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.recommendedScale.ToString() : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.footprintSize.ToString() : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.groundDatum + " / " + capability.compatibility.pivotDatum : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.colliderCount.ToString() : "0").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.entranceProfile : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.interiorProfile : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.navigationProfile : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.cameraClearanceProfile : "not_available").Append("|")
                .Append(capability.compatibility != null ? capability.compatibility.shaderMaterialProfile : "not_available").Append("|")
                .Append(capability.compatibility != null ? string.Join(", ", capability.compatibility.semanticCompatibility) : "not_available").Append("|")
                .Append(capability.usesRuntimeAlternative ? "project_owned_runtime" : string.Join(", ", capability.assetPaths)).Append("|")
                .Append(string.Join(" ", capability.notes)).AppendLine("|");
        }
        report.AppendLine();
        report.AppendLine("## Character appearance capabilities");
        report.AppendLine();
        report.AppendLine("| Capability | Actor family | Rig | Head | Material | Release | Runtime verified |");
        report.AppendLine("|---|---|---|---|---|---:|---:|");
        foreach (YQCharacterAppearanceCapabilityRecord appearance in manifest.AppearanceCapabilities)
        {
            report.Append("|").Append(appearance.capabilityId).Append("|")
                .Append(appearance.actorFamilyId).Append("|").Append(appearance.rigId).Append("|")
                .Append(appearance.headFamilyId).Append("|").Append(appearance.materialFamilyId).Append("|")
                .Append(appearance.releaseEligible ? "PASS" : "NOT AVAILABLE").Append("|")
                .Append(appearance.runtimeVerified ? "PASS" : "NOT VERIFIED").AppendLine("|");
        }
        report.AppendLine();
        report.AppendLine("## Mechanic and effect capabilities");
        report.AppendLine();
        report.AppendLine("| Capability | Mechanic | Animation | Effect | Audio | Implemented |");
        report.AppendLine("|---|---|---|---|---|---:|");
        foreach (YQBetaMechanicCapabilityRecord mechanic in manifest.MechanicCapabilities)
        {
            report.Append("|").Append(mechanic.capabilityId).Append("|")
                .Append(mechanic.mechanicKey).Append("|").Append(mechanic.animationIntent).Append("|")
                .Append(mechanic.effectFamily).Append("|").Append(mechanic.audioFamily).Append("|")
                .Append(mechanic.implementationAvailable ? "PASS" : "NOT AVAILABLE").AppendLine("|");
        }
        report.AppendLine();
        report.AppendLine("## Migration fixtures");
        report.AppendLine();
        report.AppendLine("| From | To | Stable binding key | Expected resolution | Replay only |");
        report.AppendLine("|---|---|---|---|---:|");
        foreach (YQBetaAssetMigrationFixture fixture in manifest.MigrationFixtures)
        {
            report.Append("|").Append(fixture.fromVersion).Append("|").Append(fixture.toVersion).Append("|")
                .Append(fixture.stableBindingKey).Append("|").Append(fixture.expectedResolution).Append("|")
                .Append(fixture.replayOnly ? "YES" : "NO").AppendLine("|");
        }
        report.AppendLine();
        report.AppendLine("## Explicit limitations");
        report.AppendLine();
        report.AppendLine("- Site-level circulation remains owned by G08; this report certifies ingredients and binding metadata only.");
        report.AppendLine("- Empty appearance morph lists are intentional; no unsupported sculpting or respec capability is claimed.");
        report.AppendLine("- Unverified or missing candidates remain unavailable and are not replaced by placeholders.");
        File.WriteAllText(CoverageReportPath, report.ToString());
    }

    private static List<string> FindMissingBetaFunctions(YQBetaAssetBindingManifest manifest)
    {
        // note: Keep the release gate explicit so an omitted required function cannot hide behind a broad capability count.
        string[] requiredCapabilityIds =
        {
            "site:medieval_viking_village",
            "site:hallowed_depths",
            "actor:human_male",
            "actor:human_female",
            "equipment:weapon_melee",
            "water:surface",
            "effect:procedural_families"
        };
        List<string> missing = new List<string>();
        for (int index = 0; index < requiredCapabilityIds.Length; index++)
        {
            if (!manifest.TryGetCapability(requiredCapabilityIds[index], out YQBetaAssetCapabilityRecord capability) ||
                capability == null || !capability.releaseEligible || !capability.runtimeVerified)
                missing.Add(requiredCapabilityIds[index]);
        }
        return missing;
    }

    private static string ResolveFirstExistingPrefab(IEnumerable<string> candidates)
    {
        foreach (string candidate in candidates ?? Enumerable.Empty<string>())
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null)
                return candidate;
        }
        return string.Empty;
    }

    private static List<string> SplitTags(string tags)
    {
        return (tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim()).Where(tag => tag.Length > 0).ToList();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
#endif
