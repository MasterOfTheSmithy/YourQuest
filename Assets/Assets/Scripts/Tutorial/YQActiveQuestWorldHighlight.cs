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
    private Transform _resolvedTarget;
    private string _resolvedQuestKey = string.Empty;
    private const float FullTargetRescanInterval = 5f;

    public bool TryGetCurrentTargetPosition(
        out Vector3 position)
    {
        position =
            _targetPosition;

        // note: The minimap consumes the already-resolved quest target instead of repeating the highlight system's scene scans.
        return
            _hasTarget;
    }

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
        QuestObjectiveRecord objective = GetTrackedObjective(quest);
        if (objective == null || string.IsNullOrWhiteSpace(objective.targetId))
            return false;

        string questKey =
            state.playerId + "|" + quest.questId + "|" + objective.type + "|" + objective.targetId;
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

            // note: Negative lookup caching prevents missing/streamed-out targets from forcing a scene scan every refresh.
            return false;
        }

        Transform best = null;
        bool regionTarget = string.Equals(objective.type, "enter_region", StringComparison.OrdinalIgnoreCase);
        // note: Mark the actual persisted objective target, never a scene object whose name happens to resemble quest prose.
        if (!regionTarget)
        {
            EntityInfo[] entities = FindObjectsByType<EntityInfo>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < entities.Length; i++)
            {
                EntityInfo info = entities[i];
                if (info != null && string.Equals(info.entityId, objective.targetId, StringComparison.OrdinalIgnoreCase))
                {
                    best = info.transform;
                    break;
                }
            }
        }
        else
        {
            RegionVolume[] regions = FindObjectsByType<RegionVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < regions.Length; i++)
            {
                RegionVolume region = regions[i];
                if (region != null && string.Equals(region.regionId, objective.targetId, StringComparison.OrdinalIgnoreCase))
                {
                    best = region.transform;
                    break;
                }
            }
        }

        _resolvedQuestKey = questKey;
        _resolvedTarget = best;
        _nextFullTargetScanTime = Time.unscaledTime + FullTargetRescanInterval;
        if (best == null)
            return false;
        position = ProjectToGround(best.position);
        return true;
    }

    public static QuestObjectiveRecord GetTrackedObjective(QuestRecord quest)
    {
        // note: A non-spatial current step (equip, cast, wait) should not point toward a later NPC or an invented spot in front of the player.
        if (quest?.objectives == null)
            return null;
        foreach (QuestObjectiveRecord objective in quest.objectives)
            if (objective != null && !objective.completed)
                return objective;
        return null;
    }


    private Vector3 ProjectToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 8f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * groundLift;

        // note: Unloaded ground must not relocate an otherwise valid high-altitude target to sea level.
        position.y += groundLift;
        return position;
    }

    private void OnDestroy()
    {
        // note: The glow material is runtime-owned and must be released with its marker.
        if (_discMaterial != null)
            Destroy(_discMaterial);
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
