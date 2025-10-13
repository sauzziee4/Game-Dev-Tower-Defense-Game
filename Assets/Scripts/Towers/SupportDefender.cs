using UnityEngine;
using System.Collections.Generic;
using System.Linq;  

public class SupportDefender : MonoBehaviour, IDefendable
{
    public float health { get; set; }
    public Vector3 transform_position => transform.position;

    [Header("Support Defender Stats")]
    public float maxHealth = 120f;
    public float energyRegenRate = 5f; // Energy regenerated per second
    public float maxEnergy = 100f; // Maximum energy capacity
    private float currentEnergy;

    [Header("Healing System")]
    public float healRange = 6f;
    public float healingRate = 4f; // Health restored per second to nearby defenders
    public float healCost = 2f; // Energy cost per second to heal

    [Header("Buff Settings")]
    public float buffRange = 4f;
    public float damageBuffMultiplier = 1.3f; // 30% damage increase
    public float attackSpeedBuffMultiplier = 1.2f; // 20% attack speed increase
    public float buffEnergyCost = 1f; // Energy cost per second to maintain buff    

    [Header("Shield System")]
    public float shieldRange = 5f;
    public float shieldStrength = 25f; // Amount of damage the shield can absorb
    public float shieldDuration = 6f; // Duration the shield lasts
    public float shieldCoolDown = 20f; // Cooldown time before the shield can be used again
    public float shieldEnergyCost = 40f; // Energy cost to activate the shield
    public float lastShieldTime = 0f; // Last time the shield was activated

    [Header("Visual Effects")]
    public GameObject healingRangeIndicator;
    public GameObject buffRangeIndicator;
    public GameObject shieldRangeIndicator;
    public ParticleSystem healEffect;
    public ParticleSystem shieldEffect;
    public ParticleSystem buffEffect;
    public Material lowEnergyMaterial;


    private Vector2Int gridPosition;
    private bool isDestroyed = false;
    private List<IDefendable> buffedDefenders = new List<IDefendable>();
    private Dictionary<IDefendable, float> defenderShields = new Dictionary<IDefendable, float>();
    private Renderer supportRenderer;
    private Material originalMaterial;

    #region Unity Functions
    private void OnEnable()
    {

    }

    private void Awake()
    {

    }

    private void Start()
    {

    }

    private void Update()
    {

    }
    #endregion

    #region Health and Damage System

    private void TakeDamage(float damage)
    {
        if (isDestroyed) return;

        if (defenderShields.ContainsKey(this) && defenderShields[this] > 0)
        {
            float shieldedDamage = Mathf.Min(damage, defenderShields[this]);
            defenderShields[this] -= shieldedDamage;
            damage -= shieldedDamage;

            if (defenderShields[this] <= 0)
            {
                defenderShields.Remove(this);
                if (shieldEffect != null) shieldEffect.Stop();
            }
        }

        health -= damage;
        health = Mathf.Max(health, 0);

        Debug.Log($"Support Defender took {damage} damage. Current health: {health}");

        if(health <= 0)
        {
            DestroySupport();
        }
    }

    private void DestroySupport()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        ClearAllBuffs();

        var placementManager = FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            placementManager.RemoveTurret(gridPosition);
        }

        if (healingRangeIndicator != null) Destroy(healingRangeIndicator);
        if (buffRangeIndicator != null) Destroy(buffRangeIndicator);
        if (shieldRangeIndicator != null) Destroy(shieldRangeIndicator);

        Destroy(gameObject);
    }
    #endregion

    #region Energy System
    private void RegenerateEnergy()
    {
        if (currentEnergy < maxEnergy)
        {
            currentEnergy = Mathf.Min(currentEnergy + energyRegenRate * Time.deltaTime, maxEnergy);
        }
    }
    
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

    private void PreformSupportActions()
    {
        var nearbyDefenders = FindNearbyDefenders();
        HealNearbyDefenders(nearbyDefenders);
        BuffNearbyDefenders(nearbyDefenders);
    }

    private List<IDefendable> FindNearbyDefenders()
    {
        var defenders = new List<IDefendable>();

        var turrets = FindObjectsByType<PlaceableTurret>(FindObjectsSortMode.None);
        defenders.AddRange(turrets.Where(t => !t.IsDestroyed &&
            Vector3.Distance(t.transform.position, transform.position) <= healRange));

        var barriers = FindObjectsByType<BarrierDefender>(FindObjectsSortMode.None);
        defenders.AddRange(barriers.Where(b => !b.IsDestroyed() &&
            Vector3.Distance(transform.position, b.transform.position) <= healRange));

        var supports = FindObjectsByType<SupportDefender>(FindObjectsSortMode.None);
        defenders.AddRange(supports.Where(s => s != this && !s.IsDestroyed &&
            Vector3.Distance(s.transform.position, transform.position) <= healRange));

        return defenders;
    }

    private void HealNearbyDefenders(List<IDefendable> nearbyDefenders)
    {
        foreach(var defender in nearbyDefenders)
        {
            if(defender.health < GetMaxHealth(defender))
            {
                if (ConsumeEnergy(healCost * Time.deltaTime))
                {
                    float healAmount = healingRate * Time.deltaTime;
                    defender.health = Mathf.Min(defender.health + healAmount, GetMaxHealth(defender));

                    if(healEffect != null && !healEffect.isPlaying) healEffect.Play();
                } 
            }
        }
    }


    private void BuffNearbyDefenders(List<IDefendable> nearbyDefenders)
    {
        var defendersInBuffRange = nearbyDefenders.Where(d =>
            Vector3.Distance(transform.position, d.transform_position) <= buffRange).ToList();

        for (int i = buffedDefenders.Count - 1; i >= 0; i--)
        {
            if (!defendersInBuffRange.Contains(buffedDefenders[i]))
            {
                RemoveBuffFromDefender(buffedDefenders[i]);
                buffedDefenders.RemoveAt(i);
            }
        }
            
            foreach(var defender in defendersInBuffRange)
            {
                if (!buffedDefenders.Contains(defender))
                {
                    if (ConsumeEnergy(buffEnergyCost))
                    {
                        ApplyBuffToDefender(defender);
                        buffedDefenders.Add(defender);
                    }
                }
                else
                {
                    ConsumeEnergy(buffEnergyCost * Time.deltaTime);
                }
                
                
            }
    }

    private void ApplyBuffToDefender(IDefendable defender)
    {
        if (defender is PlaceableTurret turret)
        {
            turret.damage *= damageBuffMultiplier;
            turret.fireRate *= attackSpeedBuffMultiplier;
        }
        
        if(buffEffect != null)
        {
            var effect = Instantiate(buffEffect, defender.transform_position, Quaternion.identity);
            effect.transform.parent = ((MonoBehaviour)defender).transform;
            effect.Play();
        }
    }

    private void RemoveBuffFromDefender(IDefendable defender)
    {
        if(defender is PlaceableTurret turret)
        {
            turret.damage /= damageBuffMultiplier;
            turret.fireRate /= attackSpeedBuffMultiplier;
        }
    }

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

    private void HandleShieldAbility()
    {
        // Auto-cast shield when nearby defenders are low on health
        if (Time.time - lastShieldTime >= shieldCoolDown && currentEnergy >= shieldEnergyCost)
        {
            var defendersInDanger = FindNearbyDefenders()
                .Where(d => Vector3.Distance(transform.position, d.transform_position) <= shieldRange &&
                           d.health / GetMaxHealth(d) < 0.3f) // Less than 30% health
                .ToList();

            if (defendersInDanger.Count > 0)
            {
                CastShield();
            }
        }
    }

    private void CastShield()
    {
        if (!ConsumeEnergy(shieldEnergyCost)) return;

        var nearbyDefenders = FindNearbyDefenders()
            .Where(d => Vector3.Distance(transform.position, d.transform_position) <= shieldRange)
            .ToList();

        foreach (var defender in nearbyDefenders)
        {
            defenderShields[defender] = shieldStrength;

            if (shieldEffect != null)
            {
                var effect = Instantiate(shieldEffect, defender.transform_position, Quaternion.identity);
                effect.transform.parent = ((MonoBehaviour)defender).transform;
                effect.Play();
                Destroy(effect.gameObject, shieldDuration);
            }
        }

        lastShieldTime = Time.time;
            

    }

    #endregion

    #region Utility
    private float GetMaxHealth(IDefendable defender)
    {
        if (defender is PlaceableTurret turret) return turret.maxHealth;
        if (defender is BarrierDefender barrier) return barrier.maxHealth;
        if (defender is SupportDefender support) return support.maxHealth;
        return 100f;
    }
    #endregion
    
    #region Visual Systems
    private void CreateRangeIndicators()
    {
        // Healing range indicator
        healingRangeIndicator = CreateRangeIndicator("HealingRange", healRange, new Color(0f, 1f, 0f, 0.1f));
        
        // Buff range indicator  
        buffRangeIndicator = CreateRangeIndicator("BuffRange", buffRange, new Color(0f, 0f, 1f, 0.1f));
        
        // Shield range indicator
        shieldRangeIndicator = CreateRangeIndicator("ShieldRange", shieldRange, new Color(1f, 0f, 1f, 0.1f));
    }

    private GameObject CreateRangeIndicator(string name, float range, Color color)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        indicator.name = name;
        indicator.transform.parent = transform;
        indicator.transform.localPosition = Vector3.zero;
        
        float diameter = range * 2f;
        indicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        
        Destroy(indicator.GetComponent<Collider>());
        
        var renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Mode", 2);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            renderer.material = mat;
        }
        
        indicator.SetActive(false);
        return indicator;
    }

    private void UpdateVisualState()
    {
        if (supportRenderer == null) return;

        // Change material based on energy level
        float energyPercent = currentEnergy / maxEnergy;
        if (energyPercent < 0.3f && lowEnergyMaterial != null)
        {
            supportRenderer.material = lowEnergyMaterial;
        }
        else if (originalMaterial != null)
        {
            supportRenderer.material = originalMaterial;
        }

        // Scale based on health
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

    #region Mouse Interaction
    private void OnMouseEnter()
    {
        if (!isDestroyed)
            ShowRangeIndicators();
    }

    private void OnMouseExit()
    {
        HideRangeIndicators();
    }

    private void ShowRangeIndicators()
    {
        if (healingRangeIndicator != null) healingRangeIndicator.SetActive(true);
        if (buffRangeIndicator != null) buffRangeIndicator.SetActive(true);
        if (shieldRangeIndicator != null) shieldRangeIndicator.SetActive(true);
    }

    private void HideRangeIndicators()
    {
        if (healingRangeIndicator != null) healingRangeIndicator.SetActive(false);
        if (buffRangeIndicator != null) buffRangeIndicator.SetActive(false);
        if (shieldRangeIndicator != null) shieldRangeIndicator.SetActive(false);
    }

    void IDefendable.TakeDamage(float damage)
    {
        TakeDamage(damage);
    }
    #endregion

    public Vector2Int GridPosition => gridPosition;
    public float EnergyPercent => currentEnergy / maxEnergy;
    public bool IsDestroyed => isDestroyed;
    public float HealthPercent => health / maxHealth;
    public int BuffedDefendersCount => buffedDefenders.Count;

    public bool CanCastShield => Time.time - lastShieldTime >= shieldCoolDown && currentEnergy >= shieldEnergyCost;
}
