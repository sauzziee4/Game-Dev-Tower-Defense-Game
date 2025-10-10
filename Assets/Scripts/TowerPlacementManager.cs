using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;


public class TurretPlacementManager : MonoBehaviour
{
    [System.Serializable]
public class DefenderType
{
    public string name;
    public GameObject prefab;
    public GameObject previewPrefab;
    public float cost;
    public Sprite icon;
}
    [Header("Defender Types")]
    public DefenderType[] defenderTypes;


    [Header("Turret Settings")]
    public GameObject turretPrefab;
    public float turretCost = 50f;


    public float turretPlacementHeight = 0.1f;

    [Header("Visual Feedback")]
    public Material validPlacementMaterial;

    public Material invalidPlacementMaterial;
    public GameObject placementPreviewPrefab;

    [Header("UI References")]
    public UnityEngine.UI.Button placeTurretButton;

    public TextMeshProUGUI resourceText;

    private HexGrid hexGrid;
    private HexGridGenerator hexGridGenerator;
    private Camera playerCamera;

    // Placement state
    private bool isPlacingTurret = false;
    private int selectedDefenderIndex = 0;

    private GameObject currentPreview;
    private Vector2Int hoveredHexCoords;
    private GameObject hoveredHex;

    private Material originalHexMaterial;

    // Resource management
    private float playerResources = 200f; // Starting resources

    // Track placed turrets
    private Dictionary<Vector2Int, GameObject> placedTurrets = new Dictionary<Vector2Int, GameObject>();

    private void Start()
    {
        hexGrid = Object.FindFirstObjectByType<HexGrid>();
        hexGridGenerator = Object.FindFirstObjectByType<HexGridGenerator>();
        playerCamera = Camera.main;
        if(defenderTypes == null || defenderTypes.Length == 0)
        {
            InitializeLegacyDefenderTypes();
        }

        //set up UI button event
        if (placeTurretButton != null)
        {
            //when clicked calls 'ToggleTurretPlacement'
            placeTurretButton.onClick.AddListener(ToggleTurretPlacement);
        }

        UpdateResourceDisplay();
    }

    private void InitializeLegacyDefenderTypes()
    {
        defenderTypes = new DefenderType[1];
        defenderTypes[0] = new DefenderType
        {
            name = "Basic Turret",
            prefab = turretPrefab,
            previewPrefab = placementPreviewPrefab,
            cost = turretCost,
            icon = null
        };
    }
    private void Update()
    {
        if (isPlacingTurret)
        {
            HandleTurretPlacement();
        }

        // Cancel placement with right click or escape
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTurretPlacement();
        }

        for(int i = 0; i <defenderTypes.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedDefenderIndex = i;
                UpdatePlacementPreview();
                Debug.Log($"Selected defender: {defenderTypes[selectedDefenderIndex].name}");
            }
        }
    }

    public void ToggleTurretPlacement()
    {
        if (isPlacingTurret)
        {
            CancelTurretPlacement();
        }
        else
        {
            StartTurretPlacement();
        }
    }

    private void StartTurretPlacement()
    {
        DefenderType selectedType = defenderTypes[selectedDefenderIndex];

        if (playerResources < selectedType.cost)
        {
            Debug.Log($"Not enough resources to place {selectedType.name} Cost: {selectedType.cost!}, Available: {playerResources}");
            return;
        }

        isPlacingTurret = true;

        // Update button text or state
        if (placeTurretButton != null)
        {
            var buttonText = placeTurretButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (buttonText != null)
            {
                buttonText.text = "Cancel";
            }
        }

        // Create preview if prefab is assigned
        UpdatePlacementPreview();
    }

    private void CancelTurretPlacement()
    {
        isPlacingTurret = false;

        // Destroy preview
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }

        // Reset hovered hex material
        ResetHoveredHex();

        // Update button text
        if (placeTurretButton != null)
        {
            var buttonText = placeTurretButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (buttonText != null)
            {
                buttonText.text = "Place Turret";
            }
        }
    }

    private void HandleTurretPlacement()
    {
        // Raycast from camera to world
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Convert world position to hex coordinates
            Vector2Int hexCoords = hexGridGenerator.WorldToHex(hit.point);

            // Check if we're hovering over a new hex
            if (hexCoords != hoveredHexCoords)
            {
                ResetHoveredHex();
                hoveredHexCoords = hexCoords;
                hoveredHex = hexGrid.GetTileAt(hexCoords);

                if (hoveredHex != null)
                {
                    bool canPlace = CanPlaceTurretAt(hexCoords);
                    HighlightHex(hoveredHex, canPlace);

                    // Update preview position
                    if (currentPreview != null)
                    {
                        Vector3 previewPos = hexGridGenerator.HexToWorld(hexCoords);
                        previewPos.y += turretPlacementHeight;
                        currentPreview.transform.position = previewPos;
                        currentPreview.SetActive(true);

                        // Change preview color based on validity
                        var renderers = currentPreview.GetComponentsInChildren<Renderer>();
                        foreach (var renderer in renderers)
                        {
                            renderer.material = canPlace ? validPlacementMaterial : invalidPlacementMaterial;
                        }
                    }
                }
            }

            // Handle placement on left click
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceTurret(hexCoords);
            }
        }
        else
        {
            // No hit, hide preview
            if (currentPreview != null)
            {
                currentPreview.SetActive(false);
            }
            ResetHoveredHex();
        }
    }

    private bool CanPlaceTurretAt(Vector2Int coords)
    {
        // Check if tile exists
        GameObject tile = hexGrid.GetTileAt(coords);
        if (tile == null) return false;

        // Check using HexTile component first
        HexTile hexTile = tile.GetComponent<HexTile>();
        if (hexTile != null)
        {
            return hexTile.CanPlaceTurret();
        }

        // Fallback to name checking if no HexTile component
        string tileName = tile.name.ToLower();
        if (tileName.Contains("path") || tileName.Contains("castle"))
        {
            return false;
        }

        // Check if there's already a turret here
        if (placedTurrets.ContainsKey(coords))
        {
            return false;
        }

        DefenderType selectedType = defenderTypes[selectedDefenderIndex];
        if (playerResources < selectedType.cost)
        {
            return false;
        }

        return true;
    }

    private void TryPlaceTurret(Vector2Int coords)
    {
        if (!CanPlaceTurretAt(coords))
        {
            Debug.Log("Cannot place turret at this location!");
            return;
        }

        DefenderType selectedType = defenderTypes[selectedDefenderIndex];

        // Get the tile and mark it as occupied before placing turret
        GameObject tile = hexGrid.GetTileAt(coords);
        HexTile hexTile = tile?.GetComponent<HexTile>();

        Vector3 worldPos = hexGridGenerator.HexToWorld(coords);
        worldPos.y += turretPlacementHeight;

        GameObject newTurret = Instantiate(selectedType.prefab, worldPos, Quaternion.identity);

        // Track in both systems
        placedTurrets[coords] = newTurret;

        // Mark the hex tile as occupied
        if (hexTile != null)
        {
            hexTile.SetOccupied(newTurret);
        }

        playerResources -= selectedType.cost;
        UpdateResourceDisplay();

        CancelTurretPlacement();

        Debug.Log($"{selectedType.name} placed at {coords}. Remaining resources: {playerResources}");
    }

    private void HighlightHex(GameObject hex, bool isValid)
    {
        if (hex == null) return;

        Renderer renderer = hex.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (originalHexMaterial == null)
            {
                originalHexMaterial = renderer.material;
            }

            Material highlightMaterial = isValid ? validPlacementMaterial : invalidPlacementMaterial;
            if (highlightMaterial != null)
            {
                renderer.material = highlightMaterial;
            }
        }
    }

    public bool DeductResources(float amount)
    {
        if (playerResources >= amount)
        {
            playerResources -= amount;
            UpdateResourceDisplay();
            return true;
        }
        return false;
    }

    private void ResetHoveredHex()
    {
        if (hoveredHex != null && originalHexMaterial != null)
        {
            Renderer renderer = hoveredHex.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = originalHexMaterial;
            }

            originalHexMaterial = null;
            hoveredHex = null;
        }
        hoveredHexCoords = new Vector2Int(int.MaxValue, int.MaxValue);
    }

    private void UpdateResourceDisplay()
    {
        if (resourceText != null)
        {
            DefenderType selectedType = defenderTypes[selectedDefenderIndex];
            resourceText.text = $"Resources: {playerResources:F0} | {selectedType.name} Cost: {selectedType.cost}";
        }
    }

    public void AddResources(float amount)
    {
        playerResources += amount;
        UpdateResourceDisplay();
    }

    public GameObject GetTurretAt(Vector2Int coords)
    {
        return placedTurrets.TryGetValue(coords, out GameObject turret) ? turret : null;
    }

    public bool RemoveTurret(Vector2Int coords)
    {
        if (placedTurrets.TryGetValue(coords, out GameObject turret))
        {
            placedTurrets.Remove(coords);

            // Clear the hex tile occupancy
            GameObject tile = hexGrid.GetTileAt(coords);
            HexTile hexTile = tile?.GetComponent<HexTile>();
            if (hexTile != null)
            {
                hexTile.SetUnoccupied();
            }

            if (turret != null)
            {
                Destroy(turret);
            }
            return true;
        }
        return false;
    }

    public void SelectDefenderType(int index)
    {
        if (index >= 0 && index < defenderTypes.Length)
        {
            selectedDefenderIndex = index;
            UpdatePlacementPreview();
            Debug.Log($"Selected defender: {defenderTypes[selectedDefenderIndex].name}");
        }
    }

    private void UpdatePlacementPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }

        if (isPlacingTurret)
        {
            DefenderType selectedType = defenderTypes[selectedDefenderIndex];
            if(selectedType.previewPrefab != null)
            {
                currentPreview = Instantiate(selectedType.previewPrefab);
                currentPreview.SetActive(false);
            }
        }
    }
    public float PlayerResources => playerResources;
    public bool IsPlacingTurret => isPlacingTurret;
    public DefenderType[] DefenderTypes => defenderTypes;
    public int SelectedDefenderIndex => selectedDefenderIndex;
}