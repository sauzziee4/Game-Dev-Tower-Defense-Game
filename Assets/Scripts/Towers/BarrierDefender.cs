using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A defensive barrier structure that provides area denial through slowing and damaging enemies.
/// Features self-repair capabilities and visual feedback systems.
/// </summary>
public class BarrierDefender : MonoBehaviour, IDefendable
{
    public float health { get; set; } 
    public float maxHealth { get; private set; } = 200f;
    public Vector3 transform_position => transform.position;

    [Header("Health Bar")]
    public GameObject healthBarPrefab;
    private GameObject healthBarInstance;
    public float healthBarYOffset = 2f;

    [Header("Barrier Defender Stat Settings")]
    public float repairRate = 2f;
    public float maxRepairHealth = 150f;

    [Header("Area Denial")]
    public float slowRadius = 3f;
    public float slowEffect = 0.5f;
    public float damageRadius = 1.5f;
    public float damagePerSecond = 10f;

    [Header("Visual Effects")]
    public GameObject slowAreaIndicator;
    public GameObject damageAreaIndicator;
    public ParticleSystem repairEffect;
    public Material damagedMaterial;

    private Vector2Int gridPosition;
    private bool isDestroyed = false;
    private float lastDamageTime = 0f;
    private float repairCooldown = 5f;
    private List<Enemy> affectedEnemies = new List<Enemy>();
    private Renderer barrierRenderer;
    private Material originalMaterial;

    #region Unity Functions

    // Called when the object becomes enabled and active. Registers this barrier with the DefendableManager.
    
    private void OnEnable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.AddDefendable(this);
    }

    
    // Called when the object becomes disabled. Unregisters from DefendableManager and cleans up enemy effects.
    
    private void OnDisable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.RemoveDefendable(this);

        // Remove slow effects from all affected enemies before disabling
        foreach (Enemy enemy in affectedEnemies)
        {
            if (enemy != null)
            {
                RemoveSlowEffect(enemy);
            }
        }
        affectedEnemies.Clear();
    }

    
    // Initialize barrier properties and get required components.
    
    private void Awake()
    {
        maxHealth = 200f;
        health = maxHealth;
        barrierRenderer = GetComponent<Renderer>();
        if (barrierRenderer != null)
        {
            originalMaterial = barrierRenderer.material;
        }
    }

    
    // Setup area indicators, grid position, and health bar after object instantiation.
        private void Start()
    {
        CreateAreaIndicators();
        
        // Get position on hex grid for placement management
        var hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (hexGridGenerator != null)
        {
            gridPosition = hexGridGenerator.WorldToHex(transform.position);
        }

        CreateHealthBar();
    }

    
    // Main update loop - handles health bar, self-repair, area effects, and visual updates.
    
    private void Update()
    {
        if (isDestroyed) return;

        UpdateHealthBar();
        HandleSelfRepair();
        ApplyAreaEffects();
        UpdateVisualState();
    }
    #endregion

    #region Health and Damage System

    
    // Creates and positions the health bar above the barrier.
    
    private void CreateHealthBar()
    {
        if (healthBarPrefab != null)
        {
            healthBarInstance = Instantiate(healthBarPrefab, transform);
            healthBarInstance.transform.localPosition = Vector3.up * healthBarYOffset;
            healthBarInstance.transform.localScale = Vector3.one;
        }
    }

    
    // Updates the health bar fill amount based on current health percentage.
    
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

   
    // Applies damage to the barrier and triggers destruction if health reaches zero.
    
    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        health -= amount;
        health = Mathf.Max(health, 0f);
        lastDamageTime = Time.time; // Reset repair cooldown

        Debug.Log($"Barrier Defender took {amount} damage. Current health: {health}/{maxHealth}");
        if (health <= 0f)
        {
            DestroyBarrier();
        }
    }

    
    // Handles automatic self-repair when the barrier hasn't taken damage recently.
    
    private void HandleSelfRepair()
    {
        // Only repair if cooldown has passed, health is below max repair threshold, and barrier isn't destroyed
        if (Time.time - lastDamageTime >= repairCooldown && health < maxRepairHealth && health > 0)
        {
            float repairAmount = repairRate * Time.deltaTime;
            health = Mathf.Min(health + repairAmount, maxRepairHealth);

            // Play repair effect if available
            if (repairEffect != null && !repairEffect.isPlaying)
            {
                repairEffect.Play();
            }
        }
        else if (repairEffect != null && repairEffect.isPlaying)
        {
            // Stop repair effect when not repairing
            repairEffect.Stop();
        }
    }

    
    // Handles barrier destruction by removing from grid, cleans up UI elements, and destroys the game object.
    
    private void DestroyBarrier()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Debug.Log($"Barrier at grid {gridPosition} destroyed.");

        // Clean up health bar
        if (healthBarInstance != null) Destroy(healthBarInstance);

        // Remove from placement grid
        var placementManager = FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            placementManager.RemoveTurret(gridPosition);
        }

        // Clean up area indicators
        if (slowAreaIndicator != null) Destroy(slowAreaIndicator);
        if (damageAreaIndicator != null) Destroy(damageAreaIndicator);

        Destroy(gameObject);
    }

    #endregion

    #region Area Effects System

    
    // Applies area effects to enemies - slowing in outer radius and damage in inner radius.
    
    private void ApplyAreaEffects()
    {
        if (Enemy.allEnemies == null) return;
        
        // Find enemies in each effect radius
        var enemiesInSlowRange = Enemy.allEnemies.Where(enemy => enemy != null && Vector3.Distance(transform.position, enemy.transform.position) <= slowRadius).ToList();
        var enemiesInDamageRange = Enemy.allEnemies.Where(enemy => enemy != null && Vector3.Distance(transform.position, enemy.transform.position) <= damageRadius).ToList();

        // Apply slow effect to new enemies entering slow range
        foreach (Enemy enemy in enemiesInSlowRange)
        {
            if (!affectedEnemies.Contains(enemy))
            {
                ApplySlowEffect(enemy);
                affectedEnemies.Add(enemy);
            }
        }

        // Remove slow effect from enemies that left the slow range
        for (int i = affectedEnemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = affectedEnemies[i];
            if (enemy == null || !enemiesInSlowRange.Contains(enemy))
            {
                if (enemy != null)
                {
                    RemoveSlowEffect(enemy);
                }
                affectedEnemies.RemoveAt(i);
            }
        }
        
        // Apply continuous damage to enemies in damage range
        foreach (Enemy enemy in enemiesInDamageRange)
        {
            float damage = damagePerSecond * Time.deltaTime;
            enemy.TakeDamage(damage);
        }
    }

    
    // Applies slow effect to an enemy by reducing their movement speed.

    private void ApplySlowEffect(Enemy enemy)
    {
        var navAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.speed *= slowEffect; // Reduce speed by multiplying with slow factor
        }
    }


    // Removes slow effect from an enemy by restoring their original movement speed.
    private void RemoveSlowEffect(Enemy enemy)
    {
        var navAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            
            navAgent.speed /= slowEffect; 
        }
    }

    #endregion

    #region Visual System
    // Creates visual area indicators for slow and damage ranges.
    // These are semi-transparent cylinders that show the effective ranges.
    private void CreateAreaIndicators()
    {
        // Create slow area indicator 
        if (slowAreaIndicator == null)
        {
            slowAreaIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            slowAreaIndicator.name = "SlowAreaIndicator";
            slowAreaIndicator.transform.parent = transform;
            slowAreaIndicator.transform.localPosition = Vector3.zero;
            
            float diameter = slowRadius * 2f;
            slowAreaIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            
            Destroy(slowAreaIndicator.GetComponent<Collider>());
            
            var renderer = slowAreaIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material slowMat = new Material(Shader.Find("Standard"));
                slowMat.color = new Color(1f, 1f, 0f, 0.1f); 
                slowMat.SetFloat("_Mode", 2); 
                slowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                slowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                slowMat.SetInt("_ZWrite", 0);
                slowMat.EnableKeyword("_ALPHABLEND_ON");
                slowMat.renderQueue = 3000;
                renderer.material = slowMat;
            }
            
            slowAreaIndicator.SetActive(false); // Hidden by default
        }

        // Create damage area indicator 
        if (damageAreaIndicator == null)
        {
            damageAreaIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            damageAreaIndicator.name = "DamageAreaIndicator";
            damageAreaIndicator.transform.parent = transform;
            damageAreaIndicator.transform.localPosition = Vector3.zero;
            
            float diameter = damageRadius * 2f;
            damageAreaIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            
            // Remove collider to prevent interference with gameplay
            Destroy(damageAreaIndicator.GetComponent<Collider>());
            
            var renderer = damageAreaIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material damageMat = new Material(Shader.Find("Standard"));
                damageMat.color = new Color(1f, 0f, 0f, 0.2f); 
                damageMat.SetFloat("_Mode", 2);
                damageMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                damageMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                damageMat.SetInt("_ZWrite", 0);
                damageMat.EnableKeyword("_ALPHABLEND_ON");
                damageMat.renderQueue = 3000;
                renderer.material = damageMat;
            }
            
            damageAreaIndicator.SetActive(false); // Hidden by default
        }
    }

    
    // Updates the visual appearance of the barrier based on health status.
    // Changes material and scale to indicate damage level.
    
    private void UpdateVisualState()
    {
        if (barrierRenderer == null) return;
        float healthPercent = health / maxHealth;

        // Switch to damaged material when health is below 50%
        if (healthPercent < 0.5f && damagedMaterial != null)
        {
            barrierRenderer.material = damagedMaterial;
        }
        else if (originalMaterial != null)
        {
            barrierRenderer.material = originalMaterial;
        }
        
        // Shrink the barrier when health is critically low (below 30%)
        if(healthPercent <= 0.3f)
        {
            transform.localScale = Vector3.one * (0.9f + healthPercent * 0.1f);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    #endregion

    #region Mouse Interaction

    // Called when mouse hovers over the barrier. Shows area effect indicators.
    private void OnMouseEnter()
    {
        if (!isDestroyed)
            ShowAreaIndicators();
    }

    // Called when mouse leaves the barrier. Hides area effect indicators.
    private void OnMouseExit()
    {
        HideAreaIndicators();
    }

    // Shows the visual area indicators for slow and damage ranges.
    private void ShowAreaIndicators()
    {
        if (slowAreaIndicator != null)
            slowAreaIndicator.SetActive(true);
        if (damageAreaIndicator != null)
            damageAreaIndicator.SetActive(true);
    }

    // Hides the visual area indicators for slow and damage ranges.
    private void HideAreaIndicators()
    {
        if (slowAreaIndicator != null)
            slowAreaIndicator.SetActive(false);
        if (damageAreaIndicator != null)
            damageAreaIndicator.SetActive(false);
    }

    #endregion

    // Public utility methods for other systems to query barrier status
    
    // Gets the grid position of this barrier.
    public Vector2Int GetGridPosition() => gridPosition;
    
    // Gets the slow effect radius.
    public float SlowRadius() => slowRadius;

    // Gets the damage effect radius.
    public float DamageRadius() => damageRadius;
    
    // Checks if the barrier has been destroyed.
    public bool IsDestroyed() => isDestroyed;

    // Gets the current health as a percentage of max health.
    public float HealthPercent => health / maxHealth;
    
    /// Checks if the barrier is currently in self-repair mode.
    public bool IsRepairing() => Time.time - lastDamageTime >= repairCooldown && health < maxRepairHealth && health > 0;

}