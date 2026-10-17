# Bounded local pilot: $0, under two hours per experiment

The proposed first experiment uses the existing RTX 5060 (8,151 MiB VRAM), approximately 32 GiB system RAM and configured Qwen3.5-4B base. Target optimization time is 20-40 minutes, with a 50-minute optimization limit. Loading, checks, saving and a small base-versus-adapter comparison bring the entire GPU experiment to a **110-minute hard limit**. These are spending and time limits, not measured speed or memory-fit claims. No training is started by this document or its JSON configuration.

## Before reserving GPU time

Allow one 30-minute active preparation attempt. Confirm matched source weights and tokenizer for the deployed GGUF, a runnable pinned CUDA training environment, compatible adapter conversion, and reviewed rights-approved examples. Record unresolved prerequisites and stop when that preparation limit expires. Human review, model retrieval and environment preparation are separate from training; their elapsed completion time is not promised to fit within two hours. Current-phase downloads and installations remain prohibited.

Matching source provenance remains unresolved, the Python training environment is not validated, and all 42 train candidates remain pending human review. Production G03/G09/G15/G16 bindings are also incomplete. A fixture-only infrastructure canary, if separately authorized, must be labelled development-only and cannot justify runtime integration or production adoption. No pending fixture may bypass the existing training export guard.

Begin only when the GPU is otherwise idle and a fresh probe shows at least 7 GiB free. The latest audit snapshot had only 675 MiB free, so it did not meet this condition. Leave existing work and processes alone. Reserve up to 30 GiB additional local disk for one matching source copy, the isolated environment and adapter artifacts, while retaining at least 10 GiB free. That allocation is provisional; inventory the actual environment/cache sizes before proceeding. Do not download a second base or create merged-model intermediates in this pilot.

## One experiment, with a fixed clock

| Phase | Limit | Required result |
|---|---:|---|
| Load, compatibility and speed checks | 20 minutes | Complete longest input fits; finite nonzero adapter gradients; base stays frozen; zero-adapter export/load is compatible; ten scratch microbatches provide timing and memory measurements. |
| Optimization | 50 minutes | One complete pass over the approved train set; a second pass only if the measured forecast fits. No search over multiple configurations. |
| Save and convert | 5 minutes | Adapter-only checkpoint, hashes, run telemetry and supported runtime conversion. A missed deadline is a recorded failure. |
| Small paired comparison | 25 minutes | Same 17 public validation/adversarial/regression inputs for base and adapter, when complete role projections fit the runtime output budget. Score raw outputs, errors and timeouts. |
| Cleanup and report | 10 minutes | Release the pilot's resources and record CONTINUE, REVISE or STOP. |
| **Total** | **110 minutes** | The hard experiment deadline includes failures, fallback attempts and both model variants. |

The clock starts at the first GPU load, including compatibility probes. Stage limits do not extend the total limit. Use a watchdog for the pilot's own processes, with termination/cleanup time reserved inside the limit; never terminate unrelated processes. If a stage cannot complete, preserve evidence and stop. Do not silently turn the experiment into an overnight job.

The 17-case comparison comprises four public validation, eight adversarial and five regression cases; no hidden inputs are used for tuning. At the audited 40-second request timeout, 34 serial requests have a 22-minute-40-second timeout allowance, leaving only 2 minutes 20 seconds for comparison-specific loading and scoring. This is a cap, not an inference-speed estimate: if overhead consumes the remainder, report incomplete coverage rather than extending time or dropping inconvenient failures. Raw and repaired results remain separate; no retries are planned. Transport and tokenizer-aware projections must exist before this can run.

## Small training configuration

- First candidate: selective ordinary LoRA on an 8-bit training base, rank 4, alpha 8, dropout 0.05, batch one and gradient accumulation four. This is an unmeasured fit candidate. Enumerate the actual hybrid model modules and select only text modules supported by the installed adapter converter; freeze the vision stack, base weights, embeddings and output head.
- Use gradient checkpointing, adapter-only AdamW, provisional learning rate 0.00005, linear scheduling, 5% warmup and seed 17031. Pin library versions only after the compatibility check. No full-model tuning, architecture-specific compilation experiment or blind all-linear targeting.
- Start with a 1,024-token allocation only if the matched tokenizer confirms every selected input plus target fits. Allow 2,048 only if the full canary fits memory/time. Never truncate evidence, canonical facts, IDs or target JSON. If neither fits, stop or prepare a separately versioned, complete role projection; do not alter the runtime schema to force success.
- Use up to the current 42 train candidates after review and rights approval. Preserve family splits and the abstain/incubate-heavy distribution; do not compensate for a small dataset by duplicating spectacular rewards. One pass is the default. At most two passes, 84 training microbatches and 22 optimizer updates are allowed, including normalized partial accumulation windows. Also cap processed input-plus-target tokens at 100,000. A complete pass must fit all caps before it starts. Scratch speed-check updates are discarded and recorded separately before reinitializing the seeded pilot adapter.
- Keep at most two adapter-only checkpoints within a 1 GiB artifact allocation. Target total GPU use at or below 7 GiB; abort at 7.2 GiB or any allocation failure. Cap training-process RAM at 20 GiB and require at least 6 GiB available system RAM. Recheck these with live telemetry: installed RAM and old GPU snapshots do not certify availability.

After warmup, forecast a complete pass using **1.25 times the measured p95 seconds per microbatch times remaining microbatches, plus one minute of overhead**. Include forward/backward and amortized optimizer time. Recompute during training and preserve the checkpoint reserve. This estimate from ten samples is a conservative planning heuristic, not a statistical guarantee; the watchdog remains authoritative. For illustration only, 42 microbatches at a measured p95 of 30 seconds would forecast about 27 minutes including that allowance. At 60 seconds it would forecast about 53.5 minutes and fail the 50-minute optimization budget. These are arithmetic examples, not measurements of this PC.

## Alternatives and stop rules

If the 8-bit route fails, do not launch an automatic second training run. Four-bit NF4 QLoRA is the only lower-memory route retained for a later bounded canary. Its quality and conversion risks must be checked against the actual deployed Q4_K_M GGUF; these quantizations differ. Qwen3.5's current training guide discourages four-bit training. CPU-only training, CPU parameter offload, installing WSL as a detour, rentals, paid teacher APIs and remote uploads are outside this first attempt.

Stop on unmatched provenance, unapproved data, incomplete input context, unsupported conversion, missing gradients, nonfinite loss, changing frozen weights, unavailable memory, excessive forecast time or the hard deadline. A crash or timeout is a result, not permission to restart the clock. Stop the adapter experiment on increased rarity inflation, rewarding spam, fabricated canon/entities, unsupported mechanics or unauthorized NPC knowledge. Keep the existing base in production.

## Realistic result

With 42 approved examples and at most 22 updates, the useful question is whether this route fits the PC and yields an early improvement in schema discipline, grounded interpretation and abstention. It may produce no measurable improvement. One small pilot cannot establish reliable rare/unique behavior or satisfy the full production contract.

CONTINUE requires successful fit/gradient/export checks, completed paired diagnostics, no new observed authority violations or safety regressions, and at least one genuine held-out causal/abstention improvement confirmed by review. Otherwise report REVISE or STOP and preserve the base. These diagnostic criteria do not replace the preregistered production gates. Hidden family-separated evaluation, all eight task families, rarity calibration, noncombat quality, scheduler invariants, all A-F demonstrations and save/reload with inference disabled remain mandatory before ADOPT. Complete their datasets and gates before proposing another training run; do not chain unbounded two-hour experiments.

The existing scheduler, one permanent generative model, deterministic mechanical/canonical authority, accepted content, saves and G08 remain unchanged.

Primary sources: [Transformers adapter training with 8/4-bit bases](https://github.com/huggingface/transformers/blob/main/docs/source/en/quantization/bitsandbytes.md), [PEFT LoRA configuration](https://huggingface.co/docs/peft/v0.21.0/package_reference/lora), [Qwen3.5 training guidance and four-bit caution](https://unsloth.ai/docs/models/qwen3.5/fine-tune). These establish available techniques, not measured performance on this PC. See [zero-budget alternatives](ZERO_BUDGET_OPTIONS.md) for the broader research and [the machine-readable proposal](../../Tools/AdaptiveContentLoRA/training-pilot.NOT_RUN.json) for matching limits.
