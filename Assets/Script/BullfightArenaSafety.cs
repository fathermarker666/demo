using UnityEngine;

public class BullfightArenaSafety : MonoBehaviour
{
    public float fallThresholdY = -2f;
    public float horizontalOverflowTolerance = 0.35f;
    public float horizontalSnapPadding = 0.1f;

    private BullfightSpawnManager spawnManager;

    private void Awake()
    {
        spawnManager = GetComponent<BullfightSpawnManager>() ?? FindObjectOfType<BullfightSpawnManager>(true);
    }

    private void LateUpdate()
    {
        if (spawnManager == null)
            return;

        if (transform.position.y < fallThresholdY)
        {
            spawnManager.ResetPlayerToSpawn();
            return;
        }

        spawnManager.KeepPlayerInsideArena(horizontalOverflowTolerance, horizontalSnapPadding);
    }
}
