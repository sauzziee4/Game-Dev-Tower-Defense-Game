// TowerPlacementManager.cs
using UnityEngine;
using System.Collections.Generic;

public class TowerPlacementManager : MonoBehaviour
{
    [Header("Tower Settings")]
    public GameObject[] towerPrefabs;

    public LayerMask tileLayerMask = 1;

    [Header("UI Feedback")]
    public GameObject placementPreviewPrefab; // Optional: ghost tower for preview

    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    // A reference to the HexGridGenerator for WorldToHex conversion
    private HexGridGenerator hexGridGenerator;

    // A new reference to the HexGrid for tile lookups
    private HexGrid hexGrid;

    private Camera playerCamera;
    private GameObject currentPreview;
    private int selectedTowerIndex = 0;
    private Dictionary<Vector2Int, GameObject> placedTowers = new Dictionary<Vector2Int, GameObject>();

    // Events for UI/game management
    public System.Action<Vector2Int, GameObject> OnTowerPlaced;

    public System.Action<Vector2Int> OnTowerRemoved;

    private void Start()
    {
        hexGridGenerator = FindFirstObjectByType<HexGridGenerator>();
        hexGrid = FindFirstObjectByType<HexGrid>();
        playerCamera = Camera.main;

        if (hexGridGenerator == null)
        {
            Debug.Log("HexGridGenerator not found! Make sure it exists in the scene.");
        }
        if (hexGrid == null)
        {
            Debug.Log("HexGrid not found! Make sure it exists in the scene.");
        }
    }

    private void Update()
    {
        HandleInput();
        UpdatePlacementPreview();
    }

    private void HandleInput()
    {
        // Left click to place tower
        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceTower();
        }

        // Right click to remove tower
        if (Input.GetMouseButtonDown(1))
        {
            TryRemoveTower();
        }

        // Number keys to select tower type
        for (int i = 0; i < towerPrefabs.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedTowerIndex = i;
                DestroyPreview();
            }
        }

        // Escape to cancel placement
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DestroyPreview();
        }
    }

    private void UpdatePlacementPreview()
    {
        Vector2Int? hoveredCoords = GetHoveredHexCoordinates();

        if (hoveredCoords.HasValue && CanPlaceTowerAt(hoveredCoords.Value))
        {
            ShowPlacementPreview(hoveredCoords.Value, true);
        }
        else if (hoveredCoords.HasValue)
        {
            ShowPlacementPreview(hoveredCoords.Value, false);
        }
        else
        {
            DestroyPreview();
        }
    }

    private Vector2Int? GetHoveredHexCoordinates()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, tileLayerMask))
        {
            // Convert world position back to hex coordinates
            return hexGridGenerator.WorldToHex(hit.point);
        }

        return null;
    }

    private bool CanPlaceTowerAt(Vector2Int hexCoords)
    {
        // Check if there's already a tower here
        if (placedTowers.ContainsKey(hexCoords))
            return false;

        // Check if this is a valid tile (grass tile, not path or castle)
        GameObject tile = hexGrid.GetTileAt(hexCoords);
        if (tile == null)
            return false;

        // Check if it's a grass tile (not a path or castle)
        if (tile.name.Contains("PathTile_") || tile.name.Contains("Castle"))
            return false;

        return true;
    }

    private void TryPlaceTower()
    {
        Vector2Int? coords = GetHoveredHexCoordinates();

        if (coords.HasValue && CanPlaceTowerAt(coords.Value))
        {
            PlaceTower(coords.Value, selectedTowerIndex);
        }
    }

    private void PlaceTower(Vector2Int hexCoords, int towerIndex)
    {
        if (towerIndex >= towerPrefabs.Length)
            return;

        Vector3 worldPos = hexGridGenerator.GetHexPosition(hexCoords);
        worldPos.y += 0.1f;

        GameObject tower = Instantiate(towerPrefabs[towerIndex], worldPos, Quaternion.identity);
        tower.name = $"Tower_{hexCoords.x}_{hexCoords.y}_{towerPrefabs[towerIndex].name}";

        // Store the tower
        placedTowers[hexCoords] = tower;

        // Invoke event
        OnTowerPlaced?.Invoke(hexCoords, tower);

        Debug.Log($"Tower placed at hex coordinates {hexCoords}");
    }

    private void TryRemoveTower()
    {
        Vector2Int? coords = GetHoveredHexCoordinates();

        if (coords.HasValue && placedTowers.ContainsKey(coords.Value))
        {
            RemoveTower(coords.Value);
        }
    }

    private void RemoveTower(Vector2Int hexCoords)
    {
        if (placedTowers.TryGetValue(hexCoords, out GameObject tower))
        {
            Destroy(tower);
            placedTowers.Remove(hexCoords);

            OnTowerRemoved?.Invoke(hexCoords);

            Debug.Log($"Tower removed from hex coordinates {hexCoords}");
        }
    }

    private void ShowPlacementPreview(Vector2Int hexCoords, bool isValid)
    {
        if (placementPreviewPrefab == null || towerPrefabs.Length == 0)
            return;

        if (currentPreview == null)
        {
            CreatePreviewTower();
        }

        Vector3 worldPos = hexGridGenerator.GetHexPosition(hexCoords);
        worldPos.y += 0.1f;
        currentPreview.transform.position = worldPos;

        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        Material materialToUse = isValid ? validPlacementMaterial : invalidPlacementMaterial;

        foreach (Renderer renderer in renderers)
        {
            if (materialToUse != null)
            {
                renderer.material = materialToUse;
            }
        }

        currentPreview.SetActive(true);
    }

    private void CreatePreviewTower()
    {
        if (placementPreviewPrefab != null)
        {
            currentPreview = Instantiate(placementPreviewPrefab);
        }
        else if (towerPrefabs[selectedTowerIndex] != null)
        {
            currentPreview = Instantiate(towerPrefabs[selectedTowerIndex]);

            // Disable any scripts on the preview
            MonoBehaviour[] scripts = currentPreview.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                script.enabled = false;
            }
        }

        if (currentPreview != null)
        {
            currentPreview.name = "TowerPreview";

            // Make it slightly transparent
            Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Color color = renderer.material.color;
                color.a = 0.7f;
                renderer.material.color = color;
            }
        }
    }

    private void DestroyPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    // Public methods for external access
    public GameObject GetTowerAt(Vector2Int hexCoords)
    {
        placedTowers.TryGetValue(hexCoords, out GameObject tower);
        return tower;
    }

    public Dictionary<Vector2Int, GameObject> GetAllTowers()
    {
        return new Dictionary<Vector2Int, GameObject>(placedTowers);
    }

    public void SetSelectedTowerType(int index)
    {
        if (index >= 0 && index < towerPrefabs.Length)
        {
            selectedTowerIndex = index;
            DestroyPreview();
        }
    }
}