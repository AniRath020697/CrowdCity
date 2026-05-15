using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Center-screen wave title that fades in, holds, then fades out at the start of each wave.
/// </summary>
public class WaveAnnouncementUI : MonoBehaviour
{
    public static WaveAnnouncementUI Instance { get; private set; }

    [Header("Timing")]
    public float fadeInDuration = 0.45f;
    public float holdDuration = 1.35f;
    public float fadeOutDuration = 0.45f;

    [Header("Style")]
    public int fontSize = 72;
    public Color textColor = Color.white;

    TextMeshProUGUI _label;
    CanvasGroup _canvasGroup;
    Coroutine _routine;

    public static WaveAnnouncementUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return null;

        var go = new GameObject("WaveAnnouncement", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        return go.AddComponent<WaveAnnouncementUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUI();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetReferenceFont(TextMeshProUGUI source)
    {
        EnsureUI();
        if (source != null && _label != null)
            _label.font = source.font;
    }

    public void ShowWaveStart(int waveNumber)
    {
        ShowMessage("WAVE " + waveNumber);
    }

    public void ShowMessage(string message, float? holdDuration = null)
    {
        EnsureUI();
        _label.text = message;

        if (_routine != null)
            StopCoroutine(_routine);

        gameObject.SetActive(true);
        _routine = StartCoroutine(CoFadeSequence(holdDuration ?? this.holdDuration));
    }

    void EnsureUI()
    {
        if (_label != null)
            return;

        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(900f, 140f);

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;

        _label = gameObject.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = fontSize;
        _label.fontStyle = FontStyles.Bold;
        _label.color = textColor;
        _label.raycastTarget = false;
        _label.text = "WAVE 1";
    }

    IEnumerator CoFadeSequence(float holdSeconds)
    {
        _canvasGroup.alpha = 0f;

        if (fadeInDuration > 0f)
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
        }

        _canvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(holdSeconds);

        if (fadeOutDuration > 0f)
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
                yield return null;
            }
        }

        _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        _routine = null;
    }
}
