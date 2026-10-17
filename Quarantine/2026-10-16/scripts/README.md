# YourQuest script quarantine and restore

The archived `.cs.quarantined` and `.cs.meta.quarantined` files preserve their original paths beneath this directory. The source `.meta` GUIDs and SHA-256 values are recorded in `quarantine_manifest.json`; the archive receipt records the exact transfer and hashes. The helper and manifest are copied here during Archive so this folder remains a self-contained restore package if the ignored `outputs` folder is removed.

The source review found no current project entry point or serialized script reference for these ten files:

| Script (under `Assets/Assets/Scripts`) | Bounded retirement reason |
|---|---|
| `Data/Serialization/SerializableDictionary.cs` | Generic serialization helper with no containing field, closed generic, or type-name use found. |
| `Data/State/StateCompatibilityExtensions.cs` | No external call found; inspected call sites use matching PlayerState/WorldState instance methods. |
| `Generated/YQRuntimeWorldAssetRegistrySmokeTest.cs` | Unattached one-shot registry smoke component with no current invocation. |
| `LLM/Debug/DebugSkillGenerator.cs` | Unattached keyboard draft generator; its two DTOs had no external consumer found. |
| `Observation/Threat/ThreatProbe.cs` | Unattached probe; `Tick` had no caller and `ThreatContact` had no external consumer found. |
| `Tutorial/YourQuestTutorialVitals.cs` | Unattached component with no production invocation found. |
| `PrototypeBuilder/Editor/YQPrototypeSceneBuilder.cs` | Hidden legacy builder; no current caller or command recipe, and its target scene is absent. |
| `Tutorial/Editor/YourQuestPolishedPrototypeSceneMenu.cs` | Hidden wrapper with no current caller; its sole dependency is retired in this batch. |
| `Tutorial/Editor/YourQuestInvestorSceneBuilder.cs` | Hidden builder called only by the polished wrapper; its target scene is absent. |
| `Tutorial/Editor/YourQuestTestSceneMenu.cs` | Hidden test-scene builder; no current caller or command recipe, and its target scene is absent. |

The one detached build excluded exactly these ten files and compiled 392 runtime plus 194 Editor sources through a fresh runtime project reference. It exited 0 with 0 warnings and 0 errors; all 596 source inputs were unchanged at completion. This is compile evidence only. Unity import, scene deserialization, PlaySafe, and player-visible behavior remain unverified.

From a PowerShell prompt in the project checkout, preview a restore with:

```powershell
& 'Quarantine/2026-10-16/scripts/Invoke-EditorScriptQuarantine.ps1' -Action Restore
```

After reviewing the preview, add `-Apply` to restore the complete batch. The helper refuses existing destination files, checks every SHA-256 and path for reparse points, and checks that Unity is closed before moving files. It returns each `.cs` and `.meta` to the original relative path and preserves the original GUID. Keep this entire quarantine directory until the restored project has been checked in Unity. If the batch is interrupted or the helper reports a rollback problem, leave the files in place and inspect the receipt and exact paths; do not overwrite either copy.

```powershell
& 'Quarantine/2026-10-16/scripts/Invoke-EditorScriptQuarantine.ps1' -Action Restore -Apply
```

The archive came from a source-only retirement review. A detached C# compile does not prove old external `-executeMethod` automation is unused, and restoring source files may require a later Unity import/compile check.
