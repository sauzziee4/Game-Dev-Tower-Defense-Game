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
        new Vector2Int(1, -1),   // 0 NE side
        new Vector2Int(1, 0),  // 1 E side
        new Vector2Int(0, 1),  // 2 SE side
        new Vector2Int(-1, 1),  // 3 SWest side
        new Vector2Int(-1, 0),  // 4 W side
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
    }

    private Vector2Int GetRandomEdgePosition()
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

    private void CreatePathToCastle(Vector2Int start, Vector2Int castle)
    {
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
            Debug.LogError("No Variants for path");
            return;
        }

        List<int> requiredEdges = new List<int>();

        if (directionFrom >= 0)
        {
            requiredEdges.Add((directionFrom + 3) % 6);
        }

        if (directionTo >= 0)
        {
            requiredEdges.Add(directionTo);
        }

        HexVariant chosen = null;
        int bestRotation = 0;
        bool isCornerConnection = false;

        if (directionFrom >= 0 && directionTo >= 0)
        {
            int fromEdge = (directionFrom + 3) % 6;
            int toEdge = directionTo;
            int edgeDiff = Mathf.Abs(fromEdge = toEdge);
            isCornerConnection = (edgeDiff == 1 || edgeDiff == 5);
        }

        foreach (HexVariant variant in variants)
        {
            if (variant.openEdges == null || variant.openEdges.Length == 0) continue;

            bool isCornerPiece = IsCornerPiece(variant);

            if (isCornerConnection && !isCornerPiece && Random.Range(0f, 1f) < 0.7f) continue;
            if (!isCornerConnection && isCornerPiece && Random.Range(0f, 1f) < 0.5f) continue;

            for (int rotation = 0; rotation < 6; rotation++)
            {
                int[] rotatedEdges = new int[variant.openEdges.Length];
                for (int i = 0; i < variant.openEdges.Length; i++)
                {
                    rotatedEdges[i] = (variant.openEdges[i] + rotation) % 6;
                }

                bool canConnect = true;
                foreach (int requiredEdge in requiredEdges)
                {
                    bool hasEdge = false;
                    foreach (int edge in rotatedEdges)
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
                    bestRotation = rotation; break;
                }
            }
            if (chosen != null) break;
        }
        if (chosen == null)
        {
            chosen = variants[Random.Range(0, variants.Count)];
            bestRotation = 0;
            Debug.LogWarning($"Could not find perfect path connection at {coords}, using fallback");
        }

        float rotationAngle = bestRotation * 60f;
        Vector3 pos = HexToWorld(coords);
        GameObject placed = Instantiate(chosen.prefab, pos, Quaternion.Euler(0, rotationAngle, 0), transform);
        placed.name = $"PathTile_{coords.x}_{coords.y}_{(chosen.openEdges.Length == 2 && IsCornerPiece(chosen) ? "Corner" : "Straight")}";
        tileMap[coords] = placed;
    }

    private bool IsCornerPiece(HexVariant variant)
    {
        if (variant.openEdges.Length == 2) return false;

        int edge1 = variant.openEdges[0];
        int edge2 = variant.openEdges[1];
        int diff = Mathf.Abs(edge1 - edge2);

        return (diff == 1 || diff == 5);
    }

    private Vector2Int StepTowards(Vector2Int current, Vector2Int castle)
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

    private int GetDirectionToCastle(Vector2Int current, Vector2Int castle)
    {
        Vector2Int delta = castle - current;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? 0 : 3;
        else
            return delta.y > 0 ? 5 : 2;
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
            Debug.LogError("No variants for " + type);
            return;
        }

        HexVariant chosen = variants[Random.Range(0, variants.Count)];

        Vector3 pos = HexToWorld(coords);
        GameObject placed = Instantiate(chosen.prefab, pos, Quaternion.Euler(0, 0, 0), transform);
        placed.name = $"Tile_{coords.x}_{coords.y}";
        tileMap[coords] = placed;
    }

    private Vector3 HexToWorld(Vector2Int hexCoords)
    {
        float x = Mathf.Sqrt(3f) * (hexCoords.x + hexCoords.y / 2f);
        float z = 1.5f * hexCoords.y;
        return new Vector3(x, 0, z);
    }
}