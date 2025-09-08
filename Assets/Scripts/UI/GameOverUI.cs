using UnityEngine;
using UnityEngine.UI;
using TMPro;


// Handles the Game Over UI display and interactions

public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI gameOverText;
    public TextMeshProUGUI finalScoreText;
    public Button restartButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Animation")]
    public float fadeInDuration = 1f;
    public CanvasGroup canvasGroup;

    private void Start()
    {
        
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(LoadMainMenu);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void OnEnable()
    {
        
        UpdateFinalScore();

        
        if (canvasGroup != null)
        {
            StartCoroutine(FadeIn());
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        while (elapsedTime < fadeInDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeInDuration);
            elapsedTime += Time.unscaledDeltaTime; 
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private void UpdateFinalScore()
    {
        if (finalScoreText != null)
        {
            finalScoreText.text = "Tower Defense Failed!";
        }

        if (gameOverText != null)
        {
            gameOverText.text = "GAME OVER";
        }
    }

    public void RestartGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }

    public void LoadMainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMainMenu();
        }
    }

    public void QuitGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.QuitGame();
        }
    }
}