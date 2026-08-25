// Assets/Assets/Scripts/Tutorial/YourQuestTutorialEnemySpawner.cs
using System.Collections.Generic;
using UnityEngine;

public class YourQuestTutorialEnemySpawner : MonoBehaviour
{
    public int enemyCount = 4;
    public string factionId = "wild_hollows";
    public string semanticRegionId = "region_unknown";

    private readonly List<YourQuestTutorialEnemy> _alive = new List<YourQuestTutorialEnemy>();

    public void SpawnNow()
    {
        if (_alive.Count > 0) return;

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 8f;
            offset.y = 0f;
            Vector3 pos = transform.position + offset + Vector3.up;

            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "TutorialEnemy_" + i;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1f, 1.1f, 1f);

            var info = go.AddComponent<EntityInfo>();
            info.entityId = semanticRegionId + "_enemy_" + i;
            info.displayName = "Echo Marauder";
            info.level = 2;
            info.factionId = factionId;
            info.hostility = Hostility.Hostile;
            info.isNotable = false;
            info.tags = new [] { "enemy", "echo", "tutorial" };

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(0.8f, 0.22f, 0.22f, 1f);

            var enemy = go.AddComponent<YourQuestTutorialEnemy>();
            enemy.Initialize(this, semanticRegionId);
            _alive.Add(enemy);
        }
    }

    public void NotifyEnemyDied(YourQuestTutorialEnemy enemy)
    {
        _alive.Remove(enemy);
        if (_alive.Count == 0)
            SpawnNow();
    }
}
