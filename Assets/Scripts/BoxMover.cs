using UnityEngine;

public class BoxMover : MonoBehaviour
{
    public float speed = 2f;
    public Transform pickupPoint;

    bool isMoving = true;

    void Update()
    {
        if (!isMoving || pickupPoint == null) return;

        transform.Translate(Vector3.right * speed * Time.deltaTime, Space.World);

        if (transform.position.x >= pickupPoint.position.x)
        {
            isMoving = false;
            Vector3 pos = transform.position;
            pos.x = pickupPoint.position.x;
            transform.position = pos;

            Debug.Log("[Box] " + name + " reached pickup point");

            if (RobotArmController.Instance != null)
                RobotArmController.Instance.NotifyBoxReady(gameObject);
            else
                Debug.LogWarning("[Box] RobotArmController.Instance is null!");
        }
    }
}
