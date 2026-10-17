"""Describe only terrain-refactor edits relative to their captured dirty preimages."""
import difflib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'outputs/TerrainRefactor_20261011'
RUNTIME = 'Assets/Assets/Scripts/Generated/'
CAPTURED = {
    'YQGeneratedWorldRuntimeBuilder.cs': 'preimages',
    'YQSpatialBlueprintV2Validation.cs': 'preimages',
    'YQWorldGenerationV2Contracts.cs': 'preimages',
    'YQSpatialBlueprintCompilerV2.cs': 'basin_preimages',
    'YQSpatialBlueprintV2Contracts.cs': 'basin_preimages',
    'Editor/YQSavedWorldPlacementReviewV2.cs': 'basin_preimages',
    'YQSpatialBlueprintTerrainSamplerV2.cs': 'domain_preimages',
    'YQSpatialMaterializationV2.cs': 'domain_preimages',
    'YQSpatialSiteAccessV2.cs': 'domain_preimages',
    'YQSpatialSiteNetworkCompilerV2.cs': 'domain_preimages',
    'YQContinuousWorldFeatureAuthority.cs': 'domain_preimages',
    'YQGeneratedWorldEnvironment.cs': 'domain_preimages',
    'YQGeneratedWorldTerrain.cs': 'domain_preimages',
    'YQConnectedBasinDomainV2.cs': None,
    'Editor/YQTerrainRefactorBasinVerification.cs': None,
    'Editor/YQTerrainRefactorNativeShoreVerification.cs': None,
}

# note: Exact captured worktree files exclude pre-existing user edits; never compare these files to HEAD or alter the checkout to obtain the diff.
parts = []
for relative, capture in CAPTURED.items():
    source = ROOT / RUNTIME / relative
    original = EVIDENCE / capture / source.name if capture else None
    before = original.read_text(encoding='utf-8').splitlines(True) if original else []
    after = source.read_text(encoding='utf-8').splitlines(True)
    parts.extend(difflib.unified_diff(before, after,
        fromfile='captured-dirty-preimage/' + RUNTIME + relative if original else '/dev/null',
        tofile=RUNTIME + relative))
destination = EVIDENCE / 'ScopePatch.diff'
destination.write_text(''.join(parts), encoding='utf-8')
print(str(destination))
print(''.join(parts).count('\n'), 'lines across', len(CAPTURED), 'files')
