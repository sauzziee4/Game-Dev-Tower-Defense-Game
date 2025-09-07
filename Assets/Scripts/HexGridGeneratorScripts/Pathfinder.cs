using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Pathfinder : MonoBehaviour
{
    private HexGrid hexGrid;

    // Hex axial directions (0 = East, counter-clockwise)
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
    }

    // This method finds a path using a simple breadth-first search.
    public List<Vector2Int> FindPath(Vector2Int startCoords, Vector2Int endCoords, int gridRadius)
    {
        if (hexGrid.GetTileAt(startCoords) == null || hexGrid.GetTileAt(endCoords) == null)
        {
            Debug.LogWarning("Pathfinding failed: Start or end tile does not exist.");
            return null;
        }

        var openSet = new PriorityQueue<Vector2Int>();
        openSet.Enqueue(startCoords, 0);

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        gScore[startCoords] = 0;

        var fScore = new Dictionary<Vector2Int, float>();
        fScore[startCoords] = HexDistance(startCoords, endCoords);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current.Equals(endCoords))
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                float tentativeGScore = gScore.ContainsKey(current) ? gScore[current] + 1 : 1;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HexDistance(neighbor, endCoords);
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
        }
        return null;
    }

    public List<GameObject> GeneratePathsToCenter(int gridRadius, int pathCount, HexGridGenerator hexGridGenerator)
    {
        List<GameObject> spawnPoints = new List<GameObject>();
        Vector2Int center = Vector2Int.zero;

        for (int i = 0; i < pathCount; i++)
        {
            Vector2Int startCoords = GetRandomOuterHex(gridRadius);
            List<Vector2Int> path = FindPath(startCoords, center, gridRadius);

            if (path != null)
            {
                bool firstTile = true;
                foreach (var coords in path)
                {
                    hexGridGenerator.SpawnHex(coords, HexType.Path);
                    if (firstTile)
                    {
                        // Create a spawn point at the start of the path.
                        Vector3 spawnPosition = hexGridGenerator.HexToWorld(coords);
                        GameObject spawnPoint = new GameObject("SpawnPoint");
                        spawnPoint.transform.position = spawnPosition;
                        spawnPoint.transform.parent = hexGridGenerator.transform;
                        spawnPoints.Add(spawnPoint);
                        firstTile = false;
                    }
                }
            }
        }
        return spawnPoints;
    }

    private Vector2Int GetRandomOuterHex(int gridRadius)
    {
        int side = UnityEngine.Random.Range(0, 6);
        int offset = UnityEngine.Random.Range(0, gridRadius);
        Vector2Int coords = Vector2Int.zero;

        switch (side)
        {
            case 0: // E
                coords = new Vector2Int(gridRadius, -offset);
                break;

            case 1: // SE
                coords = new Vector2Int(gridRadius - offset, -gridRadius);
                break;

            case 2: // SW
                coords = new Vector2Int(-offset, -gridRadius + offset);
                break;

            case 3: // W
                coords = new Vector2Int(-gridRadius, offset);
                break;

            case 4: // NW
                coords = new Vector2Int(-gridRadius + offset, gridRadius);
                break;

            case 5: // NE
                coords = new Vector2Int(offset, gridRadius - offset);
                break;
        }
        return coords;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> totalPath = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            totalPath.Add(current);
        }
        totalPath.Reverse();
        return totalPath;
    }

    private List<Vector2Int> GetNeighbors(Vector2Int coords)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        foreach (var direction in HexDirections)
        {
            Vector2Int neighbor = coords + direction;
            if (hexGrid.GetTileAt(neighbor) != null)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    private float HexDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dz = Mathf.Abs(a.x + a.y - b.x - b.y);
        return (dx + dy + dz) / 2.0f;
    }

    private class PriorityQueue<T> where T : IEquatable<T>
    {
        private List<(T item, float priority)> elements = new List<(T, float)>();
        public int Count => elements.Count;

        public void Enqueue(T item, float priority)
        {
            elements.Add((item, priority));
            elements.Sort((a, b) => a.priority.CompareTo(b.priority));
        }

        public T Dequeue()
        {
            T bestItem = elements[0].item;
            elements.RemoveAt(0);
            return bestItem;
        }

        public bool Contains(T item)
        {
            return elements.Any(e => e.item.Equals(item));
        }
    }
}