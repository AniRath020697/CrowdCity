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
        ApplyMenuLayout();
        EnsureTeamNameLabel();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SceneManager.LoadScene(gameSceneName);
    }

    void ApplyMenuLayout()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        UiLayout1024x768.ConfigureCanvas(canvas);

        GameObject titleObject = GameObject.Find("GameTitleText");
        if (titleObject != null)
        {
            TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
            if (title != null)
                UiLayout1024x768.ApplyMenuTitle(title);
        }

        TextMeshProUGUI[] labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null) continue;

            string name = label.gameObject.name;
            if (name.IndexOf("PressSpace", System.StringComparison.OrdinalIgnoreCase) >= 0)
                UiLayout1024x768.ApplyMenuPrompt(label);
            else if (name == "Om" || name == "Ani")
            {
                RectTransform rt = label.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(700f, 32f);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 22;
            }
        }
    }

    void EnsureTeamNameLabel()
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return;

        GameObject existing = GameObject.Find("TeamNameText");
        TextMeshProUGUI teamLabel;

        if (existing != null)
        {
            teamLabel = existing.GetComponent<TextMeshProUGUI>();
            if (teamLabel != null)
            {
                teamLabel.text = teamName;
                UiLayout1024x768.ApplyMenuSubtitle(teamLabel, -40f);
            }

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

        teamLabel = teamObject.AddComponent<TextMeshProUGUI>();
        teamLabel.font = titleLabel.font;
        teamLabel.fontStyle = FontStyles.Normal;
        teamLabel.color = titleLabel.color;
        teamLabel.text = teamName;
        UiLayout1024x768.ApplyMenuSubtitle(teamLabel, -40f);
    }
}
