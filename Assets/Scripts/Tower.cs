using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//represents central tower, main object and main defense system
public class Tower : MonoBehaviour, IDefendable
{
    //IDefendable implementation
    public float health { get; set; } = 100f;

    public float maxHealth = 100f;

    public Vector3 transform_position
    { get { return transform.position; } }

    [Header("Defense System")]
    public float attackRange = 3f;

    public float fireRate = 0.0001f; //attacks per second
    private float nextFireTime = 0f;
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public float projectileSpeed = 0.0003f;
    public float projectileDamage = 10f;

    //adds and removes tower from defendableManager
    private void OnEnable()
    {
        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance.AddDefendable(this);
        }
    }

    private void OnDisable()
    {
        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance.RemoveDefendable(this);
        }
    }

    private void Update()
    {
        if (Time.time >= nextFireTime)
        {
            //find closest enemy to attack
            Enemy closestEnemy = FindClosestEnemy();
            if (closestEnemy != null && Vector3.Distance(transform.position, closestEnemy.transform.position) <= attackRange)
            {
                Shoot(closestEnemy);
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    //finds closest enemy within tower's attack range
    //returns closest enemy object or null if none are found
    private Enemy FindClosestEnemy()
    {
        if (Enemy.allEnemies.Count == 0)
        {
            return null;
        }

        Enemy closestEnemy = Enemy.allEnemies
            .Where(enemy => Vector3.Distance(transform.position, enemy.transform.position) <= attackRange)
            .OrderBy(enemy => Vector3.Distance(transform.position, enemy.transform.position))
            .FirstOrDefault();
        return closestEnemy;
    }

    //fires a projectile at target enemy
    private void Shoot(Enemy targetEnemy)
    {
        if (projectilePrefab == null || targetEnemy == null) return;

        //instantiates new projectile
        GameObject projectileGO = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Projectile projectile = projectileGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.SetTarget(targetEnemy, projectileDamage, projectileSpeed);
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        Debug.Log($"Tower health: {health}");

        if (health <= 0)
        {
            health = 0; //ensure does not go into negative

            // Trigger game over through GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }
            else
            {
                Debug.LogError("GameManager not found! Cannot trigger game over.");
            }
        }
    }
}