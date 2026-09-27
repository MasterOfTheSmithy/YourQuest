using System;
using System.Collections.Generic;
using UnityEngine;

// note: Immutable editor-review snapshot of existing blueprint reservations and explicitly supplied neighboring foundations.
public sealed class YQTerrainRepairProtectionV2
{
    private const int MaximumAreas = 512;
    private const int MaximumPoints = 513;
    private readonly Area[] areas;

    private readonly struct Area
    {
        public readonly string label;
        public readonly Vector2 a, b;
        public readonly float radius;
        public readonly Rect bounds;
        public readonly bool rectangle;

        public Area(string label, Vector2 a, Vector2 b, float radius)
        {
            this.label = label;
            this.a = a;
            this.b = b;
            this.radius = radius;
            bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - radius, Mathf.Min(a.y, b.y) - radius,
                Mathf.Max(a.x, b.x) + radius, Mathf.Max(a.y, b.y) + radius);
            rectangle = false;
        }

        public Area(string label, Rect bounds)
        {
            this.label = label;
            this.bounds = bounds;
            a = b = default;
            radius = 0;
            rectangle = true;
        }
    }

    private YQTerrainRepairProtectionV2(List<Area> areas) { this.areas = areas.ToArray(); }

    public static bool TryCreate(YQSpatialBlueprintV2 blueprint, string ownerSiteId,
        IReadOnlyList<Bounds> neighboringFoundations, out YQTerrainRepairProtectionV2 protection, out string failure)
    {
        protection = null;
        failure = string.Empty;
        if (blueprint?.hydrology == null || blueprint.routes == null || blueprint.sites == null || neighboringFoundations == null ||
            blueprint.hydrology.Count > MaximumAreas || blueprint.routes.Count > MaximumAreas ||
            blueprint.sites.Count > MaximumAreas || neighboringFoundations.Count > MaximumAreas)
        {
            failure = "Missing or over-budget water, route, site or foundation protection data.";
            return false;
        }
        var prepared = new List<Area>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        bool ownerFound = string.IsNullOrEmpty(ownerSiteId);
        // note: Even waterfalls and routes belonging to the owner remain protected; ownership only exempts its own site reserve.
        foreach (var water in blueprint.hydrology)
        {
            if (water == null || !Identity(ids, "water", water.hydrologyId) || !Positive(water.nominalWidth) ||
                !AppendPolyline(prepared, "water " + water.hydrologyId, water.controlPoints, water.nominalWidth * 0.5f, true))
            { failure = "Invalid or over-budget water protection geometry."; return false; }
        }
        foreach (var route in blueprint.routes)
        {
            if (route == null || !Identity(ids, "route", route.routeId) || !Positive(route.width) ||
                !Finite(route.shoulderWidth) || route.shoulderWidth < 0 ||
                !AppendPolyline(prepared, "route " + route.routeId, route.controlPoints, route.width * 0.5f + route.shoulderWidth, false))
            { failure = "Invalid or over-budget route protection geometry."; return false; }
        }
        foreach (var site in blueprint.sites)
        {
            if (site == null || !Identity(ids, "site", site.siteId) || !Finite(site.x) || !Finite(site.z) || !Positive(site.reservedRadius))
            { failure = "Invalid site reservation protection geometry."; return false; }
            if (string.Equals(site.siteId, ownerSiteId, StringComparison.Ordinal)) { ownerFound = true; continue; }
            Vector2 center = new Vector2(site.x, site.z);
            prepared.Add(new Area("site " + site.siteId, center, center, Mathf.Max(4f, site.reservedRadius)));
            if (prepared.Count > MaximumAreas) { failure = "Protection area budget exceeded."; return false; }
        }
        if (!ownerFound) { failure = "Repair owner is not present in the supplied blueprint."; return false; }
        for (int index = 0; index < neighboringFoundations.Count; index++)
        {
            Bounds box = neighboringFoundations[index];
            if (!Finite(box.center.x) || !Finite(box.center.y) || !Finite(box.center.z) ||
                !Positive(box.size.x) || !Positive(box.size.y) || !Positive(box.size.z))
            { failure = "Invalid neighboring foundation bounds."; return false; }
            // note: Protect horizontal support regardless of foundation height, preventing a repair from undermining a raised building.
            prepared.Add(new Area("foundation " + index, Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z)));
            if (prepared.Count > MaximumAreas) { failure = "Protection area budget exceeded."; return false; }
        }
        protection = new YQTerrainRepairProtectionV2(prepared);
        return true;
    }

    private static bool AppendPolyline(List<Area> areas, string label, IReadOnlyList<YQBlueprintPointV2> points, float radius, bool water)
    {
        if (!Finite(radius) || points == null || points.Count == 0 || points.Count > MaximumPoints) return false;
        for (int index = 0; index < points.Count; index++)
        {
            var point = points[index];
            if (point == null || !Finite(point.x) || !Finite(point.z) || !Finite(point.width) || point.width < 0) return false;
        }
        int segments = Mathf.Max(1, points.Count - 1);
        if (areas.Count + segments > MaximumAreas) return false;
        for (int index = 0; index < segments; index++)
        {
            var a = points[index];
            var b = points[Mathf.Min(index + 1, points.Count - 1)];
            // note: Match the water sampler's minimum footprint; retain per-point width without reducing nominal protection.
            float extent = water ? Mathf.Max(1.5f, Mathf.Max(radius, Mathf.Max(a.width, b.width) * 0.5f)) : Mathf.Max(1f, radius);
            areas.Add(new Area(label, new Vector2(a.x, a.z), new Vector2(b.x, b.z), extent));
        }
        return true;
    }

    public bool TryAllow(Rect affectedGround, ref int remainingChecks, out string failure)
    {
        failure = string.Empty;
        if (!Finite(affectedGround.xMin) || !Finite(affectedGround.xMax) || !Finite(affectedGround.yMin) ||
            !Finite(affectedGround.yMax) || affectedGround.width < 0 || affectedGround.height < 0)
        { failure = "Invalid repair influence bounds."; return false; }
        foreach (Area area in areas)
        {
            if (--remainingChecks < 0) { failure = "Protection intersection budget exceeded."; return false; }
            if (!Intersects(affectedGround, area.bounds)) continue;
            if (!area.rectangle && !CapsuleIntersects(affectedGround, area.a, area.b, area.radius)) continue;
            failure = "Repair overlaps protected " + area.label + ".";
            return false;
        }
        return true;
    }

    private static bool CapsuleIntersects(Rect rect, Vector2 a, Vector2 b, float radius)
    {
        // note: Continuous segment/rectangle distance catches narrow features between sample points without excluding an entire diagonal river's bounding box.
        float low = 0, high = 1;
        Vector2 delta = b - a;
        if (Clip(a.x, delta.x, rect.xMin, rect.xMax, ref low, ref high) &&
            Clip(a.y, delta.y, rect.yMin, rect.yMax, ref low, ref high)) return true;
        float distance = Mathf.Min(PointRectDistanceSquared(a, rect), PointRectDistanceSquared(b, rect));
        distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(rect.xMin, rect.yMin), a, b));
        distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(rect.xMin, rect.yMax), a, b));
        distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(rect.xMax, rect.yMin), a, b));
        distance = Mathf.Min(distance, PointSegmentDistanceSquared(new Vector2(rect.xMax, rect.yMax), a, b));
        return distance <= radius * radius;
    }

    private static bool Clip(float start, float delta, float min, float max, ref float low, ref float high)
    {
        if (Mathf.Abs(delta) < 0.000001f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        low = Mathf.Max(low, Mathf.Min(a, b));
        high = Mathf.Min(high, Mathf.Max(a, b));
        return low <= high;
    }

    private static float PointRectDistanceSquared(Vector2 point, Rect rect)
    {
        Vector2 nearest = new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        return (point - nearest).sqrMagnitude;
    }

    private static float PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude) : 0;
        return (point - a - segment * t).sqrMagnitude;
    }

    private static bool Intersects(Rect a, Rect b) => a.xMin <= b.xMax && a.xMax >= b.xMin && a.yMin <= b.yMax && a.yMax >= b.yMin;
    private static bool Identity(HashSet<string> ids, string kind, string id) => !string.IsNullOrWhiteSpace(id) && ids.Add(kind + ":" + id);
    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) <= 1000000f;

    public static string TestProtectionPolicies()
    {
        var blueprint = new YQSpatialBlueprintV2();
        var route = new YQRouteCorridorV2 { routeId = "diagonal", width = 1,
            controlPoints = new List<YQBlueprintPointV2> { new YQBlueprintPointV2 { x = -10, z = -10 }, new YQBlueprintPointV2 { x = 10, z = 10 } } };
        blueprint.routes.Add(route);
        if (!TryCreate(blueprint, null, Array.Empty<Bounds>(), out var protection, out string failure)) return failure;
        int budget = 100;
        if (!protection.TryAllow(new Rect(-9, 8, 0.5f, 0.5f), ref budget, out _))
            return "A distant corner of a diagonal corridor's bounding box was incorrectly blocked.";
        if (protection.TryAllow(new Rect(-0.1f, -0.1f, 0.2f, 0.2f), ref budget, out _))
            return "A narrow crossing between route vertices escaped continuous protection.";
        route.controlPoints.Clear(); // note: Later mutations to source records cannot invalidate the captured review evidence.
        if (protection.TryAllow(new Rect(-0.1f, -0.1f, 0.2f, 0.2f), ref budget, out _)) return "Protection was not snapshotted.";
        budget = 0;
        if (protection.TryAllow(new Rect(30, 30, 1, 1), ref budget, out _)) return "Protection bypassed its intersection budget.";

        blueprint.routes.Clear();
        blueprint.sites.Add(new YQSiteAnchorV2 { siteId = "owner", reservedRadius = 4 });
        if (!TryCreate(blueprint, "owner", Array.Empty<Bounds>(), out protection, out failure)) return failure;
        budget = 100;
        if (!protection.TryAllow(new Rect(-1, -1, 2, 2), ref budget, out _)) return "The owner could not repair inside its own site reserve.";
        blueprint.hydrology.Add(new YQHydrologyFeatureV2 { hydrologyId = "owner-water", nominalWidth = 2,
            controlPoints = new List<YQBlueprintPointV2> { new YQBlueprintPointV2() } });
        if (!TryCreate(blueprint, "owner", Array.Empty<Bounds>(), out protection, out failure)) return failure;
        if (protection.TryAllow(new Rect(-1, -1, 2, 2), ref budget, out _)) return "Owner exemption removed water protection.";
        if (TryCreate(blueprint, "missing-owner", Array.Empty<Bounds>(), out _, out _)) return "Unknown ownership was accepted.";
        blueprint.sites.Add(new YQSiteAnchorV2 { siteId = "owner", reservedRadius = 4 });
        if (TryCreate(blueprint, "owner", Array.Empty<Bounds>(), out _, out _)) return "Duplicate owner reservations were accepted.";
        blueprint.sites.Clear();
        blueprint.hydrology[0].controlPoints[0].x = float.NaN;
        if (TryCreate(blueprint, null, Array.Empty<Bounds>(), out _, out _)) return "Nonfinite water geometry was accepted.";
        blueprint.hydrology.Clear();
        if (TryCreate(blueprint, null, null, out _, out _) || TryCreate(null, null, Array.Empty<Bounds>(), out _, out _))
            return "Missing protection data silently became an empty world.";
        var oversized = new Bounds[MaximumAreas + 1];
        if (TryCreate(blueprint, null, oversized, out _, out _)) return "An oversized foundation set passed.";
        return null;
    }
}
