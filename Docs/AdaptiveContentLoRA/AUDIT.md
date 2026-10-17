# Current runtime audit

Audit date: October 1, 2026. Source inspection and fresh metadata/hardware checks establish identity and implementation facts; they do not certify loaded Unity state, compilation or execution.

The configured model is `C:\Ai\Text Models\Qwen3.5-4B-Q4_K_M.gguf`, selected by `Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs:59`. No serialized override was found in the targeted project-owned scene/resource/asset search. The bootstrap creates the ordinary LLM client. An existing healthy server can be reused, so source defaults alone cannot certify a server's loaded weights. The read-only model-list probe timed out; no server was started.

The file is 2,707,513,856 bytes, GGUF v3 Q4_K_M, `qwen35`, 426 tensors, 32 blocks, hidden size 2560 and full attention every fourth block. The embedded tokenizer is `gpt2`/`qwen35`, with 248,320 token entries. Complete fingerprints, metadata and checks are in `Tools/AdaptiveContentLoRA/audit.json`.

```text
GGUF SHA-256
5d4978a0065869fb353034a4fe41aec32ea47722399a617ee049c23c6ba1843c

Embedded Jinja template UTF-8 SHA-256
7f0e529032c25183bcd66c7f238da2d377f43be754a94e2725a58c4e16d2ed67

llama-server-impl.dll SHA-256
b4590de3b49eb7c2bb4d1e4774760f202828d5749c37eec34347398c45779550
```

llama.cpp reports build 10516, commit `b95502ba9`, with adapter-loading CLI flags. Qwen3.5-specific PEFT conversion/loading was not tested. The runtime uses a 12,288-token context and one parallel request. Current progression requests use StructuredState, 420 output tokens, a 40-second timeout and no retries. JSON mode is enabled without the requested domain schema.

The local GGUF differs from the current publisher's Q4_K_M file hash. The public history exposes upload commit `720bb03`; its historical hash was not retrievable with available tools. Exact-hash search found no result. A narrow search found no adjacent download/manifest/checksum metadata or matching default cached training weights. Direct metadata API access failed. **No matching source safetensors/tokenizer revision has been established.** A quantized hash cannot recover that mapping by itself. [Publisher file](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/blob/main/Qwen3.5-4B-Q4_K_M.gguf), [file history](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/commits/main/Qwen3.5-4B-Q4_K_M.gguf)

Hardware checks identify an RTX 5060 with 8,151 MiB VRAM, Ryzen 7 5800X/16 logical processors and approximately 32 GiB RAM. The initial snapshot had 7,247 MiB VRAM used. After reported Unity closure, the refresh showed **744 MiB used, 7,152 MiB free**, 1% GPU utilization and a remaining small Unity process. These are point-in-time observations, not a memory budget guarantee.

The discovered Python environment failed to launch; direct Python execution was denied, and WSL reported not installed. Package metadata exists but imports were not tested. Current Qwen3.5 guidance estimates about 10 GB for 4B BF16 LoRA and cautions against its 4-bit training path. A runnable, isolated larger-GPU environment is the preferred provisional route; local 8 GB QLoRA is not assumed viable. Text-only Transformers support and backend adapter flags establish documented possibilities, not tested compatibility. [Training guidance](https://unsloth.ai/docs/models/qwen3.5/fine-tune), [Transformers](https://huggingface.co/docs/transformers/main/en/model_doc/qwen3_5), [installed-commit converter](https://github.com/ggml-org/llama.cpp/blob/b95502ba9/convert_lora_to_gguf.py)

| Owner/source | Established implementation | Required boundary still missing or unverified |
|---|---|---|
| `LLM/YQLlmRequest.cs`, `LLM/LLMClient.cs:1585` | Priorities, cancellation, epochs, profile/world and revision checks, terminalization | Relevance-specific entity/evidence dependencies and production lifecycle proof |
| `LLM/LLMContextCompiler.cs` | Character-estimated compaction and middle truncation | Actual tokenizer accounting and preservation of required schema/facts |
| `LLM/LlamaCppServerProcess.cs:287` | Existing live-presentation CPU safeguard | Nonempty extraServerArgs bypasses this branch; adding --lora casually is unsafe |
| `Generated/YQBetaAssetBindingManifest.cs:233`, Resources manifest | Five melee/pulse/shield/heal/projectile records | Complete progression-linked target/cost/power/executor validator |
| `Generated/Editor/YQBetaAssetBindingCertification.cs`, CheckMechanics | Checks availability flags and populated semantic/presentation fields | Does not prove execution correctness |
| `Tutorial/YQInvestorCombat.cs:195` | Current resource/cooldown and spell execution path | Generated descriptors affect projectile routing; inspected self/buff branch can reach damaging pulse |
| `Observation/Models/ActionEvent.cs`, ActionRecorder, EventAccumulator | Verbs/positions/runtime targets/counters and a shared transient buffer | Stable evidence IDs, tools, attempt/outcome facts, independent cursors and durable hypotheses |
| `Gameplay/Systems/Progression/PlayerBehaviorRollup.cs` | Time-window prose rollup and counters | Durable G15 evidence/candidate contract |
| `ProgressionThinkCycle.cs:201`, `:626` | Current generation and offer flow | Buffer clearing after offer; no explicit incubation/evidence IDs/rarity/destination schema; fixed lore anchor |
| `ProgressionDecisionApplier.cs` | Confidence/similarity/curation and narrower decision parsing | Generated-text keyword eligibility; no requested typed G16 chain/rarity/evidence boundary |
| `Data/State/Player State/PlayerState.cs` | Accepted identities, pending offers, partial skill ancestry and structured quests | Title/class effects and complete naming lineage/provenance contract unverified; inspected acceptance does not transfer all skill runtime fields |
| `Data/State/World State/WorldState.cs`, `Dialogue/NpcDialogueAgent.cs` | Stable NPC state and dialogue context/history | Typed authorized NPC-knowledge projection not established |
| `LLM/YQContentProposalBoundary.cs`, `Data/State/YQStateFoundation.cs` | Parse/validate/normalize/curate and accepted-content references, save schema 7 | Full G16 atomic offer/effect/world-response transaction and model-off replay proof |

All paths above are relative to `Assets/Assets/Scripts` unless stated otherwise. The complete G03, G09, G11–G16, G19 and G20 specifications were read from `Docs/ClosedBetaRoadmap_2026-09-16/goals/Goal-NN.md`, alongside GOAL_STATUS.json and relevant design-document sections. The index's queued/unverified status is not production acceptance. G20 frame, long-session and reload requirements remain runtime gates; no G20 test was run here.

Existing dirty LLMClient/LlamaCppServerProcess changes were inspected and preserved. No runtime, G08, save, asset, project-setting or model file was modified. Microsoft Phi-4-mini-instruct is an exploratory candidate named by the user; this package neither adopts nor invokes it.
