using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public enum HexType { Grass, Path, Hill, River, TowerBase}

[System.Serializable]
public class HexVariant
{
    public GameObject prefab;
    public List<int> openEdges = new List<int>(); //eg {0,3} means straight N-S connection
}


public class HexVariantSet
{
    public HexType hexType;
    public List<HexVariant> variants = new List<HexVariant>();
}

public class HexTile : MonoBehaviour
{
    public HexType hexType;
    public Vector2Int hexCoords; 
    public List<int> openEdges = new List<int>();
}
