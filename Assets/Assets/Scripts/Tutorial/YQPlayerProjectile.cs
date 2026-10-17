using UnityEngine;

// note: A released ranged attack owns one swept projectile; this component never spends resources or replays a release.
public sealed class YQPlayerProjectile : MonoBehaviour
{
    public static GameObject ArrowPrefab;
    // note: Disabled domain reload must not retain a prefab from a previous editor session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBinding() => ArrowPrefab = null;
    private Vector3 velocity;
    private int damage;
    private GameObject source;
    private PlayerState owner;
    private float expires;
    private readonly RaycastHit[] hits = new RaycastHit[24];

    public static void Spawn(Vector3 origin, Vector3 direction, int damage, GameObject source, PlayerState owner)
    {
        if (ArrowPrefab == null) ArrowPrefab = Resources.Load<YQPlayerActionAssets>("Player/YQPlayerActionAssets")?.arrowPrefab;
        GameObject root = new GameObject("YQ_RangedProjectile");
        root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
        if (ArrowPrefab != null)
        {
            GameObject arrow = Instantiate(ArrowPrefab, root.transform);
            YQRuntimeUrpMaterialRepair.RepairHierarchy(arrow);
            arrow.transform.localPosition = Vector3.zero;
            foreach (Collider shape in arrow.GetComponentsInChildren<Collider>(true)) shape.enabled = false;
            foreach (Rigidbody body in arrow.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        }
        else Debug.LogWarning("[YQPlayerProjectile] Approved arrow binding missing; projectile mechanics remain active without a replacement mesh.");
        var projectile = root.AddComponent<YQPlayerProjectile>();
        projectile.velocity = direction.normalized * 45f;
        projectile.damage = damage; projectile.source = source; projectile.owner = owner;
        projectile.expires = Time.time + 3f;
    }

    private void Update()
    {
        PlayerState current = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (Time.time >= expires || source == null || !ReferenceEquals(current, owner)) { Destroy(gameObject); return; }
        if (Time.deltaTime <= 0f || RuntimeModalUiBlocker.IsBlocked) return;
        Vector3 delta = velocity * Time.deltaTime + Physics.gravity * (.5f * Time.deltaTime * Time.deltaTime);
        velocity += Physics.gravity * Time.deltaTime;
        float length = delta.magnitude;
        int count = length > .001f ? Physics.SphereCastNonAlloc(transform.position, .035f, delta / length, hits, length, ~0, QueryTriggerInteraction.Ignore) : 0;
        if (count == hits.Length) { Destroy(gameObject); return; }
        Collider closest = null; float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (hits[i].collider != null && !hits[i].transform.IsChildOf(source.transform) && hits[i].distance < distance)
            { closest = hits[i].collider; distance = hits[i].distance; }
        if (closest != null)
        {
            YQInvestorEnemy enemy = closest.GetComponentInParent<YQInvestorEnemy>();
            if (enemy != null) { enemy.ReceiveHit(damage, source); owner?.IncCounter("combat:ranged_hit", 1f); }
            Destroy(gameObject);
            return;
        }
        transform.position += delta;
        if (velocity.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(velocity, Vector3.up);
    }
}
