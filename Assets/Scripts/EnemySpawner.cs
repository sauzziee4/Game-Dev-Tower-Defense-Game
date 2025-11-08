using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

// Handles spawning of enemies at a set interval
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyPrefabInfo
    {
        public GameObject prefab;
        public float spawnWeight = 1f; // Relative chance to spawn this type
    }
    [Header("Enemy Settings")]
    public EnemyPrefabInfo easyEnemyPrefab;
    public EnemyPrefabInfo mediumEnemyPrefab;
    public EnemyPrefabInfo hardEnemyPrefab;

    [Header("Spawn Settings")]
    public float spawnInterval = 3f; // Time in seconds between spawns
    public float spawnHeightOffset = 0.5f; // Height offset above tile surface

    [Header("References")]
    [SerializeField] private HexGridGenerator hexGridGenerator;

    private List<Vector2Int> spawnPointCoords = new List<Vector2Int>();
    private bool IsReadyToSpawn = false;
    private bool isSpawning = false; // Track if spawning is active
    private Coroutine spawnCoroutine; // Reference to the spawning coroutine

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

        if (easyEnemyPrefab.prefab == null && mediumEnemyPrefab.prefab == null && hardEnemyPrefab.prefab == null)
        {
            Debug.LogError("EnemySpawner: Enemy prefab is not assigned!");
        }
    }

    //fetches path start point from HexGridGenerator
    private void InitializeSpawner()
    {
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: InitializeSpawner called");

        if (hexGridGenerator != null)
        {
            spawnPointCoords = hexGridGenerator.GetSpawnPointCoords();

            if (enableDebugLogs)
                Debug.Log($"EnemySpawner: Retrieved {spawnPointCoords?.Count ?? 0} spawn points");

            //check if spawn coords were retreived correctly
            if (spawnPointCoords != null && spawnPointCoords.Count > 0)
            {
                //if valid, allow spawning to commence
                IsReadyToSpawn = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"EnemySpawner: Spawn points: {string.Join(", ", spawnPointCoords)}");
                }
                //start continous spawning
                StartSpawning();
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
                StartSpawning();
            }
            else
            {
                Debug.LogError("EnemySpawner: Still no spawn points after retry");
            }
        }
    }

    public void StartSpawning()
    {
        //only proceed if not already spawning enemies
        if (!isSpawning && IsReadyToSpawn)
        {
            isSpawning = true;
            spawnCoroutine = StartCoroutine(SpawnEnemies());
            if (enableDebugLogs)
                Debug.Log("EnemySpawner: Started spawning enemies"); 
        }
    }

    public void StopSpawning()
    {
        if (isSpawning)
        {
            isSpawning = false;

            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            if (enableDebugLogs)
                Debug.Log("EnemySpawner: Stopped spawning enemies");
        }
    }

    public void ResumeSpawning()
    {
        if (!isSpawning && IsReadyToSpawn)
        {
            StartSpawning();
            if (enableDebugLogs)
                Debug.Log("EnemySpawner: Resumed spawning enemies");
        }
    }

    public bool IsSpawning()
    {
        return isSpawning;
    }

    // Coroutine that spawns enemies at a set interval
    private IEnumerator SpawnEnemies()
    {
        if (enableDebugLogs)
            Debug.Log("EnemySpawner: Starting enemy spawn loop");

        // Wait a bit before starting to ensure everything is properly initialized
        yield return new WaitForSeconds(1f);

        while (isSpawning) // Changed from while(true) to while(isSpawning)
        {
            if (IsReadyToSpawn && spawnPointCoords.Count > 0)
            {
                SpawnSingleEnemy();
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"EnemySpawner: Cannot spawn - Ready: {IsReadyToSpawn}, SpawnPoints: {spawnPointCoords.Count}, Prefabs: Easy={easyEnemyPrefab.prefab != null}, Medium={mediumEnemyPrefab.prefab != null}, Hard={hardEnemyPrefab.prefab != null}");
            }

            // Wait for specified interval before spawning next enemy
            yield return new WaitForSeconds(spawnInterval);
        }

        if (enableDebugLogs)
            Debug.Log("EnemySpawner: Spawn loop ended");
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
                GameObject selectedPrefab = SelectEnemyPrefab();
                if (selectedPrefab != null)
                {
                    GameObject newEnemy = Instantiate(selectedPrefab, hit.position, Quaternion.identity);

                    if (enableDebugLogs)
                        Debug.Log($"EnemySpawner: Successfully spawned enemy at {hit.position}");

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
            }
            else
            {
                Debug.LogError($"EnemySpawner: Could not find valid NavMesh position near tile at {randomCoord}. Tile position: {spawnPos}");
            }
        }
        else
        {
            Debug.LogError($"EnemySpawner: No tile found at spawn coordinate {randomCoord}");
        }
    }

    // Randomly selects an enemy prefab based on their weights
    private GameObject SelectEnemyPrefab()
    {
        float totalWeight = 0f;

        // Sum up all weights for available prefabs
        if (easyEnemyPrefab.prefab != null) totalWeight += easyEnemyPrefab.spawnWeight;
        if (mediumEnemyPrefab.prefab != null) totalWeight += mediumEnemyPrefab.spawnWeight;
        if (hardEnemyPrefab.prefab != null) totalWeight += hardEnemyPrefab.spawnWeight;

        float random = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        // Pick prefab based on weighted random selection
        if (easyEnemyPrefab.prefab != null)
        {
            currentWeight += easyEnemyPrefab.spawnWeight;
            if (random <= currentWeight) return easyEnemyPrefab.prefab;
        }
      
        if (mediumEnemyPrefab.prefab != null)
        {
            currentWeight += mediumEnemyPrefab.spawnWeight;
            if (random <= currentWeight) return mediumEnemyPrefab.prefab;
        }

        if (hardEnemyPrefab.prefab != null)
        {
            return hardEnemyPrefab.prefab;
        }

        return null;
    }
}