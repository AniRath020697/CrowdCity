using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Expanding ground shock ripple — flattened builtin sphere mesh + HDR emission + point-light flare.
/// </summary>
public class ShockwaveEffect : MonoBehaviour
{
    const float BuiltinSphereApproxRadiusXZ = 0.5f;

    [Header("Animation")]
    public float lifetime = 0.45f;

    [Range(0.02f, 0.5f)]
    public float groundFlatten = 0.095f;

    public float radiusVisualStretch = 1.85f;

    [Tooltip("Used only if spawning code skips Configure.")]
    public float gameplayRadiusFallback = 8f;

    [Header("Tint")]
    [ColorUsage(true, true)] public Color tint = new Color(0.42f, 0.93f, 1f, 0.92f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    float _elapsed;
    float _minRadius;
    float _maxRadius;
    Color _shade;

    MaterialPropertyBlock _mpb;
    Renderer[] _rend;
    Light _pulse;
    bool _initialized;

    void Awake()
    {
        _rend = GetComponentsInChildren<Renderer>(true);

        foreach (var r in _rend)
        {
            if (r == null) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        _mpb = new MaterialPropertyBlock();

        _pulse = gameObject.AddComponent<Light>();
        _pulse.type = LightType.Point;
        _pulse.shadows = LightShadows.None;
        _pulse.enabled = false;
    }

    public void Configure(float gameplayRadius) => Configure(gameplayRadius, tint);

    public void Configure(float gameplayRadius, Color theme)
    {
        gameplayRadius = Mathf.Max(gameplayRadius, 2f);
        lifetime = Mathf.Max(lifetime, 1e-3f);

        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _minRadius = Mathf.Clamp(gameplayRadius * 0.05f, 0.35f, 2f);
        _maxRadius = Mathf.Max(gameplayRadius * radiusVisualStretch, gameplayRadius + 4f);

        if (_pulse != null)
        {
            _pulse.color = Color.Lerp(theme, Color.white, 0.22f);
            _pulse.range = Mathf.Clamp(gameplayRadius * 4f, 14f, 80f);
            _pulse.enabled = true;
            _pulse.intensity = 0f;
        }

        SetScaleXZ(_minRadius);
        Paint(progress: 0f);
        Physics.SyncTransforms();
    }

    void LateUpdate()
    {
        if (!_initialized)
            Configure(gameplayRadiusFallback, tint);

        lifetime = Mathf.Max(lifetime, 1e-3f);
        _elapsed += Time.deltaTime;

        float p = Mathf.Clamp01(_elapsed / lifetime);
        float eased = Mathf.SmoothStep(0f, 1f, p);

        SetScaleXZ(Mathf.Lerp(_minRadius, _maxRadius, eased));
        Paint(p);

        if (_pulse != null && _pulse.enabled)
        {
            float sine = Mathf.Sin(Mathf.Clamp01((p + 0.02f) * 4f) * Mathf.PI);
            float crest = Mathf.Exp(-Mathf.Abs(p - 0.33f) * 17f);
            float flash = Mathf.Clamp(sine * 95f + crest * 128f + 55f * (1f - p * p), 55f, 240f);

            _pulse.intensity = Mathf.Lerp(flash, 0f, eased * eased * eased * eased * eased);
        }

        if (p >= 1f)
            Destroy(gameObject);
    }

    void SetScaleXZ(float worldReachXZDesired)
    {
        float xz = Mathf.Max(worldReachXZDesired / BuiltinSphereApproxRadiusXZ, 0.06f);
        transform.localScale = new Vector3(xz, xz * groundFlatten, xz);
    }

    void Paint(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float fade = Mathf.Pow(1f - progress, 0.92f);

        float sineBurst = Mathf.Sin(Mathf.Clamp01(progress * 3.95f + 0.04f) * Mathf.PI);
        float ringCrest = Mathf.Exp(-Mathf.Abs(progress - 0.32f) * 16f);
        float emission = Mathf.Clamp(sineBurst * 95f + ringCrest * 185f + 30f + (1f - progress) * 40f,
            8f, 260f); // visibly bright even without post-process bloom tweaks

        foreach (Renderer r in _rend)
        {
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);

            Color surface = _shade;
            surface.a = Mathf.Clamp01(_shade.a * fade);

            Color emit = surface;
            emit.r *= emission;
            emit.g *= emission;
            emit.b *= emission;

            emit += Mathf.Max(sineBurst, ringCrest) * Color.white * 14f;

            _mpb.SetColor(BaseColorId, surface);
            _mpb.SetColor(EmissionColorId, emit);
            r.SetPropertyBlock(_mpb);
        }
    }
}
