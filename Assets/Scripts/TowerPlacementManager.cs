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

        tileLayerMask = LayerMask.GetMask("HexTile");
        if (tileLayerMask.value == 0)
        {
            Debug.LogWarning("HexTile layer not found. Raycasts might not work correctly. Please add a layer named 'HexTile' to your tiles.");
        }
    }

    private void Update()
    {
        HandlePlacementPreview();
        HandlePlacementInput();
    }

    private void HandlePlacementPreview()
    {
        if (selectedTowerIndex >= towerPrefabs.Length) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, tileLayerMask))
        {
            Vector2Int hexCoords = hexGridGenerator.WorldToHex(hit.point);
            GameObject hoveredTile = hexGrid.GetTileAt(hexCoords);

            if (hoveredTile != null)
            {
                if (currentPreview == null)
                {
                    CreatePreview();
                }

                bool isValidPlacement = IsValidPlacement(hexCoords, hoveredTile);
                UpdatePreviewPosition(hit.point, isValidPlacement);
            }
        }
        else
        {
            DestroyPreview();
        }
    }

    private void HandlePlacementInput()
    {
        if (Input.GetMouseButtonDown(0) && currentPreview != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, tileLayerMask))
            {
                Vector2Int hexCoords = hexGridGenerator.WorldToHex(hit.point);
                GameObject hoveredTile = hexGrid.GetTileAt(hexCoords);

                if (hoveredTile != null && IsValidPlacement(hexCoords, hoveredTile))
                {
                    PlaceTower(hexCoords, hit.point);
                }
            }
        }
    }

    private bool IsValidPlacement(Vector2Int hexCoords, GameObject tile)
    {
        // Get the HexTile component to check the tile type.
        HexTile hexTile = tile.GetComponent<HexTile>();
        if (hexTile == null) return false;

        // Check if the tile is a Grass tile and no tower is already placed there.
        return hexTile.variant.hexType == HexType.Grass && !placedTowers.ContainsKey(hexCoords);
    }

    private void CreatePreview()
    {
        if (placementPreviewPrefab != null)
        {
            GameObject selectedPrefab = towerPrefabs[selectedTowerIndex];
            currentPreview = Instantiate(selectedPrefab);

            // Disable any scripts on the preview object to prevent unwanted behavior.
            MonoBehaviour[] scripts = currentPreview.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                script.enabled = false;
            }

            // Set the name for easy identification in the hierarchy.
            currentPreview.name = "TowerPreview";

            // Make it slightly transparent.
            Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Color color = renderer.material.color;
                color.a = 0.7f;
                renderer.material.color = color;
            }
        }
    }

    private void UpdatePreviewPosition(Vector3 hitPoint, bool isValid)
    {
        if (currentPreview == null) return;

        //position preview slightly above ground
        currentPreview.transform.position = new Vector3(hitPoint.x, 0.05f, hitPoint.z);

        //change material based on validity (for visual feedback)
        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = isValid ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }

    private void PlaceTower(Vector2Int hexCoords, Vector3 hitPoint)
    {
        GameObject newTower = Instantiate(towerPrefabs[selectedTowerIndex], new Vector3(hitPoint.x, 0, hitPoint.z), Quaternion.identity);
        placedTowers.Add(hexCoords, newTower);

        // Destroy the preview after placement.
        DestroyPreview();

        OnTowerPlaced?.Invoke(hexCoords, newTower);
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