using System.Collections.Generic;
using UnityEngine;

public class EventAccumulator : MonoBehaviour
{
    public static EventAccumulator Instance;

    private readonly List<ActionEvent> actionEvents = new();
    private readonly List<SkillData> skills = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RecordEvent(ActionEvent ev)
    {
        actionEvents.Add(ev);
        Debug.Log($"[EventAccumulator] Recorded event: {ev.Verb}, significance {ev.Significance:0.00}");
    }

    public void AddSkill(SkillData skill)
    {
        skills.Add(skill);
        Debug.Log($"[EventAccumulator] Added skill: {skill.skillName}");
    }

    public IReadOnlyList<ActionEvent> GetEvents() => actionEvents;
    public IReadOnlyList<SkillData> GetSkills() => skills;
}
