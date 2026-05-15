using UnityEngine;

/// <summary>
/// Full-volume 2D one-shot SFX (audible with the top-down camera).
/// </summary>
public static class SfxOneShot
{
    public static void Play2D(AudioClip clip, float volume, Vector3 worldPosition)
    {
        if (clip == null)
            return;

        GameObject host = new GameObject("OneShotSfx");
        host.transform.position = worldPosition;

        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        source.loop = false;
        source.Play();

        Object.Destroy(host, clip.length + 0.2f);
    }
}
