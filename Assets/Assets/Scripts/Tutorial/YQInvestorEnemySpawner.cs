// Assets/Assets/Scripts/Tutorial/YQInvestorEnemySpawner.cs
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class YQInvestorEnemySpawner : MonoBehaviour
{
    private const float EnemyGroundOffset = 0.025f;
    private const string SpiderController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Spiders/Models/Spider 1.controller";
    private const string PlantController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Plant Monster/Models/Plant Monster Demo.controller";
    private const string MushroomController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mushroom Monster/Models/Mushroom Monster Demo.controller";
    private const string DragonController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Dragons/Models/Dragon Demo (v5).controller";
    private const string DemonController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Demons/Models/Demon Demo.controller";
    private const string HumanController = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/Demo Files/Human (Male & Female).controller";

    public int enemyCount = 4;
    public string factionId = "wild_hollows";
    public string semanticRegionId = "region_unknown";
    public string enemyDisplayName = "Echo Marauder";
    public string enemyPrefabPath = string.Empty;
    public bool allowImportedPrefabModelsInPlay = false;
    public bool requireOriginComplete = true;
    public bool requirePlayerNear = true;
    public bool despawnWhenPlayerFar = true;
    public string requiredCounter = string.Empty;
    public float requiredCounterMinimum = 1f;
    public float gatedSpawnRetryInterval = 1.25f;
    public float playerActivationDistance = 34f;
    public float playerFarDespawnDistance = 58f;
    [Range(0f, 8f)] public float spawnRadius = 2.6f;
    public Color primaryColor = new Color(1f, 0.42f, 0.12f, 1f);
    public Color secondaryColor = new Color(0.52f, 0.78f, 1f, 1f);

    private readonly List<YQInvestorEnemy> _alive = new List<YQInvestorEnemy>();
    private float _nextGatedSpawnCheckTime;
    private float _nextPlayerResolveTime;
    private Transform _player;

#if UNITY_EDITOR
    private static readonly Dictionary<string, GameObject> s_editorPrefabCache = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RuntimeAnimatorController> s_editorControllerCache = new Dictionary<string, RuntimeAnimatorController>(System.StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Material> s_fallbackMaterialCache = new Dictionary<string, Material>(System.StringComparer.OrdinalIgnoreCase);
#endif

    private void Awake()
    {
        PrimeSpawnGate();
    }

    public void PrimeSpawnGate()
    {
        _nextGatedSpawnCheckTime = Time.time + ResolveStableJitter(enemyDisplayName + "|" + semanticRegionId, 0.15f, 0.85f);
    }

    private void Update()
    {
        PruneMissingEnemies();
        if (_alive.Count > 0)
        {
            if (despawnWhenPlayerFar && IsPlayerFarFromActiveSpawn())
                DespawnAliveEnemies();
            return;
        }

        if (Time.time < _nextGatedSpawnCheckTime)
            return;

        _nextGatedSpawnCheckTime = Time.time + Mathf.Max(0.25f, gatedSpawnRetryInterval);
        SpawnNow();
    }

    public void SpawnNow()
    {
        if (_alive.Count > 0)
            return;
        if (!CanSpawnNow())
            return;

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * Mathf.Max(0f, spawnRadius);
            offset.y = 0f;
            Vector3 pos = transform.position + offset;
            if (!TryGetGroundedEnemyPosition(pos, out pos, EnemyGroundOffset, null))
                pos.y = Mathf.Min(pos.y, EnemyGroundOffset);

            GameObject go = new GameObject(enemyDisplayName + "_" + i);
            go.name = enemyDisplayName + "_" + i;
            go.transform.position = pos;

            GameObject model = TryCreateModel(go.transform, enemyDisplayName);
            bool hasImportedModel = model != null;
            float targetHeight = ResolveModelHeight(enemyPrefabPath, enemyDisplayName);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.height = hasImportedModel ? Mathf.Clamp(targetHeight, 1.1f, 3.1f) : 1.55f;
            collider.radius = hasImportedModel ? Mathf.Clamp(targetHeight * 0.28f, 0.38f, 1.05f) : 0.48f;
            collider.center = new Vector3(0f, collider.height * 0.5f, 0f);

            EntityInfo info = go.AddComponent<EntityInfo>();
            info.entityId = semanticRegionId + "_enemy_" + i;
            info.displayName = enemyDisplayName;
            info.level = 2;
            info.factionId = factionId;
            info.hostility = Hostility.Hostile;
            info.isNotable = false;
            info.tags = new string[] { "enemy", "tutorial", semanticRegionId };

            if (!hasImportedModel)
            {
                YQEchoFlameWispVisual visual = go.AddComponent<YQEchoFlameWispVisual>();
                visual.ApplyPalette(primaryColor, secondaryColor);
            }

            YQInvestorEnemy enemy = go.AddComponent<YQInvestorEnemy>();
            enemy.semanticRegionId = semanticRegionId;
            enemy.factionId = factionId;
            enemy.displayName = enemyDisplayName;
            enemy.allowFlight = IsFlyingEnemy(enemyPrefabPath, enemyDisplayName);
            enemy.Initialize(this);
            ApplyRarity(enemy, i);
            _alive.Add(enemy);
        }
    }

    public void NotifyEnemyDied(YQInvestorEnemy enemy)
    {
        _alive.Remove(enemy);
        if (_alive.Count == 0)
            _nextGatedSpawnCheckTime = Time.time + Mathf.Max(0.65f, gatedSpawnRetryInterval);
    }

    private bool CanSpawnNow()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (requireOriginComplete && !GeneratedRpgContentService.HasCompletedOrigin(state))
            return false;

        if (!string.IsNullOrWhiteSpace(requiredCounter))
        {
            if (state == null)
                return false;

            state.EnsureCollections();
            if (!state.behaviorCounters.TryGetValue(requiredCounter.Trim(), out float value) || value < requiredCounterMinimum)
                return false;
        }

        if (requirePlayerNear && !IsPlayerNearSpawn())
            return false;

        return true;
    }

    private bool IsPlayerNearSpawn()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null)
            return false;

        float distance = Mathf.Max(4f, playerActivationDistance);
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= distance * distance;
    }

    private bool IsPlayerFarFromActiveSpawn()
    {
        Transform player = ResolvePlayerTransform();
        if (player == null)
            return false;

        float distance = Mathf.Max(playerActivationDistance + 8f, playerFarDespawnDistance);
        Vector3 delta = player.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude > distance * distance;
    }

    private Transform ResolvePlayerTransform()
    {
        if (_player != null)
            return _player;
        if (Time.time < _nextPlayerResolveTime)
            return null;

        _nextPlayerResolveTime = Time.time + 0.75f;
        if (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative)
        {
            _player = YQInvestorPlayerMotor.ActiveMotor.transform;
            return _player;
        }

        GameObject player = GameObject.FindWithTag("Player");
        _player = player != null ? player.transform : null;
        return _player;
    }

    private void DespawnAliveEnemies()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            YQInvestorEnemy enemy = _alive[i];
            if (enemy != null)
                DestroyUnityObject(enemy.gameObject);
        }

        _alive.Clear();
        _nextGatedSpawnCheckTime = Time.time + Mathf.Max(1.0f, gatedSpawnRetryInterval);
    }

    private void PruneMissingEnemies()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }
    }

    private static float ResolveStableJitter(string value, float min, float max)
    {
        min = Mathf.Max(0f, min);
        max = Mathf.Max(min, max);
        uint hash = 2166136261u;
        string text = value ?? string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= 16777619u;
        }

        float t = (hash & 0xFFFF) / 65535f;
        return Mathf.Lerp(min, max, t);
    }

    public static bool TryGetGroundedEnemyPosition(Vector3 position, out Vector3 grounded, float yOffset = EnemyGroundOffset, Transform ignoreRoot = null)
    {
        grounded = position;
        float probeTop = Mathf.Clamp(position.y + 10f, 32f, 500f);
        Vector3 origin = new Vector3(position.x, probeTop, position.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeTop + 140f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsUsableEnemyGroundHit(hit, ignoreRoot))
                continue;

            grounded.y = hit.point.y + Mathf.Max(0f, yOffset);
            return true;
        }

        return false;
    }

    public static bool IsFlyingEnemy(string prefabPath, string label)
    {
        string text = ((prefabPath ?? string.Empty) + " " + (label ?? string.Empty)).Replace('\\', '/').ToLowerInvariant();
        return text.Contains("dragon") || text.Contains("drake");
    }

    private static bool IsUsableEnemyGroundHit(RaycastHit hit, Transform ignoreRoot)
    {
        Collider collider = hit.collider;
        if (collider == null || collider.isTrigger || hit.normal.y < 0.35f)
            return false;

        Transform hitTransform = collider.transform;
        if (hitTransform == null)
            return false;
        if (ignoreRoot != null && (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
            return false;
        if (hitTransform.GetComponentInParent<YQInvestorEnemy>() != null)
            return false;
        if (hitTransform.GetComponentInParent<YQInvestorPlayerMotor>() != null)
            return false;
        if (hitTransform.GetComponentInParent<EntityInfo>() != null)
            return false;
        if (collider is TerrainCollider || hitTransform.GetComponentInParent<Terrain>() != null)
            return true;

        return HasGroundNameHint(hitTransform);
    }

    private static bool HasGroundNameHint(Transform hitTransform)
    {
        for (Transform current = hitTransform; current != null; current = current.parent)
        {
            string name = current.name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            name = name.ToLowerInvariant();
            if (name.Contains("ground") ||
                name.Contains("floor") ||
                name.Contains("terrain") ||
                name.Contains("path") ||
                name.Contains("road") ||
                name.Contains("trail") ||
                name.Contains("walkable") ||
                name.Contains("pad") ||
                name.Contains("dirt") ||
                name.Contains("soil") ||
                name.Contains("grass") ||
                name.Contains("moss") ||
                name.Contains("stone"))
                return true;
        }

        return false;
    }

    private static void ApplyRarity(YQInvestorEnemy enemy, int seedOffset)
    {
        if (enemy == null)
            return;

        float roll = Mathf.Repeat(Random.value + seedOffset * 0.173f, 1f);
        if (roll > 0.985f)
            enemy.ApplyVariant("legendary", new Color(1f, 0.72f, 0.22f, 1f), 1.55f, 2.25f, 1.8f);
        else if (roll > 0.94f)
            enemy.ApplyVariant("epic", new Color(0.74f, 0.48f, 1f, 1f), 1.32f, 1.7f, 1.45f);
        else if (roll > 0.82f)
            enemy.ApplyVariant("rare", new Color(0.42f, 0.78f, 1f, 1f), 1.15f, 1.35f, 1.2f);
        else
            enemy.ApplyVariant("common", Color.white, 1f, 1f, 1f);
    }

    private GameObject TryCreateModel(Transform parent, string label)
    {
        string path = enemyPrefabPath;
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Application.isPlaying && !allowImportedPrefabModelsInPlay)
            return null;

#if UNITY_EDITOR
        GameObject prefab = LoadEditorPrefab(path);
        if (prefab == null)
            return null;

        GameObject model = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (model == null)
            return null;

        model.name = "Model_" + label;
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
        DisableModelPhysics(model);
        NormalizeModel(model, parent, path, label);
        ConfigureModelAnimation(model, path);
        YQRuntimeUrpMaterialRepair.RepairHierarchy(model);
        YQVisualStabilityDirector.StabilizeHierarchy(model);
        if (!EnsureRenderableMaterials(model, path, label))
        {
            DestroyUnityObject(model);
            return null;
        }
        return model;
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static void ConfigureModelAnimation(GameObject model, string prefabPath)
    {
        if (model == null)
            return;

        Animator[] animators = model.GetComponentsInChildren<Animator>(true);
        if (animators == null || animators.Length == 0)
            return;

        bool hasUsableAnimator = false;
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            EnsureAnimationEventReceiver(animator);
            animator.applyRootMotion = false;
            if (animator.runtimeAnimatorController == null)
            {
                animator.enabled = false;
                continue;
            }

            animator.enabled = true;
            hasUsableAnimator = true;
        }

        if (hasUsableAnimator)
            return;

        string controllerPath = ResolveDefaultControllerPath(prefabPath);
        if (string.IsNullOrWhiteSpace(controllerPath))
            return;

        RuntimeAnimatorController controller = LoadEditorController(controllerPath);
        if (controller == null)
            return;

        Animator target = FindBestAnimator(animators, model.transform);
        if (target == null)
            return;

        target.runtimeAnimatorController = controller;
        target.applyRootMotion = false;
        EnsureAnimationEventReceiver(target);
        target.enabled = true;
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

    private static Animator FindBestAnimator(Animator[] animators, Transform root)
    {
        Animator first = null;
        Animator firstWithAvatar = null;
        Animator rootAnimator = null;

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            first ??= animator;
            if (animator.transform == root)
                rootAnimator = animator;
            if (firstWithAvatar == null && animator.avatar != null)
                firstWithAvatar = animator;
        }

        if (rootAnimator != null)
            return rootAnimator;
        if (firstWithAvatar != null)
            return firstWithAvatar;
        return first;
    }

    private static string ResolveDefaultControllerPath(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
            return null;

        string normalized = prefabPath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("/spiders/"))
            return SpiderController;
        if (normalized.Contains("/plant monster/"))
            return PlantController;
        if (normalized.Contains("/mushroom monster/"))
            return MushroomController;
        if (normalized.Contains("/dragons/"))
            return DragonController;
        if (normalized.Contains("/demons/"))
            return DemonController;
        if (normalized.Contains("/human - humans/"))
            return HumanController;

        return null;
    }

    private static GameObject LoadEditorPrefab(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (s_editorPrefabCache.TryGetValue(normalized, out GameObject cached))
            return cached;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(normalized);
        s_editorPrefabCache[normalized] = prefab;
        return prefab;
    }

    private static RuntimeAnimatorController LoadEditorController(string path)
    {
        string normalized = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (s_editorControllerCache.TryGetValue(normalized, out RuntimeAnimatorController cached))
            return cached;

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(normalized);
        s_editorControllerCache[normalized] = controller;
        return controller;
    }
#endif

    private static void DisableModelPhysics(GameObject model)
    {
        if (model == null)
            return;

        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].isKinematic = true;
        }

        MonoBehaviour[] behaviours = model.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].GetType() != typeof(YQAnimationEventAudioReceiver))
                behaviours[i].enabled = false;
        }
    }

    private static void NormalizeModel(GameObject model, Transform parent, string prefabPath, string label)
    {
        if (model == null || parent == null || !TryGetBounds(model, out Bounds bounds))
            return;

        float height = Mathf.Max(0.1f, bounds.size.y);
        float scale = Mathf.Clamp(ResolveModelHeight(prefabPath, label) / height, 0.04f, 3.2f);
        model.transform.localScale *= scale;

        if (!TryGetBounds(model, out bounds))
            return;

        Vector3 targetBottomCenter = parent.position;
        Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.transform.position += targetBottomCenter - currentBottomCenter;
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
                bounds.Encapsulate(renderer.bounds);
        }

        return found;
    }

    private static bool EnsureRenderableMaterials(GameObject model, string prefabPath, string label)
    {
        Renderer[] renderers = model != null ? model.GetComponentsInChildren<Renderer>(true) : null;
        if (renderers == null || renderers.Length == 0)
            return false;

        bool foundRenderable = false;
        Material fallback = null;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled)
                continue;

            foundRenderable = true;
            Material[] materials = renderer.sharedMaterials;
            bool needsFallback = materials == null || materials.Length == 0;
            if (!needsFallback)
            {
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null || material.shader == null || material.shader.name.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        needsFallback = true;
                        break;
                    }
                }
            }

            if (!needsFallback)
                continue;

            fallback ??= CreateFallbackMaterial(prefabPath, label);
            renderer.sharedMaterial = fallback;
        }

        return foundRenderable;
    }

    private static Material CreateFallbackMaterial(string prefabPath, string label)
    {
#if UNITY_EDITOR
        string cacheKey = ((prefabPath ?? string.Empty) + "|" + (label ?? string.Empty)).Replace('\\', '/');
        if (s_fallbackMaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            return cached;
#endif

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Color color = ResolveFallbackColor(prefabPath, label);
        Material material = new Material(shader)
        {
            name = "YQ_EnemyFallback_" + SanitizeMaterialName(label),
            hideFlags = HideFlags.DontSave
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        material.DisableKeyword("_EMISSION");
#if UNITY_EDITOR
        s_fallbackMaterialCache[cacheKey] = material;
#endif
        return material;
    }

    private static Color ResolveFallbackColor(string prefabPath, string label)
    {
        string text = ((prefabPath ?? string.Empty) + " " + (label ?? string.Empty)).ToLowerInvariant();
        if (text.Contains("frost") || text.Contains("ice"))
            return new Color(0.54f, 0.78f, 0.90f, 1f);
        if (text.Contains("ember") || text.Contains("cinder") || text.Contains("fire") || text.Contains("demon"))
            return new Color(0.72f, 0.28f, 0.18f, 1f);
        if (text.Contains("root") || text.Contains("thorn") || text.Contains("plant") || text.Contains("spore"))
            return new Color(0.30f, 0.56f, 0.26f, 1f);
        if (text.Contains("tide") || text.Contains("water") || text.Contains("brine"))
            return new Color(0.24f, 0.54f, 0.62f, 1f);

        return new Color(0.43f, 0.39f, 0.33f, 1f);
    }

    private static string SanitizeMaterialName(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "Enemy";

        char[] chars = label.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static float ResolveModelHeight(string prefabPath, string label)
    {
        string text = ((prefabPath ?? string.Empty) + " " + (label ?? string.Empty)).Replace('\\', '/').ToLowerInvariant();
        if (text.Contains("dragon") || text.Contains("drake"))
            return 3.0f;
        if (text.Contains("demon"))
            return 2.25f;
        if (text.Contains("plant monster") || text.Contains("stalker"))
            return 2.05f;
        if (text.Contains("human") || text.Contains("bandit"))
            return 1.85f;
        if (text.Contains("mushroom") || text.Contains("spore"))
            return 1.45f;
        if (text.Contains("spider"))
            return 1.15f;

        return 1.55f;
    }
}
