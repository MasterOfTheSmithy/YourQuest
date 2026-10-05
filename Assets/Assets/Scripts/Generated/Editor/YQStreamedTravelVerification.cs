#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// note: Explicit assisted runtime witness: normal Continue, real keyboard events and the authoritative motor; no teleport or forced generation.
[InitializeOnLoad]
public static class YQStreamedTravelVerification
{
    private const string KeyPrefix = "YQStreamedTravel.";
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static InputSettings originalSettings, testSettings;
    private static bool moveHeld;
    private static YQInvestorPlayerMotor inputMotor;
    private static Vector3 desiredDirection;
    private static double nextReport;
    private static double nextTick;
    private static readonly HashSet<string> Visited = new HashSet<string>();
    private static readonly FieldInfo SiteId = typeof(YQCompiledWorldSiteInstance).GetField("settlementId", BindingFlags.NonPublic | BindingFlags.Instance);

    static YQStreamedTravelVerification() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }

    public static void RunFromCommandLine()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Idle owned batch Editor required.");
        EditorSceneManager.OpenScene("Assets/Assets/Scenes/YourQuest_PlaySafe.unity", OpenSceneMode.Single);
        var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(Application.persistentDataPath, "Profiles/profiles_manifest.json")));
        SessionState.SetString(KeyPrefix + "profile", (string)manifest["activeProfileId"]);
        SessionState.SetString(KeyPrefix + "started", DateTime.UtcNow.ToString("O"));
        SessionState.SetBool(KeyPrefix + "running", true);
        SessionState.SetBool(KeyPrefix + "ending", false);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(KeyPrefix + "running", false) || EditorApplication.isCompiling) return;
        if (SessionState.GetBool(KeyPrefix + "ending", false))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(KeyPrefix + "running", false);
            EditorApplication.Exit(SessionState.GetBool(KeyPrefix + "success", false) ? 0 : 1);
            return;
        }
        if (!EditorApplication.isPlaying) return;
        if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(KeyPrefix + "started", ""), null, System.Globalization.DateTimeStyles.RoundtripKind) > TimeSpan.FromMinutes(12))
        { Finish(false, "Twelve minute assisted traversal bound elapsed; inspect partial receipts."); return; }
        // note: Observation stays at five hertz; held keyboard state continues between samples.
        if (EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .2d;
        if (!YQTitleScreenUI.StartupFlowComplete && YQTitleScreenUI.Instance != null)
            YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(SessionState.GetString(KeyPrefix + "profile", ""));
        if (!YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) { Report("WAITING_FOR_CONTINUE", null); return; }
        var motor = UnityEngine.Object.FindFirstObjectByType<YQInvestorPlayerMotor>();
        var world = WorldStateManager.Instance?.State;
        var player = PlayerStateManager.Instance?.state;
        if (motor == null || !motor.IsAuthoritative || world == null || player == null) return;
        if (keyboard == null)
        {
            // note: A headless batch has no focused Game view; only this owned fixture ignores focus so its real device events reach the motor.
            Application.runInBackground = true;
            originalSettings = InputSystem.settings;
            testSettings = UnityEngine.Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>("StreamedTravelWitness");
            mouse = InputSystem.AddDevice<Mouse>("StreamedTravelWitnessMouse");
            InputSystem.onBeforeUpdate += QueueInput;
            InputSystem.onAfterUpdate += KeepDevicesCurrent;
        }
        inputMotor = motor;
        if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        var locations = world.generatedWorldPlan?.spatialPlanV2?.acceptedContinuation?.locations;
        GeneratedSpatialContinuationLocationV2Record destination = null;
        float nearest = float.PositiveInfinity;
        Vector3 logical = motor.transform.position + player.renderOrigin;
        if (locations != null)
            foreach (var location in locations)
            {
                string kind = location.anchor.kind.ToString();
                if (Visited.Contains(kind)) continue;
                float distance = Vector2.Distance(new Vector2(logical.x, logical.z), new Vector2(location.anchor.x, location.anchor.z));
                if (distance < nearest) { nearest = distance; destination = location; }
            }
        int npcs = 0, enemies = 0, gear = 0;
        bool loaded = false;
        if (destination != null)
        {
            foreach (var owner in UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsSortMode.None))
                if ((string)SiteId.GetValue(owner) == destination.anchor.sourceSemanticId && owner.IsLoaded)
                {
                    int rendered = 0, colliders = 0;
                    foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(false)) if (renderer.enabled) rendered++;
                    foreach (Collider collider in owner.GetComponentsInChildren<Collider>(false)) if (collider.enabled && !collider.isTrigger) colliders++;
                    loaded = rendered > 0 && colliders > 0;
                }
            foreach (var actor in UnityEngine.Object.FindObjectsByType<EntityInfo>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(actor.transform.position, motor.transform.position) > 200f) continue;
                if (actor.GetComponent<NpcDialogueAgent>() != null) npcs++;
                if (actor.GetComponent<YQInvestorEnemy>() != null) enemies++;
                var inventory = YQContainerInventory.Find(world, actor.entityId);
                if (inventory?.equippedItemBySlot.Count > 0 && actor.GetComponent<YQGeneratedActorEquipment>() != null)
                    foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(false))
                        if (renderer.enabled && IsEquipmentRenderer(renderer.transform, actor.transform)) { gear++; break; }
            }
            string kind = destination.anchor.kind.ToString();
            bool population = kind == "Settlement" ? npcs > 0 && gear > 0 : kind != "HostileSite" || enemies > 0;
            if (nearest < 180f && loaded && population) { Visited.Add(kind); Debug.Log("[YQStreamedTravel] WALKED_TO_" + kind); }
            if (!Visited.Contains(kind) && nearest > 45f)
            {
                Vector3 direction = new Vector3(destination.anchor.x - logical.x, 0f, destination.anchor.z - logical.z);
                desiredDirection = direction;
                // note: The existing G08 device routing supplies real mouse turns and keys; speed, physics and movement gates stay production-owned.
                SetMovementKeys(true);
            }
            else SetMovementKeys(false);
        }
        else SetMovementKeys(false);
        Report(destination == null ? "WAITING_FOR_ACCEPTED_SITE" : "WALKING", new { logical, nearest, loaded, npcs, enemies, gear,
            target = destination?.anchor.sourceSemanticId, visited = new List<string>(Visited), accepted = locations?.Count ?? 0,
            moveInput = motor.MoveInput.ToString(), inputGate = motor.CanProcessMovementInput, keyboardEnabled = keyboard.enabled,
            currentKeyboard = Keyboard.current == keyboard, timeScale = Time.timeScale });
        if (Visited.Count >= 3) Finish(true, "Motor traversal encountered all three streamed site categories.");
    }

    private static void SetMovementKeys(bool moving)
    {
        moveHeld = moving;
    }

    private static void QueueInput()
    {
        if (keyboard == null || mouse == null || inputMotor == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
        float angle = desiredDirection.sqrMagnitude > .001f ? Vector3.SignedAngle(inputMotor.transform.forward, desiredDirection, Vector3.up) : 0f;
        InputSystem.QueueStateEvent(keyboard, moveHeld ? new KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift) : new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(Mathf.Clamp(angle, -12f, 12f) / Mathf.Max(.001f, inputMotor.sensitivityX), 0f) });
        KeepDevicesCurrent();
    }

    private static void KeepDevicesCurrent()
    {
        keyboard?.MakeCurrent(); mouse?.MakeCurrent();
    }

    private static bool IsEquipmentRenderer(Transform node, Transform actor)
    {
        for (; node != null && node != actor; node = node.parent)
            if (node.name.StartsWith("Equipped__", StringComparison.Ordinal)) return true;
        return false;
    }

    private static void Report(string status, object witness)
    {
        if (EditorApplication.timeSinceStartup < nextReport) return;
        nextReport = EditorApplication.timeSinceStartup + 15d;
        Directory.CreateDirectory("outputs/StreamedRepair_20261005");
        File.AppendAllText("outputs/StreamedRepair_20261005/TravelWitness.jsonl", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, status, witness },
            new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }) + Environment.NewLine);
        Debug.Log("[YQStreamedTravel] " + status + " " + JsonConvert.SerializeObject(witness, new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }));
    }

    private static void Finish(bool success, string reason)
    {
        Directory.CreateDirectory("outputs/StreamedRepair_20261005");
        InputSystem.onBeforeUpdate -= QueueInput; InputSystem.onAfterUpdate -= KeepDevicesCurrent;
        if (keyboard != null) { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); keyboard = null; }
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        if (originalSettings != null) InputSystem.settings = originalSettings;
        if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        File.WriteAllText("outputs/StreamedRepair_20261005/TravelResult.json", JsonConvert.SerializeObject(new { success, reason, visited = new List<string>(Visited) }, Formatting.Indented));
        SessionState.SetBool(KeyPrefix + "success", success);
        SessionState.SetBool(KeyPrefix + "ending", true);
        EditorApplication.isPlaying = false;
    }
}
#endif
