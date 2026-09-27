using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Offline collision evidence is deliberately separate from asset approval and runtime generation.
public sealed class YQCellPassageReviewV2 : IDisposable
{
    public const int SampleCount = 9;
    private const float Height = 1.8f;
    // note: Match the authoritative bootstrap's standing player; the former 0.30m probe falsely accepted narrow interiors.
    private const float Radius = 0.38f;
    private const float FootprintRadius = Radius * 0.8f;
    private const float Step = 0.3f;
    private readonly Scene scene;
    private readonly PhysicsScene physics;
    private readonly Transform root;
    private readonly Collider[] overlaps = new Collider[32];
    private readonly List<Collider> removedDoor = new List<Collider>();
    private int unsupported;

    public sealed class Result
    {
        public int supportedSamples, blockedSamples, excessiveSteps, unsupportedColliders;
        public string failure = "";
        public readonly List<string> blockers = new List<string>();
        public readonly List<string> supports = new List<string>();
        public bool Clear => failure.Length == 0 && supportedSamples == SampleCount &&
            blockedSamples == 0 && excessiveSteps == 0 && unsupportedColliders == 0;

        public string Describe()
        {
            return (Clear ? "COLLISION PROBE CLEAR (not approved)" : "NEEDS REVIEW") +
                "; supported=" + supportedSamples + "/" + SampleCount +
                "; obstructed=" + blockedSamples + "; excessive steps=" + excessiveSteps +
                "; uncopied colliders=" + unsupportedColliders +
                (failure.Length == 0 ? "" : "; " + failure) +
                (blockers.Count == 0 ? "" : "; blockers: " + string.Join(", ", blockers)) +
                (supports.Count == 0 ? "" : "; support: " + string.Join(", ", supports));
        }
    }

    public YQCellPassageReviewV2(GameObject source)
    {
        if (source == null || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("An authored cell and Edit Mode are required.");
        // note: Copy transforms and collision shapes only. No imported scripts, managers, renderers or player objects execute.
        scene = EditorSceneManager.NewPreviewScene();
        physics = scene.GetPhysicsScene();
        try
        {
            // note: Fail closed if this Unity version cannot isolate preview collision from the user's open scenes.
            if (!physics.IsValid() || physics == Physics.defaultPhysicsScene)
                throw new InvalidOperationException("Doorway review requires an isolated editor-preview physics scene.");
            root = CopyNode(source.transform, null);
            Physics.SyncTransforms();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private Transform CopyNode(Transform source, Transform parent)
    {
        GameObject copy = new GameObject(source.name);
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        copy.SetActive(source.gameObject.activeSelf);
        foreach (Collider original in source.GetComponents<Collider>())
        {
            if (!original.enabled || original.isTrigger)
                continue;
            // note: Preserve source geometry, including thin-axis door boxes; never substitute bounds boxes for missing mesh collision.
            if (original is BoxCollider box)
            {
                BoxCollider target = copy.AddComponent<BoxCollider>();
                target.center = box.center;
                target.size = box.size;
            }
            else if (original is MeshCollider mesh && mesh.sharedMesh != null)
            {
                MeshCollider target = copy.AddComponent<MeshCollider>();
                target.cookingOptions = mesh.cookingOptions;
                target.convex = mesh.convex;
                target.sharedMesh = mesh.sharedMesh;
            }
            else if (original is SphereCollider sphere)
            {
                SphereCollider target = copy.AddComponent<SphereCollider>();
                target.center = sphere.center;
                target.radius = sphere.radius;
            }
            else if (original is CapsuleCollider capsule)
            {
                CapsuleCollider target = copy.AddComponent<CapsuleCollider>();
                target.center = capsule.center;
                target.radius = capsule.radius;
                target.height = capsule.height;
                target.direction = capsule.direction;
            }
            else if (original is TerrainCollider terrain && terrain.terrainData != null)
            {
                // note: Explicit test terrain participates in collision checks; no invisible fallback plane is supplied when terrain is absent.
                copy.AddComponent<TerrainCollider>().terrainData = terrain.terrainData;
            }
            else
                unsupported++;
        }
        foreach (Transform child in source)
            CopyNode(child, copy.transform);
        return copy.transform;
    }

    public sealed class SwingResult
    {
        public int samples, blockedSamples, exactPoseOverlaps, closedPoseOverlaps, unsupportedColliders;
        public string failure = "";
        public readonly List<string> blockers = new List<string>();
        public bool Clear => failure.Length == 0 && samples > 0 && blockedSamples == 0 && unsupportedColliders == 0;
        public string Describe() => (Clear ? "SWING CLEAR (collision evidence only)" : "NEEDS REPAIR") +
            "; samples=" + samples + "; blocked=" + blockedSamples +
            "; exact-pose overlaps=" + exactPoseOverlaps + "; closed overlaps=" + closedPoseOverlaps +
            "; uncopied colliders=" + unsupportedColliders +
            (failure.Length == 0 ? "" : "; " + failure) +
            (blockers.Count == 0 ? "" : "; blockers: " + string.Join(", ", blockers));
    }

    public SwingResult MeasureSwing(string doorPath, float openDegrees)
    {
        SwingResult result = MeasureSwingWithStep(doorPath, openDegrees, 2f);
        // note: Refine envelope-only hinge warnings without relaxing the geometry test. The second pass is capped at 1,361 poses for a 170-degree swing.
        return result.failure.Length == 0 && result.blockedSamples > 0 && result.exactPoseOverlaps == 0
            ? MeasureSwingWithStep(doorPath, openDegrees, 0.125f)
            : result;
    }

    private SwingResult MeasureSwingWithStep(string doorPath, float openDegrees, float stepDegrees)
    {
        SwingResult result = new SwingResult { unsupportedColliders = unsupported };
        if (float.IsNaN(openDegrees) || float.IsInfinity(openDegrees) ||
            Mathf.Abs(openDegrees) < 15f || Mathf.Abs(openDegrees) > 170f ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(root, doorPath, out Transform door) ||
            door == root || !door.gameObject.activeInHierarchy)
        {
            result.failure = "Swing requires an active unique leaf and a finite 15–170 degree opening.";
            return result;
        }
        BoxCollider leaf = door.GetComponent<BoxCollider>();
        if (leaf == null || !leaf.enabled || Vector3.Dot(door.up, Vector3.up) < 0.999f)
        {
            result.failure = "Swing requires an upright enabled authored box leaf.";
            return result;
        }
        Quaternion closed = door.localRotation;
        Collider[] ownColliders = door.GetComponentsInChildren<Collider>();
        List<Collider> enabledColliders = new List<Collider>();
        foreach (Collider collider in ownColliders)
            if (collider.enabled)
            {
                enabledColliders.Add(collider);
                collider.enabled = false;
            }
        try
        {
            Physics.SyncTransforms();
            int intervals = Mathf.CeilToInt(Mathf.Abs(openDegrees) / stepDegrees);
            for (int index = 0; index <= intervals; index++)
            {
                // note: Use the runtime's exact local hinge multiplication; only isolated collision copies are moved.
                float angle = index == 0 ? 0f : openDegrees * (index - 0.5f) / intervals;
                door.localRotation = closed * Quaternion.Euler(0, angle, 0);
                Vector3 x = door.TransformVector(Vector3.right), y = door.TransformVector(Vector3.up),
                    z = door.TransformVector(Vector3.forward);
                if (x.magnitude < 0.0001f || y.magnitude < 0.0001f || z.magnitude < 0.0001f ||
                    Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > 0.001f ||
                    Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > 0.001f ||
                    Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > 0.001f)
                {
                    result.failure = "Sheared or degenerate leaf transforms need authored hinge repair.";
                    return result;
                }
                Vector3 half = Vector3.Scale(leaf.size * 0.5f, new Vector3(x.magnitude, y.magnitude, z.magnitude));
                // note: Cover each angular interval conservatively, not just sampled poses. A 5mm vertical inset tolerates floor contact without ignoring jamb/wall penetration.
                float reach = door.TransformVector(leaf.center).magnitude + half.magnitude;
                float guard = index == 0 ? 0f : 2f * reach * Mathf.Sin(Mathf.Abs(openDegrees) * Mathf.Deg2Rad / intervals / 4f);
                Vector3 exactHalf = half;
                exactHalf.y = Mathf.Max(0.001f, exactHalf.y - 0.005f);
                half.x += guard;
                half.z += guard;
                half.y = Mathf.Max(0.001f, half.y - 0.005f);
                int hits = physics.OverlapBox(door.TransformPoint(leaf.center), half, overlaps,
                    Quaternion.LookRotation(z.normalized, y.normalized), ~0, QueryTriggerInteraction.Ignore);
                result.samples++;
                if (hits == 0) continue;
                result.blockedSamples++;
                if (hits >= overlaps.Length) result.failure = "Swing overlap buffer saturated; clearance is unproven.";
                for (int hit = 0; hit < hits; hit++)
                    if (overlaps[hit] != null) AddEvidence(result.blockers, overlaps[hit].transform);
                // note: Distinguish confirmed collision-shape overlap from a conservative between-pose envelope warning; neither justifies deleting a wall.
                int exactHits = physics.OverlapBox(door.TransformPoint(leaf.center), exactHalf, overlaps,
                    Quaternion.LookRotation(z.normalized, y.normalized), ~0, QueryTriggerInteraction.Ignore);
                if (exactHits > 0) result.exactPoseOverlaps++;
                if (index == 0) result.closedPoseOverlaps = exactHits;
            }
            return result;
        }
        finally
        {
            // note: Repeated direction checks begin from the identical source pose and collider state, including failure paths.
            door.localRotation = closed;
            foreach (Collider collider in enabledColliders) collider.enabled = true;
            Physics.SyncTransforms();
        }
    }

    public Result Measure(string doorPath, Quaternion? closedLocalRotation = null)
    {
        Result result = new Result { unsupportedColliders = unsupported };
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, doorPath, out Transform door) ||
            door == root || !door.gameObject.activeInHierarchy)
        {
            result.failure = "Door path missing, ambiguous or inactive.";
            return result;
        }
        BoxCollider leaf = door.GetComponent<BoxCollider>();
        if (leaf == null || Vector3.Dot(door.up, Vector3.up) < 0.99f)
        {
            result.failure = "Requires an upright door with an authored box collider.";
            return result;
        }

        // note: A source leaf can be posed open. An explicit frame-aligned closed pose defines the portal, never its current swing direction.
        Quaternion sourceRotation = door.localRotation;
        if (closedLocalRotation.HasValue)
            door.localRotation = closedLocalRotation.Value;

        // note: Existing door gameplay disables the opened leaf's collision; this measures that passage, not an unverified visual swing arc.
        removedDoor.Clear();
        foreach (Collider collider in door.GetComponentsInChildren<Collider>())
            if (collider.enabled)
            {
                collider.enabled = false;
                removedDoor.Add(collider);
            }
        try
        {
            Physics.SyncTransforms();
            Vector3 normal = door.TransformDirection(leaf.size.x < leaf.size.z ? Vector3.right : Vector3.forward);
            Vector3 lateral = Vector3.Cross(Vector3.up, normal).normalized;
            Vector3 bottom = door.TransformPoint(leaf.center - Vector3.up * leaf.size.y * 0.5f);
            float previousHeight = 0;
            bool previousSupported = false;
            for (int index = 0; index < SampleCount; index++)
            {
                Vector3 sample = bottom + normal * Mathf.Lerp(-1.2f, 1.2f, index / (float)(SampleCount - 1));
                bool supported = true;
                float highest = float.NegativeInfinity;
                float lowest = float.PositiveInfinity;
                // note: Five footprint probes catch unsupported ledges and narrow beams that a single center ray would accept.
                for (int foot = 0; foot < 5; foot++)
                {
                    Vector3 offset = foot == 0 ? Vector3.zero :
                        foot <= 2 ? lateral * (foot == 1 ? FootprintRadius : -FootprintRadius) : normal * (foot == 3 ? FootprintRadius : -FootprintRadius);
                    if (!physics.Raycast(sample + offset + Vector3.up * 0.55f, Vector3.down,
                            out RaycastHit hit, 1.1f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7071f)
                    {
                        supported = false;
                        continue;
                    }
                    highest = Mathf.Max(highest, hit.point.y);
                    lowest = Mathf.Min(lowest, hit.point.y);
                    AddEvidence(result.supports, hit.collider.transform);
                }
                if (supported)
                {
                    result.supportedSamples++;
                    if (highest - lowest > Step || (previousSupported && Mathf.Abs(highest - previousHeight) > Step))
                        result.excessiveSteps++;
                    previousHeight = highest;
                }
                previousSupported = supported;
                sample.y = supported ? highest : bottom.y;
                Vector3 feet = sample + Vector3.up * 0.03f;
                int count = physics.OverlapCapsule(feet + Vector3.up * Radius,
                    feet + Vector3.up * (Height - Radius), Radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                if (count > 0)
                {
                    result.blockedSamples++;
                    for (int hit = 0; hit < count; hit++)
                        AddEvidence(result.blockers, overlaps[hit].transform);
                }
            }
        }
        finally
        {
            door.localRotation = sourceRotation;
            foreach (Collider collider in removedDoor)
                collider.enabled = true;
            Physics.SyncTransforms();
        }
        return result;
    }

    private void AddEvidence(List<string> evidence, Transform target)
    {
        string path = AnimationUtility.CalculateTransformPath(target, root);
        if (evidence.Count < 6 && !evidence.Contains(path))
            evidence.Add(path);
    }

    public string CheckObjectPenetration(string objectPath)
    {
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, objectPath, out Transform item) ||
            item == root || !item.gameObject.activeInHierarchy)
            return "Missing or inactive object for collision-fit review: " + objectPath;
        Collider[] own = item.GetComponentsInChildren<Collider>();
        int checkedShapes = 0;
        foreach (Collider shape in own)
        {
            if (!shape.enabled || shape.isTrigger) continue;
            // note: Imported furnishings use box, sphere or capsule collision. Reject unsupported shape tests rather than claiming approximate bounds prove a fit.
            if (!(shape is BoxCollider) && !(shape is SphereCollider) && !(shape is CapsuleCollider))
                return "Unsupported furnishing collision shape: " + objectPath;
            checkedShapes++;
            Bounds bounds = shape.bounds;
            int count = physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * 0.002f,
                overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return "Furnishing overlap buffer saturated: " + objectPath;
            for (int index = 0; index < count; index++)
            {
                Collider other = overlaps[index];
                if (other == null || other.transform.IsChildOf(item)) continue;
                // note: The physics narrow phase distinguishes actual wall/furniture penetration from broad bounds overlap; 15mm permits authored foot/floor contact.
                if (Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.015f)
                    return objectPath + " penetrates " + AnimationUtility.CalculateTransformPath(other.transform, root) +
                        " by " + depth.ToString("F4") + "m.";
            }
        }
        return checkedShapes > 0 ? string.Empty : "No solid furnishing collision: " + objectPath;
    }

    public sealed class FloorRegionResult
    {
        public int samples, supported, walkable, reachable, unsupportedColliders;
        public bool entryClear;
        public string failure = "";
        public readonly List<Vector3> unreachableLocalPoints = new List<Vector3>();
        // note: Furniture may occupy floor space. Every free sampled area must still connect to the declared entry.
        public bool Ready => failure.Length == 0 && samples > 0 && supported == samples &&
            entryClear && walkable > 0 && reachable == walkable && unsupportedColliders == 0;
        public string Describe() => "supported=" + supported + "/" + samples +
            "; standing-clear=" + walkable + "; reachable=" + reachable + "/" + walkable +
            "; entry-clear=" + entryClear + "; uncopied colliders=" + unsupportedColliders +
            (failure.Length == 0 ? "" : "; " + failure) +
            (unreachableLocalPoints.Count == 0 ? "" : "; disconnected examples=" + string.Join(", ", unreachableLocalPoints));
    }

    private struct FloorPoint
    {
        public Vector3 feet;
        public bool supported, walkable;
    }

    public FloorRegionResult MeasureFloorRegion(string anchorPath, Rect localXZ, float localFloorY,
        Vector3 localEntry, float spacing = 0.45f, string furnishingsPath = null)
    {
        FloorRegionResult result = new FloorRegionResult { unsupportedColliders = unsupported };
        // note: Explicit room/approach extents are authoring evidence, not inferred from mesh names or a doorway's existence.
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, anchorPath, out Transform anchor) ||
            !anchor.gameObject.activeInHierarchy || Vector3.Dot(anchor.up, Vector3.up) < 0.99f ||
            !Finite(localXZ.x) || !Finite(localXZ.y) || !Finite(localXZ.width) || !Finite(localXZ.height) ||
            !Finite(localFloorY) || !Finite(localEntry.x) || !Finite(localEntry.y) || !Finite(localEntry.z) ||
            !Finite(spacing) || spacing < 0.2f || spacing > 2f || localXZ.width <= 0 || localXZ.height <= 0 ||
            localXZ.width > 64 || localXZ.height > 64)
        {
            result.failure = "Invalid room anchor, extent or probe spacing.";
            return result;
        }
        int columns = Mathf.CeilToInt(localXZ.width / spacing) + 1;
        int rows = Mathf.CeilToInt(localXZ.height / spacing) + 1;
        if (columns * rows > 1024)
        {
            result.failure = "Room review exceeds its 1024-point work limit.";
            return result;
        }
        result.samples = columns * rows;
        FloorPoint[] points = new FloorPoint[result.samples];
        Vector3 along = anchor.right;
        Vector3 across = anchor.forward;
        List<Collider> furniture = new List<Collider>();
        if (!string.IsNullOrEmpty(furnishingsPath))
        {
            if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, furnishingsPath, out Transform furnishings) || furnishings == root)
            {
                result.failure = "Furnishing support exclusion requires an explicit child hierarchy.";
                return result;
            }
            foreach (Collider collider in furnishings.GetComponentsInChildren<Collider>())
                if (collider.enabled) furniture.Add(collider);
        }
        FloorPoint entry;
        try
        {
            // note: Measure real structural support beneath furniture; tabletops and chests cannot masquerade as floor.
            foreach (Collider collider in furniture) collider.enabled = false;
            if (furniture.Count > 0) Physics.SyncTransforms();
            for (int z = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                Vector3 local = new Vector3(Mathf.Lerp(localXZ.xMin, localXZ.xMax, x / (float)(columns - 1)),
                    localFloorY, Mathf.Lerp(localXZ.yMin, localXZ.yMax, z / (float)(rows - 1)));
                FloorPoint point = ProbeFloorPoint(anchor.TransformPoint(local), along, across);
                points[z * columns + x] = point;
                if (point.supported) result.supported++;
            }
            entry = ProbeFloorPoint(anchor.TransformPoint(localEntry), along, across);
        }
        finally
        {
            foreach (Collider collider in furniture) collider.enabled = true;
            if (furniture.Count > 0) Physics.SyncTransforms();
        }
        // note: Furniture remains solid for standing clearance and every connectivity sweep, including the entry point.
        for (int index = 0; index < points.Length; index++)
        {
            if (points[index].walkable && !StandingClear(points[index].feet)) points[index].walkable = false;
            if (points[index].walkable) result.walkable++;
        }
        entry.walkable &= StandingClear(entry.feet);
        result.entryClear = entry.walkable;
        if (!entry.walkable) return result;
        int seed = -1;
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < points.Length; index++)
        {
            float distance = (points[index].feet - entry.feet).sqrMagnitude;
            if (points[index].walkable && distance < nearest && distance <= spacing * spacing * 2f &&
                CanTraverse(entry.feet, points[index].feet))
            {
                nearest = distance;
                seed = index;
            }
        }
        if (seed < 0) return result;

        // note: Four-neighbour flood fill cannot jump diagonally around a corner. Capsule sweeps reject thin walls between grid points.
        bool[] visited = new bool[points.Length];
        Queue<int> pending = new Queue<int>();
        visited[seed] = true;
        pending.Enqueue(seed);
        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            result.reachable++;
            int x = index % columns;
            int z = index / columns;
            for (int direction = 0; direction < 4; direction++)
            {
                int nextX = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                int nextZ = z + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                if (nextX < 0 || nextX >= columns || nextZ < 0 || nextZ >= rows) continue;
                int next = nextZ * columns + nextX;
                if (visited[next] || !points[next].walkable || !CanTraverse(points[index].feet, points[next].feet)) continue;
                visited[next] = true;
                pending.Enqueue(next);
            }
        }
        // note: Bounded coordinate evidence identifies furniture pockets without requiring a rendered desktop inspection.
        for (int index = 0; index < points.Length && result.unreachableLocalPoints.Count < 8; index++)
            if (points[index].walkable && !visited[index])
                result.unreachableLocalPoints.Add(anchor.InverseTransformPoint(points[index].feet));
        return result;
    }

    private bool StandingClear(Vector3 feet) => physics.OverlapCapsule(
        feet + Vector3.up * Radius, feet + Vector3.up * (Height - Radius),
        Radius, overlaps, ~0, QueryTriggerInteraction.Ignore) == 0;

    private FloorPoint ProbeFloorPoint(Vector3 sample, Vector3 along, Vector3 across)
    {
        FloorPoint result = new FloorPoint { supported = true, feet = sample };
        float highest = float.NegativeInfinity, lowest = float.PositiveInfinity;
        for (int foot = 0; foot < 5; foot++)
        {
            Vector3 offset = foot == 0 ? Vector3.zero : foot <= 2 ?
                along * (foot == 1 ? FootprintRadius : -FootprintRadius) : across * (foot == 3 ? FootprintRadius : -FootprintRadius);
            if (!physics.Raycast(sample + offset + Vector3.up * 0.55f, Vector3.down,
                    out RaycastHit hit, 1.1f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7071f)
            {
                result.supported = false;
                continue;
            }
            highest = Mathf.Max(highest, hit.point.y);
            lowest = Mathf.Min(lowest, hit.point.y);
        }
        if (!result.supported) return result;
        result.feet.y = highest + 0.03f;
        result.walkable = highest - lowest <= Step && physics.OverlapCapsule(
            result.feet + Vector3.up * Radius, result.feet + Vector3.up * (Height - Radius),
            Radius, overlaps, ~0, QueryTriggerInteraction.Ignore) == 0;
        return result;
    }

    private bool CanTraverse(Vector3 from, Vector3 to)
    {
        if (Mathf.Abs(to.y - from.y) > Step) return false;
        Vector3 movement = to - from;
        float distance = movement.magnitude;
        if (distance < 0.001f) return true;
        // note: This conservative sweep is evidence of connected standing space, not a replacement for the player's step/climb controller.
        return !physics.CapsuleCast(from + Vector3.up * Radius, from + Vector3.up * (Height - Radius),
            Radius, movement / distance, out _, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public sealed class SteppedRouteResult
    {
        public int samples, supported, clear, connected;
        public string failure = "";
        public bool Ready => samples > 0 && supported == samples && clear == samples &&
            connected == samples - 3 && failure.Length == 0;
        public string Describe() => "support=" + supported + "/" + samples + "; body-clear=" + clear +
            "/" + samples + "; bidirectional links=" + connected + "/" + Mathf.Max(0, samples - 3) +
            (failure.Length == 0 ? "" : "; " + failure);
    }

    public SteppedRouteResult MeasureSteppedRoute(IReadOnlyList<Vector3> worldPoints, Vector3 across, float width)
    {
        SteppedRouteResult result = new SteppedRouteResult();
        if (worldPoints == null || worldPoints.Count < 2 || worldPoints.Count > 32 ||
            !Finite(width) || width < Radius * 2f || width > 3 || across.sqrMagnitude < 0.99f ||
            Mathf.Abs(across.y) > 0.01f || unsupported != 0)
        {
            result.failure = "Invalid route, insufficient width or unsupported source collision.";
            return result;
        }
        across.Normalize();
        List<Vector3> samples = new List<Vector3>();
        for (int segment = 1; segment < worldPoints.Count; segment++)
        {
            Vector3 from = worldPoints[segment - 1], to = worldPoints[segment];
            if (!Finite(from.x) || !Finite(from.y) || !Finite(from.z) || !Finite(to.x) || !Finite(to.y) || !Finite(to.z))
            {
                result.failure = "Nonfinite route point.";
                return result;
            }
            float distance = Vector3.Distance(from, to);
            if (distance > 16f) { result.failure = "Route segment exceeds review budget."; return result; }
            int divisions = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
            if (samples.Count + divisions + 1 > 512) { result.failure = "Route exceeds 512 stations."; return result; }
            if (segment == 1) samples.Add(from);
            for (int step = 1; step <= divisions; step++) samples.Add(Vector3.Lerp(from, to, step / (float)divisions));
        }
        result.samples = samples.Count * 3;
        // note: Test centre and both usable edge lanes. Step-specific stance/sweeps are separate from the stricter flat-room checker.
        for (int lane = -1; lane <= 1; lane++)
        {
            Vector3 previous = default;
            float previousSupport = 0;
            bool previousClear = false;
            for (int index = 0; index < samples.Count; index++)
            {
                Vector3 point = samples[index] + across * (lane * (width * 0.5f - Radius));
                bool supported = true;
                float highest = float.NegativeInfinity, lowest = float.PositiveInfinity;
                // note: Longitudinal support advances in 8cm increments; lateral footprint probes reject a narrow ledge masquerading as a full-width tread.
                for (int foot = -1; foot <= 1; foot++)
                {
                    bool contact = false;
                    float treadHeight = float.NegativeInfinity;
                    // note: A capsule can bridge the imported treads' 2cm seams. A 3cm toe/heel neighbourhood handles those seams without inventing support for a missing tread.
                    for (int toe = -1; toe <= 1; toe++)
                    {
                        Vector3 offset = across * (foot * FootprintRadius) + Vector3.Cross(across, Vector3.up) * (toe * 0.03f);
                        if (!physics.Raycast(point + offset + Vector3.up * 0.55f, Vector3.down,
                                out RaycastHit hit, 1.1f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7071f) continue;
                        contact = true;
                        treadHeight = Mathf.Max(treadHeight, hit.point.y);
                    }
                    if (!contact) { supported = false; continue; }
                    highest = Mathf.Max(highest, treadHeight);
                    lowest = Mathf.Min(lowest, treadHeight);
                }
                if (!supported || highest - lowest > Step)
                {
                    if (result.failure.Length == 0) result.failure = "Missing/uneven tread support near " + point.ToString("F3");
                    previousClear = false;
                    continue;
                }
                result.supported++;
                bool clear = false;
                Vector3 stance = point;
                // note: A capsule bridges a riser rather than occupying the exact centre of a narrow tread. Keep that stance adjustment bounded to 30cm.
                for (int lift = 0; lift <= 10; lift++)
                {
                    stance.y = highest + 0.03f + lift * 0.03f;
                    if (physics.OverlapCapsule(stance + Vector3.up * Radius, stance + Vector3.up * (Height - Radius),
                            Radius, overlaps, ~0, QueryTriggerInteraction.Ignore) != 0) continue;
                    clear = true;
                    break;
                }
                if (clear)
                {
                    result.clear++;
                    if (previousClear)
                    {
                        if (Mathf.Abs(highest - previousSupport) <= Step &&
                            CanStepBetween(previous, stance) && CanStepBetween(stance, previous)) result.connected++;
                        else if (result.failure.Length == 0) result.failure = "Step sweep/rise failed near " + point.ToString("F3");
                    }
                }
                else if (result.failure.Length == 0) result.failure = "Body blocked near " + point.ToString("F3") +
                    (overlaps[0] != null ? " by " + overlaps[0].name : "");
                previous = stance;
                previousSupport = highest;
                previousClear = clear;
            }
        }
        return result;
    }

    private bool CanStepBetween(Vector3 from, Vector3 to)
    {
        if (SweepClear(from, to)) return true;
        // note: Swept lift/travel/lower checks prevent a staircase test from accepting a wall or low ceiling just because its endpoints are clear.
        float raisedY = Mathf.Max(from.y, to.y) + Step;
        Vector3 raisedFrom = new Vector3(from.x, raisedY, from.z);
        Vector3 raisedTo = new Vector3(to.x, raisedY, to.z);
        return SweepClear(from, raisedFrom) && SweepClear(raisedFrom, raisedTo) && SweepClear(raisedTo, to);
    }

    private bool SweepClear(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f) return true;
        return !physics.CapsuleCast(from + Vector3.up * Radius, from + Vector3.up * (Height - Radius),
            Radius, delta.normalized, out _, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
    }

    public static string TestSteppedRouteEvidence()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = new GameObject("StairFixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            // note: Explicit synthetic slabs exercise the route checker independently of the imported staircase and candidate recipes.
            List<Vector3> route = new List<Vector3>();
            for (int step = 0; step < 4; step++)
            {
                FixtureBox(root.transform, "Step" + step, new Vector3(step * 0.6f, step * 0.15f - 0.1f, 0), new Vector3(0.61f, 0.2f, 1.8f));
                route.Add(new Vector3(step * 0.6f, step * 0.15f, 0));
            }
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(root))
                if (!review.MeasureSteppedRoute(route, Vector3.forward, 1.2f).Ready)
                    return "A supported staircase failed bidirectional capsule checks.";
            BoxCollider wall = FixtureBox(root.transform, "Wall", new Vector3(0.9f, 1.1f, 0), new Vector3(0.05f, 2, 2));
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(root))
                if (review.MeasureSteppedRoute(route, Vector3.forward, 1.2f).Ready)
                    return "A wall crossed the staircase without rejection.";
            UnityEngine.Object.DestroyImmediate(wall.gameObject);
            UnityEngine.Object.DestroyImmediate(root.transform.Find("Step1").gameObject);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(root))
                if (review.MeasureSteppedRoute(route, Vector3.forward, 1.2f).Ready)
                    return "Missing tread support passed.";
            return null;
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public void Dispose()
    {
        // note: Closing the temporary physics scene also destroys all copied shapes; the active user scene is not replaced or saved.
        if (scene.IsValid() && scene.isLoaded)
            EditorSceneManager.ClosePreviewScene(scene);
    }

    public static void AppendReport(YQReviewedSemanticSiteManifest manifest, StringBuilder report)
    {
        report.AppendLine("## Doorway collision and support probes");
        report.AppendLine();
        report.AppendLine("Offline 1.8m-high, 0.3m-radius review capsule; 0.3m step limit; nine stations across a 2.4m doorway approach. Not a player-controller or visual test.");
        report.AppendLine("Only authored collision is measured. Missing terrain support stays missing; no artificial ground is inserted. Opened door collision is excluded, matching the existing door provider.");
        report.AppendLine();
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone?.prefab == null || zone.cellContractsV2 == null)
                continue;
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(zone.prefab))
                foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                    foreach (YQCellDoorBindingV2 door in contract?.doorBindings ?? new List<YQCellDoorBindingV2>())
                        if (door != null)
                            report.AppendLine("- " + zone.stableId + " / `" + door.targetPath + "`: " + review.Measure(door.targetPath).Describe());
        }
        report.AppendLine();
        report.AppendLine("These measurements never set passageVerified or approve habitation. Swing direction, interior furnishing, rendered geometry and terrain integration still require review.");
        report.AppendLine();
    }

    public static string TestCollisionEvidence()
    {
        Scene fixtureScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject fixture = new GameObject("PassageFixture");
            SceneManager.MoveGameObjectToScene(fixture, fixtureScene);
            // note: Synthetic fixtures exercise evidence rejection without loading vendors, terrain or game managers.
            fixture.transform.SetPositionAndRotation(new Vector3(27, 8, -13), Quaternion.Euler(0, 37, 0));
            BoxCollider door = FixtureBox(fixture.transform, "Door", new Vector3(0, 1.1f, 0), new Vector3(0.06f, 2.2f, 1.3f));
            BoxCollider floor = FixtureBox(fixture.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(5, 0.2f, 5));
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
            {
                if (!review.Measure("Door").Clear || !review.Measure("Door").Clear)
                    return "A supported open passage failed or repeat measurement changed it.";
                if (review.Measure("Missing").Clear)
                    return "Missing source path passed.";
            }
            if (!door.enabled)
                return "Review changed the source door collider.";
            // note: A 0.70m opening admits the old 0.60m-diameter probe, but must reject the real 0.76m player.
            BoxCollider narrowA = FixtureBox(fixture.transform, "NarrowA", new Vector3(0, 1, -0.45f), new Vector3(5, 2, 0.2f));
            BoxCollider narrowB = FixtureBox(fixture.transform, "NarrowB", new Vector3(0, 1, 0.45f), new Vector3(5, 2, 0.2f));
            using (var review = new YQCellPassageReviewV2(fixture))
            {
                if (review.Measure("Door").blockedSamples == 0)
                    return "A 0.70m corridor incorrectly accepted the standing player.";
                Vector3[] path = { fixture.transform.TransformPoint(Vector3.left), fixture.transform.TransformPoint(Vector3.right) };
                if (review.MeasureSteppedRoute(path, fixture.transform.forward, 0.70f).failure.Length == 0)
                    return "A route narrower than the player was not rejected before sampling.";
            }
            UnityEngine.Object.DestroyImmediate(narrowA.gameObject);
            UnityEngine.Object.DestroyImmediate(narrowB.gameObject);
            // note: A narrow authored corridor must be measured along its portal even when its leaf was saved open.
            BoxCollider sideA = FixtureBox(fixture.transform, "CorridorA", new Vector3(0, 1, -0.8f), new Vector3(5, 2, 0.2f));
            BoxCollider sideB = FixtureBox(fixture.transform, "CorridorB", new Vector3(0, 1, 0.8f), new Vector3(5, 2, 0.2f));
            door.transform.localRotation = Quaternion.Euler(0, 100, 0);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
            {
                if (review.Measure("Door").Clear || !review.Measure("Door", Quaternion.identity).Clear || review.Measure("Door").Clear)
                    return "An open leaf changed portal direction or the review failed to restore its pose.";
            }
            if (Quaternion.Angle(door.transform.localRotation, Quaternion.Euler(0, 100, 0)) > 0.01f)
                return "Portal review modified the source pose.";
            door.transform.localRotation = Quaternion.identity;
            UnityEngine.Object.DestroyImmediate(sideA.gameObject);
            UnityEngine.Object.DestroyImmediate(sideB.gameObject);
            BoxCollider wall = FixtureBox(fixture.transform, "BlockedWall", new Vector3(0, 1, 0), new Vector3(0.2f, 2, 4));
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
                if (review.Measure("Door").blockedSamples == 0)
                    return "Wall across a doorway passed.";
            UnityEngine.Object.DestroyImmediate(wall.gameObject);
            UnityEngine.Object.DestroyImmediate(floor.gameObject);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
                if (review.Measure("Door").supportedSamples != 0 || review.Measure("Door").Clear)
                    return "An unsupported doorway passed.";
            return null;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(fixtureScene);
        }
    }

    public static string TestDoorSwingEvidence()
    {
        Scene fixtureScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject fixture = new GameObject("SwingFixture");
            SceneManager.MoveGameObjectToScene(fixture, fixtureScene);
            fixture.transform.SetPositionAndRotation(new Vector3(27, 8, -13), Quaternion.Euler(0, 37, 0));
            BoxCollider door = FixtureBox(fixture.transform, "Door", Vector3.zero, new Vector3(0.08f, 2, 1.4f));
            door.center = new Vector3(0, 1, 0.7f);
            FixtureBox(fixture.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(5, 0.2f, 5));
            using (var review = new YQCellPassageReviewV2(fixture))
            {
                if (!review.MeasureSwing("Door", 90).Clear || !review.MeasureSwing("Door", -90).Clear ||
                    !review.MeasureSwing("Door", 90).Clear)
                    return "Clear swing failed or direction review changed the closed pose.";
                if (review.MeasureSwing("Door", float.NaN).Clear || review.MeasureSwing("Missing", 90).Clear ||
                    review.MeasureSwing("Door", 180).Clear)
                    return "Unbounded or missing swing inputs passed.";
            }
            // note: The obstruction is outside the closed and fully opened leaf: endpoint-only tests would miss it.
            BoxCollider obstruction = FixtureBox(fixture.transform, "Obstruction", new Vector3(0.7f, 1, 0.7f), new Vector3(0.08f, 2, 0.08f));
            using (var review = new YQCellPassageReviewV2(fixture))
            {
                if (review.MeasureSwing("Door", 90).Clear || !review.MeasureSwing("Door", -90).Clear)
                    return "Mid-swing obstruction or opposite safe direction was not distinguished.";
            }
            UnityEngine.Object.DestroyImmediate(obstruction.gameObject);
            // note: An obstacle inside a two-degree interval is covered by the angular envelope, even between sampled poses.
            door.size = new Vector3(0.001f, 2, 1.4f);
            float radians = 0.4f * Mathf.Deg2Rad;
            FixtureBox(fixture.transform, "ThinObstacle", new Vector3(Mathf.Sin(radians) * 1.2f, 1,
                Mathf.Cos(radians) * 1.2f), new Vector3(0.001f, 1, 0.001f));
            using (var review = new YQCellPassageReviewV2(fixture))
                if (review.MeasureSwing("Door", 90).Clear)
                    return "A thin between-pose obstruction passed.";
            if (!door.enabled || Quaternion.Angle(door.transform.localRotation, Quaternion.identity) > 0.001f)
                return "Swing review modified the source leaf.";
            fixture.transform.localScale = new Vector3(2, 1, 1);
            using (var review = new YQCellPassageReviewV2(fixture))
                if (review.MeasureSwing("Door", 90).failure.Length == 0)
                    return "A sheared hinge escaped the shape validity gate.";
            return null;
        }
        finally { EditorSceneManager.ClosePreviewScene(fixtureScene); }
    }

    public static string TestRoomFloorEvidence()
    {
        Scene fixtureScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject fixture = new GameObject("RoomFixture");
            SceneManager.MoveGameObjectToScene(fixture, fixtureScene);
            fixture.transform.SetPositionAndRotation(new Vector3(19, 5, -8), Quaternion.Euler(0, 31, 0));
            Transform anchor = new GameObject("Room").transform;
            anchor.SetParent(fixture.transform, false);
            BoxCollider floor = FixtureBox(fixture.transform, "Floor", new Vector3(0, -0.1f, 0), new Vector3(7, 0.2f, 7));
            Rect area = new Rect(-2, -1.5f, 4, 3);
            Vector3 entry = new Vector3(-1.5f, 0, 0);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
            {
                if (!review.MeasureFloorRegion("Room", area, 0, entry, 1f).Ready)
                    return "A supported, connected room failed review.";
                if (review.MeasureFloorRegion("Room", new Rect(0, 0, 64, 64), 0, entry).failure.Length == 0)
                    return "An oversized room escaped its bounded work limit.";
            }
            // note: The thin barrier sits between one-metre grid columns: both sides have clear sample points but must not be connected.
            BoxCollider barrier = FixtureBox(fixture.transform, "ThinDivider", new Vector3(0.5f, 1, 0), new Vector3(0.04f, 2, 6));
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
            {
                FloorRegionResult divided = review.MeasureFloorRegion("Room", area, 0, entry, 1f);
                if (divided.supported != divided.samples || divided.reachable >= divided.walkable || divided.Ready)
                    return "Disconnected floor space or a between-sample wall passed connectivity.";
            }
            UnityEngine.Object.DestroyImmediate(barrier.gameObject);
            // note: A tall furnishing must obstruct walking while the support test continues to measure the actual floor beneath it.
            BoxCollider furnishing = FixtureBox(fixture.transform, "Furnishing", new Vector3(0.9f, 1.1f, 0.8f), new Vector3(0.7f, 2.2f, 0.7f));
            using (var review = new YQCellPassageReviewV2(fixture))
            {
                var furnished = review.MeasureFloorRegion("Room", area, 0, entry, 0.3f, "Furnishing");
                if (!furnished.Ready || furnished.walkable >= furnished.samples)
                    return "Furnishing support exclusion hid an obstacle or lost the underlying floor.";
                if (review.CheckObjectPenetration("Furnishing").Length != 0)
                    return "Clear furniture/floor contact was incorrectly rejected.";
                if (review.MeasureFloorRegion("Room", area, 0, entry, 0.3f, "MissingFurniture").Ready)
                    return "A missing furnishing exclusion path passed.";
            }
            BoxCollider intersecting = FixtureBox(fixture.transform, "OtherFurniture", new Vector3(0.95f, 1, 0.8f), Vector3.one);
            using (var review = new YQCellPassageReviewV2(fixture))
                if (review.CheckObjectPenetration("Furnishing").Length == 0)
                    return "Intersecting furniture collision was accepted.";
            UnityEngine.Object.DestroyImmediate(intersecting.gameObject);
            if (!furnishing.enabled) return "Furniture review changed source collision state.";
            floor.enabled = false;
            using (var review = new YQCellPassageReviewV2(fixture))
                if (review.MeasureFloorRegion("Room", area, 0, entry, 0.3f, "Furnishing").supported != 0)
                    return "Furniture supplied fake floor support.";
            floor.enabled = true;
            UnityEngine.Object.DestroyImmediate(furnishing.gameObject);
            floor.size = new Vector3(7, 0.2f, 0.15f);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
                if (review.MeasureFloorRegion("Room", area, 0, entry).Ready || review.MeasureFloorRegion("Room", area, 0, entry).entryClear)
                    return "A narrow beam was accepted as a supported player footprint.";
            UnityEngine.Object.DestroyImmediate(floor.gameObject);
            using (YQCellPassageReviewV2 review = new YQCellPassageReviewV2(fixture))
                if (review.MeasureFloorRegion("Room", area, 0, entry).supported != 0)
                    return "Missing authored support was replaced with implicit terrain.";
            return null;
        }
        finally { EditorSceneManager.ClosePreviewScene(fixtureScene); }
    }

    private static BoxCollider FixtureBox(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        BoxCollider box = item.AddComponent<BoxCollider>();
        box.size = size;
        return box;
    }
}

// note: Prefab isolation provides a direct manual inspection target without loading or modifying the gameplay scene.
public sealed class YQCellDoorReviewWindowV2 : EditorWindow
{
    private Vector2 scroll;
    private YQReviewedSemanticSiteManifest manifest;
    private bool inspectCandidates;

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Inspect First Kit Doorways")]
    private static void OpenReview() => GetWindow<YQCellDoorReviewWindowV2>("V2 Doorway Review");

    private void OnEnable()
    {
        manifest = AssetDatabase.LoadAssetAtPath<YQReviewedSemanticSiteManifest>(YQSiteFunctionContractReviewV2.RuntimePath);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Inspect the exact doorway in Prefab Mode. Check floor contact on both sides, head clearance, hinge/swing, furnishing and the connected route. This tool does not approve contracts or save prefabs.", MessageType.Info);
        if (manifest == null)
            return;
        // note: Review the repaired variant explicitly instead of accidentally reopening the unchanged source district.
        inspectCandidates = EditorGUILayout.ToggleLeft("Inspect repaired candidates (not active in gameplay)", inspectCandidates);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone?.prefab == null || zone.cellContractsV2 == null)
                continue;
            GameObject targetPrefab = inspectCandidates ? AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/" + zone.stableId + "_entrance_candidate.prefab") : zone.prefab;
            if (targetPrefab == null)
            {
                EditorGUILayout.LabelField(zone.stableId + " — no candidate", EditorStyles.boldLabel);
                continue;
            }
            EditorGUILayout.LabelField(zone.stableId, EditorStyles.boldLabel);
            foreach (YQReviewedCellFunctionContractV2 contract in zone.cellContractsV2)
                foreach (YQCellDoorBindingV2 door in contract?.doorBindings ?? new List<YQCellDoorBindingV2>())
                {
                    if (door == null || !GUILayout.Button(door.targetPath))
                        continue;
                    PrefabStage stage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(targetPrefab));
                    if (stage != null && YQCellDoorBindingsV2.TryResolveUniquePath(stage.prefabContentsRoot.transform, door.targetPath, out Transform target))
                    {
                        Selection.activeGameObject = target.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                }
        }
        EditorGUILayout.EndScrollView();
    }
}
