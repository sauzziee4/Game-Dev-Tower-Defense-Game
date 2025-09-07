using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Handles spawning of enemies at a set interval
public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;

    public float spawnInterval = 3f; // Time in seconds between spawns

    [Header("Spawn Point Settings")]
    public List<Transform> spawnPoints;

    [SerializeField]
    private HexGridGenerator hexGridGenerator;

    private Pathfinder pathfinder;

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
        PopulateSpawnPoints();

        // Starts the coroutine to begin spawning enemies
        StartCoroutine(SpawnEnemies());
    }

    private void PopulateSpawnPoints()
    {
        if (hexGridGenerator != null)
        {
            List<GameObject> spawnPointObjects = hexGridGenerator.GetSpawnPoints();

            spawnPoints.Clear();
            foreach (GameObject spawnPointObj in spawnPointObjects)
            {
                if (spawnPointObj != null)
                {
                    spawnPoints.Add(spawnPointObj.transform);
                }
            }
        }
    }

    // Coroutine that spawns enemies at a set interval
    private IEnumerator SpawnEnemies()
    {
        while (true)
        {
            // Wait for the specified interval before spawning next enemy
            yield return new WaitForSeconds(spawnInterval);

            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                // Select a random spawn point from the list
                Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

                Vector2Int startCoords = hexGridGenerator.WorldToHex(randomSpawnPoint.position);
                Vector2Int endCoords = Vector2Int.zero;
                List<Vector2Int> path = pathfinder.FindPath(startCoords, endCoords, hexGridGenerator.gridRadius);

                // Only spawn if a valid path exists
                if (path != null && path.Count > 1)
                {
                    GameObject newEnemy = Instantiate(enemyPrefab, randomSpawnPoint.position, Quaternion.identity);
                    Enemy enemyScript = newEnemy.GetComponent<Enemy>();
                    if (enemyScript != null)
                    {
                        enemyScript.SetPath(path);
                    }
                }
            }
        }
    }
}