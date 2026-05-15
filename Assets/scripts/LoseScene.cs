using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseScene : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public string menuSceneName = "MainMenu";

    void Start()
    {
        Time.timeScale = 1f;
        SceneInputHints.AppendMenuHintToPressSpaceLabels();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SceneManager.LoadScene(gameSceneName);
        else if (Input.GetKeyDown(KeyCode.M))
            SceneManager.LoadScene(menuSceneName);
    }
}
