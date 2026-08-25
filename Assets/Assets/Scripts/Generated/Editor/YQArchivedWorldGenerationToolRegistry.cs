using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class YQArchivedWorldGenerationToolRegistry
{
    public sealed class Record
    {
        public string toolId;
        public string category;
        public string displayName;
        public string archivedReason;
        public string successor;
    }

    private static readonly Record[] ArchivedRecords =
    {
        new Record
        {
            toolId = "wg1.scan_first_benchmark",
            category = "AAA World Generation / Asset Intake",
            displayName = "Scan First Benchmark Kit",
            archivedReason = "The Viking benchmark intake snapshot and scanner calibration are complete.",
            successor = "Asset Intake / Scan Approved Asset Libraries"
        },
        new Record
        {
            toolId = "wg1.repair_benchmark_materials",
            category = "AAA World Generation / Asset Intake",
            displayName = "Repair Benchmark Material Compatibility",
            archivedReason = "All 45 benchmark materials now have verified runtime URP bindings.",
            successor = "Asset Intake Workbench material gates"
        },
        new Record
        {
            toolId = "wg2.analyze_viking_source",
            category = "AAA World Generation / Assembly Extraction",
            displayName = "Analyze Authored Viking Scene",
            archivedReason = "The authored hierarchy and 1,056-instance spatial snapshot have been captured.",
            successor = "Golden assembly authoring and review"
        },
        new Record
        {
            toolId = "wg2.extract_viking_houses",
            category = "AAA World Generation / Assembly Extraction",
            displayName = "Build First Viking Golden Building Candidates",
            archivedReason = "Five unique house archetypes were extracted and duplicate placements were collapsed.",
            successor = "Parcel and street assembly authoring"
        },
        new Record
        {
            toolId = "wg2.extract_viking_diverse_structures",
            category = "AAA World Generation / Assembly Extraction",
            displayName = "Build Viking Diverse Structure Candidates",
            archivedReason = "Stable, windmill, watchtower, outhouse, and curved-bridge candidates were extracted.",
            successor = "Defensive edge-cell and elevated-path extraction"
        },
        new Record
        {
            toolId = "wg2.extract_viking_edges_and_streets",
            category = "AAA World Generation / Assembly Extraction",
            displayName = "Build Viking Edge and Street Cell Candidates",
            archivedReason = "Three defensive and three elevated-path grammar cells were extracted with deterministic connection sockets.",
            successor = "Parcel and street-layout grammar"
        },
        new Record
        {
            toolId = "wg2.build_viking_parcel_grammar",
            category = "AAA World Generation / Assembly Authoring",
            displayName = "Build Viking Parcel Grammar Candidates",
            archivedReason = "Seven frontage-aware parcel candidates and the Viking rural street profile passed visual spatial-contract review.",
            successor = "Settlement Compiler / Build Viking Fixed-Seed Benchmark"
        },
        new Record
        {
            toolId = "wg3.prototype_sparse_parcel_compiler",
            category = "AAA World Generation / Rejected Prototypes",
            displayName = "Build Viking Fixed-Seed Benchmark",
            archivedReason = "Mechanical validation passed, but seven isolated parcels produced a sparse roadside layout rather than an authored settlement. Its validator proved only non-overlap, not visual completeness.",
            successor = "Assembly Authoring / Extract Viking Authored District Candidates"
        },
        new Record
        {
            toolId = "wg4.prototype_blind_semantic_clustering",
            category = "AAA World Generation / Rejected Prototypes",
            displayName = "Run Viking Blind Semantic Clustering Benchmark",
            archivedReason = "Repeated evidence showed that statistical density clusters do not reproduce authored semantic districts: dense eastern works split while intentionally sparse western and southern quarters disappeared.",
            successor = "Semantic Authoring / Compile Reviewed Viking Semantic Districts"
        },
        new Record
        {
            toolId = "wg5.repair_generated_streaming_lods",
            category = "AAA World Generation / Recovery",
            displayName = "Repair Generated Streaming LOD Contracts",
            archivedReason = "The pre-normalization 798-cell batch was repaired successfully. New streaming compilation now fixes LOD ordering and ownership during cell creation.",
            successor = "Production Queue / Review"
        },
        new Record
        {
            toolId = "testing.asset_scene_force_rebuild",
            category = "Testing / Asset Test Scene",
            displayName = "Rebuild Asset Test Scene (Legacy)",
            archivedReason = "The active Build or Refresh command already regenerates and overwrites the complete test scene.",
            successor = "Testing / Asset Test Scene / Build or Refresh"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.rebuild",
            category = "Legacy Runtime Registry",
            displayName = "Rebuild Runtime World Asset Registry",
            archivedReason = "Raw prefab registry rebuilding belongs to the superseded scatter-based world pipeline and must not feed the golden-assembly compiler.",
            successor = "AAA World Generation / Asset Intake"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.rebuild_repair_all",
            category = "Legacy Runtime Registry",
            displayName = "Rebuild and Repair All Procedural Assets",
            archivedReason = "This broad legacy command combines unrelated mutations and is unsafe as a routine production action.",
            successor = "Use the focused Asset Intake and Content Pipeline commands"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.optimize",
            category = "Legacy Runtime Registry",
            displayName = "Optimize Existing Runtime Registry",
            archivedReason = "Oversized legacy registries are already optimized automatically when required; manual use is retained only for recovery.",
            successor = "Automatic registry size guard"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.repair_urp",
            category = "Legacy Runtime Registry",
            displayName = "Repair Existing Runtime Registry to URP",
            archivedReason = "URP compatibility is now established during asset intake before an assembly can be promoted.",
            successor = "AAA World Generation / Asset Intake"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.prune_missing_scripts",
            category = "Legacy Runtime Registry",
            displayName = "Prune Runtime Registry Missing Scripts",
            archivedReason = "This remains a recovery command for old registries, not part of the approved assembly workflow.",
            successor = "Asset intake validation gates"
        },
        new Record
        {
            toolId = "legacy_runtime_registry.hivemind_materials",
            category = "Legacy Runtime Registry",
            displayName = "Build Hivemind URP Material Bindings",
            archivedReason = "Material compatibility now resolves at intake and curated binding rather than by mutating the old runtime registry.",
            successor = "Asset intake material bindings"
        }
    };

    public static IReadOnlyList<Record> Records =>
        ArchivedRecords;

    [MenuItem(
        "Tools/YourQuest/Archived Tools/Open Registry",
        false,
        -100)]
    private static void OpenRegistry()
    {
        YQArchivedWorldGenerationToolRegistryWindow.Open();
    }
}

public sealed class YQArchivedWorldGenerationToolRegistryWindow : EditorWindow
{
    private Vector2 scrollPosition;

    public static void Open()
    {
        YQArchivedWorldGenerationToolRegistryWindow window =
            GetWindow<YQArchivedWorldGenerationToolRegistryWindow>(
                "Archived World Tools");

        window.minSize =
            new Vector2(620f, 320f);

        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Archived YourQuest Editor Tools",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Archived commands remain available for reproducibility and repair, but are removed from the active production menu.",
            MessageType.Info);

        scrollPosition =
            EditorGUILayout.BeginScrollView(
                scrollPosition);

        IReadOnlyList<YQArchivedWorldGenerationToolRegistry.Record> records =
            YQArchivedWorldGenerationToolRegistry.Records;

        for (int index = 0;
             index < records.Count;
             index++)
        {
            YQArchivedWorldGenerationToolRegistry.Record record =
                records[index];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(
                record.displayName,
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Category",
                record.category ?? string.Empty);
            EditorGUILayout.LabelField(
                "ID",
                record.toolId);
            EditorGUILayout.LabelField(
                "Archived because",
                record.archivedReason,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                "Successor",
                record.successor,
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        // note: The registry is informational; archived commands remain explicit submenu items and never run automatically.
        EditorGUILayout.EndScrollView();
    }
}
