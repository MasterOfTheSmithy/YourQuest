using System.Collections.Generic;
using UnityEngine;

public class ActionRegistry : MonoBehaviour
{
    public static ActionRegistry Instance;

    [SerializeField] private int maxHistory = 50;
    private readonly List<ActionEvent> actionHistory = new();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void Record(ActionEvent action)
    {
        actionHistory.Add(action);
        if (actionHistory.Count > maxHistory)
            actionHistory.RemoveAt(0);
    }

    public void Clear() => actionHistory.Clear();

    public IReadOnlyList<ActionEvent> GetRecent() => actionHistory;
}
