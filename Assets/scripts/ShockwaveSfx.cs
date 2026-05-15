using UnityEngine;

/// <summary>
/// One-shot explosion SFX for player and enemy shockwaves.
/// </summary>
public static class ShockwaveSfx
{
    const string DefaultClipPath = "Assets/Audio Effects/Explosion.wav";

    static AudioClip _clip;
    static float _volume = 0.9f;

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
            Debug.LogWarning("ShockwaveSfx: no AudioClip assigned. Assign Explosion.wav on CrowdManager.");
            return;
        }

        SfxOneShot.Play2D(_clip, _volume, worldPosition);
    }

    static void ResolveClip()
    {
        if (_clip != null)
            return;

        CrowdManager crowd = CrowdManager.Instance;
        if (crowd != null && crowd.shockwaveExplosionClip != null)
        {
            _clip = crowd.shockwaveExplosionClip;
            _volume = crowd.shockwaveExplosionVolume;
            return;
        }

#if UNITY_EDITOR
        _clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipPath);
#endif
    }
}
