using UnityEngine;

public interface IUpgradeable
{
    int UpgradeLevel { get; }
    public float GetUpgradeCost();
    void UpgradeTower();
    
}

