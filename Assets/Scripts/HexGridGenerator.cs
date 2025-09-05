using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    [Header("Tile Variants")]
    public HexVariantSet[] variantSets;

    [Header("Map Settings")]
    public int gridRadius = 8;

    public int pathCount = 3;

    [Header("Path Separation")]
    [Range(1, 5)]
    public int minPathSeparation = 2;

    [Header("Visual Settings")]
    public float hexSize = 1f;

    [Header("Decoration Settings")]
    public GameObject[] decorationPrefabs;

    [Range(0f, 1f)]
    public float decorationChance = 0.3f;

    public float decorationHeightOffset = 0.02f;

    [Header("Spawn Point Settings")]
    public float spawnPointHeight = 0.5f;

    [Header("Tower Settings")]
    public GameObject towerPrefab;

    private GameObject centralTowerInstance;

    [Header("NavMesh Settings")]
    [SerializeField]
    private NavMeshSurface navMeshSurface;

    private Dictionary<HexType, List<HexVariant>> variantDict;
    private List<GameObject> spawnPoints = new List<GameObject>();

    private HexGrid hexGrid;
    private Pathfinder pathfinder;

    private void Awake()
    {
        InitializeVariantDictionary();
        hexGrid = GetComponent<HexGrid>();
        pathfinder = GetComponent<Pathfinder>();
    }

    private void Start()
    {
        GenerateGrid();
    }

    private void InitializeVariantDictionary()
    {
        variantDict = new Dictionary<HexType, List<HexVariant>>();
        foreach (var set in variantSets)
        {
            if (!variantDict.ContainsKey(set.hexType))
            {
                variantDict.Add(set.hexType, new List<HexVariant>());
            }
            variantDict[set.hexType].AddRange(set.variants);
        }
    }

    public void GenerateGrid()
    {
        // Clear existing grid
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        hexGrid.ClearGrid();
        spawnPoints.Clear();

        // 1. Generate the base terrain with Grass tiles
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                Vector2Int coords = new Vector2Int(q, r);
                PlaceTile(coords, HexType.Grass);
            }
        }

        // 2. Generate multiple paths from the edge to the center
        Vector2Int centralCoords = Vector2Int.zero;
        PlaceTile(centralCoords, HexType.Castle);

        // This is a placeholder for the tower instance
        centralTowerInstance = FindFirstObjectByType<Tower>()?.gameObject;
        if (centralTowerInstance == null && towerPrefab != null)
        {
            Vector3 worldPos = HexToWorld(centralCoords);
            centralTowerInstance = Instantiate(towerPrefab, worldPos, Quaternion.identity, transform);
            centralTowerInstance.name = "CentralTower";
        }

        // Generate multiple paths
        for (int i = 0; i < pathCount; i++)
        {
            GeneratePath(centralCoords);
        }

        // 3. Re-place tiles on the path
        foreach (var tile in hexGrid.GetAllTiles())
        {
            HexTile hexTile = tile.GetComponent<HexTile>();
            if (hexTile.variant.openEdges.Length > 0) // Check if the tile is a path tile
            {
                ReplaceTileWithVariant(tile, hexTile);
            }
        }

        // 4. Decorate the Grass tiles
        foreach (var tile in hexGrid.GetAllTiles())
        {
            HexTile hexTile = tile.GetComponent<HexTile>();
            if (hexTile.variant.hexType == HexType.Grass)
            {
                if (UnityEngine.Random.value < decorationChance)
                {
                    DecorateTile(tile);
                }
            }
        }

        // Build the NavMesh on the generated terrain
        if (navMeshSurface != null)
        {
            Debug.Log("Building NavMesh...");
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            Debug.Log("NavMeshSurface component not found! Please add it to the HexGridGenerator object.");
        }
    }

    private void ReplaceTileWithVariant(GameObject oldTile, HexTile hexTile)
    {
        Vector2Int coords = WorldToHex(oldTile.transform.position);
        Destroy(oldTile);
        PlaceTile(coords, hexTile.variant.hexType, hexTile.variant.openEdges);
    }

    private void GeneratePath(Vector2Int endCoords)
    {
        // Select a random starting tile on the outer edge
        Vector2Int startCoords = GetRandomOuterHex();

        // Find a path using A*
        List<Vector2Int> path = pathfinder.FindPath(startCoords, endCoords, gridRadius);

        if (path != null)
        {
            // Mark the tiles on the path
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int coords = path[i];
                // Mark the tile as a path tile
                GameObject tile = hexGrid.GetTileAt(coords);
                if (tile != null)
                {
                    HexTile hexTile = tile.GetComponent<HexTile>();
                    hexTile.variant = GetRandomVariant(HexType.Path);
                    // Add start point
                    if (i == 0)
                    {
                        spawnPoints.Add(tile);
                    }
                }
            }
        }
    }

    private Vector2Int GetRandomOuterHex()
    {
        List<Vector2Int> outerHexes = new List<Vector2Int>();
        int q, r, s;

        // Iterate through all coordinates to find outer edge tiles
        for (q = -gridRadius; q <= gridRadius; q++)
        {
            r = -q - gridRadius;
            s = -q - r;
            if (Mathf.Abs(s) <= gridRadius) outerHexes.Add(new Vector2Int(q, r));

            r = -q + gridRadius;
            s = -q - r;
            if (Mathf.Abs(s) <= gridRadius) outerHexes.Add(new Vector2Int(q, r));
        }

        for (r = -gridRadius; r <= gridRadius; r++)
        {
            q = -r - gridRadius;
            s = -q - r;
            if (Mathf.Abs(s) <= gridRadius) outerHexes.Add(new Vector2Int(q, r));

            q = -r + gridRadius;
            s = -q - r;
            if (Mathf.Abs(s) <= gridRadius) outerHexes.Add(new Vector2Int(q, r));
        }
        // Remove duplicates and return a random one
        return outerHexes.Distinct().ElementAt(UnityEngine.Random.Range(0, outerHexes.Count));
    }

    private void PlaceTile(Vector2Int coords, HexType type, int[] openEdges = null)
    {
        HexVariant variant = GetRandomVariant(type);
        if (openEdges != null)
        {
            // Find a variant that matches the required edges for path connections
            variant = variantDict[type].FirstOrDefault(v => v.openEdges != null && v.openEdges.SequenceEqual(openEdges));
            if (variant == null)
            {
                variant = GetRandomVariant(type);
            }
        }

        Vector3 position = HexToWorld(coords);
        GameObject tile = Instantiate(variant.prefab, position, Quaternion.identity, transform);
        HexTile hexTileComponent = tile.AddComponent<HexTile>();
        hexTileComponent.variant = variant;
        hexGrid.AddTile(coords, tile);
    }

    private void DecorateTile(GameObject tile)
    {
        int randomDecorationIndex = UnityEngine.Random.Range(0, decorationPrefabs.Length);
        GameObject decorationPrefab = decorationPrefabs[randomDecorationIndex];
        Vector3 tilePosition = tile.transform.position;
        Vector3 decorationPosition = new Vector3(
            tilePosition.x,
            tilePosition.y + decorationHeightOffset,
            tilePosition.z
        );
        Instantiate(decorationPrefab, decorationPosition, Quaternion.identity, tile.transform);
    }

    private HexVariant GetRandomVariant(HexType type)
    {
        if (variantDict.ContainsKey(type))
        {
            var variants = variantDict[type];
            return variants[UnityEngine.Random.Range(0, variants.Count)];
        }
        Debug.LogError("No variant set found for type: " + type);
        return null;
    }

    private Vector3 HexToWorld(Vector2Int coords)
    {
        float x = hexSize * (coords.x * 1.5f);
        float z = hexSize * (coords.y * Mathf.Sqrt(3) + coords.x * Mathf.Sqrt(3) / 2f);
        return new Vector3(x, 0, z);
    }

    public Vector2Int WorldToHex(Vector3 worldPosition)
    {
        float q = (worldPosition.x * Mathf.Sqrt(3) / 3f - worldPosition.z / 3f) / hexSize;
        float r = worldPosition.z * 2f / 3f / hexSize;
        return HexRound(q, r);
    }

    // Snaps floating-point hex coordinates to the nearest integer hex coordinates.
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
        {
            rq = -rr - rs;
        }
        else if (r_diff > s_diff)
        {
            rr = -rq - rs;
        }

        return new Vector2Int(rq, rr);
    }

    // Gets the world position of a hex coordinate.
    public Vector3 GetHexPosition(Vector2Int coords)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * coords.x + Mathf.Sqrt(3f) / 2f * coords.y);
        float z = hexSize * (3f / 2f * coords.y);
        return new Vector3(x, 0, z);
    }

    // Public method to get spawn points
    public List<GameObject> GetSpawnPoints()
    {
        return spawnPoints;
    }
}