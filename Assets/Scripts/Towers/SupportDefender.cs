using UnityEngine;
using System.Collections.Generic;
using System.Linq;  

/// <summary>
/// A support tower that provides healing, buffs, and shield abilities to nearby defensive structures.
/// Uses an energy system to manage ability usage and prevent infinite support capabilities.
/// </summary>
public class SupportDefender : MonoBehaviour, IDefendable, IUpgradeable
{
    // IDefendable interface implementation
    public float health { get; set; }
    public float maxHealth { get; private set; } = 120f;
    public Vector3 transform_position => transform.position;

    [Header("Health Bar")]
    public GameObject healthBarPrefab; 
    private GameObject healthBarInstance; 
    public float healthBarYOffset = 2f; 

    [Header("Support Defender Stats")]
    public float energyRegenRate = 5f; 
    public float maxEnergy = 100f; 
    private float currentEnergy; 

    [Header("Healing System")]
    public float healRange = 6f; 
    public float healingRate = 4f; 
    public float healCost = 2f; 

    [Header("Buff Settings")]
    public float buffRange = 4f; 
    public float damageBuffMultiplier = 1.3f; // 30% damage increase for buffed defenders
    public float attackSpeedBuffMultiplier = 1.2f; // 20% attack speed increase for buffed defenders
    public float buffEnergyCost = 1f;   

    [Header("Shield System")]
    public float shieldRange = 5f; 
    public float shieldStrength = 25f; // Amount of damage the shield can absorb
    public float shieldDuration = 6f; 
    public float shieldCoolDown = 20f; 
    public float shieldEnergyCost = 40f;
    public float lastShieldTime = 0f; // Last time the shield was activated (for cooldown tracking)

    [Header("Upgrade Settings")]
    public int upgradeLevel = 1;
    public float upgradeCostMultiplier = 1.5f;
    public float upgradeStatsMultiplier = 1.3f;
    private float baseCost = 70f; 
    [SerializeField] private StarUpgradeVisual starUpgradeVisuals;

    [Header("Visual Effects")]
    public GameObject healingRangeIndicator; 
    public GameObject buffRangeIndicator; 
    public GameObject shieldRangeIndicator; 
    public ParticleSystem healEffect; // Particle effect played during healing
    public ParticleSystem shieldEffect; // Particle effect for shield activation
    public ParticleSystem buffEffect; // Particle effect for buff application
    public Material lowEnergyMaterial; 

    // Internal state variables
    private Vector2Int gridPosition; 
    private bool isDestroyed = false; 
    private List<IDefendable> buffedDefenders = new List<IDefendable>(); // List of currently buffed defenders
    private Dictionary<IDefendable, float> defenderShields = new Dictionary<IDefendable, float>(); // Active shields and their remaining strength
    private Renderer supportRenderer;
    private Material originalMaterial;
    private Color origionalColour;
    
    private float initialRegenEnergyRate;
    private float initialHealRange;
    private float initialHealingRate;
    private float initialBuffRange;
    private float initialShieldRange;
    private float initialShieldStrength;
    private float initialMaxHealth;


    #region Unity Functions
    // Called when the object becomes enabled and active. Registers this support tower with the DefendableManager.
    private void OnEnable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.AddDefendable(this);
    }

    // Called when the object becomes disabled. Unregisters from DefendableManager.
    private void OnDisable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.RemoveDefendable(this);
    }

    // Initialize support tower properties and get required components.
    private void Awake()
    {
        maxHealth = 120f;
        health = maxHealth;
        currentEnergy = maxEnergy; // Start with full energy
        supportRenderer = GetComponent<Renderer>();
        if(supportRenderer != null)
        {
            originalMaterial = supportRenderer.material;
        }
        CreateHealthBar();

        if (starUpgradeVisuals == null)
        {
            starUpgradeVisuals = GetComponentInChildren<StarUpgradeVisual>();
        }

        initialRegenEnergyRate = energyRegenRate;
        initialHealRange = healRange;
        initialHealingRate = healingRate;
        initialBuffRange = buffRange;
        initialShieldRange = shieldRange;
        initialShieldStrength = shieldStrength;
        initialMaxHealth = maxHealth;
    }


    // Setup range indicators and grid position after object instantiation.
    private void Start()
    {
        CreateRangeIndicators();
        
        // Get position on hex grid for placement management
        var hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if(hexGridGenerator != null)
        {
            gridPosition = hexGridGenerator.WorldToHex(transform.position);
        }
    }

    /// Main update loop - handles energy regeneration, support abilities, visual updates, and shield management.
    private void Update()
    {
        if (isDestroyed) return;

        RegenerateEnergy();
        PreformSupportActions(); // Healing and buffing nearby defenders
        UpdateVisualState();
        HandleShieldAbility(); // Auto-cast shields when defenders are in danger
    }
    #endregion

    #region Health and Damage System

    
    // Creates and positions the health bar above the support tower.
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

    // Applies damage to the support tower. Shields can absorb damage before affecting health.
    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;

        // Check if this support tower has an active shield
        if (defenderShields.ContainsKey(this) && defenderShields[this] > 0)
        {
            float shieldedDamage = Mathf.Min(damage, defenderShields[this]);
            defenderShields[this] -= shieldedDamage;
            damage -= shieldedDamage;

            // Remove shield if depleted
            if (defenderShields[this] <= 0)
            {
                defenderShields.Remove(this);
                if (shieldEffect != null) shieldEffect.Stop();
            }
        }

        // Apply remaining damage to health
        health -= damage;
        health = Mathf.Max(health, 0);

        Debug.Log($"Support Defender took {damage} damage. Current health: {health}");

        if(health <= 0)
        {
            DestroySupport();
        }
    }

    // Handles support tower destruction - removes all buffs, cleans up UI elements, and destroys the game object.
    private void DestroySupport()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        
        // Remove all buffs from affected defenders before destruction
        ClearAllBuffs();

        // Clean up health bar
        if (healthBarInstance != null) Destroy(healthBarInstance);

        // Remove from placement grid
        var placementManager = FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            placementManager.RemoveTurret(gridPosition);
        }

        // Clean up range indicators
        if (healingRangeIndicator != null) Destroy(healingRangeIndicator);
        if (buffRangeIndicator != null) Destroy(buffRangeIndicator);
        if (shieldRangeIndicator != null) Destroy(shieldRangeIndicator);

        Destroy(gameObject);
    }
    #endregion

    #region Energy System

    // Regenerates energy over time up to the maximum capacity.
    // Energy is required for all support abilities.
    private void RegenerateEnergy()
    {
        if (currentEnergy < maxEnergy)
        {
            currentEnergy = Mathf.Min(currentEnergy + energyRegenRate * Time.deltaTime, maxEnergy);
        }
    }
    

    // Attempts to consume the specified amount of energy for ability usage.
    private bool ConsumeEnergy(float amount)
    {
        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            return true;
        }
        return false;
    }

    #endregion

    #region Support Abilities


    // Main support actions controller - executes healing and buffing for nearby defenders.
    // Called every frame to maintain continuous support effects
    private void PreformSupportActions()
    {
        var nearbyDefenders = FindNearbyDefenders();
        HealNearbyDefenders(nearbyDefenders);
        BuffNearbyDefenders(nearbyDefenders);
    }


    // Finds all defendable objects within the maximum support range.
    // Includes turrets, barriers, and other support towers (excluding self).
    private List<IDefendable> FindNearbyDefenders()
    {
        var defenders = new List<IDefendable>();

        // Find all turrets within heal range
        var turrets = FindObjectsByType<PlaceableTurret>(FindObjectsSortMode.None);
        defenders.AddRange(turrets.Where(t => !t.IsDestroyed &&
            Vector3.Distance(t.transform.position, transform.position) <= healRange));

        // Find all barriers within heal range
        var barriers = FindObjectsByType<BarrierDefender>(FindObjectsSortMode.None);
        defenders.AddRange(barriers.Where(b => !b.IsDestroyed() &&
            Vector3.Distance(transform.position, b.transform.position) <= healRange));

        // Find other support towers within heal range (excluding self)
        var supports = FindObjectsByType<SupportDefender>(FindObjectsSortMode.None);
        defenders.AddRange(supports.Where(s => s != this && !s.IsDestroyed &&
            Vector3.Distance(s.transform.position, transform.position) <= healRange));

        return defenders;
    }

    // Heals nearby defenders that are below maximum health.
    // Consumes energy per second while healing is active.
    private void HealNearbyDefenders(List<IDefendable> nearbyDefenders)
    {
        foreach (var defender in nearbyDefenders)
        {
            // Only heal if defender is damaged
            if (defender.health < GetMaxHealth(defender))
            {
                // Consume energy for healing
                if (ConsumeEnergy(healCost * Time.deltaTime))
                {
                    float healAmount = healingRate * Time.deltaTime;
                    defender.health = Mathf.Min(defender.health + healAmount, GetMaxHealth(defender));
                    
                    // Play healing effect
                    if (healEffect != null && !healEffect.isPlaying)
                    {
                        Debug.LogWarning("Healing Effect!");
                        healEffect.Play();
                    }
                }
            }
        }
    }

    // Manages buff application and removal for defenders within buff range.
    // Buffs increase damage and attack speed but consume energy to maintain.
    private void BuffNearbyDefenders(List<IDefendable> nearbyDefenders)
    {
        // Get defenders within the smaller buff range
        var defendersInBuffRange = nearbyDefenders.Where(d =>
            Vector3.Distance(transform.position, d.transform_position) <= buffRange).ToList();

        // Remove buffs from defenders that left the buff range
        for (int i = buffedDefenders.Count - 1; i >= 0; i--)
        {
            if (!defendersInBuffRange.Contains(buffedDefenders[i]))
            {
                RemoveBuffFromDefender(buffedDefenders[i]);
                buffedDefenders.RemoveAt(i);
            }
        }
            
        // Apply buffs to new defenders entering buff range
        foreach(var defender in defendersInBuffRange)
        {
            if (!buffedDefenders.Contains(defender))
            {
                // Try to apply new buff 
                if (ConsumeEnergy(buffEnergyCost))
                {
                    ApplyBuffToDefender(defender);
                    buffedDefenders.Add(defender);
                }
            }
            else
            {
                // Maintain existing buff
                ConsumeEnergy(buffEnergyCost * Time.deltaTime);
            }
        }
    }


    // Applies damage and attack speed buffs to a defender.
    // Currently only affects PlaceableTurret objects.
    private void ApplyBuffToDefender(IDefendable defender)
    {
        if (defender is PlaceableTurret turret)
        {
            turret.damage *= damageBuffMultiplier; // 30% damage increase
            turret.fireRate *= attackSpeedBuffMultiplier; // 20% attack speed increase
        }
        
        // Spawn buff effect visual
        if(buffEffect != null)
        {
            var effect = Instantiate(buffEffect, defender.transform_position, Quaternion.identity);
            effect.transform.parent = ((MonoBehaviour)defender).transform;
            effect.Play();
        }
    }

    // Removes buffs from a defender by reversing the multiplier effects.
    private void RemoveBuffFromDefender(IDefendable defender)
    {
        if(defender is PlaceableTurret turret)
        {
            turret.damage /= damageBuffMultiplier; // Restore original damage
            turret.fireRate /= attackSpeedBuffMultiplier; // Restore original attack speed
        }
    }

    // Removes all buffs from all currently buffed defenders.
    // Called when the support tower is destroyed.
    private void ClearAllBuffs()
    {
        foreach (var defender in buffedDefenders)
        {
            if (defender != null)
            {
                RemoveBuffFromDefender(defender);
            }
        }

        buffedDefenders.Clear();
    }
    #endregion

    #region Shield System

    // Monitors nearby defenders and automatically casts shields when they are in critical condition.
    // Shields are cast when defenders drop below 30% health and cooldown/energy requirements are met.
    private void HandleShieldAbility()
    {
        // Check if shield is off cooldown and we have enough energy
        if (Time.time - lastShieldTime >= shieldCoolDown && currentEnergy >= shieldEnergyCost)
        {
            // Find defenders in danger (below 30% health) within shield range
            var defendersInDanger = FindNearbyDefenders()
                .Where(d => Vector3.Distance(transform.position, d.transform_position) <= shieldRange &&
                           d.health / GetMaxHealth(d) < 0.3f) // Less than 30% health = critical
                .ToList();

            // If any defenders are in critical condition, cast shield
            if (defendersInDanger.Count > 0)
            {
                CastShield();
            }
        }
    }

    // Casts protective shields on all nearby defenders within shield range.
    // Shields absorb damage and have visual effects that last for the shield duration.
    private void CastShield()
    {
        // Consume energy for shield casting
        if (!ConsumeEnergy(shieldEnergyCost)) return;

        // Find all defenders within shield range
        var nearbyDefenders = FindNearbyDefenders()
            .Where(d => Vector3.Distance(transform.position, d.transform_position) <= shieldRange)
            .ToList();

        // Apply shields to all nearby defenders
        foreach (var defender in nearbyDefenders)
        {
            // Add shield strength to the defender
            defenderShields[defender] = shieldStrength;

            // Spawn shield visual effect
            if (shieldEffect != null)
            {
                var effect = Instantiate(shieldEffect, defender.transform_position, Quaternion.identity);
                effect.transform.parent = ((MonoBehaviour)defender).transform;
                effect.Play();
                
                // Destroy the effect after shield duration
                Destroy(effect.gameObject, shieldDuration);
            }
        }

        // Update cooldown timer
        lastShieldTime = Time.time;
    }

    #endregion

    #region Utility
    // Gets the maximum health value for different types of defendable objects.
    // Used for healing calculations and shield triggers.
    private float GetMaxHealth(IDefendable defender)
    {
        if (defender is PlaceableTurret turret) return turret.maxHealth;
        if (defender is BarrierDefender barrier) return barrier.maxHealth;
        if (defender is SupportDefender support) return support.maxHealth;
        return 100f; // Default fallback value
    }
    #endregion
    
    #region Visual Systems
    /// Creates visual range indicators for all three support abilities.
    private void CreateRangeIndicators()
    {
        // Healing range indicator 
        healingRangeIndicator = CreateRangeIndicator("HealingRange", healRange, new Color(0f, 1f, 0f, 0.1f));
        
        // Buff range indicator 
        buffRangeIndicator = CreateRangeIndicator("BuffRange", buffRange, new Color(0f, 0f, 1f, 0.1f));
        
        // Shield range indicator 
        shieldRangeIndicator = CreateRangeIndicator("ShieldRange", shieldRange, new Color(1f, 0f, 1f, 0.1f));
    }

    // Creates a single range indicator with specified properties.
    // Helper method to reduce code duplication when creating multiple indicators.
    private GameObject CreateRangeIndicator(string name, float range, Color color)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = name;
        indicator.transform.parent = transform;
        indicator.transform.localPosition = Vector3.zero;
        
        float diameter = range * 2f;
        indicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        
        
        Destroy(indicator.GetComponent<Collider>());
        
        // Setup transparent material
        var renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Mode", 2); // Set to transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            renderer.material = mat;
        }
        
        indicator.SetActive(false); // Hidden by default
        return indicator;
    }

    // Updates the visual appearance of the support tower based on energy and health status.
    // Changes material when energy is low and scales the tower when health is critical.
    private void UpdateVisualState()
    {
        if (supportRenderer == null) return;

        // Change material based on energy level (low energy warning)
        float energyPercent = currentEnergy / maxEnergy;
        if (energyPercent < 0.3f && lowEnergyMaterial != null)
        {
            supportRenderer.material = lowEnergyMaterial;
        }
        else if (originalMaterial != null)
        {
            supportRenderer.material = originalMaterial;
        }

        // Scale based on health (visual damage indicator)
        float healthPercent = health / maxHealth;
        if (healthPercent < 0.3f)
        {
            transform.localScale = Vector3.one * (0.9f + healthPercent * 0.1f);
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }
    #endregion

#region Upgrade Systems
    public int UpgradeLevel => upgradeLevel;

    public float GetUpgradeCost()
    {
        var placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            return baseCost * Mathf.Pow(upgradeCostMultiplier, upgradeLevel - 1);
        }

        return 100f;
    }

    public void UpgradeTower()
    {
        if (isDestroyed) return;

        
            upgradeLevel++;
            energyRegenRate *= upgradeStatsMultiplier;
            maxEnergy *= upgradeStatsMultiplier;
            healRange *= upgradeStatsMultiplier;
            healingRate *= upgradeStatsMultiplier;
            buffRange *= 1.1f;
            shieldRange *= 1.1f;
            shieldStrength *= upgradeStatsMultiplier;

            maxHealth *= upgradeStatsMultiplier;
            health = maxHealth;

            if (supportRenderer != null)
            {
                supportRenderer.material.color = origionalColour;
                transform.localScale = Vector3.one * Mathf.Pow(1.05f, upgradeLevel - 1);
            }

            if (healingRangeIndicator != null)
            {
                float healingDiameter = healRange * 2f;
                healingRangeIndicator.transform.localScale = new Vector3(healingDiameter, 0.01f, healingDiameter);
            }

            if (buffRangeIndicator != null)
            {
                float buffDiamter = buffRange * 2f;
                buffRangeIndicator.transform.localScale = new Vector3(buffDiamter, 0.01f, buffDiamter);
            }

            if (shieldRangeIndicator != null)
            {
                float shieldDiameter = shieldRange * 2f;
                shieldRangeIndicator.transform.localScale = new Vector3(shieldDiameter, 0.01f, shieldDiameter);
            }
                
            if (starUpgradeVisuals != null)
            {
                starUpgradeVisuals.UpdateStarMaterial(upgradeLevel);
            }

        Debug.Log($"Support Tower upgraded to {upgradeLevel}");
        
    }
    
    #endregion

    #region Mouse Interaction
    // Called when mouse hovers over the support tower. Shows all range indicators.
    private void OnMouseEnter()
    {
        if (!isDestroyed)
            ShowRangeIndicators();
    }

    // Called when mouse leaves the support tower. Hides all range indicators.
    private void OnMouseExit()
    {
        HideRangeIndicators();
    }


    // Shows all three range indicators 
    private void ShowRangeIndicators()
    {
        if (healingRangeIndicator != null) healingRangeIndicator.SetActive(true);
        if (buffRangeIndicator != null) buffRangeIndicator.SetActive(true);
        if (shieldRangeIndicator != null) shieldRangeIndicator.SetActive(true);
    }

    
    // Hides all three range indicators.
    private void HideRangeIndicators()
    {
        if (healingRangeIndicator != null) healingRangeIndicator.SetActive(false);
        if (buffRangeIndicator != null) buffRangeIndicator.SetActive(false);
        if (shieldRangeIndicator != null) shieldRangeIndicator.SetActive(false);
    }
    #endregion

    // Public utility methods for other systems to query support tower status
    

    // Gets the grid position of this support tower.
    public Vector2Int GridPosition => gridPosition;
    
    
    // Gets the current energy as a percentage of maximum energy.
    public float EnergyPercent => currentEnergy / maxEnergy;
    
    
    // Checks if the support tower has been destroyed. Returns true of destoryed, false otherwise. 
    public bool IsDestroyed => isDestroyed;
    
    
    // Gets the current health as a percentage of max health
    public float HealthPercent => health / maxHealth;
    
    
    // Gets the number of defenders currently receiving buffs from this support tower and returns the count of these buffed defenders
    public int BuffedDefendersCount => buffedDefenders.Count;

    
    // Checks if the support tower can cast a shield (cooldown and energy requirements) and returns true or false depending. 
    public bool CanCastShield => Time.time - lastShieldTime >= shieldCoolDown && currentEnergy >= shieldEnergyCost;
}
