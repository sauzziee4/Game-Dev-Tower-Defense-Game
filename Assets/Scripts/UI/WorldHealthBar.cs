using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    public Image fillImage;
    public float healthBarYOffset = 2f;
    private Camera mainCamera;
    private IDefendable target;
    private Canvas canvas;

    private void Start()
    {
        mainCamera = Camera.main;
        target = GetComponentInParent<IDefendable>();
        canvas = GetComponentInChildren<Canvas>();

        if (canvas != null && mainCamera != null)
        {
            canvas.worldCamera = mainCamera;
        }

        if (fillImage != null)
        {
            fillImage.color = Color.green;
        }
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector3 worldPos = target.transform_position + new Vector3(0, healthBarYOffset, 0);
            transform.position = worldPos;
        }

        //make health bar face the camera
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }

        //update fill amount
        if (fillImage != null && target != null)
        {
            fillImage.fillAmount = Mathf.Clamp01(target.health / target.maxHealth);
        }
    }
}
