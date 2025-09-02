using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Handles spawning of enemies at a set interval
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 3f; // Time in seconds between spawns
    public List<Transform> spawnPoints;

    private void Start()
    {
        // Starts the coroutine to begin spawning enemies
        StartCoroutine(SpawnEnemies());
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

                // Instantiate a new enemy at the selected spawn point
                Instantiate(enemyPrefab, randomSpawnPoint.position, Quaternion.identity);
            }
        }
    }
}