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

    // Hex axial directions for neighbor lookup.
    private readonly Vector2Int[] HexDirections =
    {
         new Vector2Int(1, 0),    // E
        new Vector2Int(0, 1),    // SE
        new Vector2Int(-1, 1),   // SW
        new Vector2Int(-1, 0),   // W
        new Vector2Int(0, -1),   // NW
        new Vector2Int(1, -1)    // NE
    };

    private void Awake()
    {
        hexGrid = GetComponent<HexGrid>();
        pathfinder = GetComponent<Pathfinder>();
        // Ensure the Pathfinder component is on the same GameObject or accessible.
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
        if (hexGrid != null)
        {
            hexGrid.ClearGrid();
        }

        // Generate the base grass layer and place the central tower.
        GenerateHexagons();
        PlaceCentralTower();

        // Generate paths from the edge to the center and collect spawn points. This is the updated method call to work
        // with the fixed Pathfinder.cs.
        spawnPoints = GeneratePaths();

        // Populate the remaining empty hexagons with random decorations.
        GenerateDecorations();

        // After all tiles are in place, build the NavMesh.
        navMeshSurface.BuildNavMesh();
    }

    // Creates the central tile and the surrounding grass tiles.
    private void GenerateHexagons()
    {
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);
            for (int r = r1; r <= r2; r++)
            {
                Vector2Int hexCoords = new Vector2Int(q, r);
                SpawnHex(hexCoords, HexType.Grass);
            }
        }
    }

    public void SpawnHex(Vector2Int coords, HexType type)
    {
        HexVariant variant = GetRandomVariant(type);
        if (variant != null)
        {
            Vector3 worldPos = HexToWorld(coords);
            GameObject hex = Instantiate(variant.prefab, worldPos, Quaternion.identity, transform);
            hexGrid.AddTile(coords, hex);
        }
    }

    // Handles the generation and placement of all paths.
    private List<GameObject> GeneratePaths()
    {
        // Now calling the updated Pathfinder method with the correct arguments.
        return pathfinder.GeneratePathsToCenter(gridRadius, pathCount, this);
    }

    private void GenerateDecorations()
    {
        if (decorationPrefabs.Length == 0) return;

        foreach (var tile in hexGrid.GetAllTiles())
        {
            HexTile hexTile = tile.GetComponent<HexTile>();
            if (hexTile != null && hexTile.variant.hexType == HexType.Grass && UnityEngine.Random.value < decorationChance)
            {
                // Get a random decoration prefab from the array.
                GameObject decorationPrefab = decorationPrefabs[UnityEngine.Random.Range(0, decorationPrefabs.Length)];

                // Instantiate the decoration with a slight height offset.
                Vector3 position = tile.transform.position;
                position.y += decorationHeightOffset;
                GameObject decoration = Instantiate(decorationPrefab, position, Quaternion.identity, tile.transform);
            }
        }
    }

    //spawns central tower and marks tile as castle type
    private void PlaceCentralTower()
    {
        if (centralTowerInstance != null)
        {
            Destroy(centralTowerInstance);
        }

        Vector2Int centerCoords = Vector2Int.zero;
        SpawnHex(centerCoords, HexType.Castle);

        Vector3 towerPosition = HexToWorld(centerCoords);
        centralTowerInstance = Instantiate(towerPrefab, towerPosition, Quaternion.identity, transform);
    }

    // Gets a random hex variant for a given hex type.
    private HexVariant GetRandomVariant(HexType type)
    {
        if (variantDict.TryGetValue(type, out var variants))
        {
            if (variants.Count > 0)
            {
                return variants[UnityEngine.Random.Range(0, variants.Count)];
            }
        }
        return null;
    }

    private void InitializeVariantDictionary()
    {
        variantDict = new Dictionary<HexType, List<HexVariant>>();
        foreach (HexVariantSet set in variantSets)
        {
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

    // Public method to get spawn points
    public List<GameObject> GetSpawnPoints()
    {
        return spawnPoints;
    }

    public GameObject GetTowerInstance()
    {
        return centralTowerInstance;
    }
}