# Character-creation context

`YQTitleScreenUI` selects or creates profiles. `YQOriginQuestionnaireUI` collects ordinary origin answers beneath the authored threshold presentation. `YQOriginGenerationService` submits a typed JSON request through `LLMClient`, validates with `YQContentProposalBoundary`, commits a `YQMutationReceipt`, and falls back deterministically when the optional model result is rejected. `YQProfileSaveSystem` persists accepted player origin fields; Goddess voice/readout remains presentation.

Do not bypass the startup lock, invent origin fields outside `PlayerState`, or treat model prose as canonical mechanics.
