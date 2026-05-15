using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared helpers for sliding crowd movement along a baked NavMesh (no NavMeshAgent required).
/// Bake the NavMesh in the editor (Nav Mesh Surface) or use <see cref="NavMeshSceneBootstrap"/> at runtime.
/// </summary>
public static class CrowdNavMeshMovement
{
    static bool _availabilityChecked;
    static bool _hasNavMesh;

    public static void RefreshAvailability()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        _hasNavMesh = tri.indices != null && tri.indices.Length > 0;
        _availabilityChecked = true;
    }

    public static bool HasNavMeshData()
    {
        if (!_availabilityChecked)
            RefreshAvailability();
        return _hasNavMesh;
    }

    public static void InvalidateAvailability()
    {
        _availabilityChecked = false;
    }

    public static bool TrySampleOnNavMesh(Vector3 worldPos, float sampleRadius, int areaMask, out Vector3 onMesh)
    {
        onMesh = worldPos;

        if (!_availabilityChecked)
            RefreshAvailability();
        if (!_hasNavMesh)
            return false;

        Vector3 probe = new Vector3(worldPos.x, worldPos.y, worldPos.z);
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, sampleRadius, areaMask))
            return false;

        onMesh = hit.position;
        return true;
    }

    /// <summary>
    /// Horizontal slide along NavMesh edges. Height is preserved; street filtering blocks stepping onto roof/prop NavMesh.
    /// </summary>
    public static bool TryMove(
        Vector3 fromWorld,
        Vector3 toWorld,
        float sampleRadius,
        out Vector3 result,
        bool preserveHeight = true,
        float streetReferenceY = float.NaN,
        float maxHeightAboveStreet = 1.35f,
        int areaMask = NavMesh.AllAreas)
    {
        result = toWorld;
        float keepY = fromWorld.y;

        if (!_availabilityChecked)
            RefreshAvailability();
        if (!_hasNavMesh)
            return false;

        float sampleY = float.IsNaN(streetReferenceY) ? fromWorld.y : streetReferenceY;
        Vector3 fromProbe = new Vector3(fromWorld.x, sampleY, fromWorld.z);
        Vector3 toProbe = new Vector3(toWorld.x, sampleY, toWorld.z);

        if (!NavMesh.SamplePosition(fromProbe, out NavMeshHit fromHit, sampleRadius, areaMask))
            return false;

        if (!IsSampleOnStreet(fromHit.position, streetReferenceY, maxHeightAboveStreet))
            return false;

        Vector3 from = fromHit.position;
        if (preserveHeight)
            from.y = keepY;

        Vector3 toFlat = toWorld;
        toFlat.y = from.y;

        if (NavMesh.Raycast(from, toFlat, out NavMeshHit rayHit, areaMask))
        {
            result = rayHit.position;
            if (preserveHeight)
                result.y = keepY;

            if (!IsDestinationNavAllowed(result, streetReferenceY, maxHeightAboveStreet, sampleRadius, areaMask))
                return false;

            return true;
        }

        if (NavMesh.SamplePosition(toProbe, out NavMeshHit toHit, sampleRadius, areaMask))
        {
            result = toHit.position;
            if (preserveHeight)
                result.y = keepY;

            if (!IsDestinationNavAllowed(result, streetReferenceY, maxHeightAboveStreet, sampleRadius, areaMask))
                return false;

            return true;
        }

        result = toWorld;
        if (preserveHeight)
            result.y = keepY;
        return true;
    }

    static bool IsSampleOnStreet(Vector3 pos, float streetReferenceY, float maxHeightAboveStreet)
    {
        if (float.IsNaN(streetReferenceY))
            return true;

        return pos.y <= streetReferenceY + maxHeightAboveStreet;
    }

    static bool IsDestinationNavAllowed(
        Vector3 result,
        float streetReferenceY,
        float maxHeightAboveStreet,
        float sampleRadius,
        int areaMask)
    {
        if (float.IsNaN(streetReferenceY))
            return true;

        Vector3 probe = new Vector3(result.x, streetReferenceY, result.z);
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, sampleRadius, areaMask))
            return true;

        if (hit.position.y <= streetReferenceY + maxHeightAboveStreet)
            return true;

        return false;
    }
}
