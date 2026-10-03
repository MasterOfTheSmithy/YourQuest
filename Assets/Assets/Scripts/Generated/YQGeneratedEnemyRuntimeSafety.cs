using System;
using UnityEngine;
using Unity.Profiling;

public sealed class YQGeneratedEnemyRuntimeSafety :
    MonoBehaviour
{
    private static readonly ProfilerMarker G08UpdateMarker = new ProfilerMarker("G08FrameCost.YQGeneratedEnemyRuntimeSafety.Update()");
    private const float GroundCheckInterval =
        0.25f;

    private const float AudioCheckInterval =
        2.00f;

    private const float VisualCheckInterval =
        0.40f;

    private const float GroundProbeAbove =
        8.0f;

    private const float GroundProbeDistance =
        40.0f;

    private const float GroundEmbedDepth =
        0.005f;

    private const float AllowedBelowGround =
        0.30f;

    private const float EmergencyFallDistance =
        3.0f;

    private static YQGeneratedEnemyRuntimeSafety
        _manager;

    private static readonly RaycastHit[]
        GroundHits =
            new RaycastHit[32];

    private bool _isManager;

    private YQInvestorEnemy _enemy;

    private float _nextGroundCheckTime;
    private Rigidbody _body;

    private CapsuleCollider _safetyCollider;

    private bool _initialized;

    private float _fallbackScanTime;

    private float _nextAudioCheckTime;

    private float _lastSafeGroundY;

    private float _lastSafeRootY;

    private bool _hasSafeGround;
    private Vector3 _lastSafeBodyPosition;
    private bool _hasSafeBodyPosition;
    private AudioSource[] _cachedAudioSources;
    private Renderer[] _cachedVisualRenderers;
    private bool[] _baselineRendererEnabled;
    private bool _explicitlySuspended;
    private float _nextVisualCheckTime;
    private bool _reportedVisualRecovery;

    private float _nextLocomotionAudioCheckTime;

    // ============================================================
    // AUTO INSTALL
    // ============================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallManager()
    {
        if (_manager != null)
            return;

        GameObject root =
            new GameObject(
                "__YQ_GENERATED_ENEMY_RUNTIME_SAFETY");

        DontDestroyOnLoad(
            root);

        _manager =
            root.AddComponent<
                YQGeneratedEnemyRuntimeSafety>();

        _manager._isManager =
            true;

        // note: Run one delayed compatibility scan for pre-existing scene enemies; generated spawn paths attach guards directly without recurring world searches.
        _manager._fallbackScanTime =
            Time.unscaledTime +
            1.0f;
    }

    public static void EnsureAttached(
        YQInvestorEnemy enemy)
    {
        if (enemy == null ||
            enemy.GetComponent<
                YQGeneratedEnemyRuntimeSafety>() !=
                null)
        {
            return;
        }

        // note: Attach safety while this one generated hostile is created so initialization work follows the existing frame-budgeted spawn cadence.
        YQGeneratedEnemyRuntimeSafety guard =
            enemy.gameObject.AddComponent<
                YQGeneratedEnemyRuntimeSafety>();

        guard._isManager =
            false;
    }

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (_isManager)
            return;

        InitializeEnemyGuard();
    }

    private void Start()
    {
        if (_isManager)
            return;

        InitializeEnemyGuard();

        /*
         * Wait until all prefab Awake/Start methods have had an
         * opportunity to initialize, then correct placement.
         */
        GroundEnemy(
            true);

        SanitizeEnemyAudio();
    }

    // note: Attribute this project-owned callback during the focused G08 frame-budget witness.
    private void Update()
    {
        using (G08UpdateMarker.Auto())
            UpdateCore();
    }

    private void UpdateCore()
    {
        if (_isManager)
        {
            ManagerUpdate();

            return;
        }

        if (!_initialized)
        {
            InitializeEnemyGuard();
        }

        if (Time.unscaledTime >=
            _nextAudioCheckTime)
        {
            _nextAudioCheckTime =
                Time.unscaledTime +
                AudioCheckInterval;

            SanitizeEnemyAudio();
            if (Time.unscaledTime >=
    _nextLocomotionAudioCheckTime)
            {
                _nextLocomotionAudioCheckTime =
                    Time.unscaledTime +
                    0.15f;

                SuppressFalseLocomotionAudio();
            }
        }

        if (Time.unscaledTime >= _nextVisualCheckTime)
        {
            _nextVisualCheckTime = Time.unscaledTime + VisualCheckInterval;
            int restored = RestoreBaselineRendererVisibility(
                _cachedVisualRenderers,
                _baselineRendererEnabled);
            if (restored > 0 && !_reportedVisualRecovery)
            {
                // note: Keep this diagnostic one-shot per hostile so a defective imported animation cannot flood the console while the visible combat body is recovered.
                Debug.LogWarning(
                    "[YQGeneratedEnemyRuntimeSafety] Restored " + restored +
                    " unexpectedly disabled combat renderer(s) on " + gameObject.name + ".");
                _reportedVisualRecovery = true;
            }
        }
    }

    private void FixedUpdate()
    {
        if (_isManager ||
            !_initialized)
        {
            return;
        }

        /*
         * Ground safety is a recovery mechanism, not locomotion.
         *
         * Running a raycast every physics frame on ~100 enemies is
         * unnecessary and causes avoidable CPU spikes.
         */
        if (Time.unscaledTime <
            _nextGroundCheckTime)
        {
            return;
        }

        _nextGroundCheckTime =
            Time.unscaledTime +
            GroundCheckInterval;

        MaintainGroundSafety();
        MaintainWallSafety();
    }

    // ============================================================
    // MANAGER
    // ============================================================

    private void ManagerUpdate()
    {
        if (Time.unscaledTime <
            _fallbackScanTime)
        {
            return;
        }

        // note: This one-time scan preserves compatibility for generated hostiles already present at scene startup without allocating and traversing every enemy forever.
        YQInvestorEnemy[] enemies =
            FindObjectsByType<
                YQInvestorEnemy>(
                    FindObjectsSortMode.None);

        for (int i = 0;
             i < enemies.Length;
             i++)
        {
            YQInvestorEnemy enemy =
                enemies[i];

            if (enemy == null ||
                !IsGeneratedEnemy(
                    enemy))
            {
                continue;
            }

            /*
             * Mimics have their own chest/reveal physics path.
             */
            if (string.Equals(
                    enemy.factionId,
                    "mimics",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureAttached(
                enemy);
        }

        enabled =
            false;
    }

    private static bool IsGeneratedEnemy(
        YQInvestorEnemy enemy)
    {
        if (enemy == null)
            return false;

        string objectName =
            enemy.gameObject.name ?? string.Empty;

        if (objectName.StartsWith(
                "Hostile__",
                StringComparison.OrdinalIgnoreCase) ||
            objectName.StartsWith(
                "HostileLeader__",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Transform current =
            enemy.transform;

        while (current != null)
        {
            if (string.Equals(
                    current.name,
                    "YQ_GENERATED_WORLD_RUNTIME",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void InitializeEnemyGuard()
    {
        if (_initialized ||
            _isManager)
        {
            return;
        }

        _enemy =
            GetComponent<YQInvestorEnemy>();

        if (_enemy == null)
        {
            enabled =
                false;

            return;
        }

        _body =
            GetComponent<Rigidbody>();
        _explicitlySuspended =
            YQTerrainSupportComposer.IsExplicitlySuspended(
                gameObject);
        _cachedAudioSources =
    GetComponentsInChildren<AudioSource>(
        true);
        // note: Visual grounding renderers are captured once; quarter-second recovery checks never allocate a new hierarchy array per hostile.
        _cachedVisualRenderers =
            GetComponentsInChildren<Renderer>(
                true);
        _baselineRendererEnabled = new bool[_cachedVisualRenderers.Length];
        for (int rendererIndex = 0;
             rendererIndex < _cachedVisualRenderers.Length;
             rendererIndex++)
        {
            // note: Only renderers intentionally enabled when the generated enemy becomes live are eligible for recovery; hidden variants remain hidden.
            _baselineRendererEnabled[rendererIndex] =
                IsGroundVisualRenderer(_cachedVisualRenderers[rendererIndex]);
        }

        if (_body == null)
        {
            _body =
                gameObject.AddComponent<Rigidbody>();

            _body.mass =
                1f;

            _body.useGravity =
                true;
        }

        ConfigureRootPhysics();

        EnsureRootGroundCollider();

        MakeChildRigidbodiesSafe();

        SanitizeEnemyAudio();

        _lastSafeRootY =
            transform.position.y;

        // note: The last accepted rigidbody pose lets the guard reject a later movement step that crosses a generated building wall.
        _lastSafeBodyPosition = _body != null ? _body.position : transform.position;
        _hasSafeBodyPosition = true;

        float phase = StablePhase01(GetInstanceID());
        // note: Generated enemies are commonly spawned in batches; deterministic phasing distributes their recovery raycasts and audio checks across later frames.
        _nextGroundCheckTime = Time.unscaledTime +
            phase * GroundCheckInterval;
        _nextAudioCheckTime = Time.unscaledTime +
            phase * AudioCheckInterval;
        _nextLocomotionAudioCheckTime = _nextAudioCheckTime;
        _nextVisualCheckTime = Time.unscaledTime +
            phase * VisualCheckInterval;

        _initialized =
            true;
    }

    internal static int RestoreBaselineRendererVisibility(
        Renderer[] renderers,
        bool[] baselineEnabled)
    {
        if (renderers == null || baselineEnabled == null)
            return 0;

        int count = Mathf.Min(renderers.Length, baselineEnabled.Length);
        bool hasRecoverableVisual = false;
        bool anyEnabled = false;
        for (int index = 0; index < count; index++)
        {
            Renderer renderer = renderers[index];
            if (!baselineEnabled[index] || renderer == null ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            hasRecoverableVisual = true;
            if (renderer.enabled)
            {
                anyEnabled = true;
                break;
            }
        }

        if (!hasRecoverableVisual || anyEnabled)
            return 0;

        int restored = 0;
        for (int index = 0; index < count; index++)
        {
            Renderer renderer = renderers[index];
            if (!baselineEnabled[index] || renderer == null ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            renderer.enabled = true;
            restored++;
        }

        // note: Recovery is all-or-nothing and only runs when every originally visible combat renderer was disabled, preserving ordinary authored renderer/LOD selection.
        return restored;
    }

    private static float StablePhase01(int instanceId)
    {
        unchecked
        {
            uint value = (uint)instanceId;
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            return (value & 1023u) / 1024f;
        }
    }

    // ============================================================
    // ROOT PHYSICS
    // ============================================================

    private void ConfigureRootPhysics()
    {
        if (_body == null)
            return;

        /*
         * Never allow imported prefabs to rotate themselves onto
         * their side through physics.
         */
        _body.constraints |=
            RigidbodyConstraints.FreezeRotation;

        _body.interpolation =
            RigidbodyInterpolation.Interpolate;

        if (_body.isKinematic)
        {
            _body.collisionDetectionMode =
                CollisionDetectionMode
                    .ContinuousSpeculative;
        }
        else
        {
            _body.collisionDetectionMode =
                CollisionDetectionMode
                    .ContinuousDynamic;
        }
    }

    private void MakeChildRigidbodiesSafe()
    {
        Rigidbody[] bodies =
            GetComponentsInChildren<Rigidbody>(
                true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            Rigidbody body =
                bodies[i];

            if (body == null ||
                body ==
                _body)
            {
                continue;
            }

            /*
             * Imported monster ragdolls must not wake at spawn and
             * pull the model through the floor.
             */
            body.isKinematic =
                true;

            body.useGravity =
                false;
        }
    }

    // ============================================================
    // COLLIDER
    // ============================================================

    private void EnsureRootGroundCollider()
    {
        Collider rootCollider =
            GetComponent<Collider>();

        Bounds visualBounds;

        if (!TryGetVisualBounds(
                out visualBounds))
        {
            return;
        }

        if (rootCollider != null &&
            rootCollider.enabled &&
            !rootCollider.isTrigger &&
            IsColliderEnvelopeCredible(rootCollider.bounds, visualBounds))
        {
            return;
        }

        if (rootCollider != null &&
            rootCollider.enabled &&
            !rootCollider.isTrigger)
        {
            // note: Imported root collision that substantially exceeds the visible creature creates invisible combat walls; replace it with the measured safety capsule below.
            rootCollider.enabled = false;
        }

        _safetyCollider =
            GetComponent<CapsuleCollider>();

        if (_safetyCollider == null)
        {
            _safetyCollider =
                gameObject.AddComponent<
                    CapsuleCollider>();
        }

        Vector3 lossy =
            transform.lossyScale;

        float scaleX =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    lossy.x));

        float scaleY =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    lossy.y));

        float scaleZ =
            Mathf.Max(
                0.001f,
                Mathf.Abs(
                    lossy.z));

        float worldHeight =
            Mathf.Max(
                0.8f,
                visualBounds.size.y);

        float worldWidth =
            Mathf.Max(
                0.4f,
                Mathf.Min(
                    visualBounds.size.x,
                    visualBounds.size.z));

        float localHeight =
            worldHeight /
            scaleY;

        float localRadius =
            Mathf.Max(
                0.12f,
                (worldWidth *
                 0.32f) /
                Mathf.Max(
                    scaleX,
                    scaleZ));

        _safetyCollider.direction =
            1;

        _safetyCollider.height =
            Mathf.Max(
                localHeight *
                0.92f,
                localRadius *
                2f);

        _safetyCollider.radius =
            Mathf.Min(
                localRadius,
                _safetyCollider.height *
                0.48f);

        Vector3 centerWorld =
            visualBounds.center;

        Vector3 localCenter =
            transform.InverseTransformPoint(
                centerWorld);

        _safetyCollider.center =
            localCenter;

        _safetyCollider.isTrigger =
            false;

        _safetyCollider.enabled =
            true;
    }

    internal static bool IsColliderEnvelopeCredible(
        Bounds colliderBounds,
        Bounds visualBounds)
    {
        if (visualBounds.size.x <= 0.01f ||
            visualBounds.size.y <= 0.01f ||
            visualBounds.size.z <= 0.01f)
        {
            return false;
        }

        bool credibleX = colliderBounds.size.x <= Mathf.Max(
            visualBounds.size.x * 1.75f,
            visualBounds.size.x + 0.8f);
        bool credibleY = colliderBounds.size.y <= Mathf.Max(
            visualBounds.size.y * 1.75f,
            visualBounds.size.y + 1.0f);
        bool credibleZ = colliderBounds.size.z <= Mathf.Max(
            visualBounds.size.z * 1.75f,
            visualBounds.size.z + 0.8f);
        bool overlapsVisual = colliderBounds.Intersects(visualBounds);

        // note: Simplified enemy collision may exceed limbs and weapons, but it must remain close to and overlap the visible combat body.
        return credibleX && credibleY && credibleZ && overlapsVisual;
    }

    // ============================================================
    // GROUNDING
    // ============================================================

    private void MaintainGroundSafety()
    {
        if (UsesSuspendedPlacement())
        {
            // note: Explicitly flying or suspended hostiles retain their authored air position; the safety recovery path never drags them onto terrain.
            return;
        }

        Bounds bounds;

        if (!TryGetVisualBounds(
                out bounds))
        {
            return;
        }

        RaycastHit ground;

        if (TryFindGround(
                bounds,
                out ground))
        {
            float visualBottom =
                bounds.min.y;

            float difference =
                visualBottom -
                ground.point.y;

            /*
             * Enemy is already visibly below the floor.
             */
            if (difference <
                -AllowedBelowGround)
            {
                MoveVisualBottomToGround(
                    bounds,
                    ground.point.y);

                SaveSafeGround(
                    ground.point.y);

                return;
            }

            /*
             * Close enough to valid ground that this position can
             * become our recovery anchor.
             */
            if (difference >=
                    -AllowedBelowGround &&
                difference <=
                    1.5f)
            {
                SaveSafeGround(
                    ground.point.y);
            }
        }

        /*
         * Emergency recovery.
         *
         * If physics somehow gets past the TerrainCollider or a
         * malformed imported collider, restore the hostile instead
         * of allowing it to disappear into the world.
         */
        if (_hasSafeGround &&
            transform.position.y <
                _lastSafeRootY -
                EmergencyFallDistance)
        {
            Vector3 restored =
                transform.position;

            restored.y =
                _lastSafeRootY;

            // note: Update only this hostile's transform and Rigidbody pose; a full-world Physics.SyncTransforms here can hitch when many generated colliders exist.
            SetRootPosition(
                restored);

            ZeroVerticalVelocity();

            Bounds recoveredBounds;

            if (TryGetVisualBounds(
                    out recoveredBounds))
            {
                RaycastHit recoveredGround;

                if (TryFindGround(
                        recoveredBounds,
                        out recoveredGround))
                {
                    MoveVisualBottomToGround(
                        recoveredBounds,
                        recoveredGround.point.y);

                    SaveSafeGround(
                        recoveredGround.point.y);
                }
            }

            Debug.LogWarning(
                "[YQGeneratedEnemyRuntimeSafety] " +
                "Recovered generated hostile that fell below ground: " +
                gameObject.name);
        }
    }

    private void GroundEnemy(
        bool initialPlacement)
    {
        if (UsesSuspendedPlacement())
            return;

        Bounds bounds;

        if (!TryGetVisualBounds(
                out bounds))
        {
            return;
        }

        RaycastHit ground;

        if (!TryFindGround(
                bounds,
                out ground))
        {
            return;
        }

        float visualBottom =
            bounds.min.y;

        float difference =
            visualBottom -
            ground.point.y;

        /*
         * Initial generated placement should sit on the actual world
         * surface rather than trusting arbitrary prefab pivots.
         */
        if (initialPlacement ||
            difference <
                -AllowedBelowGround)
        {
            MoveVisualBottomToGround(
                bounds,
                ground.point.y);
        }

        SaveSafeGround(
            ground.point.y);
    }

    private bool TryFindGround(
        Bounds visualBounds,
        out RaycastHit bestHit)
    {
        bestHit =
            default;

        float startY =
            Mathf.Max(
                visualBounds.max.y +
                    GroundProbeAbove,
                _hasSafeGround
                    ? _lastSafeGroundY +
                        GroundProbeAbove
                    : transform.position.y +
                        GroundProbeAbove);

        Vector3 origin =
            new Vector3(
                visualBounds.center.x,
                startY,
                visualBounds.center.z);

        int count =
            Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                GroundHits,
                GroundProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

        float bestDistance =
            float.MaxValue;

        bool found =
            false;

        for (int i = 0;
             i < count;
             i++)
        {
            RaycastHit hit =
                GroundHits[i];

            Collider collider =
                hit.collider;

            if (collider == null)
                continue;

            Transform hitTransform =
                collider.transform;

            if (hitTransform ==
                    transform ||
                hitTransform.IsChildOf(
                    transform))
            {
                continue;
            }

            /*
             * Do not treat another enemy as terrain.
             */
            YQInvestorEnemy otherEnemy =
                collider.GetComponentInParent<
                    YQInvestorEnemy>();

            if (otherEnemy != null)
                continue;

            if (hit.distance >=
                bestDistance)
            {
                continue;
            }

            bestDistance =
                hit.distance;

            bestHit =
                hit;

            found =
                true;
        }

        return found;
    }

    private void MoveVisualBottomToGround(
        Bounds bounds,
        float groundY)
    {
        // note: Enemy recovery preserves a tiny terrain embed and uses filtered cached body bounds; it no longer allocates a hierarchy scan or reintroduces a visible 4 cm hover gap.
        float delta =
            groundY -
            GroundEmbedDepth -
            bounds.min.y;

        if (Mathf.Abs(
                delta) <
            0.001f)
        {
            return;
        }

        Vector3 position =
            transform.position;

        position.y +=
            delta;

        // note: Grounding updates only the corrected hostile pose so startup batches do not force a global physics reconciliation per enemy.
        SetRootPosition(
            position);

        ZeroVerticalVelocity();
    }

    private void MaintainWallSafety()
    {
        if (!_hasSafeBodyPosition || _body == null || _safetyCollider == null ||
            !_safetyCollider.enabled || UsesSuspendedPlacement())
            return;

        Vector3 current = _body.position;
        Vector3 delta = current - _lastSafeBodyPosition;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
            return;

        Bounds bounds = _safetyCollider.bounds;
        float radius = Mathf.Max(0.08f, Mathf.Min(bounds.extents.x, bounds.extents.z));
        float half = Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 bottom = bounds.center + Vector3.down * half;
        Vector3 top = bounds.center + Vector3.up * half;
        Vector3 direction = delta / distance;
        if (Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit,
                distance + 0.03f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider != null &&
            !(hit.collider is TerrainCollider) &&
            !hit.collider.transform.IsChildOf(transform))
        {
            // note: Revert only the blocked translation and preserve tangential velocity, preventing enemies from tunnelling through walls while still allowing ordinary sliding.
            Vector3 safe = _lastSafeBodyPosition + direction * Mathf.Max(0f, hit.distance - 0.04f);
            _body.position = safe;
            // note: Kinematic imported hostiles are moved by their transform; only dynamic bodies accept a velocity correction.
            if (!_body.isKinematic)
                _body.linearVelocity = Vector3.ProjectOnPlane(_body.linearVelocity, hit.normal);
            return;
        }

        _lastSafeBodyPosition = current;
    }

    private void SetRootPosition(
        Vector3 position)
    {
        transform.position =
            position;

        if (_body != null)
        {
            _body.position =
                position;
        }
    }

    private void SaveSafeGround(
        float groundY)
    {
        _lastSafeGroundY =
            groundY;

        _lastSafeRootY =
            transform.position.y;

        _hasSafeGround =
            true;
    }

    private bool UsesSuspendedPlacement()
    {
        return (_enemy != null && _enemy.allowFlight) ||
            _explicitlySuspended;
    }

    private void ZeroVerticalVelocity()
    {
        if (_body == null ||
            _body.isKinematic)
        {
            return;
        }

        Vector3 velocity =
            _body.linearVelocity;

        velocity.y =
            0f;

        _body.linearVelocity =
            velocity;
    }

    // ============================================================
    // VISUAL BOUNDS
    // ============================================================

    private bool TryGetVisualBounds(
        out Bounds result)
    {
        result =
            default;

        Renderer[] renderers =
            _cachedVisualRenderers;

        if (renderers == null)
        {
            renderers =
                GetComponentsInChildren<Renderer>(
                    true);
            _cachedVisualRenderers =
                renderers;
        }

        bool found =
            false;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer =
                renderers[index];

            if (!IsGroundVisualRenderer(renderer))
                continue;

            if (!found)
            {
                result =
                    renderer.bounds;
                found =
                    true;
            }
            else
            {
                result.Encapsulate(
                    renderer.bounds);
            }
        }

        return found;
    }

    private static bool IsGroundVisualRenderer(
        Renderer renderer)
    {
        if (renderer == null ||
            !renderer.enabled ||
            renderer is ParticleSystemRenderer ||
            renderer is TrailRenderer ||
            renderer is LineRenderer)
        {
            return false;
        }

        string objectName =
            renderer.name ?? string.Empty;
        return objectName.IndexOf(
                   "preview",
                   StringComparison.OrdinalIgnoreCase) < 0 &&
            objectName.IndexOf(
                "decal",
                StringComparison.OrdinalIgnoreCase) < 0 &&
            objectName.IndexOf(
                "gizmo",
                StringComparison.OrdinalIgnoreCase) < 0;
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void SuppressFalseLocomotionAudio()
    {
        if (_cachedAudioSources == null ||
            _cachedAudioSources.Length == 0)
        {
            return;
        }

        float planarSpeed =
            0f;

        if (_body != null &&
            !_body.isKinematic)
        {
            Vector3 velocity =
                _body.linearVelocity;

            velocity.y =
                0f;

            planarSpeed =
                velocity.magnitude;
        }

        /*
         * Only suppress locomotion sounds when the generated enemy
         * is actually standing still.
         */
        if (planarSpeed >
            0.20f)
        {
            return;
        }

        for (int i = 0;
             i < _cachedAudioSources.Length;
             i++)
        {
            AudioSource source =
                _cachedAudioSources[i];

            if (source == null ||
                !source.isPlaying)
            {
                continue;
            }

            AudioClip clip =
                source.clip;

            if (clip == null)
                continue;

            string clipName =
                clip.name
                    .Replace(
                        '-',
                        '_')
                    .Replace(
                        ' ',
                        '_')
                    .ToLowerInvariant();

            bool locomotionSound =
                clipName.Contains(
                    "footstep") ||
                clipName.Contains(
                    "foot_step") ||
                clipName.Contains(
                    "steps") ||
                clipName.Contains(
                    "walking") ||
                clipName.Contains(
                    "walk_loop") ||
                clipName.Contains(
                    "running") ||
                clipName.Contains(
                    "run_loop");

            if (!locomotionSound)
                continue;

            source.Stop();
        }
    }

    private void SanitizeEnemyAudio()
    {
        // note: Enemy combat owns its own audio; imported demo managers must not fire startup/animation one-shots.
        YQImportedDemoAudioFirewall
            .RemoveImportedDemoAudioBehaviours(
                gameObject,
                removeAnimationEventReceiver: false);

        AudioSource[] sources =
            GetComponentsInChildren<AudioSource>(
                true);

        for (int i = 0;
             i < sources.Length;
             i++)
        {
            AudioSource source =
                sources[i];

            if (source == null)
                continue;

            source.playOnAwake =
                false;

            source.spatialBlend =
                1f;

            source.dopplerLevel =
                0f;

            source.volume =
                Mathf.Min(
                    source.volume,
                    0.16f);

            source.minDistance =
                Mathf.Max(
                    source.minDistance,
                    1.5f);

            source.maxDistance =
                Mathf.Clamp(
                    source.maxDistance,
                    8f,
                    24f);

            source.rolloffMode =
                AudioRolloffMode.Logarithmic;

            /*
             * We now know from the diagnostic logs that imported
             * generated monsters are shipping with this persistent
             * prefab/demo idle clip.
             *
             * Remove ONLY the idle loop. The AudioSource stays enabled,
             * so PlayOneShot attack, hurt and death audio can still work.
             */
            if (IsImportedIdleLoop(
                    source.clip) ||
                YQImportedDemoAudioFirewall
                    .IsImportedDemoAmbientOrVoxAudioSource(
                        source))
            {
                // note: Imported idle/vox clips are prefab demo ambience; generated enemies should only speak through gameplay-owned audio.
                if (source.isPlaying)
                {
                    source.Stop();
                }

                source.loop =
                    false;

                source.clip =
                    null;
            }
        }
    }

    private static bool IsImportedIdleLoop(
        AudioClip clip)
    {
        if (clip == null)
            return false;

        string name =
            clip.name ?? string.Empty;

        name =
            name
                .Replace(
                    '-',
                    '_')
                .Replace(
                    ' ',
                    '_')
                .ToLowerInvariant();

        return
            name ==
                "idle_loop" ||
            name.StartsWith(
                "idle_loop_",
                StringComparison.Ordinal);
    }
}
