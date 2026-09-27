#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Explicit, isolated geometry fixtures; no gameplay player, scene saves, or Play-mode transition.
public static class YQRouteTraversalProbeVerification
{
    private const string Request = "Temp/YQRouteTraversalProbeVerification.request";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Verify();
        };
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Route Traversal Probe")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run outside Play mode.");
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            PhysicsScene physics = scene.GetPhysicsScene();
            Require(physics.IsValid() && physics != Physics.defaultPhysicsScene, "Fixtures require isolated physics.");
            var player = NewObject(scene, "Measurement-only capsule");
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .38f;
            controller.center = new Vector3(.2f, 1.1f, -.1f);
            controller.skinWidth = .02f;
            controller.stepOffset = .52f;
            controller.slopeLimit = 58f;
            Require(YQRouteTraversalProbe.Shape.TryCapture(controller, out var shape), "Capture upright capsule.");
            Require(Mathf.Abs(shape.radius - .38f) < .0001f && Mathf.Abs(shape.height - 1.8f) < .0001f,
                "Use actual player dimensions, not the old .34m capsule.");
            Require(Vector3.Distance(shape.rootToFeet, new Vector3(.2f, .2f, -.1f)) < .0001f, "Preserve center offset.");
            shape.Capsule(Vector3.zero, out var lower, out var upper, out float radius);
            Require(Mathf.Abs(lower.y - radius - .02f) < .0001f && Mathf.Abs(upper.y + radius - 1.78f) < .0001f,
                "Capsule endpoints preserve skin contact tolerance.");

            BoxCollider floor = Box(scene, "Floor", new Vector3(0, -.25f, 0), new Vector3(12, .5f, 12));
            var probe = new YQRouteTraversalProbe(physics, shape, player.transform);
            Sync(physics);
            Require(probe.Measure(Vector3.zero, false, default).issues == YQRouteTraversalProbe.Issue.None, "Flat floor is clear.");
            // note: A landing over lower ground must be acquired as support and crossed through the controller's legal step-up path.
            var landing = Box(scene, "Low landing", new Vector3(1.5f, .06f, 0), new Vector3(2f, .12f, 2f));
            Sync(physics);
            var stepped = probe.Measure(new Vector3(1f, 0f, 0f), true, Vector3.zero);
            Require(stepped.supportCollider == landing && Mathf.Abs(stepped.feet.y - .12f) < .001f &&
                stepped.issues == YQRouteTraversalProbe.Issue.None, "Low landing must be walkable above underlying ground.");
            var edgeStep = probe.Measure(new Vector3(.3f, 0f, 0f), true, Vector3.zero);
            Require(edgeStep.issues == YQRouteTraversalProbe.Issue.None && Mathf.Abs(edgeStep.feet.y - .12f) < .001f,
                "Capsule must acquire the low tread before its centre ray reaches the landing edge.");
            UnityEngine.Object.DestroyImmediate(landing.gameObject);
            Sync(physics);
            var wall = Box(scene, "Thin wall", new Vector3(0, 1, 0), new Vector3(.04f, 2, 2));
            Sync(physics);
            var crossed = probe.Measure(new Vector3(2, 0, 0), true, new Vector3(-2, 0, 0));
            Require((crossed.issues & YQRouteTraversalProbe.Issue.SweptObstacle) != 0, "Sweep detects a wall between clear endpoints.");
            Require(wall.enabled, "Measurement never removes a blocker.");
            Require(crossed.supportCollider == floor && crossed.sweepCollider == wall && crossed.overlapCollider == null,
                "A thin-wall crossing must identify the actual floor and swept wall, without inventing an endpoint overlap.");
            // note: Create a second wall later in the same sweep; evidence must not depend on the physics buffer's hit ordering.
            var fartherWall = Box(scene, "Farther wall", new Vector3(1, 1, 0), new Vector3(.04f, 2, 2));
            Sync(physics);
            Require(probe.Measure(new Vector3(2, 0, 0), true, new Vector3(-2, 0, 0)).sweepCollider == wall,
                "The reported sweep obstacle must be the nearest returned contact.");
            UnityEngine.Object.DestroyImmediate(fartherWall.gameObject);
            // note: Change only isolated fixture pairs, restoring their states even if an assertion fails. Never alter the project layer matrix.
            bool ignoredWall = Physics.GetIgnoreCollision(controller, wall);
            bool ignoredFloor = Physics.GetIgnoreCollision(controller, floor);
            try
            {
                Physics.IgnoreCollision(controller, wall, true);
                Require(probe.Measure(Vector3.zero, false, default).issues == YQRouteTraversalProbe.Issue.None,
                    "A wall ignored by the controller must not block standing clearance.");
                Require((probe.Measure(new Vector3(2, 0, 0), true, new Vector3(-2, 0, 0)).issues &
                    YQRouteTraversalProbe.Issue.SweptObstacle) == 0,
                    "A controller-ignored wall must not block the route sweep.");
                Physics.IgnoreCollision(controller, floor, true);
                Require(!probe.Measure(Vector3.zero, false, default).hasSupport,
                    "A controller-ignored floor cannot count as walkable support.");
            }
            finally
            {
                Physics.IgnoreCollision(controller, floor, ignoredFloor);
                Physics.IgnoreCollision(controller, wall, ignoredWall);
            }
            Require((probe.Measure(new Vector3(2, 0, 0), true, new Vector3(-2, 0, 0)).issues &
                YQRouteTraversalProbe.Issue.SweptObstacle) != 0, "Restored wall collision must be observed without a stale policy cache.");
            UnityEngine.Object.DestroyImmediate(wall.gameObject);

            var ceiling = Box(scene, "Low ceiling", new Vector3(0, 1.65f, 0), new Vector3(3, .1f, 3));
            Sync(physics);
            var low = probe.Measure(Vector3.zero, false, default);
            Require(low.supportCollider == floor && low.overlapCollider == ceiling,
                "Low-ceiling diagnostics must distinguish the floor from the actual obstruction.");
            Require((low.issues & YQRouteTraversalProbe.Issue.Occupied) != 0 && Mathf.Abs(low.feet.y) < .001f,
                "Low ceiling blocks the standing player but is not mistaken for ground.");
            UnityEngine.Object.DestroyImmediate(ceiling.gameObject);
            Require(!probe.Measure(new Vector3(20, 0, 0), false, default).hasSupport, "Missing floor is not approved.");

            // note: The centre ray hits this beam, but the actual player's footprint extends over empty space.
            Box(scene, "Narrow beam", new Vector3(20, -.25f, 0), new Vector3(.12f, .5f, 3));
            Sync(physics);
            var beam = probe.Measure(new Vector3(20, 0, 0), false, default);
            Require(beam.hasSupport && (beam.issues & YQRouteTraversalProbe.Issue.UnsupportedFootprint) != 0,
                "A centre-supported narrow beam must not report full footprint support.");
            Require(probe.Measure(new Vector3(float.NaN, 0, 0), false, default).issues == YQRouteTraversalProbe.Issue.InvalidInput,
                "Nonfinite positions must be rejected before native physics.");
            Require(!default(YQRouteTraversalProbe.Shape).IsValid, "An uncaptured default capsule is invalid.");

            // note: More support shapes than the bounded query can hold must remain inconclusive, even if they overlap exactly.
            for (int i = 0; i < 65; i++)
                Box(scene, "Crowded support " + i, new Vector3(30, -.25f, 0), new Vector3(2, .5f, 2));
            Sync(physics);
            var crowded = probe.Measure(new Vector3(30, 0, 0), false, default);
            Require(!crowded.hasSupport && (crowded.issues & YQRouteTraversalProbe.Issue.QuerySaturated) != 0,
                "A saturated support query must not select an arbitrary incomplete hit.");

            // note: Exercise the actual legacy route consumer, not just the new probe; size must never authorize collider deletion.
            var shortWall = Box(scene, "Short wall", new Vector3(3, .7f, 0), new Vector3(.3f, 1.4f, 1));
            Sync(physics);
            var routeReport = new YQGeneratedWorldIntegrityValidator.RouteReport();
            var reportedBlockers = new HashSet<int>();
            MethodInfo inspect = typeof(YQGeneratedWorldIntegrityValidator).GetMethod("InspectRouteClearance",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(inspect != null, "Legacy route inspection test entry must exist.");
            // note: Reflection stays in this editor regression so the private runtime implementation need not become public API.
            object[] inspectArgs = { physics, new Vector2(3, 0), 0f, "fixture-route", "fixture-seed", reportedBlockers, routeReport };
            inspect.Invoke(null, inspectArgs);
            inspect.Invoke(null, inspectArgs);
            Require(shortWall.enabled && routeReport.repairedDecorativeBlockers == 0 &&
                routeReport.unresolvedStructuralBlockers == 1,
                "Repeated route inspection must preserve a short wall and report it only once.");

            // note: An elevated floor must be measured at its own walking height, not at lower terrain height.
            Box(scene, "Bridge", new Vector3(0, 2.75f, 0), new Vector3(3, .5f, 3));
            Sync(physics);
            var bridge = probe.Measure(new Vector3(0, 3, 0), false, default);
            Require(bridge.hasSupport && Mathf.Abs(bridge.feet.y - 3f) < .001f, "Resolve elevated support.");
            player.transform.localScale = new Vector3(2, 1.5f, 1);
            Require(YQRouteTraversalProbe.Shape.TryCapture(controller, out var scaled) &&
                Mathf.Abs(scaled.radius - .76f) < .001f && Mathf.Abs(scaled.height - 2.7f) < .001f, "Capture nonuniform scale.");
            player.transform.rotation = Quaternion.Euler(30, 0, 0);
            Require(!YQRouteTraversalProbe.Shape.TryCapture(controller, out _), "Unsupported tilted capsule is inconclusive.");
            // note: Exercise the shared home-review implementation, including the formerly false-positive narrow doorway.
            string passageFailure = YQCellPassageReviewV2.TestCollisionEvidence();
            Require(string.IsNullOrEmpty(passageFailure), passageFailure);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQRouteTraversalProbeVerification.txt", "PASS: isolated capsule, floor, thin-wall sweep, collision-pair filtering/restoration, low ceiling, missing support, footprint/beam, saturated queries, invalid input, preserved short-wall collision, bridge, scale, tilt and actual-width home passage fixtures. Not visual route certification.");
            Debug.Log("[YQRouteTraversalProbeVerification] PASS. See Logs/YQRouteTraversalProbeVerification.txt.");
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/YQRouteTraversalProbeVerification.txt", "FAIL: " + exception);
            Debug.LogException(exception);
            throw;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        // note: Only inspect the real home after synthetic geometry tests pass; keep candidate failures separate from test results.
        try
        {
            File.WriteAllText("Logs/YQExistingHomeTraversalReview.md", YQCandidateInteriorBuilderV2.InspectExistingCandidate());
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/YQExistingHomeTraversalReview.md", "NOT CERTIFIED: candidate review failed. " + exception);
            Debug.LogException(exception);
        }
    }

    private static GameObject NewObject(Scene scene, string name)
    {
        var value = new GameObject(name);
        SceneManager.MoveGameObjectToScene(value, scene);
        return value;
    }

    private static BoxCollider Box(Scene scene, string name, Vector3 position, Vector3 size)
    {
        var value = NewObject(scene, name);
        value.transform.position = position;
        var collider = value.AddComponent<BoxCollider>();
        collider.size = size;
        return collider;
    }

    private static void Sync(PhysicsScene physics)
    {
        // note: Only advance the isolated fixture physics, never the user's gameplay scene.
        Physics.SyncTransforms();
        physics.Simulate(.02f);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
