using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TurretPlacementManager : MonoBehaviour
{
    [Header("Turret Settings")]
    public GameObject turretPrefab;
    public float turretCost = 50f;
    public float turretPlacementHeight = 0.1f;

    [Header("Visual Feedback")]
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    public GameObject placementPreviewPrefab; // Optional preview of turret

    [Header("UI References")]
    public UnityEngine.UI.Button placeTurretButton;
    public UnityEngine.UI.Text resourceText;

    private HexGrid hexGrid;
    private HexGridGenerator hexGridGenerator;
    private Camera playerCamera;

    // Placement state
    private bool isPlacingTurret = false;
    private GameObject currentPreview;
    private Vector2Int hoveredHexCoords;
    private GameObject hoveredHex;

    // Resource management
    private float playerResources = 200f; // Starting resources

    // Track placed turrets
    private Dictionary<Vector2Int, GameObject> placedTurrets = new Dictionary<Vector2Int, GameObject>();

    private void Start()
    {
        hexGrid = Object.FindFirstObjectByType<HexGrid>();
        hexGridGenerator = Object.FindFirstObjectByType<HexGridGenerator>();
        playerCamera = Camera.main;

        if (placeTurretButton != null)
        {
            placeTurretButton.onClick.AddListener(ToggleTurretPlacement);
        }

        UpdateResourceDisplay();
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
        if (playerResources < turretCost)
        {
            Debug.Log("Not enough resources to place turret!");
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
        if (placementPreviewPrefab != null)
        {
            currentPreview = Instantiate(placementPreviewPrefab);
            currentPreview.SetActive(false);
        }
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

        // Check resources
        if (playerResources < turretCost)
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

        // Calculate world position
        Vector3 worldPos = hexGridGenerator.HexToWorld(coords);
        worldPos.y += turretPlacementHeight;

        // Instantiate turret
        GameObject newTurret = Instantiate(turretPrefab, worldPos, Quaternion.identity);

        // Track the placed turret
        placedTurrets[coords] = newTurret;

        // Deduct resources
        playerResources -= turretCost;
        UpdateResourceDisplay();

        // End placement mode
        CancelTurretPlacement();

        Debug.Log($"Turret placed at {coords}. Remaining resources: {playerResources}");
    }

    private void HighlightHex(GameObject hex, bool isValid)
    {
        if (hex == null) return;

        Renderer renderer = hex.GetComponent<Renderer>();
        if (renderer != null)
        {
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
        if (hoveredHex != null)
        {
            // Reset to original material
            HexTile hexTile = hoveredHex.GetComponent<HexTile>();
            if (hexTile != null && hexTile.variant != null && hexTile.variant.material != null)
            {
                Renderer renderer = hoveredHex.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = hexTile.variant.material;
                }
            }
            hoveredHex = null;
        }
        hoveredHexCoords = new Vector2Int(int.MaxValue, int.MaxValue); // Invalid coords
    }

    private void UpdateResourceDisplay()
    {
        if (resourceText != null)
        {
            resourceText.text = $"Resources: {playerResources:F0}";
        }
    }

    // Public method to add resources (for when enemies are defeated, etc.)
    public void AddResources(float amount)
    {
        playerResources += amount;
        UpdateResourceDisplay();
    }

    // Public method to get turret at specific coordinates
    public GameObject GetTurretAt(Vector2Int coords)
    {
        return placedTurrets.TryGetValue(coords, out GameObject turret) ? turret : null;
    }

    // Public method to remove turret (for selling, etc.)
    public bool RemoveTurret(Vector2Int coords)
    {
        if (placedTurrets.TryGetValue(coords, out GameObject turret))
        {
            placedTurrets.Remove(coords);
            if (turret != null)
            {
                Destroy(turret);
            }
            return true;
        }
        return false;
    }

    // Properties
    public float PlayerResources => playerResources;
    public bool IsPlacingTurret => isPlacingTurret;
}