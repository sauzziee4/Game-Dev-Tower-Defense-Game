using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//This class handles the turrets that the player is able to place within the game on the hex grid. 
//it handles the combat, health, upgrades, visual feedback and the management of resources. 
public class PlaceableTurret : MonoBehaviour, IDefendable, IUpgradeable
{
    //implements IDefendable
    public float health { get; set; }
    public float maxHealth => _maxHealth;
    [SerializeField] private float _maxHealth = 100f;
    public Vector3 transform_position => transform.position;

    //declarations for turret stats 
    [Header("Turret Stats")]
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
    public GameObject healthBarPrefab;
    private GameObject healthBarInstance; 
    public float healthBarYOffset = 2f;
    private Renderer turretRenderer;
    private Material originalMaterial;
    private Color originalColor;

    [Header("Upgrade Settings")]
    public int upgradeLevel = 1;
    public float upgradeCostMultiplier = 1.5f;
    public float upgradeStatMultiplier = 1.3f;
    private float baseCost = 50f;

    private float nextFireTime = 0f;
    private Enemy currentTarget;
    private Vector2Int gridPosition;
    private bool showingRange = false;
    private bool isDestroyed = false;

    #region Unity
    //This will register the turret with the DefendableManager when it is enabled 
    //It will allow for the defensive system to track and protect the turret 
    private void OnEnable()
    {
        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance.AddDefendable(this);
        }
    }

    //this will unregister the turret
    private void OnDisable()
    {
        if (DefendableManager.Instance != null)
        {
            DefendableManager.Instance.RemoveDefendable(this);
        }
    }

    //On awake, the turrets healtha nd visual components will be initialized.
    
    private void Awake()
    {
        //health is initialized to its max
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

        // Health bar setup
        if (healthBarPrefab != null)
        {
            CreateHealthBar();
        }
    }

    //Set up the visual components and grid positioning after all the objects are initailized. 

    private void Start()
    {
        // Create range indicator if not assigned in the inspector. This will allow for the player to see the range in which their turrets shoot
        if (rangeIndicator == null)
        {
            CreateRangeIndicator();
        }

        // Hide range indicator by default which will then be shown on mouse hover 
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false);
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

    //The main loop for the turrets logic. It will target, rotate and fire
    //It is called every frame that the turret is active. 
    private void Update()
    {
        // dont't do anything if the turret is destroyed
        if (isDestroyed) return;
        UpdateHealthBar();

        // find and track target
        if (currentTarget == null || !IsValidTarget(currentTarget))
        {
            currentTarget = FindClosestEnemy();
        }

        // rotate the turrets head to face the target
        if (currentTarget != null && turretHead != null)
        {
            Vector3 direction = (currentTarget.transform.position - turretHead.position).normalized;
            direction.y = 0; // Keep rotation only on Y axis
            if (direction != Vector3.zero)
            {
                turretHead.rotation = Quaternion.LookRotation(direction);
            }
        }

        // fire at the target
        if (currentTarget != null && Time.time >= nextFireTime)
        {
            if (IsValidTarget(currentTarget))
            {
                Fire();
                nextFireTime = Time.time + 1f / fireRate;
            }
        }
    }
    #endregion

    #region Health and Damage Systems
    private void CreateHealthBar()
    {
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * healthBarYOffset;
        healthBarInstance.transform.localScale = Vector3.one;
    }

    private void UpdateHealthBar()
    {
        if (healthBarInstance == null) return;
        Transform fillBar = healthBarInstance.transform.GetChild(0);
        if (fillBar != null)
        {
            float healthPercent = health / maxHealth;
            fillBar.localScale = new Vector3(healthPercent, 1f, 1f);
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
    #endregion

    #region Targetting System
    //Method to find the closest enemy to the turret 
    private Enemy FindClosestEnemy()
    {
        //it will return a null if no enemies are found
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

    //this method will checker if an enemy is a valid target which in this case is whether the target exists and is in range 
    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null) return false;

        float distance = Vector3.Distance(transform.position, enemy.transform.position);
        return distance <= attackRange;
    }
    #endregion

    #region Combat System
    //fire a projectile at the current target 
    private void Fire()
    {
        //checks the valid components before firing 
        if (projectilePrefab == null || projectileSpawnPoint == null || currentTarget == null)
            return;

        // instantiates a projectile at the spawn point 
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

    #endregion

    #region Upgrade System
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

    public void UpgradeTower()
    {
        if (isDestroyed) return;

            // Upgrade stats
            upgradeLevel++;
            damage *= upgradeStatMultiplier;
            fireRate *= upgradeStatMultiplier;
            attackRange *= 1.1f; // Smaller range increase

            // Increase max health and fully heal on upgrade
            _maxHealth *= upgradeStatMultiplier;
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
    #endregion

    #region Range Visibilty System
    // Range visualization
    //This will allow the players to view the range in which their placeable turrets can shoot enenmies 
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

    //This will create a range indicator if there isnt one assigned within the inspector. 
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
    #endregion

    #region Mouse Interaction

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
    #endregion

    // Public properties
    public Vector2Int GridPosition => gridPosition;
    public float AttackRange => attackRange;
    public int UpgradeLevel => upgradeLevel;
    public bool IsDestroyed => isDestroyed;
    public float HealthPercent => health / maxHealth;
}