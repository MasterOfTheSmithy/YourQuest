using System.Collections.Generic;
using UnityEngine;

public class ActionRecorder : MonoBehaviour
{
    public static ActionRecorder Instance { get; private set; }

    // Lazy reference to EventAccumulator to avoid null on Awake
    private EventAccumulator accumulator;
    private EventAccumulator Accumulator
    {
        get
        {
            if (accumulator == null)
            {
                accumulator = EventAccumulator.Instance;
                if (accumulator == null)
                    Debug.LogWarning("ActionRecorder: EventAccumulator instance still null!");
            }
            return accumulator;
        }
    }

    private ActionRegistry registry;
    private ActionRegistry Registry
    {
        get
        {
            if (registry == null)
            {
                registry = ActionRegistry.Instance;
                if (registry == null)
                    Debug.LogWarning("ActionRecorder: ActionRegistry instance still null!");
            }
            return registry;
        }
    }

    [Header("Context Bucketing")]
    public float cellSize = 25f;

    [Header("Cooldowns (seconds)")]
    public float moveCooldown = 0.35f;
    public float jumpCooldown = 0.75f;
    public float combatCooldown = 0.25f;
    public float crouchCooldown = 0.6f;
    public float dodgeCooldown = 0.5f;
    public float interactCooldown = 0.4f;

    private readonly Dictionary<string, float> lastRecorded = new Dictionary<string, float>();
    private Dictionary<string, float> cooldowns;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        cooldowns = new Dictionary<string, float>
        {
            { "movement", moveCooldown },
            { "jump", jumpCooldown },
            { "combat", combatCooldown },
            { "crouch", crouchCooldown },
            { "dodge", dodgeCooldown },
            { "interact", interactCooldown }
        };
    }

    private bool CanRecord(string key)
    {
        // Safe default if you forget to configure a cooldown
        float cd = cooldowns.TryGetValue(key, out float v) ? v : 0.5f;
        return !lastRecorded.ContainsKey(key) || Time.time - lastRecorded[key] >= cd;
    }

    /// <summary>
    /// Fallback region bucket if no semantic region exists.
    /// </summary>
    private string ComputeGridRegionId(Vector3 pos)
    {
        int cx = Mathf.FloorToInt(pos.x / Mathf.Max(0.01f, cellSize));
        int cz = Mathf.FloorToInt(pos.z / Mathf.Max(0.01f, cellSize));
        return $"x{cx}_z{cz}";
    }

    /// <summary>
    /// Preferred semantic region id/name from PlayerContext, falling back to grid bucket.
    /// </summary>
    private void GetRegionContext(Vector3 pos, out string regionId, out string regionName)
    {
        regionId = null;
        regionName = null;

        var ctx = PlayerContext.Instance;
        if (ctx != null)
        {
            if (!string.IsNullOrWhiteSpace(ctx.SemanticRegionId))
                regionId = ctx.SemanticRegionId;

            if (!string.IsNullOrWhiteSpace(ctx.SemanticRegionName))
                regionName = ctx.SemanticRegionName;
        }

        if (string.IsNullOrWhiteSpace(regionId))
            regionId = ComputeGridRegionId(pos);
    }

    private void Record(string key, ActionEvent ev)
    {
        if (!CanRecord(key)) return;

        // Ensure registry is populated even if Accumulator isn't ready.
        if (Registry != null) Registry.Record(ev);

        if (Accumulator != null)
            Accumulator.AddEvent(ev);

        lastRecorded[key] = Time.time;
    }

    public void RecordMove()
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("movement", new ActionEvent("movement", 0.4f, null, pos, null, regionId, regionName));
    }

    public void RecordJump()
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("jump", new ActionEvent("jump", 0.6f, null, pos, null, regionId, regionName));
    }

    public void RecordCombat(GameObject target)
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("combat", new ActionEvent("combat", 1.2f, target, pos, null, regionId, regionName));
    }

    public void RecordCrouch()
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("crouch", new ActionEvent("crouch", 0.5f, null, pos, null, regionId, regionName));
    }

    public void RecordDodge()
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("dodge", new ActionEvent("dodge", 0.7f, null, pos, null, regionId, regionName));
    }

    public void RecordInteract(GameObject target)
    {
        var pos = transform.position;
        GetRegionContext(pos, out var regionId, out var regionName);
        Record("interact", new ActionEvent("interact", 0.8f, target, pos, null, regionId, regionName));
    }
}
