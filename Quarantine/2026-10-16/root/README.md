# Preserved root artifacts

Four reviewed root files were moved here without changing their bytes. Their original paths, Git status, lengths and SHA-256 hashes are recorded in [manifest.json](manifest.json). Active tools, accepted data, assets and Unity files were not moved. The quarantine files use `.quarantined` suffixes and must not be executed.

| Original root file | Reason for quarantine |
| --- | --- |
| `start` | Contains only the text `Temp\YQ_STREAMER_RUNTIME_START.request`. The Editor watches that canonical **Temp path**, not this root pointer file. No executable body or observed active consumer. |
| `continue` | Contains only the text `Temp\YQ_STARTUP_CONTINUE.request`. The Editor watches that canonical **Temp path**, not this root pointer file. No executable body or observed active consumer. |
| `testchange.txt` | Empty tracked placeholder with no observed reference or executable content. Preserved rather than deleted. |
| `Run_Project_Dump.bat` | Requires the absent `YourQuest_ProjectDump.ps1` and exits before export. `Run_Project_Dump_LEAN.bat` and its real `YourQuest_ProjectDump_LEAN.ps1` target remain at the project root. |

The reference check covered current root guidance, CI/configuration, project-owned helper sources and known workflow documents. It cannot certify that no external shortcut exists. Restoration is available if an external workflow still needs one of these original paths.

From the project root, validate the complete restoration plan without moving anything:

```powershell
& '.\Quarantine\2026-10-16\root\Restore-Root.ps1' -DryRun
```

To restore all four original files after reviewing that plan:

```powershell
& '.\Quarantine\2026-10-16\root\Restore-Root.ps1' -Apply
```

The default invocation is also a dry run. Restoration checks all four entries before moving any file and refuses missing or changed quarantine bytes, existing originals, unknown/escaping paths and reparse parents. It never executes a quarantined file or overwrites an original. If a move fails, it attempts to return only the files moved by that invocation to this archive.

Eight isolated fixture checks passed, including the four-file restore roundtrip and changed/missing/conflicting-path refusals. This environment denied creation of an isolated junction, so reparse refusal was reviewed in source but could not be exercised with a junction fixture. The actual quarantine passed its read-only restoration precheck; it was not restored during verification.
