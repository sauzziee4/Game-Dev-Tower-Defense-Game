using UnityEngine;

public class StarUpgradeVisual : MonoBehaviour
{
    [Header("Star Materials")]
    [SerializeField] private Material level1Material;
    [SerializeField] private Material level2Material;
    [SerializeField] private Material level3Material;

    private Renderer starRenderer;
    private int currentUpgradeLevel = 0;

    private void Awake()
    {
        starRenderer = GetComponent<Renderer>();

    }

    public void UpdateStarMaterial(int upgradeLevel)
    {
        if (starRenderer == null) return;

        switch (upgradeLevel)
        {
            case 1:
                if (level1Material != null)
                {
                    starRenderer.material = level1Material;
                }
                break;

            case 2:
                if (level2Material != null)
                {
                    starRenderer.material = level2Material;
                }
                break;

            case 3:
                if (level3Material != null)
                {
                    starRenderer.material = level3Material;
                }
                break;

            default:
                Debug.LogWarning($"StarUpgradeVisual: unsupported upgrade level {upgradeLevel}");
                break;
        }
    }
    public int GetCurrentUpgradeLevel() => currentUpgradeLevel;
}
