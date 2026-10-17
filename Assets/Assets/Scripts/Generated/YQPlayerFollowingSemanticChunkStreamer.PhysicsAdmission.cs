using System.Collections.Generic;
using UnityEngine;

// note: The existing streamer owns these transient physical admission boxes; they never become semantic content, terrain, saved records or a second physics/player authority.
public sealed partial class YQPlayerFollowingSemanticChunkStreamer
{
    private const int PhysicsAdmissionCellLimit = 256;
    private readonly Dictionary<Vector2Int, BoxCollider> _physicsAdmissionBoxes = new Dictionary<Vector2Int, BoxCollider>();
    private readonly Stack<BoxCollider> _physicsAdmissionPool = new Stack<BoxCollider>();
    private readonly HashSet<Vector2Int> _physicsAdmissionRequired = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> _physicsAdmissionRetired = new List<Vector2Int>();
    private YQPlayerRagdoll _physicsAdmissionActor;
    private int _physicsAdmissionEpoch = -1, _physicsAdmissionAllocated;

    internal bool OwnsPhysicsAdmissionCollider(Collider collider, int epoch, Vector2Int cell) =>
        epoch == _configurationEpoch && epoch == _physicsAdmissionEpoch &&
        _physicsAdmissionBoxes.TryGetValue(cell, out var owned) && owned == collider && owned.enabled;

    internal bool TryPreparePlayerPhysicsAdmission(YQPlayerRagdoll actor, Collider[] shapes, float motionReach,
        out string failure)
    {
        failure = "physics admission context unavailable";
        var motor = actor != null ? actor.GetComponent<YQInvestorPlayerMotor>() : null;
        if (!IsPlayerMovementAdmissionAvailable(motor) || _player == null || actor == null || !motor.IsAuthoritative ||
            shapes == null || shapes.Length == 0 || !FiniteAdmission(motionReach) || motionReach < 0f) return false;
        if (_physicsAdmissionActor != null && _physicsAdmissionActor != actor)
        { failure = "physics admission already belongs to another actor"; return false; }
        if (_physicsAdmissionEpoch != _configurationEpoch) ClearPlayerPhysicsAdmission(true);
        _physicsAdmissionEpoch = _configurationEpoch; _physicsAdmissionActor = actor;
        bool initialized = false; Bounds actorBounds = default;
        foreach (Collider shape in shapes)
        {
            if (shape == null || !shape.enabled || !shape.gameObject.activeInHierarchy)
            { failure = "current owned ragdoll collider bounds unavailable"; return false; }
            Bounds shapeBounds = shape.bounds;
            if (!FiniteAdmission(shapeBounds.center) || !FiniteAdmission(shapeBounds.size) || shapeBounds.size.sqrMagnitude <= 0f)
            { failure = "current owned ragdoll collider bounds unavailable"; return false; }
            if (!initialized) { actorBounds = shapeBounds; initialized = true; }
            else actorBounds.Encapsulate(shapeBounds);
        }
        if (!TryValidatePlayerPhysicsBounds(actorBounds, out failure)) return false;
        // note: Rotation may change every capsule's projected extents. Include the complete current body's radius plus its admitted movement envelope; native speculative CCD still supplies actual contacts.
        float reach = motionReach + actorBounds.extents.magnitude + .1f;
        Bounds demand = actorBounds; demand.Expand(new Vector3(reach * 2f, 0f, reach * 2f));
        if (!TryAdmissionRange(demand, out int minX, out int maxX, out int minZ, out int maxZ))
        { failure = "physics admission request exceeds 256-cell bounded work budget"; return false; }
        _physicsAdmissionRequired.Clear();
        for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
        {
            var cell = new Vector2Int(x, z);
            if (_chunks.TryGetValue(cell, out var owner) && IsReadyForPlayerEntry(cell, owner)) continue;
            _physicsAdmissionRequired.Add(cell);
            // note: Reuse the existing traversal-demand owner; the blocker cannot certify or fabricate this cell's missing assets.
            AdmitPhysicalTraversalDemand(cell);
        }
        bool changed = RetireReadyPhysicsAdmissionBoxes();
        _physicsAdmissionRetired.Clear();
        foreach (var pair in _physicsAdmissionBoxes)
            if (!_physicsAdmissionRequired.Contains(pair.Key)) _physicsAdmissionRetired.Add(pair.Key);
        foreach (var cell in _physicsAdmissionRetired) { RetirePhysicsAdmissionBox(cell); changed = true; }
        float size = Mathf.Max(32f, chunkWorldSize);
        float padding = Mathf.Max(.02f, Physics.defaultContactOffset * 2f);
        float wallHeight = Mathf.Max(4096f, actorBounds.size.y + motionReach * 4f + 16f);
        foreach (var cell in _physicsAdmissionRequired)
        {
            if (!_physicsAdmissionBoxes.TryGetValue(cell, out var wall) || wall == null)
            {
                if (_physicsAdmissionBoxes.Remove(cell)) _physicsAdmissionAllocated--;
                while (_physicsAdmissionPool.Count > 0 && wall == null)
                { wall = _physicsAdmissionPool.Pop(); if (wall == null) _physicsAdmissionAllocated--; }
                if (wall == null)
                {
                    if (_physicsAdmissionAllocated >= PhysicsAdmissionCellLimit)
                    { failure = "physics admission collider budget exhausted"; return false; }
                    var go = new GameObject("YQ_PhysicsAdmissionBoundary") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
                    go.transform.SetParent(transform, true);
                    wall = go.AddComponent<BoxCollider>(); wall.enabled = false;
                    go.AddComponent<YQPhysicalChunkAdmissionBoundary>();
                    _physicsAdmissionAllocated++;
                }
                _physicsAdmissionBoxes.Add(cell, wall); changed = true;
                var marker = wall.GetComponent<YQPhysicalChunkAdmissionBoundary>();
                marker.Bind(this, _configurationEpoch, cell);
                wall.name = "YQ_PhysicsAdmissionBoundary_" + cell.x + "_" + cell.y;
            }
            Vector3 center = new Vector3(WorldGridOrigin + (cell.x + .5f) * size, actorBounds.center.y,
                WorldGridOrigin + (cell.y + .5f) * size);
            Vector3 dimensions = new Vector3(size + padding * 2f, wallHeight, size + padding * 2f);
            if ((wall.transform.position - center).sqrMagnitude > .000001f || (wall.size - dimensions).sqrMagnitude > .000001f || !wall.enabled)
            {
                wall.transform.SetPositionAndRotation(center, Quaternion.identity); wall.size = dimensions;
                wall.isTrigger = false; wall.enabled = true; changed = true;
            }
            // note: Admission must not pretend to supply contacts when the existing body/layer/pair policy disables them. Preserve that policy and hold the actual actor instead of changing physics settings.
            foreach (Collider shape in shapes)
            {
                var body = shape.attachedRigidbody;
                int wallBit = 1 << wall.gameObject.layer, bodyBit = 1 << shape.gameObject.layer;
                if (shape.isTrigger || body == null || !body.detectCollisions ||
                    Physics.GetIgnoreLayerCollision(shape.gameObject.layer, wall.gameObject.layer) || Physics.GetIgnoreCollision(shape, wall) ||
                    ((shape.excludeLayers.value | body.excludeLayers.value) & wallBit) != 0 || (wall.excludeLayers.value & bodyBit) != 0)
                { failure = "existing ragdoll contact policy does not admit physical frontier contacts"; return false; }
            }
        }
        if (changed) Physics.SyncTransforms();
        failure = string.Empty; return true;
    }

    internal bool TryValidatePlayerPhysicsBounds(Bounds bounds, out string failure)
    {
        if (!IsMovementAdmissionAvailable || _groundSafetyPhysicsSyncPending)
        { failure = "required streamed-world physics admission or native synchronization is unavailable"; return false; }
        failure = "physical player bounds exceed bounded admission range";
        if (!TryAdmissionRange(bounds, out int minX, out int maxX, out int minZ, out int maxZ)) return false;
        for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
        {
            var cell = new Vector2Int(x, z);
            if (_chunks.TryGetValue(cell, out var owner) && IsReadyForPlayerEntry(cell, owner)) continue;
            AdmitPhysicalTraversalDemand(cell);
            failure = "physical player footprint intersects unfinished cell " + cell; return false;
        }
        failure = string.Empty; return true;
    }

    internal void ReleasePlayerPhysicsAdmission(YQPlayerRagdoll actor)
    { if (_physicsAdmissionActor == actor) ClearPlayerPhysicsAdmission(false); }

    private bool RetireReadyPhysicsAdmissionBoxes()
    {
        if (_physicsAdmissionBoxes.Count == 0) return false;
        _physicsAdmissionRetired.Clear();
        foreach (var pair in _physicsAdmissionBoxes)
            if (_physicsAdmissionEpoch != _configurationEpoch ||
                (_chunks.TryGetValue(pair.Key, out var owner) && IsReadyForPlayerEntry(pair.Key, owner)))
                _physicsAdmissionRetired.Add(pair.Key);
        foreach (var cell in _physicsAdmissionRetired) RetirePhysicsAdmissionBox(cell);
        return _physicsAdmissionRetired.Count > 0;
    }

    private void RetirePhysicsAdmissionBox(Vector2Int cell)
    {
        if (!_physicsAdmissionBoxes.TryGetValue(cell, out var box)) return;
        _physicsAdmissionBoxes.Remove(cell);
        if (box != null) { box.enabled = false; _physicsAdmissionPool.Push(box); }
        else _physicsAdmissionAllocated = Mathf.Max(0, _physicsAdmissionAllocated - 1);
    }

    private void ClearPlayerPhysicsAdmission(bool destroy)
    {
        _physicsAdmissionRetired.Clear();
        foreach (var cell in _physicsAdmissionBoxes.Keys) _physicsAdmissionRetired.Add(cell);
        foreach (var cell in _physicsAdmissionRetired) RetirePhysicsAdmissionBox(cell);
        _physicsAdmissionActor = null; _physicsAdmissionEpoch = destroy ? -1 : _configurationEpoch; _physicsAdmissionRequired.Clear();
        if (!destroy) return;
        while (_physicsAdmissionPool.Count > 0)
        {
            var box = _physicsAdmissionPool.Pop();
            if (box == null) continue;
            box.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(box.gameObject);
            else UnityEngine.Object.DestroyImmediate(box.gameObject);
        }
        _physicsAdmissionAllocated = 0;
    }

    private bool TryAdmissionRange(Bounds bounds, out int minX, out int maxX, out int minZ, out int maxZ)
    {
        minX = maxX = minZ = maxZ = 0;
        if (!FiniteAdmission(bounds.center) || !FiniteAdmission(bounds.size) || !FiniteAdmission(bounds.min) || !FiniteAdmission(bounds.max) || bounds.size.x < 0f || bounds.size.z < 0f) return false;
        float size = Mathf.Max(32f, chunkWorldSize);
        // note: Reject integer-boundary coordinates before FloorToInt/loop increments can saturate or wrap; ordinary grid math remains identical to the existing owner.
        double x0 = ((double)bounds.min.x - WorldGridOrigin) / size, x1 = ((double)bounds.max.x - WorldGridOrigin) / size;
        double z0 = ((double)bounds.min.z - WorldGridOrigin) / size, z1 = ((double)bounds.max.z - WorldGridOrigin) / size;
        if (x0 <= int.MinValue + PhysicsAdmissionCellLimit || x1 >= int.MaxValue - PhysicsAdmissionCellLimit ||
            z0 <= int.MinValue + PhysicsAdmissionCellLimit || z1 >= int.MaxValue - PhysicsAdmissionCellLimit) return false;
        minX = Mathf.FloorToInt((bounds.min.x - WorldGridOrigin) / size); maxX = Mathf.FloorToInt((bounds.max.x - WorldGridOrigin) / size);
        minZ = Mathf.FloorToInt((bounds.min.z - WorldGridOrigin) / size); maxZ = Mathf.FloorToInt((bounds.max.z - WorldGridOrigin) / size);
        long width = (long)maxX - minX + 1, depth = (long)maxZ - minZ + 1;
        return width > 0 && depth > 0 && width <= PhysicsAdmissionCellLimit && depth <= PhysicsAdmissionCellLimit && width * depth <= PhysicsAdmissionCellLimit;
    }
    private static bool FiniteAdmission(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool FiniteAdmission(Vector3 value) => FiniteAdmission(value.x) && FiniteAdmission(value.y) && FiniteAdmission(value.z);
}
