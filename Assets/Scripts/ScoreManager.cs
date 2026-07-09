using System;
using UnityEngine;

// Tracks score and the style multiplier called for in the GDD (2.1: bonus
// points for speed and style; 6.1 HUD: score + style multiplier).
// Consecutive pin hits within ComboWindowSeconds keep the multiplier
// climbing; letting it lapse resets to the base multiplier.
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

    public int Score { get; private set; }
    public float StyleMultiplier { get; private set; } = 1f;
    public int PinsKnocked { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<float> OnStyleMultiplierChanged;

    private float lastHitTime = float.NegativeInfinity;

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

        OnStyleMultiplierChanged?.Invoke(StyleMultiplier);
        OnScoreChanged?.Invoke(Score);
    }
}
