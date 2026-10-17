# YourQuest tools

Project-owned helpers for status, focused checks, asset preparation and research. Paths stay stable because wrappers, documentation and other tools reference them. This catalog does not authorize running every tool or treat a historical receipt as current acceptance.

## Status and source checks

| Entry point | Purpose and boundary |
| --- | --- |
| `node Tools/verify-project-guidance.mjs --guidance-only` | Validates canonical guidance and goal links. |
| `node Tools/project-goal.mjs G08` | Reads the selected complete goal packet; imports the guidance validator. It does not launch work. |
| `node --test Tools/project-guidance.test.mjs` | Focused guidance checks. |
| `Tools/player-animation-status.ps1` | Reads animation owner/source identities and receipts; does not open Unity or certify animation quality. |
| `Tools/yourquest-readonly-mcp/README.md` | Requirements and provenance connector, its documented commands and tests. |
| `python Tools/PlayableEvaluation/compile_current.py --out outputs/<fresh-folder>` | Detached runtime and Editor compilation. It depends on `Tools/InitialModelStack/verify_editor_compile.py`; keep both paths. Compilation is separate from Unity import or gameplay acceptance. |
| `Tools/reconcile-world-asset-library.mjs` | Reads approved catalog/source metadata and writes an investigation report; `--check-only` avoids that report write. It is not an asset importer or approval authority. |

## Verification and retained research

`ContainerVerification`, `ItemAuthoringVerification`, `TerrainRefactor`, `GoddessSpeechVerification` and `VoiceRoleplayVerification` contain scoped verification helpers. Read the specific runner before invocation: some checks compile detached code, while others depend on captured fixtures or explicitly invoke runtime/model services.

`PlayableEvaluation`, `InitialModelStack`, `LLM8GBBenchmark` and `LocalSpeech` hold model, dialogue, speech and build investigations. `AdaptiveContentLoRA/package.json` defines its offline build/check/test/evaluation commands. These are distinct from ordinary source inspection; inference, training, network access or audio use requires the corresponding task scope.

`florence_native_analysis/static_review.py` and `native_vision_result_analysis/analyze.py` reproduce explicitly linked document reviews. They remain useful even though they are standalone historical analysis tools. The latter imports NumPy and analyzes retained results; neither file is a reason to launch inference.

Captured runs, preimages, proposal copies and test outputs are evidence, not redundant active code merely because their bytes match. Keep their provenance and restoration purpose intact. New results belong in a fresh named folder under `outputs/`.

## Asset and documentation preparation

DOT `stage-*`, `prepare-*`, layout and source-layout scripts, plus `Split-YQRuntimeRegistry.ps1`, can write project assets or manifests. They are intentional mutating tools. Inspect their declared inputs, manifests and safeguards before any authorized use; do not run them as cleanup or a generic health check. Associated `verify-dot-*` helpers perform the corresponding delivery checks.

`CreateYourQuestObsidianVault.ps1` writes an Obsidian vault from project planning/design material. It is a document generator, not a read-only status command.

## Application and service control

`KeepUnityFocused.ps1` changes Windows focus/display-awake behavior. `StartYourQuestLlamaServer.ps1` starts the configured local model server. Both require an appropriate live-computer task; source-only work must not invoke them.

## Root launchers and quarantine

The root `Run_Project_Dump_LEAN.bat` uses the existing `YourQuest_ProjectDump_LEAN.ps1`. The separate quarantined old `Run_Project_Dump.bat` expects a missing `YourQuest_ProjectDump.ps1`; its preserved copy is quarantined with a guarded restore utility, not a replacement for the LEAN tool.

Reviewed inactive root artifacts are preserved with a SHA-256 manifest and [guarded restore instructions](../Quarantine/2026-10-16/root/README.md). Active repository tool paths remain unchanged. Never restore over a newer file or infer that an untracked helper is useless.

Retired Unity scripts are preserved separately with their original `.meta` GUIDs in the [script quarantine](../Quarantine/2026-10-16/scripts/README.md). Its manifest identifies the exact files, and its restore helper works without the temporary audit output. The retirement review includes source and serialized-reference checks plus detached compilation; it does not certify Unity import or gameplay.
