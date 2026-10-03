# Testing and diagnostics context

The repository uses menu-driven editor verification and runtime harnesses rather than one consolidated NUnit suite. Important entry points include V2 contract, spatial seed, continuous terrain determinism, semantic world authority, production-slice, production-baseline, route traversal, asset-kit/palette, settlement layout, saved-placement, and LLM scheduler verification scripts under `Assets/Assets/Scripts/Generated/Editor/`. Runtime deep evidence comes from `YQSemanticChunkRuntimeVerification` and `YQProductionBaselineDiagnostics`.

Tests are evidence only when their current run, seed, scene, and status are recorded. Historical receipts must remain historical.
