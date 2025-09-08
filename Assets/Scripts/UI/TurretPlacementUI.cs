using UnityEngine;
using UnityEngine.UI;

public class TurretPlacementUI : MonoBehaviour
{
    [Header("UI References")]
    public UnityEngine.UI.Button placeTurretButton;
    public Text resourceText;
    public Text instructionText;
    public GameObject turretInfoPanel;

    [Header("Turret Info")]
    public Text turretDamageText;
    public Text turretRangeText;
    public Text turretFireRateText;
    public UnityEngine.UI.Button upgradeTurretButton;
    public UnityEngine.UI.Button sellTurretButton;
    public Text upgradeCostText;

    private TurretPlacementManager placementManager;
    private PlaceableTurret selectedTurret;

    private void Start()
    {
        placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();

        if (turretInfoPanel != null)
        {
            turretInfoPanel.gameObject.SetActive(false);
        }

        // Setup button listeners
        if (upgradeTurretButton != null)
        {
            upgradeTurretButton.onClick.AddListener(UpgradeSelectedTurret);
        }

        if (sellTurretButton != null)
        {
            sellTurretButton.onClick.AddListener(SellSelectedTurret);
        }

        // Note: Don't add listener to placeTurretButton since TurretPlacementManager already handles it
    }

    private void Update()
    {
        UpdateInstructionText();
        UpdateResourceText();

        // Handle right-click input for turret selection/deselection
        if (Input.GetMouseButtonDown(1) && !placementManager.IsPlacingTurret)
        {
            if (selectedTurret != null)
            {
                DeselectTurret();
            }
            else
            {
                SelectTurretAtMousePosition();
            }
        }
    }

    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        if (placementManager.IsPlacingTurret)
        {
            instructionText.text = "Left Click to place turret\nRight click to cancel";
            instructionText.color = Color.yellow;
        }
        else if (selectedTurret != null)
        {
            instructionText.text = "Turret selected\nRight click to deselect";
            instructionText.color = Color.cyan;
        }
        else
        {
            instructionText.text = "Click 'Place Turret' to build\nRight click turrets to select";
            instructionText.color = Color.white;
        }
    }

    private void UpdateResourceText()
    {
        if (resourceText != null && placementManager != null)
        {
            resourceText.text = $"Resources: {placementManager.PlayerResources:F0}";
        }
    }

    private void SelectTurretAtMousePosition()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            PlaceableTurret turret = hit.collider.GetComponent<PlaceableTurret>();
            if (turret != null)
            {
                SelectTurret(turret);
            }
        }
    }

    public void SelectTurret(PlaceableTurret turret)
    {
        if (selectedTurret != null)
        {
            selectedTurret.HideRange();
        }

        selectedTurret = turret;

        if (selectedTurret != null)
        {
            selectedTurret.ShowRange();
            UpdateTurretInfoPanel();

            if (turretInfoPanel != null)
            {
                turretInfoPanel.gameObject.SetActive(true);
            }
        }
    }

    public void DeselectTurret()
    {
        if (selectedTurret != null)
        {
            selectedTurret.HideRange();
            selectedTurret = null;
        }

        if (turretInfoPanel != null)
        {
            turretInfoPanel.gameObject.SetActive(false);
        }
    }

    public void UpdateTurretInfoPanel()
    {
        if (selectedTurret == null) return;

        if (turretDamageText != null)
        {
            turretDamageText.text = $"Damage: {selectedTurret.damage:F1}";
        }

        if (turretRangeText != null)
        {
            turretRangeText.text = $"Range: {selectedTurret.attackRange:F1}";
        }

        if (turretFireRateText != null)
        {
            turretFireRateText.text = $"Fire Rate: {selectedTurret.fireRate:F1}/s";
        }

        if (upgradeCostText != null)
        {
            float upgradeCost = selectedTurret.GetUpgradeCost();
            upgradeCostText.text = $"Upgrade: {upgradeCost:F0}";

            if (upgradeTurretButton != null)
            {
                upgradeTurretButton.interactable = placementManager.PlayerResources >= upgradeCost;
            }
        }
    }

    public void UpgradeSelectedTurret()
    {
        if (selectedTurret != null)
        {
            float upgradeCost = selectedTurret.GetUpgradeCost();

            // Use the boolean DeductResources method from TurretPlacementManager
            if (placementManager.DeductResources(upgradeCost))
            {
                selectedTurret.UpgradeTurret();
                UpdateTurretInfoPanel();
                Debug.Log($"Turret upgraded for {upgradeCost} resources");
            }
            else
            {
                Debug.Log("Not enough resources to upgrade turret");
            }
        }
    }

    public void SellSelectedTurret()
    {
        if (selectedTurret != null)
        {
            float sellValue = placementManager.turretCost * 0.75f;

            // Add resources back to player
            placementManager.AddResources(sellValue);

            // Get the turret's grid position
            Vector2Int turretPos = selectedTurret.GridPosition;

            // Remove turret from the placement manager
            placementManager.RemoveTurret(turretPos);

            // Deselect the turret
            DeselectTurret();

            Debug.Log($"Turret sold for {sellValue} resources");
        }
    }

    // Public method to start turret placement (can be called from other scripts or UI)
    public void StartTurretPlacement()
    {
        if (placementManager != null)
        {
            placementManager.ToggleTurretPlacement();
        }
    }
}