using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public float height = 25f;
    public float backOffset = 6f;
    public float smoothSpeed = 5f;

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 targetPos = new Vector3(
            player.position.x,
            height,
            player.position.z - backOffset
        );

        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(60f, 0f, 0f);
    }
}
