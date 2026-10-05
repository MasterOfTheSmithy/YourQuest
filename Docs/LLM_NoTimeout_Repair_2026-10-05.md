# LLM requests without elapsed-time expiry

## Requirement and result

The user's instruction on 2026-10-05, "Requests to llm keep timing out. They must NEVER time out," supersedes the earlier finite generation/repair lifetime policy. This repair removes game-imposed elapsed-time expiry, including legacy serialized and per-role timeout settings. A server exit, connection failure, invalid response, explicit cancellation, or retired profile/world ownership remains a distinct terminal condition.

## Changed

- `LLM/LLMClient.cs`: generation and Ollama unload HTTP use Unity's unlimited timeout (zero). Old `request_timeout_seconds` overrides are ignored and stripped before sampling. Queued repair episodes and startup exclusive leases no longer expire on a clock. Residency acknowledgement waits for release before replacing a model. A nested coroutine adapter disposes pending loading/probing/unload operations on explicit cancellation so the shared queue can advance.
- `LLM/YQBoundedRepair.cs` and `YQLlmRequest.cs`: repair lifetimes are unlimited; compatibility fields/signatures remain. Submission and physical-call limits remain enforced.
- `LLM/OllamaServerProcess.cs` and `LlamaCppServerProcess.cs`: readiness waiting no longer has a total startup deadline. Disposal and known process exit still stop it. The game-owned Ollama service sets `OLLAMA_LOAD_TIMEOUT=0`; [Ollama's configuration implementation](https://raw.githubusercontent.com/ollama/ollama/main/envconfig/config.go) defines a nonpositive value as infinite. An externally started service keeps its own environment.
- `Generated/YQGeneratedNpcPlanningService.cs`: slow generation no longer triggers the independent NPC fallback timer.
- `Generated/YQGoddessSpeech.cs`: removed speech transport and episode clock limits without changing the qualified model, sampling, grounding, or prompt contract.
- `Generated/YQGeneratedWorldRuntimeBuilder.cs`: the physical-build watchdog does not cancel startup during pending LLM work; frontier failure wording no longer implies a removed deadline.
- Existing editor verification fixtures now check unlimited lifetime and explicit cancellation rather than expecting automatic timeout.

Paths above are relative to `Assets/Assets/Scripts/`. Single transport/scheduler ownership, typed validation, queue/call limits, accepted records, save schemas, determinism, model routes, and model memory cleanup ownership are preserved. Idle server cleanup runs only when no LLM work is pending. Short health probes, process cleanup/help probes, and terrain/physics budgets remain bounded; these are not generation lifetime limits.

## Verified

Fresh Unity 6000.3.2f1 headless compilation succeeded after the final executable patch. `YQLlmLocalStartupVerification.RunFromCommandLine -yqLlmQueueOnly -yqLlmNoTimeout` completed **29 checks, zero failures**. See [the current receipt](LLM_NoTimeout_Repair_2026-10-05.json). Checks cover retained model routes and Goddess qualification, old queued work, retired ownership, unlimited transport policy, NPC timer removal, repair call budgets, startup lease lifetime/release, and nested loading resource disposal on explicit cancellation. Scoped whitespace/diff checking passed.

No Play Mode, real model inference, world generation, profile publication, or ordinary player flow was exercised by this verification. The revised runtime scheduler fixture compiled but was not run. Infinite-duration inference cannot be empirically established by a finite test; the absence of game-imposed deadlines is established by the patch and focused policy checks.

## Remaining

Live slow inference and ordinary gameplay remain unverified. Start a new Play session after Unity imports the repair. Real backend/network/process failures remain visible and are not disguised as accepted content.
