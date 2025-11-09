using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//procedurally controls screen-space VFX based on game intensity
public class DynamicThreatVisualizer : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("The main Post-Processing Volume to manipulate.")]
    public Volume postProcessVolume;
    [Tooltip("The central tower that players must defend.")]
    public Tower centralTower;

    [Header("Threat Calculation")]
    [Tooltip("The maximum number of enemies expected, used to normalise the enemy threat level.")]
    public int maxExpectedEnemies = 50;
    [Tooltip("How quickly the VFX adapt to threat level changes.")]
    public float adaptionSpeed = 1.5f;

    [Header("Effect Configuration")]
    [Tooltip("The colour to tint the screen at maximum threat.")]
    public Color criticalThreatColor = new Color(0.8f, 0.1f, 0.1f, 1f);
    [Tooltip("The intensity of vignette effect at maximum threat.")]
    [Range(0f, 1f)]
    public float maxVignetteIntensity = 0.8f;
    [Tooltip("The saturation level at maximum threat. -100 us fully desaturated.")]
    [Range(-100f, 0f)]
    public float minSaturation = -40f;
    [Tooltip("The contrast level at maximum threat.")]
    [Range(0f, 100f)]
    public float maxContrast = 25f;

    //private refs to post-processing overrides
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;

    private float _currentThreatLevel = 0f;

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (postProcessVolume == null || centralTower == null)
        {
            //disable if refs are missing after initialization
            enabled = false;
            return;
        }

        // 1. caluclate current threat level
        float healthPercent = Mathf.Clamp01(centralTower.health / centralTower.maxHealth);
        float healthThreat = 1.0f - healthPercent;

        float enemyCount = Enemy.allEnemies != null ? Enemy.allEnemies.Count : 0;
        float enemyThreat = Mathf.Clamp01(enemyCount / maxExpectedEnemies);

        float targetThreatLevel = Mathf.Clamp01((healthThreat + enemyThreat) * 0.5f);

        // 2. smoothly interpolate to the target threat level
        _currentThreatLevel = Mathf.Lerp(_currentThreatLevel, targetThreatLevel, Time.deltaTime * adaptionSpeed);

       // Debug.LogWarning($"Health: {healthPercent:P0}, Enemies: {enemyCount}, Threat Level: {_currentThreatLevel:P0}");

        // 3. apply VFX based on current threat level
        ApplyVisuals();
    }

    //finds central tower and gets needed post-processing overrides from volume profile
    private void Initialize()
    {
        if (centralTower == null)
        {
            centralTower = FindFirstObjectByType<Tower>();
            if (centralTower == null)
            {
                Debug.LogError("DynamicThreatVisualizer: Central tower not found! Disabling script", this);
                enabled = false;
                return;
            }
        }

        if (postProcessVolume == null || postProcessVolume.profile == null)
        {
            Debug.LogError("DynamicThreatVisualizer: Post Process Volume or its Profile is not assigned! Disabling script.", this);
            enabled = false;
            return;
        }

        //safely get the effect overrides from the profile
        postProcessVolume.profile.TryGet(out _vignette);
        postProcessVolume.profile.TryGet(out _colorAdjustments);

        if (_vignette == null) Debug.LogWarning("Vignette override not found in the Post Processing Profile.", this);
        if (_colorAdjustments == null) Debug.LogWarning("Color Adjustments override not found in the Post Processing Profile.", this);
    }
    //updates parameters of post-processing effects based on current threat level
    private void ApplyVisuals()
    {
        //vignette
        if (_vignette != null)
        {
            _vignette.intensity.value = Mathf.Lerp(0f, maxVignetteIntensity, _currentThreatLevel);
        }

        //colour adjustments
        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = Mathf.Lerp(0f, minSaturation, _currentThreatLevel);
            _colorAdjustments.contrast.value = Mathf.Lerp(0f, maxContrast, _currentThreatLevel);

            _colorAdjustments.colorFilter.value = Color.Lerp(Color.white, criticalThreatColor, _currentThreatLevel);
        }
    }
}
