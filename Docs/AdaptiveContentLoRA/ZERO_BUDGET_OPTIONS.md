# Zero-budget training options

Current owner constraint: spending budget is $0. Prioritize the existing Windows PC; remote dataset upload is not authorized. This research note replaces the earlier larger-GPU purchase/rental recommendation for current planning. It does not certify a training setup, change the configured base model or authorize a model run.

The newer [bounded local pilot](BOUNDED_LOCAL_PILOT.md) governs the first attempt: 50 minutes maximum optimization, 75 minutes including loading/checks/saving and 110 minutes for the whole GPU experiment. CPU-only, offload and remote routes below remain research alternatives outside that attempt; no automatic fallback or extra training run is planned.

The existing audit identifies a Ryzen 7 5800X, approximately 32 GiB RAM and an RTX 5060 with 8,151 MiB VRAM. Fresh read-only checks found approximately 90.1 GiB free on C: and 86.7 GiB on D:. At the GPU snapshot, 7,221 MiB was used, 675 MiB was free and utilization was 40%. These are observations, not reserved capacity. A fresh available-system-RAM query was denied; the older installed-RAM figure is retained. No process was stopped.

The configured Qwen3.5-4B GGUF and existing scheduler remain the production baseline. Matching source weights/tokenizer remain unresolved. All 64 dataset candidates are pending human review; 42 are train candidates, none training-eligible. Independent hidden inputs remain unauthored. These constraints apply to every training route below.

| Route | Evidence and fit | Current recommendation |
|---|---|---|
| Selective 8-bit-base LoRA | Transformers/PEFT document 8-bit adapter training. LlamaFactory lists Qwen3.5 and Windows QLoRA support; bitsandbytes documents Windows CUDA 12.8 builds including sm120. This establishes a possible toolchain, not an 8 GB Qwen3.5 training benchmark. | First local memory/gradient/export canary candidate after source/environment gates. Start with compatible ordinary LoRA modules and batch one; measure complete role projections. |
| Lower-memory adapter configuration | PEFT documents selected-layer adapters and LoRA-FA, which freezes A and reduces activation storage. Freezing vision, embeddings and lm_head avoids unrelated trainable state. It does not eliminate base-weight residency. | An adjunct to a working base loader, not an independent answer to a too-large BF16 model. Keep standard adapter export semantics and validate gradients. |
| BF16 LoRA with training-aware parameter CPU offload | DeepSpeed ZeRO-3 documents parameter offload to CPU. PEFT has a DeepSpeed training integration. The current hybrid model, consumer GPU and Windows/Linux stack have not been validated. LoRA optimizer-only offload saves little when adapter states are already small. | Local fallback with setup and throughput risk; likely needs a validated Linux/WSL environment. Do not substitute inference-only device_map=auto for training support. |
| Four-bit NF4 QLoRA | Transformers documents NF4 adapter training and nested quantization. Unsloth's Qwen3.5 guide specifically discourages four-bit training because of quantization differences. NF4 training and deployed Q4_K_M are different quantizations. | Experimental fallback only. Memory fit, correct gradients and quality on the actual deployed GGUF must all pass; never assume generic 4B recipes transfer. |
| CPU-only LoRA | RAM can hold more weights than the GPU, but Qwen3.5 CPU backward/kernel compatibility and practical throughput were not tested. There is no trustworthy training-duration estimate. | Last-resort feasibility experiment, not the preferred production training plan. |
| Free remote GPU notebook | Colab offers a free tier; Unsloth supplies a Qwen3.5-4B notebook. GPU type/capacity is not guaranteed and free sessions can terminate. | Optional only if the owner later permits uploading reviewed original data. Do not create an account, upload data or assume available GPU capacity in this phase. |

Unsloth's approximately 10 GB estimate refers to its BF16 LoRA recipe, not a universal impossibility result for every 8 GB strategy. The local alternatives above are feasibility candidates, not measured recommendations to adopt an adapter.

The next bounded phase should establish a runnable isolated training environment and matched base/tokenizer provenance without installing into Unity or changing global project settings. Then enumerate export-supported text modules and tokenizer-measure the complete role inputs and target outputs. A provisional rank 4–8, batch one and checkpointing are memory-probe settings, not final training choices. Short context is permissible only where the entire required input/target fits; never truncate canonical facts, IDs or target JSON to force a PASS.

Before a long run, record one forward/backward/optimizer canary's nonzero finite adapter gradients, frozen base parameters, elapsed time, RAM/VRAM peaks and adapter checkpoint identity. Validate a zero-adapter export/load canary against the installed llama.cpp backend, followed by a trained-adapter conversion check. Stop on incompatibility, missing complete context or resource failure. Keep a bounded number of adapter-only checkpoints; defer merged-model intermediates until export is proven. Exact disk/memory/time requirements remain unmeasured.

Training, downloads, installs, GPU inference, dataset uploads and process interruption were not performed. Accepted game content, deterministic mechanical authority, runtime ownership, G08 and save state remain unchanged. Full paired causal-quality, rarity, noncombat, scheduler and model-off persistence gates still determine adoption.

Primary sources checked:

- [Qwen3.5 training guide](https://unsloth.ai/docs/models/qwen3.5/fine-tune): BF16 estimate, four-bit caution, text-only options and free notebooks.
- [Unsloth Windows installation](https://unsloth.ai/docs/get-started/install/windows-installation): native Windows, Conda and WSL routes.
- [Transformers bitsandbytes documentation](https://github.com/huggingface/transformers/blob/main/docs/source/en/quantization/bitsandbytes.md): adapter-only 8/4-bit training, NF4 and inference-only auto device mapping.
- [PEFT LoRA](https://huggingface.co/docs/peft/v0.21.0/package_reference/lora): selected-layer configuration and LoRA-FA optimizer.
- [bitsandbytes installation](https://huggingface.co/docs/bitsandbytes/main/en/installation): Windows CUDA 12.8 and sm120 builds.
- [LlamaFactory](https://github.com/hiyouga/LlamaFactory): Qwen3.5 task templates, training approaches and Windows compatibility caveats.
- [DeepSpeed ZeRO](https://www.deepspeed.ai/tutorials/zero/), [PEFT DeepSpeed integration](https://github.com/huggingface/peft/blob/main/docs/source/accelerate/deepspeed.md): training-aware parameter offload, not a validated Qwen3.5-specific local recipe.
- [Colab FAQ](https://research.google.com/colaboratory/faq.html): free availability and runtime limits.

Recommendation: the first bounded local fit check uses selective 8-bit LoRA and complete role projections. If it fails, preserve the evidence and stop; a later four-bit canary would require its own bounded plan and quality checks. CPU offload and remote alternatives are deferred. Spend $0 and keep the existing base unless measured evidence warrants a separately reviewed change.
