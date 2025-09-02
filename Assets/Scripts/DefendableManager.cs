using UnityEngine;
using System.Collections.Generic;
using System.Linq;

///a static class to manage all IDendable objects in scene for efficent lookup
public static class DefendableManager
{
    private static List<IDefendable> allDefendables = new List<IDefendable>();

    //adds a new IDefenable object to manager's list
    //"defendable" is the object to add
    public static void AddDefendable(IDefendable defendable)
    {
        if (!allDefendables.Contains(defendable))
        {
            allDefendables.Add(defendable);
        }
    }

    //removes IDefendable object from manager's list
    //"defendable" is the object to remove
    public static void RemoveDefendable(IDefendable defendable)
    {
        allDefendables.Remove(defendable);
    }

    //returns IDefendable object closest to a given position
    //"position" the position to check from
    // returns closest IDefendable Object, or null if none exist
    public static IDefendable GetClosestDefendable(Vector3 position)
    {
        if (allDefendables.Count == 0)
        {
            return null;
        }

        return allDefendables
            .OrderBy(d => Vector3.Distance((d as MonoBehaviour).transform.position, position))
            .FirstOrDefault();
    }
}