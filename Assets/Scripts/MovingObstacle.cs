using UnityEngine;

// Patrols back and forth between two points at a steady speed -- e.g. a
// steamroller sweeping across part of the road. Moved via Rigidbody.MovePosition
// (kinematic) so it properly pushes/collides with the car instead of passing
// through it. Tag this object "Barrier" so CarController's
// BarrierImpactPenalty treats getting hit by it the same as any other crash.
[RequireComponent(typeof(Rigidbody))]
public class MovingObstacle : MonoBehaviour
{
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private float speed = 3f;
    [Tooltip("Seconds to pause at each end before reversing direction -- gives the player a readable beat to time around, rather than it whipping straight back.")]
    [SerializeField] private float pauseAtEnds = 1f;

    private Rigidbody rb;
    private bool movingToPointB = true;
    private float pauseTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    private void FixedUpdate()
    {
        if (pointA == null || pointB == null) return;

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 target = movingToPointB ? pointB.position : pointA.position;
        Vector3 newPosition = Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPosition);

        if (Vector3.Distance(newPosition, target) < 0.05f)
        {
            movingToPointB = !movingToPointB;
            pauseTimer = pauseAtEnds;
        }
    }
}
