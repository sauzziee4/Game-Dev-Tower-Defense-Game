using UnityEngine;
using System.Collections.Generic;

public class TowerPlacer : MonoBehaviour
{
    [Header("Tower Settings")]
    public GameObject towerPrefab;

    public float towerHealth = 100f;
    public float attackRange = 5f;
    public float attackDamage = 25f;
    public float attackRate = 1f; //attacks per second

    [Header("Placement Settings")]
    public LayerMask terrainLayer = 1;

    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    private HexGridGenerator hexGridGenerator;
    private GameObject towerPreview;
    private GameObject placedTower;
    private Camera playerCamera;
    private bool placementMode = false;

    //events
    public System.Action<GameObject> OnTowerPlaced;

    public System.Action<Vector3> OnTowerdestroyed;

    private void Start()
    {
        playerCamera = Camera.main;
        hexGridGenerator = FindObjectOfType<HexGridGenerator>();

        //Create tower preview object
        CreateTowerPreview();

        //Enable placement  immediately since hex grid generates in Awake()
        EnableTowerPlacement();
    }

    private void Update()
    {
        if (placementMode && placedTower == null)
        {
            HandleTowerPlacement();
        }
    }

    private void CreateTowerPreview()
    {
        if (towerPreview != null)
        {
            towerPreview = Instantiate(towerPrefab);
            towerPreview.name = "TowerPreview";

            //Disable colliders and scripts on preview
            Collider[] colliders = towerPreview.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
                col.enabled = false;

            MonoBehaviour[] scripts = towerPreview.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != this)
                    script.enabled = false;
            }

            towerPreview.SetActive(false);
        }
    }

    private void EnableTowerPlacement()
    {
        placementMode = true;
        Debug.Log("Tower Placement Enabled!");
    }

    private void HandleTowerPlacement()
    {
        Vector3 mousePosition = Input.mousePosition;
        Ray ray = playerCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayer))
        {
            Vector3 placementPosition = GetValidPlacementPosition(hit.point);
            //show preview
            if (towerPreview != null)
            {
                towerPreview.SetActive(true);
                towerPreview.transform.position = placementPosition;

                //update privew material based on validity
                bool isValid = IsValidPlacement(placementPosition);
                UpdatePreviewMaterial(isValid);

                // place tower on click (maybe like when you click restart or new game??
                if (Input.GetMouseButtonDown(0) && isValid)
                {
                    PlaceTower(placementPosition);
                }
            }
        }
        else
        {
            if (towerPreview != null)
                towerPreview.SetActive(false);
        }
    }

    private Vector3 GetValidPlacementPosition(Vector3 hitPoint)
    {
        //snap to hex center using the hex grid system
        if (hexGridGenerator != null)
        {
            Vector2Int hexCoords = WorldToHex(hitPoint);
            return HexToWorld(hexCoords);
        }
        //fallback: just use the hit point with some Y offset
        return new Vector3(hitPoint.x, hitPoint.y + 0.1f, hitPoint.z);
    }

    private bool IsValidPlacement(Vector3 position)
    {
        //convert world position to hex coordinates
        Vector2Int hexCoords = WorldToHex(position);

        //check if position is the castle (center) hex
        if (hexCoords == Vector2Int.zero)
        {
            return true; //tower should be placed at the castle
        }

        //dont allow placement on other hexes
        return false;
    }

    private bool IsOnPathway(Vector3 position)
    {
        Vector2Int hexCoords = WorldToHex(position);

        //check if there is a tile at this position
        if (hexGridGenerator != null && hexGridGenerator.GetTileAt(hexCoords) != null)
        {
            //check if the tile has a path component or is tagged as a path
            GameObject tile = hexGridGenerator.GetTileAt(hexCoords);
            return tile.name.Contains("PathTile") || tile.CompareTag("Path");
        }
        return false;
    }

    private void UpdatePreviewMaterial(bool isValid)
    {
        if (towerPreview == null) return;
        Renderer[] renderers = towerPreview.GetComponentsInChildren<Renderer>();
        Material materialToUse = isValid ? validPlacementMaterial : invalidPlacementMaterial;

        foreach (var renderer in renderers)
        {
            if (materialToUse != null)
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = materialToUse;
                }
                renderer.materials = materials;
            }
        }
    }

    private void PlaceTower(Vector3 position)
    {
        if (towerPrefab != null)
        {
            //instantiate the actual tower
            placedTower = Instantiate(towerPrefab, position, Quaternion.identity);
            placedTower.name = "PlayerTower";

            //setup tower component
            TowerBehaviour towerComponent = placedTower.GetComponent<TowerBehaviour>();
            if (towerComponent == null)
            {
                towerComponent = placedTower.AddComponent<TowerBehaviour>();
            }

            //configure tower properties
            towerComponent.Initialize(towerHealth, attackRange, attackDamage, attackRate);
            towerComponent.OnTowerDestroyed += HandleTowerDestroyed;

            //Disable placement mode
            placementMode = false;
            towerPreview.SetActive(false);

            //notify other systems
            OnTowerPlaced?.Invoke(placedTower);
            Debug.Log($"Tower placed at {position}");
        }
    }

    private void HandleTowerDestroyed(TowerPlacer tower)
    {
        OnTowerdestroyed?.Invoke(tower.transform.position);
        placedTower = null;

        //could re-enable placement mode for rebuilding
        //placementMode = true;
    }

    //public methods for external control
    public void EnablePlacementMode()
    {
        if (placedTower == null)
        {
            placementMode = true;
        }
    }

    public void DisablePlacementMode()
    {
        placementMode = false;
        if (towerPreview != null)
            towerPreview.SetActive(false);
    }

    public bool IsTowerPlaced()
    {
        return placedTower != null;
    }

    public GameObject GetPlacedTower()
    {
        return placedTower;
    }

    public Vector3 GetTowerPosition()
    {
        return placedTower != null ? placedTower.transform.position : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        //visualise castle position(tower placement area)
        if (hexGridGenerator != null)
        {
            Gizmos.color = Color.green;
            Vector3 castlePos = HexToWorld(Vector2Int.zero);
            Gizmos.DrawWireSphere(castlePos, 1f);
        }

        //visualise tower attack range if placed
        if (placedTower != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(placedTower.transform.position, attackRange);
        }
    }

    //helper methos for hex coordinate conversion (matching HexGridGenerator)
    private Vector2Int WorldToHex(Vector3 worldPos)
    {
        float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.z) / 1f;
        float r = (2f / 3f * worldPos.z) / 1f;

        return HexRound(q, r);
    }

    private Vector2Int HexRound(float q, float r)
    {
        float s = -q - r;

        int rq = Mathf.RoundToInt(q);
        int rr = Mathf.RoundToInt(r);
        int rs = Mathf.RoundToInt(s);

        float q_diff = Mathf.Abs(rq - q);
        float r_diff = Mathf.Abs(rr - r);
        float s_diff = Mathf.Abs(rs - s);

        if (q_diff > r_diff && q_diff > s_diff)
            rq = -rr - rs;
        else if (r_diff > s_diff)
            rr = -rq - rs;

        return new Vector2Int(rq, rr);
    }

    private Vector3 HexToWorld(Vector2Int hexCoords)
    {
        float x = Mathf.Sqrt(3f) * (hexCoords.x + hexCoords.y / 2f);
        float z = 1.5f * hexCoords.y;
        return new Vector3(x, 0, z);
    }
}