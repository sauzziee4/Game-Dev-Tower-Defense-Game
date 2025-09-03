using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using JetBrains.Annotations;
using Unity.VisualScripting;

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

    private Dictionary<HexType, List<HexVariant>> variantDict;
    private Dictionary<Vector2Int, GameObject> tileMap = new Dictionary<Vector2Int, GameObject>();
    private List<GameObject> spawnPoints = new List<GameObject>();

    // Hex axial directions (0 = East, counter-clockwise)
    private readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(1, -1),   // 0 NE side
        new Vector2Int(1, 0),    // 1 E side
        new Vector2Int(0, 1),    // 2 SE side
        new Vector2Int(-1, 1),   // 3 SW side
        new Vector2Int(-1, 0),   // 4 W side
        new Vector2Int(0, -1)    // 5 NW side
    };

    private void Awake()
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

    private void GenerateMap()
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

        GenerateDecorations();
    }

    private Vector2Int GetRandomEdgePosition()
    {
        List<Vector2Int> edgePositions = new List<Vector2Int>();
        for(int q = -gridRadius; q <= gridRadius;q++)
        {
            for(int r = -gridRadius; r <= gridRadius;r++)
            {
                if (Mathf.Abs(q + r) <= gridRadius)
                {
                    Vector2Int pos = new Vector2Int(q,r);

                    if(IsEdgePosition(pos))
                    {
                        edgePositions.Add(pos);
                    }
                }
            }
        }

        if(edgePositions.Count > 0)
        {
            return edgePositions[Random.Range(0, edgePositions.Count)];
        }

        return new Vector2Int(-gridRadius, 0);
    }
    private bool IsEdgePosition(Vector2Int pos)
    {
        int q = pos.x;
        int r = pos.y;
        int s = -q - r;

        return Mathf.Abs(q) == gridRadius || Mathf.Abs(r) == gridRadius || Mathf.Abs(s) == gridRadius;
    }
    private void CreatePathToCastle(Vector2Int start, Vector2Int castle)
    {

        CreateSpawnPoint(start);
        Vector2Int current = start;
        Vector2Int previous = start;
        int currentDirection = -1;

        while (current != castle)
        {
            Vector2Int next;
            int nextDirection;

            if (Random.Range(0f, 1f) < 0.3f && Vector2.Distance(current, castle) > 2)
            {
                next = StepTowardsWithVariation(current, castle, currentDirection);
                nextDirection = GetHexDirection(current, next);
            }
            else
            {
                next = StepTowards(current, castle);
                nextDirection = GetHexDirection(current, next);
            }

            int directionFromPrevious = -1;

            if (current != start)
            {
                directionFromPrevious = GetHexDirection(previous, current);
            }

            PlacePathTile(current, directionFromPrevious, nextDirection);

            previous = current;
            current = next;
            currentDirection = nextDirection;
        }
    }
    private void CreateSpawnPoint(Vector2Int coords)
    {
        GameObject spawnPoint = new GameObject("spawnPoint");

        Vector3 worldPos = HexToWorld(coords, false);
        worldPos.y = spawnPointHeight;
        spawnPoint.transform.position = worldPos;

        spawnPoint.transform.SetParent(this.transform);

        spawnPoints.Add(spawnPoint);
        Debug.Log($"Spawn point created at hex coords {coords} (world pos {worldPos})");

    }

    public List<GameObject> GetSpawnPoints()
    {
        return new List<GameObject>(spawnPoints);
    }


    private Vector2Int StepTowardsWithVariation(Vector2Int current, Vector2Int castle, int avoidDirection)
    {
        Vector2Int directStep = StepTowards(current, castle);
        int directDirection = GetHexDirection(current, directStep);

        int[] adjacentDirs =
        {
            (directDirection + 5) % 6,
            (directDirection + 1) % 6
        };

        foreach (int dir in adjacentDirs)
        {
            if (dir == avoidDirection) continue;
            Vector2Int candidate = current + HexDirections[dir];

            float currentDist = Vector2Int.Distance(current, castle);
            float candidateDist = Vector2Int.Distance(candidate, castle);

            if (candidateDist <= currentDist + 1)
            {
                return candidate;
            }
        }
        return directStep;
    }

    private int GetHexDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;

        for (int i = 0; i < HexDirections.Length; i++)
        {
            if (HexDirections[i] == delta)
            {
                return i;
            }
        }
        return 0;
    }

    private void PlacePathTile(Vector2Int coords, int directionFrom, int directionTo)
    {
        if (tileMap.ContainsKey(coords)) return;

        int hexDistance = Mathf.Abs(coords.x) + Mathf.Abs(coords.y) + Mathf.Abs(coords.x + coords.y);
        if (hexDistance / 2 > gridRadius)
            return;

        if (!variantDict.TryGetValue(HexType.Path, out List<HexVariant> variants) || variants.Count == 0)
        {
            return;
        }

        List<int> requiredEdges = GetRequiredEdgesFromNeighbors(coords, directionFrom, directionTo);

        HexVariant chosen = null;
        int bestRotation = 0;

        
        foreach (HexVariant variant in variants)
        {
            if (variant.openEdges == null || variant.openEdges.Length == 0) continue;

            
            if (variant.openEdges.Length != 2) continue;

            int edge1 = variant.openEdges[0];
            int edge2 = variant.openEdges[1];
            int edgeDiff = Mathf.Abs(edge1 - edge2);

            
            if (edgeDiff != 3) continue;

            
            bool canConnect = true;
            foreach (int requiredEdge in requiredEdges)
            {
                bool hasEdge = false;
                foreach (int edge in variant.openEdges)
                {
                    if (edge == requiredEdge)
                    {
                        hasEdge = true;
                        break;
                    }
                }
                if (!hasEdge)
                {
                    canConnect = false;
                    break;
                }
            }

            if (canConnect)
            {
                chosen = variant;
                bestRotation = 0; 
            }
        }

        if (chosen == null)
        {
            chosen = variants[Random.Range(0, variants.Count)];
            bestRotation = 0;
        }

        float rotationAngle = bestRotation * 60f;
        Vector3 pos = HexToWorld(coords, true); 
        GameObject placed = Instantiate(chosen.prefab, pos, Quaternion.Euler(0, rotationAngle, 0), transform);

        
        int[] actualEdges = chosen.openEdges;

        
        string tileType = "Straight";
        if (actualEdges.Length == 2)
        {
            int edge1 = actualEdges[0];
            int edge2 = actualEdges[1];
            int edgeDiff = Mathf.Abs(edge1 - edge2);

            if (edgeDiff == 3)
            {
                tileType = "Straight";
            }
            else
            {
                tileType = $"Invalid_{edgeDiff}";
            }
        }
        else
        {
            tileType = $"Invalid_{actualEdges.Length}Edge";
        }

        
        string edgeList = string.Join("-", actualEdges);
        placed.name = $"PathTile_{coords.x}_{coords.y}_{tileType}_Edges[{edgeList}]";

        tileMap[coords] = placed;

        
        StoreTileConnectionData(placed, chosen, 0);
    }

    private List<int> GetRequiredEdgesFromNeighbors(Vector2Int coords, int directionFrom, int directionTo)
    {
        List<int> required = new List<int>();

        
        if (directionFrom >= 0)
        {
            int fromEdge = (directionFrom + 3) % 6; 
            required.Add(fromEdge);
        }
        if (directionTo >= 0)
        {
            required.Add(directionTo);
        }

        
        for (int dir = 0; dir < 6; dir++)
        {
            Vector2Int neighborCoords = coords + HexDirections[dir];
            if (tileMap.TryGetValue(neighborCoords, out GameObject neighbor))
            {
                if (NeighborHasEdgePointingToUs(neighbor, (dir + 3) % 6))
                {
                    if (!required.Contains(dir))
                    {
                        required.Add(dir);
                    }
                }
            }
        }

        return required;
    }

    private bool NeighborHasEdgePointingToUs(GameObject neighborTile, int expectedEdge)
    {
        HexTile hexTile = neighborTile.GetComponent<HexTile>();
        if (hexTile == null)
        {
            return false;
        }

        if (hexTile.variant == null || hexTile.variant.openEdges == null)
        {
            return false;
        }

        
        foreach (int edge in hexTile.variant.openEdges)
        {
            if (edge == expectedEdge)
            {
                return true;
            }
        }

        return false;
    }

    private void StoreTileConnectionData(GameObject tile, HexVariant variant, int rotation)
    {
        HexTile hexTile = tile.GetComponent<HexTile>();
        if (hexTile == null)
            hexTile = tile.AddComponent<HexTile>();

        
        hexTile.variant = variant;
        hexTile.rotation = rotation;
    }

    private Vector2Int StepTowards(Vector2Int current, Vector2Int castle)
    {
        Vector2Int delta = castle - current;

        
        int bestDirection = 0;
        float bestDistance = float.MaxValue;

        for (int dir = 0; dir < HexDirections.Length; dir++)
        {
            Vector2Int candidate = current + HexDirections[dir];
            float distance = Vector2.Distance(candidate, castle);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestDirection = dir;
            }
        }

        return current + HexDirections[bestDirection];
    }

    private void PlaceTile(Vector2Int coords, HexType type, int directionToConnect)
    {
        if (type == HexType.Path)
        {
            PlacePathTile(coords, directionToConnect, -1);
            return;
        }

        if (tileMap.ContainsKey(coords)) return;

        int hexDistance = Mathf.Abs(coords.x) + Mathf.Abs(coords.y) + Mathf.Abs(coords.x + coords.y);
        if (hexDistance / 2 > gridRadius)
            return;

        if (!variantDict.TryGetValue(type, out List<HexVariant> variants) || variants.Count == 0)
        {
            return;
        }

        HexVariant chosen = variants[Random.Range(0, variants.Count)];

        Vector3 pos = HexToWorld(coords, false); 
        GameObject placed = Instantiate(chosen.prefab, pos, Quaternion.Euler(0, 0, 0), transform);
        placed.name = $"Tile_{coords.x}_{coords.y}";
        tileMap[coords] = placed;
    }

    private Vector3 HexToWorld(Vector2Int hexCoords, bool isPath = false)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * hexCoords.x + Mathf.Sqrt(3f) / 2f * hexCoords.y);
        float z = hexSize * (3f / 2f * hexCoords.y);
        float y = isPath ? 0.01f : 0f;
        return new Vector3(x, y, z);
    }

    private void GenerateDecorations()
    {
        if (decorationPrefabs == null || decorationPrefabs.Length == 0) return;

        foreach (var kvp in tileMap)
        {
            Vector2Int coords = kvp.Key;
            GameObject tile = kvp.Value;

            if (IsGrassTile(tile))
            {
                if(Random.Range(0f,1f) < decorationChance)
                {
                    PlaceDecoration(coords, tile);
                }
            }
        } 
            
    }

    private bool IsGrassTile(GameObject tile)
    {
        return tile.name.Contains("Tile_") && !tile.name.Contains("PathTile_");
    }

    private void PlaceDecoration(Vector2Int coords, GameObject grassTile)
    {
        GameObject decorationPrefab = decorationPrefabs[Random.Range(0, decorationPrefabs.Length)];

        Vector3 decorationPos = HexToWorld(coords, false);
        decorationPos.y += decorationHeightOffset;

        float maxOffset = hexSize * 0.3f;
        float randomX = Random.Range(-maxOffset, maxOffset);
        float randomZ = Random.Range(-maxOffset, maxOffset);
        decorationPos.x += randomX;
        decorationPos.z += randomZ;

        float randomRotation = Random.Range(0f, 360f);

        GameObject decoration = Instantiate(decorationPrefab, decorationPos, Quaternion.Euler(0, randomRotation, 0), grassTile.transform);

        float scaleVariation = Random.Range(0.8f, 1.2f);
        decoration.transform.localScale *= scaleVariation;

        decoration.name = $"Decoration_{coords.x}_{coords.y}_{decorationPrefab.name}";
    }

    public Vector3 HexToWorldPublic(Vector2Int hexCoords, bool isPath = false)
    {
        return HexToWorld(hexCoords, isPath);
    }

    public GameObject GetTileAt(Vector2Int hexCoords)
    {
        tileMap.TryGetValue(hexCoords, out GameObject tile);
        return tile;
    }

    public bool IsValidHexCoordinate(Vector2Int coords)
    {
        return Mathf.Abs(coords.x) <= gridRadius &&
            Mathf.Abs(coords.y) <= gridRadius &&
            Mathf.Abs(coords.x + coords.y) <= gridRadius;
    }

    public List<Vector2Int> GetTilesOfType(HexType type)
    {
        List<Vector2Int> tileOfType = new List<Vector2Int>();

        foreach (var kvp in tileMap)
        {
            GameObject tile = kvp.Value;
            string tileName = tile.name;

            switch(type)
            {
                case HexType.Grass:
                    if(tileName.Contains("Tile_")&& !tileName.Contains("PathTile_")&& !tileName.Contains("Castle")) 
                        tileOfType.Add(kvp.Key); 
                    break;
                case HexType.Path:
                    if(tileName.Contains("PathTile_"))
                        tileOfType.Add(kvp.Key);
                    break;
                case HexType.Castle:
                    if(tileName.Contains("Castle"))
                        tileOfType.Add(kvp.Key);
                    break;
            }
        }
        return tileOfType;
    }

    public List<Vector2Int> GetNeighbors(Vector2Int hexCoords)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        for(int i = 0; i < HexDirections.Length; i++)
        {
            Vector2Int neighbor = hexCoords + HexDirections[i];
            if(IsValidHexCoordinate(neighbor) && tileMap.ContainsKey(neighbor))
            {
                neighbors.Add(neighbor);
            }
              
        }

        return neighbors;
    }
}