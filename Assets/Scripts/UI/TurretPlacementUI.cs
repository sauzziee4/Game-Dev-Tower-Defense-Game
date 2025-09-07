using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

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
    public Text upgradeCostText;

    private TurretPlacementManager placementManager;
    private PlaceableTurret selectedTurret;

    private void Start()
    {
        placementManager = Object.FindFirstObjectByType<TurretPlacementManager>();

        UpdateInstructionText();

        if(turretInfoPanel != null )
        {
            turretInfoPanel.gameObject.SetActive( false );
        }

        if(upgradeTurretButton != null )
        {
            upgradeTurretButton.onClick.AddListener(UpgradeSelectedTurret);
        }
    }
    private void Update()
    {
        UpdateInstructionText();

        if(Input.GetMouseButtonDown(1) && !placementManager.IsPlacingTurret)
        {
            if(selectedTurret != null)
            {
                DeselectTurret();
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
            instructionText.text = "Turret selected\nRight click to upgrade or sell";
            instructionText.color = Color.cyan  ;
        }
        else
        {
            instructionText.text = "Click 'Place Turret' to build\nRight click turrets to select";
            instructionText.color = Color.white;
        }
    }

    private void SelectTurretAtMousePosition()
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition );
        RaycastHit hit;
        
        if(Physics.Raycast(ray, out hit) )
        {
            PlaceableTurret turret = hit.collider.GetComponent<PlaceableTurret>();
            if(turret != null)
            {
                SelectTurret(turret);
            }
        }
    }

    public void SelectTurret(PlaceableTurret turret)
    {
        if(selectedTurret != null)
        {
            selectedTurret.HideRange();
        }

        selectedTurret = turret;

        if(selectedTurret != null)
        {
            selectedTurret.ShowRange();
            UpdateTurretInfoPanel();

            if(turretInfoPanel != null)
            {
                turretInfoPanel.gameObject.SetActive(true);
            }
        }
    }

    public void DeselectTurret()
    {
        if(selectedTurret != null)
        {
            selectedTurret.HideRange();
            selectedTurret = null;
        }

        if(selectedTurret != null)
        {
            turretInfoPanel.gameObject.SetActive(true );
        }
    }

    public void UpdateTurretInfoPanel()
    {
        if (selectedTurret == null) return;

        if(turretDamageText != null)
        {
            turretDamageText.text = $"Damage: {selectedTurret.damage:F1}";
        }

        if(turretRangeText != null)
        {
            turretRangeText.text = $"Range: {selectedTurret.attackRange:F1}";
        }

        if(turretFireRateText != null)
        {
            turretFireRateText.text = $"Fire Rate: {selectedTurret.fireRate:F1}/s";
        }

        if(upgradeCostText != null)
        {
            float upgradeCost = selectedTurret.GetUpgradeCost();
            upgradeCostText.text = $"Upgrade: {upgradeCost:F0}";

            if(upgradeTurretButton != null )
            {
                upgradeTurretButton.interactable = placementManager.PlayerResources >= upgradeCost;
            }
        }
    }

    public void UpgradeSelectedTurret()
    {
        if(selectedTurret != null ) 
        {
            float upgradeCost = selectedTurret.GetUpgradeCost();

            if(placementManager.PlayerResources >= upgradeCost)
            {
                placementManager.DeductResources(upgradeCost);
                selectedTurret.UpgradeTurret();
                UpdateTurretInfoPanel();
            }
            else
            {
                Debug.Log("Not enough resources to upgrade turret");
            }
        }
    }

    public void SellSelectedTurret()
    {
        if(selectedTurret !=null)
        {
            float sellValue = placementManager.turretCost * 0.75f;

            placementManager.AddResources(sellValue);

            Vector2Int turretPos = selectedTurret.GridPosition;
            placementManager.RemoveTurret(turretPos);

            DeselectTurret();

            Debug.Log($"Turret sold for {sellValue} resources");
        }
    }
}
