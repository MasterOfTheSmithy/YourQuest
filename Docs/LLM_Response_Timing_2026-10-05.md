# Local LLM timing and frontier planning repair

## Changed

- Kept the existing approved model assignments, qualified Goddess settings, single `LLMClient` scheduler, and domain acceptance gates.
- Frontier briefs use direct generation on the same control model. Full world plans and quest reasoning retain their existing settings.
- Frontier scheduling now forecasts model service time, active work and queued work, then plans beyond the visible terrain footprint along travel direction. Its measured settlement floor is 180 seconds, with a publication/switch margin. The distance remains bounded at 3,840 metres. Physical terrain prediction, seeds and admission guards are unchanged.
- Settlement briefs have a 240-second active transport budget instead of 120 seconds. Other frontier briefs retain 120 seconds. Population/schema requirements remain unchanged; a tested smaller-population hint was not adopted.
- The hostile prompt explicitly states the existing validator requirement: the leader's faction must match the site's faction.
- Model switches wait for owned llama.cpp process exit or Ollama unload acknowledgement plus an absent runner before loading a replacement. Failed release blocks replacement. Same-model calls remain warm. The game-owned Ollama service permits one loaded model and one concurrent request; external services remain external.

## Existing acceptance reference

The earlier source-bound model evaluation and acceptance work already exists at:

`C:\Users\Garri\.codex\visualizations\2026\10\01\01a0f854-8032-7ae3-96e3-e0306efdc00a\ollama-yourquest-eval`

Relevant reports are `YourQuest-Local-Model-Results.html`, `autorepair-refinement/YourQuest-Autorepair-Refinement.html`, and `goddess-structured-repair/YourQuest-Goddess-Structured-Repair.html`. This run reused the grounded dialogue and qualified native Goddess fixtures, together with current production prompt/compiler/wire methods. The repository's broader acceptance and measurement rules are in `ClosedBetaRoadmap_2026-09-16/LLM-Architecture-Audit_2026-09-28.md`, sections 12–13. Current model routing remains in `Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs`.

## Measured requests

Isolated local services, Unity closed, 4 control-model CPU threads, 12,288-token control context. Two samples per baseline case; elapsed seconds include provider loading when it occurs. These are representative request-category timings, not p95 estimates or ordinary gameplay measurements.

| Request | Assigned model | GPU baseline mean | CPU baseline mean |
|---|---|---:|---:|
| Origin questionnaire | Qwen3.5 4B control GGUF | 9.82 s | — |
| Full world plan | Control | 26.98 s | — |
| NPC location population | Control | 22.81 s | — |
| Quest director | Control | 6.85 s | 97.45 s |
| Progression offer | Control | 2.73 s | 43.26 s |
| Structured canon proposal | Control | 1.12 s | 11.11 s |
| Frontier POI | Control | 6.29 s | 70.42 s |
| Frontier settlement | Control | 14.53 s | 172.39 s completed; second call timed out at 180 s |
| Frontier hostile site | Control | 12.48 s | 116.23 s; both rejected for faction mismatch |
| Grounded dialogue | MiniFantasy Q4_K_M | Separate partial-offload probe below | 16.08 s first / 5.19 s warm |
| Qualified Goddess speech | smaller-test:latest | CPU qualification retained | 16.83 s first / 4.08 s warm |
| Summarization diagnostic | small-test:latest | — | 3.48 s first / 0.92 s warm |
| Default diagnostic | Control | 0.38 s | — |
| Dialogue verifier diagnostic | yourquest-qwen3-4b:latest | — | 5.81 s |
| Goddess verifier diagnostic | yourquest-qwen3-4b:latest | — | 3.17 s |

Verifier lanes remain disabled. Diagnostic transport replies cannot qualify or enable them. Full world/NPC CPU batches were not timed. The NPC fixture uses the base population prompt/profile budget, rather than every live transaction's final count/name constraints and 95-second timeout. The quest fixture has detached owners and an empty snapshot. Summarization/default probes do not certify a domain integration.

### Direct-generation comparison

One exploratory sample per case and placement, same model and unchanged canonical validator/schema. It also included a preference for 2–3 initial residents; the CPU settlement still generated six, and this hint was not adopted.

| Frontier brief | CPU direct comparison | GPU direct comparison |
|---|---:|---:|
| POI | 40.66 s | 2.53 s |
| Settlement | 169.58 s | 10.45 s |
| Hostile | 87.38 s | 6.23 s |

All six comparison replies passed canonical frontier proposal preparation. This is evidence for preserving direct generation and allowing sufficient planning lead, not proof of a general speedup or blinded quality equivalence. Final production payloads were exported separately in `outputs/LlmTiming_20261005/Repaired/Cases.json`; their fresh GPU measurements were **2.22 s POI, 13.38 s settlement, 5.13 s hostile**, and all three passed canonical proposal preparation. No accepted save was published by these probes.

## Unload evidence

- Control GPU startup: 5.34 seconds; total dedicated VRAM rose from 2,847 to 5,975 MiB. After process exit it returned to 2,878 MiB before the CPU reload.
- All tested Ollama model changes acknowledged an empty running-model list before the next model load.
- Partial GPU dialogue probe: 6.64 seconds including 3.96 seconds loading; total VRAM rose from about 4,972 to 6,340 MiB. After unload, Windows attributed only 270,336 dedicated bytes to the owned service/process tree.
- Other model-authoring processes changed total VRAM during testing. The original CPU cleanup total-baseline check failed and was retained in `Measurements.jsonl`; subsequent checks attribute ownership when total usage is ambiguous. No unrelated process was stopped.
- Runtime uses asynchronous service/process acknowledgement rather than running GPU diagnostic commands on gameplay frames. Ollama's documented [unload request](https://docs.ollama.com/api/generate) and [running-model endpoint](https://docs.ollama.com/api/ps) support this handoff.
- The detached Editor runner initially left two CPU servers after exit because its non-playing component did not receive lifecycle teardown. Both recorded test-owned processes were stopped, with no remaining process GPU counters. The runner now explicitly disposes its adapters before exiting; this final Editor-only cleanup change compiled in the reopened editor, but that batch teardown was not rerun. Gameplay's tested model-switch path is unchanged.

## Verification and limits

Fresh Unity compilation and detached planning/normalization/owned-server switch checks are recorded under `outputs/LlmTiming_20261005/`. The real handoff run passed 45 checks; the final production/planning checks passed 35 checks plus 237 fixed-seed continuity/admission checks. Across 52 calls, 51 returned text and one timed out. Canonical preparation accepted 18 of 20 returned frontier proposals; the two original faction-mismatch rejections remain recorded. Final production response checks and current totals are in the accompanying receipt.

No accepted world, player profile, seed, asset binding, save schema, or model qualification was regenerated. Both active save files were checked against pre-test copies. No ordinary PlaySafe traversal, game frame-time distribution, complete NPC batch acceptance, long-session quality, or p95 model latency is certified by this run. Queued foreground dialogue can still wait for an already active background inference; there is one canonical inference slot.

Tools: `YQLlmTimingCaseExport.RunFromCommandLine`, `Tools/measure_llm_response_times.py`, `Tools/measure-frontier-direct.py`, and `YQLlmSpeedVerification.RunFromCommandLine`. Raw prompts/replies and detached save fixtures remain local outputs.
