using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlaceableTurret : MonoBehaviour, IDefendable
{
    public float health { get; set; }
    public Vector3 transform_position => transform.position;

    [Header("Turret Stats")]
    public float maxHealth = 100f;
    public float attackRange = 5f;
    public float fireRate = 2f; // attacks per second
    public float damage = 15f;
    public float projectileSpeed = 15f;

    [Header("Visual Settings")]
    public Transform turretHead;
    public Transform projectileSpawnPoint;
    public GameObject projectilePrefab;
    public GameObject rangeIndicator;

    [Header("Health Visual Feedback")]
    public GameObject healthBarPrefab; // Optional health bar prefab
    private GameObject healthBarInstance;
    private Renderer turretRenderer;
    private Material originalMaterial;
    private Color originalColor;

    [Header("Upgrade Settings")]
    public int upgradeLevel = 1;
    public float upgradeCostMultiplier = 1.5f;
    public float upgradeStatMultiplier = 1.3f;

    private float nextFireTime = 0f;
    private Enemy currentTarget;
    private Vector2Int gridPosition;
    private bool showingRange = false;
    private bool isDestroyed = false;

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

    private void Awake()
    {
        health = maxHealth;

        // Get renderer for visual feedback
        turretRenderer = GetComponent<Renderer>();
        if (turretRenderer == null)
        {
            turretRenderer = GetComponentInChildren<Renderer>();
        }

        if (turretRenderer != null)
        {
            originalMaterial = turretRenderer.material;
            originalColor = originalMaterial.color;
        }
    }

    private void Start()
    {
        // Create range indicator if not assigned
        if (rangeIndicator == null)
        {
            CreateRangeIndicator();
        }

        // Hide range indicator by default
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false);
        }

        // Create health bar if prefab is assigned
        if (healthBarPrefab != null)
        {
            CreateHealthBar();
        }

        // Store grid position for reference
        var placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            var hexGridGenerator = Object.FindFirstObjectByType<HexGridGenerator>();
            if (hexGridGenerator != null)
            {
                gridPosition = hexGridGenerator.WorldToHex(transform.position);
            }
        }
    }

    private void Update()
    {
        // Don't do anything if destroyed
        if (isDestroyed) return;

        // Update health bar if it exists
        UpdateHealthBar();

        // Find and track target
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindClosestEnemy();
        }

        // Rotate to face target
        if (currentTarget != null && turretHead != null)
        {
            Vector3 direction = (currentTarget.transform.position - turretHead.position).normalized;
            direction.y = 0; // Keep rotation only on Y axis
            if (direction != Vector3.zero)
            {
                turretHead.rotation = Quaternion.LookRotation(direction);
            }
        }

        // Fire at target
        if (currentTarget != null && Time.time >= nextFireTime)
        {
            if (IsValidTarget(currentTarget))
            {
                Fire();
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }

    // Allows turret to be damaged by enemies
    public void TakeDamage(float damageAmount)
    {
        if (isDestroyed) return;

        health -= damageAmount;
        health = Mathf.Max(health, 0); // Clamp health to 0

        Debug.Log($"Turret took {damageAmount} damage. Health: {health}/{maxHealth}");

        // Visual feedback for taking damage
        UpdateVisualFeedback();

        if (health <= 0)
        {
            DestroyTurret();
        }
    }

    private void DestroyTurret()
    {
        if (isDestroyed) return;

        isDestroyed = true;

        Debug.Log($"Turret at {gridPosition} destroyed!");

        // Hide range indicator
        HideRange();

        // Destroy health bar
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }

        // Remove from placement manager's tracking
        var placementManager = FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            placementManager.RemoveTurret(gridPosition);
        }
        else
        {
            // Fallback - this will also trigger OnDisable
            Destroy(gameObject);
        }
    }

    private void UpdateVisualFeedback()
    {
        if (turretRenderer == null) return;

        // Change color based on health percentage
        float healthPercent = health / maxHealth;
        Color damageColor = Color.Lerp(Color.red, originalColor, healthPercent);
        turretRenderer.material.color = damageColor;

        // Optional: Scale down slightly when heavily damaged
        if (healthPercent < 0.3f)
        {
            transform.localScale = Vector3.one * (0.9f + healthPercent * 0.1f);
        }
    }

    private void CreateHealthBar()
    {
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * 2f; // Position above turret
        healthBarInstance.transform.localScale = Vector3.one;
    }

    private void UpdateHealthBar()
    {
        if (healthBarInstance == null) return;

        // Simple health bar update - you'll need to implement this based on your health bar prefab
        // This is a basic example assuming the health bar has a child with a scale-based fill
        Transform fillBar = healthBarInstance.transform.GetChild(0);
        if (fillBar != null)
        {
            float healthPercent = health / maxHealth;
            fillBar.localScale = new Vector3(healthPercent, 1f, 1f);
        }
    }

    private Enemy FindClosestEnemy()
    {
        if (Enemy.allEnemies == null || Enemy.allEnemies.Count == 0)
        {
            return null;
        }

        Enemy closestEnemy = Enemy.allEnemies
            .Where(enemy => enemy != null && IsValidTarget(enemy))
            .OrderBy(enemy => Vector3.Distance(transform.position, enemy.transform.position))
            .FirstOrDefault();

        return closestEnemy;
    }

    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null) return false;

        float distance = Vector3.Distance(transform.position, enemy.transform.position);
        return distance <= attackRange;
    }

    private void Fire()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null || currentTarget == null)
            return;

        // Instantiate projectile
        GameObject projectileObj = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);

        // Set up projectile
        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.SetTarget(currentTarget, damage, projectileSpeed);
        }
        else
        {
            // If no Projectile component, add basic physics movement
            Rigidbody rb = projectileObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = (currentTarget.transform.position - projectileSpawnPoint.position).normalized;
                rb.linearVelocity = direction * projectileSpeed;

                // Destroy projectile after 5 seconds if it doesn't hit anything
                Destroy(projectileObj, 5f);
            }
        }
    }

    // Upgrade system
    public float GetUpgradeCost()
    {
        var placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            float baseCost = placementManager.turretCost;
            return baseCost * Mathf.Pow(upgradeCostMultiplier, upgradeLevel - 1);
        }
        return 100f; // Default cost
    }

    public void UpgradeTurret()
    {
        if (isDestroyed) return;

        var placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();
        float upgradeCost = GetUpgradeCost();

        if (placementManager != null && placementManager.DeductResources(upgradeCost))
        {
            // Upgrade stats
            upgradeLevel++;
            damage *= upgradeStatMultiplier;
            fireRate *= upgradeStatMultiplier;
            attackRange *= 1.1f; // Smaller range increase

            // Increase max health and fully heal on upgrade
            maxHealth *= upgradeStatMultiplier;
            health = maxHealth;

            // Reset visual appearance since we're at full health
            if (turretRenderer != null)
            {
                turretRenderer.material.color = originalColor;
                transform.localScale = Vector3.one * Mathf.Pow(1.05f, upgradeLevel - 1);
            }

            // Update range indicator if it exists
            if (rangeIndicator != null)
            {
                float diameter = attackRange * 2f;
                rangeIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            }

            Debug.Log($"Turret upgraded to level {upgradeLevel}!");
        }
        else
        {
            Debug.Log("Not enough resources to upgrade!");
        }
    }

    // Range visualization
    public void ShowRange()
    {
        if (rangeIndicator != null && !showingRange && !isDestroyed)
        {
            rangeIndicator.SetActive(true);
            showingRange = true;
        }
    }

    public void HideRange()
    {
        if (rangeIndicator != null && showingRange)
        {
            rangeIndicator.SetActive(false);
            showingRange = false;
        }
    }

    private void CreateRangeIndicator()
    {
        GameObject rangeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeObj.name = "RangeIndicator";
        rangeObj.transform.parent = transform;
        rangeObj.transform.localPosition = Vector3.zero;

        float diameter = attackRange * 2f;
        rangeObj.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        Destroy(rangeObj.GetComponent<Collider>());

        Renderer renderer = rangeObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material rangeMat = new Material(Shader.Find("Unlit/Transparent"));

            if (rangeMat.shader.name == "Hidden/InternalErrorShader")
            {
                rangeMat = new Material(Shader.Find("Standard"));

                rangeMat.SetFloat("_Mode", 2);
                rangeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                rangeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                rangeMat.SetInt("_ZWrite", 0);
                rangeMat.DisableKeyword("_ALPHATEST_ON");
                rangeMat.EnableKeyword("_ALPHABLEND_ON");
                rangeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                rangeMat.renderQueue = 3000;
            }

            rangeMat.color = new Color(0.3f, 0.6f, 1f, 0.1f);
            renderer.material = rangeMat;
        }

        rangeIndicator = rangeObj;
    }

    // Mouse interaction for showing range
    private void OnMouseEnter()
    {
        if (!isDestroyed)
        {
            ShowRange();
        }
    }

    private void OnMouseExit()
    {
        HideRange();
    }

    // Public properties
    public Vector2Int GridPosition => gridPosition;
    public float AttackRange => attackRange;
    public int UpgradeLevel => upgradeLevel;
    public bool IsDestroyed => isDestroyed;
    public float HealthPercent => health / maxHealth;
}