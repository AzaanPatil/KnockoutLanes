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

    [Header("Self-Right (hold accelerator while flipped)")]
    [Tooltip("Needed to read the player's current throttle input.")]
    [SerializeField] private CarController carController;
    [Tooltip("How long to hold the accelerator while flipped before the car rights itself in place -- much faster than waiting out the full flipped-reset timer above, and doesn't cost track position the way teleporting to the last checkpoint does.")]
    [SerializeField] private float selfRightHoldTime = 0.75f;
    [Tooltip("Extra height added when righting in place, so the car doesn't spawn back down still clipping into whatever flipped it.")]
    [SerializeField] private float selfRightHeightOffset = 0.5f;

    public UnityEvent OnReset = new UnityEvent();

    private Rigidbody rb;
    private float stuckTimer;
    private float flippedTimer;
    private float selfRightTimer;

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
            selfRightTimer = 0f;
            return;
        }

        bool isFlipped = Vector3.Dot(transform.up, Vector3.up) < flippedDotThreshold;
        flippedTimer = isFlipped ? flippedTimer + Time.deltaTime : 0f;

        bool holdingAccelerator = carController != null && Mathf.Abs(carController.ThrottleInput) > 0.1f;
        selfRightTimer = (isFlipped && holdingAccelerator) ? selfRightTimer + Time.deltaTime : 0f;

        bool isStuck = rb.linearVelocity.magnitude < stuckSpeedThreshold;
        stuckTimer = isStuck ? stuckTimer + Time.deltaTime : 0f;

        if (selfRightTimer >= selfRightHoldTime)
        {
            SelfRightInPlace();
        }
        else if (flippedTimer >= flippedTimeToReset || stuckTimer >= stuckTimeToReset)
        {
            ResetToLastCheckpoint();
        }
    }

    // Rights the car back onto its wheels at its current position -- unlike
    // ResetToLastCheckpoint, this doesn't cost the player any track
    // progress, so it's the reward for actively trying to recover (holding
    // the accelerator) rather than just waiting out the timer.
    private void SelfRightInPlace()
    {
        Quaternion uprightRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 position = transform.position + Vector3.up * selfRightHeightOffset;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(position, uprightRotation);

        flippedTimer = 0f;
        stuckTimer = 0f;
        selfRightTimer = 0f;
        OnReset.Invoke();
    }

    private void ResetToLastCheckpoint()
    {
        Transform checkpoint = RaceManager.Instance.GetLastPassedCheckpoint();
        if (checkpoint == null) return;

        int lastPassedIndex = RaceManager.Instance.NextCheckpointIndex - 1;
        Quaternion facing = RaceManager.Instance.GetCheckpointFacingRotation(lastPassedIndex);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(checkpoint.position, facing);

        flippedTimer = 0f;
        stuckTimer = 0f;
        OnReset.Invoke();
    }
}
