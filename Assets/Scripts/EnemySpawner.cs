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

    [Header("References")]
    [SerializeField] private HexGridGenerator hexGridGenerator;

    private List<Vector2Int> spawnPointCoords = new List<Vector2Int>();
    private bool IsReadyToSpawn = false;

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
    }

    private void InitializeSpawner()
    {
        if (hexGridGenerator != null)
        {
            spawnPointCoords = hexGridGenerator.GetSpawnPointCoords();
            if (spawnPointCoords != null && spawnPointCoords.Count > 0)
            {
                IsReadyToSpawn = true;
                StartCoroutine(SpawnEnemies());
            }
            else
            {
                Debug.LogError("Enemy Spawner could not initialize: No spawn points found!");
            }
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

            if (IsReadyToSpawn && spawnPointCoords.Count > 0)
            {
                Vector2Int randomCoord = spawnPointCoords[Random.Range(0, spawnPointCoords.Count)];
                GameObject spawnTile = hexGridGenerator.GetTileAt(randomCoord);

                if (spawnTile != null)
                {
                    Vector3 spawnPos = spawnTile.transform.position;
                    if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                    {
                        Instantiate(enemyPrefab, hit.position, Quaternion.identity);
                        // The enemy script itself handles setting the destination
                    }
                    else
                    {
                        Debug.LogError($"Could not find valid NavMesh position near tile at {randomCoord}");
                    }
                }
            }
        }
    }
}