using UnityEngine;
using UnityEngine.SceneManagement;

// Escape-key pause menu. Freezes gameplay via Time.timeScale rather than
// disabling scripts individually -- physics, animations, and any
// WaitForSeconds-based coroutines (like RaceManager's countdown) all
// naturally respect this, no extra wiring needed anywhere else.
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private bool isPaused;

    private void Start()
    {
        SetPaused(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && CanTogglePause())
        {
            SetPaused(!isPaused);
        }
    }

    // Don't let pause open over the results screen -- that already owns
    // the "what next" flow once the race is finished.
    private bool CanTogglePause()
    {
        return RaceManager.Instance == null || RaceManager.Instance.CurrentState != RaceManager.RaceState.Finished;
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void Restart()
    {
        Time.timeScale = 1f; // must reset before loading -- timeScale persists across scene loads
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
