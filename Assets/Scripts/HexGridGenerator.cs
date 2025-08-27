using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using JetBrains.Annotations;



public class HexGridGenerator : MonoBehaviour
{
    [Header("Tile Variants")]
    public HexVariantSet[] variantSets;

    [Header("Map Settings")]
    public int gridRadius = 8;
    public int pathCount = 3;

    private Dictionary<HexType, List<HexVariant>> variantDict;
    private Dictionary<Vector2Int, GameObject> tileMap = new Dictionary<Vector2Int, GameObject>();

    // Hex axial directions (0 = East, counter-clockwise)
    private readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(1, 0),   // 0 East
        new Vector2Int(1, -1),  // 1 NE
        new Vector2Int(0, -1),  // 2 NW
        new Vector2Int(-1, 0),  // 3 West
        new Vector2Int(-1, 1),  // 4 SW
        new Vector2Int(0, 1)    // 5 SE
    };

    void Awake()
    {
        variantDict = new Dictionary<HexType, List<HexVariant>>();
        foreach (var set in variantSets)
        {
            if (!variantDict.ContainsKey(set.hexType))
                variantDict[set.hexType] = new List<HexVariant>();
            variantDict[set.hexType].AddRange(set.variants);
        }

        GenerateMap();
    }

    void GenerateMap()
    {
        Vector2Int castlePos = new Vector2Int(0, 0);
        PlaceTile(castlePos, HexType.Castle, -1);

        for (int i = 0; i < pathCount; i++)
        {
            Vector2Int start = GetRandomEdgePosition();
            CreatePathToCastle(start, castlePos);
        }

        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            for (int r = -gridRadius; r <= gridRadius; r++)
            {
                if (Mathf.Abs(q + r) <= gridRadius)
                {
                    Vector2Int coords = new Vector2Int(q, r);
                    if (!tileMap.ContainsKey(coords))
                        PlaceTile(coords, HexType.Grass, -1);
                }
            }
        }
    }

    Vector2Int GetRandomEdgePosition()
    {
        int side = Random.Range(0, 4);
        int offset = Random.Range(-gridRadius, gridRadius + 1);

        switch (side)
        {
            case 0: return new Vector2Int(-gridRadius, offset);
            case 1: return new Vector2Int(gridRadius, offset);
            case 2: return new Vector2Int(offset, -gridRadius);
            default: return new Vector2Int(offset, gridRadius);
        }
    }

    void CreatePathToCastle(Vector2Int start, Vector2Int castle)
    {
        Vector2Int current = start;
        while (current != castle)
        {
            int dir = GetDirectionToCastle(current, castle);
            PlaceTile(current, HexType.Path, dir);
            current = StepTowards(current, castle);
        }
    }

    Vector2Int StepTowards(Vector2Int current, Vector2Int castle)
    {
        int dx = castle.x - current.x;
        int dy = castle.y - current.y;

        if (Mathf.Abs(dx) > Mathf.Abs(dy))
            return new Vector2Int(current.x + (int)Mathf.Sign(dx), current.y);
        else if (dy != 0)
            return new Vector2Int(current.x, current.y + (int)Mathf.Sign(dy));
        else
            return current;
    }

    int GetDirectionToCastle(Vector2Int current, Vector2Int castle)
    {
        Vector2Int delta = castle - current;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? 0 : 3;
        else
            return delta.y > 0 ? 5 : 2;
    }

    void PlaceTile(Vector2Int coords, HexType type, int directionToConnect)
    {
        if (tileMap.ContainsKey(coords)) return;

        if (!variantDict.TryGetValue(type, out var variants) || variants.Count == 0)
        {
            Debug.LogError("No variants for " + type);
            return;
        }

        HexVariant chosen = null;

        if (directionToConnect >= 0)
        {
            int requiredEdge = (directionToConnect + 3) % 6;
            var candidates = variants.Where(v => v.openEdges.Contains(requiredEdge)).ToList();

            if (candidates.Count > 0)
            {
                chosen = candidates[Random.Range(0, candidates.Count)];
            }
        }

        if (chosen == null)
            chosen = variants[Random.Range(0, variants.Count)];

        int prefabEdge = 0; // default value if no open edges
        if (chosen.openEdges != null && chosen.openEdges.Length > 0)
        {
            prefabEdge = chosen.openEdges[0];
        }
        else
        {
            Debug.LogWarning("Chosen tile prefab " + chosen.prefab.name + " has no open edges!");
        }
        int rotationSteps = 0;
        
        if (directionToConnect >= 0)
        {
            rotationSteps = (directionToConnect - prefabEdge + 6) % 6;
        }
        
        float rotationAngle = rotationSteps * 60f;

        Vector3 pos = HexToWorld(coords);
        Instantiate(chosen.prefab, pos, Quaternion.Euler(0, rotationAngle, 0), transform);

        tileMap[coords] = chosen.prefab != null ? chosen.prefab : new GameObject("Tile");
    }

    Vector3 HexToWorld(Vector2Int hexCoords)
    {
        float x = Mathf.Sqrt(3f) * (hexCoords.x + hexCoords.y / 2f);
        float z = 1.5f * hexCoords.y;
        return new Vector3(x, 0, z);
    }
}
