using UnityEngine;

/// <summary>
/// Decides which city geometry blocks crowd movement (buildings + outer walls only).
/// Trees, lamps, benches, and other props stay passable.
/// </summary>
public static class CrowdEnvironmentClassification
{
    public static bool BlocksCrowdMovement(Collider col)
    {
        if (col == null)
            return false;

        return IsCityBoundaryWall(col.transform) || HasBuildingAncestor(col.transform);
    }

    public static bool ShouldReceiveBuildingCollider(Transform t)
    {
        return HasBuildingAncestor(t);
    }

    public static bool IsPassableProp(Collider col)
    {
        return col != null && !BlocksCrowdMovement(col);
    }

    static bool HasBuildingAncestor(Transform t)
    {
        for (Transform node = t; node != null; node = node.parent)
        {
            if (IsBuildingObjectName(node.name))
                return true;
        }

        return false;
    }

    static bool IsCityBoundaryWall(Transform t)
    {
        bool underCityBoundary = false;

        for (Transform node = t; node != null; node = node.parent)
        {
            if (node.name.IndexOf("City Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0)
                underCityBoundary = true;

            if (!underCityBoundary)
                continue;

            string wallName = node.name.Trim();
            if (wallName == "Wall 1" || wallName == "Wall 2" || wallName == "Wall 3" || wallName == "Wall 4")
                return true;
        }

        return false;
    }

    static bool IsBuildingObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();

        if (lower.Contains("building"))
            return true;
        if (lower.StartsWith("build_g"))
            return true;
        if (lower.StartsWith("shop_") || lower.StartsWith("shop "))
            return true;
        if (lower.Contains("hospital") && !lower.Contains("sign"))
            return true;
        if (lower.Contains("police") && !lower.Contains("sign"))
            return true;
        if (lower.Contains("fire_department") || lower.Contains("fire department"))
            return true;

        return false;
    }
}
