using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using System.Linq;

/// <summary>
/// Advanced AI-driven enemy spawning system that adapts difficulty based on player performance.
/// Manages both continuous and wave-based spawning with intelligent location selection and enemy scaling.
/// </summary>
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
    [SerializeField] private float minSpawnInterval = 1.5f; // Fastest possible spawn rate
    [SerializeField] private float maxSpawnInterval = 5f; // Slowest possible spawn rate
    [SerializeField] private float baseSpawnInterval = 3f; // Default spawn interval

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

    [Header("Wave Progression System")]
    [SerializeField] private float enemiesPerWaveGrowth = 1.2f;
    [SerializeField] private int maxEnemiesPerWave = 30;
    [SerializeField] private float spawnIntervalDecreaseRate = 0.95f;
    [SerializeField] private float minWaveSpawnInterval = 0.8f;
    [SerializeField] private bool scaleEnemyHalthWithWaves = true;
    [SerializeField] private float enemyHealthScaling = 1.1f;
    [SerializeField] private bool increaseEliteChance = true;
    [SerializeField] private int bossWaveInterval = 5;
    [SerializeField] private bool enableBossWave = true;

    private float waveMultiplier = 1f;
    private int currentWaveEnemies = 5; 

    [Header("Elite Enemy Settings")]
    [SerializeField] private float eliteSpawnChance = 0.9f;
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

    // Player performance tracking
    private Dictionary<Vector2Int, int> defenderPlacements = new Dictionary<Vector2Int, int>(); // Track where players place defenses
    private int lastDefenderCount = 0; // Previous number of defenders
    private float lastResourceCount = 0f; // Previous resource amount
    private float resournceAccumulationRate = 0f; // Rate of resource gain/loss

    // Wave system state
    private int currentWave = 0; // Current wave number
    private int enemiesSpawnedThisWave = 0; // Enemies spawned in current wave
    private bool isWaveActive = false; // Whether a wave is currently active

    public static ProceduralSpawnManager Instance { get; private set; }

    // Singleton pattern implementation - ensures only one spawn manager exists.
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

    // Initialize the spawn system and start the appropriate spawning coroutine.
    private void Start()
    {
        InitializeSystem();
        gameStartTime = Time.time;
        nextEliteSpawnTime = Time.time + eliteSpawnInterval;

        // Start the appropriate spawning system based on settings
        if (useWaveSystem)
        {
            StartCoroutine(WaveSpawnSystem());
        }
        else
        {
            StartCoroutine(ContinuousSpawnSystem());
        }

        // Start the difficulty adjustment system
        StartCoroutine(DifficultyAdjustmentSystem());
    }

    // Initialize all system components and find required references.
    // Sets up spawn points, weights, and initial difficulty values.
    private void InitializeSystem()
    {
        // Find required components if not assigned
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

        // Stop any existing spawning to prevent conflicts
        if (enemySpawner != null)
        {
            enemySpawner.StopSpawning();
        }

        // Get spawn points from the hex grid
        if (hexGridGenerator != null)
        {
            availableSpawnPoints = hexGridGenerator.GetSpawnPointCoords();

            // If no spawn points found, try again later
            if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
            {
                StartCoroutine(RetryInitialization());
                return;
            }

            InitializeSpawnWeights();
        }

        // Set initial difficulty and timing values
        currentDifficultyScore = baseDifficultyScore;
        currentSpawnInterval = baseSpawnInterval;
        lastDifficultyCheckTime = Time.time;

        // Initialize resource tracking
        if (turretPlacementManager != null)
        {
            lastResourceCount = turretPlacementManager.PlayerResources;
        }

        // Generate additional spawn points if dynamic spawning is enabled
        if (enableDynamicSpawns && hexGrid != null)
        {
            GenerateDynamicSpawnPoints();
        }

        if (enableDebugLogs)
            Debug.Log("ProceduralSpawnManager initialized with " + availableSpawnPoints.Count + " spawn points");
    }
    
    // Retry initialization if spawn points aren't immediately available.
    // Sometimes the hex grid needs time to fully initialize.
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

    // Generate additional spawn points on path tiles that are far from the central tower.
    // This creates more strategic spawning options as the game progresses.
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

    // Initialize the weighting system for spawn point selection.
    // All points start with equal weight and no recent usage.
    private void InitializeSpawnWeights()
    {
        foreach(Vector2Int spawnPoint in availableSpawnPoints)
        {
            spawnLocationWeights[spawnPoint] = 1f;
            spawnLocationLastUsed[spawnPoint] = -100f; // Far in the past
        }
    }
    #region Continuous Spawn System

    // Continuous spawning system - spawns enemies at regular intervals without waves.
    // Adjusts spawn rate based on current difficulty level.
    private IEnumerator ContinuousSpawnSystem()
    {
        yield return new WaitForSeconds(2f); // Initial delay
        isWaveActive = true; // Mark as active for UI purposes

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
    // Advanced wave-based spawning system with preparation phases, escalating difficulty,
    // and special boss waves. Provides structured challenge progression.
    private IEnumerator WaveSpawnSystem()
    {
        yield return new WaitForSeconds(2f); // Initial delay

        while(!GameManager.Instance.isGameOver)
        {
            
            currentWave++;
            enemiesSpawnedThisWave = 0;
            isWaveActive = false;
            
            // Calculate wave difficulty progression
            CalculateWaveProgression();
            
            if (enableDebugLogs)
                Debug.Log($"=== WAVE {currentWave} PREPARATION ===\n" +
                         $"Enemies: {currentWaveEnemies}\n" +
                         $"Spawn Interval: {currentSpawnInterval:F2}s\n" +
                         $"Wave Multiplier: {waveMultiplier:F2}x\n" +
                         $"Elite Chance: {eliteSpawnChance:F1%}");
            
            // Notify wave is coming 
            OnWaveStart?.Invoke(currentWave);
            
            // Give player time to prepare
            yield return new WaitForSeconds(wavePrepTime);
            
            
            isWaveActive = true;
            
            // Check if this is a boss wave
            bool isBossWave = enableBossWave && (currentWave % bossWaveInterval == 0);
            
            if (isBossWave)
            {
                if (enableDebugLogs)
                    Debug.Log($"🔥 BOSS WAVE {currentWave} STARTED! 🔥");
                
                yield return StartCoroutine(SpawnBossWave());
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"⚔️ Wave {currentWave} STARTED - Spawning {currentWaveEnemies} enemies");

                // Spawn all enemies in the wave
                while (enemiesSpawnedThisWave < currentWaveEnemies && !GameManager.Instance.isGameOver)
                {
                    if (!GameManager.Instance.isPaused)
                    {
                        SpawnEnemy();
                        totalEnemiesSpawned++;
                        enemiesSpawnedThisWave++;
                        
                        
                        OnWaveProgress?.Invoke(enemiesSpawnedThisWave, currentWaveEnemies);
                    }

                    yield return new WaitForSeconds(currentSpawnInterval);
                }
            }

            
            isWaveActive = false;
            
            if (enableDebugLogs)
                Debug.Log($"Wave {currentWave} spawning complete. Waiting for enemies to be cleared...");
            
            // Wait for all enemies from this wave to be defeated before starting cooldown
            yield return StartCoroutine(WaitForWaveCleanup());
            
            if (enableDebugLogs)
                Debug.Log($" Wave {currentWave} comeplete Next wave in {timeBetweenWaves} seconds");
            
            // Notify wave complete
            OnWaveComplete?.Invoke(currentWave);

            // Cooldown between waves
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    // Calculate how the wave should scale up in difficulty.
    // Increases enemy count, spawn rate, elite chance, and overall difficulty.
    private void CalculateWaveProgression()
    {
        // Calculate wave multiplier 
        waveMultiplier = 1f + (currentWave - 1) * 0.15f; // 15% increase per wave
        
        // Increase number of enemies per wave
        currentWaveEnemies = Mathf.RoundToInt(enemiesPerWave * Mathf.Pow(enemiesPerWaveGrowth, currentWave - 1));
        currentWaveEnemies = Mathf.Min(currentWaveEnemies, maxEnemiesPerWave);
        
        // Decrease spawn interval (spawn faster)
        currentSpawnInterval = baseSpawnInterval * Mathf.Pow(spawnIntervalDecreaseRate, currentWave - 1);
        currentSpawnInterval = Mathf.Max(currentSpawnInterval, minWaveSpawnInterval);
        
        // Increase elite spawn chance
        if (increaseEliteChance)
        {
            eliteSpawnChance = Mathf.Min(0.1f + (eliteSpawnChance * currentWave), 0.5f); // Cap at 50%
        }
        
        // Scale difficulty score with wave progression
        currentDifficultyScore = baseDifficultyScore + (currentWave * 0.2f);
        currentDifficultyScore = Mathf.Min(currentDifficultyScore, maxDifficultyScore);
    }

    // Wait for all enemies to be defeated before proceeding to the next wave.
    // Includes a timeout to prevent infinite waiting.
    private IEnumerator WaitForWaveCleanup()
    {
        float timeout = 60f;
        float elapsed = 0f;

        while (Enemy.allEnemies.Count > 0 && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout && enableDebugLogs)
        {
            Debug.LogWarning($"Wave cleanup timeout - {Enemy.allEnemies.Count} enemies still active");
        }
    }
    #endregion

    #region Boss Wave System
    // Spawns a special boss wave with enhanced enemies and support units.
    // Boss waves occur every Nth wave and feature scaled-up enemies with visual modifications.
    private IEnumerator SpawnBossWave()
    {
        // Calculate boss wave composition
        int bossCount = Mathf.Max(2, currentWave / bossWaveInterval); // More bosses in later waves
        int miniEnemies = currentWaveEnemies / 2; // Support enemies to accompany bosses

        if (enableDebugLogs)
            Debug.Log($"Spawning {bossCount} boss enemies with {miniEnemies} support enemies");

        // First, spawn support enemies at faster rate
        for (int i = 0; i < miniEnemies && !GameManager.Instance.isGameOver; i++)
        {
            if (!GameManager.Instance.isPaused)
            {
                SpawnEnemy();
                totalEnemiesSpawned++;
                enemiesSpawnedThisWave++;
                OnWaveProgress?.Invoke(enemiesSpawnedThisWave, currentWaveEnemies);
            }

            yield return new WaitForSeconds(currentSpawnInterval * 0.5f); // Spawn support faster
        }

        yield return new WaitForSeconds(2f); // Brief pause before boss spawning

        // Then spawn boss enemies with dramatic timing
        for (int i = 0; i < bossCount && !GameManager.Instance.isGameOver; i++)
        {
            if (!GameManager.Instance.isPaused)
            {
                SpawnBossEnemy();
                totalEnemiesSpawned++;
                enemiesSpawnedThisWave++;
                OnWaveProgress?.Invoke(enemiesSpawnedThisWave, currentWaveEnemies);
            }
            yield return new WaitForSeconds(2f); // Longer delay between boss spawns for impact
        }
    }
    
    // Spawns a single boss enemy with enhanced stats and visual modifications.
    // Boss enemies are always Hard type with significant stat multipliers.
    private void SpawnBossEnemy()
    {
        Vector2Int spawnLocation = SelectSpawnLocation();
        EnemyType enemyType = EnemyType.Hard; // Bosses are always Hard type

        SpawnEnemyAtLocation(spawnLocation, enemyType, isBoss: true);
        UpdateSpawnTracking(spawnLocation, enemyType);
    }

    #endregion

    #region Enemy Spawning
    // Main enemy spawning method that handles location selection, enemy type determination,
    // and spawn tracking. This is the primary entry point for all enemy spawning.
    private void SpawnEnemy()
    {
        Vector2Int spawnLocation = SelectSpawnLocation();
        EnemyType enemyType = SelectEnemyType();

        SpawnEnemyAtLocation(spawnLocation, enemyType);
        UpdateSpawnTracking(spawnLocation, enemyType);
    }

    // Intelligently selects a spawn location using weighted probability system.
    // Considers defender proximity, distance from tower, and recent usage patterns.
    private Vector2Int SelectSpawnLocation()
    {
        // Safety check = ensure spawn points are available
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
    {
        Debug.LogError("ProceduralSpawnManager: No spawn points available! Cannot spawn enemies safely.");
        
        // Attempt emergency reinitialization
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
                Debug.LogError("ProceduralSpawnManager: CRITICAL - Cannot find any spawn points. Halting enemy spawning.");
                StopAllCoroutines(); 
                return Vector2Int.zero; 
            }
        }
        
        // Final safety check
        if (availableSpawnPoints == null || availableSpawnPoints.Count == 0)
        {
            return Vector2Int.zero; 
        }
    }
    
    // Update weights based on current game state
    UpdateSpawnLocationWeights();
    
    // Calculate total weight for weighted random selection
    float totalWeight = 0f;
    foreach (var kvp in spawnLocationWeights)
    {
        if (kvp.Value > 0)
        {
            totalWeight += kvp.Value;
        }
    }
    
    // Safety check for valid weights
    if (totalWeight <= 0f)
    {
        Debug.LogWarning("ProceduralSpawnManager: All spawn weights were 0 or negative. Resetting to default.");
        InitializeSpawnWeights();
        totalWeight = spawnLocationWeights.Values.Sum();
    }
    
    // Weighted random selection
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
    
    // Fallback to random selection if weighted selection fails
    Debug.LogWarning("ProceduralSpawnManager: Weighted selection failed, using random fallback.");
    return availableSpawnPoints[Random.Range(0, availableSpawnPoints.Count)];
    }

    // Updates the weight values for all spawn locations based on strategic factors.
    // Higher weights indicate better spawning locations for challenging the player.
    private void UpdateSpawnLocationWeights()
    {
        foreach(Vector2Int spawnPoint in availableSpawnPoints)
        {
            float weight = 1f; // Base weight

            // Factor 1: Time since last use (encourages spawn point variety)
            float timeSinceLastUsed = Time.time - spawnLocationLastUsed[spawnPoint];
            weight *= Mathf.Clamp(timeSinceLastUsed / 10f, 0.3f, 2f); // Scale from 30% to 200%

            // Factor 2: Defender proximity (prefer less defended areas)
            float defenderProx = CalcDefProxScore(spawnPoint);
            weight *= (1f + defenderProx); // Boost weight for less defended areas

            // Factor 3: Distance to tower (prefer strategic distances)
            float distanceTower = CalcDistToTower(spawnPoint);
            weight *= Mathf.Clamp(1f / (distanceTower + 1f), 0.5f, 1.5f); // Moderate distance preference

            spawnLocationWeights[spawnPoint] = weight;
        }
    }


    // Calculates a score based on defender coverage around a spawn point.
    // Returns higher scores for areas with less defensive coverage.
    private float CalcDefProxScore(Vector2Int spawnPoint)
    {
    if (turretPlacementManager == null) return 0f;
    
    Vector3 spawnWorldPos = hexGridGenerator.HexToWorld(spawnPoint);
    int nearbyDefenders = 0;
    float searchRadius = 15f; // Detection radius for nearby defenders
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
        return 2f; // No defenders nearby = high priority spawn location
    }
    else if (nearbyDefenders == 1)
    {
        return 1f; // Some defense = medium priority
    }
    else
    {
        // Multiple defenders = low priority
        // The more defenders, the lower the score
        return Mathf.Max(0.1f, 1f / nearbyDefenders);
    }
    }

    // Calculates the distance from a spawn point to the central tower.
    // Used for strategic spawn location weighting.
    private float CalcDistToTower(Vector2Int spawnPoint)
    {
        if (centralTower == null) return 10f; // Default fallback distance

        Vector3 spawnWorldPos = hexGridGenerator.HexToWorld(spawnPoint);
        return Vector3.Distance(spawnWorldPos, centralTower.transform.position);
    }

    // Determines which enemy type to spawn based on elite timing, wave progression,
    // and recent spawn history for variety.
    private EnemyType SelectEnemyType()
    {
        // Check for elite spawn timing first (overrides normal selection)
        if (Time.time >= nextEliteSpawnTime && Random.value < eliteSpawnChance)
        {
            nextEliteSpawnTime = Time.time + eliteSpawnInterval;
            return EnemyType.Hard; // Elite spawns are always Hard type
        }

        // Use weighted system for normal enemy type selection
        Dictionary<EnemyType, float> typeWeights = CalculateEnemyTypeWeights();

        // Weighted random selection
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
        
        return EnemyType.Easy; // Fallback to Easy if selection fails
    }

    // Calculates weighted probabilities for each enemy type based on wave progression,
    // difficulty level, and recent spawn variety. Creates dynamic enemy composition.
    private Dictionary<EnemyType, float> CalculateEnemyTypeWeights()
    {
        Dictionary<EnemyType, float> weights = new Dictionary<EnemyType, float>();
        
        // Base weights shift based on both difficulty and wave number
        float waveProgress = (float)currentWave / 10f; // Normalize wave progression
        
        // Wave-based progression system for enemy type distribution
        if (currentWave <= 3)
        {
            // Early waves =  mostly easy enemies to teach player mechanics
            weights[EnemyType.Easy] = 4f;
            weights[EnemyType.Medium] = 1f;
            weights[EnemyType.Hard] = 0.1f;
        }
        else if (currentWave <= 7)
        {
            // Mid-early waves = introducing medium enemies
            weights[EnemyType.Easy] = 3f;
            weights[EnemyType.Medium] = 2f;
            weights[EnemyType.Hard] = 0.5f;
        }
        else if (currentWave <= 12)
        {
            // Mid waves = balanced with more challenge
            weights[EnemyType.Easy] = 2f;
            weights[EnemyType.Medium] = 3f;
            weights[EnemyType.Hard] = 1.5f;
        }
        else if (currentWave <= 20)
        {
            // Late waves = harder enemies dominate
            weights[EnemyType.Easy] = 1f;
            weights[EnemyType.Medium] = 2f;
            weights[EnemyType.Hard] = 3f;
        }
        else
        {
            // End game = mostly hard enemies for maximum challenge
            weights[EnemyType.Easy] = 0.5f;
            weights[EnemyType.Medium] = 1.5f;
            weights[EnemyType.Hard] = 4f;
        }
        
        // Further adjust by current difficulty score (adaptive difficulty)
        if (currentDifficultyScore > 3f)
        {
            weights[EnemyType.Hard] *= 1.5f;  // More hard enemies if player doing well
            weights[EnemyType.Easy] *= 0.5f;  // Fewer easy enemies
        }
        
        // Reduce weight for recently spawned types (encourage variety)
        foreach (EnemyType recentType in recentlySpawnedTypes)
        {
            if (weights.ContainsKey(recentType))
            {
                weights[recentType] *= 0.7f; // 30% reduction for recently used types
            }
        }
        
        return weights;
    }

    // Creates and positions an enemy at the specified location with scaling based on wave progression.
    // Handles NavMesh validation, prefab instantiation, and boss modifications.
    private void SpawnEnemyAtLocation(Vector2Int spawnCoord, EnemyType enemyType, bool isBoss = false)
    {
        // Get the physical tile at the spawn coordinate
        GameObject spawnTile = hexGridGenerator.GetTileAt(spawnCoord);
        if (spawnTile == null)
        {
            Debug.LogError($"No tile found at {spawnCoord}");
            return;
        }

        // Get the enemy prefab for the specified type
        GameObject prefab = GetEnemyPrefabForType(enemyType);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for enemy type {enemyType}");
            return;
        }

        // Calculate spawn position slightly above the tile
        Vector3 spawnPos = spawnTile.transform.position + Vector3.up * 0.5f;

        // Validate NavMesh accessibility at spawn position
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            // Instantiate enemy at valid NavMesh position
            GameObject newEnemy = Instantiate(prefab, hit.position, Quaternion.identity);

            // Scale enemy with wave progression if enabled
            Enemy enemyComponent = newEnemy.GetComponent<Enemy>();
            if (enemyComponent != null && scaleEnemyHalthWithWaves)
            {
                ScaleEnemyForWave(enemyComponent, isBoss);
            }

            // Debug logging for spawn tracking
            if (enableDebugLogs)
            {
                string bossLabel = isBoss ? "BOSS" : "";
                Debug.LogWarning($"Spawned {enemyType}{bossLabel} at {spawnCoord} " +
                         $"(Wave {currentWave}, Difficulty: {currentDifficultyScore:F2}, " +
                         $"Health: {enemyComponent?.health:F0})");
            }
        }
    }
    
    // Retrieves the appropriate enemy prefab for the specified enemy type.
    // Maps EnemyType enum to actual GameObject prefabs.
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
    
    // Updates spawn tracking data for AI decision making.
    // Records when and where enemies were spawned for location weighting and type variety.
    private void UpdateSpawnTracking(Vector2Int location, EnemyType type)
    {
        // Update location tracking for spawn point weighting
        spawnLocationLastUsed[location] = Time.time;
        
        // Update type history for variety enforcement
        recentlySpawnedTypes.Enqueue(type);
        if (recentlySpawnedTypes.Count > recentTypeHistorySize)
        {
            recentlySpawnedTypes.Dequeue(); // Remove oldest entry to maintain size limit
        }
    }
    
    #endregion
    
    #region Difficulty Adjustment
    // Continuous monitoring system that adjusts difficulty based on player performance.
    // Runs independently of the spawning system to maintain responsive difficulty scaling.
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

    // Analyzes player performance and adjusts difficulty accordingly.
    // Increases challenge when player is succeeding, reduces when struggling.
    private void AdjustDifficulty()
    {
        float performanceScore = CalculatePerformanceScore();

        // Adjust difficulty based on performance
        if (performanceScore > 0.7f)
        {
            // Player is doing well, increase difficulty to maintain challenge
            currentDifficultyScore = Mathf.Min(currentDifficultyScore + difficultyIncreaseRate, maxDifficultyScore);
        }
        else if (performanceScore < 0.3f)
        {
            // Player is struggling,  decrease difficulty to prevent frustration
            currentDifficultyScore = Mathf.Max(currentDifficultyScore - difficultyIncreaseRate * 0.5f, baseDifficultyScore);
        }
        // Performance between 0.3-0.7 maintains current difficulty

        // Adjust spawn interval based on difficulty (higher difficulty = faster spawning)
        float intervalRange = maxSpawnInterval - minSpawnInterval;
        float normalizedDifficulty = (currentDifficultyScore - baseDifficultyScore) / (maxDifficultyScore - baseDifficultyScore);
        currentSpawnInterval = maxSpawnInterval - (intervalRange * normalizedDifficulty);

        if (enableDebugLogs)
        {
            Debug.Log($"Difficulty adjusted - Score: {currentDifficultyScore:F2}, Performance: {performanceScore:F2}, Spawn Interval: {currentSpawnInterval:F2}s");
        }
    }
    
    // Calculates overall player performance score based on multiple metrics.
    // Combines tower health, resource management, and enemy elimination efficiency.
    private float CalculatePerformanceScore()
    {
        float score = 0f;
        int factors = 0;

        // Factor 1: Tower health (40% weight) - most critical metric
        if (centralTower != null)
        {
            float healthRatio = centralTower.health / centralTower.maxHealth;

            if (healthRatio >= targetTowerHealthThreshold)
            {
                score += 0.9f * 0.4f; // Excellent tower health
            }
            else if (healthRatio <= criticalTowerHealthThreshold)
            {
                score += 0.1f * 0.4f; // Critical tower health
            }
            else
            {
                // Linear interpolation between thresholds
                score += (healthRatio - criticalTowerHealthThreshold) /
                         (targetTowerHealthThreshold - criticalTowerHealthThreshold) * 0.4f;
            }
            factors++;
        }

        // Factor 2: Resource accumulation (30% weight) - economic performance
        if (turretPlacementManager != null)
        {
            float currentResources = turretPlacementManager.PlayerResources;
            resournceAccumulationRate = (currentResources - lastResourceCount) / difficultyCheckInterval;
            lastResourceCount = currentResources;

            if (currentResources >= highResourceThreshold)
            {
                score += 0.9f * 0.3f; // High resource accumulation
            }
            else if (currentResources <= lowResourceThreshold)
            {
                score += 0.1f * 0.3f; // Low resources - struggling
            }
            else
            {
                // Linear scaling between resource thresholds
                score += ((currentResources - lowResourceThreshold) /
                         (highResourceThreshold - lowResourceThreshold)) * 0.3f;
            }
            factors++;
        }

        // Factor 3: Enemy elimination efficiency (30% weight) - combat effectiveness
        int activeEnemies = Enemy.allEnemies.Count;
        totalEnemiesKilled = totalEnemiesSpawned - activeEnemies;

        if (totalEnemiesSpawned > 0)
        {
            float eliminationRate = (float)totalEnemiesKilled / totalEnemiesSpawned;
            score += eliminationRate * 0.3f;
            factors++;
        }

        return factors > 0 ? score : 0.5f; // Default to neutral if no factors available
    }
    
    // Scales enemy stats based on wave progression and boss status.
    // Applies health, damage, speed, and visual scaling for progressive difficulty.
    private void ScaleEnemyForWave(Enemy enemy, bool isBoss)
    {
        if (enemy == null) return;
        
        // Calculate scaling based on wave number
        float healthMultiplier = Mathf.Pow(enemyHealthScaling, currentWave - 1);
        
        // Boss enemies get extra scaling
        if (isBoss)
        {
            healthMultiplier *= 8f; // Bosses have 8x more health
            enemy.attackDamage *= 1.8f; // Bosses deal 80% more damage
            
            // Visual modifications for boss enemies
            Transform visualTransform = enemy.transform;
            visualTransform.localScale *= 2f; // Make bosses twice as big
            
            // Color modification to indicate boss status
            Renderer renderer = enemy.GetComponent<Renderer>();
            if (renderer == null)
                renderer = enemy.GetComponentInChildren<Renderer>();
            
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = Color.Lerp(renderer.material.color, Color.red, 0.5f);
            }
        }
        
        // Use reflection to modify private _maxHealth field
        var maxHealthField = typeof(Enemy).GetField("_maxHealth", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (maxHealthField != null)
        {
            float currentMaxHealth = enemy.maxHealth;
            float scaledMaxHealth = currentMaxHealth * healthMultiplier;
            maxHealthField.SetValue(enemy, scaledMaxHealth);
            
            // Set current health to the new scaled max health
            enemy.health = scaledMaxHealth;
        }
        else
        {
            Debug.LogError("Could not find _maxHealth field in Enemy class!");
        }

        // Slight speed increase for variety 
        enemy.speed *= 1f + (currentWave * 0.02f);

        // Decrease resource reward to balance economy
        enemy.resourceReward /= 0.5f;
        
        if (enableDebugLogs && isBoss)
            Debug.Log($"Boss scaled: Health={enemy.health:F0}, Damage={enemy.attackDamage:F0}");
    }
    
    #endregion
    
    #region Public API
    // Called externally when an enemy is killed to update tracking statistics.
    // Used for difficulty adjustment and performance monitoring.
    public void NotifyEnemyKilled() => totalEnemiesKilled++;
    
    // Called when a defender is placed to track player strategy patterns.
    // Helps the AI understand defensive coverage for intelligent spawn selection.
    public void NotifyDefenderPlaced(Vector2Int coords)
    {
        if (!defenderPlacements.ContainsKey(coords))
        {
            defenderPlacements[coords] = 0;
        }
        defenderPlacements[coords]++;
    }

    // Gets the current difficulty score for external systems.
    public float GetCurrentDifficulty() => currentDifficultyScore;
    
    // Gets the current wave number for UI display.
    public int GetCurrentWave() => currentWave;
    
    // Checks if a wave is currently active for gameplay state management.
    public bool IsWaveActive() => isWaveActive;
    
    // Gets current wave progress for UI display.
    public (int current, int total) GetWaveProgress() => (enemiesSpawnedThisWave, enemiesPerWave);
    
    // Gets remaining enemies to spawn in current wave.
    public int GetRemainingEnemiesInWave() => enemiesPerWave - enemiesSpawnedThisWave;
    #endregion
}