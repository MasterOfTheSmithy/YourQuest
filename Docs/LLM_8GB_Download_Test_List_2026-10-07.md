# YourQuest download and live test list

Historical download-readiness snapshot. The isolated runtime has since been installed, all six control files hashed, and the standalone tests completed. See the [live test report](C:/Users/Garri/YourQuest/Docs/LLM_8GB_Live_Test_Report_2026-10-07.md) and updated [manifest](C:/Users/Garri/YourQuest/Docs/LLM_8GB_Download_Test_Manifest_2026-10-07.json) for current status; the original instructions below are preserved as history.

7 October 2026. Both download batches and both runtime archives are present in the user-supplied **E:/GPT LLM TEST** folder. All ten selected model files and both archives match the pinned byte sizes and SHA-256 checksums. Batch 1 remains the first live comparison; batch 2 adds four specialists once the common engine and initial fixtures work. The JSON companion records exact sources and verified local paths.

## Required benchmark software

Use these two Windows x64 CUDA 13.3 packages from the pinned [llama.cpp b10828 release](https://github.com/ggml-org/llama.cpp/releases/tag/b10828):

- [cudart-llama-bin-win-cuda-13.3-x64.zip](https://github.com/ggml-org/llama.cpp/releases/download/b10828/cudart-llama-bin-win-cuda-13.3-x64.zip) — 0.39 GB.
- [llama-b10828-bin-win-cuda-13.3-x64.zip](https://github.com/ggml-org/llama.cpp/releases/download/b10828/llama-b10828-bin-win-cuda-13.3-x64.zip) — 0.15 GB.

Both archives are verified in E:/GPT LLM TEST. Extract both packages into the same new benchmark directory: **E:/GPT LLM TEST/Runtime/b10828-cuda13.3**. Extraction and installation are still pending. Keep the current production server at C:/Ai/llama.cpp/llama-server.exe.

This build meets Spark's published minimum and the CUDA 13.3 package matches the installed driver's reported CUDA UMD version. Startup and model compatibility are still test gates. Begin with ordinary decoding, then use a separate greedy MTP off/on diagnostic. Sampled MTP needs its own correctness check before it can be used for production roleplay.

The two archives total approximately **0.54 GB**. No CUDA Toolkit installation is part of this list.

## Batch 1

Six model files, approximately **15.09 GB** total, or **15.63 GB** including the two runtime archives before extraction.

| Test order | Exact download | Filename | Size | Test purpose |
| --- | --- | --- | --- | --- |
| 1 | [Qwen3.5-4B stock MTP Q4_K_M](https://huggingface.co/unsloth/Qwen3.5-4B-MTP-GGUF/resolve/86835bf9949e4d14d6860f7910b1340ad4f271a9/Qwen3.5-4B-Q4_K_M.gguf?download=true) | Qwen3.5-4B-Q4_K_M.gguf | 2.83 GB | Control-family acceleration; compare this exact file with MTP off and on. |
| 2 | [Official Spark-X2.5-4B Q4_K_M](https://huggingface.co/XHToken/Spark-X2.5-4B-GGUF/resolve/9826e0be84e6e6e8b9668abc91421109a1df1e2d/Spark-X2.5-4B-Q4_K_M.gguf?download=true) | Spark-X2.5-4B-Q4_K_M.gguf | 2.60 GB | General reasoning, roleplay, and schema-constrained proposals. |
| 3 | [Official LFM2.5-2.6B QAD Q4_0](https://huggingface.co/LiquidAI/LFM2.5-2.6B-GGUF/resolve/e7caca5d835a3901a8e0d63e94009429bafafdfc/LFM2.5-2.6B-QAD-Q4_0.gguf?download=true) | LFM2.5-2.6B-QAD-Q4_0.gguf | 1.59 GB | Minimum-memory general-model challenger; reconstruction and dialogue. |
| 4 | [Official LFM2.5-2.6B Q6_K](https://huggingface.co/LiquidAI/LFM2.5-2.6B-GGUF/resolve/e7caca5d835a3901a8e0d63e94009429bafafdfc/LFM2.5-2.6B-Q6_K.gguf?download=true) | LFM2.5-2.6B-Q6_K.gguf | 2.22 GB | Matched LFM quality comparison against QAD Q4. |
| 5 | [SmallThinker-4B-A0.6B ordinary Q4_K_S](https://huggingface.co/Tiiny/SmallThinker-4BA0.6B-Instruct-GGUF/resolve/b6ca58deaddfaa18501040b24cc88294c26f84b8/SmallThinker-4B-A0.6B-Instruct.Q4_K_S.gguf?download=true) | SmallThinker-4B-A0.6B-Instruct.Q4_K_S.gguf | 2.49 GB | CPU helper candidate for summaries, extraction, and Goddess voice. |
| 6 | [Official Gemma 4 E2B QAT Q4_0](https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/675cff42a74c774d6cb76f76d8eacb49b48c9b93/gemma-4-E2B_q4_0-it.gguf?download=true) | gemma-4-E2B_q4_0-it.gguf | 3.35 GB | Independent general-model and roleplay comparison, without a drafter. |

For LFM, select the file with **QAD-Q4_0** in its name. Ordinary Q4_0 is a different file. The Q6 comparison tests whether additional precision preserves intelligence and reconstruction sufficiently to justify its extra memory.

SmallThinker in this batch is the ordinary GGUF, compatible with llama.cpp. Its PowerInfer format and engine are deferred until the model's quality passes. Gemma in this batch is Google's exact official QAT GGUF, **3.35 GB**, rather than a generic size estimate or a different uploader's quantization.

## Batch 2

Four additional files, approximately **10.15 GB**. These are the selected extension tests; the rest of the research shortlist does not need to be downloaded now.

| Test order | Exact download | Filename | Size | Test purpose |
| --- | --- | --- | --- | --- |
| 7 | [DavidAU LFM NEO Q6_K](https://huggingface.co/DavidAU/LFM2.5-2.6B-Qwen3.8-Turbo-Brilliance-Power-X12-NEO-MAX-GGUF/resolve/3ad53ebe212c3e0c4cd27fe2c3c6ff3eaf16ade4/LFM2.5-2.6B-Q3.8-TBrilliance-NEO-Q6_K.gguf?download=true) | LFM2.5-2.6B-Q3.8-TBrilliance-NEO-Q6_K.gguf | 2.22 GB | Roleplay specialist compared against official LFM and MiniFantasy. |
| 8 | [Spark abliterated FIT MINI Q4_K_S](https://huggingface.co/SC117/Spark-X2.5-4B-abliterated-FIT-GGUF/resolve/0ab053e15f5343627fb68d1b421d4a7c8fa9fe87/Spark-X2.5-4B-abliterated-FIT-MINI-2.26GiB-Q4_K_S.gguf?download=true) | Spark-X2.5-4B-abliterated-FIT-MINI-2.26GiB-Q4_K_S.gguf | 2.43 GB | Smaller Spark voice candidate, compared against official Spark. |
| 9 | [insraq distilled Heretic MTP Q4_K_M](https://huggingface.co/insraq/Qwen3.5-4B-EmperoAI-Qwen3.8-Distill-Heretic-Abliterated-MTP-GGUF/resolve/538d127c4e8980283b36e68978ffa74534f4d194/Qwen3.5-4B-EmperoAI-Qwen3.8-Distill-Heretic-Abliterated-MTP-GGUF-Q4_K_M.gguf?download=true) | Qwen3.5-4B-EmperoAI-Qwen3.8-Distill-Heretic-Abliterated-MTP-GGUF-Q4_K_M.gguf | 2.79 GB | Reasoning/voice variation and MTP off/on after stock MTP. |
| 10 | [Official APUS OpenJev Q4_K_M](https://huggingface.co/apus-ailab/APUS-OpenJev-v1-4B-GGUF/resolve/7389d774472c9e29ddc84fffb392951f0f25de74/APUS-OpenJev-v1-4B-Q4_K_M.gguf?download=true) | APUS-OpenJev-v1-4B-Q4_K_M.gguf | 2.71 GB | Ambiguous-choice selection only; compare net latency against deterministic routing. |

Choose DavidAU's **NEO Q6_K** first. MAX and Q8 variants are deferred. Choose Spark **FIT MINI** first; COMPACT offers little weight-size relief relative to official Spark Q4. Use only the official OpenJev package initially.

OpenJev follows a separate selector test: short relevant context, allowed choices, and a label response. Compare its full latency and routing mistakes with direct deterministic routing. It is not entered into the dialogue or world/player reconstruction contest as a general replacement.

Both batches plus the runtime archives total approximately **25.78 GB** before extraction. Allow about **30 GB** of free disk space for the full set and unpacked runtime.

## Reuse installed controls

These models were found in the configured model stores, so there is no new model download for them. Presence and manifest metadata were checked; full local checksums will be verified before live testing.

| Existing model | Use |
| --- | --- |
| Qwen3.5-4B-Q4_K_M.gguf at C:/Ai/Text Models | Current structured-generation and reasoning control. |
| MiniFantasy Q4_K_M in Ollama | Current dialogue and character-memory control. |
| yourquest-qwopus-q4km:latest in Ollama | Current Goddess control from freshly inspected configuration; model blob about 5.63 GB. |
| small-test:latest in Ollama | Current summary control; model blob about 0.73 GB. |
| Deckard Q4_K_M in Ollama | Installed optional prose/reasoning comparison. |
| Kimi Luna Q4_K_M in Ollama | Installed optional reduced-reasoning comparison. |

The current Goddess source now names yourquest-qwopus-q4km. Earlier timing evidence for smaller-test does not establish timing for this current model. [Current role configuration](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/LLM/LLMRuntimeConfig.cs:180).

## Verified test storage

Use the supplied files directly under **E:/GPT LLM TEST**. The JSON manifest now supplies each actual local path. No files were moved or renamed.

The stock MTP file at E:/GPT LLM TEST/Qwen3.5-4B-Q4_K_M.gguf has the same filename as the production Qwen model at C:/Ai/Text Models/Qwen3.5-4B-Q4_K_M.gguf. These are different files; keep both original locations.

Three additional files are present: Qwen3-4B-Hivemind-Inst-Hrtic-Ablit-Uncensored-Q4_K_M-imat.gguf, nsfw-6b-q2_k.gguf, and Spark-X2.5-4B-Q4_K_M (1).gguf. Hivemind and the 6B model need provenance and checksum qualification before scored testing. The second Spark file is a possible duplicate; do not count it as a distinct model. These extra files remain untouched.

For this text-only test, download only the listed language-model files. Vision projectors, BF16 weights, alternate quantizations, imatrix files, and draft packages outside the list are unnecessary for the first comparison.

## Live check sequence

1. Selected model and runtime archive sizes/SHA-256 are verified. Extract the pinned runtime and verify its executable startup and GPU availability. Installed control model checksums still require verification.
2. Run the original production controls. Run the current Qwen on the benchmark engine as well, so backend changes are distinguished from weight changes.
3. Screen batch 1 without speculation. Use the actual required context, real templates, and supported thinking controls. Compare general reasoning, canonical world/player JSON reconstruction, and blind dialogue quality.
4. Compare the exact stock MTP target file with MTP off/on under a matched greedy diagnostic. Measure accepted-result time and draft acceptance. Qualify sampled MTP separately.
5. Test cold loads, warm repetitions, and alternating main/helper requests. Evaluate SmallThinker on CPU first and assess CPU frame impact as well as helper speed.
6. Run batch 2's voice/reasoning comparisons. Evaluate OpenJev independently by whether it avoids more work than it adds.
7. Compare finalists while Unity occupies its representative GPU budget. Record VRAM/RAM, frame time, queue delay, prompt processing, generated tokens, retries, and time to complete valid results.
8. Test residency, bounded caching, and foreground scheduling as separate changes after the model comparisons, so their effects remain attributable.

The first pass uses one active request. Do not apply the current Qwen filename-based fitting calculation to arbitrary models or silently halve each request's context to add slots. Test responses stay detached proposals; accepted saves and canonical world/player state remain unchanged.

A winner must preserve essential reasoning, roleplay, and semantic reconstruction. Faster tokens or syntactically valid JSON alone do not pass. Standalone live inference, Unity-shared performance, and ordinary player-flow acceptance are separate evidence levels.

## Deferred downloads

Ministral, Qwen3.5-2B, Jackrong, PJRM, Parable, Qwen2507 variants, Ling, and Gemma E4B remain in the full research audit. They are later investigations if the selected models leave a demonstrated gap. Hivemind is now supplied locally as an additional candidate; qualification is pending. PowerInfer, DSpark, Gemma assistants, LiteRT-LM mobile formats, ExLlama, and compiled engines follow only after their associated targets justify the integration.

## Prepared artifacts

- [Download manifest JSON](C:/Users/Garri/YourQuest/Docs/LLM_8GB_Download_Test_Manifest_2026-10-07.json) — exact sources, verified local paths and checksums, controls, and test rules.
- [Local download readiness receipt](C:/Users/Garri/YourQuest/Docs/LLM_8GB_Download_Readiness_2026-10-07.json) — observed sizes, expected/actual SHA-256 values, timestamps, and the extra file inventory.
- [Full model and method audit](C:/Users/Garri/YourQuest/Docs/LLM_8GB_Model_Method_Audit_2026-10-07.md) — broader candidates and architectural reasoning.

The user-supplied ten selected model files and two runtime archives are locally verified against the pinned checksums. No files were downloaded or moved by this preparation, no runtime was installed, and no inference, Unity Play Mode, production setting, or accepted save change was performed.
