using System;
using UnityEngine;

public enum YQCellLootPurposeV2 { HouseholdStorage = 0, EncounterCache = 1 }

[Serializable]
public sealed class YQCellLootBindingV2
{
    public string bindingId = string.Empty;
    public string targetPath = string.Empty;
    public string sourceSignature = string.Empty;
    public string sourcePrefabGuid = string.Empty;
    public YQSemanticSiteReviewState reviewState = YQSemanticSiteReviewState.Pending;
    public YQCellLootPurposeV2 purpose;
    public Vector3 sourceLocalPosition;
    public Quaternion sourceLocalRotation = Quaternion.identity;
    public Vector3 sourceLocalScale = Vector3.one;
    public Vector3 colliderCenter;
    public Vector3 colliderSize;
    public YQAssetSocketRecordV2 accessSocket;
}

public static class YQCellLootBindingsV2
{
    public static string BuildLootId(string locationId, string cellId, string bindingId)
    {
        // note: Length-prefixed identity gives each physical generated storage slot independent persisted consumption and reward seeds.
        return "loot:v2:" + Part(locationId) + Part(cellId) + Part(bindingId);
    }

    public static bool TryBind(Transform cell, YQCellLootBindingV2 binding, string sourceSignature,
        string locationId, string cellId, string generatedLabel, string regionId, int threatTier, out string failure)
    {
        failure = string.Empty;
        if (cell == null || cell.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(locationId) ||
            string.IsNullOrWhiteSpace(cellId) || string.IsNullOrWhiteSpace(generatedLabel) || string.IsNullOrWhiteSpace(regionId))
        {
            failure = "Reviewed storage requires inactive staging and generated site identity.";
            return false;
        }
        if (!TryResolve(cell, binding, sourceSignature, out Transform target, out failure)) return false;
        string id = BuildLootId(locationId, cellId, binding.bindingId);
        YQLockpickableLoot existing = target.GetComponent<YQLockpickableLoot>();
        if (existing != null)
        {
            // note: Streaming retries keep the existing owner and never reset its consumption state or overwrite authored containers.
            if (existing.persistentLootId == id && existing.PreservesReviewedCollider) return true;
            failure = "Storage target already has a different interaction owner.";
            return false;
        }
        if (target.GetComponentInChildren<YQLockpickableLoot>(true) != null ||
            (target.parent != null && target.parent.GetComponentInParent<YQLockpickableLoot>() != null) ||
            target.GetComponentInChildren<YQLockpickableDoor>(true) != null ||
            target.GetComponentInParent<YQLockpickableDoor>() != null)
        {
            failure = "Storage overlaps another interaction owner.";
            return false;
        }
        // note: Mechanical rewards derive from the persisted site threat and stable slot ID; generated identity/region remain supplied by the accepted world record.
        int tier = Mathf.Clamp(threatTier, 1, 12);
        uint hash = 2166136261;
        unchecked { foreach (char character in id) { hash ^= character; hash *= 16777619; } }
        bool encounter = binding.purpose == YQCellLootPurposeV2.EncounterCache;
        int gold = encounter ? tier * 12 + (int)(hash % 9) : 2 + (int)(hash % 7);
        var loot = target.gameObject.AddComponent<YQLockpickableLoot>();
        loot.ConfigureReviewedGeneratedLoot(id, regionId, generatedLabel, gold,
            encounter && (hash & 1) != 0, encounter ? Mathf.Clamp01(0.18f + tier * 0.05f) : 0f,
            encounter ? tier : 1);
        return true;
    }

    private static bool TryResolve(Transform root, YQCellLootBindingV2 binding, string signature,
        out Transform target, out string failure)
    {
        target = null;
        failure = "Storage lacks current reviewed geometry and access evidence.";
        if (!HasReviewedEvidence(binding, signature) ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(root, binding.targetPath, out target) || target == root)
            return false;
        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (!target.gameObject.activeSelf || collider == null || !collider.enabled || collider.isTrigger ||
            target.GetComponentsInChildren<Collider>(true).Length != 1 ||
            target.GetComponentInChildren<Renderer>(true) == null ||
            (target.localPosition - binding.sourceLocalPosition).sqrMagnitude > 0.0001f ||
            (target.localScale - binding.sourceLocalScale).sqrMagnitude > 0.0001f ||
            Quaternion.Angle(target.localRotation, binding.sourceLocalRotation) > 0.1f ||
            (collider.center - binding.colliderCenter).sqrMagnitude > 0.0001f ||
            (collider.size - binding.colliderSize).sqrMagnitude > 0.0001f)
            return false;
        YQAssetSocketRecordV2 socket = binding.accessSocket;
        if (!YQCellDoorBindingsV2.TryResolveUniquePath(root, socket.transformPath, out Transform access) || access == root ||
            (access.position - root.TransformPoint(socket.localPosition)).sqrMagnitude > 0.0001f ||
            Quaternion.Angle(access.rotation, root.rotation * socket.localRotation) > 0.1f)
            return false;
        // note: A path may still exist beneath a disabled variant; all ancestors inside the staged cell must be authored active.
        for (Transform node = target; node != null && node != root; node = node.parent)
            if (!node.gameObject.activeSelf) return false;
        for (Transform node = access; node != null && node != root; node = node.parent)
            if (!node.gameObject.activeSelf) return false;
        if (Vector3.Distance(root.InverseTransformPoint(target.TransformPoint(collider.center)), socket.localPosition) > 2.5f)
            return false;
        failure = string.Empty;
        return true;
    }

    public static bool HasReviewedEvidence(YQCellLootBindingV2 binding, string signature)
    {
        // note: Function selection and actual provider installation share the same typed/source evidence requirements.
        if (binding == null || binding.reviewState != YQSemanticSiteReviewState.Approved ||
            string.IsNullOrWhiteSpace(binding.bindingId) || string.IsNullOrWhiteSpace(binding.sourcePrefabGuid) ||
            string.IsNullOrWhiteSpace(binding.targetPath) ||
            string.IsNullOrWhiteSpace(signature) || !string.Equals(binding.sourceSignature, signature, StringComparison.Ordinal) ||
            !Enum.IsDefined(typeof(YQCellLootPurposeV2), binding.purpose) ||
            !Finite(binding.sourceLocalPosition) || !Positive(binding.sourceLocalScale) ||
            !Rotation(binding.sourceLocalRotation) || !Finite(binding.colliderCenter) || !Positive(binding.colliderSize))
            return false;
        YQAssetSocketRecordV2 socket = binding.accessSocket;
        if (socket == null || socket.kind != YQAssetSocketKindV2.Interaction || string.IsNullOrWhiteSpace(socket.socketId) ||
            string.IsNullOrWhiteSpace(socket.transformPath) || !Positive(socket.clearanceSize) || socket.clearanceSize.x < 0.6f || socket.clearanceSize.y < 1.8f ||
            socket.clearanceSize.z < 0.6f || !Finite(socket.localPosition) || !Rotation(socket.localRotation))
            return false;
        return true;
    }

    private static string Part(string value) { value ??= string.Empty; return value.Length + ":" + value; }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Positive(Vector3 value) => Finite(value) && value.x > 0 && value.y > 0 && value.z > 0;
    private static bool Rotation(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w) &&
        Mathf.Abs(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w - 1f) < 0.01f;
}
