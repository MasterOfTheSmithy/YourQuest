# Unity MCP usage guide

No MCP schema is committed in this repository. The connected installation is authoritative; do not invent tool names or parameters.

On 2026-09-30, the native Computer Use route below captured the running Unity 6000.3.2f1 Editor in `YourQuest_PlaySafe`, outside Play Mode, with no blocking modal and zero visible Console counts. This is fresh Editor-access evidence only; no new compilation or gameplay test was run for the documentation/setup changes. No purpose-built Unity MCP tool was exposed in this session.

## Working Windows Editor access (verified 2026-09-27)

The installed `computer-use:computer-use` skill provides native Windows access through **`@oai/sky` in `mcp__node_repl__js`**. This is a different provider from `mcp__cua_repl`: an empty `cua.getState().apps` list does not establish that Unity is inaccessible. This route successfully discovered, focused, and captured the existing YourQuest PlaySafe Editor on 2026-09-27. No Unity restart or user-side exposure step was needed.

Before native UI work, read the current Computer Use skill and its `docs/guidance.md`, `docs/confirmations.md`, and relevant `docs/api.md`. Discover `mcp__node_repl__js` in `ALL_TOOLS` if it is not in the initially displayed tool list. The following cells run in that Node tool, not in `mcp__cua_repl` or a shell.

### Session-start access check

Run this protocol at the start of each Editor task and after a provider reset or Editor restart. The access check succeeds only when a fresh screenshot shows the intended YourQuest Editor and its current scene. Record the returned window title, observed scene, Play Mode state, compilation/import state, and any blocking modal. A running process, log file, stale screenshot, or title match alone is insufficient. Keep access status separate from compilation and runtime acceptance status.

Resolve the Computer Use skill from the current session's skill catalog. On 2026-09-30 the installed path was `C:/Users/Garri/.codex/plugins/cache/openai-bundled/computer-use/26.928.21956/skills/computer-use/SKILL.md`; supporting documents are under `../../docs/` relative to it. Never treat that cache version or a prior window ID as permanent configuration.

1. Initialize once per fresh Node session:

   ```javascript
   if (!globalThis.sky) {
     const { sky } = await import("@oai/sky");
     globalThis.sky = sky;
   }
   ```

2. Discover current windows and inspect the returned candidates:

   ```javascript
   globalThis.unityApps = await sky.list_apps();
   globalThis.unityCandidates = unityApps.flatMap(a => a.windows)
     .filter(w => w.title?.startsWith("YourQuest - YourQuest_PlaySafe -"));
   nodeRepl.write(JSON.stringify(unityCandidates, null, 2));
   ```

3. Require exactly one candidate, bind its returned identifiers, focus, and capture:

   ```javascript
   if (unityCandidates.length !== 1)
     throw new Error("Expected exactly one YourQuest PlaySafe Editor window");
   globalThis.unityWindow = await sky.get_window({
     id: unityCandidates[0].id, app: unityCandidates[0].app
   });
   await sky.activate_window({ window: unityWindow });
   globalThis.unityState = await sky.get_window_state({ window: unityWindow });
   globalThis.unityWindow = unityState.window;
   ```

4. Inspect the displayed screenshot before acting. Use one observed UI action per cell and immediately capture fresh state afterward. Use the current screenshot ID for coordinate actions. Reacquire windows after a restart; never store window IDs or screenshot coordinates as permanent identifiers. Only one agent may drive the Editor at a time.

5. Before starting the runtime harness, confirm the PlaySafe scene, compile completion, Console errors, and actual Play Mode state. Enter Play Mode through the observed Editor control, complete the ordinary title flow, then use `YourQuest > Verification > Run Terrain Acceptance in Current Play Session`. This exact menu is implemented in `Assets/Assets/Scripts/Generated/Editor/YQEditorAutoRefreshBootstrap.cs`. Match a new receipt timestamp to that run and inspect its actual gates. Editor access alone is not a runtime PASS.

### Recovery and limits

- If the exact PlaySafe title is absent but a YourQuest Editor is present, inspect that returned Editor first: it may have another scene open, an unsaved-scene marker, a loading screen, or a modal. Confirm the project and use the observed Project/scene UI to open `Assets/Assets/Scenes/YourQuest_PlaySafe.unity` when needed. Preserve any unsaved work. Do not treat a scene-title mismatch as a missing application or launch another Editor.
- If no matching Editor window is returned, inspect `sky.list_windows()` for loading or modal windows before launching anything. If Unity is closed, the supported `sky.launch_app` accepts the installed executable path or returned app ID; use the resulting Unity/Hub UI to open this project, then rediscover the actual Editor window. The installed version is recorded in `ProjectSettings/ProjectVersion.txt`; do not assume a permanently fixed version or launch a duplicate project instance.
- If Unity has not noticed changed source while unfocused, activate the selected Editor and capture fresh state. Wait for its import/compilation cycle and inspect fresh Console diagnostics before starting Play Mode; foregrounding alone does not prove compilation.
- For a lightweight provider timeout, wait two seconds and retry that call once. If it times out again, reset the Node session, initialize `sky`, and retry once; report the exact failure if it still fails. For activation/capture failure, discard stale state, rediscover the target, and retry once. If input reports another window covering the target, activate the target, capture again, and retry once using the new observation.
- If the desktop is locked, the user must unlock it. If `mcp__node_repl__js` or `@oai/sky` is unavailable in a future session, report that exact missing capability and request that the Windows Computer Use plugin be enabled for that session. Do not ask the user to "expose Unity" to the browser provider.
- This is a repeatable supported access method, not a guarantee across locked desktops, missing plugins, or crashed Editors. Read-only process/log checks and launching Unity do not establish UI control. Do not replace this route with native interop, custom listeners, permission changes, or continuous focus-stealing helpers.

## Preferred loop

Read capabilities/project/version → snapshot Console → patch → wait for reload → inspect compiler/Console → run the named editor/runtime regression → reproduce in `Assets/Assets/Scenes/YourQuest_PlaySafe.unity` when needed → capture scene/screenshot/profiler evidence → exit Play Mode before edits when required → inspect diff.

## Verified repository entry points

- Production startup: `Assets/Assets/Scripts/Tutorial/YourQuestTutorialAutoBootstrap.cs`.
- Top-level regression menu: `YourQuest > Beta Baseline > Run Production Regression`.
- Deep streaming/persistence harness: `YQSemanticChunkRuntimeVerification`.
- World-generation editor tests/verification live under `Assets/Assets/Scripts/Generated/Editor/` and are menu-driven rather than a single NUnit assembly.

## Fallbacks

If MCP cannot compile, use the installed Unity batch/Editor process and its log. If it cannot run a menu test, use the exact menu entry in the active Editor and capture its structured receipt. If it cannot inspect Play Mode, record `NOT_YET_TESTABLE`; never infer runtime PASS from source or historical logs. Use Unity Profiler for performance claims.
