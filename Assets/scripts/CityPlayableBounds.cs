using UnityEngine;

/// <summary>
/// Playable area inside the walled city (Wall 1–4 under City Boundary).
/// Clamps spawns and crowd movement so nothing appears outside the walls.
/// </summary>
public class CityPlayableBounds : MonoBehaviour
{
    public static CityPlayableBounds Instance { get; private set; }

    [Tooltip("Parent object that holds Wall 1–4. Auto-finds \"City Boundary\" if empty.")]
    public Transform boundaryRoot;

    [Tooltip("Shrink interior from the outer wall box so spawns stay off the walls.")]
    public float innerPadding = 6f;

    [Tooltip("Extra inset for player/enemy leaders near the boundary edge.")]
    public float leaderEdgePadding = 2f;

    Bounds _outerBounds;
    Bounds _interiorBounds;
    bool _isValid;

    public bool IsValid => _isValid;

    /// <summary>Axis-aligned box around Wall 1–4 before inner padding.</summary>
    public Bounds OuterBounds => _outerBounds;

    public Bounds InteriorBounds => _interiorBounds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        RebuildBounds();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Rebuild Bounds")]
    public void RebuildBounds()
    {
        _isValid = false;

        if (boundaryRoot == null)
        {
            GameObject root = GameObject.Find("City Boundary");
            if (root != null)
                boundaryRoot = root.transform;
        }

        if (boundaryRoot == null)
            return;

        Bounds outer = default;
        bool hasOuter = false;

        for (int i = 0; i < boundaryRoot.childCount; i++)
        {
            Transform wall = boundaryRoot.GetChild(i);
            if (!IsBoundaryWall(wall.name))
                continue;

            if (TryEncapsulateColliderBounds(wall, ref outer, ref hasOuter))
                continue;

            TryEncapsulateRendererBounds(wall, ref outer, ref hasOuter);
        }

        if (!hasOuter)
            return;

        _outerBounds = outer;
        _interiorBounds = outer;
        _interiorBounds.Expand(-innerPadding);

        if (_interiorBounds.size.x < 8f || _interiorBounds.size.z < 8f)
            return;

        _isValid = true;
    }

    static bool IsBoundaryWall(string wallName)
    {
        string n = wallName.Trim();
        return n == "Wall 1" || n == "Wall 2" || n == "Wall 3" || n == "Wall 4";
    }

    static bool TryEncapsulateColliderBounds(Transform wall, ref Bounds outer, ref bool hasOuter)
    {
        Collider col = wall.GetComponent<Collider>();
        if (col == null)
            return false;

        if (!hasOuter)
        {
            outer = col.bounds;
            hasOuter = true;
        }
        else
        {
            outer.Encapsulate(col.bounds);
        }

        return true;
    }

    static void TryEncapsulateRendererBounds(Transform wall, ref Bounds outer, ref bool hasOuter)
    {
        Renderer r = wall.GetComponent<Renderer>();
        if (r == null)
            return;

        if (!hasOuter)
        {
            outer = r.bounds;
            hasOuter = true;
        }
        else
        {
            outer.Encapsulate(r.bounds);
        }
    }

    public bool ContainsXZ(Vector3 worldPos)
    {
        if (!_isValid) return true;

        return worldPos.x >= _interiorBounds.min.x && worldPos.x <= _interiorBounds.max.x
            && worldPos.z >= _interiorBounds.min.z && worldPos.z <= _interiorBounds.max.z;
    }

    public Vector3 ClampXZ(Vector3 worldPos, float extraInset = 0f)
    {
        if (!_isValid)
            return worldPos;

        float minX = _interiorBounds.min.x + extraInset;
        float maxX = _interiorBounds.max.x - extraInset;
        float minZ = _interiorBounds.min.z + extraInset;
        float maxZ = _interiorBounds.max.z - extraInset;

        worldPos.x = Mathf.Clamp(worldPos.x, minX, maxX);
        worldPos.z = Mathf.Clamp(worldPos.z, minZ, maxZ);
        return worldPos;
    }

    public Vector3 GetRandomPointXZ()
    {
        if (!_isValid)
            return Vector3.zero;

        float x = Random.Range(_interiorBounds.min.x, _interiorBounds.max.x);
        float z = Random.Range(_interiorBounds.min.z, _interiorBounds.max.z);
        return new Vector3(x, 0f, z);
    }
}
