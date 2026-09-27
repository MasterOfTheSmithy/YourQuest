using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// note: Editor-only construction evidence; this deliberately cannot mutate a saved terrain asset or live world.
public static class YQTerrainApproachConstructionReviewV2
{
    private const int MaximumPatchSamples = 4096;

    public static bool TryGradePreview(Terrain terrain, Vector3 start, Vector3 outward,
        YQTerrainApproachContractV2 contract, YQTerrainRepairProtectionV2 protection, out string failure)
    {
        failure = string.Empty;
        if (protection == null || terrain == null || terrain.terrainData == null ||
            !EditorSceneManager.IsPreviewSceneObject(terrain.gameObject) ||
            EditorUtility.IsPersistent(terrain.terrainData))
        {
            failure = "Construction review requires protection data and an unsaved terrain in an isolated preview scene.";
            return false;
        }
        if (!YQTerrainApproachV2.TryPlan(start, outward, contract,
                (Vector3 point, out float y) => YQTerrainApproachV2.TrySampleTerrain(terrain, point, out y),
                out var plan, out failure)) return false;
        // note: Already-connected terrain is not permission to walk into water or another reservation. Check the route and its one-metre handoff even when no heights change.
        int remainingChecks = 262144;
        Vector3 connectionEnd = plan.end + outward.normalized;
        int intervals = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, connectionEnd) / 0.5f));
        float halfWidth = contract.width * 0.5f;
        for (int index = 0; index < intervals; index++)
        {
            Vector3 stripFrom = Vector3.Lerp(start, connectionEnd, index / (float)intervals);
            Vector3 stripTo = Vector3.Lerp(start, connectionEnd, (index + 1) / (float)intervals);
            Rect strip = Rect.MinMaxRect(Mathf.Min(stripFrom.x, stripTo.x) - halfWidth, Mathf.Min(stripFrom.z, stripTo.z) - halfWidth,
                Mathf.Max(stripFrom.x, stripTo.x) + halfWidth, Mathf.Max(stripFrom.z, stripTo.z) + halfWidth);
            if (!protection.TryAllow(strip, ref remainingChecks, out failure)) return false;
        }
        if (!plan.terrainWorkRequired) return true;

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = data.size;
        int resolution = data.heightmapResolution;
        float dx = size.x / (resolution - 1), dz = size.z / (resolution - 1);
        // note: A full grid-cell diagonal protects the walkable strip from interpolation with its untouched neighbours.
        float guard = Mathf.Sqrt(dx * dx + dz * dz);
        if (guard > 3f)
        {
            failure = "Height grid is too coarse for this bounded entrance repair.";
            return false;
        }
        outward.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, outward);
        float coreWidth = contract.width * 0.5f + guard;
        float shoulder = Mathf.Max(2f, guard);
        float run = Vector3.Dot(plan.end - start, outward);
        float outerWidth = coreWidth + shoulder;
        Vector3 a = start - outward * (guard + shoulder) - lateral * outerWidth;
        Vector3 b = start - outward * (guard + shoulder) + lateral * outerWidth;
        Vector3 c = plan.end + outward * (guard + shoulder) - lateral * outerWidth;
        Vector3 d = plan.end + outward * (guard + shoulder) + lateral * outerWidth;
        Vector3 min = Vector3.Min(Vector3.Min(a, b), Vector3.Min(c, d));
        Vector3 max = Vector3.Max(Vector3.Max(a, b), Vector3.Max(c, d));
        int x0 = Mathf.FloorToInt((min.x - origin.x) / dx), z0 = Mathf.FloorToInt((min.z - origin.z) / dz);
        int x1 = Mathf.CeilToInt((max.x - origin.x) / dx), z1 = Mathf.CeilToInt((max.z - origin.z) / dz);
        int width = x1 - x0 + 1, depth = z1 - z0 + 1;
        if (x0 < 0 || z0 < 0 || x1 >= resolution || z1 >= resolution ||
            width <= 1 || depth <= 1 || (long)width * depth > MaximumPatchSamples)
        {
            failure = "Repair shoulders leave the terrain tile or exceed the height-sample budget.";
            return false;
        }
        // note: Reject every hole touched by the patch, not just holes coinciding with the three approach rails.
        int holes = data.holesResolution;
        int hx0 = Mathf.FloorToInt(x0 / (float)(resolution - 1) * holes);
        int hz0 = Mathf.FloorToInt(z0 / (float)(resolution - 1) * holes);
        int hx1 = Mathf.Min(holes - 1, Mathf.CeilToInt(x1 / (float)(resolution - 1) * holes));
        int hz1 = Mathf.Min(holes - 1, Mathf.CeilToInt(z1 / (float)(resolution - 1) * holes));
        if ((long)(hx1 - hx0 + 1) * (hz1 - hz0 + 1) > MaximumPatchSamples)
        {
            failure = "Repair hole coverage exceeds the review budget.";
            return false;
        }
        for (int z = hz0; z <= hz1; z++) for (int x = hx0; x <= hx1; x++)
            if (data.IsHole(x, z)) { failure = "Repair overlaps a terrain hole."; return false; }

        float[,] before = data.GetHeights(x0, z0, width, depth);
        float[,] after = (float[,])before.Clone();
        for (int z = 0; z < depth; z++) for (int x = 0; x < width; x++)
        {
            Vector3 point = origin + new Vector3((x0 + x) * dx, 0, (z0 + z) * dz);
            Vector3 delta = point - start;
            float along = Vector3.Dot(delta, outward), side = Mathf.Abs(Vector3.Dot(delta, lateral));
            float alongBlend = 1f - Mathf.SmoothStep(0, 1, Mathf.Max(-guard - along, along - run - guard) / shoulder);
            float sideBlend = 1f - Mathf.SmoothStep(0, 1, (side - coreWidth) / shoulder);
            float weight = alongBlend * sideBlend;
            if (weight <= 0) continue;
            float ground = origin.y + before[z, x] * size.y;
            // note: Keep a flat landing shelf across one grid cell, then grade toward the measured terrain join.
            float t = Mathf.Clamp01((along - guard) / Mathf.Max(0.25f, run - guard));
            float desired = Mathf.Lerp(start.y, plan.end.y, t);
            float change = (desired - ground) * weight;
            float value = (ground + change - origin.y) / size.y;
            if (!Finite(value) || value < 0 || value > 1 ||
                change > contract.maximumFill + 0.0001f || -change > contract.maximumCut + 0.0001f)
            {
                failure = "Repair exceeds its cut/fill or terrain-height limits.";
                return false;
            }
            after[z, x] = value;
            // note: A changed height vertex influences its adjoining quads. Protect that whole area so tiny ponds/foundations between vertices cannot be missed.
            if (value != before[z, x] && !protection.TryAllow(
                    new Rect(point.x - dx, point.z - dz, dx * 2, dz * 2), ref remainingChecks, out failure)) return false;
        }
        // note: Only one bounded patch is uploaded. Failed post-construction evidence restores the original heights before returning.
        bool accepted = false;
        try
        {
            data.SetHeightsDelayLOD(x0, z0, after);
            data.SyncHeightmap();
            accepted = YQTerrainApproachV2.TryPlan(start, outward, contract,
                (Vector3 point, out float y) => YQTerrainApproachV2.TrySampleTerrain(terrain, point, out y),
                out var built, out failure) && !built.terrainWorkRequired;
            if (!accepted && string.IsNullOrEmpty(failure)) failure = "Constructed heightfield still fails the full-width connection.";
            return accepted;
        }
        finally
        {
            if (!accepted)
            {
                data.SetHeightsDelayLOD(x0, z0, before);
                data.SyncHeightmap();
            }
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static string TestBoundedConstruction()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        try
        {
            // note: Match the production terrain's two-metre height spacing and 140m height range in a small disposable tile.
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(64, YQGeneratedWorldTerrain.TerrainHeight, 64) };
            float[,] flat = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) flat[z, x] = 0.5f;
            data.SetHeights(0, 0, flat);
            GameObject ground = new GameObject("ConstructionReviewTerrain");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(-32, -data.size.y * 0.5f - 0.4f, -32);
            Terrain terrain = ground.AddComponent<Terrain>();
            terrain.terrainData = data;
            var contract = new YQTerrainApproachContractV2();
            // note: This isolated fixture explicitly declares no other world features; real callers must supply their existing blueprint and neighboring bounds.
            if (!YQTerrainRepairProtectionV2.TryCreate(new YQSpatialBlueprintV2(), null, Array.Empty<Bounds>(), out var protection, out string failure)) return failure;
            if (!TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, protection, out failure))
                return "Coarse-grid fill failed: " + failure;
            float[,] built = data.GetHeights(0, 0, 33, 33);
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++)
                if ((x < 10 || x > 22 || z < 10 || z > 24) && built[z, x] != flat[z, x])
                    return "Construction modified heights outside its local patch.";
            if (!TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, protection, out failure) ||
                !Equal(built, data.GetHeights(0, 0, 33, 33))) return "Repeated grading changed accepted terrain.";
            var wet = new YQSpatialBlueprintV2();
            wet.hydrology.Add(new YQHydrologyFeatureV2 { hydrologyId = "flat-water", nominalWidth = 2,
                controlPoints = new System.Collections.Generic.List<YQBlueprintPointV2> { new YQBlueprintPointV2() } });
            if (!YQTerrainRepairProtectionV2.TryCreate(wet, null, Array.Empty<Bounds>(), out var wetProtection, out failure)) return failure;
            if (TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, wetProtection, out failure) ||
                !failure.Contains("protected water") || !Equal(built, data.GetHeights(0, 0, 33, 33)))
                return "Already-connected terrain bypassed water protection.";
            if (TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, null, out _))
                return "Missing protection context was accepted.";

            data.SetHeights(0, 0, flat);
            string reservationFailure = TestProtectionRejectionsOnFixture(terrain, Vector3.zero, Vector3.forward, contract);
            if (reservationFailure != null) return reservationFailure;
            ground.transform.position += Vector3.down * 2f;
            if (TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, protection, out _) ||
                !Equal(flat, data.GetHeights(0, 0, 33, 33))) return "Excessive fill was applied rather than rejected.";
            ground.transform.position += Vector3.up * 2f;
            bool[,] holes = new bool[data.holesResolution, data.holesResolution];
            for (int z = 0; z < holes.GetLength(0); z++) for (int x = 0; x < holes.GetLength(1); x++) holes[z, x] = true;
            holes[16, 18] = false; // note: Off-route shoulder hole must remain protected too.
            data.SetHoles(0, 0, holes);
            if (TryGradePreview(terrain, Vector3.zero, Vector3.forward, contract, protection, out _) ||
                !Equal(flat, data.GetHeights(0, 0, 33, 33))) return "Construction filled or graded across a protected terrain hole.";
            return null;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static bool Equal(float[,] a, float[,] b)
    {
        // note: Exact equality is intentional for the no-write/idempotence contract.
        for (int z = 0; z < a.GetLength(0); z++) for (int x = 0; x < a.GetLength(1); x++)
            if (a[z, x] != b[z, x]) return false;
        return true;
    }

    public static string TestProtectionRejectionsOnFixture(Terrain terrain, Vector3 start, Vector3 outward, YQTerrainApproachContractV2 contract)
    {
        if (terrain == null || !EditorSceneManager.IsPreviewSceneObject(terrain.gameObject) || terrain.terrainData == null ||
            EditorUtility.IsPersistent(terrain.terrainData) || terrain.terrainData.heightmapResolution != 33)
            return "Protection regression requires its isolated 33-point terrain fixture.";
        TerrainData data = terrain.terrainData;
        float[,] before = data.GetHeights(0, 0, 33, 33);
        Vector3 lateral = Vector3.Cross(Vector3.up, outward.normalized);
        Vector3 reserved = start + lateral * 5.5f + outward.normalized * 0.9f;
        // note: All four fixtures sit off the walkable center strip, deliberately between height vertices in the blended repair shoulder.
        for (int kind = 0; kind < 4; kind++)
        {
            var blueprint = new YQSpatialBlueprintV2();
            Bounds[] foundations = Array.Empty<Bounds>();
            string label;
            if (kind == 0)
            {
                label = "water";
                blueprint.hydrology.Add(new YQHydrologyFeatureV2 { hydrologyId = "review-water", nominalWidth = 0.2f,
                    controlPoints = new System.Collections.Generic.List<YQBlueprintPointV2> { new YQBlueprintPointV2 { x = reserved.x, z = reserved.z } } });
            }
            else if (kind == 1)
            {
                label = "route";
                blueprint.routes.Add(new YQRouteCorridorV2 { routeId = "review-route", width = 0.5f,
                    controlPoints = new System.Collections.Generic.List<YQBlueprintPointV2> { new YQBlueprintPointV2 { x = reserved.x, z = reserved.z } } });
            }
            else if (kind == 2)
            {
                label = "site";
                blueprint.sites.Add(new YQSiteAnchorV2 { siteId = "review-neighbor", x = reserved.x, z = reserved.z, reservedRadius = 4 });
            }
            else
            {
                label = "foundation";
                foundations = new[] { new Bounds(reserved + Vector3.up * 20, new Vector3(0.1f, 0.1f, 0.1f)) };
            }
            if (!YQTerrainRepairProtectionV2.TryCreate(blueprint, null, foundations, out var protection, out string failure)) return failure;
            bool graded = TryGradePreview(terrain, start, outward, contract, protection, out failure);
            bool unchanged = Equal(before, data.GetHeights(0, 0, 33, 33));
            if (!unchanged) data.SetHeights(0, 0, before); // note: Restore the disposable fixture even when a regression is detected.
            if (graded || !unchanged || !failure.Contains("protected " + label))
                return "Shoulder " + label + " was not rejected before a write: " + failure;
        }
        return null;
    }
}
