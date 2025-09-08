using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

//represents an enemy unit that moves along path and attacks the tower
//this is a placeholder and needs to be intergrated with pathfinding logic
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
    public float attackRate = 1.5f; //attacks per secound
    private float nextAttackTime = 0f;

    [Header("Targeting")]
    public float aggroRange = 10f; //how close defender must be to become a target

    private float targetCheckInterval = 0.5f; //how often to chheck for new targets

    [Header("Resource Reward")]
    public float resourceReward = 10f;

    //references
    private NavMeshAgent agent;

    private IDefendable currentTarget;
    private Transform centralTowerTransform;

    /* private List<Vector2Int> path;
     private int currentPathIndex = 0;
     private HexGridGenerator hexGridGenerator; */

    // private HexGrid hexGrid;
    /* private IDefendable centralTower;

     private bool hasReachedTower;*/

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.Log("enemy prefab missing navMeshAgent", this);
        }
        health = maxHealth;
    }

    private void OnEnable()
    {
        //add this enemy to static list when it's enabled
        allEnemies.Add(this);
        DefendableManager.Instance?.AddDefendable(this); //enemies can now be targeted
    }

    private void OnDisable()
    {
        //Remove this enemy from static list when it is disabled or destroyed
        allEnemies.Remove(this);
        DefendableManager.Instance?.RemoveDefendable(this);
    }

    private void Start()
    {
        HexGridGenerator hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        GameObject towerObject = hexGridGenerator?.GetTowerInstance();
        if (towerObject != null)
        {
            //set main tower as default primary target
            centralTowerTransform = towerObject.transform;
            currentTarget = towerObject.GetComponent<IDefendable>();
        }
        //check for closer targets periodically
        InvokeRepeating(nameof(FindBestTarget), 0f, targetCheckInterval);
    }

    private void Update()
    {
        if (currentTarget == null || (currentTarget as MonoBehaviour)?.gameObject == null)
        {
            //if target destroyed, find new one
            FindBestTarget();
            if (currentTarget == null) return;
        }

        //check if inrange to attack current target
        if (Vector3.Distance(transform.position, currentTarget.transform_position) <= attackRange)
        {
            agent.isStopped = true; //stop moving

            if (Time.time >= nextAttackTime)
            {
                AttackTarget();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }
        else
        {
            agent.isStopped = false;
            //make sure agent is still heading towards current target
            if (agent.destination != currentTarget.transform_position)
            {
                agent.SetDestination(currentTarget.transform_position);
            }
        }
    }

    private void FindBestTarget()
    {
        IDefendable closestDefender = DefendableManager.Instance.GetClosestDefendable(transform.position);

        //find closest defendable within aggrorange
        if (closestDefender != null && Vector3.Distance(transform.position, closestDefender.transform_position) <= aggroRange)
        {
            currentTarget = closestDefender;
        }
        else
        {
            //if no defender nearby, default to main tower
            currentTarget = centralTowerTransform.GetComponent<IDefendable>();
        }
    }

    /*
    public void SetPath(List<Vector2Int> newPath)
    {
        path = newPath;
        currentPathIndex = 0;
    }

    private void FollowPath()
    {
        if (path == null || path.Count == 0 || hexGridGenerator == null) return;

        // Check if we have reached the current point in the path.
        Vector3 currentTarget = hexGridGenerator.HexToWorld(path[currentPathIndex]);
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
        if (distanceToTarget < 0.2f)
        {
            currentPathIndex++;
            if (currentPathIndex >= path.Count)
            {
                hasReachedTower = true;
                return;
            }
        }

        // Move towards the next point on the path
        if (currentPathIndex < path.Count)
        {
            Vector3 nextWaypoint = hexGridGenerator.HexToWorld(path[currentPathIndex]);
            Vector3 direction = (nextWaypoint - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    //finds closest defender or the tower to attack
    private void FindTarget()
    {
        //find all objects in scene that defends
        IDefendable closestTarget = DefendableManager.Instance.GetClosestDefendable(transform.position);
        if (closestTarget != null)
        {
            target = (closestTarget as MonoBehaviour).transform;
        }
    }
    */

    //inflicts damage on current target
    private void AttackTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.TakeDamage(attackDamage);
        }
    }

    //reduces the enemy's health and handles iits destruction
    //"damage" is amount of health to be reduced by
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
}