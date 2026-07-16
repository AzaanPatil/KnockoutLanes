using UnityEngine;

// Tracks score and the style multiplier called for in the GDD (2.1: bonus
// points for speed and style; 6.1 HUD: score + style multiplier).
// Consecutive pin hits within ComboWindowSeconds keep the multiplier
// climbing; letting it lapse resets to the base multiplier.
//
// This script doesn't know where hits come from -- wire BowlingPin.OnKnockedDown
// (or any other UnityEvent<float>) to RegisterPinHit in the Inspector.
public class ScoreManager : Singleton<ScoreManager>
{
    [Header("Scoring")]
    [SerializeField] private int basePinScore = 100;
    [SerializeField] private float speedBonusPerUnit = 2f;
    [SerializeField] private float minSpeedForBonus = 5f;

    [Header("Style Multiplier")]
    [SerializeField] private float comboWindowSeconds = 2f;
    [SerializeField] private float multiplierStep = 0.5f;
    [SerializeField] private float maxMultiplier = 4f;
    [Tooltip("How fast the multiplier drifts back down to 1.0 once the combo window has lapsed with no new stylish action.")]
    [SerializeField] private float decayPerSecond = 0.1f;

    [Header("Events")]
    public IntEvent OnScoreChanged = new IntEvent();
    public FloatEvent OnStyleMultiplierChanged = new FloatEvent();

    public int Score { get; private set; }
    public float StyleMultiplier { get; private set; } = 1f;
    public int PinsKnocked { get; private set; }

    private float lastHitTime = float.NegativeInfinity;

    private void Update()
    {
        // Only decay once the combo window has genuinely lapsed -- don't
        // fight RegisterPinHit while a combo is actively being built.
        if (StyleMultiplier <= 1f) return;
        if (Time.time - lastHitTime <= comboWindowSeconds) return;

        float decayed = Mathf.Max(1f, StyleMultiplier - decayPerSecond * Time.deltaTime);
        if (Mathf.Approximately(decayed, StyleMultiplier)) return;

        StyleMultiplier = decayed;
        OnStyleMultiplierChanged.Invoke(StyleMultiplier);
    }

    // Immediately snaps the multiplier back to baseline -- for crashes,
    // missed checkpoints, or anything else that should punish a combo
    // instantly rather than let it decay away naturally.
    public void ResetStyleMultiplier()
    {
        if (Mathf.Approximately(StyleMultiplier, 1f)) return;

        StyleMultiplier = 1f;
        OnStyleMultiplierChanged.Invoke(StyleMultiplier);
    }

    public void RegisterPinHit(float carSpeed)
    {
        bool withinCombo = Time.time - lastHitTime <= comboWindowSeconds;
        lastHitTime = Time.time;

        StyleMultiplier = withinCombo
            ? Mathf.Min(StyleMultiplier + multiplierStep, maxMultiplier)
            : 1f;

        float speedBonus = carSpeed > minSpeedForBonus
            ? (carSpeed - minSpeedForBonus) * speedBonusPerUnit
            : 0f;
        int points = Mathf.RoundToInt((basePinScore + speedBonus) * StyleMultiplier);

        Score += points;
        PinsKnocked++;

        OnStyleMultiplierChanged.Invoke(StyleMultiplier);
        OnScoreChanged.Invoke(Score);
    }
}
