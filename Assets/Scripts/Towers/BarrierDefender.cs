using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BarrierDefender : MonoBehaviour, IDefendable
{
    public float health { get; set; }
    public Vector3 transform_position => transform.position;

    [Header("Barrier Defender Stat Settings")]
    public float maxHealth = 200f;
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
    private void OnEnable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.AddDefendable(this);
    }

    private void OnDisable()
    {
        if (DefendableManager.Instance != null)
            DefendableManager.Instance.RemoveDefendable(this);

        foreach (Enemy enemy in affectedEnemies)
        {
            if (enemy != null)
            {
                RemoveSlowEffect(enemy);
            }
        }
        affectedEnemies.Clear();
    }

    private void Awake()
    {
        health = maxHealth;
        barrierRenderer = GetComponent<Renderer>();
        if (barrierRenderer != null)
        {
            originalMaterial = barrierRenderer.material;
        }
    }

    private void Start()
    {
        CreateAreaIndicators();
        var hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        if (hexGridGenerator != null)
        {
            gridPosition = hexGridGenerator.WorldToHex(transform.position);
        }
    }

    private void Update()
    {
        if (isDestroyed) return;

        HandleSelfRepair();
        ApplyAreaEffects();
        UpdateVisualState();
    }
    #endregion

    #region Health and Damage System

    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        health -= amount;
        health = Mathf.Max(health, 0f);
        lastDamageTime = Time.time;

        Debug.Log($"Barrier Defender took {amount} damage. Current health: {health}/{maxHealth}");
        if (health <= 0f)
        {
            
            DestroyBarrier();
        }
    }

    private void HandleSelfRepair()
    {
        if (Time.time - lastDamageTime >= repairCooldown && health < maxRepairHealth && health > 0)
        {
            float repairAmount = repairRate * Time.deltaTime;
            health = Mathf.Min(health + repairAmount, maxRepairHealth);

            if (repairEffect != null && !repairEffect.isPlaying)
            {
                repairEffect.Play();
            }
        }
        else if (repairEffect != null && repairEffect.isPlaying)
        {
            repairEffect.Stop();
        }
    }

    private void DestroyBarrier()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        Debug.Log($"Barrier at grid {gridPosition} destroyed.");

        var placementManager = FindFirstObjectByType<TurretPlacementManager>();
        if (placementManager != null)
        {
            placementManager.RemoveTurret(gridPosition);
        }

        if (slowAreaIndicator != null) Destroy(slowAreaIndicator);
        if (damageAreaIndicator != null) Destroy(damageAreaIndicator);

        Destroy(gameObject);
    }

    #endregion

    #region  Area Effects System

    private void ApplyAreaEffects()
    {
        if (Enemy.allEnemies == null) return;
        var enemiesInSlowRange = Enemy.allEnemies.Where(enemy => enemy != null && Vector3.Distance(transform.position, enemy.transform.position) <= slowRadius).ToList();
        var enemiesInDamageRange = Enemy.allEnemies.Where(enemy => enemy != null && Vector3.Distance(transform.position, enemy.transform.position) <= damageRadius).ToList();

        foreach (Enemy enemy in enemiesInSlowRange)
        {
            if (!affectedEnemies.Contains(enemy))
            {
                ApplySlowEffect(enemy);
                affectedEnemies.Add(enemy);
            }
        }

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
        
        foreach (Enemy enemy in enemiesInDamageRange)
        {
            float damage = damagePerSecond * Time.deltaTime;
            enemy.TakeDamage(damage);
        }
    }

    private void ApplySlowEffect(Enemy enemy)
    {
        var navAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.speed *= slowEffect;
        }
    }

    private void RemoveSlowEffect(Enemy enemy)
    {
        var navAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.speed *= slowEffect;
        }
    }

    #endregion

    #region Visual System
    private void CreateAreaIndicators()
    {
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
            
            slowAreaIndicator.SetActive(false); 
        }

        
        if (damageAreaIndicator == null)
        {
            damageAreaIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            damageAreaIndicator.name = "DamageAreaIndicator";
            damageAreaIndicator.transform.parent = transform;
            damageAreaIndicator.transform.localPosition = Vector3.zero;
            
            float diameter = damageRadius * 2f;
            damageAreaIndicator.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            
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
            
            damageAreaIndicator.SetActive(false); 
        }
    }

    private void UpdateVisualState()
    {
        if (barrierRenderer == null) return;
        float healthPercent = health / maxHealth;

        if (healthPercent < 0.5f && damagedMaterial != null)
        {
            barrierRenderer.material = damagedMaterial;
        }
        else if (originalMaterial != null)
        {
            barrierRenderer.material = originalMaterial;
        }
        
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

    private void OnMouseEnter()
    {
        if (!isDestroyed)
            ShowAreaIndicators();
    }

    private void OnMouseExit()
    {
        
        HideAreaIndicators();

    }

    private void ShowAreaIndicators()
    {
        if (slowAreaIndicator != null)
            slowAreaIndicator.SetActive(true);
        if (damageAreaIndicator != null)
            damageAreaIndicator.SetActive(true);
    }

    private void HideAreaIndicators()
    {
        if (slowAreaIndicator != null)
            slowAreaIndicator.SetActive(false);
        if (damageAreaIndicator != null)
            damageAreaIndicator.SetActive(false);
    }

    #endregion

    public Vector2Int GetGridPosition() => gridPosition;
    public float SlowRadius() => slowRadius;
    public float DamageRadius() => damageRadius;
    public bool IsDestroyed() => isDestroyed;
    public float HealthPercent => health / maxHealth;
    public bool IsRepairing() => Time.time - lastDamageTime >= repairCooldown && health < maxRepairHealth && health > 0;

}