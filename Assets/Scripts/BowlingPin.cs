using UnityEngine;

// A standing pin that reports itself to ScoreManager once physics has
// tipped it past TipAngleThreshold. Uses Rigidbody physics per the GDD
// (3.2.1) rather than a scripted knockdown animation.
[RequireComponent(typeof(Rigidbody))]
public class BowlingPin : MonoBehaviour
{
    [SerializeField] private float tipAngleThreshold = 60f;

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
        ScoreManager.Instance.RegisterPinHit(lastImpactSpeed);
    }
}
