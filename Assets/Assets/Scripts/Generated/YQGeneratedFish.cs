using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small deterministic fish instances used by generated rivers, lakes, and wetlands.
/// The component owns only presentation and the catch contract; inventory remains owned by PlayerState.
/// </summary>
[DisallowMultipleComponent]
public sealed class YQGeneratedFish : MonoBehaviour
{
    private Vector3[] _path;
    private float _speed;
    private float _pathLength;
    private float _progress;
    private bool _loop;
    private string _waterKind;
    private bool _caught;

    public static void SpawnForRiver(Transform parent, IReadOnlyList<Vector3> points, IReadOnlyList<float> widths, YQHydrologyKindV2 kind, string waterId)
    {
        if (parent == null || points == null || points.Count < 2 ||
            (kind != YQHydrologyKindV2.River && kind != YQHydrologyKindV2.Wetland))
            return;

        List<Vector3> sampledPoints = Downsample(points, 36);
        List<float> sampledWidths = DownsampleWidths(widths, points.Count, sampledPoints.Count);
        int fishCount = kind == YQHydrologyKindV2.River ? 3 : 2;
        // note: Emit one compact generation record so runtime verification can distinguish accepted fish dressing from an empty water pass.
        Debug.Log("[YQGeneratedFish] River school prepared: " + fishCount + " fish for " + (waterId ?? "unknown"));
        for (int index = 0; index < fishCount; index++)
        {
            float lane = (index - (fishCount - 1) * .5f) * .24f;
            Vector3[] path = BuildRiverPath(sampledPoints, sampledWidths, lane, index);
            CreateFish(parent, path, speed: kind == YQHydrologyKindV2.River ? 2.35f : .65f,
                loop: false, waterKind: kind.ToString(), seed: waterId + ":" + index);
        }
    }

    public static void SpawnForLake(Transform parent, YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin, YQHydrologyKindV2 kind, string waterId)
    {
        if (parent == null || (kind != YQHydrologyKindV2.Lake && kind != YQHydrologyKindV2.Wetland))
            return;

        int fishCount = kind == YQHydrologyKindV2.Lake ? 4 : 2;
        // note: Lake schools use fewer instances than terrain vegetation to keep water dressing cheap at runtime.
        Debug.Log("[YQGeneratedFish] Lake school prepared: " + fishCount + " fish for " + (waterId ?? "unknown"));
        for (int index = 0; index < fishCount; index++)
        {
            float radius = .24f + index * .08f;
            List<Vector3> path = new List<Vector3>(20);
            for (int sample = 0; sample < 20; sample++)
            {
                float angle = sample / 20f * Mathf.PI * 2f + index * .7f;
                Vector2 offset = basin.LongAxisXZ * (Mathf.Cos(angle) * basin.LongRadius * radius) +
                    basin.ShortAxisXZ * (Mathf.Sin(angle) * basin.ShortRadius * radius);
                path.Add(new Vector3(basin.CenterWorld.x + offset.x, basin.WaterSurfaceY - .42f - index * .03f,
                    basin.CenterWorld.z + offset.y));
            }
            CreateFish(parent, path.ToArray(), speed: kind == YQHydrologyKindV2.Lake ? .9f : .35f,
                loop: true, waterKind: kind.ToString(), seed: waterId + ":lake:" + index);
        }
    }

    private static YQGeneratedFish CreateFish(Transform parent, Vector3[] path, float speed, bool loop, string waterKind, string seed)
    {
        if (path == null || path.Length < 2)
            return null;

        // note: A tiny capsule is deliberately used as a low-fidelity fish proxy so the feature has no asset import or streaming cost.
        GameObject fishObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fishObject.name = "GeneratedFish_" + StableHash(seed).ToString("X8");
        fishObject.transform.SetParent(parent, true);
        fishObject.transform.position = path[0];
        fishObject.transform.localScale = new Vector3(.10f, .26f, .10f);
        Collider primitiveCollider = fishObject.GetComponent<Collider>();
        if (primitiveCollider != null)
            UnityEngine.Object.Destroy(primitiveCollider);
        BoxCollider interactionCollider = fishObject.AddComponent<BoxCollider>();
        interactionCollider.size = new Vector3(1.5f, .75f, 1.5f);
        YQInvestorRuntimeVisuals.SetRendererColor(fishObject.GetComponent<Renderer>(), new Color(.38f, .62f, .72f, 1f));

        YQGeneratedFish fish = fishObject.AddComponent<YQGeneratedFish>();
        fish.Initialize(path, speed, loop, waterKind, StableHash(seed));
        return fish;
    }

    private void Initialize(Vector3[] path, float speed, bool loop, string waterKind, int seed)
    {
        // note: Fish keep a bounded world-space path and advance along it without Rigidbody or per-frame allocations.
        _path = path;
        _speed = Mathf.Max(.05f, speed);
        _loop = loop;
        _waterKind = waterKind ?? "Water";
        _progress = Mathf.Abs(seed % 1000) / 1000f;
        _pathLength = 0f;
        for (int index = 1; index < _path.Length; index++)
            _pathLength += Vector3.Distance(_path[index - 1], _path[index]);
        if (_loop)
            _pathLength += Vector3.Distance(_path[_path.Length - 1], _path[0]);
        _pathLength = Mathf.Max(.1f, _pathLength);
    }

    private void Update()
    {
        if (_caught || _path == null || _path.Length < 2)
            return;

        // note: Movement is visual only; water remains the authoritative surface and no fish collider participates in navigation.
        // note: Advance in proportion to path length so river fish keep a stable world speed on short or long water segments.
        float normalizedTravel = Time.deltaTime * _speed / _pathLength;
        _progress = _loop ? Mathf.Repeat(_progress + normalizedTravel, 1f) : Mathf.PingPong(_progress + normalizedTravel, 1f);
        Vector3 position = EvaluatePath(_progress);
        position.y += Mathf.Sin(Time.time * 2.4f + GetInstanceID() * .013f) * .035f;
        Vector3 next = EvaluatePath(Mathf.Clamp01(_progress + .01f));
        Vector3 direction = next - position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        transform.position = position;
    }

    public bool TryCatch(GameObject collector)
    {
        if (_caught)
            return false;

        PlayerStateManager manager = PlayerStateManager.Instance;
        PlayerState state = manager != null ? manager.state : null;
        if (!HasFishingSkill(state))
        {
            GeneratedRpgContentService.Instance?.SetInventoryMessage("A fishing, foraging, or survival skill is required.");
            return false;
        }

        // note: Catching writes a normal generated-material item through the existing inventory authority and records a stable gameplay counter.
        InventoryItemRecord item = new InventoryItemRecord
        {
            itemId = Guid.NewGuid().ToString("N"),
            templateId = "generated_fish",
            displayName = _waterKind == nameof(YQHydrologyKindV2.River) ? "River Fish" : "Lake Fish",
            itemType = "material",
            rarity = "common",
            description = "A freshly caught fish from the generated " + _waterKind.ToLowerInvariant() + ".",
            quantity = 1,
            stackable = true,
            familyKey = "foraging:fishing",
            generatedAtUnixString = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };
        state.AddOrUpdateItem(item, true);
        state.IncCounter("foraging:fish_caught", 1f);
        state.AddLedgerLine("The player caught a fish.");
        manager.Save();
        GeneratedRpgContentService.Instance?.SetInventoryMessage("Caught " + item.displayName + ".");
        YQRuntimeAudioFeedback.PlayPickup(transform.position);
        _caught = true;
        gameObject.SetActive(false);
        return true;
    }

    public static bool HasFishingSkill(PlayerState state)
    {
        if (state == null || state.skills == null)
            return false;
        for (int index = 0; index < state.skills.Count; index++)
        {
            SkillRecord skill = state.skills[index];
            if (skill == null || !skill.unlocked || skill.isSpell)
                continue;
            string evidence = (skill.name + " " + skill.type + " " + skill.description + " " +
                skill.context + " " + skill.environment + " " + skill.payloadJson).ToLowerInvariant();
            if (evidence.Contains("fish") || evidence.Contains("forag") || evidence.Contains("gather") ||
                evidence.Contains("harvest") || evidence.Contains("surviv") || evidence.Contains("angler") ||
                evidence.Contains("nature") || evidence.Contains("waterman"))
                return true;
        }
        return false;
    }

    private Vector3 EvaluatePath(float normalized)
    {
        if (_path.Length == 2)
            return Vector3.Lerp(_path[0], _path[1], normalized);
        float targetDistance = normalized * _pathLength;
        int segmentCount = _loop ? _path.Length : _path.Length - 1;
        float traversed = 0f;
        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 from = _path[index];
            Vector3 to = index + 1 < _path.Length ? _path[index + 1] : _path[0];
            float length = Vector3.Distance(from, to);
            if (traversed + length >= targetDistance)
                return Vector3.Lerp(from, to, (targetDistance - traversed) / Mathf.Max(.001f, length));
            traversed += length;
        }
        return _path[_path.Length - 1];
    }

    private static List<Vector3> Downsample(IReadOnlyList<Vector3> source, int maximum)
    {
        List<Vector3> result = new List<Vector3>(Mathf.Min(source.Count, maximum));
        int stride = Mathf.Max(1, Mathf.CeilToInt(source.Count / (float)maximum));
        for (int index = 0; index < source.Count; index += stride)
            result.Add(source[index]);
        if (result[result.Count - 1] != source[source.Count - 1])
            result.Add(source[source.Count - 1]);
        return result;
    }

    private static List<float> DownsampleWidths(IReadOnlyList<float> source, int sourceCount, int targetCount)
    {
        List<float> result = new List<float>(targetCount);
        if (source == null || source.Count == 0)
        {
            for (int index = 0; index < targetCount; index++) result.Add(8f);
            return result;
        }
        int stride = Mathf.Max(1, Mathf.CeilToInt(sourceCount / (float)targetCount));
        for (int index = 0; index < targetCount; index++)
            result.Add(source[Mathf.Min(source.Count - 1, index * stride)]);
        return result;
    }

    private static Vector3[] BuildRiverPath(List<Vector3> points, List<float> widths, float lane, int fishIndex)
    {
        Vector3[] path = new Vector3[points.Count];
        for (int index = 0; index < points.Count; index++)
        {
            Vector3 tangent = points[Mathf.Min(points.Count - 1, index + 1)] - points[Mathf.Max(0, index - 1)];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            float lateral = lane * Mathf.Clamp(widths[Mathf.Min(widths.Count - 1, index)], 2f, 18f);
            path[index] = points[index] + side * lateral + Vector3.down * (.38f + fishIndex * .04f);
        }
        return path;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            for (int index = 0; !string.IsNullOrEmpty(value) && index < value.Length; index++)
                hash = hash * 31 + value[index];
            return hash;
        }
    }
}
