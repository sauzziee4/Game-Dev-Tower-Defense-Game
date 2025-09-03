using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public enum HexType { Grass, Path, Castle}

[System.Serializable]
public class HexVariant
{
    public GameObject prefab;
    //public List<int> openEdges = new List<int>(); //eg {0,3} means straight N-S connection
    
    public int[] openEdges;
    
}

[System.Serializable]
public class HexVariantSet
{
    public HexType hexType;
    public HexVariant[] variants;
}

public class HexTile : MonoBehaviour
{
    public HexVariant variant;
    public int rotation;
}