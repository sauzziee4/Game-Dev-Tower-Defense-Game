using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/* public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    public float maxHealth = 50f;

    public float currentHealth;
    public float moveSpeed = 2f;
    public float damageToTower = 10f;

    [Header("Visual Components")]
    public Canvas healthBarCanvas;

    public UnityEngine.UI.Slider healthBar;
    public GameObject deathEffect;

    [Header("Audio")]
    public AudioSource audioSource;

    public AudioClip hitSound;
    public AudioClip deathSound;

    //private variables
    private List<Vector3> pathToTower;

    private int currentPathIndex = 0;
    private bool isDead = false;
    private bool reachedTower = false;
    private HexGridGenerator hexGrid;

    //event
    public System.Action<Enemy> onEnemyDied;

    public System.Action<Enemy, float> OnTowerDamage;

    private void Start()
    {
        currentHealth = maxHealth;

        //Setup health bar
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        //find hex grid and get path
        hexGrid = FindFirstObjectByType<HexGridGenerator>();
        if (hexGrid != null)
        {
            SetupPath();
        }

        //setup audio
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void SetupPath()
    {
        //find the nearest path tile to the current position
        Vector2Int startHex = WorldToHex(transform.position);

        //if we're not on a path, find the nearest path edge
        if (!hexGrid.IsPathTile(startHex))
        {
            startHex = FindNearestPathEdge();
        }

        //get path from our position to the castle
        pathToTower = hexGrid.GetPathTocastle(startHex);
        currentPathIndex = 0;

        //position enemy at the start of the path
        if (pathToTower.Count > 0)
        {
            transform.position = pathToTower[0] + Vector3.up * 0.5f; //slight offset
        }
    }

    private Vector2Int FindNearestPathEdge()
    {
        //get all path coordinates and find the one furthest from castle
        List<Vector2Int> pathCoords = hexGrid.GetAllPathCoordinates();
        Vector2Int castle = Vector2Int.zero;

        Vector2Int furthest = Vector2Int.zero;
        float maxDistance = 0f;

        foreach (Vector2Int pathCoord in pathCoords)
        {
            float distance = Vector2Int.Distance(pathCoord, castle);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                furthest = pathCoord;
            }
        }

        return furthest;
    }

    private void Update()
    {
        if (isDead || reachedTower) return;

        MoveAlongPath();

        //always face camera if health bar is present
        if (healthBarCanvas != null)
        {
            healthBarCanvas.transform.LookAt(Camera.main.transform);
        }
    }

    private void MoveAlongPath()
    {
        if (pathToTower == null || pathToTower.Count == 0) return;

        if (currentPathIndex >= pathToTower.Count)
        {
            ReachTower();
            return;
        }

        Vector3 targetPosition = pathToTower[currentPathIndex] + Vector3.up * 0.5f;
        Vector3 direction = (targetPosition - targetPosition).normalized;

        //move towards target
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        //rotate face movement direction
        if (direction.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        //check if we have reached the current waypoint
        if (Vector3.Distance(transform.position, targetPosition) < 0.2f)
        {
            currentPathIndex++;
        }
    }

    private void ReachTower()
    {
        if (reachedTower) return;

        reachedTower = true;

        //find and damage the tower
        TowerBehaviour tower = FindFirstObjectByType<TowerBehaviour>();
        if (tower != null)
        {
            tower.TakeDamage(damageToTower);
            OnTowerDamage?.Invoke(this, damageToTower);
        }

        //destroy this
        StartCoroutine(DestroyAfterDelay(0.5f));
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        //update health bar
        if (healthBar != null)
        {
            healthBar.value = currentHealth;
        }

        //play hit sound
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        //check if enemy died
        if (currentHealth <= 0)
        {
            onEnemyDied();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        //play death sound
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        //spawn death effect
        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, transform.rotation);
            Destroy(effect, 3f);
        }

        //notify
        OnEnemyDied?.Invoke(this);

        //destroy enemy
        StartCoroutine(DestroyAfterDelay(deathSound != null ? deathSound.length : 1f));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    //public utility methods
    public bool IsDead()
    {
        return isDead;
    }

    public bool HasReachedTower()
    {
        return reachedTower;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public Vector3 GetnextWaypoint()
    {
        if (pathToTower != null && currentPathIndex < pathToTower.Count)
        {
            return pathToTower[currentPathIndex];
        }

        return transform.position;
    }

    //helper method for hex conversion (matches HexGridGenerator)
    private Vector2Int WorldToHex(Vector3 worldPos)
    {
        float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.z);
        float r = (2f / 3f * worldPos.z);

        return HexRound(q, r);
    }

    private Vector2Int HexRound(float q, float r)
    {
        float s = -q - r;

        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float q_diff = Mathf.Abs(rq - q);
        float r_diff = Mathf.Abs(rr - r);
        float s_diff = Mathf.Abs(rs - s);

        if (q_diff > r_diff && q_diff > s_diff)
            rq = -rr - rs;
        else if (r_diff > s_diff)
            rr = -rq - rs;

        return new Vector2Int(rq, rr);
    }

    private void OnDrawGizmosSelected()
    {
        //draw path to tower
        if (pathToTower != null && pathToTower.Count > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < pathToTower.Count - 1; i++)
            {
                Gizmos.DrawLine(pathToTower[i], pathToTower[i + 1]);
            }

            //highlight current target
            if (currentPathIndex < pathToTower.Count)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(pathToTower[currentPathIndex], 0.3f);
            }
        }
    }
} */