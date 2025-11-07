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
    public GameObject defenderSelectionPanel; // Panel containing defender type buttons
    public Transform defenderButtonContainer; // Container for defender type buttons
    public GameObject defenderButtonPrefab; // Prefab for defender selection buttons

    [Header("Tower Info")]
    public TextMeshProUGUI towerNameText;
    public TextMeshProUGUI towerDamageText;
    public TextMeshProUGUI towerRangeText;
    public TextMeshProUGUI towerFireRateText;
    public TextMeshProUGUI towerUpgradeLevelText;
    public TextMeshProUGUI towerStatsText;
    public UnityEngine.UI.Button upgradeTowerButton;
    public UnityEngine.UI.Button sellTowerButton;
    public TextMeshProUGUI upgradeCostText;

    private TurretPlacementManager placementManager;
    private IUpgradeable selectedUpgradeable;
    private MonoBehaviour selectedTower;
    private Button[] defenderButtons; // Array to track defender selection buttons
    private bool defenderSelectionVisible = false;

    private void Start()
    {
        placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();

        if (turretInfoPanel != null)
        {
            turretInfoPanel.gameObject.SetActive(false);
        }

        if(defenderSelectionPanel != null)
        {
            defenderSelectionPanel.SetActive(false);
        }
        // Setup button listeners
        if (upgradeTowerButton != null)
        {
            upgradeTowerButton.onClick.AddListener(UpgradeSelectedTower);
        }

        if (sellTowerButton != null)
        {
            sellTowerButton.onClick.AddListener(SellSelectedTower);
        }

        if(placeTurretButton != null)
        {   
            placeTurretButton.onClick.RemoveAllListeners();
            placeTurretButton.onClick.AddListener(OnPlaceTurretButtonClicked);
        }
        CreateDefenderButtons();

    }


    private void OnPlaceTurretButtonClicked()
    {
        if (placementManager.IsPlacingTurret)
        {
            placementManager.ToggleTurretPlacement();
            HideDefenderSelection();
        }
        else
        {
            ShowDefenderSelection();
        }
    }

    private void ShowDefenderSelection()
    {
        if (defenderSelectionPanel != null)
        {
            defenderSelectionPanel.SetActive(true);
            defenderSelectionVisible = true;
        }

        if (placementManager != null)
        {
            var buttonText = placeTurretButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = "Cancel";
            }
        }
    }
    
    private void HideDefenderSelection()
    {
        if (defenderSelectionPanel != null)
        {
            defenderSelectionPanel.SetActive(false);
            defenderSelectionVisible = false;
        }

        if (placementManager != null)
        {
            var buttonText = placeTurretButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = "Place Turret";
            }
        }
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

            
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{defenderTypes[i].name}";
                buttonText.fontSize = 14;
            }

            
            int index = i; 
            button.onClick.AddListener(() => OnDefenderSelected(index)); 

            defenderButtons[i] = button;
        }
    }

    private void OnDefenderSelected(int index)
    {
        placementManager.SelectDefenderType(index);
        placementManager.ToggleTurretPlacement();
        
    }

    private void Update()
    {
        UpdateInstructionText();
        UpdateResourceText();
        
        if(defenderSelectionVisible)
        {
            UpdateDefenderButtons();
        }

        // Handle right-click input for tower selection/deselection
        if (Input.GetMouseButtonDown(1) && !placementManager.IsPlacingTurret)
        {
            if (selectedTower != null)
            {
                DeselectTower();
            }
            else
            {
                SelectTowerAtMousePosition();
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
                    colors.normalColor = new Color(1f,1f,0.5f,1f);
                    colors.highlightedColor = new Color(1f,1f,0.3f,1f);
                }
                else
                {
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.9f,0.9f,0.9f,1f);
                }
                defenderButtons[i].colors = colors;

                // Disable button if not enough resources
                var defenderType = placementManager.DefenderTypes[i];
                bool canAfford = placementManager.PlayerResources >= defenderType.cost;
                defenderButtons[i].interactable = canAfford;

                Text buttonText = defenderButtons[i].GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.color = canAfford ? Color.black : Color.red;
                }
            }
        }
    }

    
    private void UpdateInstructionText()
    {
        if (instructionText == null) return;

        if(defenderSelectionVisible)
        {
            instructionText.text = "Choose a defender type\nor Esc to cancel";
            instructionText.color = Color.white;
        }
        else if (placementManager.IsPlacingTurret)
        {
            var selectedDefender = placementManager.DefenderTypes[placementManager.SelectedDefenderIndex];
            instructionText.text = $"Placing: {selectedDefender.name}\nCost: {selectedDefender.cost}\nLeft Click to place\nRight Click or Esc to cancel";
            instructionText.color = Color.yellow;
        }
        else if (selectedTower != null)
        {
            instructionText.text = "Tower selected\nRight click to deselect";
            instructionText.color = Color.cyan;
        }
        else
        {
            instructionText.text = "Click 'Place Turret'\nLeft Click to place a tower";
            instructionText.color = Color.white;
        }
    }

    
    private void UpdateResourceText()
    {
        if (resourceText != null && placementManager != null)
        {
            resourceText.text = $":{placementManager.PlayerResources:F0}";
        }
    }

    private void SelectTowerAtMousePosition()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Try to find any tower with IUpgradeable interface
            IUpgradeable upgradeable = hit.collider.GetComponent<IUpgradeable>();
            if (upgradeable != null)
            {
                MonoBehaviour tower = hit.collider.GetComponent<MonoBehaviour>();
                if (tower != null)
                {
                    SelectTower(tower, upgradeable);
                }
            }
        }
    }

    public void SelectTower(MonoBehaviour tower, IUpgradeable upgradeable)
    {
        if (selectedTower != null)
        {
            HideTowerRange(selectedTower);
        }

        selectedTower = tower;
        selectedUpgradeable = upgradeable;

        if (selectedTower != null && selectedUpgradeable != null)
        {
            ShowTowerRange(selectedTower);
            UpdateTowerInfoPanel();

            if (turretInfoPanel != null)
            {
                turretInfoPanel.gameObject.SetActive(true);
            }
        }
    }

    public void DeselectTower()
    {
        if (selectedTower != null)
        {
            HideTowerRange(selectedTower);
            selectedTower = null;
            selectedUpgradeable = null;
        }

        if (turretInfoPanel != null)
        {
            turretInfoPanel.gameObject.SetActive(false);
        }
    }

    private void ShowTowerRange(MonoBehaviour tower)
    {
        if (tower is PlaceableTurret turret)
        {
            turret.ShowRange();
        }
        // Add other tower types as needed
    }

    private void HideTowerRange(MonoBehaviour tower)
    {
        if (tower is PlaceableTurret turret)
        {
            turret.HideRange();
        }
        // Add other tower types as needed
    }

    public void UpdateTowerInfoPanel()
    {
        if (selectedUpgradeable == null || selectedTower == null) return;

        // Update tower name
        if (towerNameText != null)
        {
            towerNameText.text = selectedTower.GetType().Name;
        }

        // Update upgrade level
        if (towerUpgradeLevelText != null)
        {
            towerUpgradeLevelText.text = $"Level: {selectedUpgradeable.UpgradeLevel}";
        }

        // Update stats based on tower type
        if (selectedTower is PlaceableTurret turret)
        {
            if (towerDamageText != null)
                towerDamageText.text = $"Damage: {turret.damage:F1}";
            if (towerRangeText != null)
                towerRangeText.text = $"Range: {turret.attackRange:F1}";
            if (towerFireRateText != null)
                towerFireRateText.text = $"Fire Rate: {turret.fireRate:F1}/s";
            if (towerStatsText != null)
                towerStatsText.text = $"Projectile Speed: {turret.projectileSpeed:F1}";
        }
        else if (selectedTower is BarrierDefender barrier)
        {
            if (towerDamageText != null)
                towerDamageText.text = $"Damage/s: {barrier.damagePerSecond:F1}";
            if (towerRangeText != null)
                towerRangeText.text = $"Slow Radius: {barrier.slowRadius:F1}";
            if (towerFireRateText != null)
                towerFireRateText.text = $"Repair Rate: {barrier.repairRate:F1}";
            if (towerStatsText != null)
                towerStatsText.text = $"Health: {barrier.health:F0}/{barrier.maxHealth:F0}";
        }
        else if (selectedTower is SupportDefender support)
        {
            if (towerDamageText != null)
                towerDamageText.text = $"Heal Rate: {support.healingRate:F1}";
            if (towerRangeText != null)
                towerRangeText.text = $"Heal Range: {support.healRange:F1}";
            if (towerFireRateText != null)
                towerFireRateText.text = $"Shield Strength: {support.shieldStrength:F1}";
            if (towerStatsText != null)
                towerStatsText.text = $"Energy: {(support.maxEnergy > 0 ? support.maxEnergy : 0):F0}";
        }

        // Update upgrade cost
        if (upgradeCostText != null)
        {
            float upgradeCost = selectedUpgradeable.GetUpgradeCost();
            upgradeCostText.text = $"Upgrade: {upgradeCost:F0}";

            if (upgradeTowerButton != null)
            {
                upgradeTowerButton.interactable = placementManager.PlayerResources >= upgradeCost;
            }
        }
    }

    public void UpgradeSelectedTower()
    {
        if (selectedUpgradeable != null)
        {
            float upgradeCost = selectedUpgradeable.GetUpgradeCost();

            // Use the boolean DeductResources method from TurretPlacementManager
            if (placementManager.DeductResources(upgradeCost))
            {
                selectedUpgradeable.UpgradeTower();
                UpdateTowerInfoPanel();
                Debug.Log($"Tower upgraded for {upgradeCost} resources");
            }
            else
            {
                Debug.Log("Not enough resources to upgrade tower");
            }
        }
    }

    public void SellSelectedTower()
    {
        if (selectedTower == null || selectedUpgradeable == null) return;

        // Calculate sell value (75% of base cost)
        float sellValue = 0f;
        if (selectedTower is PlaceableTurret)
            sellValue = placementManager.turretCost * 0.75f;
        else if (selectedTower is BarrierDefender)
            sellValue = 60f * 0.75f; // Base cost for barrier
        else if (selectedTower is SupportDefender)
            sellValue = 70f * 0.75f; // Base cost for support

        // Add resources back to player
        placementManager.AddResources(sellValue);

        // Get the tower's grid position if it's a placeable turret
        if (selectedTower is PlaceableTurret turret)
        {
            Vector2Int turretPos = turret.GridPosition;
            placementManager.RemoveTurret(turretPos);
        }
        else
        {
            // For other tower types, just destroy them
            Object.Destroy(selectedTower.gameObject);
        }

        // Deselect the tower
        DeselectTower();

        Debug.Log($"Tower sold for {sellValue} resources");
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