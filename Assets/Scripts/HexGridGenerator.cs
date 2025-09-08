using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    public static event Action OnGridGenerated;

    [Header("Tile Variants")]
    public HexVariantSet[] variantSets;

    [Header("Map Settings")]
    public int gridRadius = 8;
    public int pathCount = 3;

    [Header("Visual Settings")]
    public float hexSize = 1f;

    [Header("Decoration Settings")]
    public GameObject[] decorationPrefabs;
    [Range(0f, 1f)]
    public float decorationChance = 0.3f;
    public float decorationHeightOffset = 0.02f;

    [Header("Tower Settings")]
    public GameObject towerPrefab;
    private GameObject centralTowerInstance;

    [Header("NavMesh Settings")]
    [SerializeField]
    private NavMeshSurface navMeshSurface;

    [Header("NavMesh Area Settings")]
    [Tooltip("Make path tiles walkable (usually 0) and grass tiles non-walkable (usually 1)")]
    public bool useCustomNavMeshAreas = false;
    public int pathNavMeshArea = 0; // Walkable by default
    public int grassNavMeshArea = 1; // Not Walkable by default

    private Dictionary<HexType, List<HexVariant>> variantDict;
    private List<Vector2Int> spawnPointCoords = new List<Vector2Int>();

    private HexGrid hexGrid;
    private Pathfinder pathfinder;

    private void Awake()
    {
        hexGrid = GetComponent<HexGrid>();
        pathfinder = GetComponent<Pathfinder>();
        if (pathfinder == null)
        {
            Debug.LogError("Pathfinder component not found on this GameObject.");
        }
    }

    private void Start()
    {
        InitializeVariantDictionary();
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        hexGrid?.ClearGrid();

        GenerateHexagons();
        PlaceCentralTower();

        spawnPointCoords = pathfinder.GeneratePathsToCenter(gridRadius, pathCount, this);

        StartCoroutine(GenerateDecorationsDelayed());
        StartCoroutine(BuildNavMeshDelayed());
    }

    private IEnumerator BuildNavMeshDelayed()
    {
        yield return new WaitForEndOfFrame();
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh built successfully.");
        }
        else
        {
            Debug.LogError("NavMeshSurface is not assigned in the HexGridGenerator!");
        }
        OnGridGenerated?.Invoke();
        Debug.Log("Grid generation complete. OnGridGenerated event invoked.");
    }

    private IEnumerator GenerateDecorationsDelayed()
    {
        yield return null;
        GenerateDecorations();
    }

    private void GenerateHexagons()
    {
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                SpawnHex(new Vector2Int(q, r), HexType.Grass);
            }
        }
    }

    public void SpawnHex(Vector2Int coords, HexType type)
    {
        GameObject existingTile = hexGrid.GetTileAt(coords);
        if (existingTile != null)
        {
            Destroy(existingTile);
        }

        HexVariant variant = GetRandomVariant(type);
        if (variant != null && variant.prefab != null)
        {
            Vector3 worldPos = HexToWorld(coords);
            GameObject hex = Instantiate(variant.prefab, worldPos, Quaternion.identity, transform);
            hex.name = $"{type}_{coords.x}_{coords.y}";

            HexTile hexTile = hex.GetComponent<HexTile>() ?? hex.AddComponent<HexTile>();
            hexTile.SetCoordinates(coords);
            hexGrid.AddTile(coords, hex);

            // Set up NavMesh modifier for pathfinding
            if (useCustomNavMeshAreas)
            {
                NavMeshModifier modifier = hex.GetComponent<NavMeshModifier>() ?? hex.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;

                switch (type)
                {
                    case HexType.Path:
                        modifier.area = pathNavMeshArea; // Usually 0 (Walkable)
                        break;
                    case HexType.Grass:
                        modifier.area = grassNavMeshArea; // Usually 1 (Not Walkable) 
                        break;
                    case HexType.Castle:
                        modifier.area = pathNavMeshArea; // Castle should be walkable
                        break;
                }
            }
            else
            {
                // Don't override areas, let Unity use default behavior
                // All tiles will be walkable by default if they have colliders
                NavMeshModifier modifier = hex.GetComponent<NavMeshModifier>();
                if (modifier != null)
                {
                    modifier.overrideArea = false;
                }
            }
        }
    }

    private void GenerateDecorations()
    {
        if (decorationPrefabs.Length == 0) return;
        List<Vector2Int> grassTileCoords = hexGrid.GetTilesOfType(HexType.Grass);
        foreach (Vector2Int coords in grassTileCoords)
        {
            if (UnityEngine.Random.value < decorationChance)
            {
                GameObject tile = hexGrid.GetTileAt(coords);
                HexTile hexTile = tile?.GetComponent<HexTile>();
                if (hexTile != null && !hexTile.isOccupied)
                {
                    GameObject prefab = decorationPrefabs[UnityEngine.Random.Range(0, decorationPrefabs.Length)];
                    Vector3 pos = tile.transform.position + new Vector3(0, decorationHeightOffset, 0);
                    GameObject decoration = Instantiate(prefab, pos, Quaternion.identity, tile.transform);
                    hexTile.SetOccupied(decoration);
                }
            }
        }
    }

    private void PlaceCentralTower()
    {
        if (centralTowerInstance != null) Destroy(centralTowerInstance);
        Vector2Int centerCoords = Vector2Int.zero;
        SpawnHex(centerCoords, HexType.Castle);
        if (towerPrefab != null)
        {
            centralTowerInstance = Instantiate(towerPrefab, HexToWorld(centerCoords), Quaternion.identity, transform);
        }
    }

    private HexVariant GetRandomVariant(HexType type)
    {
        if (variantDict.TryGetValue(type, out var variants) && variants.Count > 0)
        {
            return variants[UnityEngine.Random.Range(0, variants.Count)];
        }
        Debug.LogWarning($"No variant found for hex type {type}. Check Inspector setup.");
        return null;
    }

    private void InitializeVariantDictionary()
    {
        variantDict = new Dictionary<HexType, List<HexVariant>>();
        foreach (var set in variantSets)
        {
            if (set.variants != null && set.variants.Length > 0)
                variantDict.Add(set.hexType, new List<HexVariant>(set.variants));
        }
    }

    public Vector3 HexToWorld(Vector2Int hexCoords)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * hexCoords.x + Mathf.Sqrt(3f) / 2f * hexCoords.y);
        float z = hexSize * (3f / 2f * hexCoords.y);
        return new Vector3(x, 0, z);
    }

    public Vector2Int WorldToHex(Vector3 worldPosition)
    {
        float q = (Mathf.Sqrt(3f) / 3f * worldPosition.x - 1f / 3f * worldPosition.z) / hexSize;
        float r = (2f / 3f * worldPosition.z) / hexSize;
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
        if (q_diff > r_diff && q_diff > s_diff) rq = -rr - rs;
        else if (r_diff > s_diff) rr = -rq - rs;
        return new Vector2Int(rq, rr);
    }

    public List<Vector2Int> GetSpawnPointCoords() => spawnPointCoords;

    public GameObject GetTileAt(Vector2Int coords) => hexGrid.GetTileAt(coords);

    public GameObject GetTowerInstance() => centralTowerInstance;
}