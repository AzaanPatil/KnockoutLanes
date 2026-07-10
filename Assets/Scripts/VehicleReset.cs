using UnityEngine;
using UnityEngine.Events;

// Recovers a stuck or flipped car by teleporting it back to the last
// checkpoint it passed (GDD 7.1: "vehicle will reset if stuck").
//
// OnReset fires right as the teleport happens -- wire it to a screen
// flash, a sound, or a particle burst from the Inspector.
[RequireComponent(typeof(Rigidbody))]
public class VehicleReset : MonoBehaviour
{
    [SerializeField] private float stuckSpeedThreshold = 0.5f;
    [SerializeField] private float stuckTimeToReset = 3f;
    [SerializeField] private float flippedDotThreshold = 0.3f;
    [SerializeField] private float flippedTimeToReset = 1.5f;

    public UnityEvent OnReset = new UnityEvent();

    private Rigidbody rb;
    private float stuckTimer;
    private float flippedTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (RaceManager.Instance == null || RaceManager.Instance.CurrentState != RaceManager.RaceState.Racing)
        {
            stuckTimer = 0f;
            flippedTimer = 0f;
            return;
        }

        bool isFlipped = Vector3.Dot(transform.up, Vector3.up) < flippedDotThreshold;
        flippedTimer = isFlipped ? flippedTimer + Time.deltaTime : 0f;

        bool isStuck = rb.linearVelocity.magnitude < stuckSpeedThreshold;
        stuckTimer = isStuck ? stuckTimer + Time.deltaTime : 0f;

        if (flippedTimer >= flippedTimeToReset || stuckTimer >= stuckTimeToReset)
        {
            ResetToLastCheckpoint();
        }
    }

    private void ResetToLastCheckpoint()
    {
        Transform checkpoint = RaceManager.Instance.GetLastPassedCheckpoint();
        if (checkpoint == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(checkpoint.position, checkpoint.rotation);

        flippedTimer = 0f;
        stuckTimer = 0f;
        OnReset.Invoke();
    }
}
