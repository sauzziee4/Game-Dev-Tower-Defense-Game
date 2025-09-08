using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

// Handles spawning of enemies at a set interval
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;

    public float spawnInterval = 3f; // Time in seconds between spawns

    /* [Header("Spawn Point Settings")]
    public List<Transform> spawnPoints; */

    [Header("References")]
    [SerializeField] private HexGridGenerator hexGridGenerator;

    [SerializeField] private Pathfinder pathfinder;

    private List<Transform> spawnPoints = new List<Transform>();
    private bool IsReadyToSpawn = false;

    /*   private bool spawnPointsPopulated = false;
       private bool spawningActivated = true; */

    private void OnEnable()
    {
        HexGridGenerator.OnGridGenerated += InitializeSpawner;
    }

    private void OnDisable()
    {
        HexGridGenerator.OnGridGenerated -= InitializeSpawner;
    }

    private void Start()
    {
        if (hexGridGenerator == null)
        {
            hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        }
        if (pathfinder == null)
        {
            pathfinder = FindFirstObjectByType<Pathfinder>();
        }
    }

    private void InitializeSpawner()
    {
        PopulateSpawnPoints();

        if (IsReadyToSpawn)
        {
            StartCoroutine(SpawnEnemies());
        }
        else
        {
            Debug.LogError("Enemy Spawner could not initialize because no spawn points were found!");
        }
    }

    private void PopulateSpawnPoints()
    {
        if (hexGridGenerator == null) return;

        List<GameObject> spawnPointObjects = hexGridGenerator.GetSpawnPoints();

        if (spawnPointObjects != null && spawnPointObjects.Count > 0)
        {
            spawnPoints.Clear();
            foreach (GameObject spawnPointObj in spawnPointObjects)
            {
                if (spawnPointObj != null)
                {
                    spawnPoints.Add(spawnPointObj.transform);
                }
            }
            IsReadyToSpawn = true;
            Debug.Log($"Successfully populated {spawnPoints.Count} spawn points");
        }
        else
        {
            Debug.LogWarning("HexGridGenerator returned no spawn points during population.");
            IsReadyToSpawn = false;
        }
    }

    // Coroutine that spawns enemies at a set interval
    private IEnumerator SpawnEnemies()
    {
        Debug.Log("starting enemy spawn loop");
        while (true)
        {
            // Wait for the specified interval before spawning next enemy
            yield return new WaitForSeconds(spawnInterval);

            if (spawnPoints.Count > 0)
            {
                // Select a random spawn point from the list
                Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

                GameObject newEnemy = Instantiate(enemyPrefab, randomSpawnPoint.position, Quaternion.identity);

                UnityEngine.AI.NavMeshAgent agent = newEnemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && hexGridGenerator.GetTowerInstance() != null)
                {
                    agent.SetDestination(hexGridGenerator.GetTowerInstance().transform.position);
                }
                else
                {
                    Debug.LogWarning("Enemy prefab is missing NavMeshAgent or tower instance is not found.");
                }
                Vector2Int startCoords = hexGridGenerator.WorldToHex(randomSpawnPoint.position);
                Vector2Int endCoords = Vector2Int.zero;
                List<Vector2Int> path = pathfinder.FindPath(startCoords, endCoords, hexGridGenerator.gridRadius);
            }
        }
    }
}