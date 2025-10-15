using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using System.Linq;

public class ProceduralSpawnManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private HexGridGenerator hexGridGenerator;
    [SerializeField] private TurretPlacementManager turretPlacementManager;
    private Tower centralTower;
    private HexGrid hexGrid;

    [Header("Difficulty Settings")]
    [SerializeField] private float baseDifficultyScore = 1f;
    [SerializeField] private float difficultyIncreaseRate = 0.05f;
    [SerializeField] private float maxDifficultyScore = 5f;
    [SerializeField] private float difficultyCheckInterval = 5f;

    [Header("Performance Metrics")]
    [SerializeField] private float targetTowerHealthThreshold = 0.85f;
    [SerializeField] private float criticalTowerHealthThreshold = 0.4f;
    [SerializeField] private float highResourceThreshold = 200f;
    [SerializeField] private float lowResourceThreshold = 50f;

    [Header("Spawn Timing")]
    [SerializeField] private float minSpawnInterval = 1.5f;
    [SerializeField] private float maxSpawnInterval = 5f;
    [SerializeField] private float baseSpawnInterval = 3f;

    [Header("Wave Settings")]
    [SerializeField] private int enemiesPerWave = 5;
    [SerializeField] private float timeBetweenWaves = 10f;
    [SerializeField] private bool useWaveSystem = false;

    [Header("Wave Events")]
    public UnityEngine.Events.UnityEvent<int> OnWaveStart;
    public UnityEngine.Events.UnityEvent<int> OnWaveComplete;
    public UnityEngine.Events.UnityEvent<int, int> OnWaveProgress;

    [Header("Wave Preparation")]
    [SerializeField] private bool allowDefenderPlacementBetweenWaves = true;
    [SerializeField] private float wavePrepTime = 5f;

    [Header("Elite Enemy Settings")]
    [SerializeField] private float eliteSpawnChance = 0.1f;
    [SerializeField] private float eliteSpawnInterval = 30f;
    private float nextEliteSpawnTime;

    [Header("Dynamic Spawn Settings")]
    [SerializeField] private bool enableDynamicSpawns = true;
    [SerializeField] private float dynamicSpawnDistanceFromTower = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private float currentDifficultyScore;
    private float currentSpawnInterval;
    private int totalEnemiesKilled = 0;
    private int totalEnemiesSpawned = 0;
    private float lastDifficultyCheckTime;
    private float gameStartTime;

    private Dictionary<Vector2Int, float> spawnLocationWeights = new Dictionary<Vector2Int, float>();
    private Dictionary<Vector2Int, float> spawnLocationLastUsed = new Dictionary<Vector2Int, float>();
    private List<Vector2Int> availableSpawnPoints = new List<Vector2Int>();

    private Queue<EnemyType> recentlySpawnedTypes = new Queue<EnemyType>();
    private int recentTypeHistorySize = 10;

    private Dictionary<Vector2Int, int> defenderPlacements = new Dictionary<Vector2Int, int>();
    private int lastDefenderCount = 0;
    private float lastResourceCount = 0f;
    private float resournceAccumulationRate = 0f;

    private int currentWave = 0;
    private int enemiesSpawnedThisWave = 0;
    private bool isWaveActive = false;

    public static ProceduralSpawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeSystem();
        gameStartTime = Time.time;
        nextEliteSpawnTime = Time.time + eliteSpawnInterval;

        if (useWaveSystem)
        {
            StartCoroutine(WaveSpawnSystem());
        }
        else
        {
            StartCoroutine(ContinuousSpawnSystem());
        }

        StartCoroutine(DifficultyAdjustmentSystem());
    }

    private void InitializeSystem()
    {
        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemySpawner>();

        if (hexGridGenerator == null)
            hexGridGenerator = FindAnyObjectByType<HexGridGenerator>();

        if (turretPlacementManager == null)
            turretPlacementManager = FindAnyObjectByType<TurretPlacementManager>();

        centralTower = FindAnyObjectByType<Tower>();

        if (hexGridGenerator != null)
        {
            hexGrid = hexGridGenerator.GetComponent<HexGrid>();
        }
        
        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
        }

        if (hexGridGenerator != null)
        {
            availableSpawnPoints = hexGridGenerator.GetSpawnPointCoords();

            if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
            {
                StartCoroutine(RetryInitialization());
                return;
            }
            
            InitializeSpawnWeights();
        }

            currentDifficultyScore = baseDifficultyScore;
            currentSpawnInterval = baseSpawnInterval;
            lastDifficultyCheckTime = Time.time;

            if (turretPlacementManager != null)
            {
                lastResourceCount = turretPlacementManager.PlayerResources;
            }

            if (enableDynamicSpawns && hexGrid != null)
            {
                GenerateDynamicSpawnPoints();
            }


            if (enableDebugLogs)
                Debug.Log("ProceduralSpawnManager initialized with " + availableSpawnPoints.Count + " spawn points");
        }
    
    private IEnumerator RetryInitialization()
{
    int attempts = 0;
    int maxAttempts = 10;
    
    while (attempts < maxAttempts)
    {
        yield return new WaitForSeconds(0.5f);
        attempts++;
        
        if (hexGridGenerator != null)
        {
            availableSpawnPoints = hexGridGenerator.GetSpawnPointCoords();
            
            if (availableSpawnPoints != null && availableSpawnPoints.Count > 0)
            {
                Debug.Log($"ProceduralSpawnManager: Successfully retrieved {availableSpawnPoints.Count} spawn points on attempt {attempts}");
                InitializeSpawnWeights();
                
                currentDifficultyScore = baseDifficultyScore;
                currentSpawnInterval = baseSpawnInterval;
                lastDifficultyCheckTime = Time.time;
                
                if (turretPlacementManager != null)
                {
                    lastResourceCount = turretPlacementManager.PlayerResources;
                }
                
                yield break; // Success!
            }
        }
    }
    
    Debug.LogError($"ProceduralSpawnManager: Failed to retrieve spawn points after {maxAttempts} attempts!");
}

    
    private void GenerateDynamicSpawnPoints()
    {
        if (hexGrid == null || centralTower == null)
        {
            Debug.LogWarning("Cannot generate dynamic spawn points - missing HexGrid or Tower reference");
            return;
        }
        
        // Get all path tiles from the hex grid
        List<Vector2Int> allPathTiles = hexGrid.GetTilesOfType(HexType.Path);
        
        int addedPoints = 0;
        foreach (Vector2Int pathCoord in allPathTiles)
        {
            Vector3 worldPos = hexGridGenerator.HexToWorld(pathCoord);
            float distanceToTower = Vector3.Distance(worldPos, centralTower.transform.position);
            
            // Add path tiles beyond a certain distance as potential spawn points
            if (distanceToTower >= dynamicSpawnDistanceFromTower)
            {
                if (!availableSpawnPoints.Contains(pathCoord))
                {
                    availableSpawnPoints.Add(pathCoord);
                    addedPoints++;
                }
            }
        }
    
        if (enableDebugLogs)
            Debug.Log($"Dynamic spawn generation added {addedPoints} additional spawn points (Total: {availableSpawnPoints.Count})");
    }

    private void InitializeSpawnWeights()
    {
        foreach(Vector2Int spawnPoint in availableSpawnPoints)
        {
            spawnLocationWeights[spawnPoint] = 1f;
            spawnLocationLastUsed[spawnPoint] = -100f;
        }
    }
    #region Continuous Spawn System

    private IEnumerator ContinuousSpawnSystem()
    {
        yield return new WaitForSeconds(2f);
        isWaveActive = true; 

        while(!GameManager.Instance.isGameOver)
        {
            if (!GameManager.Instance.isPaused)
            {
                SpawnEnemy();
                totalEnemiesSpawned++;
            }

            yield return new WaitForSeconds(currentSpawnInterval);
        }
        
        isWaveActive = false;
    }

    #endregion

    #region Wave Spawn System

    private IEnumerator WaveSpawnSystem()
    {
        yield return new WaitForSeconds(2f);

        while (!GameManager.Instance.isGameOver)
        {

            currentWave++;
            enemiesSpawnedThisWave = 0;
            isWaveActive = false;

            if (enableDebugLogs)
                Debug.Log($"Wave {currentWave} preparation phase - {wavePrepTime} seconds until start");

            // Notify wave is coming (for UI updates)
            OnWaveStart?.Invoke(currentWave);

            // Give player time to prepare
            yield return new WaitForSeconds(wavePrepTime);

            // WAVE ACTIVE PHASE
            isWaveActive = true;

            if (enableDebugLogs)
                Debug.Log($"Wave {currentWave} STARTED - Spawning {enemiesPerWave} enemies");

            // Spawn all enemies in the wave
            while (enemiesSpawnedThisWave < enemiesPerWave && !GameManager.Instance.isGameOver)
            {
                if (!GameManager.Instance.isPaused)
                {
                    SpawnEnemy();
                    totalEnemiesSpawned++;
                    enemiesSpawnedThisWave++;

                    // Notify progress
                    OnWaveProgress?.Invoke(enemiesSpawnedThisWave, enemiesPerWave);
                }

                yield return new WaitForSeconds(currentSpawnInterval);
            }

            // WAVE COMPLETE PHASE
            isWaveActive = false;

            if (enableDebugLogs)
                Debug.Log($"Wave {currentWave} spawning complete. Waiting for enemies to be cleared...");

            // Wait for all enemies from this wave to be defeated before starting cooldown
            yield return StartCoroutine(WaitForWaveCleanup());

            if (enableDebugLogs)
                Debug.Log($"Wave {currentWave} COMPLETE. Next wave in {timeBetweenWaves} seconds");

            // Notify wave complete
            OnWaveComplete?.Invoke(currentWave);

            // Cooldown between waves
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }
    
    private IEnumerator WaitForWaveCleanup()
    {
        float timeout = 60f;
        float elapsed = 0f;

        while (Enemy.allEnemies.Count > 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if(elapsed >= timeout && enableDebugLogs)
        {
            Debug.LogWarning($"Wave cleanup timeout - {Enemy.allEnemies.Count} enemies still active");
        }
    }
    #endregion

    #region Enemy Spawning

    private void SpawnEnemy()
    {
        Vector2Int spawnLocation = SelectSpawnLocation();

        EnemyType enemyType = SelectEnemyType();

        SpawnEnemyAtLocation(spawnLocation, enemyType);

        UpdateSpawnTracking(spawnLocation, enemyType);
    }

    private Vector2Int SelectSpawnLocation()
    {
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
    {
        Debug.LogError("ProceduralSpawnManager: No spawn points available! Cannot spawn enemies safely.");
        
        // EMERGENCY FALLBACK: Try to reinitialize
        if (hexGridGenerator != null)
        {
            availableSpawnPoints = hexGridGenerator.GetSpawnPointCoords();
            if (availableSpawnPoints != null && availableSpawnPoints.Count > 0)
            {
                Debug.LogWarning("ProceduralSpawnManager: Emergency reinitialization successful!");
                InitializeSpawnWeights();
            }
            else
            {
                // STILL NO SPAWN POINTS - Don't spawn anything!
                Debug.LogError("ProceduralSpawnManager: CRITICAL - Cannot find any spawn points. Halting enemy spawning.");
                StopAllCoroutines(); // Stop trying to spawn
                return Vector2Int.zero; // This will fail but at least we logged it
            }
        }
        
        // If we still have no spawn points after retry, return first available
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
        {
            return Vector2Int.zero; // Last resort
        }
    }
    
    UpdateSpawnLocationWeights();
    
    // Weighted random selection with SAFETY CHECKS
    float totalWeight = 0f;
    foreach (var kvp in spawnLocationWeights)
    {
        if (kvp.Value > 0) // Only count positive weights
        {
            totalWeight += kvp.Value;
        }
    }
    
    // If all weights are 0 or negative, reset them
    if (totalWeight <= 0f)
    {
        Debug.LogWarning("ProceduralSpawnManager: All spawn weights were 0 or negative. Resetting to default.");
        InitializeSpawnWeights();
        totalWeight = spawnLocationWeights.Values.Sum();
    }
    
    float randomValue = Random.Range(0f, totalWeight);
    float currentWeight = 0f;
    
    foreach (var kvp in spawnLocationWeights)
    {
        if (kvp.Value > 0) // Skip zero or negative weights
        {
            currentWeight += kvp.Value;
            if (randomValue <= currentWeight)
            {
                if (enableDebugLogs)
                    Debug.Log($"Selected spawn point: {kvp.Key} with weight {kvp.Value:F2}");
                return kvp.Key;
            }
        }
    }
    
    Debug.LogWarning("ProceduralSpawnManager: Weighted selection failed, using random fallback.");
    return availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)];
    }

    private void UpdateSpawnLocationWeights()
    {
        foreach(Vector2Int spawnPoint in availableSpawnPoints)
        {
            float weight = 1f;

            float timeSinceLastUsed = Time.time - spawnLocationLastUsed[spawnPoint];
            weight *= Mathf.Clamp(timeSinceLastUsed / 10f, 0.3f, 2f);

            float defenderProx = CalcDefProxScore(spawnPoint);
            weight *= (1f + defenderProx);

            float distanceTower = CalcDistToTower(spawnPoint);
            weight *= Mathf.Clamp(1f / (distanceTower + 1f), 0.5f, 1.5f);

            spawnLocationWeights[spawnPoint] = weight;
        }
    }

    private float CalcDefProxScore(Vector2Int spawnPoint)
    {
    if (turretPlacementManager == null) return 0f;
    
    Vector3 spawnWorldPos = hexGridGenerator.HexToWorld(spawnPoint);
    int nearbyDefenders = 0;
    float searchRadius = 15f;
    float minDistance = float.MaxValue;
    
    // Get all placed defenders from the scene
    PlaceableTurret[] allTurrets = FindObjectsByType<PlaceableTurret>(FindObjectsSortMode.None);
    
    foreach (PlaceableTurret turret in allTurrets)
    {
        float distance = Vector3.Distance(spawnWorldPos, turret.transform.position);
        
        if (distance < searchRadius)
        {
            nearbyDefenders++;
        }
        
        if (distance < minDistance)
        {
            minDistance = distance;
        }
    }
    
    // Return a score based on defender coverage
    // Higher score = less defended (better for spawning)
    // Lower score = more defended (worse for spawning)
    if (nearbyDefenders == 0)
    {
        return 2f; // No defenders nearby - high priority spawn location
    }
    else if (nearbyDefenders == 1)
    {
        return 1f; // Some defense - medium priority
    }
    else
    {
        // Multiple defenders - low priority
        // The more defenders, the lower the score
        return Mathf.Max(0.1f, 1f / nearbyDefenders);
    }
    }

    private float CalcDistToTower(Vector2Int spawnPoint)
    {
        if (centralTower == null) return 10f;

        Vector3 spawnWorldPos = hexGridGenerator.HexToWorld(spawnPoint);
        return Vector3.Distance(spawnWorldPos, centralTower.transform.position);
    }

    private EnemyType SelectEnemyType()
    {
        if (Time.time >= nextEliteSpawnTime && Random.value < eliteSpawnChance)
        {
            nextEliteSpawnTime = Time.time + eliteSpawnInterval;
            return EnemyType.Hard;
        }

        Dictionary<EnemyType, float> typeWeights = CalculateEnemyTypeWeights();

        float totalWeight = typeWeights.Values.Sum();
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var kvp in typeWeights)
        {
            currentWeight += kvp.Value;
            if (randomValue <= currentWeight)
            {
                return kvp.Key;
            }
        }
        return EnemyType.Easy;
    }

    private Dictionary<EnemyType, float> CalculateEnemyTypeWeights()
    {
        Dictionary<EnemyType, float> weights = new Dictionary<EnemyType, float>();
        
        // Base weights adjusted by difficulty
        if (currentDifficultyScore < 1.5f)
        {
            // Early game - mostly easy enemies
            weights[EnemyType.Easy] = 3f;
            weights[EnemyType.Medium] = 1f;
            weights[EnemyType.Hard] = 0.2f;
        }
        else if (currentDifficultyScore < 3f)
        {
            // Mid game - balanced mix
            weights[EnemyType.Easy] = 2f;
            weights[EnemyType.Medium] = 2f;
            weights[EnemyType.Hard] = 1f;
        }
        else
        {
            // Late game - harder enemies
            weights[EnemyType.Easy] = 1f;
            weights[EnemyType.Medium] = 2f;
            weights[EnemyType.Hard] = 2.5f;
        }
        
        // Reduce weight for recently spawned types (variety)
        foreach (EnemyType recentType in recentlySpawnedTypes)
        {
            if (weights.ContainsKey(recentType))
            {
                weights[recentType] *= 0.7f;
            }
        }
        
        return weights;
    }
    
    private void SpawnEnemyAtLocation(Vector2Int spawnCoord, EnemyType enemyType)
    {
        GameObject spawnTile = hexGridGenerator.GetTileAt(spawnCoord);
        if (spawnTile == null)
        {
            Debug.LogError($"No tile found at {spawnCoord}");
            return;
        }
        
        // Get the appropriate prefab based on enemy type
        GameObject prefab = GetEnemyPrefabForType(enemyType);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for enemy type {enemyType}");
            return;
        }
        
        Vector3 spawnPos = spawnTile.transform.position + Vector3.up * 0.5f;
        
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            GameObject newEnemy = Instantiate(prefab, hit.position, Quaternion.identity);
            
            if (enableDebugLogs)
                Debug.Log($"Spawned {enemyType} enemy at {spawnCoord} (Difficulty: {currentDifficultyScore:F2})");
        }
    }
    
    private GameObject GetEnemyPrefabForType(EnemyType type)
    {
        if (enemySpawner == null) return null;
        
        switch (type)
        {
            case EnemyType.Easy:
                return enemySpawner.easyEnemyPrefab.prefab;
            case EnemyType.Medium:
                return enemySpawner.mediumEnemyPrefab.prefab;
            case EnemyType.Hard:
                return enemySpawner.hardEnemyPrefab.prefab;
            default:
                return enemySpawner.easyEnemyPrefab.prefab;
        }
    }
    
    private void UpdateSpawnTracking(Vector2Int location, EnemyType type)
    {
        // Update location tracking
        spawnLocationLastUsed[location] = Time.time;
        
        // Update type history
        recentlySpawnedTypes.Enqueue(type);
        if (recentlySpawnedTypes.Count > recentTypeHistorySize)
        {
            recentlySpawnedTypes.Dequeue();
        }
    }
    
    #endregion
    
    #region Difficulty Adjustment
    
    private IEnumerator DifficultyAdjustmentSystem()
    {
        while (!GameManager.Instance.isGameOver)
        {
            yield return new WaitForSeconds(difficultyCheckInterval);
            
            if (!GameManager.Instance.isPaused)
            {
                AdjustDifficulty();
            }
        }
    }
    
    private void AdjustDifficulty()
    {
        float performanceScore = CalculatePerformanceScore();
        
        // Adjust difficulty based on performance
        if (performanceScore > 0.7f)
        {
            // Player is doing well - increase difficulty
            currentDifficultyScore = Mathf.Min(currentDifficultyScore + difficultyIncreaseRate, maxDifficultyScore);
        }
        else if (performanceScore < 0.3f)
        {
            // Player is struggling - decrease difficulty
            currentDifficultyScore = Mathf.Max(currentDifficultyScore - difficultyIncreaseRate * 0.5f, baseDifficultyScore);
        }
        
        // Adjust spawn interval based on difficulty
        float intervalRange = maxSpawnInterval - minSpawnInterval;
        float normalizedDifficulty = (currentDifficultyScore - baseDifficultyScore) / (maxDifficultyScore - baseDifficultyScore);
        currentSpawnInterval = maxSpawnInterval - (intervalRange * normalizedDifficulty);
        
        if (enableDebugLogs)
        {
            Debug.Log($"Difficulty adjusted - Score: {currentDifficultyScore:F2}, Performance: {performanceScore:F2}, Spawn Interval: {currentSpawnInterval:F2}s");
        }
    }
    
    private float CalculatePerformanceScore()
    {
        float score = 0f;
        int factors = 0;
        
        // Factor 1: Tower health (40% weight)
        if (centralTower != null)
        {
            float healthRatio = centralTower.health / centralTower.maxHealth;
            
            if (healthRatio >= targetTowerHealthThreshold)
            {
                score += 0.9f * 0.4f;
            }
            else if (healthRatio <= criticalTowerHealthThreshold)
            {
                score += 0.1f * 0.4f;
            }
            else
            {
                score += (healthRatio - criticalTowerHealthThreshold) / 
                         (targetTowerHealthThreshold - criticalTowerHealthThreshold) * 0.4f;
            }
            factors++;
        }
        
        // Factor 2: Resource accumulation (30% weight)
        if (turretPlacementManager != null)
        {
            float currentResources = turretPlacementManager.PlayerResources;
            resournceAccumulationRate = (currentResources - lastResourceCount) / difficultyCheckInterval;
            lastResourceCount = currentResources;
            
            if (currentResources >= highResourceThreshold)
            {
                score += 0.9f * 0.3f;
            }
            else if (currentResources <= lowResourceThreshold)
            {
                score += 0.1f * 0.3f;
            }
            else
            {
                score += ((currentResources - lowResourceThreshold) / 
                         (highResourceThreshold - lowResourceThreshold)) * 0.3f;
            }
            factors++;
        }
        
        // Factor 3: Enemy elimination efficiency (30% weight)
        int activeEnemies = Enemy.allEnemies.Count;
        totalEnemiesKilled = totalEnemiesSpawned - activeEnemies;
        
        if (totalEnemiesSpawned > 0)
        {
            float eliminationRate = (float)totalEnemiesKilled / totalEnemiesSpawned;
            score += eliminationRate * 0.3f;
            factors++;
        }
        
        return factors > 0 ? score : 0.5f;
    }
    
    #endregion
    
    #region Public API
    
    public void NotifyEnemyKilled() => totalEnemiesKilled++;
    
    
    public void NotifyDefenderPlaced(Vector2Int coords)
    {
        if (!defenderPlacements.ContainsKey(coords))
        {
            defenderPlacements[coords] = 0;
        }
        defenderPlacements[coords]++;
    }

    public float GetCurrentDifficulty() => currentDifficultyScore;
    public int GetCurrentWave() => currentWave;
    public bool IsWaveActive() => isWaveActive;
    public (int current, int total) GetWaveProgress() => (enemiesSpawnedThisWave, enemiesPerWave);
    public int GetRemainingEnemiesInWave() => enemiesPerWave - enemiesSpawnedThisWave;
    #endregion
}