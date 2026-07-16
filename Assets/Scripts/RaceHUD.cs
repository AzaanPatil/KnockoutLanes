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

    private void Start()
    {
        // Hidden during the countdown, shown once the race actually starts,
        // hidden again once it finishes.
        SetHUDVisible(false);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.AddListener(HandleScoreChanged);
            ScoreManager.Instance.OnStyleMultiplierChanged.AddListener(HandleStyleMultiplierChanged);
            HandleScoreChanged(ScoreManager.Instance.Score);
            HandleStyleMultiplierChanged(ScoreManager.Instance.StyleMultiplier);
        }
        else
        {
            Debug.LogWarning("RaceHUD: no ScoreManager in the scene (or it's disabled) -- score/style multiplier won't update.");
        }

        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnCheckpointPassed.AddListener(HandleCheckpointPassed);
            RaceManager.Instance.OnRaceStart.AddListener(HandleRaceStart);
            RaceManager.Instance.OnRaceFinished.AddListener(HandleRaceFinished);
            HandleCheckpointPassed(RaceManager.Instance.NextCheckpointIndex, RaceManager.Instance.TotalCheckpoints);
        }
        else
        {
            Debug.LogWarning("RaceHUD: no RaceManager in the scene (or it's disabled) -- checkpoint progress won't update.");
        }
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
            RaceManager.Instance.OnRaceStart.RemoveListener(HandleRaceStart);
            RaceManager.Instance.OnRaceFinished.RemoveListener(HandleRaceFinished);
        }
    }

    private void HandleRaceStart() => SetHUDVisible(true);

    private void HandleRaceFinished(float finalTime) => SetHUDVisible(false);

    private void SetHUDVisible(bool visible)
    {
        timerText.gameObject.SetActive(visible);
        scoreText.gameObject.SetActive(visible);
        styleMultiplierText.gameObject.SetActive(visible);
        checkpointText.gameObject.SetActive(visible);
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
