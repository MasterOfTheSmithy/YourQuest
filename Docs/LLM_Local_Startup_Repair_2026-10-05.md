# Local model startup and queue repair

## Changed

- The existing LLMClient starts a headless local Ollama service on demand. Its service adapter uses the approved model store at `D:\OllamaModels` when present. Existing external services remain external.
- Existing approved category/model assignments remain unchanged. Every Ollama request checks service readiness, including recovery after an external service stops.
- Busy/loading HTTP services retain their port ownership; the game waits instead of launching a competing server.
- Idle llama.cpp shutdown waits through retry backoff and counts idle time from the end of an attempt. Standby is distinct from disabled generation.
- Admitted current requests no longer expire merely because they waited behind inference. Ownership cancellation, explicit repair deadlines, bounded admission and priority remain intact.
- NPC population's stall guard measures its own active attempt, excluding another role's execution and queued time.
- Request and baseline diagnostics report the dispatched model instead of the legacy llama3.1 fallback field.

## Fresh evidence

- Installed Unity 6000.3.2f1 compiled runtime and Editor code successfully.
- `Docs/LLM_Local_Startup_Verification_2026-10-05.log`: 26 checks, zero failures. The production service adapters started servers on isolated loopback ports without opening the Ollama desktop application. Real completion text was returned by:
  - `hf.co/Nubinu/Qwen3.5-4B-MiniFantasy-GGUF:Q4_K_M`
  - `smaller-test:latest`
  - `small-test:latest`
  - `yourquest-qwen3-4b:latest` (transport only; verifier qualification was not enabled)
  - The configured `Qwen3.5-4B-Q4_K_M.gguf` control model.
- `Docs/LLM_Queue_Verification_2026-10-05.log`: 19 checks, zero failures after the NPC timeout repair. Current requests survive waiting beyond 90 seconds, retired owners are superseded, and NPC timeouts exclude queued work while retaining the active stall guard.

## Boundary

These are isolated real-server transport and detached queue checks, not ordinary gameplay or long NPC batch latency certification. No accepted world/profile records were replaced or regenerated. Previously observed 95-second NPC HTTP timeouts still require an ordinary PlaySafe check with full production prompts. Existing bounded repair deadlines and queue capacity still apply.

Ollama's background CLI integration is documented at https://docs.ollama.com/windows. llama.cpp's health URL is `http://127.0.0.1:11435/health` while loaded; Ollama's model list is `http://127.0.0.1:11434/api/tags`. The llama.cpp endpoint intentionally disappears after its idle model unload and returns on the next queued request.
