using UnityEngine;
using UnityEngine.UI;

public class TowerHealthBar : MonoBehaviour
{
    public Tower tower;
    public Image healthFill; 

    private void Update()
    {
        if (tower != null && healthFill != null)
        {
            float fillAmount = Mathf.Clamp01(tower.health / tower.maxHealth);
            healthFill.fillAmount = fillAmount;
        }
    }
}