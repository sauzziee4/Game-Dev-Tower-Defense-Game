using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

//represents an enemy unit that moves along path and attacks the tower
//this is a placeholder and needs to be intergrated with pathfinding logic
public class Enemy : MonoBehaviour
{
    //static list keeping track of all active enemies in scene
    public static List<Enemy> allEnemies = new List<Enemy>();

    [Header("Enemy Stats")]
    public float health = 20f;

    public float maxHealth = 20f;
    public float speed = 5f;
    public float attackDamage = 5f;
    public float attackRange = 1.5f;
    public float attackRate = 1.5f; //attacks per secound
    private float nextAttackTime = 0f;

    private Transform target; //can be defender or tower
    private HexTile currentTile; //current tile enemy is on for pathfinding
    // private NavMeshAgent navMeshAgent;

    private List<Vector2Int> path;
    private int currentPathIndex = 0;
    private HexGridGenerator hexGridGenerator;
    private HexGrid hexGrid;
    private IDefendable centralTower;

    /* private void Awake()
    {
        //get navMeshAgent compo and set speed
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            navMeshAgent.speed = speed;
        }
    }
    */

    private void OnEnable()
    {
        //add this enemy to static list when it's enabled
        allEnemies.Add(this);
    }

    private void OnDisable()
    {
        //Remove this enemy from static list when it is disabled or destroyed
        allEnemies.Remove(this);
    }

    private void Start()
    {
        hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        centralTower = hexGridGenerator?.GetTowerInstance()?.GetComponent<IDefendable>();
    }

    private void Update()
    {
        if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange)
        {
            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + 1f / attackRate;
                AttackTarget();
            }
        }
        else
        {
            FollowPath();
        }
    }

    public void SetPath(List<Vector2Int> newPath)
    {
        path = newPath;
        currentPathIndex = 0;
        // Set initial target to the first point on the path
        if (path != null && path.Count > 0)
        {
            target = hexGrid.GetTileAt(path[currentPathIndex]).transform;
        }
    }

    private void FollowPath()
    {
        if (path == null || path.Count == 0) return;

        // Check if we have reached the current point in the path.
        if (Vector3.Distance(transform.position, hexGridGenerator.HexToWorld(path[currentPathIndex])) < 0.1f)
        {
            currentPathIndex++;
            if (currentPathIndex >= path.Count)
            {
                // We have reached the end of the path (the central tower)
                if (centralTower != null)
                {
                    target = (centralTower as MonoBehaviour).transform;
                    // The enemy is now close enough to attack. We stop pathfinding.
                    return;
                }
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

    //inflicts damage on current target
    private void AttackTarget()
    {
        if (target == null) return;

        IDefendable defendableTarget = target.GetComponent<IDefendable>();
        if (defendableTarget != null)
        {
            defendableTarget.TakeDamage(attackDamage);
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
            Destroy(gameObject);
        }
    }
}