# Prefab dependency source resolution repair — 2026-10-18

## Status and baseline

This bounded follow-up is based on `94c132808183b64bc952be7426fb37952aa094cb` on `repair/chat-audit-2026-10-17`. It addresses the local Unity exception from **YourQuest > Assets > Build Prefab Dependency Preloads**:

```text
Complete imported source and runtime leaf required:
Assets/YourQuest DOT Creatures/cairnback/cairnback_darkness_affinity_set_v1.prefab
```

The reported Git checkout had reached the repair branch successfully. This exception came from the new Editor dependency builder's preflight, before it published descriptors or its dependency manifest.

## Cause and evidence boundary

The builder treated a registry's stable runtime key as the prefab's physical filename. Existing creature intake explicitly preserves historical runtime keys and user-relocated prefab folders; the registry's serialized prefab reference follows its Unity GUID. A valid moved prefab can therefore remain correctly bound at runtime while `File.Exists(binding.path)` rejects its old key. The same assumption also affected dependency discovery, source hashing, and validation, so changing only the first existence guard would not repair the complete operation.

The published cairnback manifest and conserved legacy shard agree on this exact reference:

```yaml
  - assetPath: Assets/YourQuest DOT Creatures/cairnback/cairnback_darkness_affinity_set_v1.prefab
    prefab: {fileID: 5846611850104292756, guid: c5741d01345fda5449c0ff9e0d97c9cf, type: 3}
    material: {fileID: 0}
    materialOverrides: []
```

The normalized entry hash is `7821b2aaca638b2a5c95d72ce0803a59318e63552922a28c1ae8f501f033c359`. Reconstructing the individual leaf using the existing generator gives its published hash, `09232d07b9aa0aa2bc3912f0891adbe2b4f47619cd7cce39ff019daf933132e6`, and resource `YQWorldAssetLeaves/91d84b5f1c2626c2ef1d7ab772e3eec35f2241c60df6f658c3ab6a756262f99c`.

The old exception combined missing-source and missing-leaf checks. The source checkpoint excludes the actual cairnback prefab family and generated leaves, so the error alone cannot establish which local file was absent or where the prefab currently resides. The repair addresses the established stable-key defect and gives separate errors if a required file is genuinely missing.

## Changed

Only `YQRuntimePrefabDependencyBuilder.cs` changes production behavior, in its explicit Editor build and validation operations:

1. Validate the binding manifest owner/version, exact hashed resource name, full leaf hash, sole canonical entry, and conserved entry hash.
2. Read the entry's exact prefab GUID and signed 64-bit file ID from the generated text. Header script GUIDs and material-override GUIDs cannot stand in for the prefab reference.
3. Resolve the current source through `AssetDatabase.GUIDToAssetPath`, require the file to exist, check the GUID round trip with `OnlyExistingAssets`, and require the object at that file ID to be a `GameObject`.
4. Use the resolved physical source for dependency discovery and source hashes in both build and validation. Retain the historical `path` and `entrySha256` in the published dependency manifest.
5. Capture each source hash before dependency discovery and recheck the leaf, GUID mapping, object identity, and source hash before writing descriptors or the manifest.
6. Report missing leaves, missing imported sources, stale leaf hashes, and imported-object identity mismatches separately.

The resolver does not explicitly load every runtime leaf or prefab graph to locate the source. It uses the existing generated text and Unity's metadata APIs. [Unity 6000.3 documents the path/file-ID type query](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.GetTypeFromPathAndFileID.html) and [GUID-to-path resolution](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.GUIDToAssetPath.html). These APIs do not supply a native allocation or performance guarantee.

No imported asset, prefab, scene, `.meta` file, runtime binding key, dependency-manifest schema, gameplay owner, accepted world record, or save format is rewritten by this source repair. Missing assets remain hard failures. No filename guesses, content omissions, or validation bypasses are introduced.

## Verification

The focused verification compiles the actual complete Editor builder and dependency descriptor against explicit controlled Unity boundaries. It exercises public build/validation operations against temporary files, records their dependency and hashing paths, and checks that rejected preflight and preparation changes do not publish output. An independent agent reviews the production diff.

The [verification receipt](Prefab_Dependency_Repair_2026-10-18_Verification.json) retains source hashes, compiler and execution logs, the before/after outcomes, and the serialized compatibility check's script and provenance.

| Check | Result |
| --- | --- |
| Original complete builder compiled against controlled Editor APIs | Passed; no compiler errors |
| Original builder, moved cairnback regression | Reproduced the exact reported exception; expected test exit 1 |
| Repaired complete builder compiled against the same boundary | Passed; no compiler errors |
| Repaired public build, validation, and build-guard regressions | 10 groups passed; 0 failed |
| Published prefab reference/entry/leaf compatibility | 7,801 prefab rows across 104 conserved shards; 0 mismatches |
| Folded paths and file IDs larger than Int32 in that population | 9 folded paths; 2,541 large file IDs accepted |
| Independent production review and patch whitespace checks | Passed |

The repaired source SHA-256 is `4a56e11ef7c8594ec1668b1ffd19cb4c5885ae86ccdc871231b8eab88e5fa2ad`. The portable compilation emitted the existing SDK Newtonsoft.Json `CS1701` reference-unification warning; there were no compiler errors. No source changes followed the successful run or independent review.

Reproduce the repaired managed checks with:

```sh
python Tools/ChatAuditVerification/run_prefab_dependencies.py --dotnet-root /path/to/dotnet --output /path/to/prefab-verification-output
```

These checks do not run Unity, import the user's asset library, or establish cold-activation or travel performance.

## Local Unity follow-up

Pull the repair branch, allow Unity 6000.3.2f1 to finish compiling/importing in Edit mode, and run **YourQuest > Assets > Build Prefab Dependency Preloads** again. Then run **YourQuest > Assets > Validate Prefab Dependency Preloads**.

If the new error specifically says **Missing runtime leaf**, run the existing **YourQuest > Assets > Build Individual Runtime Asset Bindings (Preserve Legacy)** operation, then build and validate dependency preloads again. That operation preserves the existing serialized source bindings; it is not a substitute for restoring a genuinely missing imported source.

If the new error says **Missing imported prefab source** or **Imported prefab GUID/fileID identity mismatch**, retain the exact key, GUID/file ID, and resolved-path message. The original imported source and its `.meta` identity must be available before the builder can complete. Do not replace the creature with a similarly named asset or remove its binding.

After these Editor checks succeed, continue the earlier [beta follow-up](Beta_Progress_02.md): movement admission regression, production PlaySafe activation, save/reload and cancellation, and sustained travel with the existing readiness, visual, and physics requirements. Native Unity acceptance remains outstanding.
