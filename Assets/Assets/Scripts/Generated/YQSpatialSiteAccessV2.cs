using System;
using System.Collections.Generic;
using UnityEngine;

// note: A prepared query validates/indexes water once so placement attempts do not repeatedly scan and allocate across the full blueprint.
public sealed class YQSpatialSiteAccessQueryV2
{
    private readonly float worldSize;
    private readonly List<YQHydrologyFeatureV2> waters;

    internal YQSpatialSiteAccessQueryV2(
        float worldSize,
        List<YQHydrologyFeatureV2> waters)
    {
        this.worldSize = worldSize;
        this.waters = waters;
    }

    public bool TryMeasure(
        string siteId,
        float siteX,
        float siteZ,
        float reservedRadius,
        out float access,
        out float nearestBankDistance,
        out string nearestId,
        out bool centerInsideWater,
        out string failure)
    {
        access = 0f;
        nearestBankDistance = 0f;
        nearestId = string.Empty;
        centerInsideWater = false;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(siteId) ||
            !YQSpatialSiteAccessV2.IsFinite(siteX) ||
            !YQSpatialSiteAccessV2.IsFinite(siteZ) ||
            !YQSpatialSiteAccessV2.IsFinite(reservedRadius) ||
            reservedRadius <= 0f ||
            !YQSpatialSiteAccessV2.IsFinite(reservedRadius * 2.5f) ||
            Mathf.Abs(siteX) > worldSize * 0.5f ||
            Mathf.Abs(siteZ) > worldSize * 0.5f)
        {
            failure = "Water access requires finite in-bounds site geometry and a positive reserve radius.";
            return false;
        }

        double nearest = double.MaxValue;
        for (int index = 0; index < waters.Count; index++)
        {
            MeasureGeometry(waters[index], siteX, siteZ,
                out double bankDistance,
                out double centerlineDistance,
                out double halfWidth);
            if (centerlineDistance + 0.001d < halfWidth)
                centerInsideWater = true;
            if (bankDistance >= nearest)
                continue;
            nearest = bankDistance;
            nearestId = waters[index].hydrologyId;
        }

        if (nearest == double.MaxValue)
            return true;
        if (nearest > float.MaxValue)
        {
            failure = "Water access distance exceeds the supported numeric range.";
            return false;
        }

        nearestBankDistance = (float)nearest;
        access = 1f - Mathf.Clamp01(
            nearestBankDistance /
            Mathf.Max(24f, reservedRadius * 2.5f));
        return true;
    }

    private static void MeasureGeometry(
        YQHydrologyFeatureV2 water,
        float x,
        float z,
        out double bankDistance,
        out double centerlineDistance,
        out double halfWidth)
    {
        YQBlueprintPointV2 first = water.controlPoints[0];
        double dx = (double)x - first.x;
        double dz = (double)z - first.z;
        double bestSquared = dx * dx + dz * dz;
        double nearestWidth = first.width;
        for (int index = 1; index < water.controlPoints.Count; index++)
        {
            YQBlueprintPointV2 a = water.controlPoints[index - 1];
            YQBlueprintPointV2 b = water.controlPoints[index];
            double segmentX = (double)b.x - a.x;
            double segmentZ = (double)b.z - a.z;
            double lengthSquared = segmentX * segmentX + segmentZ * segmentZ;
            double t = lengthSquared > 0.0001d
                ? Math.Max(0d, Math.Min(1d,
                    (((double)x - a.x) * segmentX +
                     ((double)z - a.z) * segmentZ) / lengthSquared))
                : 0d;
            dx = x - (a.x + segmentX * t);
            dz = z - (a.z + segmentZ * t);
            double squared = dx * dx + dz * dz;
            if (squared >= bestSquared)
                continue;
            bestSquared = squared;
            // note: Match the terrain's nearest-centerline width interpolation instead of assuming nominal width everywhere.
            nearestWidth = a.width + ((double)b.width - a.width) * t;
        }

        centerlineDistance = Math.Sqrt(bestSquared);
        halfWidth = Math.Max(water.nominalWidth, nearestWidth) * 0.5d;
        bankDistance = Math.Max(0d, centerlineDistance - halfWidth);
    }
}

// note: Planning, acceptance, and runtime placement share this pure query; it neither repairs nor accepts persisted world data.
public static class YQSpatialSiteAccessV2
{
    public const int MaximumWaterFeatures = 512;
    public const int MaximumPointsPerFeature = 256;
    public const int MaximumRelationships = 4096;

    public static bool TryCreateQuery(
        YQSpatialBlueprintV2 blueprint,
        out YQSpatialSiteAccessQueryV2 query,
        out string failure)
    {
        query = null;
        failure = string.Empty;
        if (blueprint == null || !IsFinite(blueprint.worldSize) ||
            blueprint.worldSize < 256f || blueprint.hydrology == null ||
            blueprint.relationships == null ||
            blueprint.hydrology.Count > MaximumWaterFeatures ||
            blueprint.relationships.Count > MaximumRelationships)
        {
            failure = "Water access requires a finite world and collections within the 512-water/4096-relationship query limits.";
            return false;
        }

        HashSet<string> waterIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<YQHydrologyFeatureV2> waters =
            new List<YQHydrologyFeatureV2>(blueprint.hydrology.Count);
        float halfExtent = blueprint.worldSize * 0.5f;
        for (int waterIndex = 0;
             waterIndex < blueprint.hydrology.Count;
             waterIndex++)
        {
            YQHydrologyFeatureV2 water = blueprint.hydrology[waterIndex];
            int minimumPoints = water != null &&
                                water.kind == YQHydrologyKindV2.Lake ? 1 : 2;
            if (water == null ||
                string.IsNullOrWhiteSpace(water.hydrologyId) ||
                !waterIds.Add(water.hydrologyId) ||
                water.kind < YQHydrologyKindV2.River ||
                water.kind > YQHydrologyKindV2.Waterfall ||
                !IsFinite(water.nominalWidth) || water.nominalWidth <= 0f ||
                !IsFinite(water.nominalDepth) || water.nominalDepth <= 0f ||
                !IsFinite(water.waterLevelNormalized) ||
                water.waterLevelNormalized < 0f ||
                water.waterLevelNormalized > 1f ||
                water.controlPoints == null ||
                water.controlPoints.Count < minimumPoints ||
                water.controlPoints.Count > MaximumPointsPerFeature)
            {
                failure = "Water access found invalid/duplicate hydrology or a feature outside the 256-point limit.";
                return false;
            }

            for (int pointIndex = 0;
                 pointIndex < water.controlPoints.Count;
                 pointIndex++)
            {
                YQBlueprintPointV2 point = water.controlPoints[pointIndex];
                if (point == null ||
                    !IsFinite(point.x) || !IsFinite(point.z) ||
                    Mathf.Abs(point.x) > halfExtent ||
                    Mathf.Abs(point.z) > halfExtent ||
                    !IsFinite(point.width) || point.width < 0f ||
                    !IsFinite(point.normalizedElevation) ||
                    point.normalizedElevation < 0f ||
                    point.normalizedElevation > 1f)
                {
                    failure = "Water access found malformed control points in " +
                              water.hydrologyId + ".";
                    return false;
                }
            }
            waters.Add(water);
        }

        // note: Relationships are validated by the blueprint gate, but cannot manufacture water access without matching bank geometry.
        query = new YQSpatialSiteAccessQueryV2(
            blueprint.worldSize, waters);
        return true;
    }

    public static bool TryMeasureWaterAccess(
        YQSpatialBlueprintV2 blueprint,
        YQSiteAnchorV2 site,
        out float access,
        out float nearestDistance,
        out string nearestId,
        out string failure)
    {
        access = 0f;
        nearestDistance = 0f;
        nearestId = string.Empty;
        failure = string.Empty;
        if (site == null || !IsFinite(site.minimumWaterAccess) ||
            site.minimumWaterAccess < 0f ||
            site.minimumWaterAccess > 1f)
        {
            failure = "Water access requires a site with a finite normalized requirement.";
            return false;
        }
        return TryMeasureWaterAccess(
            blueprint, site.siteId, site.x, site.z, site.reservedRadius,
            out access, out nearestDistance, out nearestId, out failure);
    }

    // note: The convenience path prepares once per independent call; hot placement loops retain and reuse the prepared query directly.
    public static bool TryMeasureWaterAccess(
        YQSpatialBlueprintV2 blueprint,
        string siteId,
        float siteX,
        float siteZ,
        float reservedRadius,
        out float access,
        out float nearestDistance,
        out string nearestId,
        out string failure)
    {
        access = 0f;
        nearestDistance = 0f;
        nearestId = string.Empty;
        if (!TryCreateQuery(
                blueprint,
                out YQSpatialSiteAccessQueryV2 query,
                out failure))
        {
            return false;
        }
        return query.TryMeasure(
            siteId, siteX, siteZ, reservedRadius,
            out access, out nearestDistance, out nearestId,
            out _, out failure);
    }

    internal static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
