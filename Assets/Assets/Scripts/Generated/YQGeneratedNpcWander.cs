using UnityEngine;

/// <summary>
/// Small deterministic pacing controller for generated residents.
/// It stays local to the authored settlement and yields whenever a wall or
/// missing floor blocks the next step, so it never becomes a second navigation system.
/// </summary>
public sealed class YQGeneratedNpcWander : MonoBehaviour
{
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

    // note: Population configures the controller once so every resident gets a stable local route without runtime allocation or a global NPC manager.
    public void Configure(string seed, float localRadius = 6f, float movementSpeed = 0.8f)
    {
        _origin = transform.position;
        _radius = Mathf.Clamp(localRadius, 2f, 10f);
        _speed = Mathf.Clamp(movementSpeed, 0.35f, 1.35f);
        _state = StableHash((seed ?? string.Empty) + "|npc_wander|" + GetInstanceID());
        if (_state == 0u)
            _state = 0x9E3779B9u;
        _nextDecision = Time.unscaledTime + Next01() * DecisionInterval;
    }

    private void Awake()
    {
        _origin = transform.position;
        if (_state == 0u)
            _state = (uint)Mathf.Abs(GetInstanceID()) + 1u;
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
            return;

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
        next.y = ground.y;
        // note: Turn the visual actor toward its accepted local destination so movement reads as intentional instead of sliding sideways.
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), Mathf.Clamp01(Time.deltaTime * 8f));
        transform.position = next;
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
                _target = new Vector3(candidate.x, ground.y, candidate.z);
                _hasTarget = true;
                return true;
            }
        }

        _hasTarget = false;
        return false;
    }

    private bool TryGetGround(Vector3 candidate, out Vector3 ground)
    {
        int count = Physics.RaycastNonAlloc(candidate + Vector3.up * 12f, Vector3.down,
            _groundHits, 30f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        ground = candidate;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = _groundHits[index];
            if (hit.collider == null || hit.normal.y < 0.60f || IsOwnedCollider(hit.collider) || IsRoofLike(hit.collider))
                continue;
            if (hit.point.y > _origin.y + 2.5f || hit.point.y < _origin.y - 2.5f)
                continue;
            if (hit.point.y > best)
            {
                best = hit.point.y;
                ground = hit.point;
            }
        }
        return best > float.MinValue;
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
