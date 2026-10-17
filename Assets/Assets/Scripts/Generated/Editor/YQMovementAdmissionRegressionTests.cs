using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// note: Disposable edit-mode fixtures exercise the real admission/accounting methods without touching a save, loading assets, or certifying runtime travel.
public static class YQMovementAdmissionRegressionTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("YourQuest/Verification/Run Movement Admission Regression")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the disposable movement admission fixture outside Play Mode.");
        GameObject player = null, worldRoot = null, stateOwnerRoot = null;
        var activeProperty = typeof(YQPlayerFollowingSemanticChunkStreamer).GetProperty("Active", Static);
        var previousActive = activeProperty.GetValue(null);
        var worldOwnerProperty = typeof(WorldStateManager).GetProperty("Instance", Static);
        var previousWorldOwner = worldOwnerProperty.GetValue(null);
        try
        {
            activeProperty.GetSetMethod(true).Invoke(null, new object[] { null });
            player = new GameObject("YQ_MovementAdmissionFixture") { hideFlags = HideFlags.HideAndDontSave };
            player.SetActive(false);
            var motor = player.AddComponent<YQInvestorPlayerMotor>();
            Set(motor, "_controller", player.GetComponent<CharacterController>());
            Require(ResolveAdmission(motor, out var resolved) && resolved == null,
                "An authored scene without a streamed-world dependency must remain admitted.");

            worldRoot = new GameObject("YQ_StreamingAdmissionFixture") { hideFlags = HideFlags.HideAndDontSave };
            var streamer = worldRoot.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            activeProperty.GetSetMethod(true).Invoke(null, new object[] { streamer });
            // note: Install only a disposable inactive state owner; its Awake/load/save callbacks never run and any existing singleton is restored below.
            stateOwnerRoot = new GameObject("YQ_AdmissionStateFixture") { hideFlags = HideFlags.HideAndDontSave };
            stateOwnerRoot.SetActive(false);
            var stateOwner = stateOwnerRoot.AddComponent<WorldStateManager>();
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new object[] { stateOwner });
            var plan = new GeneratedWorldPlanRecord();
            var world = new WorldState { generatedWorldPlan = plan };
            var stateProperty = typeof(WorldStateManager).GetProperty("State", Instance);
            stateProperty.GetSetMethod(true).Invoke(stateOwner, new object[] { world });
            Call(motor, "BindStreamedMovementAdmission", streamer, world);
            Set(streamer, "_playerMotor", motor);
            Set(streamer, "_movementAdmissionWorldOwner", stateOwner);
            Set(streamer, "_world", world);
            Set(streamer, "_plan", plan);
            Set(streamer, "_configured", false);
            Require(!ResolveAdmission(motor, out _), "An incomplete bind admitted movement.");
            Set(streamer, "_configured", true);
            Set(streamer, "_movementAdmissionUnavailable", false);
            Require(ResolveAdmission(motor, out resolved) && resolved == streamer,
                "A configured live owner did not resolve as the motor's admission dependency.");
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new object[] { null });
            Require(!ResolveAdmission(motor, out _), "A missing selected world owner left stale movement permission.");
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new object[] { stateOwner });
            var replacementStateOwner = stateOwnerRoot.AddComponent<WorldStateManager>();
            stateProperty.GetSetMethod(true).Invoke(replacementStateOwner, new object[] { world });
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new object[] { replacementStateOwner });
            Require(!ResolveAdmission(motor, out _), "A replacement manager reused another owner's admission proof.");
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new object[] { stateOwner });
            world.generatedWorldPlan = new GeneratedWorldPlanRecord();
            Require(!ResolveAdmission(motor, out _), "An in-place world-plan replacement left stale movement permission.");
            world.generatedWorldPlan = plan;
            stateProperty.GetSetMethod(true).Invoke(stateOwner, new object[] { new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord() } });
            Require(!ResolveAdmission(motor, out _), "A newly selected generated world reused the old admission dependency.");
            stateProperty.GetSetMethod(true).Invoke(stateOwner, new object[] { new WorldState() });
            Require(ResolveAdmission(motor, out resolved) && resolved == null,
                "A newly selected authored world retained the retired generated world's dependency.");
            stateProperty.GetSetMethod(true).Invoke(stateOwner, new object[] { world });

            streamer.enabled = false;
            // note: Edit-mode fixtures invoke cleanup explicitly because a non-ExecuteAlways component need not receive Unity's play-mode lifecycle callback here.
            Call(streamer, "OnDisable");
            Require(!ResolveAdmission(motor, out _), "A disabled required streamer admitted movement.");
            Require(!streamer.TryConstrainMovement(Vector3.zero, Vector3.right, .3f, .08f, out var permitted) && permitted == Vector3.zero,
                "The direct constraint API admitted movement through a disabled owner.");
            streamer.enabled = true;
            Require(!ResolveAdmission(motor, out _), "Re-enabling a cancelled owner bypassed the required successful rebind.");

            var ragdoll = player.AddComponent<YQPlayerRagdoll>();
            Set(ragdoll, "motor", motor);
            Require(!(bool)Call(ragdoll, "PreparePhysicalAdmission"), "A new physical actor bypassed unavailable streamed admission.");
            object[] recovery = { Vector3.zero, Vector3.zero };
            Require(!(bool)Call(ragdoll, "TryGroundedPosition", recovery) && Get<bool>(ragdoll, "recoveryAdmissionRejected"),
                "Recovery did not retain the unavailable-admission reason.");

            VerifyHeldMotion(motor);
            VerifySharedBudget(streamer);
            UnityEngine.Object.DestroyImmediate(worldRoot);
            worldRoot = null;
            Require(!ResolveAdmission(motor, out _), "Destroying the required owner reopened movement admission.");
            Debug.Log("[YQMovementAdmissionRegression] PASS: authored-scene admission, bind/disable/re-enable/destroy guards, physical recovery, held integration, retained real landing, and all budget reserves. Edit-mode contract evidence only; PlaySafe/cold-build/200 m/s qualification remains required.");
        }
        finally
        {
            if (worldRoot != null) UnityEngine.Object.DestroyImmediate(worldRoot);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (stateOwnerRoot != null) UnityEngine.Object.DestroyImmediate(stateOwnerRoot);
            worldOwnerProperty.GetSetMethod(true).Invoke(null, new[] { previousWorldOwner });
            activeProperty.GetSetMethod(true).Invoke(null, new[] { previousActive });
        }
    }

    private static void VerifyHeldMotion(YQInvestorPlayerMotor motor)
    {
        Set(motor, "_planarVelocity", Vector3.right * 200f);
        Set(motor, "_verticalVelocity", -10f);
        Set(motor, "_dashTimeRemaining", .5f);
        Set(motor, "_sampleVelocity", new Vector3(200f, -10f, 0f));
        Set(motor, "_sampleElapsed", .08f);
        Set(motor, "_fallTracked", true);
        Set(motor, "_fallApex", 25f);
        for (int frame = 0; frame < 30; frame++)
        {
            Call(motor, "BeginMotionFrame", .1f);
            Call(motor, "CaptureMotionIntegrationState");
            Set(motor, "_planarVelocity", Vector3.right * 250f);
            Set(motor, "_verticalVelocity", -14.35f);
            Set(motor, "_dashTimeRemaining", .4f);
            Set(motor, "_frameMovementHeld", true);
            Call(motor, "EndMotionFrame", .1f);
            Require(Get<Vector3>(motor, "_planarVelocity") == Vector3.right * 200f &&
                Mathf.Approximately(Get<float>(motor, "_verticalVelocity"), -10f) &&
                Mathf.Approximately(Get<float>(motor, "_dashTimeRemaining"), .5f),
                "An entirely held step accumulated unsimulated acceleration, gravity, or dash time.");
            Require(Get<bool>(motor, "_fallTracked") && Mathf.Approximately(Get<float>(motor, "_fallApex"), 25f) &&
                Mathf.Approximately(Get<float>(motor, "_sampleElapsed"), .08f) &&
                Get<Vector3>(motor, "_sampleVelocity") == new Vector3(200f, -10f, 0f),
                "An admission hold erased real fall history or introduced a physical acceleration sample.");
        }

        // note: An accepted launch is captured before time integration, so a hold must retain that launch rather than refunding or cancelling it.
        Call(motor, "BeginMotionFrame", .1f);
        Set(motor, "_verticalVelocity", 12f);
        Call(motor, "CaptureMotionIntegrationState");
        Set(motor, "_verticalVelocity", 9f);
        Set(motor, "_frameMovementHeld", true);
        Call(motor, "EndMotionFrame", .1f);
        Require(Mathf.Approximately(Get<float>(motor, "_verticalVelocity"), 12f), "A held step discarded the accepted launch impulse.");

        // note: A real collision followed by denied step assistance must still consume the real displacement/landing instead of taking the full-hold shortcut.
        Set(motor, "_dashTimeRemaining", 0f);
        Call(motor, "BeginMotionFrame", .1f);
        Call(motor, "CaptureMotionIntegrationState");
        Set(motor, "_frameControllerMoveAdmitted", true);
        Set(motor, "_frameMovementHeld", true);
        Set(motor, "_frameLanded", true);
        motor.transform.position += Vector3.right;
        Call(motor, "EndMotionFrame", .1f);
        Require(!Get<bool>(motor, "_fallTracked") &&
            Get<Vector3>(motor, "_planarVelocity") == Vector3.right * 10f,
            "A later denied assist suppressed an earlier physical landing or collision-resolved momentum.");
    }

    private static void VerifySharedBudget(YQPlayerFollowingSemanticChunkStreamer streamer)
    {
        float limit = (float)typeof(YQPlayerFollowingSemanticChunkStreamer)
            .GetField("AggregateMainThreadBudgetSeconds", Static).GetRawConstantValue();
        Set(streamer, "_aggregateBudgetFrame", Time.frameCount);
        Set(streamer, "_aggregateFrameWorkSeconds", limit);
        Set(streamer, "_lifecycleBudgetFrame", Time.frameCount);
        Set(streamer, "_lifecycleDeadline", Time.realtimeSinceStartup + 10f);
        foreach (string stage in new[] { "lifecycleActivation", "lifecycleObjectActivation", "hardViewTerrainAppearance",
            "turnBufferGroundProgress", "terrainHeightUpload", "terrainColliderSync", "contentDispatch",
            "requiredContent", "requiredHardViewEcology", "emergencyGroundPublication", "provisionalGroundReactivation" })
        {
            Require(!(bool)Call(streamer, "CanAdvanceAggregateWork", stage, true, true, true, true,
                (Vector2Int?)Vector2Int.zero, (Vector2Int?)Vector2Int.zero),
                "The " + stage + " reserve bypassed the exhausted aggregate allowance.");
        }
        Set(streamer, "_aggregateFrameWorkSeconds", 0f);
        Require((bool)Call(streamer, "CanAdvanceAggregateWork", "ordinaryFixtureSlice", false, false, false, false, null, null),
            "The shared allowance denied ordinary work in an unspent frame.");
    }

    private static bool ResolveAdmission(YQInvestorPlayerMotor motor, out YQPlayerFollowingSemanticChunkStreamer owner)
    {
        object[] args = { null };
        bool admitted = (bool)Call(motor, "TryResolveStreamedMovementAdmission", args);
        owner = args[0] as YQPlayerFollowingSemanticChunkStreamer;
        return admitted;
    }

    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Instance).Invoke(target, args);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);
    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
