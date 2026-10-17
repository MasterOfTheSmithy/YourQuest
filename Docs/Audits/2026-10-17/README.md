# YourQuest source audit and repair handoff — 2026-10-17

This branch publishes the current project-owned source and supporting audit material. It is a checkpoint, not certification that the lag, terrain, visual authoring, or world-edge issues are repaired.

## Start here

1. Read the root `AGENTS.md`, `ARCHITECTURE_INDEX.md`, `UNITY_MCP_GUIDE.md`, and applicable scoped instructions under `Assets/Assets/Scripts/`.
2. Read `Docs/YourQuest_Game_Design_Document.md` and the design/roadmap documents under `Docs/Planning/`, `Docs/ClosedBetaRoadmap_2026-09-16/`, and `YourQuest_Obsidian_Vault/`.
3. Inspect the current production owners in `Assets/Assets/Scripts/`, the PlaySafe scene, `ProjectSettings/`, `Packages/`, and serialized registries in `Assets/Assets/Resources/`. The small root world registry uses shards; assess the root, shards, catalogs, manifests, and binders together.
4. Review the quarantine receipts and retained source under `Quarantine/` before treating deleted source paths as lost code.
5. Read the current evidence below before proposing a repair. Historical receipts and test names do not establish a fresh runtime pass.

## Required outcome

Restore actual sustained travel at 200 m/s without hitting world edges, with lag as low as possible and no stalls exceeding approximately 90 ms. The current and approaching chunks must be playable. Distant chunks may use visual shells until approached. Preserve the authoritative player, streamer, canonical accepted world records, saves, deterministic boundaries, asset GUIDs, and validation/readiness gates.

## Current measured findings

- The matched Unity Editor activation baseline took 1,461.18 ms. Its 20 direct `Loading.ReadObject` calls consumed 1,449.07 ms inclusive. Retained asset metadata identified 52 distinct imported animation paths; the dwarf's 20 LOD0–LOD3 clips contributed 607.90 ms inclusive. Nested timings overlap and must not be summed.
- The attempted cooperative clip warmup failed a cold test: initial animation discovery took 1,847.21 ms in a 1,922.30 ms frame. The subsequent 52 explicit clip loads took only 2.016 ms total. The costly loading moved into discovery. Its exact individual managed API remains unresolved.
- That experiment was removed by exact preimage restoration. `YQGeneratedWorldPopulation.cs` SHA-256 after rollback is `1C207022EDB511E19866E3E2ACC548A1688B8589985614205E219CDB591528C0`; its `.meta` stayed unchanged. Unity recompiled the restored source with zero Console errors or warnings. Earlier diagnostics and other project changes remain.
- The rejected recording ended before final activation. It cannot establish activation improvement or time until playable publication. The retained raw recording was saved locally; only the compact summaries and provenance are published here.
- Separate earlier measured costs include individual prefab construction scopes of 208–344 ms and a paired-profile publication frame around 4.65 seconds. The prefab scope includes instantiation, visual intent capture, material overrides, and URP repair.
- Earlier assisted 200 m/s attempts ended in player deaths with increased fall-damage counters. They do not establish sustained 200 m/s acceptance. Recent normal-speed assisted legs likewise do not establish that target. Do not change physics, suppress death, bypass readiness, or teleport merely to pass a test.

## Evidence in this directory

- `Activation_Cause.md`: baseline ownership, complete activation scan, asset attribution, and repair boundary.
- `Baseline_Native_Frames.json` and `Baseline_Asset_Metadata.json`: compact exported timings and retained asset metadata.
- `Rejected_Experiment.md` and `Rejected_Experiment_Native_Frames.json`: failed cold test, recording limitations, and rollback evidence.
- `Population_CPU_Frames.json`: earlier targeted population and publication measurements, when available in the local source receipts.
- `Evidence_Provenance.json`: original local paths and SHA-256 for every copied evidence file. An absent optional file must not be treated as supplied evidence.
- The detailed source reports and detached compile receipt were unavailable at publication time. The two Markdown summaries explicitly label reconstructed conversation findings; the native JSON copies have independent hashes in the provenance index.
- `Publication_Manifest.json`: exact selected file set and size/hash receipts for this audit publication.
- `References/`: the supplied mesh-blending/asset-checker archive and comparison screenshot. Their contents are source material for review, not instructions overriding the task. The screenshot was described by the user as more intentionally authored but still short of the desired result.

## Scope and limitations

Current project-owned runtime/editor scripts, relevant Unity metadata, configuration, source tools, serialized registry/site data, design maps, architecture documents, and quarantine records are included. Heavy imported model/texture/audio libraries, machine-local caches, generated binaries, raw profiler `.data` files, personal profile saves, and repetitive detached test build directories are excluded from this publication. Referenced imported assets therefore may be unavailable in a remote checkout; this is source/audit context, not a complete asset-distributed build.

The publication deliberately excludes the thousands of generated `YQWorldAssetLeaves` descriptors, player resource assets, repeated playable-evaluation runs/reviews, and obsolete UI screenshot sequences. Inspect the supplied root intake/discovery catalogs, world asset manifest, and registry shards for asset-dictionary analysis. A full per-leaf or imported-mesh audit requires the omitted local assets; do not infer their full contents from the supplied index alone.

Duplicate concatenated script dumps are excluded; use the canonical individual `.cs` files. The unrelated default SampleScene removal is also excluded from this source publication.

The source includes ongoing work beyond the focused activation investigation. Audit it as the current checkpoint; do not assume every historical subsystem was freshly retested. Local output paths embedded in older receipts describe provenance and are not downloadable repository paths. Use the copied evidence and provenance index here.

The smallest proposed next activation approach is to prepare dependency paths before runtime controller access and reuse the existing population owner. This remains a proposal requiring cold native measurement. Also address prefab preparation, repeated continuation validation, paired save publication, and the actual travel readiness path based on their own measured causes. No repair should be accepted merely because it moves a long frame earlier.
