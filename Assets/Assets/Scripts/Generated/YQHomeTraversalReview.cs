using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// note: Editor-only interaction harness; production movement remains owned by the existing player motor.
public sealed class YQHomeTraversalReview : MonoBehaviour
{
    public const string ScenePath = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/YQ_HomeTraversalReview.unity";
    public const string TimberScenePath = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/YQ_TimberHomeTraversalReview.unity";
    public Transform player;
    public Camera view;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public static bool IsIsolatedReview
    {
        get
        {
#if UNITY_EDITOR
            // note: Only this exact editor test scene bypasses production startup; standalone builds always use the normal bootstrap.
            var start = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
            string path = start != null ? UnityEditor.AssetDatabase.GetAssetPath(start) :
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            // note: Only these explicit unsaved review fixtures bypass startup, never arbitrary scenes.
            return path == ScenePath || path == TimberScenePath;
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR
    private void Start()
    {
        // note: Refuse interaction if a production save owner exists; the fixture must never write to a selected player profile.
        if (!IsIsolatedReview || player == null || PlayerStateManager.Instance != null || WorldStateManager.Instance != null || YQProfileSaveSystem.Instance != null)
        { enabled = false; Debug.LogError("Home traversal review requires its isolated scene without save managers."); return; }
        startPosition = player.position;
        startRotation = player.rotation;
        // note: Only an explicitly requested batch fixture receives simulated input; interactive reviews retain normal controls.
        if (Application.isBatchMode && UnityEditor.SessionState.GetBool("YQTimberMotorBatch", false))
            StartCoroutine(VerifyBatchMovement());
    }

    public static void RunTimberMotorBatch()
    {
        // note: This entry point opens only the disposable review scene and never a gameplay profile.
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Batch-only review entry point.");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TimberScenePath);
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(TimberScenePath);
        UnityEditor.SessionState.SetBool("YQTimberMotorBatch", true);
        UnityEditor.EditorApplication.isPlaying = true;
    }

    private System.Collections.IEnumerator VerifyBatchMovement()
    {
        // note: Exercise the production Update/input/controller path for approach movement, not an alternative motor or teleport.
        // note: A batch editor has no focused Game view. Use a disposable settings copy so focus filtering does not reset the virtual keyboard.
        var originalSettings = InputSystem.settings;
        var testSettings = Instantiate(originalSettings);
        testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testSettings;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        keyboard.MakeCurrent();
        Vector3 initial = player.position;
        var motor = player.GetComponent<YQInvestorPlayerMotor>();
        // note: Snapshot the closed doorway plane before the provider rotates the door leaf.
        var door = FindFirstObjectByType<YQLockpickableDoor>();
        // note: Match production material binding on the disposable house instance; the isolated scene does not run the world materializer.
        if (door != null) YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(door.transform.root.gameObject);
        Vector3 doorway = door != null ? door.transform.position : initial;
        Vector3 inward = player.forward;
        bool opened = false, crossed = false;
        int frames = 0, keyFrames = 0, motorFrames = 0, lockedFrames = 0;
        float simulatedSeconds = 0f;
        // note: Cold editor frames can take seconds; measure gameplay time while retaining a bounded wall-clock watchdog.
        float deadline = Time.realtimeSinceStartup + 45f;
        while (simulatedSeconds < 4f && !crossed && Time.realtimeSinceStartup < deadline)
        {
            // note: Request the existing unlocked interaction only at player reach; do not disable or move the collider for the test.
            if (!opened && door != null && Vector2.Distance(new Vector2(player.position.x, player.position.z),
                    new Vector2(doorway.x, doorway.z)) < 2.5f)
                opened = door.TryInteract(player.gameObject);
            // note: A headless editor may discard queued background-device events; set only the disposable test device state directly.
            UnityEngine.InputSystem.LowLevel.InputState.Change(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));
            keyboard.MakeCurrent();
            yield return null;
            // note: Record input delivery separately from motion so a headless input failure is not misdiagnosed as blocked geometry.
            frames++;
            if (Keyboard.current == keyboard && keyboard.wKey.isPressed) keyFrames++;
            if (motor.MoveInput.sqrMagnitude > .01f) motorFrames++;
            if (RuntimeModalUiBlocker.IsBlocked || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked) lockedFrames++;
            simulatedSeconds += Time.deltaTime;
            crossed = opened && Vector3.Dot(player.position - doorway, inward) >= 1f && Mathf.Abs(player.position.y - doorway.y) < .5f;
        }
        // note: Return through the same open doorway using normal reverse input; never teleport to satisfy the exit check.
        if (crossed && view != null)
        {
            // note: Record actual draw materials at capture time; preview adapters and serialized slots can differ from live bindings.
            var materialReport = new System.Text.StringBuilder();
            foreach (var renderer in door.transform.root.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.IndexOf("Bed", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    foreach (var material in renderer.sharedMaterials)
                        materialReport.AppendLine(renderer.name + ": " + (material == null ? "NULL" :
                            material.name + " shader=" + material.shader.name + " supported=" + material.shader.isSupported +
                            " pipeline=" + material.GetTag("RenderPipeline", false) + " texture=" +
                            (material.mainTexture != null ? material.mainTexture.name : "NULL")));
            System.IO.File.WriteAllText("Logs/YQTimberBedMaterials.txt", materialReport.ToString());
            // note: Capture the actual production camera pose at the successful interior endpoint, not a detached review camera.
            var previousTarget = view.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                view.targetTexture = target;
                view.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes("Logs/YQTimberInteriorPlayerView.png", pixels.EncodeToPNG());
            }
            finally
            {
                view.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
            }
        }
        bool returned = false;
        float returnSeconds = 0f;
        while (crossed && !returned && returnSeconds < 4f && Time.realtimeSinceStartup < deadline)
        {
            UnityEngine.InputSystem.LowLevel.InputState.Change(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.S));
            keyboard.MakeCurrent();
            yield return null;
            returnSeconds += Time.deltaTime;
            returned = Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(initial.x, initial.z)) < .4f &&
                Mathf.Abs(player.position.y - initial.y) < .4f;
        }
        UnityEngine.InputSystem.LowLevel.InputState.Change(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        yield return null;
        InputSystem.RemoveDevice(keyboard);
        InputSystem.settings = originalSettings;
        Destroy(testSettings);
        Vector3 displacement = player.position - initial;
        float horizontal = new Vector2(displacement.x, displacement.z).magnitude;
        bool passed = crossed && returned && motorFrames > 0 &&
            PlayerStateManager.Instance == null && WorldStateManager.Instance == null &&
            YQProfileSaveSystem.Instance == null &&
            YQInvestorPlayerMotor.ActiveMotor == player.GetComponent<YQInvestorPlayerMotor>();
        System.IO.File.WriteAllText("Logs/YQTimberMotorBatch.txt", (passed ? "PASS" : "FAIL") +
            ": production motor approach movement=" + horizontal + "m, vertical=" + displacement.y +
            "m. Frames=" + frames + ", deliveredKey=" + keyFrames + ", motorInput=" + motorFrames +
            ", locked=" + lockedFrames + ", simulatedSeconds=" + simulatedSeconds +
            ", doorOpened=" + opened + ", interiorPlaneCrossed=" + crossed + ", returnedToStart=" + returned +
            ", returnSeconds=" + returnSeconds +
            ". Single doorway round trip only; room activities and generated-world traversal remain unverified.");
        UnityEditor.SessionState.SetBool("YQTimberMotorBatch", false);
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
        UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
    }

    private void Update()
    {
        // note: E delegates to the real unlocked-door provider; R resets only this disposable scene's player pose.
        if (Keyboard.current == null || player == null || view == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame &&
            Physics.Raycast(view.transform.position, view.transform.forward, out var hit, 2.55f))
        {
            var door = hit.collider.GetComponentInParent<YQLockpickableDoor>();
            if (door != null && PlayerStateManager.Instance == null && WorldStateManager.Instance == null)
                door.TryInteract(player.gameObject);
        }
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.SetPositionAndRotation(startPosition, startRotation);
            if (controller != null) controller.enabled = true;
        }
    }

    private void OnGUI()
    {
        // note: Instructions identify this as a validation fixture, not an accepted generated location.
        GUI.Box(new Rect(12, 12, 610, 64), "HOME TRAVERSAL REVIEW — unsaved test world\nWASD/mouse: move/look   E: open door   R: reset position\nCheck stairs, threshold, sleeping/storage/work access. Stop Play Mode to exit.");
    }
#endif
}
