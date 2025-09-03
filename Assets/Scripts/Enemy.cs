using UnityEngine;
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

    //place holder for pathfinding method
    public void FollowPath()
    {
        //Implement actual pathfinding logic using hex grid here

        //currently moves towards dummy target using:
        if (target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * speed * Time.deltaTime;
        }
    }

    private void Update()
    {
        //placeholder for finding target
        if (target == null)
        {
            FindTarget();
        }

        //placeholder for attacking target
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