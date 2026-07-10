using UnityEngine;

// A standing pin that fires OnKnockedDown once physics has tipped it past
// TipAngleThreshold. Uses Rigidbody physics per the GDD (3.2.1) rather
// than a scripted knockdown animation.
//
// This pin has no idea scoring exists -- in the Inspector, wire
// OnKnockedDown to ScoreManager.RegisterPinHit (do it once on the pin
// prefab and every instance inherits it). You can also add extra
// listeners for VFX/SFX, or swap in different scoring for special pins.
[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [SerializeField] private float tipAngleThreshold = 60f;

    [Header("Events")]
    [Tooltip("Fires with the impact speed that knocked the pin over.")]
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
        OnKnockedDown.Invoke(lastImpactSpeed);
    }
}
