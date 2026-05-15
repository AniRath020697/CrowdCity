using UnityEngine;

/// <summary>
/// Blocks horizontal crowd movement against buildings and city boundary walls only.
/// Crowd characters use kinematic triggers, so physics does not stop them — this does.
/// </summary>
public static class CrowdObstacleMovement
{
    const float SkinWidth = 0.05f;

    public static bool TryBlockHorizontalMove(
        Vector3 fromWorld,
        Vector3 toWorld,
        float radius,
        float height,
        LayerMask layers,
        Transform ignoreRoot,
        out Vector3 result)
    {
        result = toWorld;

        Vector3 flatDelta = toWorld - fromWorld;
        flatDelta.y = 0f;
        float distance = flatDelta.magnitude;
        if (distance < 0.0001f)
            return true;

        Vector3 dir = flatDelta / distance;
        float capsuleRadius = Mathf.Max(0.15f, radius);
        float capsuleHeight = Mathf.Max(capsuleRadius * 2.1f, height);
        float cylinderLength = Mathf.Max(0.01f, capsuleHeight - capsuleRadius * 2f);

        Vector3 feet = new Vector3(fromWorld.x, fromWorld.y, fromWorld.z);
        Vector3 pBottom = feet + Vector3.up * capsuleRadius;
        Vector3 pTop = feet + Vector3.up * (capsuleRadius + cylinderLength);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            pBottom,
            pTop,
            capsuleRadius,
            dir,
            distance,
            layers,
            QueryTriggerInteraction.Ignore);

        float allowed = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;
            if (CrowdManager.ShouldIgnoreForMovementBlock(hit.collider, ignoreRoot))
                continue;

            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - SkinWidth));
        }

        result = feet + dir * allowed;
        result.y = toWorld.y;
        return true;
    }
}
