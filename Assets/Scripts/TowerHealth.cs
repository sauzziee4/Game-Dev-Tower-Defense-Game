using UnityEngine;
using UnityEngine.UI;

/* public class TowerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI Components")]
    public Slider healthBar;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip destroyedSound;

    // Events (other scripts can listen to these)
    public System.Action OnTowerDestroyed;
    public System.Action<float> OnHealthChanged; // sends current health percentage

    void Start()
    {
        //set health to max at start
        currentHealth = maxHealth;
        UpdateHealthBar();

        //get audio source if not assigned
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void TakeDamage(float damage)
    {
        //dont take damage if already destroyed
        if (currentHealth <= 0) return;

        //reduce health
    }
}
*/