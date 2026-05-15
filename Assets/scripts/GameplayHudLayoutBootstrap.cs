using System.Collections;
using UnityEngine;

/// <summary>
/// Re-applies HUD layout after all components initialize (fixes clipped / stale RectTransforms).
/// </summary>
[DefaultExecutionOrder(250)]
public class GameplayHudLayoutBootstrap : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(CoApplyHudLayout());
    }

    static IEnumerator CoApplyHudLayout()
    {
        yield return null;
        ApplyAll();

        yield return new WaitForEndOfFrame();
        ApplyAll();
    }

    static void ApplyAll()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        UiLayout1024x768.ConfigureCanvas(canvas);

        CrowdManager crowd = FindFirstObjectByType<CrowdManager>();
        if (crowd != null)
            crowd.ApplyHudLayoutFor1024x768();

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            player.ApplyHudLayoutFor1024x768(force: true);
    }
}
