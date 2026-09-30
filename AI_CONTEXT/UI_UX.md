# UI/UX context

Production presentation is staged by `YourQuestTutorialAutoBootstrap`, `RuntimeModalUiBlocker`, `YQTitleScreenUI`, `YQOriginQuestionnaireUI`, `YQStartupLoadingScreen`, `YourQuestTutorialHud`, pause/progression/dialogue UI, and the selected scene roots. UI observes authoritative state and gates input; it does not own saves, world plans, quest completion, or combat state. Imported demo UI remains quarantined unless a current scene/bootstrap reference proves otherwise.
