# Quest and narrative context

Quest progression uses structured `QuestRecord` and `QuestObjectiveRecord` data evaluated by `YQQuestCompletionDirector`; stable IDs and explicit objective types are authoritative. Dialogue uses `NpcDialogueAgent`, `DialogueThinkService`, `NpcDialogueMemoryStore`, and `NpcDialogueSessionStore`, with profile-scoped persistence. `DirectorPromptBuilder`/`DirectorThinkCycle` provide compatibility and world-facing orchestration but do not supersede state managers.
