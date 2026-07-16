using UnityEngine;

// Continuously builds the style multiplier while the car is genuinely
// drifting (per CarController.IsDrifting -- handbrake held, moving, and
// actually sliding sideways), rather than only rewarding pin combos.
// Complements ScoreManager's passive decay: sustained drifting keeps the
// multiplier climbing the same way a pin combo does.
public class DriftStyleTracker : MonoBehaviour
{
    [SerializeField] private CarController carController;
    [SerializeField] private float styleGainPerSecond = 0.3f;

    private void Update()
    {
        if (carController == null || !carController.IsDrifting) return;
        if (ScoreManager.Instance == null) return;

        ScoreManager.Instance.AddStyle(styleGainPerSecond * Time.deltaTime);
    }
}
