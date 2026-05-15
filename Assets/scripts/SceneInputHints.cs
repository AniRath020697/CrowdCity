using TMPro;
using UnityEngine;

/// <summary>
/// Runtime UI hints for end screens without editing scene YAML.
/// </summary>
public static class SceneInputHints
{
    const string MenuHintLine = "\nPress M for main menu";

    public static void ApplyEndScreenLayout()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        UiLayout1024x768.ConfigureCanvas(canvas);
        AppendMenuHintToPressSpaceLabels();
    }

    public static void AppendMenuHintToPressSpaceLabels()
    {
        TextMeshProUGUI[] labels = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null)
                continue;

            if (label.gameObject.name.IndexOf("PressSpace", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (label.text.IndexOf("Press M", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            label.text += MenuHintLine;
            UiLayout1024x768.ApplyMenuPrompt(label);

            GameObject title = GameObject.Find("GameTitleText");
            if (title == null)
            {
                TextMeshProUGUI headline = FindHeadlineLabel(labels);
                if (headline != null)
                    UiLayout1024x768.ApplyScreenCenter(headline, 800f, 56);
            }
        }
    }

    static TextMeshProUGUI FindHeadlineLabel(TextMeshProUGUI[] labels)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label == null) continue;

            string n = label.gameObject.name;
            if (n.IndexOf("PressSpace", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (label.fontSize >= 40f)
                return label;
        }

        return null;
    }
}
