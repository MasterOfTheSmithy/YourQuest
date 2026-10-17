using UnityEngine;
using Unity.Profiling;

/// <summary>
/// Small deterministic pacing controller for generated residents.
/// It stays local to the authored settlement and yields whenever a wall or
/// missing floor blocks the next step, so it never becomes a second navigation system.
/// </summary>
public sealed class YQGeneratedNpcWander : MonoBehaviour
{
    private static readonly ProfilerMarker G08UpdateMarker = new ProfilerMarker("G08FrameCost.YQGeneratedNpcWander.Update()");
    private const float DecisionInterval = 0.45f;
    private const float ArrivalDistance = 0.35f;
    private const float CollisionProbeHeight = 0.85f;

    private readonly RaycastHit[] _groundHits = new RaycastHit[8];
    private readonly RaycastHit[] _obstacleHits = new RaycastHit[8];

    private Vector3 _origin;
    private Vector3 _target;
    private float _radius = 6f;
    private float _speed = 0.8f;
    private float _nextDecision;
    private uint _state;
    private bool _hasTarget;
    private float _contactOffset;
    private YQDotCreatureVisual _dotVisual;
    private YQDotGaitSampler _authoredGait;
    private bool _requiresReviewedSurface;
    private YQCompiledWorldSiteInstance.ResidentWanderSurfaceContext _reviewedSurface;

    // note: Population configures the controller once so every resident gets a stable local route without runtime allocation or a global NPC manager.
    public void Configure(string seed, float localRadius = 6f, float movementSpeed = 0.8f)
    {
        _origin = transform.position;
        // note: Initial placement corrects the imported pivot to the feet. Keep that same offset while wandering instead of snapping the pivot itself onto every sampled floor.
        if (!YQGeneratedWorldTerrain.TryGetActorContactGeometry(gameObject, out _, out float bottom))
            throw new System.InvalidOperationException("Resident movement has no body contact datum: " + name);
        _contactOffset = bottom - transform.position.y;
        _radius = Mathf.Clamp(localRadius, 2f, 10f);
        // note: Supplied DOT wildlife gaits include speeds below the legacy resident minimum; retain their declared cadence without changing other residents.
        _dotVisual = GetComponent<YQDotCreatureVisual>();
        // note: The approved cat loop travels below 0.1m/s; retain its supplied speed without changing prior residents or introducing another movement owner.
        float minimumSpeed = _dotVisual != null ? Mathf.Min(0.1f, _dotVisual.AuthoredWalkSpeed) : 0.35f;
        _speed = Mathf.Clamp(movementSpeed, minimumSpeed, 1.35f);
        // note: Only an explicitly supplied, validated travel profile opts into sampled gait pacing; existing NPC and wildlife routes retain their current controller.
        _authoredGait = _dotVisual != null && _dotVisual.MotionProfile != null && _dotVisual.MotionProfile.IsValid ? new YQDotGaitSampler(_dotVisual.MotionProfile) : null;
        _state = StableHash((seed ?? string.Empty) + "|npc_wander|" + GetInstanceID());
        if (_state == 0u)
            _state = 0x9E3779B9u;
        _nextDecision = Time.unscaledTime + Next01() * DecisionInterval;
    }

    internal bool UsesReviewedSurface => _requiresReviewedSurface;

    internal void RenewReviewedSurface(YQCompiledWorldSiteInstance provider, string locationId, EntityInfo entity)
    {
        // note: Geometry reload cannot transfer a retained actor to a different provider or world; the original provider explicitly renews its registered contract.
        if (!_requiresReviewedSurface) return;
        if (_reviewedSurface == null || !_reviewedSurface.CanRenewFrom(provider))
            throw new System.InvalidOperationException("Resident wander renewal has no original actor/world owner: " + name);
        BindReviewedSurface(locationId, entity);
    }

    internal void BindReviewedSurface(string locationId, EntityInfo entity)
    {
        // note: Initial contact and all later home/step queries borrow the exact registered provider's accepted floor authority; a missing receipt never becomes terrain fallback.
        _requiresReviewedSurface = true;
        if (entity == null || entity.gameObject != gameObject ||
            !YQCompiledWorldSiteInstance.TryCaptureResidentWanderSurface(locationId, entity, out _reviewedSurface) ||
            !_reviewedSurface.TryProjectInitial(transform.position, out Vector3 floor) ||
            !YQGeneratedWorldPopulation.TryPlaceResidentOnReviewedSurface(gameObject, floor) ||
            !YQGeneratedWorldTerrain.TryGetActorContactGeometry(gameObject, out _, out float bottom))
            throw new System.InvalidOperationException("Resident wander has no current reviewed home floor: " + name);
        _origin = transform.position;
        _contactOffset = bottom - _origin.y;
        _hasTarget = false;
    }

    private void Awake()
    {
        _origin = transform.position;
        if (_state == 0u)
            _state = (uint)Mathf.Abs(GetInstanceID()) + 1u;
    }

    // note: Attribute this project-owned callback during the focused G08 frame-budget witness.
    private void Update()
    {
        using (G08UpdateMarker.Auto())
            UpdateCore();
    }

    private void UpdateCore()
    {
        if (!isActiveAndEnabled)
            return;
        if (_authoredGait != null) { UpdateAuthoredGait(); return; }

        // note: Decisions are throttled so dozens of residents do not create per-frame raycast or allocation pressure.
        if (!_hasTarget || Time.unscaledTime >= _nextDecision)
        {
            _nextDecision = Time.unscaledTime + DecisionInterval;
            if (!TryChooseTarget())
                return;
        }

        Vector3 current = transform.position;
        Vector3 planarTarget = new Vector3(_target.x, current.y, _target.z);
        Vector3 delta = planarTarget - current;
        delta.y = 0f;
        if (delta.sqrMagnitude <= ArrivalDistance * ArrivalDistance)
        {
            _hasTarget = false;
            return;
        }

        Vector3 direction = delta.normalized;
        float step = _speed * Time.deltaTime;
        if (HitsExternalObstacle(current + Vector3.up * CollisionProbeHeight, direction,
                CollisionProbeHeight + 0.15f))
        {
            _hasTarget = false;
            return;
        }

        Vector3 next = current + direction * step;
        if (!TryGetGround(next, out Vector3 ground))
        {
            _hasTarget = false;
            return;
        }

        // note: Preserve the authored character pivot while following the sampled floor, preventing residents from climbing roofs or hovering over a cut bank.
        next.y = ground.y - _contactOffset;
        // note: Turn the visual actor toward its accepted local destination so movement reads as intentional instead of sliding sideways.
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), Mathf.Clamp01(Time.deltaTime * 8f));
        transform.position = next;
    }

    private void UpdateAuthoredGait()
    {
        // note: Navigation, ground and collision remain owned here. Sampled start/loop/stop travel replaces only the repaired stag's constant-speed step.
        if (!_hasTarget && !_authoredGait.IsActive && Time.unscaledTime >= _nextDecision)
        {
            _nextDecision = Time.unscaledTime + DecisionInterval;
            TryChooseTarget();
        }
        Vector3 current = transform.position, delta = _target - current; delta.y = 0f;
        float distance = delta.magnitude;
        bool wantsMove = _hasTarget && distance > _authoredGait.ContinuationDistance + .02f;
        if (!wantsMove) _hasTarget = false;
        bool wasActive = _authoredGait.IsActive;
        float step = Mathf.Min(distance, _authoredGait.Advance(wantsMove, Time.deltaTime));
        Vector3 direction = distance > .0001f ? delta / distance : Vector3.zero;
        Vector3 next = current + direction * step;
        if (step > 0f)
        {
            if (HitsExternalObstacle(current + Vector3.up * CollisionProbeHeight, direction, CollisionProbeHeight + .15f) || !TryGetGround(next, out Vector3 ground))
            {
                // note: Physical safety still overrides authored travel when the ground or route is blocked.
                _hasTarget = false; _authoredGait.Reset(); _nextDecision = Time.unscaledTime + DecisionInterval;
            }
            else
            {
                next.y = ground.y - _contactOffset;
                // note: Rotate the measured imported body axis toward the route, keeping positive profile travel horizontal and root motion disabled.
                Vector3 forward = _dotVisual.MotionProfile.localForward;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)), Mathf.Clamp01(Time.deltaTime * 8f));
                transform.position = next;
            }
        }
        _dotVisual.SetAuthoredGait(_authoredGait);
        if (wasActive && !_authoredGait.IsActive && !_hasTarget) _nextDecision = Mathf.Max(_nextDecision, Time.unscaledTime + DecisionInterval);
    }

    private bool TryChooseTarget()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float angle = Next01() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(1.5f, _radius, Next01());
            Vector3 candidate = _origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (TryGetGround(candidate, out Vector3 ground))
            {
                _target = new Vector3(candidate.x, ground.y - _contactOffset, candidate.z);
                _hasTarget = true;
                return true;
            }
        }

        // note: A reviewed resident can head back to its validated home when nearby pacing targets fail, but each return step still requires the same current floor.
        if (_requiresReviewedSurface && TryGetGround(_origin, out Vector3 home))
        {
            _target = new Vector3(_origin.x, home.y - _contactOffset, _origin.z);
            _hasTarget = true;
            return true;
        }
        _hasTarget = false;
        return false;
    }

    private bool TryGetGround(Vector3 candidate, out Vector3 ground)
    {
        // note: Preserve reviewed ownership through every target/step/home query; unsupported or stale provider floors stop this resident instead of admitting terrain beneath a building.
        if (_requiresReviewedSurface)
        {
            ground = candidate;
            return _reviewedSurface != null && _reviewedSurface.TryProject(candidate, out ground) &&
                ground.y <= _origin.y + _contactOffset + 2.5f && ground.y >= _origin.y + _contactOffset - 2.5f;
        }
        // note: Search near the current foot plane so elevated shelves and upper floors cannot lift a grounded resident several metres in one step.
        float footHeight = candidate.y + _contactOffset;
        int count = Physics.RaycastNonAlloc(new Vector3(candidate.x, footHeight + 0.45f, candidate.z), Vector3.down,
            _groundHits, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        ground = candidate;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = _groundHits[index];
            if (hit.collider == null || YQPhysicalChunkAdmissionBoundary.IsOwned(hit.collider) || hit.normal.y < 0.60f || IsOwnedCollider(hit.collider) || IsRoofLike(hit.collider))
                continue;
            if (hit.point.y > _origin.y + _contactOffset + 2.5f || hit.point.y < _origin.y + _contactOffset - 2.5f)
                continue;
            float difference = Mathf.Abs(hit.point.y - footHeight);
            if (difference < best)
            {
                best = difference;
                ground = hit.point;
            }
        }
        return best < float.PositiveInfinity;
    }

    private bool HitsExternalObstacle(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, _obstacleHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int index = 0; index < hitCount; index++)
        {
            Collider collider = _obstacleHits[index].collider;
            if (collider != null && !IsOwnedCollider(collider) && !IsRoofLike(collider))
                return true;
        }
        return false;
    }

    private bool IsOwnedCollider(Collider collider)
    {
        return collider == null || collider.transform == transform || collider.transform.IsChildOf(transform);
    }

    private static bool IsRoofLike(Collider collider)
    {
        string name = collider != null && collider.transform != null ? collider.transform.name.ToLowerInvariant() : string.Empty;
        return name.Contains("roof") || name.Contains("ceiling") || name.Contains("gable") || name.Contains("rafters");
    }

    private float Next01()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (_state & 0x00FFFFFFu) / 16777216f;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
