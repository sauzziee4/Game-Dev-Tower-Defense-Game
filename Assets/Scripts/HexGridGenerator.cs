using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

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

    // An implementation of the A* pathfinding algorithm for the hex grid.
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        // A* Pathfinding implementation

        // Heuristic function (Manhattan distance for hex grid)
        Func<Vector2Int, Vector2Int, float> heuristic = (a, b) =>
            (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.x + a.y - b.x - b.y)) / 2f;

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();
        var openSet = new PriorityQueue<Vector2Int>();

        gScore[start] = 0;
        fScore[start] = heuristic(start, goal);
        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == goal)
            {
                var path = new List<Vector2Int>();
                while (cameFrom.ContainsKey(current))
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Reverse();
                return path;
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                var tentativeGScore = gScore[current] + 1; // Assuming uniform cost

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + heuristic(neighbor, goal);
                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
        }

        // No path found
        return new List<Vector2Int>();
    }

    // Gets the world position of a hex coordinate.
    public Vector3 GetHexPosition(Vector2Int coords)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * coords.x + Mathf.Sqrt(3f) / 2f * coords.y);
        float z = hexSize * (3f / 2f * coords.y);
        return new Vector3(x, 0, z);
    }

    // Returns a list of hex tiles of a specific type.
    public List<Vector2Int> GetTilesOfType(HexType type)
    {
        List<Vector2Int> tileOfType = new List<Vector2Int>();

        foreach (var kvp in tileMap)
        {
            GameObject tile = kvp.Value;
            string tileName = tile.name;

            switch (type)
            {
                case HexType.Grass:
                    if (tileName.Contains("Tile_") && !tileName.Contains("PathTile_") && !tileName.Contains("Castle"))
                        tileOfType.Add(kvp.Key);
                    break;

                case HexType.Path:
                    if (tileName.Contains("PathTile_"))
                        tileOfType.Add(kvp.Key);
                    break;

                case HexType.Castle:
                    if (tileName.Contains("Castle"))
                        tileOfType.Add(kvp.Key);
                    break;
            }
        }
        return tileOfType;
    }

    // Returns a list of neighboring hex coordinates for a given coordinate.
    public List<Vector2Int> GetNeighbors(Vector2Int hexCoords)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        for (int i = 0; i < HexDirections.Length; i++)
        {
            Vector2Int neighbor = hexCoords + HexDirections[i];
            if (IsValidHexCoordinate(neighbor) && tileMap.ContainsKey(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    // Checks if a given hex coordinate is valid.
    public bool IsValidHexCoordinate(Vector2Int coords)
    {
        return Mathf.Abs(coords.x) <= gridRadius &&
            Mathf.Abs(coords.y) <= gridRadius &&
            Mathf.Abs(coords.x + coords.y) <= gridRadius;
    }

    // Public method to get spawn points
    public List<GameObject> GetSpawnPoints()
    {
        return spawnPoints;
    }

    // A simple Priority Queue for the A* algorithm.
    public class PriorityQueue<T> where T : IEquatable<T>
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
            if (Count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }
            var item = elements[0].item;
            elements.RemoveAt(0);
            return item;
        }

        public bool Contains(T item)
        {
            return elements.Any(e => e.item.Equals(item));
        }
    }
}