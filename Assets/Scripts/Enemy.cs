using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyType
{
    Easy,
    Medium,
    Hard
}
//represents an enemy unit that moves along path and attacks the tower
//this is a placeholder and needs to be intergrated with pathfinding logic
public class Enemy : MonoBehaviour, IDefendable
{
    private Animator animator;

    private static readonly int IsSpawningHash = Animator.StringToHash("isSpawning");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");

    //IDefendable implementation
    public float health { get; set; }

    public Vector3 transform_position => transform.position;

    //static list keeping track of all active enemies in scene
    public static List<Enemy> allEnemies = new List<Enemy>();

    [Header("Enemy Stats")]
    public EnemyType enemyType;
    public float maxHealth = 20f;

    public float speed = 5f;
    public float attackDamage = 5f;
    public float attackRange = 1.5f;
    public float attackRate = 1.5f; //attacks per secound
    private float nextAttackTime = 0f;

    [Header("Targeting")]
    public float aggroRange = 10f; //how close defender must be to become a target

    private float targetCheckInterval = 0.5f; //how often to check for new targets

    [Header("Resource Reward")]
    public float resourceReward = 10f;

    //references
    private NavMeshAgent agent;

    private IDefendable currentTarget;
    private HexGrid hexGrid;
    private Transform centralTowerTransform;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.Log("enemy prefab missing navMeshAgent", this);
        }
        if (animator == null)
        {
            Debug.Log("enemy prefab missing Animator", this);
        }
        health = maxHealth;

        // Start with spawn animation
        if (animator != null)
        {
            animator.SetBool(IsSpawningHash, true);
            // Automatically transition to run after spawn animation
            StartCoroutine(TransitionAfterSpawn());
        }
    }

    private IEnumerator TransitionAfterSpawn()
    {
        // Adjust this time to match your spawn animation length
        yield return new WaitForSeconds(1.5f);
        animator.SetBool(IsSpawningHash, false);
        animator.SetBool(IsMovingHash, true);
    }

    private void ConfigureEnemyStats()
    {
        switch (enemyType)
        {
            case EnemyType.Easy:
                maxHealth = 20f;
                attackDamage = 3f;
                speed = 7f;
                resourceReward = 3f;
                break;

            case EnemyType.Medium:
                //keep default values
                resourceReward = 5f;
                break;

            case EnemyType.Hard:
                maxHealth = 50f;
                attackDamage = 8f;
                speed = 3.5f;
                resourceReward = 10f;
                break;
        }

        //set initial health and update NavMeshAgent speed
        health = maxHealth;
        if (agent != null)
        {
            agent.speed = speed;
        }
        Debug.Log($"Configured stats for {enemyType}: health={health}, speed={speed}, attackDamage={attackDamage}");
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
        ConfigureEnemyStats();
        HexGridGenerator hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        hexGrid = FindFirstObjectByType<HexGrid>();
        GameObject towerObject = hexGridGenerator?.GetTowerInstance();
        if (towerObject != null)
        {
            //set main tower as default primary target
            centralTowerTransform = towerObject.transform;
            currentTarget = towerObject.GetComponent<IDefendable>();

            if (agent != null && currentTarget != null)
            {
                agent.SetDestination(currentTarget.transform_position);
            }
        }
        else
        {
            Debug.LogError("Could not find the central tower instance!");
        }
        //check for closer targets periodically
        InvokeRepeating(nameof(FindBestTarget), 0f, targetCheckInterval);
    }

    private void Update()
    {
        if (agent == null || Time.timeScale == 0) return;

        // More robust target validation
        if (!IsValidTarget(currentTarget))
        {
            currentTarget = null;
            FindBestTarget();

            if (currentTarget == null)
            {
                agent.isStopped = true;
                return;
            }
        }

        // Ensure we have a valid target before proceeding
        if (currentTarget != null && IsValidTarget(currentTarget))
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform_position);

            // Check if in range to attack current target
            if (distanceToTarget <= attackRange)
            {
                agent.isStopped = true; // stop moving

                // Update animations
                if (animator != null)
                {
                    animator.SetBool(IsMovingHash, false);
                    animator.SetBool(IsAttackingHash, true);
                }

                if (Time.time >= nextAttackTime)
                {
                    AttackTarget();
                    nextAttackTime = Time.time + 1f / attackRate;
                }
            }
            else
            {
                agent.isStopped = false;

                // Update animations
                if (animator != null)
                {
                    animator.SetBool(IsMovingHash, true);
                    animator.SetBool(IsAttackingHash, false);
                }

                // Make sure agent is still heading towards current target
                if (agent.destination != currentTarget.transform_position)
                {
                    agent.SetDestination(currentTarget.transform_position);
                }
            }
        }
    }

    private bool IsValidTarget(IDefendable target)
    {
        if (target == null) return false;

        // Check if the target is a MonoBehaviour and if its GameObject still exists
        if (target is MonoBehaviour targetMono)
        {
            // This will return true if the GameObject has been destroyed
            if (targetMono == null || targetMono.gameObject == null)
            {
                return false;
            }
        }

        return true;
    }

    private void FindBestTarget()
    {
        IDefendable closestDefender = null;
        float minDistance = Mathf.Infinity;

        //find all turrets and tower
        var turrets = FindObjectsByType<PlaceableTurret>(FindObjectsSortMode.None);
        var tower = FindFirstObjectByType<Tower>();

        //check distance to turrets
        foreach (var turret in turrets)
        {
            float distance = Vector3.Distance(transform.position, turret.transform.position);
            if (distance < minDistance && distance <= aggroRange)
            {
                minDistance = distance;
                closestDefender = turret.GetComponent<IDefendable>();
            }
        }

        //make sure tower variable is not empty
        if (tower != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, tower.transform.position);
            if (distanceToTower < minDistance)
            {
                //if tower is closest defendable, it takes priority
                closestDefender = tower;
            }
        }

        //if defender found in range, target, otherwise deafult to central tower
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
        if (IsValidTarget(currentTarget))
        {
            currentTarget.TakeDamage(attackDamage);
        }
        else
        {
            // Target became invalid during attack, clear it
            currentTarget = null;
        }
    }

    //reduces the enemy's health and handles its destruction
    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log($"Enemy health: {health}");
        if (health <= 0)
        {
            FindFirstObjectByType<TurretPlacementManager>()?.AddResources(resourceReward);
            Die();
        }
    }

    public void Die()
    {
        if (ProceduralSpawnManager.Instance != null)
        {
            ProceduralSpawnManager.Instance.NotifyEnemyKilled();
        }

        allEnemies.Remove(this);

        Destroy(gameObject);
    }
}