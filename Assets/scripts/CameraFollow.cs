using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public float height = 25f;
    public float backOffset = 6f;
    public float smoothSpeed = 5f;

    [Header("City boundary")]
    [Tooltip("Keeps the camera XZ inside the walled city so less empty space shows past the outer walls.")]
    public bool clampToCityBounds = true;

    [Tooltip("Metres to pull the camera in from the outer wall box.")]
    public float cityBoundsInset = 22f;

    void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 targetPos = BuildFollowPosition();
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(60f, 0f, 0f);
    }

    /// <summary>Call after wave teleports so the player is not off-screen while the camera catches up.</summary>
    public void SnapToPlayerImmediately()
    {
        if (player == null)
            return;

        transform.position = BuildFollowPosition();
        transform.rotation = Quaternion.Euler(60f, 0f, 0f);
    }

    Vector3 BuildFollowPosition()
    {
        Vector3 targetPos = new Vector3(
            player.position.x,
            height,
            player.position.z - backOffset);

        if (clampToCityBounds)
            targetPos = ClampToCity(targetPos);

        return targetPos;
    }

    Vector3 ClampToCity(Vector3 cameraPos)
    {
        CityPlayableBounds bounds = CityPlayableBounds.Instance;
        if (bounds == null || !bounds.IsValid)
            return cameraPos;

        return bounds.ClampXZ(cameraPos, cityBoundsInset);
    }
}
