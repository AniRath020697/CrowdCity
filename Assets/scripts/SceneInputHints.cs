using TMPro;
using UnityEngine;

/// <summary>
/// Runtime UI hints for end screens without editing scene YAML.
/// </summary>
public static class SceneInputHints
{
    const string MenuHintLine = "\nPress M for main menu";

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
        }
    }
}
