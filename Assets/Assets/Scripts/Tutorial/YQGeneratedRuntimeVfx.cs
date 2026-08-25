using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-900)]
public sealed class YQGeneratedRuntimeVfx : MonoBehaviour
{
    public float equipmentPollInterval = 0.35f;

    private static readonly Dictionary<int, Material> s_particleMaterials = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> s_glowMaterials = new Dictionary<int, Material>();
    private static Shader s_particleShader;
    private static Shader s_glowShader;

    private GameObject _weaponAura;
    private string _lastWeaponSignature = string.Empty;
    private float _nextEquipmentPollTime;

    private enum GeneratedVisualFamily
    {
        Physical,
        Fire,
        Frost,
        Storm,
        Poison,
        Heal,
        Shield,
        Shadow,
        Earth,
        Air,
        Arcane
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQGeneratedRuntimeVfx>() != null)
            return;

        GameObject go = new GameObject("00__YQ_GeneratedRuntimeVfx");
        DontDestroyOnLoad(go);
        go.AddComponent<YQGeneratedRuntimeVfx>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextEquipmentPollTime)
            return;

        _nextEquipmentPollTime = Time.unscaledTime + Mathf.Max(0.1f, equipmentPollInterval);
        UpdateEquippedWeaponAura();
    }

    public static void SpawnMeleeSwing(Transform caster, string descriptor, bool hit)
    {
        if (caster == null)
            return;

        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        Color color = ResolveColor(descriptor, hit ? new Color(1f, 0.88f, 0.45f, 1f) : new Color(0.82f, 0.88f, 1f, 1f));
        Vector3 origin = caster.position + Vector3.up * 1.15f + caster.forward * 1.35f;
        GameObject root = new GameObject("YQ_MeleeSwingVfx");
        root.transform.position = origin;
        root.transform.rotation = Quaternion.LookRotation(caster.forward, Vector3.up);

        SpawnBurst(root.transform, color, 0.46f, 1.4f, hit ? 36 : 20, 0.32f, ParticleSystemShapeType.Cone);
        SpawnSlashArc(root.transform, color, hit);
        if (family != GeneratedVisualFamily.Physical)
            SpawnDescriptorAccent(root.transform, family, color, hit ? 0.82f : 0.58f);

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = hit ? 1.4f : 0.65f;
        light.range = hit ? 4.3f : 2.7f;
        light.shadows = LightShadows.None;
        UnityEngine.Object.Destroy(root, 0.85f);
    }

    public static bool TrySpawnSpellProjectile(Transform caster, string descriptor, int damage, GameObject source)
    {
        if (caster == null || !LooksProjectileLike(descriptor))
            return false;

        bool enemySource = source != null && source.GetComponent<YQInvestorEnemy>() != null;
        Vector3 forward = ResolveProjectileForward(caster, enemySource);
        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        Color color = ResolveColor(descriptor, new Color(1f, 0.48f, 0.18f, 1f));
        Vector3 origin = caster.position + Vector3.up * (enemySource ? 0.95f : 1.18f) + forward * (enemySource ? 0.55f : 0.78f);
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "YQ_GeneratedSpellProjectile";
        projectile.transform.position = origin;
        projectile.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        projectile.transform.localScale = ResolveProjectileScale(family);

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateGlowMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Collider collider = projectile.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;

        Rigidbody rb = projectile.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
        trail.time = family == GeneratedVisualFamily.Storm ? 0.18f : 0.28f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = family == GeneratedVisualFamily.Frost ? 0.14f : 0.20f;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        // note: Shared cached materials avoid one hidden material allocation per projectile trail.
        trail.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.62f));
        SpawnProjectileAura(projectile.transform, family, color);

        Light light = projectile.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = family == GeneratedVisualFamily.Storm ? 1.9f : 1.35f;
        light.range = family == GeneratedVisualFamily.Shadow ? 3.5f : 4.6f;
        light.shadows = LightShadows.None;

        YQGeneratedProjectileVfx mover = projectile.AddComponent<YQGeneratedProjectileVfx>();
        mover.velocity = forward * ResolveProjectileSpeed(family);
        mover.damage = Mathf.Max(1, damage);
        mover.source = source;
        mover.impactColor = color;
        mover.maxLifetime = family == GeneratedVisualFamily.Storm ? 1.05f : 1.5f;
        return true;
    }

    public static void SpawnOriginManifestation(Vector3 position, string descriptor)
    {
        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        Color color = ResolveColor(descriptor, new Color(1f, 0.92f, 0.62f, 1f));
        GameObject root = new GameObject("YQ_OriginManifestationVfx");
        root.transform.position = position;
        SpawnBurst(root.transform, color, 0.82f, 1.55f, 72, 0.8f, ParticleSystemShapeType.Sphere);
        SpawnPulseRing(root.transform, color, 1.35f);
        SpawnVerticalRing(root.transform, color, 0.82f, 1.65f, "OriginThresholdHalo");
        SpawnDescriptorAccent(root.transform, family, color, 1.15f);

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 2.4f;
        light.range = 6.5f;
        light.shadows = LightShadows.None;
        UnityEngine.Object.Destroy(root, 1.35f);
    }

    public static void SpawnSpellPulse(Transform caster, string descriptor, float radius)
    {
        if (caster == null)
            return;

        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        Color color = ResolveColor(descriptor, new Color(0.42f, 0.72f, 1f, 1f));
        GameObject root = new GameObject("YQ_SpellPulseVfx");
        root.transform.position = caster.position + Vector3.up * 0.85f;
        int burstCount = family == GeneratedVisualFamily.Heal || family == GeneratedVisualFamily.Shield ? 34 : 56;
        float burstSpeed = family == GeneratedVisualFamily.Shield ? 0.85f : 1.8f;
        SpawnBurst(root.transform, color, Mathf.Max(0.55f, radius * 0.16f), burstSpeed, burstCount, 0.62f, ParticleSystemShapeType.Sphere);
        SpawnPulseRing(root.transform, color, Mathf.Max(1.2f, radius * 0.45f));
        SpawnDescriptorAccent(root.transform, family, color, Mathf.Max(1f, radius * 0.34f));

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 1.45f;
        light.range = Mathf.Max(4f, radius);
        light.shadows = LightShadows.None;
        UnityEngine.Object.Destroy(root, 0.72f);
    }

    private static Vector3 ResolveProjectileForward(Transform caster, bool enemySource)
    {
        Vector3 forward = caster != null ? caster.forward : Vector3.forward;
        if (!enemySource && Camera.main != null)
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            if (cameraForward.sqrMagnitude > 0.001f && Vector3.Dot(cameraForward.normalized, forward.normalized) > -0.25f)
                forward = cameraForward;
        }

        forward.y = Mathf.Clamp(forward.y, -0.28f, enemySource ? 0.18f : 0.34f);
        if (forward.sqrMagnitude < 0.001f)
            forward = caster != null ? caster.forward : Vector3.forward;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        return forward.normalized;
    }

    public static void SpawnConsumableUse(Transform user, InventoryItemRecord item)
    {
        if (user == null)
            return;

        string descriptor = item != null ? item.displayName + " " + item.description + " " + item.effectKey : string.Empty;
        Color color = ResolveColor(descriptor, new Color(0.45f, 1f, 0.66f, 1f));
        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        GameObject root = new GameObject("YQ_ConsumableUseVfx");
        root.transform.position = user.position + Vector3.up * 1.0f;
        SpawnBurst(root.transform, color, 0.62f, 1.1f, 42, 0.7f, ParticleSystemShapeType.Sphere);
        SpawnDescriptorAccent(root.transform, family, color, 0.8f);
        UnityEngine.Object.Destroy(root, 1.2f);
    }

    private void UpdateEquippedWeaponAura()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        InventoryItemRecord weapon = state != null ? state.GetEquippedItem("weapon") : null;
        string signature = weapon != null ? weapon.itemId + ":" + BuildDescriptor(weapon) : string.Empty;
        if (string.Equals(signature, _lastWeaponSignature, StringComparison.OrdinalIgnoreCase))
            return;

        _lastWeaponSignature = signature;
        if (_weaponAura != null)
        {
            UnityEngine.Object.Destroy(_weaponAura);
            _weaponAura = null;
        }

        if (weapon == null)
            return;

        string descriptor = BuildDescriptor(weapon);
        if (!LooksElemental(descriptor))
            return;

        Transform attach = Camera.main != null ? Camera.main.transform : (GameObject.FindWithTag("Player") != null ? GameObject.FindWithTag("Player").transform : null);
        if (attach == null)
            return;

        Color color = ResolveColor(descriptor, new Color(1f, 0.46f, 0.16f, 1f));
        _weaponAura = new GameObject("YQ_EquippedWeaponAura");
        _weaponAura.transform.SetParent(attach, false);
        _weaponAura.transform.localPosition = Camera.main != null ? new Vector3(0.46f, -0.42f, 0.84f) : new Vector3(0.55f, 1.0f, 0.45f);
        _weaponAura.transform.localRotation = Quaternion.identity;
        SpawnLoop(_weaponAura.transform, color);
    }

    private static string BuildDescriptor(InventoryItemRecord item)
    {
        if (item == null)
            return string.Empty;

        return (item.displayName ?? string.Empty) + " " +
               (item.description ?? string.Empty) + " " +
               (item.effectKey ?? string.Empty) + " " +
               (item.familyKey ?? string.Empty);
    }

    private static bool LooksProjectileLike(string descriptor)
    {
        string d = Normalize(descriptor);
        if (HasAny(d, "fireball", "iceball", "projectile", "bolt", "missile", "lance", "beam", "ray", "ball", "arrow", "shot"))
            return true;

        return HasAny(d, "fire", "flame", "ember", "frost", "ice", "storm", "arc", "lightning", "electric", "poison", "acid", "shadow", "void") &&
               !HasAny(d, "pulse", "shield", "guard", "ward", "skin", "aura", "ring", "heal", "restore", "courage");
    }

    private static bool LooksElemental(string descriptor)
    {
        GeneratedVisualFamily family = ClassifyVisual(descriptor);
        return family != GeneratedVisualFamily.Physical && family != GeneratedVisualFamily.Arcane;
    }

    private static Color ResolveColor(string descriptor, Color fallback)
    {
        switch (ClassifyVisual(descriptor))
        {
            case GeneratedVisualFamily.Fire:
                return new Color(1f, 0.42f, 0.12f, 1f);
            case GeneratedVisualFamily.Frost:
                return new Color(0.45f, 0.95f, 1f, 1f);
            case GeneratedVisualFamily.Storm:
                return new Color(0.38f, 0.72f, 1f, 1f);
            case GeneratedVisualFamily.Poison:
                return new Color(0.45f, 1f, 0.35f, 1f);
            case GeneratedVisualFamily.Heal:
                return new Color(0.65f, 1f, 0.72f, 1f);
            case GeneratedVisualFamily.Shield:
                return new Color(0.62f, 0.86f, 1f, 1f);
            case GeneratedVisualFamily.Shadow:
                return new Color(0.64f, 0.42f, 1f, 1f);
            case GeneratedVisualFamily.Earth:
                return new Color(0.74f, 0.66f, 0.46f, 1f);
            case GeneratedVisualFamily.Air:
                return new Color(0.72f, 1f, 0.96f, 1f);
            case GeneratedVisualFamily.Arcane:
                return new Color(0.58f, 0.82f, 1f, 1f);
            default:
                return fallback;
        }
    }

    private static void SpawnBurst(Transform root, Color color, float size, float speed, int count, float lifetime, ParticleSystemShapeType shapeType)
    {
        ParticleSystem ps =
            root.GetComponent<ParticleSystem>();

        if (ps == null)
        {
            // note: Burst helpers may share a temporary VFX root, so only add ParticleSystem when one is not already present.
            ps =
                root.gameObject.AddComponent<ParticleSystem>();
        }

        if (ps == null)
            return;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 160)) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = shapeType;
        shape.radius = Mathf.Max(0.05f, size);
        if (shapeType == ParticleSystemShapeType.Cone)
            shape.angle = 34f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        // note: The particle material is immutable after construction and safe to share across short-lived effects.
        renderer.sharedMaterial = CreateParticleMaterial(color);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    private static void SpawnSlashArc(Transform root, Color color, bool hit)
    {
        GameObject arc = new GameObject("MeleeSlashArc");
        arc.transform.SetParent(root, false);
        arc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        arc.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

        LineRenderer line = arc.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 18;
        line.widthMultiplier = hit ? 0.085f : 0.055f;
        line.numCapVertices = 4;
        // note: Reuse the cached immutable glow material instead of instancing it for every line effect.
        line.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.76f));
        line.startColor = new Color(color.r, color.g, color.b, 0.92f);
        line.endColor = new Color(color.r, color.g, color.b, 0f);

        float radius = hit ? 0.92f : 0.72f;
        for (int i = 0; i < line.positionCount; i++)
        {
            float t = i / (float)(line.positionCount - 1);
            float angle = Mathf.Lerp(-62f, 64f, t) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * 0.28f, 0.15f + Mathf.Cos(angle) * radius * 0.28f));
        }
    }

    private static void SpawnPulseRing(Transform root, Color color, float radius)
    {
        GameObject ring = new GameObject("SpellPulseRing");
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = Vector3.down * 0.55f;
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 56;
        line.widthMultiplier = 0.06f;
        line.numCapVertices = 3;
        // note: Reuse the cached immutable glow material instead of instancing it for every line effect.
        line.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.62f));
        line.startColor = new Color(color.r, color.g, color.b, 0.82f);
        line.endColor = new Color(color.r, color.g, color.b, 0.82f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        YQLineRendererLifetime lifetime = ring.AddComponent<YQLineRendererLifetime>();
        lifetime.lifeSeconds = 0.48f;
        lifetime.line = line;
    }

    private static void SpawnLoop(Transform root, Color color)
    {
        ParticleSystem ps = root.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = 1.6f;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.055f;
        main.startSize = 0.055f;
        main.startColor = new Color(color.r, color.g, color.b, 0.22f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 2f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.075f;
        shape.angle = 10f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        // note: Shared cached particle materials prevent combat VFX from leaking renderer-owned material copies.
        renderer.sharedMaterial = CreateParticleMaterial(color);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Light light = root.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 0.06f;
        light.range = 0.85f;
        light.shadows = LightShadows.None;
        ps.Play();
    }

    private static void SpawnProjectileAura(Transform root, GeneratedVisualFamily family, Color color)
    {
        ParticleSystem ps = root.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.duration = 0.9f;
        main.loop = true;
        main.startLifetime = family == GeneratedVisualFamily.Storm ? 0.12f : 0.22f;
        main.startSpeed = family == GeneratedVisualFamily.Fire ? 0.16f : 0.055f;
        main.startSize = family == GeneratedVisualFamily.Frost ? 0.045f : 0.075f;
        main.startColor = new Color(color.r, color.g, color.b, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = family == GeneratedVisualFamily.Storm ? 11f : 7f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.17f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            // note: Short-lived particle systems must share the cached material to keep burst combat allocation-free.
            renderer.sharedMaterial = CreateParticleMaterial(color);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        ps.Play();
    }

    private static void SpawnDescriptorAccent(Transform root, GeneratedVisualFamily family, Color color, float scale)
    {
        scale = Mathf.Clamp(scale, 0.35f, 2.8f);
        switch (family)
        {
            case GeneratedVisualFamily.Fire:
                SpawnBurst(root, color, 0.18f * scale, 0.65f, 12, 0.44f, ParticleSystemShapeType.Cone);
                SpawnPulseRing(root, color, 0.62f * scale);
                break;
            case GeneratedVisualFamily.Frost:
                SpawnRadialLines(root, color, 7, 0.62f * scale, 0.22f * scale, "FrostShardAccent");
                break;
            case GeneratedVisualFamily.Storm:
                SpawnArcLines(root, color, 3, 0.75f * scale);
                break;
            case GeneratedVisualFamily.Poison:
                SpawnBurst(root, color, 0.24f * scale, 0.34f, 18, 0.9f, ParticleSystemShapeType.Sphere);
                break;
            case GeneratedVisualFamily.Heal:
                SpawnPulseRing(root, color, 0.72f * scale);
                SpawnVerticalRing(root, color, 0.46f * scale, 0.76f * scale, "HealingHalo");
                break;
            case GeneratedVisualFamily.Shield:
                SpawnPulseRing(root, color, 0.82f * scale);
                SpawnVerticalRing(root, color, 0.62f * scale, 0.95f * scale, "ShieldContour");
                break;
            case GeneratedVisualFamily.Shadow:
                SpawnBurst(root, color, 0.28f * scale, 0.28f, 18, 0.74f, ParticleSystemShapeType.Sphere);
                SpawnPulseRing(root, color, 0.58f * scale);
                break;
            case GeneratedVisualFamily.Earth:
                SpawnPulseRing(root, color, 0.72f * scale);
                SpawnRadialLines(root, color, 5, 0.55f * scale, 0.04f, "EarthCrackAccent");
                break;
            case GeneratedVisualFamily.Air:
                SpawnPulseRing(root, color, 0.74f * scale);
                SpawnRadialLines(root, color, 6, 0.7f * scale, 0.18f * scale, "AirCurrentAccent");
                break;
            case GeneratedVisualFamily.Arcane:
                SpawnPulseRing(root, color, 0.64f * scale);
                break;
        }
    }

    private static void SpawnRadialLines(Transform root, Color color, int count, float radius, float height, string name)
    {
        count = Mathf.Clamp(count, 3, 12);
        for (int i = 0; i < count; i++)
        {
            GameObject lineGo = new GameObject(name + "_" + i);
            lineGo.transform.SetParent(root, false);
            LineRenderer line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            // note: This visual uses color in the vertex stream, so the immutable cached material can be shared.
            line.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.58f));
            line.startColor = new Color(color.r, color.g, color.b, 0.7f);
            line.endColor = new Color(color.r, color.g, color.b, 0f);

            float angle = i / (float)count * Mathf.PI * 2f;
            Vector3 end = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, end);

            YQLineRendererLifetime lifetime = lineGo.AddComponent<YQLineRendererLifetime>();
            lifetime.lifeSeconds = 0.5f;
            lifetime.line = line;
        }
    }

    private static void SpawnArcLines(Transform root, Color color, int count, float radius)
    {
        count = Mathf.Clamp(count, 2, 6);
        for (int i = 0; i < count; i++)
        {
            GameObject lineGo = new GameObject("StormArcAccent_" + i);
            lineGo.transform.SetParent(root, false);
            lineGo.transform.localRotation = Quaternion.Euler(0f, i * (360f / count), 0f);
            LineRenderer line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 4;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            // note: This visual uses color in the vertex stream, so the immutable cached material can be shared.
            line.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.72f));
            line.startColor = new Color(color.r, color.g, color.b, 0.9f);
            line.endColor = new Color(color.r, color.g, color.b, 0f);

            line.SetPosition(0, new Vector3(-radius * 0.38f, 0.04f, 0f));
            line.SetPosition(1, new Vector3(-radius * 0.08f, radius * 0.22f, radius * 0.12f));
            line.SetPosition(2, new Vector3(radius * 0.14f, -radius * 0.02f, -radius * 0.1f));
            line.SetPosition(3, new Vector3(radius * 0.46f, radius * 0.18f, 0f));

            YQLineRendererLifetime lifetime = lineGo.AddComponent<YQLineRendererLifetime>();
            lifetime.lifeSeconds = 0.38f;
            lifetime.line = line;
        }
    }

    private static void SpawnVerticalRing(Transform root, Color color, float radius, float height, string name)
    {
        GameObject ring = new GameObject(name);
        ring.transform.SetParent(root, false);

        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.widthMultiplier = 0.045f;
        line.numCapVertices = 3;
        // note: Reuse the cached immutable glow material instead of creating a renderer-local copy.
        line.sharedMaterial = CreateGlowMaterial(new Color(color.r, color.g, color.b, 0.48f));
        line.startColor = new Color(color.r, color.g, color.b, 0.58f);
        line.endColor = new Color(color.r, color.g, color.b, 0.58f);

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * height, 0f));
        }

        YQLineRendererLifetime lifetime = ring.AddComponent<YQLineRendererLifetime>();
        lifetime.lifeSeconds = 0.5f;
        lifetime.line = line;
    }

    private static Vector3 ResolveProjectileScale(GeneratedVisualFamily family)
    {
        switch (family)
        {
            case GeneratedVisualFamily.Frost:
                return new Vector3(0.18f, 0.18f, 0.54f);
            case GeneratedVisualFamily.Storm:
                return Vector3.one * 0.22f;
            case GeneratedVisualFamily.Poison:
            case GeneratedVisualFamily.Shadow:
                return Vector3.one * 0.38f;
            default:
                return Vector3.one * 0.32f;
        }
    }

    private static float ResolveProjectileSpeed(GeneratedVisualFamily family)
    {
        switch (family)
        {
            case GeneratedVisualFamily.Storm:
                return 32f;
            case GeneratedVisualFamily.Frost:
                return 21f;
            case GeneratedVisualFamily.Poison:
            case GeneratedVisualFamily.Shadow:
                return 18f;
            default:
                return 24f;
        }
    }

    private static GeneratedVisualFamily ClassifyVisual(string descriptor)
    {
        string d = Normalize(descriptor);
        if (string.IsNullOrWhiteSpace(d))
            return GeneratedVisualFamily.Arcane;
        if (HasAny(d, "shield", "guard", "ward", "barrier", "protection", "protect", "stone skin"))
            return GeneratedVisualFamily.Shield;
        if (HasAny(d, "heal", "healing", "holy", "restore", "first aid", "courage"))
            return GeneratedVisualFamily.Heal;
        if (HasAny(d, "fire", "flame", "ember", "torch", "molten", "scorch", "burn"))
            return GeneratedVisualFamily.Fire;
        if (HasAny(d, "ice", "frost", "cold", "glacier"))
            return GeneratedVisualFamily.Frost;
        if (HasAny(d, "storm", "arc", "lightning", "electric", "spark", "plasma"))
            return GeneratedVisualFamily.Storm;
        if (HasAny(d, "poison", "venom", "toxic", "acid"))
            return GeneratedVisualFamily.Poison;
        if (HasAny(d, "grave", "shadow", "void", "soul", "vampiric", "dark", "curse", "fear"))
            return GeneratedVisualFamily.Shadow;
        if (HasAny(d, "stone", "earth", "ground", "rock", "shatter", "timber", "wood", "root", "bark"))
            return GeneratedVisualFamily.Earth;
        if (HasAny(d, "air", "wind", "gale", "gust", "sweep", "sweeping", "speed", "haste", "kick", "vault", "leap"))
            return GeneratedVisualFamily.Air;
        if (HasAny(d, "magic", "arcane", "echo", "spirit", "mana", "rune", "missile", "pulse", "beam"))
            return GeneratedVisualFamily.Arcane;
        return GeneratedVisualFamily.Physical;
    }

    private static bool HasAny(string value, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(value) || terms == null)
            return false;

        for (int i = 0; i < terms.Length; i++)
        {
            string term = terms[i];
            if (!string.IsNullOrWhiteSpace(term) && value.Contains(term))
                return true;
        }

        return false;
    }

    internal static Material CreateParticleMaterial(Color color)
    {
        int key = BuildMaterialKey(color, 11);
        if (s_particleMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Shader shader = ResolveParticleShader();
        Material material = new Material(shader);
        material.name = "YQ_ProceduralParticleVfx";
        material.hideFlags = HideFlags.DontSave;
        SetMaterialColor(material, new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.86f)));
        ConfigureTransparent(material, true);
        s_particleMaterials[key] = material;
        return material;
    }

    private static Material CreateGlowMaterial(Color color)
    {
        int key = BuildMaterialKey(color, 23);
        if (s_glowMaterials.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        Shader shader = ResolveGlowShader();
        Material material = new Material(shader);
        material.name = "YQ_ProceduralGlowVfx";
        material.hideFlags = HideFlags.DontSave;
        SetMaterialColor(material, color);
        ConfigureTransparent(material, false);
        s_glowMaterials[key] = material;
        return material;
    }

    private static Shader ResolveParticleShader()
    {
        if (s_particleShader != null)
            return s_particleShader;

        s_particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s_particleShader == null)
            s_particleShader = Shader.Find("Particles/Standard Unlit");
        if (s_particleShader == null)
            s_particleShader = Shader.Find("Unlit/Color");
        if (s_particleShader == null)
            s_particleShader = Shader.Find("Standard");
        return s_particleShader;
    }

    private static Shader ResolveGlowShader()
    {
        if (s_glowShader != null)
            return s_glowShader;

        s_glowShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (s_glowShader == null)
            s_glowShader = Shader.Find("Unlit/Color");
        if (s_glowShader == null)
            s_glowShader = Shader.Find("Standard");
        return s_glowShader;
    }

    private static int BuildMaterialKey(Color color, int salt)
    {
        Color32 packed = color;
        unchecked
        {
            int hash = salt;
            hash = hash * 397 ^ packed.r;
            hash = hash * 397 ^ packed.g;
            hash = hash * 397 ^ packed.b;
            hash = hash * 397 ^ packed.a;
            return hash;
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        Color emission = new Color(Mathf.Clamp01(color.r * 2.6f), Mathf.Clamp01(color.g * 2.6f), Mathf.Clamp01(color.b * 2.6f), color.a);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
        }
        if (material.HasProperty("_EmissiveColor"))
        {
            material.SetColor("_EmissiveColor", emission);
            material.EnableKeyword("_EMISSION");
        }
    }

    private static void ConfigureTransparent(Material material, bool additive)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}

public sealed class YQGeneratedProjectileVfx : MonoBehaviour
{
    public Vector3 velocity;
    public int damage;
    public GameObject source;
    public Color impactColor = Color.white;
    public float maxLifetime = 1.5f;

    private readonly Collider[] _hits = new Collider[12];
    private float _destroyAt;

    private void Awake()
    {
        _destroyAt = Time.time + Mathf.Max(0.1f, maxLifetime);
    }

    private void Update()
    {
        transform.position += velocity * Time.deltaTime;
        Vector3 position = transform.position;
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) || position.y < -8f || position.y > 80f)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time >= _destroyAt)
        {
            Destroy(gameObject);
            return;
        }

        int count = Physics.OverlapSphereNonAlloc(transform.position, 0.38f, _hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i];
            if (hit == null || source != null && hit.transform.IsChildOf(source.transform))
                continue;

            if (TryApplyDamage(hit))
            {
                SpawnImpact();
                Destroy(gameObject);
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || source != null && other.transform.IsChildOf(source.transform))
            return;

        if (TryApplyDamage(other))
        {
            SpawnImpact();
            Destroy(gameObject);
        }
    }

    private bool TryApplyDamage(Collider other)
    {
        if (other == null)
            return false;

        bool enemySource = source != null && source.GetComponent<YQInvestorEnemy>() != null;
        if (enemySource)
        {
            YQInvestorCombat player = other.GetComponentInParent<YQInvestorCombat>();
            if (player == null)
                return false;

            player.ReceiveDamage(Mathf.Max(1, damage), source);
            return true;
        }

        YQInvestorEnemy enemy = other.GetComponentInParent<YQInvestorEnemy>();
        if (enemy == null)
            return false;

        enemy.ReceiveHit(Mathf.Max(1, damage), source);
        return true;
    }

    private void SpawnImpact()
    {
        GameObject root = new GameObject("YQ_ProjectileImpactVfx");
        root.transform.position = transform.position;
        ParticleSystem ps = root.AddComponent<ParticleSystem>();
        // note: A newly added particle system can begin playing before its modules are configured, which makes duration assignment warn every impact.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = 0.32f;
        main.startSpeed = 1.8f;
        main.startSize = 0.42f;
        main.startColor = impactColor;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)36) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.28f;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            // note: Impact colors are baked into a bounded shared cache, avoiding a new material for every collision.
            renderer.sharedMaterial = YQGeneratedRuntimeVfx.CreateParticleMaterial(impactColor);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        ps.Play();

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = impactColor;
        light.intensity = 1.2f;
        light.range = 3.4f;
        light.shadows = LightShadows.None;
        Destroy(root, 0.8f);
    }
}

public sealed class YQLineRendererLifetime : MonoBehaviour
{
    public LineRenderer line;
    public float lifeSeconds = 0.5f;

    private Color _startA;
    private Color _startB;
    private float _born;

    private void Awake()
    {
        if (line == null)
            line = GetComponent<LineRenderer>();
        _born = Time.time;
        if (line != null)
        {
            _startA = line.startColor;
            _startB = line.endColor;
        }
        Destroy(gameObject, Mathf.Max(0.05f, lifeSeconds + 0.05f));
    }

    private void Update()
    {
        if (line == null)
            return;

        float t = Mathf.Clamp01((Time.time - _born) / Mathf.Max(0.05f, lifeSeconds));
        float alpha = 1f - t;
        line.startColor = new Color(_startA.r, _startA.g, _startA.b, _startA.a * alpha);
        line.endColor = new Color(_startB.r, _startB.g, _startB.b, _startB.a * alpha);
    }
}
