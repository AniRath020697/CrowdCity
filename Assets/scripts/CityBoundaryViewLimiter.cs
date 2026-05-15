using UnityEngine;

/// <summary>
/// Hides empty space beyond the city walls: camera clamp, backdrop panels, and optional fog.
/// </summary>
public class CityBoundaryViewLimiter : MonoBehaviour
{
    [Tooltip("Keep the follow camera inside the walled area so players see less void past the edges.")]
    public bool clampCameraToCity = true;

    [Tooltip("Inset (metres) applied when clamping the camera XZ position.")]
    public float cameraBoundsInset = 22f;

    [Tooltip("Spawn tall panels just outside Wall 1–4 to block the view past the skyline.")]
    public bool spawnBoundaryBackdrops = true;

    [Tooltip("Distance outside the outer wall box for backdrop placement.")]
    public float backdropWallOffset = 4f;

    [Tooltip("Height of each backdrop panel.")]
    public float backdropHeight = 48f;

    [Tooltip("Extra length added to each backdrop along the wall axis.")]
    public float backdropLengthPadding = 8f;

    public Color backdropColor = new Color(0.11f, 0.14f, 0.19f, 1f);

    [Tooltip("Softens distant views (sky / void) when enabled.")]
    public bool useBoundaryFog = true;

    public Color fogColor = new Color(0.55f, 0.62f, 0.72f, 1f);

    [Range(20f, 200f)]
    public float fogStart = 55f;

    [Range(40f, 300f)]
    public float fogEnd = 130f;

    GameObject _backdropRoot;
    bool _fogApplied;

    public static CityBoundaryViewLimiter Ensure(Transform player)
    {
        CityBoundaryViewLimiter existing = FindFirstObjectByType<CityBoundaryViewLimiter>();
        if (existing != null)
        {
            existing.Apply(player);
            return existing;
        }

        CrowdManager crowd = CrowdManager.Instance;
        GameObject host = crowd != null ? crowd.gameObject : new GameObject("CityBoundaryViewLimiter");
        CityBoundaryViewLimiter limiter = host.GetComponent<CityBoundaryViewLimiter>();
        if (limiter == null)
            limiter = host.AddComponent<CityBoundaryViewLimiter>();

        limiter.Apply(player);
        return limiter;
    }

    public void Apply(Transform player)
    {
        CityPlayableBounds bounds = CityPlayableBounds.Instance;
        if (bounds == null || !bounds.IsValid)
        {
            CityPlayableBounds found = FindFirstObjectByType<CityPlayableBounds>();
            if (found != null)
                found.RebuildBounds();
            bounds = CityPlayableBounds.Instance;
        }

        EnsureCameraFollow(player);

        if (spawnBoundaryBackdrops && bounds != null && bounds.IsValid)
            BuildBackdrops(bounds.OuterBounds);
        else
            ClearBackdrops();

        if (useBoundaryFog)
            ApplyFog();
    }

    void EnsureCameraFollow(Transform player)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        if (player != null)
            follow.player = player;

        follow.clampToCityBounds = clampCameraToCity;
        follow.cityBoundsInset = cameraBoundsInset;
    }

    void BuildBackdrops(Bounds outer)
    {
        ClearBackdrops();

        _backdropRoot = new GameObject("CityBoundaryBackdrop");
        _backdropRoot.transform.SetParent(null);

        float groundY = CrowdManager.Instance != null
            ? CrowdManager.Instance.GetStreetReferenceY()
            : outer.center.y;
        float panelY = groundY + backdropHeight * 0.5f;

        float widthX = outer.size.x + backdropLengthPadding;
        float widthZ = outer.size.z + backdropLengthPadding;

        CreateBackdropPanel(
            "BackdropSouth",
            new Vector3(outer.center.x, panelY, outer.min.z - backdropWallOffset),
            new Vector3(widthX, backdropHeight, 1f),
            Quaternion.identity);

        CreateBackdropPanel(
            "BackdropNorth",
            new Vector3(outer.center.x, panelY, outer.max.z + backdropWallOffset),
            new Vector3(widthX, backdropHeight, 1f),
            Quaternion.Euler(0f, 180f, 0f));

        CreateBackdropPanel(
            "BackdropWest",
            new Vector3(outer.min.x - backdropWallOffset, panelY, outer.center.z),
            new Vector3(widthZ, backdropHeight, 1f),
            Quaternion.Euler(0f, 90f, 0f));

        CreateBackdropPanel(
            "BackdropEast",
            new Vector3(outer.max.x + backdropWallOffset, panelY, outer.center.z),
            new Vector3(widthZ, backdropHeight, 1f),
            Quaternion.Euler(0f, -90f, 0f));
    }

    void CreateBackdropPanel(string panelName, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = panelName;
        panel.transform.SetParent(_backdropRoot.transform, false);
        panel.transform.position = position;
        panel.transform.rotation = rotation;
        panel.transform.localScale = scale;

        Collider col = panel.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateBackdropMaterial();
    }

    Material CreateBackdropMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", backdropColor);
        else
            mat.color = backdropColor;

        return mat;
    }

    void ClearBackdrops()
    {
        if (_backdropRoot != null)
        {
            Destroy(_backdropRoot);
            _backdropRoot = null;
        }
    }

    void ApplyFog()
    {
        if (_fogApplied)
            return;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        _fogApplied = true;
    }

    void OnDestroy()
    {
        ClearBackdrops();
    }
}
