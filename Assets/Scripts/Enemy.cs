using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

//represents an enemy unit that moves along path and attacks the tower
public class Enemy : MonoBehaviour, IDefendable
{
    //IDefendable implementation
    public float health { get; set; }
    public Vector3 transform_position => transform.position;

    //static list keeping track of all active enemies in scene
    public static List<Enemy> allEnemies = new List<Enemy>();

    [Header("Enemy Stats")]
    public float maxHealth = 20f;
    public float speed = 5f;
    public float attackDamage = 5f;
    public float attackRange = 1.5f;
    public float attackRate = 1.5f; //attacks per second
    private float nextAttackTime = 0f;

    [Header("Targeting")]
    public float aggroRange = 10f; //how close defender must be to become a target
    private float targetCheckInterval = 0.5f; //how often to check for new targets

    [Header("Resource Reward")]
    public float resourceReward = 10f;

    [Header("Path Following")]
    public float pathNodeReachDistance = 1.0f; //distance to consider a path node "reached"

    //references
    private NavMeshAgent agent;
    private IDefendable currentTarget;
    private HexGrid hexGrid;
    private HexGridGenerator hexGridGenerator;
    private Pathfinder pathfinder;
    private Transform centralTowerTransform;

    //path following
    private List<Vector2Int> currentPath;
    private int currentPathIndex = 0;
    private bool followingPath = true;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("Enemy prefab missing NavMeshAgent", this);
        }
        health = maxHealth;

        // Set agent speed
        if (agent != null)
        {
            agent.speed = speed;
        }
    }

    private void OnEnable()
    {
        //add this enemy to static list when it's enabled
        allEnemies.Add(this);

        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance?.AddDefendable(this); //enemies can now be targeted
        }
    }

    private void OnDisable()
    {
        //Remove this enemy from static list when it is disabled or destroyed
        allEnemies.Remove(this);

        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance?.RemoveDefendable(this);
        }
    }

    private void Start()
    {
        InitializeReferences();
        SetupInitialPath();

        //check for closer targets periodically
        InvokeRepeating(nameof(FindBestTarget), 0f, targetCheckInterval);
    }

    private void InitializeReferences()
    {
        hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        hexGrid = FindFirstObjectByType<HexGrid>();
        pathfinder = FindFirstObjectByType<Pathfinder>();

        GameObject towerObject = hexGridGenerator?.GetTowerInstance();
        if (towerObject != null)
        {
            centralTowerTransform = towerObject.transform;
            currentTarget = towerObject.GetComponent<IDefendable>();
        }
        else
        {
            Debug.LogError("Could not find the central tower instance!");
        }
    }

    private void SetupInitialPath()
    {
        if (pathfinder == null || hexGridGenerator == null)
        {
            Debug.LogError("Missing pathfinder or hexGridGenerator reference!");
            return;
        }

        // Find the closest spawn point to our current position
        List<Vector2Int> spawnPoints = hexGridGenerator.GetSpawnPointCoords();
        Vector2Int closestSpawnPoint = Vector2Int.zero;
        float closestDistance = float.MaxValue;

        foreach (Vector2Int spawnPoint in spawnPoints)
        {
            Vector3 spawnWorldPos = hexGridGenerator.HexToWorld(spawnPoint);
            float distance = Vector3.Distance(transform.position, spawnWorldPos);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSpawnPoint = spawnPoint;
            }
        }

        // Get the path from this spawn point
        currentPath = pathfinder.GetPathFromSpawnPoint(closestSpawnPoint);

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogError($"No path found for spawn point {closestSpawnPoint}!");
            followingPath = false;
            // Fall back to direct movement to tower
            if (agent != null && currentTarget != null)
            {
                agent.SetDestination(currentTarget.transform_position);
            }
            return;
        }

        currentPathIndex = 0;
        followingPath = true;

        Debug.Log($"Enemy initialized with path of {currentPath.Count} nodes");

        // Start following the path
        MoveToNextPathNode();
    }

    private void Update()
    {
        if (agent == null || Time.timeScale == 0) return;

        // Handle path following
        if (followingPath && currentPath != null && currentPathIndex < currentPath.Count)
        {
            UpdatePathFollowing();
        }

        // Handle target validation
        if (currentTarget == null || (currentTarget as MonoBehaviour)?.gameObject == null)
        {
            FindBestTarget();
            if (currentTarget == null)
            {
                agent.isStopped = true;
                return;
            }
        }

        // Handle attacking when in range
        if (currentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform_position);

            if (distanceToTarget <= attackRange)
            {
                agent.isStopped = true;
                followingPath = false; // Stop following path when attacking

                if (Time.time >= nextAttackTime)
                {
                    AttackTarget();
                    nextAttackTime = Time.time + 1f / attackRate;
                }
            }
            else if (!followingPath)
            {
                // If not following path and not in attack range, move directly to target
                agent.isStopped = false;
                if (agent.destination != currentTarget.transform_position)
                {
                    agent.SetDestination(currentTarget.transform_position);
                }
            }
        }
    }

    private void UpdatePathFollowing()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            followingPath = false;
            // Path complete, move directly to final target
            if (agent != null && currentTarget != null)
            {
                agent.SetDestination(currentTarget.transform_position);
            }
            return;
        }

        Vector3 targetNodeWorld = hexGridGenerator.HexToWorld(currentPath[currentPathIndex]);
        float distanceToNode = Vector3.Distance(transform.position, targetNodeWorld);

        if (distanceToNode <= pathNodeReachDistance)
        {
            currentPathIndex++;
            MoveToNextPathNode();
        }
    }

    private void MoveToNextPathNode()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            followingPath = false;
            // Path complete, move to final target (tower)
            if (agent != null && currentTarget != null)
            {
                agent.SetDestination(currentTarget.transform_position);
                Debug.Log("Enemy completed path, moving to final target");
            }
            return;
        }

        Vector3 nextNodeWorld = hexGridGenerator.HexToWorld(currentPath[currentPathIndex]);

        if (agent != null)
        {
            agent.isStopped = false;
            agent.SetDestination(nextNodeWorld);
        }
    }

    private void FindBestTarget()
    {
        IDefendable closestDefender = null;
        float minDistance = Mathf.Infinity;

        // Find all turrets and tower
        var turrets = FindObjectsByType<PlaceableTurret>(FindObjectsSortMode.None);
        var tower = FindFirstObjectByType<Tower>();

        // Check distance to turrets
        foreach (var turret in turrets)
        {
            float distance = Vector3.Distance(transform.position, turret.transform.position);
            if (distance < minDistance && distance <= aggroRange)
            {
                minDistance = distance;
                closestDefender = turret.GetComponent<IDefendable>();
            }
        }

        if (tower != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, tower.transform.position);
            if (distanceToTower < minDistance)
            {
                closestDefender = tower;
            }
        }

        // If defender found in range, target it, otherwise default to central tower
        if (closestDefender != null)
        {
            currentTarget = closestDefender;
        }
        else if (centralTowerTransform != null)
        {
            currentTarget = centralTowerTransform.GetComponent<IDefendable>();
        }
    }

    //inflicts damage on current target
    private void AttackTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.TakeDamage(attackDamage);
        }
    }

    //reduces the enemy's health and handles its destruction
    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log($"Enemy health: {health}");
        if (health <= 0)
        {
            //add resources when enemy defeated
            FindFirstObjectByType<TurretPlacementManager>()?.AddResources(resourceReward);
            Destroy(gameObject);
        }
    }

    // Debug method to visualize the current path
    private void OnDrawGizmosSelected()
    {
        if (currentPath != null && hexGridGenerator != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < currentPath.Count; i++)
            {
                Vector3 nodePos = hexGridGenerator.HexToWorld(currentPath[i]);
                Gizmos.DrawWireSphere(nodePos, 0.5f);

                if (i == currentPathIndex)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(nodePos, 0.7f);
                    Gizmos.color = Color.red;
                }

                if (i > 0)
                {
                    Vector3 prevNodePos = hexGridGenerator.HexToWorld(currentPath[i - 1]);
                    Gizmos.DrawLine(prevNodePos, nodePos);
                }
            }
        }
    }
}