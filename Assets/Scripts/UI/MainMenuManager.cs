using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button exitButton;

    [Header("Game Scene")]
    public string gameSceneName = "MainScene";

    private void Start()
    {
        //button listener setup
        if(startButton != null)
            startButton.onClick.AddListener(StartGame);

        if(exitButton != null)
            exitButton.onClick.AddListener(ExitGame);

        //ensure time is running normally
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            ExitGame();
        }
    }
}
