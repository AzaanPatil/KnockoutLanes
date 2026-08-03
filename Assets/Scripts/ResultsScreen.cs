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

    [Header("Course Identity")]
    [Tooltip("Unique ID for this course -- used as the PlayerPrefs key for its high score and best star result (e.g. \"TrainingCourse\", \"Landfill\"). Must be unique per course and match what's registered in CourseSelectManager.")]
    [SerializeField] private string courseId = "TrainingCourse";
    [Tooltip("Scene name to load for the \"Next Level\" button. Leave empty if this is the last course -- Next Level will just return to Course Select instead.")]
    [SerializeField] private string nextCourseSceneName = "";

    [Header("Star Thresholds (score required)")]
    [Tooltip("Below this score, the run earns 0 stars -- keeps stars meaning something instead of every attempt guaranteeing at least 1.")]
    [SerializeField] private int oneStarScore = 300;
    [SerializeField] private int twoStarScore = 1000;
    [SerializeField] private int threeStarScore = 2000;

    [Header("Banners")]
    [Tooltip("Shown only when this run beats the previously saved best score.")]
    [SerializeField] private GameObject newHighScoreBanner;

    [Header("Audio")]
    [Tooltip("Played once whenever the results screen appears.")]
    [SerializeField] private AudioClip finishFanfareSfx;
    [Tooltip("Played in addition to the fanfare specifically on a new high score.")]
    [SerializeField] private AudioClip newHighScoreSfx;

    // Derived from courseId rather than a fixed constant, so this same
    // script works unmodified across every course -- for Training Course
    // specifically this still resolves to the original
    // "TrainingCourse_HighScore" key, so existing saved high scores aren't
    // lost.
    private string HighScoreKey => courseId + "_HighScore";

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
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(finishFanfareSfx);
        }

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
        CourseProgress.ReportStars(courseId, stars);

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
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(newHighScoreSfx);
            }
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

    // Wire this to a "Next Level" button. Falls back to Course Select if
    // this course has no next scene set (i.e. it's the last one) or if the
    // next course hasn't actually been unlocked yet.
    public void NextLevel()
    {
        bool hasNext = !string.IsNullOrEmpty(nextCourseSceneName);
        bool nextUnlocked = hasNext && CourseProgress.IsUnlocked(nextCourseSceneName, courseId);
        if (hasNext && nextUnlocked)
        {
            SceneManager.LoadScene(nextCourseSceneName);
        }
        else
        {
            ReturnToCourseSelect();
        }
    }

    public void ReturnToCourseSelect()
    {
        SceneManager.LoadScene("CourseSelect");
    }
}
