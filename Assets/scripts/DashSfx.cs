using UnityEngine;

/// <summary>
/// Turbo Dash whoosh SFX. Clip is registered from <see cref="CrowdManager"/>.
/// </summary>
public static class DashSfx
{
    const string DefaultClipPath = "Assets/Audio Effects/whoosh.mp3";

    static AudioClip _clip;
    static float _volume = 0.85f;

    public static float Volume
    {
        get => _volume;
        set => _volume = Mathf.Clamp01(value);
    }

    public static void SetClip(AudioClip clip)
    {
        _clip = clip;
    }

    public static void Play(Vector3 worldPosition)
    {
        ResolveClip();
        if (_clip == null)
        {
            Debug.LogWarning("DashSfx: no AudioClip assigned. Assign whoosh.mp3 on CrowdManager.");
            return;
        }

        SfxOneShot.Play2D(_clip, _volume, worldPosition);
    }

    static void ResolveClip()
    {
        if (_clip != null)
            return;

        CrowdManager crowd = CrowdManager.Instance;
        if (crowd != null && crowd.dashWhooshClip != null)
        {
            _clip = crowd.dashWhooshClip;
            _volume = crowd.dashWhooshVolume;
            return;
        }

#if UNITY_EDITOR
        _clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipPath);
#endif
    }
}
