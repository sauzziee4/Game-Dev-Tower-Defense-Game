using UnityEngine;

// Handles the movement and collision of a projectile
public class Projectile : MonoBehaviour
{
    private Enemy targetEnemy;
    private IDefendable targetDefendable;
    private float projectileDamage;
    private float projectileSpeed;

    // Sets the target and stats for the projectile
    //"target" is the enemy or defendable object to target
    //"damage" is the damage the projectile will inflict
    //"speed" is the speed of the projectile
    public void SetTarget(Enemy target, float damage, float speed)
    {
        this.targetEnemy = target;
        this.projectileDamage = damage;
        this.projectileSpeed = speed;
    }

    // Sets the target and stats for the projectile.
    //"target">The enemy or defendable object to target
    //"damage">The damage the projectile will inflict
    //"speed">The speed of the projectile
    public void SetTarget(IDefendable target, float damage, float speed)
    {
        this.targetDefendable = target;
        this.projectileDamage = damage;
        this.projectileSpeed = speed;
    }

    private void Update()
    {
        if (targetEnemy != null)
        {
            // Move towards the enemy position
            Vector3 direction = (targetEnemy.transform.position - transform.position).normalized;
            transform.position += direction * projectileSpeed * Time.deltaTime;

            // Check if the projectile has hit the target
            if (Vector3.Distance(transform.position, targetEnemy.transform.position) < 0.5f)
            {
                targetEnemy.TakeDamage(projectileDamage);
                Destroy(gameObject);
            }
        }
        else if (targetDefendable != null)
        {
            // Move towards the defendable's position
            Vector3 direction = (targetDefendable.transform_position - transform.position).normalized;
            transform.position += direction * projectileSpeed * Time.deltaTime;

            // Check if the projectile has hit the target
            if (Vector3.Distance(transform.position, targetDefendable.transform_position) < 0.5f)
            {
                targetDefendable.TakeDamage(projectileDamage);
                Destroy(gameObject);
            }
        }
        else
        {
            // If there's no target, destroy projectile after a short time
            Destroy(gameObject, 3f);
        }
    }
}