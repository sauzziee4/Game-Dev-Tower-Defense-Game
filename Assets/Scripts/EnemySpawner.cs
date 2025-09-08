using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

// Handles spawning of enemies at a set interval
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;
    public float spawnInterval = 3f; // Time in seconds between spawns
    public float spawnHeightOffset = 0.5f; // Height offset above tile surface

    [Header("References")]
    [SerializeField] private HexGridGenerator hexGridGenerator;

    private List<Vector2Int> spawnPointCoords = new List<Vector2Int>();
    private bool IsReadyToSpawn = false;

    //debugging
    [Header("Debug")]
    public bool enableDebugLogs = true;

    private void OnEnable()
    {
        HexGridGenerator.OnGridGenerated += InitializeSpawner;
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: Subscribed to OnGridGenerated event");
    }

    private void OnDisable()
    {
        HexGridGenerator.OnGridGenerated -= InitializeSpawner;
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: Unsubscribed from OnGridGenerated event");
    }

    private void Start()
    {
        if (hexGridGenerator == null)
        {
            hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: Enemy prefab is not assigned!");
        }
    }

    private void InitializeSpawner()
    {
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: InitializeSpawner called");

        if (hexGridGenerator != null)
        {
            spawnPointCoords = hexGridGenerator.GetSpawnPointCoords();

            if (enableDebugLogs)
                Debug.Log($"EnemySpawner: Retrieved {spawnPointCoords?.Count ?? 0} spawn points");

            if (spawnPointCoords != null && spawnPointCoords.Count > 0)
            {
                IsReadyToSpawn = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"EnemySpawner: Spawn points: {string.Join(", ", spawnPointCoords)}");
                }
                StartCoroutine(SpawnEnemies());
            }
            else
            {
                Debug.LogError("Enemy Spawner could not initialize: No spawn points found!");
                // Try to get spawn points directly from pathfinder as fallback
                StartCoroutine(RetryInitialization());
            }
        }
        else
        {
            Debug.LogError("EnemySpawner: HexGridGenerator reference is null!");
        }
    }

    private IEnumerator RetryInitialization()
    {
        yield return new WaitForSeconds(1f); // Wait a bit and try again

        if (hexGridGenerator != null)
        {
            spawnPointCoords = hexGridGenerator.GetSpawnPointCoords();
            if (spawnPointCoords != null && spawnPointCoords.Count > 0)
            {
                IsReadyToSpawn = true;
                Debug.Log("EnemySpawner: Successfully initialized on retry");
                StartCoroutine(SpawnEnemies());
            }
            else
            {
                Debug.LogError("EnemySpawner: Still no spawn points after retry");
            }
        }
    }

    // Coroutine that spawns enemies at a set interval
    private IEnumerator SpawnEnemies()
    {
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: Starting enemy spawn loop");

        // Wait a bit before starting to ensure everything is properly initialized
        yield return new WaitForSeconds(1f);

        while (true)
        {
            if (IsReadyToSpawn && spawnPointCoords.Count > 0 && enemyPrefab != null)
            {
                SpawnSingleEnemy();
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"EnemySpawner: Cannot spawn - Ready: {IsReadyToSpawn}, SpawnPoints: {spawnPointCoords.Count}, Prefab: {enemyPrefab != null}");
            }

            // Wait for the specified interval before spawning next enemy
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnSingleEnemy()
    {
        // Pick a random spawn point
        Vector2Int randomCoord = spawnPointCoords[Random.Range(0, spawnPointCoords.Count)];

        if (enableDebugLogs)
            Debug.Log($"EnemySpawner: Attempting to spawn at coordinate {randomCoord}");

        GameObject spawnTile = hexGridGenerator.GetTileAt(randomCoord);

        if (spawnTile != null)
        {
            Vector3 spawnPos = spawnTile.transform.position + Vector3.up * spawnHeightOffset;

            // Try to find a valid NavMesh position
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                GameObject newEnemy = Instantiate(enemyPrefab, hit.position, Quaternion.identity);

                if (enableDebugLogs)
                    Debug.Log($"EnemySpawner: Successfully spawned enemy at {hit.position}");

                // Ensure the enemy has proper components
                Enemy enemyComponent = newEnemy.GetComponent<Enemy>();
                if (enemyComponent == null)
                {
                    Debug.LogError("EnemySpawner: Spawned enemy prefab missing Enemy component!");
                }

                NavMeshAgent agent = newEnemy.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    Debug.LogError("EnemySpawner: Spawned enemy prefab missing NavMeshAgent component!");
                }
            }
            else
            {
                Debug.LogError($"EnemySpawner: Could not find valid NavMesh position near tile at {randomCoord}. Tile position: {spawnPos}");

                // Try spawning directly on tile as fallback
                GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                if (enableDebugLogs)
                    Debug.Log($"EnemySpawner: Spawned enemy at tile position (no NavMesh) {spawnPos}");
            }
        }
        else
        {
            Debug.LogError($"EnemySpawner: No tile found at spawn coordinate {randomCoord}");
        }
    }

    // Public method to manually spawn an enemy (for testing)
    public void SpawnEnemyManually()
    {
        if (IsReadyToSpawn && spawnPointCoords.Count > 0 && enemyPrefab != null)
        {
            SpawnSingleEnemy();
        }
        else
        {
            Debug.LogWarning("EnemySpawner: Cannot manually spawn enemy - not ready or missing components");
        }
    }

    // Method to pause/resume spawning
    public void SetSpawningEnabled(bool enabled)
    {
        if (enabled && !IsReadyToSpawn && spawnPointCoords.Count > 0)
        {
            IsReadyToSpawn = true;
            StartCoroutine(SpawnEnemies());
        }
        else if (!enabled)
        {
            IsReadyToSpawn = false;
            StopAllCoroutines();
        }
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (spawnPointCoords != null && hexGridGenerator != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Vector2Int coord in spawnPointCoords)
            {
                Vector3 worldPos = hexGridGenerator.HexToWorld(coord);
                Gizmos.DrawWireSphere(worldPos + Vector3.up * spawnHeightOffset, 1f);
                Gizmos.DrawLine(worldPos, worldPos + Vector3.up * 2f);
            }
        }
    }
}