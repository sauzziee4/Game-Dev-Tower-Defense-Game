using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

//main hex grid generator that creates and manages the map within the game
//script handles grid generation, pathfinding, NavMesh building, decoration placement and coordinate conversion
//uses an axial coordinate system (q,r) for hexagon math
public class HexGridGenerator : MonoBehaviour
{
    //event is fired when the entire grid generation process is complete
    //used by other systems to know when it is safe to interact with the grid
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
    public bool useCustomNavMeshAreas = false;

    public int pathNavMeshArea = 0; // Walkable by default
    public int grassNavMeshArea = 1; // Not Walkable by default

    private Dictionary<HexType, List<HexVariant>> variantDict;
    private List<Vector2Int> spawnPointCoords = new List<Vector2Int>();

    private HexGrid hexGrid;
    private Pathfinder pathfinder;

    #region Unity

    private void Awake()
    {
        hexGrid = GetComponent<HexGrid>();
        pathfinder = GetComponent<Pathfinder>();
        //Ensures pathfinder is available for path generation
        if (pathfinder == null)
        {
            Debug.LogError("Pathfinder component not found on this GameObject.");
        }
    }

    //Begins grid generation
    private void Start()
    {
        InitializeVariantDictionary();
        GenerateGrid();
    }

    #endregion Unity

    #region Grid Generation

    //The main grid generation that controls the entire process
    //it clears the existing grid and generateds new hexagons, paths, decorations and navmesh
    public void GenerateGrid()
    {
        //clears existing grid data
        hexGrid?.ClearGrid();

        //generates the base grid
        GenerateHexagons();

        //places the center defense tower (castle)
        PlaceCentralTower();

        //generates paths from edges to center and gets the spawn points
        spawnPointCoords = pathfinder.GeneratePathsToCenter(gridRadius, pathCount, this);

        //uses coroutines to ensure the timing is correct for dependent operations
        StartCoroutine(GenerateDecorationsDelayed());
        StartCoroutine(BuildNavMeshDelayed());
    }

    //builds navmesh after all of the tiles are placed to ensure accurate pathfinding
    //uses coroutine to ensure it is completed at the correct time.
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
        //notifies other systems that grid generation is complete
        OnGridGenerated?.Invoke();
        Debug.Log("Grid generation complete. OnGridGenerated event invoked.");
    }

    //generates the decorations after the tiles are correctly generated
    private IEnumerator GenerateDecorationsDelayed()
    {
        yield return null;
        GenerateDecorations();
    }

    // Generates basic hexagon grid structure using the axial coordinate method to create hexagons of the specified
    // radius which is filled with grass tiles
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

    //repsonsible for spawning a hexagon tile at specified coords with the given type this
    // method handles tiles creation, component setup and NavMesh area assignment
    public void SpawnHex(Vector2Int coords, HexType type)
    {
        //removes exisiting tiles if present
        GameObject existingTile = hexGrid.GetTileAt(coords);
        if (existingTile != null)
        {
            Destroy(existingTile);
        }

        HexVariant variant = GetRandomVariant(type);
        if (variant != null && variant.prefab != null)
        {
            //converts the hex coords to world positions
            Vector3 worldPos = HexToWorld(coords);

            //instantiates the tile prefab
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
                //walkable areas for NavMesh
                switch (type)
                {
                    case HexType.Path:
                        modifier.area = pathNavMeshArea;
                        break;

                    case HexType.Grass:
                        modifier.area = grassNavMeshArea;
                        break;

                    case HexType.Castle:
                        modifier.area = pathNavMeshArea;
                        break;
                }
            }
            else
            {
                NavMeshModifier modifier = hex.GetComponent<NavMeshModifier>();
                if (modifier != null)
                {
                    modifier.overrideArea = false;
                }
            }
        }
    }

    //method is responsible for generating  decorations on the grass tiles, includeing trees, mountains, hills ect.
    //randomly places a decorative object on an unoccupuied grass tile
    private void GenerateDecorations()
    {
        //skip if there are no decoration prefabs available
        if (decorationPrefabs.Length == 0) return;

        //gets all grass tile coords for decoration placement
        List<Vector2Int> grassTileCoords = hexGrid.GetTilesOfType(HexType.Grass);
        foreach (Vector2Int coords in grassTileCoords)
        {
            //random chance for decoration placement which can be edited in the inspector
            // it will pick a random decorative prefab , place it slightly above the surface to avoid clipping and lastly mark the tile as occupied
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

    #endregion Grid Generation

    #region Castle Tower System

    // Places the castle at the center of the grid This is the main objective in which the enemies will want to reach
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

    #endregion Castle Tower System

    #region Variant Management

    // Get a random variant of the specified hex type for variety would be used during tile generation for diverse map
    // but in this case we have used standard green due to time constraints.
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

    #endregion Variant Management

    #region Coordinate Conversion

    // Converts the axial hex coords to world. uses standard hexagon grid math for spacing.
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

    #endregion Coordinate Conversion

    public List<Vector2Int> GetSpawnPointCoords() => spawnPointCoords;

    public GameObject GetTileAt(Vector2Int coords) => hexGrid.GetTileAt(coords);

    public GameObject GetTowerInstance() => centralTowerInstance;
}