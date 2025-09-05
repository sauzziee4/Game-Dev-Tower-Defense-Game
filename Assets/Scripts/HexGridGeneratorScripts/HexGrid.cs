using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    //main data structure for storing hex tiles
    private Dictionary<Vector2Int, GameObject> tileMap = new Dictionary<Vector2Int, GameObject>();

    //ADDS TITLE TO HEX GRID INTERNAL DICTIONARY
    public void AddTile(Vector2Int coords, GameObject tile)
    {
        if (!tileMap.ContainsKey(coords))
        {
            tileMap.Add(coords, tile);
        }
    }

    // gets tile GameObject at a given coordinate.
    public GameObject GetTileAt(Vector2Int coords)
    {
        if (tileMap.TryGetValue(coords, out GameObject tile))
        {
            return tile;
        }
        return null;
    }

    // returns a list of all tiles in the grid returns A list of all tile GameObjects
    public List<GameObject> GetAllTiles()
    {
        return tileMap.Values.ToList();
    }

    // Clears the grid, removing all tiles
    public void ClearGrid()
    {
        tileMap.Clear();
    }
}