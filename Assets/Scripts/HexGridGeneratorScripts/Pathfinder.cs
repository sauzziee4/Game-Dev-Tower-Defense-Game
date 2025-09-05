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
        new Vector2Int(1, -1), // 0 NE side
        new Vector2Int(1, 0),  // 1 E side
        new Vector2Int(0, 1),  // 2 SE side
        new Vector2Int(-1, 1), // 3 SW side
        new Vector2Int(-1, 0), // 4 W side
        new Vector2Int(0, -1)  // 5 NW side
    };

    private void Awake()
    {
        hexGrid = GetComponent<HexGrid>();
    }

    /// Finds a path between a start and end coordinate using the A* algorithm. startCoords = The starting hex
    /// coordinates. endCoords = The target hex coordinates. gridRadius = The radius of the hex grid to constrain the
    /// search. returns The list of hex coordinates representing the path.
    public List<Vector2Int> FindPath(Vector2Int startCoords, Vector2Int endCoords, int gridRadius)
    {
        if (hexGrid.GetTileAt(startCoords) == null || hexGrid.GetTileAt(endCoords) == null)
        {
            Debug.LogWarning("Pathfinding failed: Start or end tile does not exist.");
            return null;
        }

        // A* algorithm implementation
        var frontier = new PriorityQueue<Vector2Int>();
        frontier.Enqueue(startCoords, 0);

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var costSoFar = new Dictionary<Vector2Int, float>();

        cameFrom[startCoords] = startCoords;
        costSoFar[startCoords] = 0;

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            if (current == endCoords)
            {
                break;
            }

            foreach (var next in GetNeighbors(current))
            {
                // Assuming all terrain types have a cost of 1 for simplicity
                float newCost = costSoFar[current] + 1;
                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    float priority = newCost + HexDistance(endCoords, next);
                    frontier.Enqueue(next, priority);
                    cameFrom[next] = current;
                }
            }
        }

        // Reconstruct path
        if (!cameFrom.ContainsKey(endCoords))
        {
            Debug.LogWarning("Pathfinding failed: No path found.");
            return null;
        }

        var path = new List<Vector2Int>();
        Vector2Int currentPathPoint = endCoords;
        while (currentPathPoint != startCoords)
        {
            path.Add(currentPathPoint);
            currentPathPoint = cameFrom[currentPathPoint];
        }
        path.Add(startCoords);
        path.Reverse();
        return path;
    }

    /// Gets the valid neighbors for a given hex coordinate. coords = The hex coordinates to check. returns A list of
    /// valid neighbor coordinates.
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

    /// Calculates the distance between two hex coordinates. a = The first hex coordinate. b = The second hex
    /// coordinate. returns The hex distance.
    private float HexDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dz = Mathf.Abs(-a.x - a.y - (-b.x - b.y));
        return (dx + dy + dz) / 2;
    }

    // A simple Priority Queue for the A* algorithm.
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
            if (Count == 0)
            {
                throw new InvalidOperationException("Queue is empty.");
            }
            var item = elements[0].item;
            elements.RemoveAt(0);
            return item;
        }
    }
}