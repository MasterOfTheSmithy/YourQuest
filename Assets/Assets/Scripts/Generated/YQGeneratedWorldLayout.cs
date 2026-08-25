using System;
using System.Collections.Generic;
using UnityEngine;

public static class YQGeneratedWorldLayout
{
    public const string LayoutVersion =
        "generated_world_layout_v2_footprints";

    /*
     * Vey's origin owns the center of the generated world.
     * No generated settlement may occupy this radius.
     */
    public const float OriginReserveRadius =
        88f;

    /*
     * The first player-facing settlement is a visible destination from
     * Vey's hut, not a distant map marker. This keeps the generated
     * world readable the instant player control begins.
     */
    private const float StarterSettlementDistance =
        148f;

    private const float StarterSettlementJitter =
        18f;

    /*
     * Keep generated locations away from the extreme terrain edge.
     */
    private const float WorldEdgeMargin =
        90f;

    private const float SiteEdgeClearance = 18f;
    private const float SiteSeparation = 24f;

    private static YQRuntimeWorldSiteCatalog spatialCatalog;
    private static readonly Dictionary<string, Vector3> runtimeEncampmentAnchors =
        new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpatialCatalog()
    {
        spatialCatalog = null;
        runtimeEncampmentAnchors.Clear();
    }

    public static void ClearRuntimeEncampmentAnchors()
    {
        // note: Runtime anchor corrections are deterministic build products, never persisted mutable world authority.
        runtimeEncampmentAnchors.Clear();
    }

    public static void SetRuntimeEncampmentAnchor(
        string encampmentId,
        Vector3 anchor)
    {
        if (string.IsNullOrWhiteSpace(encampmentId) ||
            float.IsNaN(anchor.x) || float.IsInfinity(anchor.x) ||
            float.IsNaN(anchor.z) || float.IsInfinity(anchor.z))
        {
            return;
        }

        // note: Every runtime consumer receives the same prepass-approved site anchor, including population and reward placement after geometry materialization.
        runtimeEncampmentAnchors[encampmentId] = anchor;
    }

    /*
     * Small deterministic offsets keep the world from looking like
     * a literal square grid while preserving save determinism.
     */
    private const float RegionJitter =
        18f;

    private const float SettlementJitter =
        28f;

    private const float EncampmentSettlementReserveRadius =
        150f;

    private const int EncampmentSettlementSeparationAttempts =
        6;

    private const int SettlementSeparationAttempts =
        12;

    public static Vector3 GetVeyOriginAnchor()
    {
        return
            YQGeneratedWorldTerrain.OriginWorldPosition;
    }

    public static Vector3 GetRegionCenter(
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region)
    {
        if (region == null)
            return Vector3.zero;

        GridBounds bounds =
            CalculateWorldGridBounds(
                plan);

        Vector3 position =
            MapGridCoordinate(
                bounds,
                region.gridX,
                region.gridY);

        Vector2 jitter =
            DeterministicJitter(
                SafeSeed(plan) +
                "|" +
                LayoutVersion +
                "|region|" +
                SafeString(region.regionId),
                RegionJitter);

        position.x +=
            jitter.x;

        position.z +=
            jitter.y;

        position =
            KeepInsideWorld(
                position);

        return position;
    }

    public static Vector3 GetSettlementAnchor(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement)
    {
        if (settlement == null)
            return Vector3.zero;

        float footprintRadius = ResolveFootprintRadius(
            settlement.runtimeSiteKitId);
        string placementSeed = SafeSeed(plan) + "|" +
            SafeString(settlement.settlementId);
        Vector3 position;

        if (IsStarterSettlement(plan, settlement))
        {
            // note: Preserve deterministic variety while reserving a clear northbound first destination outside the origin courtyard.
            Vector2 starterOffset =
                DeterministicJitter(
                    SafeSeed(plan) +
                    "|starter_settlement|" +
                    SafeString(settlement.settlementId),
                    StarterSettlementJitter);

            position = PlaceFootprintSafely(
                new Vector3(
                    starterOffset.x,
                    0f,
                    StarterSettlementDistance +
                    starterOffset.y),
                footprintRadius,
                    placementSeed + "|starter");
        }
        else
        {
            GridBounds bounds =
                CalculateWorldGridBounds(
                    plan);

            position =
                MapGridCoordinate(
                    bounds,
                    settlement.gridX,
                    settlement.gridY);

            string identity =
                !string.IsNullOrWhiteSpace(
                    settlement.settlementId)
                    ? settlement.settlementId
                    : settlement.displayName;

            Vector2 jitter =
                DeterministicJitter(
                    SafeSeed(plan) +
                    "|" +
                    LayoutVersion +
                    "|settlement|" +
                    SafeString(identity) +
                    "|" +
                    SafeString(
                        settlement.deterministicSeed),
                    SettlementJitter);

            position.x +=
                jitter.x;

            position.z +=
                jitter.y;

            position = PlaceFootprintSafely(
                position,
                footprintRadius,
                placementSeed + "|settlement");
        }

        // note: Reviewed districts are complete spatial footprints, so later settlements are deterministically displaced from all earlier persisted settlements before terrain construction begins.
        position = PushAwayFromEarlierSettlements(
            plan,
            settlement,
            position,
            footprintRadius,
            placementSeed);

        return PlaceFootprintSafely(
            position,
            footprintRadius,
            placementSeed + "|settlement_final");
    }

    public static Vector3 GetSettlementAnchor(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        Terrain terrain)
    {
        Vector3 position =
            GetSettlementAnchor(
                plan,
                settlement);

        if (terrain != null)
        {
            position =
                YQGeneratedWorldTerrain.GroundPoint(
                    terrain,
                    position);
        }

        return position;
    }
    public static Vector3 GetEncampmentAnchor(
    GeneratedWorldPlanRecord plan,
    GeneratedEncampmentRecord encampment)
    {
        if (encampment == null)
            return Vector3.zero;

        if (!string.IsNullOrWhiteSpace(encampment.encampmentId) &&
            runtimeEncampmentAnchors.TryGetValue(
                encampment.encampmentId,
                out Vector3 runtimeAnchor))
        {
            return runtimeAnchor;
        }

        float footprintRadius = ResolveFootprintRadius(
            encampment.runtimeSiteKitId);

        GridBounds bounds =
            CalculateWorldGridBounds(
                plan,
                includeEncampments: true);

        Vector3 position =
            MapGridCoordinate(
                bounds,
                encampment.gridX,
                encampment.gridY);

        string identity =
            !string.IsNullOrWhiteSpace(
                encampment.encampmentId)
                ? encampment.encampmentId
                : encampment.displayName;

        Vector2 jitter =
            DeterministicJitter(
                SafeSeed(plan) +
                "|" +
                LayoutVersion +
                "|encampment|" +
                SafeString(identity) +
                "|" +
                SafeString(
                    encampment.deterministicSeed),
                22f);

        position.x +=
            jitter.x;

        position.z +=
            jitter.y;

        position = PlaceFootprintSafely(
            position,
            footprintRadius,
            SafeSeed(plan) + "|" + SafeString(identity) + "|encampment");

        position =
            PushAwayFromSettlements(
                plan,
                position,
                footprintRadius,
                SafeSeed(plan) +
                "|" +
                SafeString(identity));

        position =
            PlaceFootprintSafely(
                position,
                footprintRadius,
                SafeSeed(plan) + "|" + SafeString(identity) + "|encampment_final");

        return position;
    }

    public static Vector3 GetEncampmentAnchor(
        GeneratedWorldPlanRecord plan,
        GeneratedEncampmentRecord encampment,
        Terrain terrain)
    {
        Vector3 position =
            GetEncampmentAnchor(
                plan,
                encampment);

        if (terrain != null)
        {
            position =
                YQGeneratedWorldTerrain.GroundPoint(
                    terrain,
                    position);
        }

        return position;
    }

    public static Vector3 GetRegionCenter(
        GeneratedWorldPlanRecord plan,
        GeneratedRegionRecord region,
        Terrain terrain)
    {
        Vector3 position =
            GetRegionCenter(
                plan,
                region);

        if (terrain != null)
        {
            position =
                YQGeneratedWorldTerrain.GroundPoint(
                    terrain,
                    position);
        }

        return position;
    }

    private static Vector3 MapGridCoordinate(
        GridBounds bounds,
        int gridX,
        int gridY)
    {
        float halfTerrain =
            YQGeneratedWorldTerrain.WorldSize *
            0.5f;

        float usableHalfExtent =
            Mathf.Max(
                1f,
                halfTerrain -
                WorldEdgeMargin);

        float normalizedX =
            NormalizeGridAxis(
                gridX,
                bounds.minX,
                bounds.maxX);

        float normalizedY =
            NormalizeGridAxis(
                gridY,
                bounds.minY,
                bounds.maxY);

        float worldX =
            Mathf.Lerp(
                -usableHalfExtent,
                usableHalfExtent,
                normalizedX);

        float worldZ =
            Mathf.Lerp(
                -usableHalfExtent,
                usableHalfExtent,
                normalizedY);

        return
            new Vector3(
                worldX,
                0f,
                worldZ);
    }

    private static GridBounds CalculateWorldGridBounds(
        GeneratedWorldPlanRecord plan,
        bool includeEncampments = false)
    {
        GridBounds bounds =
            new GridBounds();

        if (plan == null)
        {
            bounds.Include(
                -1,
                -1);

            bounds.Include(
                1,
                1);

            return bounds;
        }

        plan.EnsureCollections();

        /*
         * Regions and settlements share the same conceptual world
         * grid, so use both collections to establish one common
         * coordinate transform.
         */
        if (plan.regions != null)
        {
            for (int i = 0;
                 i < plan.regions.Count;
                 i++)
            {
                GeneratedRegionRecord region =
                    plan.regions[i];

                if (region == null)
                    continue;

                bounds.Include(
                    region.gridX,
                    region.gridY);
            }
        }

        if (plan.settlements != null)
        {
            for (int i = 0;
                 i < plan.settlements.Count;
                 i++)
            {
                GeneratedSettlementRecord settlement =
                    plan.settlements[i];

                if (settlement == null)
                    continue;

                bounds.Include(
                    settlement.gridX,
                    settlement.gridY);
            }
        }

        if (includeEncampments &&
            plan.encampments != null)
        {
            for (int i = 0;
                 i < plan.encampments.Count;
                 i++)
            {
                GeneratedEncampmentRecord encampment =
                    plan.encampments[i];

                if (encampment == null)
                    continue;

                // note: Encampments only affect hostile-site mapping, so settlement anchors remain stable.
                bounds.Include(
                    encampment.gridX,
                    encampment.gridY);
            }
        }

        if (!bounds.hasValue)
        {
            bounds.Include(
                -1,
                -1);

            bounds.Include(
                1,
                1);
        }

        /*
         * A single-axis world would otherwise collapse every location
         * onto the center line.
         */
        if (bounds.minX == bounds.maxX)
        {
            bounds.minX--;
            bounds.maxX++;
        }

        if (bounds.minY == bounds.maxY)
        {
            bounds.minY--;
            bounds.maxY++;
        }

        return bounds;
    }

    private static Vector3 PushAwayFromEarlierSettlements(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord target,
        Vector3 position,
        float footprintRadius,
        string seed)
    {
        if (plan == null || target == null ||
            plan.settlements == null || plan.settlements.Count < 2)
        {
            return position;
        }

        int targetIndex = -1;

        for (int index = 0; index < plan.settlements.Count; index++)
        {
            GeneratedSettlementRecord candidate = plan.settlements[index];

            if (ReferenceEquals(candidate, target) ||
                (candidate != null &&
                 !string.IsNullOrWhiteSpace(target.settlementId) &&
                 string.Equals(candidate.settlementId, target.settlementId,
                     StringComparison.OrdinalIgnoreCase)))
            {
                targetIndex = index;
                break;
            }
        }

        if (targetIndex <= 0)
            return position;

        for (int attempt = 0;
             attempt < SettlementSeparationAttempts;
             attempt++)
        {
            bool moved = false;

            for (int index = 0; index < targetIndex; index++)
            {
                GeneratedSettlementRecord earlier = plan.settlements[index];

                if (earlier == null)
                    continue;

                Vector3 earlierAnchor = GetSettlementAnchor(plan, earlier);
                float earlierRadius = ResolveFootprintRadius(
                    earlier.runtimeSiteKitId);
                float requiredDistance = footprintRadius + earlierRadius +
                    SiteSeparation;
                Vector2 offset = new Vector2(
                    position.x - earlierAnchor.x,
                    position.z - earlierAnchor.z);
                float distance = offset.magnitude;

                if (distance >= requiredDistance)
                    continue;

                Vector2 direction = offset.sqrMagnitude > 0.0001f
                    ? offset.normalized
                    : DeterministicDirection(
                        seed + "|settlement_pair|" + index + "|" + attempt);
                float targetDistance = requiredDistance + 6f + attempt * 2f;
                position.x = earlierAnchor.x + direction.x * targetDistance;
                position.z = earlierAnchor.z + direction.y * targetDistance;
                position = PlaceFootprintSafely(
                    position,
                    footprintRadius,
                    seed + "|settlement_separation|" + index + "|" + attempt);
                moved = true;
            }

            if (!moved)
                return position;
        }

        // note: The terrain prepass remains the final fail-closed validator if a densely packed plan cannot fit after deterministic displacement attempts.
        return position;
    }

    private static Vector3 PushAwayFromSettlements(
        GeneratedWorldPlanRecord plan,
        Vector3 position,
        float footprintRadius,
        string seed)
    {
        if (plan == null ||
            plan.settlements == null ||
            plan.settlements.Count == 0)
        {
            return position;
        }

        for (int attempt = 0;
             attempt < EncampmentSettlementSeparationAttempts;
             attempt++)
        {
            GeneratedSettlementRecord nearestSettlement =
                null;

            Vector3 nearestAnchor =
                Vector3.zero;

            float nearestDistance =
                float.MaxValue;

            float nearestRequiredDistance =
                EncampmentSettlementReserveRadius;

            for (int i = 0;
                 i < plan.settlements.Count;
                 i++)
            {
                GeneratedSettlementRecord settlement =
                    plan.settlements[i];

                if (settlement == null)
                    continue;

                Vector3 settlementAnchor =
                    GetSettlementAnchor(
                        plan,
                        settlement);

                float distance =
                    HorizontalDistance(
                        position,
                        settlementAnchor);

                float settlementRadius = ResolveFootprintRadius(
                    settlement.runtimeSiteKitId);
                float requiredDistance = Mathf.Max(
                    EncampmentSettlementReserveRadius,
                    footprintRadius + settlementRadius + SiteSeparation);

                if (distance < nearestDistance)
                {
                    nearestDistance =
                        distance;

                    nearestAnchor =
                        settlementAnchor;

                    nearestSettlement =
                        settlement;

                    nearestRequiredDistance =
                        requiredDistance;
                }
            }

            if (nearestSettlement == null ||
                nearestDistance >=
                nearestRequiredDistance)
            {
                return position;
            }

            Vector2 offset =
                new Vector2(
                    position.x -
                    nearestAnchor.x,
                    position.z -
                    nearestAnchor.z);

            Vector2 direction =
                offset.sqrMagnitude >
                0.0001f
                    ? offset.normalized
                    : DeterministicDirection(
                        seed +
                        "|settlement_separation|" +
                        attempt);

            float targetDistance =
                nearestRequiredDistance +
                10f +
                attempt *
                8f;

            // note: Hostile sites are gameplay pressure, not settlement furniture; push them outside town readable space.
            position.x =
                nearestAnchor.x +
                direction.x *
                targetDistance;

            position.z =
                nearestAnchor.z +
                direction.y *
                targetDistance;

            position =
                KeepInsideWorld(
                    position,
                    footprintRadius);
        }

        return position;
    }

    private static bool IsStarterSettlement(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord candidate)
    {
        if (plan == null ||
            candidate == null ||
            plan.settlements == null)
        {
            return false;
        }

        string starterRegionId =
            string.Empty;

        if (plan.regions != null)
        {
            for (int i = 0;
                 i < plan.regions.Count;
                 i++)
            {
                GeneratedRegionRecord region =
                    plan.regions[i];

                if (region == null)
                    continue;

                string role =
                    SafeString(region.role);

                string scale =
                    SafeString(region.scaleHint);

                if (role.IndexOf("origin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    scale.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    starterRegionId =
                        SafeString(region.regionId);

                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(starterRegionId))
            {
                for (int i = 0;
                     i < plan.regions.Count;
                     i++)
                {
                    GeneratedRegionRecord region =
                        plan.regions[i];

                    if (region != null &&
                        !string.IsNullOrWhiteSpace(region.regionId))
                    {
                        starterRegionId =
                            region.regionId.Trim();

                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(starterRegionId) &&
            string.Equals(
                candidate.regionId,
                starterRegionId,
                StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0;
                 i < plan.settlements.Count;
                 i++)
            {
                GeneratedSettlementRecord settlement =
                    plan.settlements[i];

                if (settlement != null &&
                    string.Equals(
                        settlement.regionId,
                        starterRegionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // note: A region can own multiple towns, but only its first canonical settlement occupies the starter approach.
                    return ReferenceEquals(
                        settlement,
                        candidate);
                }
            }
        }

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement != null)
                return ReferenceEquals(settlement, candidate);
        }

        return false;
    }

    private static float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        float dx =
            a.x -
            b.x;

        float dz =
            a.z -
            b.z;

        return
            Mathf.Sqrt(
                dx *
                dx +
                dz *
                dz);
    }

    private static Vector2 DeterministicDirection(
        string seed)
    {
        uint hash =
            StableHash32(
                seed);

        float angle =
            (hash /
                (float)uint.MaxValue) *
            Mathf.PI *
            2f;

        return
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle));
    }

    private static float NormalizeGridAxis(
        int value,
        int minimum,
        int maximum)
    {
        if (maximum <= minimum)
            return 0.5f;

        return
            Mathf.InverseLerp(
                minimum,
                maximum,
                value);
    }

    private static float ResolveFootprintRadius(string kitId)
    {
        if (string.IsNullOrWhiteSpace(kitId))
            return 0f;

        spatialCatalog ??= Resources.Load<YQRuntimeWorldSiteCatalog>(
            "YQRuntimeWorldSiteCatalog");
        YQRuntimeWorldSiteRecord record = spatialCatalog?.FindByKitId(kitId);

        if (record == null || !record.spatiallyValidated ||
            float.IsNaN(record.authoredFootprintRadius) ||
            float.IsInfinity(record.authoredFootprintRadius))
        {
            return 0f;
        }

        return Mathf.Clamp(
            record.authoredFootprintRadius,
            0f,
            YQGeneratedWorldTerrain.WorldSize * 0.22f);
    }

    private static Vector3 PlaceFootprintSafely(
        Vector3 position,
        float footprintRadius,
        string seed)
    {
        footprintRadius = Mathf.Max(0f, footprintRadius);
        position = KeepInsideWorld(position, footprintRadius);
        float requiredDistance = OriginReserveRadius +
            footprintRadius + SiteSeparation;
        Vector2 horizontal = new Vector2(position.x, position.z);

        if (horizontal.magnitude >= requiredDistance)
            return position;

        Vector2 preferredDirection = horizontal.sqrMagnitude > 0.0001f
            ? horizontal.normalized
            : DeterministicDirection(seed + "|origin_clearance");
        Vector2 selectedDirection = preferredDirection;
        float selectedCapacity = DirectionalWorldCapacity(
            selectedDirection,
            footprintRadius);

        // note: A wide reviewed district may not fit due north/south, yet can fit safely along a diagonal of the square terrain; test deterministic alternatives before rejecting its intended distance.
        for (int attempt = 0; attempt < 16 &&
             selectedCapacity < requiredDistance; attempt++)
        {
            float angle = attempt * Mathf.PI * 0.125f;
            Vector2 candidate = new Vector2(
                Mathf.Sin(angle),
                Mathf.Cos(angle));
            float capacity = DirectionalWorldCapacity(
                candidate,
                footprintRadius);

            if (capacity > selectedCapacity)
            {
                selectedDirection = candidate;
                selectedCapacity = capacity;
            }
        }

        float targetDistance = Mathf.Min(
            requiredDistance,
            selectedCapacity);
        position.x = selectedDirection.x * targetDistance;
        position.z = selectedDirection.y * targetDistance;
        return KeepInsideWorld(position, footprintRadius);
    }

    private static float DirectionalWorldCapacity(
        Vector2 direction,
        float footprintRadius)
    {
        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.up;
        float halfWorld = YQGeneratedWorldTerrain.WorldSize * 0.5f;
        float margin = Mathf.Max(
            WorldEdgeMargin,
            footprintRadius + SiteEdgeClearance);
        float axisLimit = Mathf.Max(1f, halfWorld - margin);
        float xCapacity = Mathf.Abs(direction.x) > 0.0001f
            ? axisLimit / Mathf.Abs(direction.x)
            : float.MaxValue;
        float zCapacity = Mathf.Abs(direction.y) > 0.0001f
            ? axisLimit / Mathf.Abs(direction.y)
            : float.MaxValue;
        return Mathf.Min(xCapacity, zCapacity);
    }

    private static Vector3 KeepInsideWorld(
        Vector3 position)
    {
        return KeepInsideWorld(position, 0f);
    }

    private static Vector3 KeepInsideWorld(
        Vector3 position,
        float footprintRadius)
    {
        float margin = Mathf.Max(
            WorldEdgeMargin,
            Mathf.Max(0f, footprintRadius) + SiteEdgeClearance);
        float limit = YQGeneratedWorldTerrain.WorldSize * 0.5f - margin;

        position.x =
            Mathf.Clamp(
                position.x,
                -limit,
                limit);

        position.z =
            Mathf.Clamp(
                position.z,
                -limit,
                limit);

        return position;
    }

    private static Vector2 DeterministicJitter(
        string seed,
        float maximumDistance)
    {
        if (maximumDistance <= 0f)
            return Vector2.zero;

        uint hashA =
            StableHash32(
                seed +
                "|angle");

        uint hashB =
            StableHash32(
                seed +
                "|radius");

        float angle =
            (hashA /
                (float)uint.MaxValue) *
            Mathf.PI *
            2f;

        float radius =
            (hashB /
                (float)uint.MaxValue) *
            maximumDistance;

        return
            new Vector2(
                Mathf.Cos(angle) *
                    radius,
                Mathf.Sin(angle) *
                    radius);
    }

    private static string SafeSeed(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null ||
            string.IsNullOrWhiteSpace(
                plan.worldSeed))
        {
            return
                "yourquest_default_world";
        }

        return
            plan.worldSeed.Trim();
    }

    private static string SafeString(
        string value)
    {
        return
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
    }

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    private struct GridBounds
    {
        public bool hasValue;

        public int minX;
        public int maxX;

        public int minY;
        public int maxY;

        public void Include(
            int x,
            int y)
        {
            if (!hasValue)
            {
                hasValue =
                    true;

                minX =
                    x;

                maxX =
                    x;

                minY =
                    y;

                maxY =
                    y;

                return;
            }

            if (x < minX)
                minX = x;

            if (x > maxX)
                maxX = x;

            if (y < minY)
                minY = y;

            if (y > maxY)
                maxY = y;
        }
    }
}
