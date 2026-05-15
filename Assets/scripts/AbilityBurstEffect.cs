using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// HDR burst VFX for dash streaks, rally rings, and recruit beams. Uses the same mesh setup as ShockwaveEffect.
/// </summary>
public class AbilityBurstEffect : MonoBehaviour
{
    public enum BurstMode
    {
        ExpandingRing,
        DirectionalStreak,
        LinkBeam,
        RecruitPulse,
        ContractingRing,
        PullStream,
        AbsorbIntoPoint
    }

    const float BuiltinSphereApproxRadiusXZ = 0.5f;

    [Header("Defaults")]
    public float lifetime = 0.38f;

    [Range(0.02f, 0.5f)]
    public float groundFlatten = 0.09f;

    [ColorUsage(true, true)]
    public Color tint = new Color(0.35f, 0.95f, 1.15f, 0.9f);

    BurstMode _mode;
    float _elapsed;
    float _minRadius;
    float _maxRadius;
    Color _shade;
    Vector3 _streakDirection = Vector3.forward;
    float _streakLength = 4f;
    Vector3 _beamStart;
    Vector3 _beamEnd;
    Vector3 _attractCenter;
    Vector3 _absorbOrigin;
    bool _initialized;

    MaterialPropertyBlock _mpb;
    Renderer[] _rend;
    Light _pulse;

    void Awake()
    {
        EnsureRenderSetup();
    }

    void EnsureRenderSetup()
    {
        if (_mpb != null)
            return;

        _rend = GetComponentsInChildren<Renderer>(true);
        if (_rend == null)
            _rend = System.Array.Empty<Renderer>();

        foreach (Renderer r in _rend)
        {
            if (r == null) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        _mpb = new MaterialPropertyBlock();

        _pulse = GetComponent<Light>();
        if (_pulse == null)
            _pulse = gameObject.AddComponent<Light>();

        if (_pulse != null)
        {
            _pulse.type = LightType.Point;
            _pulse.shadows = LightShadows.None;
            _pulse.enabled = false;
        }
    }

    public void ConfigureRing(float gameplayRadius, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.ExpandingRing;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.42f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        float radius = Mathf.Max(gameplayRadius, 1.5f);
        _minRadius = Mathf.Clamp(radius * 0.12f, 0.4f, 2.5f);
        _maxRadius = Mathf.Max(radius * 1.65f, radius + 2f);

        SetupLight(theme, radius * 3.5f, 55f);
        SetRingScale(_minRadius);
        Paint(0f);
    }

    public void ConfigureStreak(Vector3 worldDirection, float length, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.DirectionalStreak;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.22f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _streakDirection = worldDirection;
        _streakDirection.y = 0f;
        if (_streakDirection.sqrMagnitude < 0.01f)
            _streakDirection = Vector3.forward;
        _streakDirection.Normalize();

        _streakLength = Mathf.Max(length, 1f);
        transform.rotation = Quaternion.LookRotation(_streakDirection, Vector3.up);

        SetupLight(theme, _streakLength * 2.5f, 85f);
        SetStreakScale(1f);
        Paint(0f);
    }

    public void ConfigureBeam(Vector3 from, Vector3 to, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.LinkBeam;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.34f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _beamStart = from;
        _beamStart.y += 0.9f;
        _beamEnd = to;
        _beamEnd.y += 0.9f;

        Vector3 delta = _beamEnd - _beamStart;
        delta.y = 0f;
        float span = Mathf.Max(delta.magnitude, 0.5f);

        if (delta.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

        transform.position = Vector3.Lerp(_beamStart, _beamEnd, 0.5f);

        SetupLight(theme, span * 1.8f, 95f);
        SetBeamScale(span, 0.35f);
        Paint(0f);
    }

    public void ConfigureRecruitPulse(Vector3 worldPosition, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.RecruitPulse;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.3f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;

        _minRadius = 0.35f;
        _maxRadius = 2.2f;

        SetupLight(theme, 12f, 70f);
        SetRingScale(_minRadius);
        Paint(0f);
    }

    /// <summary>Outer ring collapses inward — rally / attract field at the caster.</summary>
    public void ConfigureContractingRing(Vector3 center, float outerRadius, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.ContractingRing;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.48f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _attractCenter = center;
        _attractCenter.y += 0.05f;
        transform.position = _attractCenter;
        transform.rotation = Quaternion.identity;

        float radius = Mathf.Max(outerRadius, 2f);
        _maxRadius = radius;
        _minRadius = Mathf.Clamp(radius * 0.1f, 0.3f, 1.8f);

        SetupLight(theme, radius * 2.8f, 48f);
        SetRingScale(_maxRadius);
        Paint(0f, gentle: true);
    }

    /// <summary>Energy stream pulled from a neutral toward the player.</summary>
    public void ConfigurePullStream(Vector3 fromWorld, Vector3 toWorld, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.PullStream;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.42f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _beamStart = fromWorld;
        _beamStart.y += 0.75f;
        _beamEnd = toWorld;
        _beamEnd.y += 0.75f;

        transform.position = _beamStart;
        Vector3 delta = _beamEnd - _beamStart;
        delta.y = 0f;
        if (delta.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

        float span = Mathf.Max(delta.magnitude, 0.5f);
        SetupLight(theme, span * 1.4f, 62f);
        SetBeamScale(span, 0.28f);
        Paint(0f, gentle: true);
    }

    /// <summary>Neutral is absorbed — ring shrinks and drifts toward the pull target.</summary>
    public void ConfigureAbsorbIntoPoint(Vector3 atWorld, Vector3 pullTarget, Color theme, float effectLifetime = -1f)
    {
        EnsureRenderSetup();
        _mode = BurstMode.AbsorbIntoPoint;
        lifetime = effectLifetime > 0f ? effectLifetime : 0.36f;
        _shade = theme;
        _initialized = true;
        _elapsed = 0f;

        _absorbOrigin = atWorld;
        _absorbOrigin.y += 0.05f;
        _beamEnd = pullTarget;
        _beamEnd.y += 0.05f;

        transform.position = _absorbOrigin;
        transform.rotation = Quaternion.identity;

        _maxRadius = 2.4f;
        _minRadius = 0.18f;

        SetupLight(theme, 10f, 58f);
        SetRingScale(_maxRadius);
        Paint(0f, gentle: true);
    }

    void SetupLight(Color theme, float range, float peakIntensity)
    {
        EnsureRenderSetup();
        if (_pulse == null) return;

        _pulse.color = Color.Lerp(theme, Color.white, 0.25f);
        _pulse.range = Mathf.Clamp(range, 8f, 60f);
        _pulse.enabled = true;
        _pulse.intensity = peakIntensity;
    }

    void LateUpdate()
    {
        if (!_initialized) return;

        lifetime = Mathf.Max(lifetime, 1e-3f);
        _elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(_elapsed / lifetime);
        float eased = Mathf.SmoothStep(0f, 1f, p);

        switch (_mode)
        {
            case BurstMode.ExpandingRing:
                SetRingScale(Mathf.Lerp(_minRadius, _maxRadius, eased));
                break;
            case BurstMode.DirectionalStreak:
                SetStreakScale(1f - eased * 0.65f);
                break;
            case BurstMode.LinkBeam:
                SetBeamScale(Vector3.Distance(_beamStart, _beamEnd), Mathf.Lerp(0.45f, 0.05f, eased));
                break;
            case BurstMode.RecruitPulse:
                float pulse = Mathf.Sin(p * Mathf.PI);
                SetRingScale(Mathf.Lerp(_minRadius, _maxRadius, pulse));
                break;
            case BurstMode.ContractingRing:
                SetRingScale(Mathf.Lerp(_maxRadius, _minRadius, eased));
                break;
            case BurstMode.PullStream:
            {
                transform.position = Vector3.Lerp(_beamStart, _beamEnd, eased);
                Vector3 delta = _beamEnd - _beamStart;
                delta.y = 0f;
                float span = Mathf.Max(delta.magnitude * (1f - eased * 0.85f), 0.35f);
                if (delta.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                SetBeamScale(span, Mathf.Lerp(0.32f, 0.06f, eased));
                break;
            }
            case BurstMode.AbsorbIntoPoint:
                transform.position = Vector3.Lerp(_absorbOrigin, _beamEnd, eased * 0.45f);
                SetRingScale(Mathf.Lerp(_maxRadius, _minRadius, eased));
                break;
        }

        bool gentle = _mode == BurstMode.ContractingRing
            || _mode == BurstMode.PullStream
            || _mode == BurstMode.AbsorbIntoPoint;
        Paint(p, gentle);

        if (_pulse != null && _pulse.enabled)
            _pulse.intensity = Mathf.Lerp(_pulse.intensity, 0f, Mathf.Pow(p, 3f));

        if (p >= 1f)
            Destroy(gameObject);
    }

    void SetRingScale(float worldReachXZ)
    {
        float xz = Mathf.Max(worldReachXZ / BuiltinSphereApproxRadiusXZ, 0.06f);
        transform.localScale = new Vector3(xz, xz * groundFlatten, xz);
    }

    void SetStreakScale(float widthMul)
    {
        float lengthScale = _streakLength / BuiltinSphereApproxRadiusXZ;
        float width = Mathf.Clamp(0.22f * widthMul, 0.08f, 0.5f) / BuiltinSphereApproxRadiusXZ;
        transform.localScale = new Vector3(width, width * groundFlatten * 0.6f, lengthScale);
    }

    void SetBeamScale(float span, float thickness)
    {
        float lengthScale = span / BuiltinSphereApproxRadiusXZ;
        float width = Mathf.Max(thickness, 0.08f) / BuiltinSphereApproxRadiusXZ;
        transform.localScale = new Vector3(width, width * 0.35f, lengthScale);
    }

    void Paint(float progress, bool gentle = false)
    {
        progress = Mathf.Clamp01(progress);
        float fade = Mathf.Pow(1f - progress, gentle ? 1.1f : 0.9f);
        float sineBurst = Mathf.Sin(Mathf.Clamp01(progress * (gentle ? 2.6f : 4f) + 0.05f) * Mathf.PI);
        float crest = Mathf.Exp(-Mathf.Abs(progress - (gentle ? 0.62f : 0.28f)) * (gentle ? 9f : 15f));
        float emission = gentle
            ? Mathf.Clamp(sineBurst * 42f + crest * 72f + 18f + (1f - progress) * 22f, 6f, 140f)
            : Mathf.Clamp(sineBurst * 80f + crest * 150f + 25f + (1f - progress) * 35f, 8f, 240f);

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
            emit += Mathf.Max(sineBurst, crest) * Color.white * 12f;

            _mpb.SetColor(Shader.PropertyToID("_BaseColor"), surface);
            _mpb.SetColor(Shader.PropertyToID("_EmissionColor"), emit);
            r.SetPropertyBlock(_mpb);
        }
    }
}
