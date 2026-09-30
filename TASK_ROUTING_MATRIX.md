# YourQuest task-routing matrix

| Task property | Route | Required gate |
|---|---|---|
| Local compiler/null/lifecycle defect in one owner | Configured engineer | Compile, targeted reproduction, patch review |
| Focused editor verification or diagnostic instrumentation | Local implementer | Unity reload/menu smoke check; observer stays read-only |
| Pure V2 planner, boundary, parser, or state validator test | Local test engineer | Fixed-seed/invariant result and cleanup |
| Measured streaming hitch/allocation issue | Profiler diagnosis → implementer | Before/after profile plus content/determinism parity |
| `YQWorldGenerationArchitecture`/V1-V2 authority or generation-order change | Coordinator contract review | Focused architecture review and fixed-seed/regression evidence |
| `YQPlayerFollowingSemanticChunkStreamer` lifecycle/budget change | Coordinator contract review | Applicable semantic harness and current-cell safety evidence |
| `YQProfileSaveSystem`/`YQProfileCommitStore`/schema/migration change | State/migration contract review | Existing authorization or approval for save invalidation; compatibility evidence |
| Player/state/faction/quest/dialogue ownership change | Coordinator contract review | Focused architecture review; no duplicate authority |
| New/changed asset binding, scene/prefab, or imported package path | Senior review | GUID/reference and runtime asset evidence |
| Destructive feature/asset/scene removal | Human approval | Backup/rollback and final review |
| Documentation/goal setup | Configured engineer | Guidance validator; preserved runtime/assets and acceptance requirements |
| Inline completion/FIM | Available autocomplete | Engineer reviews the code |

Risk classes are defined in `AI_CHANGE_CONTROL.md`. Legacy routing labels describe responsibility, not mandatory model names or another permission gate. Mechanical support cannot decide architecture, generation semantics, migrations, or acceptance. See `MODEL_ROUTING.md` for available-model substitutions.
