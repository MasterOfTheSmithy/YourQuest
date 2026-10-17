#!/usr/bin/env python3
"""Portable registry contracts with explicit Unity API stubs; never Unity build/runtime certification."""
import argparse
import json
import pathlib
import re
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--dotnet-root", type=pathlib.Path, required=True)
args = parser.parse_args()
sdk = sorted((args.dotnet_root / "sdk").iterdir())[-1]
refs = sorted((args.dotnet_root / "packs/Microsoft.NETCore.App.Ref").iterdir())[-1] / "ref/net8.0"
runtime = sorted((args.dotnet_root / "shared/Microsoft.NETCore.App").iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix="yourquest-registry-contracts-") as directory:
    out = pathlib.Path(directory)
    environment = (ROOT / "Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs").read_text()
    catalog = (ROOT / "Assets/Assets/Scripts/Generated/YQWorldAssetCatalog.cs").read_text()

    def method(text, signature, next_signature):
        start = text.index(signature)
        return text[start:text.index(next_signature, start + len(signature))]

    fallback_paths = re.search(r"private static readonly string\[\]\s+ApprovedFallbackUnderstoryPrefabs\s*=\s*\{.*?\};", environment, re.S).group(0)
    approval = method(catalog, "    public static bool IsSpatiallyApprovedForRuntime(", "    public static bool IsRuntimeQuarantinedPath(")
    fallback = method(environment, "    private static void AddApprovedFallbackUnderstoryReferences(", "    private static int ResolveVisibleTreeTarget(")
    unique = method(environment, "    private static void AddUniqueSmallReference(", "    private static GeneratedAssetReferenceRecord")
    fixture = '''using System;
using System.Collections.Generic;
using UnityEngine;
public enum YQAssetIntakeDisposition { NeedsSpatialReview = 0, Candidate = 1 }
public sealed class YQAssetCurationContractV2 { public const int SupportedContractVersion = 2; public int contractVersion; }
public sealed class YQSpatialAssetRecord {
 public string assetPath; public bool releaseEligible, spatialMetadataAuthored;
 public YQAssetIntakeDisposition disposition; public YQAssetCurationContractV2 curationV2;
 public void EnsureCollections() { }
}
public sealed class YQWorldAssetIntakeCatalog : ScriptableObject { public IReadOnlyList<YQSpatialAssetRecord> SpatialAssets; }
public sealed class GeneratedRegionAssetPaletteRecord { public string styleKey; }
public sealed class GeneratedAssetReferenceRecord {
 public string assetKey, assetPath, assetType, slotTag, placementRule, rotationRule, notes;
 public int weight; public float scaleMin, scaleMax, footprintX, footprintZ; public bool allowRepeat, blocksNav;
 public List<string> subTags, styleTags;
}
public static class YQWorldAssetCatalog {
 public const string SlotVegetation = "vegetation";
 private static YQWorldAssetIntakeCatalog _runtimeIntakeCatalog;
 private static bool _runtimeIntakeCatalogLoaded;
 private static IReadOnlyList<YQSpatialAssetRecord> _indexedSpatialAssets;
 private static Dictionary<string, YQSpatialAssetRecord> _spatialAssetsByPath;
 public static void SetFixture(YQWorldAssetIntakeCatalog value) {
  _runtimeIntakeCatalog = value; _runtimeIntakeCatalogLoaded = true; _indexedSpatialAssets = null; _spatialAssetsByPath = null;
 }
 // Unrelated quarantine/style/geometry policies are outside this admission fixture.
 private static bool IsRuntimeQuarantinedPath(string path) => false;
 public static List<GeneratedAssetReferenceRecord> BuildApprovedDiscoveredSlotCandidates(GeneratedRegionAssetPaletteRecord palette, string slot)
  => new List<GeneratedAssetReferenceRecord>();
''' + approval + '''}
public static class YQEnvironmentFallbackFixture {
 public static List<GeneratedAssetReferenceRecord> Apply(GeneratedAssetReferenceRecord accepted) {
  var result = new List<GeneratedAssetReferenceRecord> { accepted };
  AddApprovedFallbackUnderstoryReferences(result, new GeneratedRegionAssetPaletteRecord { styleKey = "nordic_forest" });
  return result;
 }
 private static bool LooksLikeTree(object prefab, GeneratedAssetReferenceRecord reference) => false;
 private static bool IsLargeTerrainFeatureReference(GeneratedAssetReferenceRecord reference) => false;
 private static string BuildReferenceIdentitySemanticText(GeneratedAssetReferenceRecord reference) => reference.assetPath;
 private static bool ContainsAnySemantic(string text, params string[] values) => false;
''' + fallback_paths + '\n' + fallback + unique + '}\n'
    (out / "FallbackFixture.cs").write_text(fixture)
    paths = re.findall(r'"(Assets/[^"\n]+)"', fallback_paths)
    intake = (ROOT / "Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset").read_text()
    records = []
    for block in re.split(r"(?m)^  - stableAssetId: ", intake)[1:]:
        path = re.search(r"(?m)^    assetPath: (.+(?:\n {6}.+)*)", block)
        if not path:
            continue
        path = re.sub(r"\n {6}", " ", path.group(1))
        if path in paths:
            records.append({"assetPath": path, "releaseEligible": "\n    releaseEligible: 1\n" in block,
                            "spatialMetadataAuthored": "\n    spatialMetadataAuthored: 1\n" in block,
                            "disposition": int(re.search(r"(?m)^    disposition: (\d+)", block).group(1)),
                            "curationV2": {"contractVersion": 2 if "    curationV2:\n      contractVersion: 2\n" in block else 0}})
    if len(records) != len(paths):
        raise AssertionError("Every fallback must have a published intake record for this fixture")
    (out / "fallbacks.json").write_text(json.dumps(records))
    sources = [ROOT / "Assets/Assets/Scripts/Generated/YQRuntimeWorldAssetRegistry.cs",
               ROOT / "Assets/Assets/Scripts/Generated/YQRuntimePrefabDependency.cs",
               ROOT / "Tools/ChatAuditVerification/AssetRegistryStubs.cs",
               ROOT / "Tools/ChatAuditVerification/AssetRegistryTests.cs", out / "FallbackFixture.cs"]
    response = ["-nologo", "-langversion:9", "-target:exe", '-out:"' + str(out / "contracts.dll") + '"']
    response += ['-r:"' + str(path) + '"' for path in refs.glob("*.dll")]
    response += ['-r:"' + str(sdk / "Newtonsoft.Json.dll") + '"']
    response += ['"' + str(path) + '"' for path in sources]
    (out / "compile.rsp").write_text("\n".join(response))
    subprocess.run([str(args.dotnet_root / "dotnet"), str(sdk / "Roslyn/bincore/csc.dll"), "@" + str(out / "compile.rsp")], check=True)
    (out / "contracts.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {"tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": runtime}}}))
    (out / "Newtonsoft.Json.dll").write_bytes((sdk / "Newtonsoft.Json.dll").read_bytes())
    subprocess.run([str(args.dotnet_root / "dotnet"), str(out / "contracts.dll"), str(ROOT), str(out / "fallbacks.json")], check=True)
