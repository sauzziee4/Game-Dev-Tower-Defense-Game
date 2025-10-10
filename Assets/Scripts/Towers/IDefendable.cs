using UnityEngine;

//interface for any object that can be attacked by an enemy
public interface IDefendable
{
    float health { get; set; }

    void TakeDamage(float damage);

    Vector3 transform_position { get; }
}