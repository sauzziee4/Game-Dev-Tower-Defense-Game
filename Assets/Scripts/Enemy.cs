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
    private HexGrid hexGrid;
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

        if (currentTarget == null || (currentTarget as MonoBehaviour)?.gameObject == null)
        {
            //if target destroyed, find new one
            FindBestTarget();
            if (currentTarget == null)
            {
                agent.isStopped = true;
            }
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

        if (tower != null)
        {
            float distanceToTower = Vector3.Distance(transform.position, tower.transform.position);
            if (distanceToTower < minDistance)
            {
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