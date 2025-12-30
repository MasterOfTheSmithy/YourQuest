using System;
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

    // Cooldowns for various actions
    private readonly Dictionary<string, float> cooldowns = new()
    {
        { "movement", 0.75f },
        { "jump", 1.5f },
        { "combat", 0.4f },
        { "crouch", 1.0f },
        { "dodge", 1.0f },
        { "interact", 2f }
    };

    // Track last recorded time per action
    private readonly Dictionary<string, float> lastRecorded = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Don't fetch accumulator here; lazy getter will handle it
    }

    private bool CanRecord(string key)
    {
        return !lastRecorded.ContainsKey(key) || Time.time - lastRecorded[key] >= cooldowns[key];
    }

    private void Record(string key, ActionEvent ev)
    {
        if (!CanRecord(key)) return;

        lastRecorded[key] = Time.time;

        // Lazy fetch accumulator and record
        Accumulator?.RecordEvent(ev);
    }

    // Public record methods
    public void RecordMove() => Record("movement", new ActionEvent("movement", 0.4f));
    public void RecordJump() => Record("jump", new ActionEvent("jump", 0.6f));
    public void RecordCombat(GameObject target = null) => Record("combat", new ActionEvent("combat", 1.2f, target));
    public void RecordCrouch() => Record("crouch", new ActionEvent("crouch", 0.5f));
    public void RecordDodge() => Record("dodge", new ActionEvent("dodge", 0.7f));
    public void RecordInteract(GameObject target = null) => Record("interact", new ActionEvent("interact", 0.8f, target));

    // Debug helper to log skill info
    public void DebugAddSkill(string skillName, string description, SkillType type, string context = null, string environment = null)
    {
        string ctx = string.IsNullOrEmpty(context) ? "No context" : context;
        string env = string.IsNullOrEmpty(environment) ? "No environment" : environment;

        Debug.Log($"[Debug Skill] {skillName} ({type})\nContext: {ctx}\nEnvironment: {env}\nDescription: {description}");
    }
}
