using UnityEngine;

// A lightweight, knockable hazard (e.g. a traffic cone) -- unlike driving
// into a Barrier (instant full style reset), this only dings the style
// multiplier by a small amount. Pair with a low-mass Rigidbody so it gets
// knocked aside like a bowling pin rather than meaningfully slowing the
// car down. Scores no points -- this is purely a style penalty.
[RequireComponent(typeof(Rigidbody))]
public class MinorHazard : MonoBehaviour
{
    [Tooltip("Subtracted from the style multiplier on hit -- compare against ScoreManager's Multiplier Step (what a single pin hit adds) to judge how harsh this feels relative to a pin.")]
    [SerializeField] private float stylePenalty = 0.7f;
    [SerializeField] private AudioClip knockSfx;

    private bool hit;

    private void OnCollisionEnter(Collision collision)
    {
        if (hit) return;
        if (collision.rigidbody == null || !collision.rigidbody.CompareTag("Player")) return;

        hit = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ApplyStylePenalty(stylePenalty);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(knockSfx);
        }
    }
}
