using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    //main data structure for storing hex tiles
    private Dictionary<Vector2Int, GameObject> tileMap = new Dictionary<Vector2Int, GameObject>();

    public void AddTile(Vector2Int coords, GameObject tile)
    {
        tileMap[coords] = tile;
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

    // Removes a tile from the dictionary.
    public void RemoveTile(Vector2Int coords)
    {
        if (tileMap.ContainsKey(coords))
        {
            tileMap.Remove(coords);
        }
    }

    // returns a list of all tiles in the grid returns a list of all tile GameObjects
    public List<GameObject> GetAllTiles()
    {
        return tileMap.Values.ToList();
    }

    // Clears the grid, removing all tiles
    public void ClearGrid()
    {
        foreach (var tile in tileMap.Values)
        {
            Destroy(tile);
        }
        tileMap.Clear();
    }

    public List<Vector2Int> GetTilesOfType(HexType type)
    {
        List<Vector2Int> tilesOfType = new List<Vector2Int>();

        foreach (var kvp in tileMap)
        {
            GameObject tile = kvp.Value;
            string tileName = tile.name;

            //defines different hex types (grass, path and castle)
            switch (type)
            {
                case HexType.Grass:

                    if (tileName.EndsWith("_Grass"))
                        tilesOfType.Add(kvp.Key);
                    break;

                case HexType.Path:

                    if (tileName.EndsWith("_Path"))
                        tilesOfType.Add(kvp.Key);
                    break;

                case HexType.Castle:

                    if (tileName.EndsWith("_Castle"))
                        tilesOfType.Add(kvp.Key);
                    break;
            }
        }

        return tilesOfType;
    }

    //retrieves new dictionary containing only tiles that match a specific hexType
    public Dictionary<Vector2Int, GameObject> GetTileMapOfType(HexType type)
    {
        Dictionary<Vector2Int, GameObject> tilesOfType = new Dictionary<Vector2Int, GameObject>();
        //loop through every coord and gameObject pair in main tile map
        foreach (var kvp in tileMap)
        {
            GameObject tile = kvp.Value;
            HexTile hexTile = tile.GetComponent<HexTile>();

            if (hexTile != null && hexTile.variant != null && hexTile.variant.hexType == type)
            {
                tilesOfType.Add(kvp.Key, tile);
            }
        }
        return tilesOfType;
    }
}