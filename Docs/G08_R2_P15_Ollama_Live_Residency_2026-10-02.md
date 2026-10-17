# G08-R2 P15 — Ollama live-presentation policy parity

## Scope and evidence boundary

User authorized continuation through the existing Unity background hooks while watching a video, without Computer Use or foreground activation. Editor PID 25632 was identified read-only as YourQuest_PlaySafe, Unity 6000.3.2f1 / DX12. Fresh hook heartbeat confirmed idle Edit, no import or compilation. The old D38 PID/build identity is stale.

This repairs a demonstrated backend policy gap, **not a proven attribution of D38's graphics wait to Ollama**. D19 established paging interference for the old Unity-owned llama.cpp launch; P05 protected automatic live launches there. The subsequent runtime-default change to Ollama bypassed that protection: D38 captured a real WorldDeltaGeneration request and an overlapping CUDA model load, but not its exact paging fence. Current LLMClient still applied batch/thread bounds without placement protection. Windows profiling privileges remain unavailable to the current process; D39 was not run or retried without privileges.

## Bounded change

- `Assets/Assets/Scripts/LLM/LLMClient.cs`: apply `num_gpu=0` only to runtime-default Ollama requests during released gameplay, outside exclusive startup, with `preserveGameResponsiveness`, and only when no explicit `num_gpu` option exists. Explicit values including -1, 0 and positive offload counts are preserved. Explicit configuration assets and startup retain their previous behavior.
- Models, profile overrides, context limits, sampling, JSON contracts, request ownership, cancellation, timeouts and accepted content are unchanged. No global Ollama setting, process termination, server restart, streaming budget, view distance or quality change.
- The Ollama scheduler uses zero GPU layers to choose CPU placement: [upstream scheduler](https://github.com/ollama/ollama/blob/main/server/sched.go). The local version's actual runtime placement still requires observation.
- `YQSemanticChunkRuntimeVerification.cs`: a detached Edit-only policy matrix uses the existing analysis marker. It makes no model call or save mutation. The first fixture comparison failed on float-token representation; both sides now compare serialized wire representations. The first failure is preserved in `outputs/G08_R2_P15_PolicyFixtureFirst_20261002.tsv`.
- `YQTitleScreenUI.cs` and `Editor/YQEditorAutoRefreshBootstrap.cs`: the existing Continue marker can carry an existing profile GUID. It shares the ordinary save-row selection and Continue actions. Blank markers preserve the old behavior; malformed or nonexistent IDs cannot load the currently selected personal save as a fallback. The original parameterless development API remains available. Existing title styling changes are preserved.

Expected measurable effect: automatic live Ollama dispatch explicitly requests CPU placement, closing the P05 protection gap across backends. It may increase LLM response latency; existing request deadlines/outcomes remain authoritative. This is part of the existing residency cause family, not a reset of its repair count. No new scheduler/reservation attempt.

## Verification

Background Unity compilation and domain reload succeeded. Final detached policy matrix: **64/64 PASS**, including default versus explicit config, exclusive startup, presentation release, responsiveness opt-out, explicit GPU values, unchanged original options and model-profile/qualification data.

Receipt: `Logs/G08_R2_P15_PolicyFinal_20261002.ollamapolicy.tsv`. Source and assembly identity before/after preparation: `outputs/G08_R2_P15_BeforeIdentity_20261002.json` and `outputs/G08_R2_P15_Armed_20261002.json`. Exact pre-edit files are retained as `outputs/G08_R2_P15_*Before_20261002.cs.txt`.

One unprofiled background diagnostic is running through the normal title/selected-existing-fixture/Continue hook. Expected profile `3ccfff4df9a1479884061e51940e4457`, seed `4bb221dc`, accepted V2 `c1d6ce9d3e06f760`. The current personal selection was different, so it was not used for the witness. Startup has observed the expected seed. Binary tracing, extra preparation, focus manipulation and Computer Use are absent. The watcher records all fresh status transitions and will copy only a fresh terminal receipt.

## Acceptance

**Full R2 remains FAIL/incomplete.** Background diagnostics cannot certify the 50 ms performance gate. Clean focused PASS, independent focused repeat and remaining full R2 rows are outstanding. D38 readiness misses are not repaired or reclassified by this policy change. R3 remains gated.

## Completed background observation

Run 15:17:14.6602843–15:17:33.4788176 UTC. Immutable receipt: `Docs/G08_R2_Speed260_2026-10-02_151733_P15_Background_Diagnostic_Receipt.md`. Correct profile/world/seed/accepted-V2 identity confirmed. MVID `360f62d2-227d-4433-952f-2b89804e6f8e`; main DLL `02267FB4F84C9E237B305F915102FE25164B3BBCA326BC89BCF755D12B0D4847`. All eight armed file hashes matched after return to idle Edit.

Ordinary motor matrix, abrupt transition, boundary crossing, direction coverage, visible/turn/ground coverage, zero fallback and zero fall recovery passed their observed checks. Input/coast maxima were 54/63 ms, with 79/273 unfocused frames. Viewport was 616x264, changed from earlier focused captures. **INVALID for certification or a matched performance comparison.**

Most significantly, the reported 260 m/s coast travelled only 13.9 m in 7.664 s. Its final capsule was at (29.994,67.529,-3.447), with 3,809 side contacts against `YQ_GENERATED_WORLD_RUNTIME/YQ_GENERATED_TERRAIN`; contact normal (0.001,0.122,-0.992). Internal velocity decayed while the capsule remained blocked. This does not exercise sustained streaming demand and must not support a readiness or capacity PASS. The user explicitly requested repair of this test route next; P16 addresses that verifier defect, without changing production collision.

Ollama launch at 15:17:27 UTC contained `-ngl 0`, context12288, batch128 and threads4 for the unchanged model blob. This confirms the new request policy reached the actual backend. Normal Play teardown cancelled the WorldDeltaGeneration request after6.95 s before loading completed. The later `/api/ps` response had no loaded models; it is not a successful inference or latency result. Evidence: `outputs/G08_R2_P15_OllamaLiveRequest_20261002.txt`, `...UnityLiveRequest...txt`, `...OllamaPlacement...json`.

No clean closure attempt or repeat is inferred from this invalid background/obstructed run. P05 residency repair history and exhausted scheduling/reservation history remain intact.
