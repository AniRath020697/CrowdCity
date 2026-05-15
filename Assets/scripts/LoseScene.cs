using UnityEngine;
using UnityEngine.SceneManagement;

public class LoseScene : MonoBehaviour
{
    public string gameSceneName = "SampleScene";
    public string menuSceneName = "MainMenu";

    void Start()
    {
        // Make sure time isn't stuck paused from a previous scene.
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            SceneManager.LoadScene(menuSceneName);
        }
    }
}