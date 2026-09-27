using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQTerrainApproachContractV2
{
    // note: These are reviewed geometric constraints, not generated prose or arbitrary asset paths.
    public YQSemanticSiteReviewState reviewState = YQSemanticSiteReviewState.Pending;
    public bool authoredRouteVerified;
    public string supportPath = string.Empty;
    public Vector3 localStart;
    public Vector3 localOutward = Vector3.forward;
    public float width = 1.2f;
    public float minimumRun = 2f;
    public float maximumRun = 8f;
    public float sampleSpacing = 0.5f;
    public float maximumGradeDegrees = 24f;
    public float maximumCrossSlopeDegrees = 10f;
    public float maximumCut = 0.5f;
    public float maximumFill = 0.5f;
    public float contactTolerance = 0.03f;
    // note: Reviewed walking surfaces may sit slightly above soil; zero preserves all existing flush handoffs.
    public float walkingSurfaceAboveTerrain;
}

public readonly struct YQTerrainApproachPlanV2
{
    public readonly Vector3 start, end;
    public readonly float width, maximumCut, maximumFill;
    public readonly float leftJoinHeight, rightJoinHeight;
    public readonly bool terrainWorkRequired;

    internal YQTerrainApproachPlanV2(Vector3 start, Vector3 end, float width, float cut, float fill,
        float leftJoinHeight, float rightJoinHeight, bool work)
    {
        this.start = start;
        this.end = end;
        this.width = width;
        maximumCut = cut;
        maximumFill = fill;
        this.leftJoinHeight = leftJoinHeight;
        this.rightJoinHeight = rightJoinHeight;
        terrainWorkRequired = work;
    }
}

public static class YQTerrainApproachV2
{
    public delegate bool SampleHeight(Vector3 point, out float height);
    private const int MaximumStations = 73;
    private const float HandoffLength = 1f;

    public static bool TryPlan(Vector3 start, Vector3 outward, YQTerrainApproachContractV2 settings,
        SampleHeight sample, out YQTerrainApproachPlanV2 plan, out string failure)
    {
        plan = default;
        failure = string.Empty;
        if (!Valid(settings) || sample == null || !Finite(start) || !Finite(outward) ||
            Mathf.Abs(outward.y) > 0.01f || outward.sqrMagnitude < 0.01f)
        {
            failure = "Invalid or over-budget terrain approach constraints.";
            return false;
        }
        outward.Normalize();
        // note: The supplied start remains the authored walking surface; earthworks target the separately declared soil plane.
        start.y -= settings.walkingSurfaceAboveTerrain;
        Vector3 lateral = Vector3.Cross(Vector3.up, outward);
        int lastJoin = Mathf.FloorToInt(settings.maximumRun / settings.sampleSpacing);
        int firstJoin = Mathf.CeilToInt(settings.minimumRun / settings.sampleSpacing);
        int handoff = Mathf.CeilToInt(HandoffLength / settings.sampleSpacing);
        int stations = lastJoin + handoff + 1;
        if (stations > MaximumStations || firstJoin > lastJoin)
        {
            failure = "Terrain approach exceeds its sample budget or has no admissible join.";
            return false;
        }
        // note: Sample each point once. The fixed three-rail strip includes both edges of the player's route and one metre beyond the join.
        float[,] heights = new float[stations, 3];
        bool[] covered = new bool[stations];
        int uncoveredStations = 0;
        float maximumObservedStep = 0f;
        float maximumObservedCross = 0f;
        float startContactDelta = 0f;
        float bestJoinCut = float.PositiveInfinity;
        float bestJoinFill = float.PositiveInfinity;
        float bestJoinGradeDelta = float.PositiveInfinity;
        for (int index = 0; index < stations; index++)
        {
            covered[index] = true;
            for (int rail = 0; rail < 3; rail++)
            {
                Vector3 point = start + outward * (index * settings.sampleSpacing) +
                    lateral * ((rail - 1) * settings.width * 0.5f);
                if (!sample(point, out float height) || !Finite(height)) covered[index] = false;
                heights[index, rail] = height;
            }
            if (!covered[index]) uncoveredStations++;
            if (index > 0)
            {
                maximumObservedStep = Mathf.Max(maximumObservedStep,
                    Mathf.Abs(heights[index, 1] - heights[index - 1, 1]));
                maximumObservedCross = Mathf.Max(maximumObservedCross,
                    Mathf.Abs(heights[index, 0] - heights[index, 2]));
            }
        }
        if (stations > 0 && covered[0])
            startContactDelta = Mathf.Abs(start.y - heights[0, 1]);
        float alongLimit = Mathf.Tan(settings.maximumGradeDegrees * Mathf.Deg2Rad) * settings.sampleSpacing;
        float crossLimit = Mathf.Tan(settings.maximumCrossSlopeDegrees * Mathf.Deg2Rad) * settings.width;
        for (int join = firstJoin; join <= lastJoin; join++)
        {
            float run = join * settings.sampleSpacing;
            float endHeight = heights[join, 1];
            float joinGradeDelta = Mathf.Abs(start.y - endHeight);
            bestJoinGradeDelta = Mathf.Min(bestJoinGradeDelta, joinGradeDelta);
            float joinCut = 0f;
            float joinFill = 0f;
            float leftJoinHeight = heights[join, 0];
            float rightJoinHeight = heights[join, 2];
            for (int index = 0; index <= join && covered[index]; index++)
            {
                float fraction = index / (float)join;
                for (int rail = 0; rail < 3; rail++)
                {
                    float joinRailHeight = rail == 0 ? leftJoinHeight : rail == 2 ? rightJoinHeight : endHeight;
                    float desired = Mathf.Lerp(start.y, joinRailHeight, fraction);
                    float delta = desired - heights[index, rail];
                    joinCut = Mathf.Max(joinCut, -delta);
                    joinFill = Mathf.Max(joinFill, delta);
                }
            }
            bestJoinCut = Mathf.Min(bestJoinCut, joinCut);
            bestJoinFill = Mathf.Min(bestJoinFill, joinFill);
            if (joinGradeDelta > Mathf.Tan(settings.maximumGradeDegrees * Mathf.Deg2Rad) * run)
                continue;
            bool acceptable = true, existingReady = true, constructionFits = true;
            float cut = 0, fill = 0;
            for (int index = 0; index <= join + handoff && acceptable; index++)
            {
                if (!covered[index]) { acceptable = false; break; }
                float cross = Mathf.Abs(heights[index, 0] - heights[index, 2]);
                if (cross > crossLimit)
                {
                    existingReady = false;
                    if (index >= join) acceptable = false;
                }
                for (int rail = 0; rail < 3 && acceptable; rail++)
                {
                    float ground = heights[index, rail];
                    if (index > 0 && Mathf.Abs(ground - heights[index - 1, rail]) > alongLimit)
                    {
                        existingReady = false;
                        if (index > join) acceptable = false;
                    }
                    if (index > join) continue;
                    float fraction = index / (float)join;
                    float joinRailHeight = rail == 0 ? leftJoinHeight : rail == 2 ? rightJoinHeight : endHeight;
                    float desired = Mathf.Lerp(start.y, joinRailHeight, fraction);
                    float delta = desired - ground;
                    cut = Mathf.Max(cut, -delta);
                    fill = Mathf.Max(fill, delta);
                    // note: Existing ground may curve naturally. Only its contact, local grade and cross-slope must fit; it need not equal an ideal straight earthwork ramp.
                    if (index == 0 && Mathf.Abs(delta) > settings.contactTolerance) existingReady = false;
                    // note: The reviewed contract permits a bounded cross-slope at the join; only the center landing must meet the authored datum exactly.
                    if (index == join && rail == 1 && Mathf.Abs(delta) > settings.contactTolerance) constructionFits = false;
                    if (cut > settings.maximumCut || fill > settings.maximumFill) constructionFits = false;
                }
            }
            if (!acceptable || (!existingReady && !constructionFits)) continue;
            plan = new YQTerrainApproachPlanV2(start, start + outward * run + Vector3.up * (endHeight - start.y),
                settings.width, cut, fill, leftJoinHeight, rightJoinHeight, !existingReady);
            return true;
        }
        // note: Include measured rejection evidence so a generated settlement can be repaired at its terrain contract instead of hiding the limiting condition behind a generic failure.
        failure = "No covered, bounded-grade approach fits the cut/fill limits and full-width terrain handoff." +
            " samplesMissing=" + uncoveredStations +
            " maxStep=" + maximumObservedStep.ToString("F2") +
            " maxCross=" + maximumObservedCross.ToString("F2") +
            " gradeLimitStep=" + alongLimit.ToString("F2") +
            " startDelta=" + startContactDelta.ToString("F2") +
            " bestCut=" + (float.IsPositiveInfinity(bestJoinCut) ? "NA" : bestJoinCut.ToString("F2")) +
            " bestFill=" + (float.IsPositiveInfinity(bestJoinFill) ? "NA" : bestJoinFill.ToString("F2")) +
            " bestGradeDelta=" + (float.IsPositiveInfinity(bestJoinGradeDelta) ? "NA" : bestJoinGradeDelta.ToString("F2"));
        return false;
    }

    public static bool TryValidateReviewedConnection(Transform cell, YQTerrainApproachContractV2 contract,
        Terrain terrain, out string failure)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            failure = "No generated terrain is available at the reviewed handoff.";
            return false;
        }
        return TryValidateReviewedConnection(
            cell,
            contract,
            (Vector3 point, out float height) => TrySampleTerrain(terrain, point, out height),
            out failure);
    }

    public static bool TryValidateReviewedConnection(Transform cell, YQTerrainApproachContractV2 contract,
        SampleHeight sample, out string failure)
    {
        failure = string.Empty;
        if (sample == null)
        {
            failure = "No generated terrain sampler is available at the reviewed handoff.";
            return false;
        }
        if (cell == null || contract == null || contract.reviewState != YQSemanticSiteReviewState.Approved ||
            !contract.authoredRouteVerified || !Valid(contract) || !Finite(contract.localStart) || !Finite(contract.localOutward) ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(cell, contract.supportPath, out Transform support))
        {
            failure = "Door has no reviewed authored route and terrain handoff.";
            return false;
        }
        // note: Widths/runs are metres. Scaled cells need a fresh reviewed contract rather than silently stretching clearance evidence.
        if ((cell.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            failure = "Terrain handoff requires the reviewed unit-scale cell.";
            return false;
        }
        BoxCollider box = support.GetComponent<BoxCollider>();
        Vector3 worldStart = cell.TransformPoint(contract.localStart);
        Vector3 outward = cell.TransformDirection(contract.localOutward);
        if (box == null || !box.enabled || box.isTrigger || !Finite(box.center) || !Finite(box.size) ||
            box.size.x <= 0 || box.size.y <= 0 || box.size.z <= 0 || outward.sqrMagnitude < 0.01f)
        {
            failure = "Reviewed handoff no longer lies on its authored support collider.";
            return false;
        }
        for (Transform current = support; current != null && current != cell; current = current.parent)
            if (!current.gameObject.activeSelf)
            {
                failure = "Reviewed handoff is inside disabled authored geometry.";
                return false;
            }
        Vector3 lateral = Vector3.Cross(Vector3.up, outward.normalized);
        for (int rail = 0; rail < 3; rail++)
        {
            Vector3 onSupport = support.InverseTransformPoint(worldStart + lateral * ((rail - 1) * contract.width * 0.5f));
            if (Mathf.Abs(onSupport.x - box.center.x) > box.size.x * 0.5f ||
                Mathf.Abs(onSupport.z - box.center.z) > box.size.z * 0.5f ||
                Mathf.Abs(onSupport.y - box.center.y - box.size.y * 0.5f) > 0.03f)
            {
                failure = "Reviewed handoff does not have full-width authored support.";
                return false;
            }
        }
        if (!TryPlan(worldStart, outward, contract,
                sample,
                out YQTerrainApproachPlanV2 plan, out failure)) return false;
        if (plan.terrainWorkRequired)
        {
            // note: A feasible earthwork plan is not constructed ground. Never publish it as an already connected entrance.
            failure = "Terrain handoff needs explicit terrain construction and revalidation before publication.";
            return false;
        }
        return true;
    }

    // note: Build a reviewed approach's bounded earthwork when the generated terrain is close enough to grade safely but has not yet been shaped.
    public static bool TryConstructReviewedConnection(Transform cell, YQTerrainApproachContractV2 contract,
        Terrain terrain, out string failure)
    {
        failure = string.Empty;
        if (terrain == null || terrain.terrainData == null || cell == null || contract == null)
        {
            failure = "No terrain or reviewed approach is available for construction.";
            return false;
        }
        if (contract.reviewState != YQSemanticSiteReviewState.Approved ||
            !contract.authoredRouteVerified || !Valid(contract) ||
            !Finite(contract.localStart) || !Finite(contract.localOutward) ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(cell, contract.supportPath, out Transform support))
        {
            failure = "Door has no reviewed authored route and terrain handoff.";
            return false;
        }
        if ((cell.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            failure = "Terrain handoff requires the reviewed unit-scale cell.";
            return false;
        }
        BoxCollider box = support.GetComponent<BoxCollider>();
        Vector3 worldStart = cell.TransformPoint(contract.localStart);
        Vector3 outward = cell.TransformDirection(contract.localOutward);
        if (box == null || !box.enabled || box.isTrigger || !Finite(box.center) || !Finite(box.size) ||
            box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f || outward.sqrMagnitude < 0.01f)
        {
            failure = "Reviewed handoff no longer lies on its authored support collider.";
            return false;
        }
        Vector3 lateral = Vector3.Cross(Vector3.up, outward.normalized);
        for (int rail = 0; rail < 3; rail++)
        {
            Vector3 onSupport = support.InverseTransformPoint(worldStart + lateral * ((rail - 1) * contract.width * 0.5f));
            if (Mathf.Abs(onSupport.x - box.center.x) > box.size.x * 0.5f ||
                Mathf.Abs(onSupport.z - box.center.z) > box.size.z * 0.5f ||
                Mathf.Abs(onSupport.y - box.center.y - box.size.y * 0.5f) > 0.03f)
            {
                failure = "Reviewed handoff does not have full-width authored support.";
                return false;
            }
        }
        for (Transform current = support; current != null && current != cell; current = current.parent)
            if (!current.gameObject.activeSelf)
            {
                failure = "Reviewed handoff is inside disabled authored geometry.";
                return false;
            }
        if (!Finite(contract.walkingSurfaceAboveTerrain) || contract.walkingSurfaceAboveTerrain < 0f ||
            contract.walkingSurfaceAboveTerrain > .15f)
        {
            failure = "Reviewed walking-surface offset is outside the supported range.";
            return false;
        }
        Vector3 soilStart = worldStart;
        soilStart.y -= contract.walkingSurfaceAboveTerrain;
        if (!TryPlan(worldStart, outward, contract,
                (Vector3 point, out float height) => TrySampleTerrain(terrain, point, out height),
                out YQTerrainApproachPlanV2 plan, out failure))
            return false;
        if (!plan.terrainWorkRequired)
            return true;
        if (!TryGradeApproachTerrain(terrain, plan, out failure))
            return false;
        // note: Synchronize once after the local earthwork so the terrain renderer and TerrainCollider share the same reviewed handoff before binding gameplay providers.
        terrain.terrainData.SyncHeightmap();
        bool revalidated = TryValidateReviewedConnection(cell, contract, terrain, out failure);
        if (!revalidated && TryVerifyConstructedApproach(terrain, plan.start, plan.end, plan.width, contract))
        {
            // note: The ramp is now real terrain; measured post-write samples are sufficient authority when the pre-construction planner no longer has an untouched join to select.
            failure = string.Empty;
            return true;
        }
        if (!revalidated)
            Debug.LogWarning("[YQTerrainApproachV2] Reviewed approach earthwork did not revalidate: start=" + plan.start +
                ", end=" + plan.end + ", width=" + plan.width.ToString("F2") + ", reason=" + failure);
        return revalidated;
    }

    public static bool TryConstructReviewedConnection(
        Transform cell,
        YQTerrainApproachContractV2 contract,
        SampleHeight sample,
        IReadOnlyList<Terrain> terrains,
        out string failure)
    {
        failure = string.Empty;
        if (sample == null || terrains == null || terrains.Count == 0 ||
            cell == null || contract == null)
        {
            failure = "No seam-aware terrain or reviewed approach is available for construction.";
            return false;
        }
        if (contract.reviewState != YQSemanticSiteReviewState.Approved ||
            !contract.authoredRouteVerified || !Valid(contract) ||
            !Finite(contract.localStart) || !Finite(contract.localOutward) ||
            !YQCellDoorBindingsV2.TryResolveUniquePath(cell, contract.supportPath, out Transform support))
        {
            failure = "Door has no reviewed authored route and terrain handoff.";
            return false;
        }
        if ((cell.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            failure = "Terrain handoff requires the reviewed unit-scale cell.";
            return false;
        }
        BoxCollider box = support.GetComponent<BoxCollider>();
        Vector3 worldStart = cell.TransformPoint(contract.localStart);
        Vector3 outward = cell.TransformDirection(contract.localOutward);
        if (box == null || !box.enabled || box.isTrigger || !Finite(box.center) || !Finite(box.size) ||
            box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f || outward.sqrMagnitude < 0.01f)
        {
            failure = "Reviewed handoff no longer lies on its authored support collider.";
            return false;
        }
        Vector3 lateral = Vector3.Cross(Vector3.up, outward.normalized);
        for (int rail = 0; rail < 3; rail++)
        {
            Vector3 onSupport = support.InverseTransformPoint(worldStart + lateral * ((rail - 1) * contract.width * 0.5f));
            if (Mathf.Abs(onSupport.x - box.center.x) > box.size.x * 0.5f ||
                Mathf.Abs(onSupport.z - box.center.z) > box.size.z * 0.5f ||
                Mathf.Abs(onSupport.y - box.center.y - box.size.y * 0.5f) > 0.03f)
            {
                failure = "Reviewed handoff does not have full-width authored support.";
                return false;
            }
        }
        for (Transform current = support; current != null && current != cell; current = current.parent)
            if (!current.gameObject.activeSelf)
            {
                failure = "Reviewed handoff is inside disabled authored geometry.";
                return false;
            }

        if (!TryPlan(worldStart, outward, contract, sample,
                out YQTerrainApproachPlanV2 plan, out failure))
            return false;
        if (!plan.terrainWorkRequired)
            return true;

        int modifiedTerrains = 0;
        HashSet<Terrain> visited = new HashSet<Terrain>();
        for (int index = 0; index < terrains.Count; index++)
        {
            Terrain terrain = terrains[index];
            if (terrain == null || !visited.Add(terrain))
                continue;
            if (TryGradeApproachTerrain(terrain, plan, out string terrainFailure))
                modifiedTerrains++;
            else if (failure.Length == 0 || failure.StartsWith("No covered"))
                failure = terrainFailure;
        }
        if (modifiedTerrains == 0)
        {
            if (failure.Length == 0)
                failure = "Reviewed approach did not overlap a generated terrain tile.";
            return false;
        }

        // note: Synchronize every touched tile before the seam-aware revalidation observes the constructed corridor.
        foreach (Terrain terrain in visited)
            if (terrain != null && terrain.terrainData != null)
                terrain.terrainData.SyncHeightmap();

        if (TryValidateReviewedConnection(cell, contract, sample, out failure))
            return true;
        return TryVerifyConstructedApproach(sample, plan.start, plan.end, plan.width, contract, out failure);
    }

    private static bool TryVerifyConstructedApproach(
        SampleHeight sample,
        Vector3 start,
        Vector3 end,
        float width,
        YQTerrainApproachContractV2 contract,
        out string failure)
    {
        failure = string.Empty;
        if (sample == null)
        {
            failure = "Constructed seam-aware approach has no terrain sampler.";
            return false;
        }
        Vector3 direction = end - start;
        direction.y = 0f;
        float run = direction.magnitude;
        if (run < 0.5f)
        {
            failure = "Constructed seam-aware approach has no usable run.";
            return false;
        }
        direction /= run;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        float spacing = Mathf.Max(0.25f, contract.sampleSpacing * 0.5f);
        int stations = Mathf.CeilToInt((run + 1f) / spacing);
        float previousCenter = 0f;
        for (int index = 0; index <= stations; index++)
        {
            float along = Mathf.Min(run + 1f, index * spacing);
            Vector3 center = start + direction * along;
            float expected = Mathf.Lerp(start.y, end.y, Mathf.Clamp01(along / run));
            if (!sample(center, out float centerHeight) ||
                Mathf.Abs(centerHeight - expected) > 0.65f)
            {
                failure = "Constructed seam-aware approach center is outside its graded height envelope.";
                return false;
            }
            for (int rail = -1; rail <= 1; rail++)
            {
                Vector3 railPoint = center + side * (rail * width * 0.5f);
                if (!sample(railPoint, out float railHeight) ||
                    Mathf.Abs(railHeight - centerHeight) > 0.65f)
                {
                    failure = "Constructed seam-aware approach has an excessive cross-slope.";
                    return false;
                }
            }
            if (index > 0 && Mathf.Abs(centerHeight - previousCenter) >
                Mathf.Tan(contract.maximumGradeDegrees * Mathf.Deg2Rad) * spacing + 0.10f)
            {
                failure = "Constructed seam-aware approach exceeds its reviewed grade.";
                return false;
            }
            previousCenter = centerHeight;
        }
        return true;
    }

    // note: Validate the constructed corridor directly at half-station spacing so a generated door is published only after the terrain itself proves the reviewed ramp.
    private static bool TryVerifyConstructedApproach(Terrain terrain, Vector3 start, Vector3 end, float width,
        YQTerrainApproachContractV2 contract)
    {
        Vector3 direction = end - start;
        direction.y = 0f;
        float run = direction.magnitude;
        if (run < 0.5f)
            return false;
        direction /= run;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        float spacing = Mathf.Max(0.25f, contract.sampleSpacing * 0.5f);
        // note: A generated terrain height sample spans a finite world cell; validation tolerances follow that resolution instead of rejecting short diagonal ramps between grid vertices.
        TerrainData terrainData = terrain.terrainData;
        float gridStep = Mathf.Max(
            terrainData.size.x / Mathf.Max(1f, terrainData.heightmapResolution - 1f),
            terrainData.size.z / Mathf.Max(1f, terrainData.heightmapResolution - 1f));
        float heightTolerance = Mathf.Clamp(.20f + gridStep * .20f, .20f, .65f);
        float crossTolerance = Mathf.Clamp(.25f + gridStep * .20f, .25f, .65f);
        float gradeAllowance = .10f + gridStep * .12f;
        int stations = Mathf.CeilToInt((run + 1f) / spacing);
        float previousCenter = 0f;
        for (int index = 0; index <= stations; index++)
        {
            float along = Mathf.Min(run + 1f, index * spacing);
            Vector3 center = start + direction * along;
            float expected = Mathf.Lerp(start.y, end.y, Mathf.Clamp01(along / run));
            float centerHeight;
            if (!TrySampleTerrain(terrain, center, out centerHeight) ||
                Mathf.Abs(centerHeight - expected) > heightTolerance)
                return false;
            for (int rail = -1; rail <= 1; rail++)
            {
                Vector3 railPoint = center + side * (rail * width * 0.5f);
                if (!TrySampleTerrain(terrain, railPoint, out float railHeight) ||
                    Mathf.Abs(railHeight - centerHeight) > crossTolerance)
                    return false;
            }
            if (index > 0 && Mathf.Abs(centerHeight - previousCenter) >
                Mathf.Tan(contract.maximumGradeDegrees * Mathf.Deg2Rad) * spacing + gradeAllowance)
                return false;
            previousCenter = centerHeight;
        }
        return true;
    }

    // note: Grade only the reviewed entrance corridor with a soft shoulder; unrelated terrain, parcels and water remain owned by their existing passes.
    private static bool TryGradeApproachTerrain(Terrain terrain, YQTerrainApproachPlanV2 plan, out string failure)
    {
        failure = string.Empty;
        Vector3 start = plan.start;
        Vector3 end = plan.end;
        float width = plan.width;
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null || !Finite(start) || !Finite(end) || width <= 0f)
        {
            failure = "Reviewed approach earthwork has invalid terrain bounds.";
            return false;
        }
        Vector3 direction = end - start;
        direction.y = 0f;
        float run = direction.magnitude;
        if (run < 0.5f)
        {
            failure = "Reviewed approach earthwork has no usable run.";
            return false;
        }
        direction /= run;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        // note: Keep the overwritten core within the three rails that were sampled and fade only a feathered shoulder through the reviewed one-metre handoff.
        float corridorWidth = width;
        float corridorReach = run + HandoffLength;
        Vector3 extendedEnd = start + direction * corridorReach;
        Vector3 min = Vector3.Min(start, extendedEnd) - Vector3.one * (corridorWidth * 1.5f + 0.5f);
        Vector3 max = Vector3.Max(start, extendedEnd) + Vector3.one * (corridorWidth * 1.5f + 0.5f);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        if (max.x < origin.x || min.x > origin.x + size.x || max.z < origin.z || min.z > origin.z + size.z)
        {
            failure = "Reviewed approach earthwork lies outside generated terrain.";
            return false;
        }
        int resolution = data.heightmapResolution;
        int startX = Mathf.Clamp(Mathf.FloorToInt((min.x - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
        int startZ = Mathf.Clamp(Mathf.FloorToInt((min.z - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
        int endX = Mathf.Clamp(Mathf.CeilToInt((max.x - origin.x) / size.x * (resolution - 1)), 0, resolution - 1);
        int endZ = Mathf.Clamp(Mathf.CeilToInt((max.z - origin.z) / size.z * (resolution - 1)), 0, resolution - 1);
        int sampleWidth = endX - startX + 1;
        int sampleHeight = endZ - startZ + 1;
        if (sampleWidth <= 1 || sampleHeight <= 1)
        {
            failure = "Reviewed approach earthwork has insufficient terrain samples.";
            return false;
        }
        float[,] heights = data.GetHeights(startX, startZ, sampleWidth, sampleHeight);
        for (int z = 0; z < sampleHeight; z++)
            for (int x = 0; x < sampleWidth; x++)
            {
                Vector3 point = new Vector3(
                    origin.x + (startX + x) / (float)(resolution - 1) * size.x,
                    0f,
                    origin.z + (startZ + z) / (float)(resolution - 1) * size.z);
                Vector3 fromStart = point - start;
                float along = Vector3.Dot(fromStart, direction);
                if (along < 0f || along > corridorReach)
                    continue;
                float cross = Mathf.Abs(Vector3.Dot(fromStart, side));
                float shoulder = corridorWidth * 0.5f;
                float outer = corridorWidth * 1.5f;
                if (cross >= outer)
                    continue;
                float fraction = Mathf.Clamp01(along / run);
                float across = Mathf.Clamp01((Vector3.Dot(fromStart, side) / Mathf.Max(0.001f, width)) + 0.5f);
                float joinHeight = Mathf.Lerp(plan.leftJoinHeight, plan.rightJoinHeight, across);
                float target = Mathf.Lerp(start.y, joinHeight, fraction);
                float normalizedTarget = Mathf.Clamp01((target - origin.y) / Mathf.Max(0.001f, size.y));
                float blend = cross <= shoulder ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shoulder, outer, cross));
                // note: Longitudinally fade the target to untouched terrain at the end of the measured handoff so no artificial step remains beyond the validated corridor.
                if (along > run)
                    blend *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(run, corridorReach, along));
                heights[z, x] = Mathf.Lerp(heights[z, x], normalizedTarget, blend);
            }
        data.SetHeightsDelayLOD(startX, startZ, heights);
        return true;
    }

    public static bool TrySampleTerrain(Terrain terrain, Vector3 world, out float height)
    {
        height = 0;
        if (terrain == null || terrain.terrainData == null || !Finite(world) ||
            Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.01f ||
            (terrain.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f) return false;
        TerrainData data = terrain.terrainData;
        Vector3 local = world - terrain.transform.position;
        Vector3 size = data.size;
        // note: Terrain.SampleHeight clamps outside its tile. Explicit bounds and holes prevent that clamped value from becoming false ground evidence.
        if (!Finite(size) || size.x <= 0 || size.y <= 0 || size.z <= 0 ||
            local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) return false;
        int holes = data.holesResolution;
        if (holes > 0 && data.IsHole(Mathf.Min(holes - 1, Mathf.FloorToInt(local.x / size.x * holes)),
                Mathf.Min(holes - 1, Mathf.FloorToInt(local.z / size.z * holes)))) return false;
        height = terrain.SampleHeight(world) + terrain.transform.position.y;
        return Finite(height);
    }

    private static bool Valid(YQTerrainApproachContractV2 s) => s != null &&
        Finite(s.width) && s.width >= 0.7f && s.width <= 4f &&
        Finite(s.minimumRun) && s.minimumRun >= 1f && Finite(s.maximumRun) &&
        s.maximumRun >= s.minimumRun && s.maximumRun <= 16f &&
        Finite(s.sampleSpacing) && s.sampleSpacing >= 0.25f && s.sampleSpacing <= 1f &&
        Finite(s.maximumGradeDegrees) && s.maximumGradeDegrees > 0 && s.maximumGradeDegrees <= 35f &&
        Finite(s.maximumCrossSlopeDegrees) && s.maximumCrossSlopeDegrees > 0 && s.maximumCrossSlopeDegrees <= 15f &&
        Finite(s.maximumCut) && s.maximumCut >= 0 && s.maximumCut <= 2f &&
        Finite(s.maximumFill) && s.maximumFill >= 0 && s.maximumFill <= 2f &&
        Finite(s.contactTolerance) && s.contactTolerance >= 0 && s.contactTolerance <= 0.05f &&
        Finite(s.walkingSurfaceAboveTerrain) && s.walkingSurfaceAboveTerrain >= 0 && s.walkingSurfaceAboveTerrain <= 0.15f;

    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
