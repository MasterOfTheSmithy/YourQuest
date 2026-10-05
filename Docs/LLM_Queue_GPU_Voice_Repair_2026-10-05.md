# LLM queue, GPU placement and Goddess repair — 2026-10-05

## Scope and roadmap

Source and detached verification for the user-requested NPC generation failures, model placement, queue waiting and Goddess voice. The active roadmap workflow is G08 (`ClosedBetaRoadmap_2026-09-16/goals/G08-Environment-Cohesion.md`). This causal LLM repair also maps to Goal-03's A03 dependency binding, A06/A11 protected prompts and A12 queue/deadline requirements. Neither complete G03 nor complete G08 acceptance is claimed. Preserve accepted legacy decisions and ordinary saves.

## Changed

- NPC population uses the existing typed scheduler. Movement and unrelated save revisions no longer invalidate a batch; profile/world/epoch, exact owner, plan, region and location facts still guard acceptance. Cancellation has its own terminal outcome. Transport does not duplicate domain retries.
- New frontier targets append to the existing population transaction. Canonical NPC records outrank buffered copies, retaining concurrent identities and equipment records.
- NPC active HTTP budget is 240 seconds, with a 300-second safety guard. Waiting behind another request does not spend this active budget. The frontier builder also delegates inference timing to the existing scheduler. Cold NPC planning allowance is 180 seconds, replaced by observed workload timing.
- NPC prompts retain exact canonical facts, names, role rules, schema and final count. Presentation projects the accepted player name, direction and vow rather than questionnaire classifications and optional journey history; it uses the shorter shared speaker rail. Protected compilation rejects overflow instead of middle truncation. Required voice fields and factual validators remain intact.
- Canonical generation requests GPU offload. During live presentation, the approved 32-block Qwen3.5 4B model uses a physical-free-VRAM budget, measured overhead and rendering reserve. No spare capacity or unavailable telemetry selects an explicit CPU fallback. Explicit configurations remain authoritative. Startup-to-gameplay residency changes use the existing clean handoff.
- Approved Goddess CPU model and digest remain unchanged. The versioned voice grammar now permits grounded anxious benevolence and hurried self-correction; uncertain safety and sincere family concerns retain restrained rules. Qualification was renewed against the exact current production CPU request options and validators.

## Fresh evidence

Editor verification is opt-in through `YQLlmRepairVerification`, outside Play Mode, without installing a fixture world or publishing a save. Reports live in `outputs/LlmRepair_20261005/`.

| Check | Result |
| --- | --- |
| Unity compile and assembly reload | Fresh successful compile; current validator assembly identity recorded in review reports |
| Pure GPU, qualification and typed-request contracts | 14 checks, zero failures |
| Existing deterministic frontier continuation contracts | 235 checks, zero failures |
| NPC binding / append / canonical merge | Unrelated changes stable; referenced canon retires work; appended targets unique; buffered work retained; accepted records win |
| Required NPC prompt | Complete body preserved; oversized multilingual ID/schema sentinel rejected explicitly |
| Exact production Goddess CPU wire | 14 real replies; 14 current compositions accepted; approved digest and contract pin match |
| Goddess response timing | 11.390–14.469 seconds; mean 12.7935 seconds; no transport failure in final run |
| Final compact NPC GPU request | 160.031 seconds; 6,106 input and 1,168 output tokens; all three required NPCs accepted by the canonical parser |
| Model cleanup | Owned GPU allocation zero after exit; voice runner absent from `/api/ps` after unload; isolated owned servers terminated |

Final NPC probe used two GPU layers, 12,288 context, four CPU threads, one inference slot and the configured bounded batches, alongside idle Unity. Selected layers reproduce the earlier occupied-GPU free-memory sample of 1,548 MiB; actual probe baseline was 6,685 MiB used, loaded usage 7,340 MiB, owned dedicated allocation 760,913,920 bytes, final usage 6,626 MiB. This is an isolated backend measurement, not the live startup/placement owner or gameplay frame-time proof. Earlier auto-fit and six-layer probes are retained separately and do not certify final placement. The prior two-layer prompt took 180.421 seconds; the compact prompt changes workload, so this is not a controlled hardware performance comparison.

Source hashes, current assembly MVID, reply/case hashes and exact measured results accompany this receipt in `LLM_Queue_GPU_Voice_Repair_2026-10-05.json`. Historical timing and voice fixture inputs are provenance, not fresh acceptance.

## Preserved and remaining

One scheduler, one world/streaming authority, approved role mappings, accepted save records, stable IDs, schemas and NPC domain validators are preserved. Unity was left outside Play Mode. No ordinary profile was selected, regenerated or published by these fixtures.

**NOT VERIFIED:** ordinary PlaySafe startup and queue recovery under movement/profile switching; audible/HUD Goddess delivery and subjective character quality; live placement while rendering, frame times and model-switch hitches; NPC materialization and equipment visuals. Full G03 failure/fairness matrix and G08 settlements, hostile sites, POIs, hydrology, paths, mountains, traversal and visual acceptance remain separate work. Three-minute backend NPC latency requires ahead-of-player planning; these measurements do not justify a claim of instant generation or lag-free play.
