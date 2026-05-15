using UnityEngine;

/// <summary>
/// Rally Cry pull SFX. Clip is registered from <see cref="CrowdManager"/>.
/// </summary>
public static class RallyCrySfx
{
    const string DefaultClipPath = "Assets/Audio Effects/Pulling.mp3";

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
            Debug.LogWarning("RallyCrySfx: no AudioClip assigned. Assign Pulling.mp3 on CrowdManager.");
            return;
        }

        SfxOneShot.Play2D(_clip, _volume, worldPosition);
    }

    static void ResolveClip()
    {
        if (_clip != null)
            return;

        CrowdManager crowd = CrowdManager.Instance;
        if (crowd != null && crowd.rallyCryPullClip != null)
        {
            _clip = crowd.rallyCryPullClip;
            _volume = crowd.rallyCryPullVolume;
            return;
        }

#if UNITY_EDITOR
        _clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultClipPath);
#endif
    }
}
