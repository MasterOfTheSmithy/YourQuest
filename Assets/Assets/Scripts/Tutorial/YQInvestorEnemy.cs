// Assets/Assets/Scripts/Tutorial/YQInvestorEnemy.cs
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorEnemy : MonoBehaviour
{
    public string semanticRegionId = "region_unknown";
    public string factionId = "wild_hollows";
    public string displayName = "Echo";
    public float maxHealth = 55f;
    public float moveSpeed = 3.8f;
    public float aggroRange = 18f;
    public float attackRange = 1.75f;
    public float attackCooldown = 1.1f;
    public int attackDamage = 10;
    public int goldDrop = 12;
    public bool useWispVisual = true;
    public string rarity = "common";
    public float preferredRange = 5.4f;
    public float spellRange = 10.5f;
    public float spellCooldown = 3.4f;
    public float evadeCooldown = 1.25f;
    [Range(0f, 1f)] public float evadeChanceOnHit = 0.38f;
    public bool allowFlight;

    private float _health;
    private float _nextAttack;
    private float _nextSpell;
    private float _nextEvade;
    private float _evadeUntil;
    private float _nextDecisionTime;
    private float _staggerUntil;
    private float _strafeSign = 1f;
    private Vector3 _evadeDirection;
    private Transform _player;
    private YQInvestorCombat _playerCombat;
    private YQInvestorEnemySpawner _spawner;
    private EntityInfo _entityInfo;
    private Rigidbody _body;
    private Animator _animator;
    private Renderer[] _modelRenderers;
    private GameObject _burrowSurfaceVfx;
    private bool _usesBurrowMovement;
    private bool _burrowHidden;
    private bool _lastMoving;

    public void Initialize(YQInvestorEnemySpawner spawner)
    {
        _spawner = spawner;
        _health = maxHealth;
        allowFlight = allowFlight || YQInvestorEnemySpawner.IsFlyingEnemy(spawner != null ? spawner.enemyPrefabPath : string.Empty, displayName);
        ConfigureBodyPhysics();
        EnforceGrounding(true);
    }

    private void Awake()
    {
        _health = maxHealth;
        _entityInfo = GetComponent<EntityInfo>();
        _body = GetComponent<Rigidbody>();
        if (_body == null)
        {
            _body = gameObject.AddComponent<Rigidbody>();
            _body.mass = 1f;
        }
        ConfigureBodyPhysics();

        if (_entityInfo != null)
            _entityInfo.targetingPlayer = false;

        _animator = ResolveUsableAnimator();

        bool hasImportedModel = GetComponentInChildren<SkinnedMeshRenderer>(true) != null || transform.Find("Model_" + displayName) != null;
        if (useWispVisual && !hasImportedModel && GetComponent<YQEchoFlameWispVisual>() == null)
            gameObject.AddComponent<YQEchoFlameWispVisual>();

        _modelRenderers = ResolveModelRenderers();
        _usesBurrowMovement = hasImportedModel && !HasMovementAnimationSupport();
        _strafeSign = Random.value < 0.5f ? -1f : 1f;
        _nextSpell = Time.time + Random.Range(0.85f, 2.4f);
        _nextEvade = Time.time + Random.Range(0.45f, 1.25f);
    }

    private void Update()
    {
        EnforceGrounding(false);

        if (_player == null)
            ResolvePlayer();
        if (_player == null)
            return;

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        float sqrDist = toPlayer.sqrMagnitude;
        float aggroSqr = aggroRange * aggroRange;
        if (_entityInfo != null)
            _entityInfo.targetingPlayer = sqrDist <= aggroSqr;

        if (sqrDist > aggroSqr)
        {
            SetAnimatorMoving(false);
            SetBurrowMovementActive(false);
            return;
        }
        if (Time.time < _staggerUntil)
        {
            SetAnimatorMoving(false);
            SetBurrowMovementActive(false);
            return;
        }

        float distance = Mathf.Sqrt(Mathf.Max(0.0001f, sqrDist));
        Vector3 dir = toPlayer / distance;
        RefreshTacticalDecision();

        if (Time.time < _evadeUntil)
        {
            MoveEnemy(_evadeDirection, moveSpeed * 1.18f, true);
            return;
        }

        if (TryCastSpell(dir, distance))
            return;

        if (distance > attackRange)
        {
            MoveEnemy(BuildTacticalMoveDirection(dir, distance), moveSpeed, true);
            return;
        }

        SetAnimatorMoving(false);
        SetBurrowMovementActive(false);
        if (Time.time >= _nextAttack)
        {
            _nextAttack = Time.time + attackCooldown;
            TriggerAnimatorAttack();
            YQRuntimeAudioFeedback.PlayEnemyAttack(transform.position + Vector3.up * 1f);
            if (_playerCombat == null)
                _playerCombat = _player.GetComponent<YQInvestorCombat>();
            if (_playerCombat != null)
                _playerCombat.ReceiveDamage(attackDamage, gameObject);
        }
    }

    public void ReceiveHit(int amount, GameObject source)
    {
        _health -= Mathf.Max(1, amount);
        _staggerUntil = Time.time + 0.14f;
        TriggerAnimatorHit();
        if (_health > 0f)
        {
            TryStartEvade(source);
            YQRuntimeAudioFeedback.PlayEnemyHit(transform.position + Vector3.up * 1f);
            return;
        }

        TriggerAnimatorDeath();
        YQRuntimeAudioFeedback.PlayEnemyDeath(transform.position + Vector3.up * 1f);

        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null)
        {
            psm.state.AddXp(25);
            psm.state.AddLedgerLine("The player defeated " + displayName + " in " + semanticRegionId + ".");
            psm.state.IncCounter("kill:" + semanticRegionId, 1f);
            psm.Save();
        }

        WorldStateManager wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.State != null)
        {
            wsm.State.ApplyLocationDelta(semanticRegionId, "add", 0.05f, "contested", "A hostile echo was defeated here.");
            wsm.Save();
        }

        InventoryItemRecord loot = GeneratedRpgContentService.Instance != null
            ? GeneratedRpgContentService.Instance.GenerateItem(semanticRegionId + ":" + displayName, psm != null && psm.state != null ? psm.state.level : 1, null, false)
            : null;

        SpawnCorpse(loot, Mathf.Max(1, goldDrop));

        YQInvestorDirector director = FindFirstObjectByType<YQInvestorDirector>();
        if (director != null)
            director.NotifyEnemyKilled(this);

        _spawner?.NotifyEnemyDied(this);
        Destroy(gameObject);
    }

    public void ApplyVariant(string variantName, Color primary, float scaleMultiplier, float healthMultiplier, float damageMultiplier)
    {
        rarity = string.IsNullOrWhiteSpace(variantName) ? "common" : variantName.Trim().ToLowerInvariant();
        transform.localScale *= Mathf.Clamp(scaleMultiplier, 0.55f, 2.25f);
        maxHealth *= Mathf.Max(0.35f, healthMultiplier);
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * Mathf.Max(0.35f, damageMultiplier)));
        goldDrop = Mathf.Max(1, Mathf.RoundToInt(goldDrop * Mathf.Max(0.5f, healthMultiplier)));
        _health = maxHealth;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", primary);
            block.SetColor("_Color", primary);
            renderer.SetPropertyBlock(block);
        }
    }

    private void SpawnCorpse(InventoryItemRecord item, int gold)
    {
        GameObject corpse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        corpse.name = displayName + " Echo Residue";
        corpse.transform.position = transform.position;
        corpse.transform.rotation = transform.rotation;
        corpse.transform.localScale = new Vector3(0.75f, 0.22f, 0.75f);
        Renderer renderer = corpse.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, new Color(1f, 0.34f, 0.12f, 1f));
        ParticleSystem ps = corpse.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.22f;
        main.startSize = 0.10f;
        main.startColor = new Color(1f, 0.45f, 0.16f, 0.65f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 8f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        ParticleSystemRenderer psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
            psRenderer.material = YQGeneratedRuntimeVfx.CreateParticleMaterial(new Color(1f, 0.45f, 0.16f, 1f));
        Rigidbody rb = corpse.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        EntityInfo info = corpse.AddComponent<EntityInfo>();
        info.entityId = "loot_" + semanticRegionId + "_" + GetInstanceID();
        info.displayName = displayName + " Residue";
        info.factionId = "loot";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = new[] { "loot", "corpse", "residue", "echo", semanticRegionId };
        YQInvestorLootableCorpse lootable = corpse.AddComponent<YQInvestorLootableCorpse>();
        lootable.Initialize(displayName, item, gold);
    }

    private void ResolvePlayer()
    {
        if (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative)
        {
            _player = YQInvestorPlayerMotor.ActiveMotor.transform;
            _playerCombat = _player.GetComponent<YQInvestorCombat>();
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        _player = player.transform;
        _playerCombat = player.GetComponent<YQInvestorCombat>();
    }

    private void ConfigureBodyPhysics()
    {
        if (_body == null)
        {
            _body =
                GetComponent<Rigidbody>();
        }

        if (_body == null)
            return;

        _body.useGravity =
            false;

        /*
         * Unity forbids writing velocity while the Rigidbody is already
         * kinematic. Imported/generated character visuals can reach this
         * method already configured that way.
         */
        if (!_body.isKinematic)
        {
            _body.linearVelocity =
                Vector3.zero;

            _body.angularVelocity =
                Vector3.zero;
        }

        _body.isKinematic =
            true;

        _body.constraints =
            RigidbodyConstraints
                .FreezeRotation;

        _body.collisionDetectionMode =
            CollisionDetectionMode
                .ContinuousSpeculative;

        _body.interpolation =
            RigidbodyInterpolation
                .Interpolate;
    }

    private void EnforceGrounding(bool immediate)
    {
        if (ShouldAllowFlight())
            return;

        Vector3 grounded = GroundedEnemyPosition(transform.position);
        float deltaY = grounded.y - transform.position.y;
        if (!immediate && Mathf.Abs(deltaY) <= 0.006f)
            return;

        Vector3 position = transform.position;
        position.y = immediate || Mathf.Abs(deltaY) > 0.35f
            ? grounded.y
            : Mathf.MoveTowards(position.y, grounded.y, 6f * Time.deltaTime);
        transform.position = position;
    }

    private Vector3 GroundedEnemyPosition(Vector3 position)
    {
        if (ShouldAllowFlight())
            return position;
        if (YQInvestorEnemySpawner.TryGetGroundedEnemyPosition(position, out Vector3 grounded, 0.025f, transform))
            return grounded;
        if (position.y > 2.5f)
            position.y = Mathf.MoveTowards(position.y, 0.025f, 12f * Time.deltaTime);
        return position;
    }

    private bool ShouldAllowFlight()
    {
        return allowFlight || YQInvestorEnemySpawner.IsFlyingEnemy(_spawner != null ? _spawner.enemyPrefabPath : string.Empty, displayName);
    }

    private void RefreshTacticalDecision()
    {
        if (Time.time < _nextDecisionTime)
            return;

        _nextDecisionTime = Time.time + Random.Range(0.7f, 1.35f);
        if (Random.value < 0.42f)
            _strafeSign *= -1f;
    }

    private Vector3 BuildTacticalMoveDirection(Vector3 toPlayerDir, float distance)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, toPlayerDir).normalized * _strafeSign;
        if (lateral.sqrMagnitude < 0.01f)
            lateral = transform.right * _strafeSign;

        float preferred = Mathf.Max(attackRange + 1.15f, preferredRange);
        if (distance < preferred * 0.72f)
            return (-toPlayerDir + lateral * 0.72f).normalized;
        if (distance < preferred)
            return lateral.normalized;
        if (distance < spellRange && Time.time >= _nextSpell - 0.75f)
            return (lateral * 0.86f + toPlayerDir * 0.18f).normalized;

        return (toPlayerDir + lateral * 0.30f).normalized;
    }

    private void MoveEnemy(Vector3 direction, float speed, bool moving)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            SetAnimatorMoving(false);
            SetBurrowMovementActive(false);
            return;
        }

        direction.Normalize();
        transform.position = GroundedEnemyPosition(transform.position + direction * Mathf.Max(0f, speed) * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1f - Mathf.Exp(-12f * Time.deltaTime));
        SetAnimatorMoving(moving);
        SetBurrowMovementActive(_usesBurrowMovement && moving);
    }

    private bool TryCastSpell(Vector3 toPlayerDir, float distance)
    {
        if (distance < attackRange + 0.85f || distance > spellRange)
            return false;
        if (Time.time < _nextSpell)
            return false;
        if (!HasLineOfSightToPlayer())
            return false;

        _nextSpell = Time.time + Mathf.Max(0.65f, spellCooldown + Random.Range(-0.45f, 0.85f));
        SetAnimatorMoving(false);
        SetBurrowMovementActive(false);
        if (toPlayerDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toPlayerDir);

        TriggerAnimatorAttack();
        YQRuntimeAudioFeedback.PlaySpellCast(transform.position + Vector3.up * 1.15f);
        bool launched = YQGeneratedRuntimeVfx.TrySpawnSpellProjectile(transform, BuildEnemySpellDescriptor(), Mathf.Max(4, attackDamage + 4), gameObject);
        if (!launched && _playerCombat != null)
            _playerCombat.ReceiveDamage(Mathf.Max(2, attackDamage / 2), gameObject);
        return true;
    }

    private string BuildEnemySpellDescriptor()
    {
        string lower = ((displayName ?? string.Empty) + " " + (semanticRegionId ?? string.Empty)).ToLowerInvariant();
        if (lower.Contains("frost") || lower.Contains("ice"))
            return "hostile frost projectile shard";
        if (lower.Contains("ember") || lower.Contains("cinder") || lower.Contains("fire") || lower.Contains("drake"))
            return "hostile fire projectile bolt";
        if (lower.Contains("tide") || lower.Contains("brine") || lower.Contains("water"))
            return "hostile water projectile lance";
        if (lower.Contains("root") || lower.Contains("thorn") || lower.Contains("spore") || lower.Contains("plant"))
            return "hostile poison thorn projectile";

        return "hostile arcane projectile bolt";
    }

    private void TryStartEvade(GameObject source)
    {
        if (Time.time < _nextEvade || Random.value > evadeChanceOnHit)
            return;

        Vector3 away = transform.position - (source != null ? source.transform.position : (_player != null ? _player.position : transform.position - transform.forward));
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f)
            away = -transform.forward;

        Vector3 lateral = Vector3.Cross(Vector3.up, away.normalized) * (Random.value < 0.5f ? -1f : 1f);
        _evadeDirection = (away.normalized * 0.55f + lateral * 0.85f).normalized;
        _evadeUntil = Time.time + Random.Range(0.28f, 0.48f);
        _nextEvade = Time.time + Mathf.Max(0.4f, evadeCooldown + Random.Range(-0.15f, 0.4f));
    }

    private bool HasLineOfSightToPlayer()
    {
        if (_player == null)
            return false;

        Vector3 from = transform.position + Vector3.up * 1.05f;
        Vector3 to = _player.position + Vector3.up * 1.15f;
        Vector3 dir = to - from;
        float distance = dir.magnitude;
        if (distance <= 0.01f)
            return true;

        dir /= distance;
        RaycastHit[] hits = Physics.RaycastAll(from, dir, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (hit == null)
                continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            if (hit.transform == _player || hit.transform.IsChildOf(_player))
                return true;

            return false;
        }

        return true;
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (_animator == null)
            return;

        float speed = moving ? 1f : 0f;
        SetAnimatorFloat("Speed", speed);
        SetAnimatorFloat("speed", speed);
        SetAnimatorFloat("MoveSpeed", speed);
        SetAnimatorFloat("moveSpeed", speed);
        SetAnimatorFloat("Locomotion", speed);
        SetAnimatorFloat("locomotion", speed);
        SetAnimatorFloat("Forward", speed);
        SetAnimatorFloat("forward", speed);
        SetAnimatorBool("Moving", moving);
        SetAnimatorBool("moving", moving);
        SetAnimatorBool("IsMoving", moving);
        SetAnimatorBool("isMoving", moving);

        if (_lastMoving == moving)
            return;

        _lastMoving = moving;
        if (moving)
            PlayAnimatorState("Walk", "walk", "Run", "run", "Locomotion", "locomotion", "crawl", "Crawl");
        else
            PlayAnimatorState("Idle", "idle", "Idle_Combat", "idle_combat");
    }

    private void TriggerAnimatorAttack()
    {
        if (_animator == null)
            return;

        string lowerName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.ToLowerInvariant();
        if ((lowerName.Contains("dragon") || lowerName.Contains("drake")) &&
            SetFirstAnimatorTrigger("breatheFire", "flyBreatheFire", "attack1", "flyAttack", "Attack", "attack"))
            return;

        if (lowerName.Contains("bandit") &&
            SetFirstAnimatorTrigger("bAttack1Right", "attack1Right", "attack1", "Attack", "attack"))
            return;

        SetFirstAnimatorTrigger("Attack", "attack", "attack1", "attack2", "attack1Right", "attack1Left", "bAttack1Right", "bAttack1Left", "bKickRight", "bKickLeft", "breatheFire", "flyBreatheFire", "flyAttack");
    }

    private void TriggerAnimatorHit()
    {
        if (_animator == null)
            return;

        SetFirstAnimatorTrigger("Hit", "hit", "GotHit", "gotHit", "gotHit1", "gotHit2", "flyGotHit");
    }

    private void TriggerAnimatorDeath()
    {
        if (_animator == null)
            return;

        SetFirstAnimatorTrigger("Die", "die", "Death", "death", "bDie", "flyDeathStart");
    }

    private bool SetFirstAnimatorTrigger(params string[] parameters)
    {
        if (parameters == null)
            return false;

        for (int i = 0; i < parameters.Length; i++)
        {
            string parameter = parameters[i];
            if (!HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
                continue;

            _animator.SetTrigger(parameter);
            return true;
        }

        return false;
    }

    private void SetAnimatorFloat(string parameter, float value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Float))
            _animator.SetFloat(parameter, value);
    }

    private void SetAnimatorBool(string parameter, bool value)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Bool))
            _animator.SetBool(parameter, value);
    }

    private void SetAnimatorTrigger(string parameter)
    {
        if (HasAnimatorParameter(parameter, AnimatorControllerParameterType.Trigger))
            _animator.SetTrigger(parameter);
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameter))
            return false;

        AnimatorControllerParameter[] parameters = _animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
            if (p != null && p.type == type && string.Equals(p.name, parameter, System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private bool HasMovementAnimationSupport()
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
            return false;

        return HasAnimatorParameter("Speed", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("speed", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("MoveSpeed", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("moveSpeed", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("Locomotion", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("locomotion", AnimatorControllerParameterType.Float) ||
               HasAnimatorParameter("Moving", AnimatorControllerParameterType.Bool) ||
               HasAnimatorParameter("moving", AnimatorControllerParameterType.Bool) ||
               HasAnimatorState("Walk", "walk", "Run", "run", "Locomotion", "locomotion", "Crawl", "crawl");
    }

    private bool PlayAnimatorState(params string[] stateNames)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || stateNames == null)
            return false;

        const int layer = 0;
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int shortHash = Animator.StringToHash(stateName);
            if (_animator.HasState(layer, shortHash))
            {
                _animator.CrossFade(shortHash, 0.08f, layer);
                return true;
            }

            int fullHash = Animator.StringToHash("Base Layer." + stateName);
            if (_animator.HasState(layer, fullHash))
            {
                _animator.CrossFade(fullHash, 0.08f, layer);
                return true;
            }
        }

        return false;
    }

    private bool HasAnimatorState(params string[] stateNames)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null || stateNames == null)
            return false;

        const int layer = 0;
        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;
            if (_animator.HasState(layer, Animator.StringToHash(stateName)) ||
                _animator.HasState(layer, Animator.StringToHash("Base Layer." + stateName)))
                return true;
        }

        return false;
    }

    private Renderer[] ResolveModelRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
            return System.Array.Empty<Renderer>();

        System.Collections.Generic.List<Renderer> result = new System.Collections.Generic.List<Renderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            result.Add(renderer);
        }

        return result.ToArray();
    }

    private void SetBurrowMovementActive(bool active)
    {
        if (!_usesBurrowMovement)
        {
            if (_burrowSurfaceVfx != null)
                _burrowSurfaceVfx.SetActive(false);
            return;
        }

        if (_burrowHidden != active)
        {
            _burrowHidden = active;
            for (int i = 0; i < _modelRenderers.Length; i++)
            {
                if (_modelRenderers[i] != null)
                    _modelRenderers[i].enabled = !active;
            }
        }

        EnsureBurrowSurfaceVfx();
        if (_burrowSurfaceVfx == null)
            return;

        _burrowSurfaceVfx.transform.position = transform.position + Vector3.up * 0.08f;
        _burrowSurfaceVfx.SetActive(active);
    }

    private void EnsureBurrowSurfaceVfx()
    {
        if (_burrowSurfaceVfx != null)
            return;

        _burrowSurfaceVfx = new GameObject("Burrow_SurfaceTell");
        _burrowSurfaceVfx.transform.SetParent(transform, false);
        _burrowSurfaceVfx.transform.localPosition = Vector3.up * 0.08f;
        ParticleSystem ps = _burrowSurfaceVfx.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 0.32f;
        main.startSpeed = 0.34f;
        main.startSize = 0.12f;
        main.startColor = new Color(0.50f, 0.42f, 0.30f, 0.62f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 18f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.48f;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.material = YQGeneratedRuntimeVfx.CreateParticleMaterial(new Color(0.55f, 0.46f, 0.32f, 0.8f));
        _burrowSurfaceVfx.SetActive(false);
    }

    private Animator ResolveUsableAnimator()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            animator.enabled = true;
            animator.applyRootMotion = false;
            EnsureAnimationEventReceiver(animator);
            return animator;
        }

        return null;
    }

    private static void EnsureAnimationEventReceiver(Animator animator)
    {
        if (animator == null)
            return;

        YQAnimationEventAudioReceiver receiver = animator.GetComponent<YQAnimationEventAudioReceiver>();
        if (receiver == null)
            receiver = animator.gameObject.AddComponent<YQAnimationEventAudioReceiver>();
        receiver.enabled = true;
    }

}
