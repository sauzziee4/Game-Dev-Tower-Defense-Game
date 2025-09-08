using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Pathfinder : MonoBehaviour
{
    private HexGrid hexGrid;

    [Header("Path Spacing Settings")]
    [SerializeField]
    private int minPathDistance = 3;

    private readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1),
        new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, -1)
    };

    // Store the generated paths for enemy navigation
    private List<List<Vector2Int>> generatedPaths = new List<List<Vector2Int>>();

    private void Awake()
    {
        hexGrid = GetComponent<HexGrid>();
    }

    //uses A* pathfining algorithm to calculate routes and ensure paths are reasonably spaced out
    public List<Vector2Int> GeneratePathsToCenter(int gridRadius, int pathCount, HexGridGenerator hexGridGenerator)
    {
        //stores first coordinate of each path, becomes enemy spawn points
        List<Vector2Int> spawnPointCoords = new List<Vector2Int>();
        List<Vector2Int> usedStartPoints = new List<Vector2Int>();
        Vector2Int center = Vector2Int.zero;
        generatedPaths.Clear();

        int attempts = 0;
        int maxAttempts = pathCount * 10;

        //loops runs until path is succesfully created correct number of paths or runs out of attempts
        while (spawnPointCoords.Count < pathCount && attempts < maxAttempts)
        {
            attempts++;
            Vector2Int startCoords = FindRandomEdgeTile(gridRadius, usedStartPoints);

            //if pathfinder cant find valid starting point
            if (startCoords == Vector2Int.zero && usedStartPoints.Count > 0)
            {
                Debug.LogWarning($"Could not find enough valid spawn points. Generated {spawnPointCoords.Count} paths.");
                break; //exit the while loop
            }

            //A* pathfinding
            //calculate most direct route from start coords to center
            List<Vector2Int> path = FindPath(startCoords, center, gridRadius);

            if (path != null && path.Count > 0)
            {
                usedStartPoints.Add(startCoords);
                generatedPaths.Add(new List<Vector2Int>(path)); // Store the complete path

                //loop through every coords in calculated path
                foreach (Vector2Int coords in path)
                {
                    hexGridGenerator.SpawnHex(coords, HexType.Path);
                }
                spawnPointCoords.Add(path[0]); // First coordinate is spawn point
            }
        }

        Debug.Log($"Generated {generatedPaths.Count} paths with spawn points: {string.Join(", ", spawnPointCoords)}");
        return spawnPointCoords;
    }

    // Get a specific path by spawn point coordinate
    public List<Vector2Int> GetPathFromSpawnPoint(Vector2Int spawnPoint)
    {
        foreach (var path in generatedPaths)
        {
            if (path.Count > 0 && path[0] == spawnPoint)
            {
                return new List<Vector2Int>(path);
            }
        }
        Debug.LogWarning($"No path found for spawn point {spawnPoint}");
        return new List<Vector2Int>();
    }

    // Get all generated paths
    public List<List<Vector2Int>> GetAllPaths()
    {
        return new List<List<Vector2Int>>(generatedPaths);
    }

    //finds random tile on outer edge of grid that's a minimum distance away from previous start points
    private Vector2Int FindRandomEdgeTile(int radius, List<Vector2Int> usedStartPoints)
    {
        List<Vector2Int> allCoords = new List<Vector2Int>();
        //generate every possible coord within grid radius
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                allCoords.Add(new Vector2Int(q, r));
            }
        }

        //filter list to only get tiles on the edge of the map
        List<Vector2Int> edgeTiles = allCoords.Where(coords => HexDistance(coords, Vector2Int.zero) == radius).ToList();

        //if this path is the first, any adge tile is valid
        if (usedStartPoints.Count == 0 && edgeTiles.Count > 0)
        {
            return edgeTiles[UnityEngine.Random.Range(0, edgeTiles.Count)];
        }

        //filter to find edge tiles that are good distance from points already in use
        List<Vector2Int> validEdgeTiles = edgeTiles.Where(edgeTile =>
            usedStartPoints.All(usedPoint => HexDistance(edgeTile, usedPoint) >= minPathDistance)
        ).ToList();

        //pick random tile from valid, spaced out list
        if (validEdgeTiles.Count > 0)
        {
            return validEdgeTiles[UnityEngine.Random.Range(0, validEdgeTiles.Count)];
        }
        return Vector2Int.zero;
    }

    //calculate shortest path between two points using A*
    public List<Vector2Int> FindPath(Vector2Int startCoords, Vector2Int endCoords, int gridRadius)
    {
        //openSet for nodes to visit, cameFrom to trace path back
        var openSet = new PriorityQueue<Vector2Int>();
        openSet.Enqueue(startCoords, 0);

        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { { startCoords, 0 } };
        var fScore = new Dictionary<Vector2Int, float> { { startCoords, HexDistance(startCoords, endCoords) } };

        //while there are nodes to check
        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current.Equals(endCoords))
            {
                return ReconstructPath(cameFrom, current);
            }

            //check all neighbors of current node
            foreach (var neighbor in GetNeighbors(current, gridRadius))
            {
                //calculate potential cost to move
                float tentativeGScore = gScore.ContainsKey(current) ? gScore[current] + 1 : 1;

                //if better path, record it
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

    //traces path backward from end node to start
    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        //path built in reverse, flip it to correct order
        path.Reverse();
        return path;
    }

    //gets all valid, adjacent hex neighbors for given coord
    private List<Vector2Int> GetNeighbors(Vector2Int coords, int gridRadius)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        //check in all 6 hex directions
        foreach (var direction in HexDirections)
        {
            Vector2Int neighbor = coords + direction;
            //only add neighbor if it's inside grid boundaries
            if (HexDistance(neighbor, Vector2Int.zero) <= gridRadius)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }

    //calculates distance between 2 hex coords using axial coord system formula
    private float HexDistance(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dz = Mathf.Abs(-a.x - a.y - (-b.x - b.y));
        return (dx + dy + dz) / 2;
    }

    //priority queue implemntation for A*
    private class PriorityQueue<T> where T : IEquatable<T>
    {
        private List<(T item, float priority)> elements = new List<(T, float)>();
        public int Count => elements.Count;

        public void Enqueue(T item, float priority)
        {
            elements.Add((item, priority));
            //sorts list so cheapest is always at the start of list
            elements.Sort((a, b) => a.priority.CompareTo(b.priority));
        }

        public T Dequeue()
        {
            //gets cheapest item from start of list
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