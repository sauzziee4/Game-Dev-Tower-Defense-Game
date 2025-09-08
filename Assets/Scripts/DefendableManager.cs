using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//a static class to manage all IDendable objects in scene for efficent lookup
public class DefendableManager : MonoBehaviour
{
    public static DefendableManager Instance { get; private set; }
    private List<IDefendable> allDefendables = new List<IDefendable>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //adds a new IDefenable object to manager's list
    public void AddDefendable(IDefendable defendable)
    {
        if (defendable != null && !allDefendables.Contains(defendable))
        {
            allDefendables.Add(defendable);
        }
    }

    //removes IDefendable object from manager's list
    public void RemoveDefendable(IDefendable defendable)
    {
        if (defendable != null)
        {
            allDefendables.Remove(defendable);
        }
    }

    //returns IDefendable object closest to a given position
    //returns closest IDefendable Object, or null if none exist
    public IDefendable GetClosestDefendable(Vector3 position)
    {
        if (allDefendables.Count == 0)
        {
            return null;
        }

        IDefendable closest = null;
        float minDistanceSqr = Mathf.Infinity;

        foreach (var defendable in allDefendables)
        {
            if (defendable is MonoBehaviour defendableBehaviour)
            {
                float distanceSqr = (defendableBehaviour.transform.position - position).sqrMagnitude;
                if (distanceSqr < minDistanceSqr)
                {
                    minDistanceSqr = distanceSqr;
                    closest = defendable;
                }
            }
        }
        return closest;
    }
}