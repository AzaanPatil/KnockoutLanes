using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Final score panel (GDD flow: Final Score -> Three-Star System). Stars
// are awarded from score thresholds set per-course in the Inspector.
public class ResultsScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text finalTimeText;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private GameObject[] starIcons;

    [Header("Star Thresholds (score required)")]
    [Tooltip("Below this score, the run earns 0 stars -- keeps stars meaning something instead of every attempt guaranteeing at least 1.")]
    [SerializeField] private int oneStarScore = 300;
    [SerializeField] private int twoStarScore = 1000;
    [SerializeField] private int threeStarScore = 2000;

    [Header("Banners")]
    [Tooltip("Shown only when this run beats the previously saved best score.")]
    [SerializeField] private GameObject newHighScoreBanner;

    private const string HighScoreKey = "TrainingCourse_HighScore";

    private void Start()
    {
        RaceManager.Instance.OnRaceFinished.AddListener(HandleRaceFinished);
    }

    private void OnDisable()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceFinished.RemoveListener(HandleRaceFinished);
        }
    }

    private void HandleRaceFinished(float finalTime)
    {
        panel.SetActive(true);

        int score = ScoreManager.Instance.Score;
        int minutes = Mathf.FloorToInt(finalTime / 60f);
        float seconds = finalTime % 60f;

        finalTimeText.text = $"Time: {minutes:00}:{seconds:00.00}";
        finalScoreText.text = $"Score: {score}";

        int stars = score >= threeStarScore ? 3 : score >= twoStarScore ? 2 : score >= oneStarScore ? 1 : 0;
        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].SetActive(i < stars);
        }

        int previousHighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        bool isNewHighScore = score > previousHighScore;
        if (newHighScoreBanner != null)
        {
            newHighScoreBanner.SetActive(isNewHighScore);
        }
        if (isNewHighScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, score);
            PlayerPrefs.Save();
        }
    }

    public void Replay()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
