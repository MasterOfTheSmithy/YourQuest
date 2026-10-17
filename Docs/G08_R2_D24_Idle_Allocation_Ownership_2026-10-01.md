# G08-R2 D24 — idle allocation ownership

Status: diagnostic ownership established; focused acceptance remains P07 FAIL pending P08. No R1 certification or R3 run.

## Question and boundary

P07 clean input/coast maxima were 54/69 ms against 50 ms, and coast appearance failed at (11,-2). D23 had persistent unnamed allocation during Play; P07 also had 50,491,644 bytes on an unnamed thread in a 1.571-second Edit capture. Native thread IDs changed across reloads and cannot identify one another. The named question was which code allocates while no gameplay exists. No scheduling repair was restarted.

Only the existing Edit analyzer gained a 150 ms idle allocation-stack capture and bounded saved-stack extraction (up to eight frames, 64 representative allocations per thread/frame, 48 resolved addresses). No fixture preparation, accepted-state mutation, gameplay warmup, or Play input changed. Installed Unity 6000.3.2f1 XML confirms the actual allocation-callstack and method-resolution APIs. Fresh PlaySafe screenshot and idle compile/import status preceded capture. PID28040, Unity6000.3.2f1/DX12, original Editor session retained.

## First capture

- UTC11:12:37.2624144; MVID921ec98d-caae-42be-9692-eadf78b06df3; verifierSHA4CC4AFF67C6CCB2DE677BF364DC53B043999C8308944A9A6EF3354549D4F3613; CSharpDLL429339B6E906FD3CCBFFA7B314564C988BE9E21921AAAB111CD1053D07EA99CA.
- Logs/G08_R2_FrameTrace_20261001_111238_468.raw, 30,909,158 bytes, GUIDffe57282-0133-4a03-8ee2-a41b3750e170. Completed44metadata frames804–847; Edit Unityframe12 is static, not a gameplay frame ID.
- Logs/G08_R2_D24_IdleStacks_20261001.idlestacks.txt: actual0.1538192seconds/43Editorupdates, profiler and binary off, original callstack/log settings restored.
- Saved frames825:826 in Logs/G08_R2_D24_IdleStackOwners_20261001.samplecallstacks.tsv completed. Main:586allocations/35,334bytes in each selected frame. Worker6108:718/41,108bytes then756/44,292bytes. 127/128 selected worker allocations had call stacks.
- Of25unique representative worker stacks,24 show System.IO.DefaultWatcher.IterateAndModifyFilesData, File.GetCreationTime/GetLastWriteTime and repeated DefaultWatcher.DoFiles. No biome/height/detail owner appears on those stacks.

Source within project scripts has one FileSystemWatcher creator: YQEditorAutoRefreshBootstrap watches the entire Assets tree recursively, including third-party directories, with EnableRaisingEvents=true. Imports are separately deferred during Play. The watcher has no explicit reload/quit disposal hook in the before source. Before bootstrapSHA339F2E277C46402FC8D3511CB7A20F75115CE6FBD1FBC438223B8877FB3D4229.

## Specific watcher ownership experiment

A separate Edit-only request temporarily paused exactly that existing project watcher through a named private-field lookup, then restored its original enabled state. No new watcher, backend setting, environment variable, or global Mono policy was installed.

- UTC11:14:12.3056181; MVID37003b42-4f18-4173-bc05-e90a4922b81b.
- Logs/G08_R2_FrameTrace_20261001_111412_614.raw,29,081,656bytes, GUID25cdeb66-09ad-492e-8756-ec728b0f0939;45metadata frames848–892. Last recorded boundary151.516ms.
- Logs/G08_R2_D24_WatcherPausedOwners_20261001.samplecallstacks.tsv, saved870:871, completed. Main allocation unchanged586/35,334bytes. Worker18036:583/37,072bytes and609/43,832bytes. All53unique representative worker stacks have zero IterateAndModifyFilesData stacks;52still show directory enumeration/DefaultWatcher.DoFiles. Disabling does not instantly cancel an in-progress directory walk. No allocation-freedom claim.
- Restoration took substantially longer than the capture: terminal elapsed19.7520359seconds includes synchronous watcher reinitialization after the writer closed. Profiler/binary off, original allocation-callstack/log/watcher states all restored. This is not a 19.75-second trace and not runtime timing.

The observed stack change supports ownership of file-metadata scanning by the project watcher. Upstream Mono [DefaultWatcher source](https://github.com/mono/mono/blob/main/mcs/class/System/System.IO/DefaultWatcher.cs) also explains the distinction between disabling metadata processing and a directory recursion already in progress; it is supporting source, not proof of the exact installed implementation. No historical unnamed native ID was remapped to the new IDs.

## P08 bounded repair and shortest check

Owner: existing Editor source-refresh helper, not terrain authority or scheduler. Suspend source watching before Play; create inactive during Play domain reload; dispose on reload/quit; resume in Edit and queue one catch-up import for edits made while suspended. Keep Unity's existing balanced auto-refresh hold and all marker handlers. Record actual watcher state in the existing low-cadence status receipt. Gameplay/save/content/resource/view/input contracts unchanged.

Expected measurable change: source watcher inactive throughout ordinary Play and active again in Edit, with deferred source import preserved. The clean focused witness determines timing/readiness impact; no prediction that all remaining frame cost or coast appearance is fixed. First confirm title-session watcher inactive after final compile, then ordinary Continue plus original focused input/full coast. Original P07 deficit4/19ms remains authoritative until that run completes. This is a new demonstrated tooling cause, not a reset of the exhausted scheduling/reservation loop.
