using UnityEngine;

public static class YQInteractableColliderUtility
{
    public static BoxCollider EnsureTightBox(
        GameObject root,
        Vector3 fallbackWorldSize,
        Vector3 fallbackWorldCenterOffset,
        Vector3 minWorldSize,
        Vector3 maxWorldSize)
    {
        if (root == null)
            return null;

        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null)
            box = root.AddComponent<BoxCollider>();

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider != box)
                collider.enabled = false;
        }

        Vector3 worldSize = fallbackWorldSize;
        Vector3 worldCenter = root.transform.TransformPoint(fallbackWorldCenterOffset);
        if (TryGetRendererBounds(root, out Bounds bounds))
        {
            worldSize = new Vector3(
                Mathf.Clamp(bounds.size.x, minWorldSize.x, maxWorldSize.x),
                Mathf.Clamp(bounds.size.y, minWorldSize.y, maxWorldSize.y),
                Mathf.Clamp(bounds.size.z, minWorldSize.z, maxWorldSize.z));
            worldCenter = new Vector3(bounds.center.x, bounds.min.y + worldSize.y * 0.5f, bounds.center.z);
        }

        Vector3 lossy = root.transform.lossyScale;
        box.size = new Vector3(
            SafeDivide(worldSize.x, Mathf.Abs(lossy.x)),
            SafeDivide(worldSize.y, Mathf.Abs(lossy.y)),
            SafeDivide(worldSize.z, Mathf.Abs(lossy.z)));
        box.center = root.transform.InverseTransformPoint(worldCenter);
        box.isTrigger = false;
        box.enabled = true;
        return box;
    }

    private static float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : null;
        bounds = default;
        bool initialized = false;
        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }
}
