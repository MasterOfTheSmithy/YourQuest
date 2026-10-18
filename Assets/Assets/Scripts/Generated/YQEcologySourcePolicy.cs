using System;
using System.IO;
using UnityEngine;

public enum YQSmallFoliageKind { None, Understory, Shrub }

// note: Collider exemptions describe a measured, nonblocking role, never an entire vegetation pack.
public static class YQEcologySourcePolicy
{
    public const string PermanentFailurePrefix = "ecology source unavailable:";

    public static YQSmallFoliageKind ClassifyPath(string path)
    {
        string leaf = Path.GetFileNameWithoutExtension((path ?? string.Empty).Replace('\\', '/')).ToLowerInvariant();
        if (Contains(leaf, "tree", "palm", "trunk", "log", "rock", "wall", "hedge", "interior", "root", "thatch", "roof", "floor"))
            return YQSmallFoliageKind.None;
        if (Contains(leaf, "bush", "shrub", "heather", "gorse")) return YQSmallFoliageKind.Shrub;
        if (Contains(leaf, "fern", "grass", "nettle", "clover", "weed")) return YQSmallFoliageKind.Understory;
        return YQSmallFoliageKind.None;
    }

    public static bool TryClassifySmallFoliage(YQSpatialAssetRecord record, out YQSmallFoliageKind kind)
    {
        kind = YQSmallFoliageKind.None;
        if (record == null || !string.Equals(record.semanticRole, YQWorldAssetCatalog.SlotVegetation, StringComparison.OrdinalIgnoreCase) ||
            (record.compositionScale != YQSpatialCompositionScale.Atom && record.compositionScale != YQSpatialCompositionScale.Prop))
            return false;
        Vector3 size = record.localBoundsSize;
        if (!PositiveFinite(size.x) || !PositiveFinite(size.y) || !PositiveFinite(size.z) ||
            size.x > 6f || size.y > 4f || size.z > 6f) return false;
        kind = ClassifyPath(record.assetPath);
        return kind != YQSmallFoliageKind.None;
    }

    public static bool HasValidMeasuredSource(YQSpatialAssetRecord record)
    {
        // note: Collision relaxation never bypasses identity, mesh, shader, script or review failures.
        return record != null && !string.IsNullOrWhiteSpace(record.sourceGuid) &&
            record.stableAssetId == "asset_" + record.sourceGuid && !string.IsNullOrWhiteSpace(record.kitId) &&
            record.hasRenderer && record.rendererCount > 0 && record.materialSlotCount > 0 &&
            record.missingScriptCount == 0 && record.invalidMaterialSlotCount == 0 && record.materialReviewSlotCount == 0 &&
            record.validationIssues != null && record.validationIssues.Count == 0 &&
            PositiveFinite(record.localBoundsSize.x) && PositiveFinite(record.localBoundsSize.y) && PositiveFinite(record.localBoundsSize.z);
    }

    public static bool AllowsCollisionProfile(YQSpatialAssetRecord record, bool blocksNavigation)
    {
        // note: Trees, rocks, structures and any explicitly blocking reference retain their collider requirement.
        return record != null && (record.hasCollider || (!blocksNavigation && TryClassifySmallFoliage(record, out _)));
    }

    private static bool PositiveFinite(float value) => value > 0.001f && !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Contains(string value, params string[] terms)
    {
        foreach (string term in terms) if (value.IndexOf(term, StringComparison.Ordinal) >= 0) return true;
        return false;
    }
}
