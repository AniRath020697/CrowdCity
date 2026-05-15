using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScene : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public string menuSceneName = "MainMenu";

    void Start()
    {
        Time.timeScale = 1f;
        SceneInputHints.ApplyEndScreenLayout();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SceneManager.LoadScene(gameSceneName);
        else if (Input.GetKeyDown(KeyCode.M))
            SceneManager.LoadScene(menuSceneName);
    }
}
