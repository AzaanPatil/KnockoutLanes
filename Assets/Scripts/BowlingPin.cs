using UnityEngine;

// A standing pin that scores itself via ScoreManager.Instance once physics
// has tipped it past TipAngleThreshold. Uses Rigidbody physics per the GDD
// (3.2.1) rather than a scripted knockdown animation.
//
// Scoring is called directly in code (not just via OnKnockedDown) because
// prefab assets can't hold a serialized reference to a scene object like
// ScoreManager -- there'd be nothing to wire in the Inspector at the prefab
// level. OnKnockedDown is still exposed for genuinely optional extras (a
// specific pin's VFX/SFX) that you want to wire per-instance.
[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [SerializeField] private float tipAngleThreshold = 60f;
    [SerializeField] private AudioClip knockdownSfx;

    [Header("Events")]
    [Tooltip("Optional -- fires with the impact speed that knocked the pin over. Scoring happens automatically and doesn't depend on this being wired.")]
    public FloatEvent OnKnockedDown = new FloatEvent();

    private bool knockedDown;
    private float lastImpactSpeed;

    private void OnCollisionEnter(Collision collision)
    {
        lastImpactSpeed = Mathf.Max(lastImpactSpeed, collision.relativeVelocity.magnitude);
    }

    private void FixedUpdate()
    {
        if (knockedDown) return;

        float tipAngle = Vector3.Angle(transform.up, Vector3.up);
        if (tipAngle < tipAngleThreshold) return;

        knockedDown = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.RegisterPinHit(lastImpactSpeed);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(knockdownSfx);
        }

        OnKnockedDown.Invoke(lastImpactSpeed);
    }
}
