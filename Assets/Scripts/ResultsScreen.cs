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
    [SerializeField] private int twoStarScore = 1000;
    [SerializeField] private int threeStarScore = 2000;

    private void OnEnable()
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

        int stars = score >= threeStarScore ? 3 : score >= twoStarScore ? 2 : 1;
        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].SetActive(i < stars);
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
