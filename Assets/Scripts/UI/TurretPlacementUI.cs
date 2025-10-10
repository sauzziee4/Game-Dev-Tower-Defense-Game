using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurretPlacementUI : MonoBehaviour
{
    [Header("UI References")]
    public UnityEngine.UI.Button placeTurretButton;
    public TextMeshProUGUI resourceText;
    public TextMeshProUGUI instructionText;
    public GameObject turretInfoPanel;

    [Header("Defender Selection")]
    public Transform defenderButtonContainer; // Container for defender type buttons
    public GameObject defenderButtonPrefab; // Prefab for defender selection buttons

    [Header("Turret Info")]
    public TextMeshProUGUI turretDamageText;
    public TextMeshProUGUI turretRangeText;
    public TextMeshProUGUI turretFireRateText;
    public UnityEngine.UI.Button upgradeTurretButton;
    public UnityEngine.UI.Button sellTurretButton;
    public Text upgradeCostText;

    private TurretPlacementManager placementManager;
    private PlaceableTurret selectedTurret;
    private Button[] defenderButtons; // Array to track defender selection buttons

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

        CreateDefenderButtons();

    }

    
    private void CreateDefenderButtons()
    {
        if (placementManager == null || defenderButtonContainer == null || defenderButtonPrefab == null)
            return;

        var defenderTypes = placementManager.DefenderTypes;
        defenderButtons = new Button[defenderTypes.Length];

        for (int i = 0; i < defenderTypes.Length; i++)
        {
            GameObject buttonObj = Instantiate(defenderButtonPrefab, defenderButtonContainer);
            Button button = buttonObj.GetComponent<Button>();
            
            // Set button text
            Text buttonText = buttonObj.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = $"{defenderTypes[i].name}\n${defenderTypes[i].cost}";
            }

            // Add click listener
            int index = i; // Capture for closure
            button.onClick.AddListener(() => placementManager.SelectDefenderType(index));
            
            defenderButtons[i] = button;
        }
    }

    private void Update()
    {
        UpdateInstructionText();
        UpdateResourceText();
        UpdateDefenderButtons(); 

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

    private void UpdateDefenderButtons()
    {
        if (placementManager == null || defenderButtons == null) return;

        for (int i = 0; i < defenderButtons.Length; i++)
        {
            if (defenderButtons[i] != null)
            {
                // Highlight selected button
                ColorBlock colors = defenderButtons[i].colors;
                if (i == placementManager.SelectedDefenderIndex)
                {
                    colors.normalColor = Color.yellow;
                }
                else
                {
                    colors.normalColor = Color.white;
                }
                defenderButtons[i].colors = colors;

                // Disable button if not enough resources
                var defenderType = placementManager.DefenderTypes[i];
                defenderButtons[i].interactable = placementManager.PlayerResources >= defenderType.cost;
            }
        }
    }

    
    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        if (placementManager.IsPlacingTurret)
        {
            var selectedDefender = placementManager.DefenderTypes[placementManager.SelectedDefenderIndex];
            instructionText.text = $"Placing: {selectedDefender.name}\nLeft Click to place\nRight click to cancel\nPress 1-9 to switch types";
            instructionText.color = Color.yellow;
        }
        else if (selectedTurret != null)
        {
            instructionText.text = "Turret selected\nRight click to deselect";
            instructionText.color = Color.cyan;
        }
        else
        {
            instructionText.text = "Select defender type, then click 'Place Turret'\nRight click turrets to select\nPress 1-9 to switch types";
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