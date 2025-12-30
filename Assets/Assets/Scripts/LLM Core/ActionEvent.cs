using UnityEngine;

[System.Serializable]
public class ActionEvent
{
    public string Verb;
    public GameObject Target;
    public Vector3 Position;
    public float Significance;

    public ActionEvent(string verb, float significance, GameObject target = null, Vector3? position = null)
    {
        Verb = verb;
        Significance = significance;
        Target = target;
        Position = position ?? Vector3.zero;
    }
}
