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

    public List<GameObject> GeneratePathsToCenter(int gridRadius, int pathCount, HexGridGenerator hexGridGenerator)
    {
        List<GameObject> spawnPoints = new List<GameObject>();
        Vector2Int center = Vector2Int.zero;

        for (int i = 0; i < pathCount; i++)
        {
            Vector2Int startCoords = FindRandomEdgeTile(gridRadius);
            List<Vector2Int> path = FindPath(startCoords, center, gridRadius);

            if (path != null)
            {
                foreach (Vector2Int coords in path)
                {
                    // This is the CRUCIAL change: call SpawnHex to change the tile type.
                    hexGridGenerator.SpawnHex(coords, HexType.Path);
                }

                // Add the first tile of the path as a spawn point
                GameObject spawnPointTile = hexGrid.GetTileAt(path[0]);
                if (spawnPointTile != null)
                {
                    spawnPoints.Add(spawnPointTile);
                }
            }
        }
        return spawnPoints;
    }

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
        var gScore = new Dictionary<Vector2Int, float> { { startCoords, 0 } };
        var fScore = new Dictionary<Vector2Int, float> { { startCoords, HexDistance(startCoords, endCoords) } };

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current.Equals(endCoords))
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current, gridRadius))
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

    // Finds a random tile on the edge of the grid.
    private Vector2Int FindRandomEdgeTile(int radius)
    {
        // Get all tile coordinates
        List<Vector2Int> allCoords = new List<Vector2Int>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                allCoords.Add(new Vector2Int(q, r));
            }
        }

        // Filter for edge tiles
        List<Vector2Int> edgeTiles = allCoords.Where(coords =>
            HexDistance(coords, Vector2Int.zero) == radius).ToList();

        if (edgeTiles.Count > 0)
        {
            return edgeTiles[UnityEngine.Random.Range(0, edgeTiles.Count)];
        }
        return Vector2Int.zero;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private List<Vector2Int> GetNeighbors(Vector2Int coords, int gridRadius)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        foreach (var direction in HexDirections)
        {
            Vector2Int neighbor = coords + direction;
            // Check if the neighbor is within the grid bounds.
            if (HexDistance(neighbor, Vector2Int.zero) <= gridRadius)
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
        int dz = Mathf.Abs(-a.x - a.y - (-b.x - b.y));
        return (dx + dy + dz) / 2;
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