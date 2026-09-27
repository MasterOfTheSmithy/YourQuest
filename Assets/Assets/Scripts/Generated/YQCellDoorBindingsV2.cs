using System;
using UnityEngine;

[Serializable]
public sealed class YQCellDoorBindingV2
{
    public string bindingId = string.Empty;
    public string targetPath = string.Empty;
    public string sourcePrefabGuid = string.Empty;
    public YQSemanticSiteReviewState reviewState = YQSemanticSiteReviewState.Pending;
    public string reviewNote = string.Empty;
    public bool passageVerified;
    public Vector3 sourceLocalPosition;
    public Quaternion sourceLocalRotation = Quaternion.identity;
    public Vector3 sourceLocalScale = Vector3.one;
    public Vector3 colliderCenter;
    public Vector3 colliderSize;
    public Vector3 openEuler;
    public bool initiallyLocked;
    public float lockDifficulty = 0.45f;
    // note: A reviewed door passage alone does not establish a route from its landing to generated terrain.
    public YQTerrainApproachContractV2 terrainApproach;
}

public static class YQCellDoorBindingsV2
{
    public static bool TryBind(Transform cellRoot, YQCellDoorBindingV2 binding,
        string locationId, string cellId, string displayName, string regionId, out string failure)
    {
        failure = string.Empty;
        // note: Inactive staging is mandatory: configuration must precede Awake, collider publication and any player interaction.
        if (cellRoot == null || cellRoot.gameObject.activeInHierarchy ||
            string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(cellId))
        {
            failure = "Door binding requires an inactive cell and stable location/cell IDs.";
            return false;
        }
        if (!TryResolveReviewedTarget(cellRoot, binding, out Transform target, out failure))
            return false;
        string doorId = BuildDoorId(locationId, cellId, binding.bindingId);
        YQLockpickableDoor existing = target.GetComponent<YQLockpickableDoor>();
        if (existing != null)
        {
            // note: Repeated streaming setup is idempotent; unrelated authored behaviours are never overwritten.
            if (existing.GeneratedDoorId == doorId)
                return true;
            failure = "Reviewed door target already has a different interaction owner.";
            return false;
        }
        if (target.GetComponentInChildren<YQLockpickableDoor>(true) != null ||
            (target.parent != null && target.parent.GetComponentInParent<YQLockpickableDoor>() != null))
        {
            failure = "Reviewed door overlaps another interaction owner.";
            return false;
        }
        // note: An approved movable leaf and its LOD renderers cannot retain the imported static-batching flag.
        target.gameObject.isStatic = false;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.gameObject.isStatic = false;
        target.GetComponent<BoxCollider>().enabled = true;
        YQLockpickableDoor door = target.gameObject.AddComponent<YQLockpickableDoor>();
        door.ConfigureGeneratedBinding(doorId, displayName, regionId,
            binding.initiallyLocked, binding.lockDifficulty, binding.openEuler);
        return true;
    }

    public static bool TryResolveReviewedTarget(Transform root, YQCellDoorBindingV2 binding,
        out Transform target, out string failure)
    {
        target = null;
        failure = string.Empty;
        if (binding == null || binding.reviewState != YQSemanticSiteReviewState.Approved ||
            !binding.passageVerified || string.IsNullOrWhiteSpace(binding.bindingId) ||
            string.IsNullOrWhiteSpace(binding.sourcePrefabGuid) || !Finite(binding.openEuler) ||
            Mathf.Abs(binding.openEuler.x) > 0.01f || Mathf.Abs(binding.openEuler.z) > 0.01f ||
            Mathf.Abs(binding.openEuler.y) < 15f || Mathf.Abs(binding.openEuler.y) > 170f ||
            !Finite(binding.lockDifficulty) || binding.lockDifficulty < 0f || binding.lockDifficulty > 1f ||
            !Finite(binding.sourceLocalPosition) || !Finite(binding.sourceLocalScale) ||
            binding.sourceLocalScale.x <= 0f || binding.sourceLocalScale.y <= 0f || binding.sourceLocalScale.z <= 0f ||
            !Finite(binding.colliderCenter) || !Finite(binding.colliderSize) ||
            binding.colliderSize.x <= 0f || binding.colliderSize.y <= 0f || binding.colliderSize.z <= 0f ||
            !ValidRotation(binding.sourceLocalRotation))
        {
            failure = "Door lacks reviewed passage, hinge, collider or source evidence.";
            return false;
        }
        if (!TryResolveUniquePath(root, binding.targetPath, out target))
        {
            failure = "Reviewed door path is missing or ambiguous: " + binding.targetPath;
            return false;
        }
        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (target == root || !target.gameObject.activeSelf || collider == null || collider.isTrigger ||
            target.GetComponentInChildren<Renderer>(true) == null ||
            (target.localPosition - binding.sourceLocalPosition).sqrMagnitude > 0.0001f ||
            (target.localScale - binding.sourceLocalScale).sqrMagnitude > 0.0001f ||
            Quaternion.Angle(target.localRotation, binding.sourceLocalRotation) > 0.1f ||
            (collider.center - binding.colliderCenter).sqrMagnitude > 0.0001f ||
            (collider.size - binding.colliderSize).sqrMagnitude > 0.0001f)
        {
            failure = "Door hierarchy or authored collider changed after review.";
            target = null;
            return false;
        }
        return true;
    }

    public static bool TryResolveUniquePath(Transform root, string path, out Transform target)
    {
        target = null;
        if (root == null || string.IsNullOrWhiteSpace(path))
            return false;
        Transform current = root;
        string[] segments = path.Split('/');
        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                return false;
            Transform match = null;
            for (int index = 0; index < current.childCount; index++)
            {
                Transform child = current.GetChild(index);
                if (!string.Equals(child.name, segment, StringComparison.Ordinal))
                    continue;
                if (match != null)
                    return false;
                match = child;
            }
            if (match == null)
                return false;
            current = match;
        }
        target = current;
        return true;
    }

    public static string BuildDoorId(string locationId, string cellId, string bindingId)
    {
        // note: Length-prefixed IDs avoid collisions when generated IDs themselves contain separators; each physical instance owns independent saved state.
        return "door:v2:" + Part(locationId) + Part(cellId) + Part(bindingId);
    }

    public static bool WasOpened(WorldState world, string doorId)
    {
        return world?.globalFlags != null && !string.IsNullOrWhiteSpace(doorId) &&
            world.globalFlags.TryGetValue(doorId, out float value) && value >= 1f;
    }

    public static void RecordOpened(WorldState world, string doorId)
    {
        if (world == null || string.IsNullOrWhiteSpace(doorId))
            return;
        world.EnsureCollections();
        world.globalFlags[doorId] = 1f;
        // note: Door state rides the existing world/profile autosave; interacting never adds a synchronous full-world serialization.
        world.TouchNow();
    }

    private static string Part(string value)
    {
        value ??= string.Empty;
        return value.Length + ":" + value;
    }

    private static bool ValidRotation(Quaternion value)
    {
        if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w))
            return false;
        float norm = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
        return Mathf.Abs(norm - 1f) < 0.01f;
    }

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
