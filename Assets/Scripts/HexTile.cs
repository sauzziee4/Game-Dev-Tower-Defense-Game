using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

//defines the different types of hex tiles available in  game
// used for the gameplay logic when generating the map and visual implementaion.
public enum HexType
{
    Grass, // open green terrain where turrets can be placed as well as decorations
    Path, // the tiles in which enemies can walk on but decorations and turrents cannot be placed here
    Castle // special structure at the center of the map.
}

//Represents a specific variant of the hex tile with differences in visual and gameplay aspects/properties

[System.Serializable]
public class HexVariant
{
    public GameObject prefab;
    public Material material;
    public HexType hexType;
    public int[] openEdges;
}

// groups up multiple variants of the same hex type together
//allows for visual variety within the same functionailty of the tile type.
// this was implemented due to how the paths were previously set up, this has been changed but for the potential in future parts, the code has remained.
[System.Serializable]
public class HexVariantSet
{
    public HexType hexType;
    public HexVariant[] variants;
}

public class HexTile : MonoBehaviour
{
    [Header("Original Properties")]
    public HexVariant variant;

    public int rotation;

    [Header("Grid Properties")]
    public Vector2Int coordinates;

    [Header("Occupancy")]
    public bool isOccupied = false;

    public GameObject occupyingObject;

    [Header("Visual Feedback")]
    public Material originalMaterial;

    public Material highlightMaterial;

    private Renderer tileRenderer;
    private bool isHighlighted = false;

    #region Unity

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();

        //stores the origional material if not manually aassigned
        //this is used for the highlighted hex when placing turrets.
        if (tileRenderer != null && originalMaterial == null)
        {
            originalMaterial = tileRenderer.material;
        }
    }

    //applys variant material and rotation after all components are initialized
    private void Start()
    {
        if (variant != null && variant.material != null && tileRenderer != null)
        {
            tileRenderer.material = variant.material;
            originalMaterial = variant.material;
        }

        if (rotation != 0)
        {
            transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
    }

    #endregion Unity

    #region Variant Management

    //name organization
    public void SetCoordinates(Vector2Int coords)
    {
        coordinates = coords;
        gameObject.name = $"HexTile_{coords.x}_{coords.y}_{variant?.hexType}";
    }

    #endregion Variant Management

    #region Tile Occupancy Systems

    //Checks if turret can be placed on this tile
    //it will only allow for the turrets ot be placed on grass tiles that arent occupied
    public bool CanPlaceTurret()
    {
        // only place turrets on grass tiles that aren't occupied
        return variant != null &&
               variant.hexType == HexType.Grass &&
               !isOccupied;
    }

    // Marks the tile as occupied
    public void SetOccupied(GameObject occupier)
    {
        isOccupied = true;
        occupyingObject = occupier;
    }

    //Unmarks the tile as occupied. This is used when tiles are destroyed.

    public void SetUnoccupied()
    {
        isOccupied = false;
        occupyingObject = null;
    }

    #endregion Tile Occupancy Systems

    #region Visual Feedback

    //toggles highlights state for feedback when placing towers
    //it will show valid or invalid placements for players.
    public void Highlight(bool highlight)
    {
        if (tileRenderer == null) return;

        if (highlight && !isHighlighted)
        {
            if (highlightMaterial != null)
            {
                tileRenderer.material = highlightMaterial;
            }
            else
            {
                Material tempMaterial = new Material(originalMaterial);
                tempMaterial.color = Color.yellow;
                tileRenderer.material = tempMaterial;
            }
            isHighlighted = true;
        }
        else if (!highlight && isHighlighted)
        {
            tileRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    #endregion Visual Feedback

    public HexType TileType => variant?.hexType ?? HexType.Grass;
    public bool IsPath => variant?.hexType == HexType.Path;
    public bool IsGrass => variant?.hexType == HexType.Grass;
    public bool IsCastle => variant?.hexType == HexType.Castle;
    public Vector2Int Coordinates => coordinates;
    public bool IsHighlighted => isHighlighted;
}