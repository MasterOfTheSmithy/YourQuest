using System;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-1200)]
public sealed class YQActiveQuestWorldHighlight : MonoBehaviour
{
    public Color glowColor = new Color(1f, 0.82f, 0.24f, 0.34f);
    public float refreshInterval = 1.25f;
    public float followSharpness = 7f;
    public float groundLift = 0.075f;
    public float markerDiameter = 3.2f;
    public float lightRange = 5.5f;
    public float lightIntensity = 1.25f;

    private GameObject _markerRoot;
    private Light _light;
    private Renderer _discRenderer;
    private Material _discMaterial;
    private Vector3 _targetPosition;
    private float _nextRefreshTime;
    private float _nextFullTargetScanTime;
    private bool _hasTarget;
    private bool _usingPlayerFallback;
    private Transform _player;
    private Transform _resolvedTarget;
    private string _resolvedQuestKey = string.Empty;
    private const float FullTargetRescanInterval = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQActiveQuestWorldHighlight>() != null)
            return;

        GameObject go = new GameObject("00__YQ_ActiveQuestWorldHighlight");
        DontDestroyOnLoad(go);
        go.AddComponent<YQActiveQuestWorldHighlight>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        BuildMarker();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
            _hasTarget = TryResolveTargetPosition(out _targetPosition);
            SetVisible(_hasTarget);
        }

        if (!_hasTarget || _markerRoot == null)
            return;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        _markerRoot.transform.position = Vector3.Lerp(_markerRoot.transform.position, _targetPosition, t);
    }

    private void BuildMarker()
    {
        _markerRoot = new GameObject("ActiveQuest_GlowMarker");
        _markerRoot.transform.SetParent(transform, false);

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "SoftYellowQuestDisc";
        disc.transform.SetParent(_markerRoot.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(markerDiameter, 0.012f, markerDiameter);
        Collider collider = disc.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        _discRenderer = disc.GetComponent<Renderer>();
        _discMaterial = CreateGlowMaterial(glowColor);
        if (_discRenderer != null)
        {
            _discRenderer.sharedMaterial = _discMaterial;
            _discRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _discRenderer.receiveShadows = false;
        }

        GameObject lightGo = new GameObject("SoftQuestGlowLight");
        lightGo.transform.SetParent(_markerRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = new Color(1f, 0.82f, 0.28f, 1f);
        _light.intensity = lightIntensity;
        _light.range = lightRange;
        _light.shadows = LightShadows.None;

        SetVisible(false);
    }

    private bool TryResolveTargetPosition(out Vector3 position)
    {
        position = Vector3.zero;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        QuestRecord quest = state != null ? state.GetActiveQuest() : null;
        if (quest == null)
            return false;

        string questKey =
            (quest.questId ?? quest.name ?? string.Empty) +
            "|" + quest.updatedUnix;
        bool sameObjectiveState = string.Equals(
            questKey,
            _resolvedQuestKey,
            StringComparison.Ordinal);
        if (sameObjectiveState &&
            Time.unscaledTime < _nextFullTargetScanTime)
        {
            if (_resolvedTarget != null &&
                _resolvedTarget.gameObject.activeInHierarchy)
            {
                // note: Quest identity did not change, so follow the cached scene target instead of rebuilding normalized strings and scanning every entity.
                position = ProjectToGround(_resolvedTarget.position);
                return true;
            }

            ResolvePlayerPosition();
            if (_usingPlayerFallback && _player != null)
            {
                position = ProjectToGround(
                    _player.position + _player.forward * 8f);
                return true;
            }
        }

        string query = BuildQuestQuery(quest);
        float bestScore = float.MinValue;
        Transform best = null;
        Vector3 playerPosition = ResolvePlayerPosition();

        EntityInfo[] entities = FindObjectsByType<EntityInfo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < entities.Length; i++)
        {
            EntityInfo info = entities[i];
            if (info == null)
                continue;

            float score = ScoreEntity(info, query, playerPosition);
            if (score > bestScore)
            {
                bestScore = score;
                best = info.transform;
            }
        }

        RegionVolume[] regions = FindObjectsByType<RegionVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < regions.Length; i++)
        {
            RegionVolume region = regions[i];
            if (region == null)
                continue;

            float score = ScoreRegion(region, query, playerPosition);
            if (score > bestScore)
            {
                bestScore = score;
                best = region.transform;
            }
        }

        if (best != null && bestScore >= 12f)
        {
            _resolvedQuestKey = questKey;
            _resolvedTarget = best;
            _usingPlayerFallback = false;
            _nextFullTargetScanTime =
                Time.unscaledTime + FullTargetRescanInterval;
            position = ProjectToGround(best.position);
            return true;
        }

        if (_player == null)
            return false;

        _resolvedQuestKey = questKey;
        _resolvedTarget = null;
        _usingPlayerFallback = true;
        _nextFullTargetScanTime =
            Time.unscaledTime + FullTargetRescanInterval;
        position = ProjectToGround(_player.position + _player.forward * 8f);
        return true;
    }

    private Vector3 ResolvePlayerPosition()
    {
        if (_player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                _player = playerObject.transform;
        }

        return _player != null ? _player.position : Vector3.zero;
    }

    private static string BuildQuestQuery(QuestRecord quest)
    {
        string tags = quest.tags != null ? string.Join(" ", quest.tags) : string.Empty;
        return Normalize((quest.name ?? string.Empty) + " " + (quest.description ?? string.Empty) + " " + tags);
    }

    private static float ScoreEntity(EntityInfo info, string query, Vector3 playerPosition)
    {
        string haystack = Normalize(info.displayName + " " + info.entityId + " " + info.factionId + " " + (info.tags != null ? string.Join(" ", info.tags) : string.Empty));
        float score = CountSharedTokens(query, haystack) * 10f;
        bool hasDialogueAgent = info.GetComponent<NpcDialogueAgent>() != null || info.GetComponentInParent<NpcDialogueAgent>() != null;

        if (query.Contains("talk") || query.Contains("speak") || query.Contains("ask"))
        {
            if (hasDialogueAgent && info.hostility != Hostility.Hostile)
                score += 16f;
            else
                score -= 18f;
            if (haystack.Contains("archivist") || haystack.Contains("warden") || haystack.Contains("guide"))
                score += 22f;
        }

        if (query.Contains("defeat") || query.Contains("hostile") || query.Contains("combat") || query.Contains("echo") || query.Contains("ember"))
        {
            if (info.hostility == Hostility.Hostile)
                score += 28f;
        }

        if (query.Contains("shrine") && haystack.Contains("shrine"))
            score += 45f;

        if (info.isNotable)
            score += 4f;

        return score - DistancePenalty(info.transform.position, playerPosition);
    }

    private static float ScoreRegion(RegionVolume region, string query, Vector3 playerPosition)
    {
        string tags = region.tags != null ? string.Join(" ", region.tags) : string.Empty;
        string haystack = Normalize(region.regionId + " " + region.regionName + " " + tags + " " + region.gameObject.name);
        float score = CountSharedTokens(query, haystack) * 8f;
        if (query.Contains("ember") && haystack.Contains("ember"))
            score += 30f;
        if (query.Contains("vault") && haystack.Contains("vault"))
            score += 30f;
        if ((query.Contains("archive") || query.Contains("archivist")) && haystack.Contains("hub"))
            score += 18f;
        if (query.Contains("shrine") && haystack.Contains("shrine"))
            score += 30f;
        return score - DistancePenalty(region.transform.position, playerPosition) * 0.5f;
    }

    private static float DistancePenalty(Vector3 worldPosition, Vector3 playerPosition)
    {
        if (playerPosition == Vector3.zero)
            return 0f;
        return Vector3.Distance(playerPosition, worldPosition) * 0.08f;
    }

    private Vector3 ProjectToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 8f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * groundLift;

        position.y = groundLift;
        return position;
    }

    private void SetVisible(bool visible)
    {
        if (_markerRoot != null && _markerRoot.activeSelf != visible)
            _markerRoot.SetActive(visible);
        if (_light != null)
            _light.enabled = visible;
    }

    private static Material CreateGlowMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = "YQ_ActiveQuest_Glow";
        SetMaterialColor(material, color);
        ConfigureTransparent(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureTransparent(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = ' ';
        }
        return new string(chars);
    }

    private static int CountSharedTokens(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 0;

        string[] tokens = left.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int shared = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Length < 4)
                continue;
            if (right.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                shared++;
        }

        return shared;
    }
}
