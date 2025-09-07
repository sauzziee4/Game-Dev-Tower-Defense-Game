using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public enum HexType
{
    Grass,
    Path,
    Castle
}

[System.Serializable]
public class HexVariant
{
    public GameObject prefab;
    public Material material; // Added for visual feedback
    public HexType hexType;
    public int[] openEdges;
    //public List<int> openEdges = new List<int>(); //eg {0,3} means straight N-S connection
}

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

    private void Awake()
    {
        tileRenderer = GetComponent<Renderer>();

        // Store original material
        if (tileRenderer != null && originalMaterial == null)
        {
            originalMaterial = tileRenderer.material;
        }
    }

    private void Start()
    {
        // If variant is set and has a material, use it
        if (variant != null && variant.material != null && tileRenderer != null)
        {
            tileRenderer.material = variant.material;
            originalMaterial = variant.material;
        }

        // Apply rotation if set
        if (rotation != 0)
        {
            transform.rotation = Quaternion.Euler(0, rotation, 0);
        }
    }

    public void SetVariant(HexVariant newVariant)
    {
        variant = newVariant;

        if (variant != null && variant.material != null && tileRenderer != null)
        {
            tileRenderer.material = variant.material;
            originalMaterial = variant.material;
        }
    }

    public void SetRotation(int newRotation)
    {
        rotation = newRotation;
        transform.rotation = Quaternion.Euler(0, rotation, 0);
    }

    public void SetCoordinates(Vector2Int coords)
    {
        coordinates = coords;
        gameObject.name = $"HexTile_{coords.x}_{coords.y}_{variant?.hexType}";
    }

    public bool CanPlaceTurret()
    {
        // Can only place turrets on grass tiles that aren't occupied
        return variant != null &&
               variant.hexType == HexType.Grass &&
               !isOccupied;
    }

    public void SetOccupied(GameObject occupier)
    {
        isOccupied = true;
        occupyingObject = occupier;
    }

    public void SetUnoccupied()
    {
        isOccupied = false;
        occupyingObject = null;
    }

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
                // Create a temporary highlight effect by modifying color
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

    public void SetHighlightColor(Color color)
    {
        if (tileRenderer == null || originalMaterial == null) return;

        Material tempMaterial = new Material(originalMaterial);
        tempMaterial.color = color;
        tileRenderer.material = tempMaterial;
        isHighlighted = true;
    }

    public void ResetMaterial()
    {
        if (tileRenderer != null && originalMaterial != null)
        {
            tileRenderer.material = originalMaterial;
            isHighlighted = false;
        }
    }

    // Edge connection checking methods (for your pathfinding system)
    public bool HasOpenEdge(int edgeIndex)
    {
        if (variant == null || variant.openEdges == null) return false;

        // Adjust for rotation
        int rotatedEdge = (edgeIndex - (rotation / 60)) % 6;
        if (rotatedEdge < 0) rotatedEdge += 6;

        foreach (int edge in variant.openEdges)
        {
            if (edge == rotatedEdge) return true;
        }
        return false;
    }

    public int[] GetOpenEdges()
    {
        if (variant == null || variant.openEdges == null) return new int[0];

        // Apply rotation to open edges
        int[] rotatedEdges = new int[variant.openEdges.Length];
        for (int i = 0; i < variant.openEdges.Length; i++)
        {
            rotatedEdges[i] = (variant.openEdges[i] + (rotation / 60)) % 6;
        }
        return rotatedEdges;
    }

    // Mouse interaction events for easier debugging and interaction
    private void OnMouseEnter()
    {
        // Optional: Add hover effects or debugging info
        // Debug.Log($"Hovering over {variant?.hexType} tile at {coordinates}");
    }

    private void OnMouseExit()
    {
        // Optional: Remove hover effects
    }

    private void OnMouseDown()
    {
        // Optional: Handle direct tile clicking
        // Debug.Log($"Clicked on {variant?.hexType} tile at {coordinates}");
    }

    // Public properties for easy access
    public HexType TileType => variant?.hexType ?? HexType.Grass;
    public bool IsPath => variant?.hexType == HexType.Path;
    public bool IsGrass => variant?.hexType == HexType.Grass;
    public bool IsCastle => variant?.hexType == HexType.Castle;
    public Vector2Int Coordinates => coordinates;
    public bool IsHighlighted => isHighlighted;
}
