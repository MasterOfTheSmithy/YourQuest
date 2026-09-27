using System;
using UnityEngine;

/// <summary>
/// Editor/development visualization for persisted world structure.
/// It is deliberately data-only and never changes generation results.
/// </summary>
[DisallowMultipleComponent]
public sealed class YQGeneratedWorldDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showRegions = true;
    [SerializeField] private bool showMacroFeatures = true;
    [SerializeField] private bool showLocations = true;
    [SerializeField] private bool showRoutes = true;
    [SerializeField] private bool showFootprints = true;
    [SerializeField] private bool showRuntimeIntegrity = true;

    private GeneratedWorldPlanRecord plan;

    public void Configure(GeneratedWorldPlanRecord worldPlan)
    {
        // note: The overlay holds the accepted in-memory plan only; it does not create a second spatial authority or mutate the save.
        plan = worldPlan;
    }

    private void OnDrawGizmosSelected()
    {
        // note: Runtime captures must show authored world surfaces only; editor-only location gizmos otherwise appear as magenta origin markers over the river.
        if (Application.isPlaying)
            return;
        if (plan == null)
            return;

        GeneratedSpatialWorldPlanRecord spatial =
            YQGeneratedWorldSpatialPlanner.GetSpatialPlan(plan);
        if (spatial == null)
            return;

        if (showRegions)
            DrawRegions(spatial);
        if (showMacroFeatures)
            DrawMacroFeatures(spatial);
        if (showRoutes)
            DrawRoutes(spatial);
        if (showLocations)
            DrawLocations(spatial);
        if (showRuntimeIntegrity)
            DrawRuntimeIntegrity();
    }

    private void DrawRegions(GeneratedSpatialWorldPlanRecord spatial)
    {
        for (int i = 0; i < spatial.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = spatial.regions[i];
            if (region == null)
                continue;
            Gizmos.color = Color.Lerp(Color.green, Color.red, region.danger);
            DrawCircle(new Vector3(region.centerX, 4f, region.centerZ), region.radius, 48);
        }
    }

    private void DrawMacroFeatures(GeneratedSpatialWorldPlanRecord spatial)
    {
        for (int i = 0; i < spatial.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = spatial.macroFeatures[i];
            if (feature == null)
                continue;
            Gizmos.color = feature.featureKind == "lake_basin"
                ? new Color(0.15f, 0.55f, 1f, 0.9f)
                : feature.featureKind == "valley"
                    ? new Color(0.65f, 0.9f, 0.25f, 0.9f)
                    : new Color(0.55f, 0.45f, 0.35f, 0.9f);
            DrawEllipse(
                new Vector3(feature.centerX, 5f, feature.centerZ),
                feature.radiusX,
                feature.radiusZ,
                feature.headingDegrees,
                48);
        }
    }

    private void DrawRoutes(GeneratedSpatialWorldPlanRecord spatial)
    {
        for (int i = 0; i < spatial.routes.Count; i++)
        {
            GeneratedSpatialRouteRecord route = spatial.routes[i];
            if (route == null || route.waypoints == null)
                continue;
            Gizmos.color = route.requiresBridge ? Color.cyan : new Color(0.85f, 0.65f, 0.24f, 1f);
            for (int pointIndex = 1; pointIndex < route.waypoints.Count; pointIndex++)
            {
                GeneratedSpatialPointRecord a = route.waypoints[pointIndex - 1];
                GeneratedSpatialPointRecord b = route.waypoints[pointIndex];
                Gizmos.DrawLine(new Vector3(a.x, 7f, a.z), new Vector3(b.x, 7f, b.z));
            }
        }
    }

    private void DrawLocations(GeneratedSpatialWorldPlanRecord spatial)
    {
        for (int i = 0; i < spatial.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = spatial.locations[i];
            if (location == null)
                continue;
            Gizmos.color = location.locationKind == "settlement"
                ? Color.yellow
                : location.locationKind == "hostile_site"
                    ? Color.red
                    : location.locationKind == "origin"
                        ? Color.magenta
                        : Color.white;
            Vector3 center = new Vector3(location.worldX, 9f, location.worldZ);
            Gizmos.DrawSphere(center, location.locationKind == "origin" ? 5f : 3f);
            if (showFootprints)
                DrawCircle(center, location.footprintRadius, 32);

            Vector3 direction = Quaternion.Euler(0f, location.entranceHeadingDegrees, 0f) * Vector3.forward;
            Gizmos.DrawLine(center, center + direction * Mathf.Min(24f, location.footprintRadius));
        }
    }

    private void DrawRuntimeIntegrity()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !LooksWalkable(renderer.gameObject.name))
                continue;
            Collider collider = renderer.GetComponent<Collider>();
            bool valid = collider != null && collider.enabled && !collider.isTrigger;
            Gizmos.color = valid
                ? new Color(0.1f, 1f, 0.25f, 0.75f)
                : new Color(1f, 0.08f, 0.05f, 0.95f);
            Bounds bounds = valid ? collider.bounds : renderer.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        LODGroup[] groups = GetComponentsInChildren<LODGroup>(true);
        Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.8f);
        for (int i = 0; i < groups.Length; i++)
        {
            LODGroup group = groups[i];
            if (group == null)
                continue;
            Gizmos.DrawWireSphere(
                group.transform.TransformPoint(group.localReferencePoint),
                Mathf.Max(0.1f, group.size * 0.5f));
        }
    }

    private static bool LooksWalkable(string objectName)
    {
        string name = (objectName ?? string.Empty).ToLowerInvariant();
        return name.Contains("floor") || name.Contains("ground") ||
               name.Contains("road") || name.Contains("path") ||
               name.Contains("street") || name.Contains("bridge") ||
               name.Contains("platform") || name.Contains("foundation") ||
               name.Contains("stair") || name.Contains("walkway");
    }

    private static void DrawCircle(Vector3 center, float radius, int segments)
    {
        DrawEllipse(center, radius, radius, 0f, segments);
    }

    private static void DrawEllipse(
        Vector3 center,
        float radiusX,
        float radiusZ,
        float heading,
        int segments)
    {
        Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
        Vector3 previous = center + rotation * new Vector3(radiusX, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + rotation * new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
