using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;



public class HexGridGenerator : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridRadius = 10; //how far from the center

    [Header("Variants")]
    public List<HexVariantSet> variantSets = new List<HexVariantSet>();

    [Header("Paths")]
    public int numPaths = 3;

    private Dictionary<Vector2Int, GameObject> grid = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<HexType, List<HexVariant>> variantDict = new Dictionary<HexType, List<HexVariant>>();

    private Vector2Int towerCoords = Vector2Int.zero;
    private float hexSize = 1f;

    //axial directials
    private static readonly Vector2Int[] hexDirections = new Vector2Int[]
    {
        new Vector2Int(1,0),   // 0
        new Vector2Int(1,-1),  // 1
        new Vector2Int(0,-1),  // 2
        new Vector2Int(-1,0),  // 3
        new Vector2Int(-1,1),  // 4
        new Vector2Int(0,1)    // 5
    };

    private void Awake()
    {
        BuildVariantDictionary();

    }

    private void Start()
    {
        GenerateGrid();
        GeneratePaths();
        PlaceTower();
    }
    private void BuildVariantDictionary()
    {
        variantDict.Clear();
        if (variantSets == null) return;

        foreach (var set in variantSets)
        {
            if(set == null) continue;
            if (!variantDict.TryGetValue(set.hexType, out var list))
            {
                list = new List<HexVariant>();
                variantDict.Add(set.hexType , list);
            }

            if(set.variants != null)
                list.AddRange(set.variants.Where(v => v != null ));
        }

        if (!variantDict.ContainsKey(HexType.Grass))
            Debug.LogWarning("No grass variants assigned in variantSet. Assign at least one grass variant");

    }
    void GenerateGrid()
    {
        foreach (int q in System.Linq.Enumerable.Range(-gridRadius, gridRadius + 1))
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Abs(q + r) < gridRadius) continue;

                Vector3 pos = HexToWorld(q, r);
                GameObject tile = PlaceTile(new Vector2Int(q, r), HexType.Grass, new List<int>());
                grid[new Vector2Int(q, r)] = tile;
            }
        }
    }

    void GeneratePaths()
    {
        for (int i = 0; i < numPaths; i++)
        {
            Vector2Int start = GetRandomEdgeTile();
            Vector2Int current = start;
            Vector2Int prev = start;

            while (current != towerCoords)
            {
                Vector2Int next = StepTowardsTower(current);
                
                if (grid.ContainsKey(current))
                    Destroy(grid[current]);

                PlacePathTile(prev, current, next);

                prev = current;
                current = next;
            }
        }
    }

    void PlaceTower()
    {
        if (grid.ContainsKey(towerCoords))
            Destroy(grid[towerCoords]);
        GameObject tile = PlaceTile(towerCoords, HexType.TowerBase, new List<int>());
        grid[towerCoords] = tile;
    }
    // ------------------------ Tile Placement -----------------------

    GameObject PlaceTile(Vector2Int coords, HexType type, List<int> requiredConnections)
    {
        if (!variantDict.ContainsKey(type)) return null;

        foreach (var variant in variantDict[type])
        {
            for (int rot = 0; rot < 6; rot++)
            {
                List<int> rotatedEdges = RotateEdges(variant.openEdges, rot);
                if(EdgesMatch(rotatedEdges, requiredConnections))
                {
                    Vector3 pos = HexToWorld(coords.x, coords.y);
                    GameObject tile = Instantiate(variant.prefab, pos, Quaternion.Euler(0, 60 * rot, 0), transform);

                    HexTile ht = tile.GetComponent<HexTile>();
                    ht.hexCoords = coords;
                    ht.hexType = type;
                    ht.openEdges = rotatedEdges;

                    return tile;
                }
            }
        }
        //fallback: just drop grass
        return Instantiate(variantDict[HexType.Grass][0].prefab, HexToWorld(coords.x , coords.y), Quaternion.identity, transform);
    }

    void PlacePathTile(Vector2Int prev, Vector2Int current, Vector2Int next)
    {
        int incomingDir = GetDirection(current, prev);
        int outgoingDir = GetDirection(current, next);  

        List<int> req = new() { incomingDir, outgoingDir };
        GameObject tile = PlaceTile(current, HexType.Path, req);
        grid[current] = tile;
    }
    // --------------------- Helpers ------------------------
    Vector3 HexToWorld(int q, int r)
    {
        float x = hexSize * (Mathf.Sqrt(3) * q + Mathf.Sqrt(3) / 2 * r);
        float z = hexSize * (3f / 2 * r);
        return new Vector3(x, 0,z) ;
    }
    Vector2Int GetRandomEdgeTile()
    {
        int q = Random.Range(-gridRadius, gridRadius + 1);
        int r = Random.Range(-gridRadius, gridRadius + 1);

        while (Mathf.Abs(q + r) > gridRadius)
        {
            q = Random.Range(-gridRadius, gridRadius + 1);
            r = Random.Range(-gridRadius, gridRadius + 1);
        }

        if (Mathf.Abs(q) < gridRadius / 2 && Mathf.Abs(r) < gridRadius / 2)
            return GetRandomEdgeTile();

        return new Vector2Int(q,r);    
    }

    Vector2Int StepTowardsTower(Vector2Int current)
    {
        int dq = towerCoords.x - current.x;
        int dr = towerCoords.y - current.y;

        if (Mathf.Abs(dq) > Mathf.Abs(dr))
            current.x += (dq > 0 ? 1 : -1);
        else
            current.y += (dr > 0 ? 1 : -1);

        return current;
    }

    List<int> RotateEdges (List<int> edges, int steps)
    {
        List<int> result = new();
        foreach (int e in edges) 
            result.Add((e + steps) % 6);
        return result;
    }

    bool EdgesMatch(List<int> candidate, List<int> required)
    {
        foreach (int r in required)
            if (!candidate.Contains(r)) return false;
        return true;
    }

    int GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        for(int i = 0; i < 6; i++) 
            if (hexDirections[i] == diff) return i;
        return -1;
    }

}
