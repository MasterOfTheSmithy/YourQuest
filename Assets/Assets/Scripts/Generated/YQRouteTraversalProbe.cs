using System;
using UnityEngine;

// note: Read-only geometric diagnostics, not a semantic approval or a replacement for the loading gate.
public sealed class YQRouteTraversalProbe
{
    [Flags]
    public enum Issue
    {
        None = 0, MissingSupport = 1, SteepSupport = 2, Occupied = 4,
        SweptObstacle = 8, QuerySaturated = 16, AmbiguousSupportOverlap = 32,
        UnsupportedFootprint = 64, InvalidInput = 128
    }

    public readonly struct Shape
    {
        public readonly float radius, height, skin, step, slope;
        public readonly Vector3 rootToFeet;
        internal readonly CharacterController controller;

        // note: Default, nonfinite or unsupported measurements must never reach native physics queries.
        public bool IsValid => Finite(radius) && Finite(height) && Finite(skin) && Finite(step) &&
            Finite(slope) && Finite(rootToFeet.x) && Finite(rootToFeet.y) && Finite(rootToFeet.z) &&
            radius > .001f && height >= radius * 2f && skin >= 0f && skin < radius &&
            step >= 0f && step <= height && slope >= 0f && slope <= 90f;

        private Shape(CharacterController controller)
        {
            // note: Keep the measured controller as the collision-policy owner; a transform alone cannot resolve pair exclusions.
            this.controller = controller;
            // note: Match world-space controller geometry, including nonuniform scale and offset centers.
            Vector3 scale = controller.transform.lossyScale;
            radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2f);
            skin = Mathf.Min(controller.skinWidth * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), radius * .5f);
            step = controller.stepOffset * Mathf.Abs(scale.y);
            slope = controller.slopeLimit;
            rootToFeet = controller.transform.TransformPoint(controller.center) - controller.transform.position - Vector3.up * (height * .5f);
        }

        public static bool TryCapture(CharacterController controller, out Shape shape)
        {
            shape = default;
            // note: A tilted capsule needs a different probe; do not silently certify it as upright.
            if (controller == null || Vector3.Dot(controller.transform.up, Vector3.up) < .999f)
                return false;
            shape = new Shape(controller);
            return shape.IsValid;
        }

        public void Capsule(Vector3 feet, out Vector3 lower, out Vector3 upper, out float queryRadius)
        {
            // note: Skin is the controller's contact tolerance, not an arbitrary smaller test player.
            queryRadius = Mathf.Max(.001f, radius - skin);
            lower = feet + Vector3.up * radius;
            upper = feet + Vector3.up * (height - radius);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct Result
    {
        public readonly Issue issues;
        public readonly Vector3 feet;
        public readonly bool hasSupport;
        // note: Ephemeral physics evidence belongs to this measurement, never to persisted world identity or automatic repair authority.
        public readonly Collider supportCollider, overlapCollider, sweepCollider;
        public readonly Vector3 sweepContactPoint;
        public Result(Issue issues, Vector3 feet, bool hasSupport, Collider supportCollider = null,
            Collider overlapCollider = null, Collider sweepCollider = null, Vector3 sweepContactPoint = default)
        {
            this.issues = issues; this.feet = feet; this.hasSupport = hasSupport;
            this.supportCollider = supportCollider; this.overlapCollider = overlapCollider;
            this.sweepCollider = sweepCollider; this.sweepContactPoint = sweepContactPoint;
        }
    }

    private readonly PhysicsScene scene;
    private readonly Shape shape;
    private readonly Transform playerRoot;
    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[64];

    public YQRouteTraversalProbe(PhysicsScene scene, Shape shape, Transform playerRoot)
    {
        // note: Fail explicitly at setup instead of querying with a zero-sized fallback player.
        if (!scene.IsValid() || !shape.IsValid || shape.controller == null)
            throw new ArgumentException("Traversal measurement requires a valid physics scene and captured player shape.");
        this.scene = scene; this.shape = shape; this.playerRoot = playerRoot;
    }

    public Result Measure(Vector3 expectedFeet, bool hasPrevious, Vector3 previousFeet)
    {
        // note: A destroyed player invalidates this measurement rather than silently reverting to all-collider queries.
        if (shape.controller == null || !Finite(expectedFeet) || (hasPrevious && !Finite(previousFeet)))
            return new Result(Issue.InvalidInput, expectedFeet, false);
        // note: Search near the expected walking elevation, never down from the sky onto a roof.
        float reach = Mathf.Max(.1f, shape.step + shape.skin + .05f);
        Issue issues = Issue.None;
        if (!TrySupport(expectedFeet, reach, out RaycastHit support, ref issues))
            return new Result(issues | Issue.MissingSupport, expectedFeet, false);
        // note: The capsule reaches a step before its centre ray does; acquire a reachable tread under the capsule rim before testing its riser as an obstruction.
        float treadRadius = Mathf.Max(.001f, shape.radius - shape.skin);
        for (int sample = 0; sample < 8; sample++)
        {
            float angle = sample * Mathf.PI * .25f;
            Vector3 rim = expectedFeet + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * treadRadius;
            if (TrySupport(rim, reach, out var tread, ref issues) && tread.point.y > support.point.y &&
                tread.point.y - expectedFeet.y <= shape.step && Vector3.Angle(tread.normal, Vector3.up) <= shape.slope)
                support = tread;
        }
        Vector3 feet = new Vector3(expectedFeet.x, support.point.y, expectedFeet.z);
        if (Vector3.Angle(support.normal, Vector3.up) > shape.slope)
            issues |= Issue.SteepSupport;

        // note: A center hit is insufficient on ledges and narrow beams. Four inset footprint rays add bounded support evidence.
        float footprintRadius = shape.radius * .8f;
        for (int foot = 0; foot < 4; foot++)
        {
            Vector3 offset = foot < 2 ? Vector3.right * (foot == 0 ? footprintRadius : -footprintRadius) :
                Vector3.forward * (foot == 2 ? footprintRadius : -footprintRadius);
            if (!TrySupport(feet + offset, reach, out RaycastHit contact, ref issues))
                issues |= Issue.UnsupportedFootprint;
            else if (Mathf.Abs(contact.point.y - feet.y) > shape.step + shape.skin ||
                     Vector3.Angle(contact.normal, Vector3.up) > shape.slope)
                issues |= Issue.UnsupportedFootprint;
        }

        shape.Capsule(feet, out Vector3 lower, out Vector3 upper, out float radius);
        Collider overlapEvidence = null;
        Collider sweepEvidence = null;
        Vector3 sweepPoint = default;
        int count = scene.OverlapCapsule(lower, upper, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) issues |= Issue.QuerySaturated;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = overlaps[i];
            if (Ignore(obstacle)) continue;
            // note: A floor and a wall may share one mesh. Never exempt that whole collider by name/type.
            issues |= obstacle == support.collider ? Issue.AmbiguousSupportOverlap : Issue.Occupied;
            // note: Keep one representative overlap, preferring a separate obstacle over ambiguous contact with the support itself.
            if (overlapEvidence == null || (overlapEvidence == support.collider && obstacle != support.collider))
                overlapEvidence = obstacle;
        }

        if (hasPrevious)
        {
            shape.Capsule(previousFeet, out lower, out upper, out radius);
            float stepRise = feet.y - previousFeet.y;
            if (stepRise > .001f && stepRise <= shape.step)
            {
                // note: A supported low step requires a clear upward sweep before advancing at landing height; a diagonal cast alone falsely hits its vertical riser.
                int liftCount = scene.CapsuleCast(lower, upper, radius, Vector3.up, hits,
                    stepRise, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                bool liftClear = liftCount < hits.Length;
                if (!liftClear) issues |= Issue.QuerySaturated;
                for (int i = 0; i < liftCount; i++)
                    if (!Ignore(hits[i].collider)) { liftClear = false; break; }
                if (liftClear)
                {
                    previousFeet += Vector3.up * stepRise;
                    shape.Capsule(previousFeet, out lower, out upper, out radius);
                }
            }
            Vector3 travel = feet - previousFeet;
            if (travel.sqrMagnitude > .000001f)
            {
                // note: Continuous casts catch thin walls between samples; initial overlaps are checked separately above.
                count = scene.CapsuleCast(lower, upper, radius, travel.normalized, hits,
                    travel.magnitude, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) issues |= Issue.QuerySaturated;
                float nearestObserved = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (Ignore(hits[i].collider)) continue;
                    issues |= Issue.SweptObstacle;
                    // note: Non-alloc hit order is unspecified. Report the nearest returned contact, not whichever hit happened to be first.
                    if (hits[i].distance < nearestObserved)
                    {
                        nearestObserved = hits[i].distance;
                        sweepEvidence = hits[i].collider;
                        sweepPoint = hits[i].point;
                    }
                }
            }
        }
        return new Result(issues, feet, true, support.collider, overlapEvidence, sweepEvidence, sweepPoint);
    }

    private bool TrySupport(Vector3 expected, float reach, out RaycastHit support, ref Issue issues)
    {
        support = default;
        int count = scene.Raycast(expected + Vector3.up * reach, Vector3.down, hits,
            reach * 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        // note: Non-alloc queries may omit the nearest support when full. Do not propagate an arbitrary truncated hit as ground.
        if (count == hits.Length)
        {
            issues |= Issue.QuerySaturated;
            return false;
        }
        float highest = float.NegativeInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (Ignore(hits[i].collider)) continue;
            // note: Choose the upper reachable surface, not buried terrain closest to the last elevation; step and slope checks still constrain traversal.
            float candidate = hits[i].point.y;
            if (candidate > expected.y + shape.step + shape.skin) continue;
            if (candidate > highest)
            { highest = candidate; support = hits[i]; found = true; }
        }
        return found;
    }

    private static bool Finite(Vector3 point) =>
        !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
        !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
        !float.IsNaN(point.z) && !float.IsInfinity(point.z);

    private bool Ignore(Collider collider)
    {
        if (collider == null || collider == shape.controller ||
            (playerRoot != null && collider.transform.IsChildOf(playerRoot)))
            return true;
        // note: Match the motor's standing-clearance policy for both obstacles and supports. Ignored floors cannot support the player.
        return Physics.GetIgnoreLayerCollision(shape.controller.gameObject.layer, collider.gameObject.layer) ||
            Physics.GetIgnoreCollision(shape.controller, collider);
    }
}
