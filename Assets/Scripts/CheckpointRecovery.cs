using UnityEngine;

// If the player drives past the next checkpoint without triggering it,
// gives a few seconds to turn back before auto-resetting them right onto
// that checkpoint -- stops a missed gate from softlocking a strict-order
// course. Complements VehicleReset, which handles the separate case of a
// stuck/flipped car.
//
// "Missed it" is measured as distance actually driven since the closest
// approach to the target, not straight-line distance to it -- straight-line
// distance can rise and fall on a curved track even while driving correctly,
// which would false-positive constantly on anything but a dead-straight
// approach.
[RequireComponent(typeof(Rigidbody))]
public class CheckpointRecovery : MonoBehaviour
{
    [Tooltip("How far the car can drive past its closest approach before counting as \"missed.\"")]
    [SerializeField] private float missDistance = 15f;
    [SerializeField] private float gracePeriodSeconds = 3f;

    private Rigidbody rb;
    private int trackedCheckpointIndex = -1;
    private float closestDistanceSoFar = float.PositiveInfinity;
    private float distanceDrivenSinceClosestApproach;
    private Vector3 lastPosition;
    private float missedTimer = -1f; // negative means "not currently missed"

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (RaceManager.Instance == null || RaceManager.Instance.CurrentState != RaceManager.RaceState.Racing)
        {
            missedTimer = -1f;
            return;
        }

        Transform nextCheckpoint = RaceManager.Instance.GetNextCheckpoint();
        if (nextCheckpoint == null)
        {
            missedTimer = -1f;
            return;
        }

        // Reset tracking whenever the target checkpoint changes (including
        // the very first frame this script runs).
        if (RaceManager.Instance.NextCheckpointIndex != trackedCheckpointIndex)
        {
            trackedCheckpointIndex = RaceManager.Instance.NextCheckpointIndex;
            closestDistanceSoFar = float.PositiveInfinity;
            distanceDrivenSinceClosestApproach = 0f;
            missedTimer = -1f;
        }

        float distanceToTarget = Vector3.Distance(transform.position, nextCheckpoint.position);
        if (distanceToTarget < closestDistanceSoFar)
        {
            closestDistanceSoFar = distanceToTarget;
            distanceDrivenSinceClosestApproach = 0f;
        }
        else
        {
            distanceDrivenSinceClosestApproach += Vector3.Distance(transform.position, lastPosition);
        }
        lastPosition = transform.position;

        bool hasMissed = distanceDrivenSinceClosestApproach > missDistance;
        if (!hasMissed)
        {
            missedTimer = -1f;
            return;
        }

        if (missedTimer < 0f)
        {
            missedTimer = gracePeriodSeconds;
            return;
        }

        missedTimer -= Time.deltaTime;
        if (missedTimer <= 0f)
        {
            ResetToCheckpoint(nextCheckpoint);
        }
    }

    private void ResetToCheckpoint(Transform checkpoint)
    {
        Quaternion facing = RaceManager.Instance.GetCheckpointFacingRotation(RaceManager.Instance.NextCheckpointIndex);

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(checkpoint.position, facing);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetStyleMultiplier();
        }

        closestDistanceSoFar = float.PositiveInfinity;
        distanceDrivenSinceClosestApproach = 0f;
        missedTimer = -1f;
    }
}
