using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public bool isGameOver = false;

    public bool isPaused = false;

    [Header("Game Over Settings")]
    public float gameOverDelay = 2f; // Delay before showing game over screen

    public GameObject gameOverUI; // Assign your game over UI panel
    public GameObject gameplayUI; // Assign your main gameplay UI

    [Header("Events")]
    public UnityEvent OnGameOver;

    public UnityEvent OnGameWin;
    public UnityEvent OnGamePause;
    public UnityEvent OnGameResume;

    // References
    private Tower centralTower;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Find the central tower
        centralTower = FindFirstObjectByType<Tower>();
        if (centralTower == null)
        {
            Debug.LogError("GameManager: No Tower found in scene!");
        }

        // Initialize UI states
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
        if (gameplayUI != null)
            gameplayUI.SetActive(true);
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return; // Prevent multiple calls

        isGameOver = true;
        Debug.Log("Game Over Triggered!");

        // Stop time or disable enemy spawning
        StartCoroutine(HandleGameOver());

        // Invoke game over event
        OnGameOver?.Invoke();
    }

    private IEnumerator HandleGameOver()
    {
        Time.timeScale = 0.5f;
        yield return new WaitForSeconds(gameOverDelay * 0.5f);

        // Stop the game
        Time.timeScale = 0f;

        // Show game over UI
        if (gameOverUI != null)
            gameOverUI.SetActive(true);
        if (gameplayUI != null)
            gameplayUI.SetActive(false);

        // Stop enemy spawning if you have a spawner
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            spawner.StopSpawning();
        }
    }

    public void TriggerGameWin()
    {
        if (isGameOver) return;

        isGameOver = true;
        Debug.Log("Game Won!");

        OnGameWin?.Invoke();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // Reset time scale
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    public void PauseGame()
    {
        if (isGameOver) return;

        isPaused = true;
        Time.timeScale = 0f;
        OnGamePause?.Invoke();
    }

    public void ResumeGame()
    {
        if (isGameOver) return;

        isPaused = false;
        Time.timeScale = 1f;
        OnGameResume?.Invoke();
    }

    public bool ShouldEndGame()
    {
        return centralTower == null || centralTower.health <= 0;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}