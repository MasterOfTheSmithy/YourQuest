using UnityEngine;

[CreateAssetMenu(fileName = "Skill", menuName = "Game/Skill")]
public class SkillSO : ScriptableObject
{
    public string skillName;
    public string description;
    public float level = 0f;
}

[CreateAssetMenu(fileName = "Title", menuName = "Game/Title")]
public class TitleSO : ScriptableObject
{
    public string titleName;
    public string description;
}

[CreateAssetMenu(fileName = "Quest", menuName = "Game/Quest")]
public class QuestSO : ScriptableObject
{
    public string questName;
    public string description;
    public bool isCompleted = false;
}

[CreateAssetMenu(fileName = "Reward", menuName = "Game/Reward")]
public class RewardSO : ScriptableObject
{
    public string rewardName;
    public string description;
    public int quantity;
}

[CreateAssetMenu(fileName = "Class", menuName = "Game/Class")]
public class ClassSO : ScriptableObject
{
    public string className;
    public string description;
}


