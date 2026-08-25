using UnityEngine;

public sealed class YQRuntimeWorldAssetRegistrySmokeTest : MonoBehaviour
{
    [SerializeField]
    private string testPrefabPath =
        "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_WoodenCrate.prefab";

    [SerializeField]
    private float distanceInFrontOfPlayer = 3.0f;

    [SerializeField]
    private float verticalOffset = 0.1f;

    private bool _ran;
    private GameObject _spawnedInstance;

    private void Update()
    {
        if (_ran)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        _ran = true;
        RunTest(player.transform);
    }

    private void RunTest(Transform player)
    {
        YQRuntimeWorldAssetRegistry registry =
            YQRuntimeWorldAssetRegistry.Instance;

        if (registry == null)
        {
            Debug.LogError(
                "[YQRuntimeWorldAssetRegistrySmokeTest] FAIL: " +
                "Runtime registry could not be loaded from Resources.");

            return;
        }

        GameObject prefab =
            registry.ResolvePrefab(testPrefabPath);

        if (prefab == null)
        {
            Debug.LogError(
                "[YQRuntimeWorldAssetRegistrySmokeTest] FAIL: " +
                "Registry loaded, but prefab could not be resolved: " +
                testPrefabPath);

            return;
        }

        Vector3 forward = player.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 position =
            player.position +
            forward * distanceInFrontOfPlayer +
            Vector3.up * verticalOffset;

        _spawnedInstance =
            Instantiate(
                prefab,
                position,
                Quaternion.identity);

        _spawnedInstance.name =
            "YQ_RUNTIME_REGISTRY_TEST__" +
            prefab.name;

        Debug.Log(
            "[YQRuntimeWorldAssetRegistrySmokeTest] PASS\n" +
            "Requested path: " + testPrefabPath + "\n" +
            "Resolved prefab: " + prefab.name + "\n" +
            "Instantiated object: " + _spawnedInstance.name + "\n" +
            "Position: " + position);
    }

    private void OnDestroy()
    {
        if (_spawnedInstance != null)
            Destroy(_spawnedInstance);
    }
}