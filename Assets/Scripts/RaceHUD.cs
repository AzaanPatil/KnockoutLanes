using TMPro;
using UnityEngine;

// In-race HUD (GDD 6.1): timer, score, style multiplier, checkpoint
// progress.
public class RaceHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text styleMultiplierText;
    [SerializeField] private TMP_Text checkpointText;

    private void OnEnable()
    {
        ScoreManager.Instance.OnScoreChanged.AddListener(HandleScoreChanged);
        ScoreManager.Instance.OnStyleMultiplierChanged.AddListener(HandleStyleMultiplierChanged);
        RaceManager.Instance.OnCheckpointPassed.AddListener(HandleCheckpointPassed);

        HandleScoreChanged(ScoreManager.Instance.Score);
        HandleStyleMultiplierChanged(ScoreManager.Instance.StyleMultiplier);
        HandleCheckpointPassed(RaceManager.Instance.NextCheckpointIndex, RaceManager.Instance.TotalCheckpoints);
    }

    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.RemoveListener(HandleScoreChanged);
            ScoreManager.Instance.OnStyleMultiplierChanged.RemoveListener(HandleStyleMultiplierChanged);
        }
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnCheckpointPassed.RemoveListener(HandleCheckpointPassed);
        }
    }

    private void Update()
    {
        if (timerText == null || RaceManager.Instance == null) return;
        timerText.text = FormatTime(RaceManager.Instance.ElapsedTime);
    }

    private void HandleScoreChanged(int score) => scoreText.text = $"Score: {score}";

    private void HandleStyleMultiplierChanged(float multiplier) => styleMultiplierText.text = $"x{multiplier:0.0}";

    private void HandleCheckpointPassed(int passed, int total) => checkpointText.text = $"{passed}/{total}";

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds % 60f;
        return $"{minutes:00}:{remainingSeconds:00.00}";
    }
}
