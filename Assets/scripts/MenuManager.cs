using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Scenes")]
    public string gameSceneName = "SampleScene";

    [Header("Credits")]
    [Tooltip("Shown under the game title on the main menu.")]
    public string teamName = "Team CrowD City";

    void Start()
    {
        EnsureTeamNameLabel();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SceneManager.LoadScene(gameSceneName);
    }

    void EnsureTeamNameLabel()
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return;

        GameObject existing = GameObject.Find("TeamNameText");
        if (existing != null)
        {
            TextMeshProUGUI existingLabel = existing.GetComponent<TextMeshProUGUI>();
            if (existingLabel != null)
                existingLabel.text = teamName;
            return;
        }

        GameObject titleObject = GameObject.Find("GameTitleText");
        if (titleObject == null)
            return;

        TextMeshProUGUI titleLabel = titleObject.GetComponent<TextMeshProUGUI>();
        if (titleLabel == null)
            return;

        var teamObject = new GameObject("TeamNameText", typeof(RectTransform));
        teamObject.transform.SetParent(titleObject.transform.parent, false);

        RectTransform teamRect = teamObject.GetComponent<RectTransform>();
        RectTransform titleRect = titleLabel.rectTransform;
        teamRect.anchorMin = titleRect.anchorMin;
        teamRect.anchorMax = titleRect.anchorMax;
        teamRect.pivot = titleRect.pivot;
        teamRect.anchoredPosition = titleRect.anchoredPosition + new Vector2(0f, -52f);
        teamRect.sizeDelta = titleRect.sizeDelta;

        TextMeshProUGUI teamLabel = teamObject.AddComponent<TextMeshProUGUI>();
        teamLabel.font = titleLabel.font;
        teamLabel.fontSize = Mathf.RoundToInt(titleLabel.fontSize * 0.42f);
        teamLabel.fontStyle = FontStyles.Normal;
        teamLabel.color = titleLabel.color;
        teamLabel.alignment = TextAlignmentOptions.Center;
        teamLabel.raycastTarget = false;
        teamLabel.text = teamName;
    }
}
