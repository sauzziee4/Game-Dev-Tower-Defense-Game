using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TowerBehaviour : MonoBehaviour
{
    [Header("Tower Stats")]
    public float maxHealth = 100f;

    public float currentHealth;
    public float attackRange = 5f;
    public float attackDamage = 25f;
    public float attackRate = 1f; //attacks per second

    [Header("Visual Components")]
    public Transform turretPivot; //optional: for tower rotation

    public GameObject projectilePrefab;
    public Transform firePoint;
    public ParticleSystem muzzleFlash;

    [Header("Audio")]
    public AudioSource audioSource;

    public AudioClip shootSound;
    public AudioClip hitSound;
    public AudioClip destroyedSound;

    [Header("UI")]
    public Canvas healthBarCanvas;

    public UnityEngine.UI.Slider healthBar;

    //private variables
    private List<Enemy> enemiesInRange = new List<Enemy>();

    private Enemy currentTarget;
    private float nextAttacktime = 0f;
    private bool isDestroyed = false;

    //events
    public System.Action<TowerBehaviour> onTowerDestroyed;

    public System.Action<Enemy> OnEnemyKilled;

    private void Start()
    {
        //setup audio source if not assigned
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        //set up health bar
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        //start enemy detection
        StartCoroutine(DetectEnemies());
    }

    public void Instantialize(float health, float range, float damage, float rate)
    {
        maxHealth = health;
        currentHealth = health;
        attackRange = range;
        attackDamage = damage;
        attackRate = rate;

        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    private void Update()
    {
        if (isDestroyed) return;

        //attack current target if available and ready
        if (currentTarget != null && Time.time >= nextAttackTime)
        {
            AttackTarget();
            nextAttacktime = Time.time + (1f / attackRate);
        }

        //rotate turret towards target (optional)
        if (turretPivot != null && currentTarget != null)
        {
            Vector3 direction = currentTarget.transform.position - turretPivot.position;
            direction.y = 0; //keep roation on Y axis only
            if (direction.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                turretPivot.rotation = Quaternion.Slerp(turretPivot.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }

    private IEnumerator DetectEnemies()
    {
        while (!isDestroyed)
        {
            FindEnemiesInRange();
            SelectTarget();
            yield return new WaitForSeconds(0.1f); //check 10 times per second
        }
    }

    private void FindEnemiesInRange()
    {
        enemiesInRange.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var collider in colliders)
        {
            Enemy enemy = collider.GetComponent<Enemy>();
            if (enemy != null && !enemy.IsDead())
            {
                enemiesInRange.Add(enemy);
            }
        }
    }

    private void SelectTarget()
    {
        if (enemiesInRange.Count == 0)
        {
            currentTarget = null;
            return;
        }

        //target selection strategy: clostest to the tower (can be modified)
        Enemy closestEnemy = null;
        float closestDistance = float.MaxValue;

        foreach (var enemy in enemiesInRange)
        {
            if (enemy == null || enemy.IsDead()) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        currentTarget = closestEnemy;
    }

    private void AttackTarget()
    {
        if (currentTarget != null || currentTarget.IsDead()) return;

        //check if target is still in range
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distanceToTarget < attackRange)
        {
            currentTarget = null;
            return;
        }

        //fire projectile or deal direct damage
        if (projectilePrefab != null)
        {
            FireProjectile();
        }
        else
        {
            //direct damage
            currentTarget.TakeDamage(attackDamage);
        }

        //visual and audio effects
        PlayAttackEffects();
    }

    private void FireProjectile()
    {
    }
}