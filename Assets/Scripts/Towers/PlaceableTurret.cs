using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlaceableTurret : MonoBehaviour
{
    [Header("Turret Stats")]
    public float attackRange = 5f;
    public float fireRate = 2f; // attacks per second
    public float damage = 15f;
    public float projectileSpeed = 15f;

    [Header("Visual Settings")]
    public Transform turretHead; // The part that rotates to face enemies
    public Transform projectileSpawnPoint;
    public GameObject projectilePrefab;
    public GameObject rangeIndicator; // Optional visual range indicator

    [Header("Upgrade Settings")]
    public int upgradeLevel = 1;
    public float upgradeCostMultiplier = 1.5f;
    public float upgradeStatMultiplier = 1.3f;

    private float nextFireTime = 0f;
    private Enemy currentTarget;
    private Vector2Int gridPosition;

    // Range visualization
    private bool showingRange = false;

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
        var placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();
        float upgradeCost = GetUpgradeCost();

        if (placementManager != null && placementManager.DeductResources(upgradeCost))
        {
            // Upgrade stats
            upgradeLevel++;
            damage *= upgradeStatMultiplier;
            fireRate *= upgradeStatMultiplier;
            attackRange *= 1.1f; // Smaller range increase

            // Update visual scale to show upgrade
            transform.localScale *= 1.05f;

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
        if (rangeIndicator != null && !showingRange)
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
        // Create a simple circle to show attack range
        GameObject rangeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeObj.name = "RangeIndicator";
        rangeObj.transform.parent = transform;
        rangeObj.transform.localPosition = Vector3.zero;

        // Scale to match attack range
        float diameter = attackRange * 2f;
        rangeObj.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        // Remove collider and set up material
        Destroy(rangeObj.GetComponent<Collider>());

        Renderer renderer = rangeObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Create a transparent material for the range indicator
            Material rangeMat = new Material(Shader.Find("Standard"));
            rangeMat.color = new Color(0, 1, 0, 0.3f); // Transparent green
            rangeMat.SetFloat("_Mode", 2); // Set to Fade mode
            rangeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rangeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rangeMat.SetInt("_ZWrite", 0);
            rangeMat.DisableKeyword("_ALPHATEST_ON");
            rangeMat.EnableKeyword("_ALPHABLEND_ON");
            rangeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rangeMat.renderQueue = 3000;

            renderer.material = rangeMat;
        }

        rangeIndicator = rangeObj;
    }

    // Mouse interaction for showing range
    private void OnMouseEnter()
    {
        ShowRange();
    }

    private void OnMouseExit()
    {
        HideRange();
    }

    // Public properties
    public Vector2Int GridPosition => gridPosition;
    public float AttackRange => attackRange;
    public int UpgradeLevel => upgradeLevel;
}