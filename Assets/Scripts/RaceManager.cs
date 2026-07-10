using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Drives the Countdown -> Racing -> Finished flow for a single course and
// enforces that checkpoints are passed in order (GDD Level 1 intros:
// steering, scoring, checkpoints). Strict order stops a lap of the oval
// from being cut short by weaving between checkpoints out of sequence.
//
// All state-change moments below are UnityEvents so you can wire up
// responses (HUD, audio, VFX, other gameplay scripts) from the Inspector
// without adding new subscriber code.
public class RaceManager : Singleton<RaceManager>
{
    public enum RaceState { Countdown, Racing, Finished }

    [Header("Course Setup")]
    [Tooltip("Checkpoints in the order the player must pass through them.")]
    [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
    [SerializeField] private CarController playerCar;

    [Header("Countdown")]
    [SerializeField] private int countdownSeconds = 3;

    [Header("Events")]
    [Tooltip("Fires once per second while counting down. 0 means \"GO\".")]
    public IntEvent OnCountdownTick = new IntEvent();
    [Tooltip("Fires once the countdown ends and driving is allowed.")]
    public UnityEvent OnRaceStart = new UnityEvent();
    [Tooltip("Fires (passedCount, total) whenever a checkpoint is accepted.")]
    public CheckpointEvent OnCheckpointPassed = new CheckpointEvent();
    [Tooltip("Fires with the final elapsed time once the race is complete.")]
    public FloatEvent OnRaceFinished = new FloatEvent();

    public RaceState CurrentState { get; private set; } = RaceState.Countdown;
    public float ElapsedTime { get; private set; }
    public int NextCheckpointIndex { get; private set; }
    public int TotalCheckpoints => checkpoints.Count;

    protected override void Awake()
    {
        base.Awake();
        if (playerCar != null)
        {
            playerCar.CanDrive = false;
        }
    }

    private void Start()
    {
        StartCoroutine(RunCountdown());
    }

    private void Update()
    {
        if (CurrentState == RaceState.Racing)
        {
            ElapsedTime += Time.deltaTime;
        }
    }

    private IEnumerator RunCountdown()
    {
        for (int remaining = countdownSeconds; remaining > 0; remaining--)
        {
            OnCountdownTick.Invoke(remaining);
            yield return new WaitForSeconds(1f);
        }

        OnCountdownTick.Invoke(0);
        CurrentState = RaceState.Racing;
        if (playerCar != null)
        {
            playerCar.CanDrive = true;
        }
        OnRaceStart.Invoke();
    }

    // Returns true if the checkpoint was next in sequence and got accepted.
    public bool RegisterCheckpointPassed(Checkpoint checkpoint)
    {
        if (CurrentState != RaceState.Racing) return false;
        if (NextCheckpointIndex >= checkpoints.Count) return false;
        if (checkpoints[NextCheckpointIndex] != checkpoint) return false; // out of order, ignore

        NextCheckpointIndex++;
        OnCheckpointPassed.Invoke(NextCheckpointIndex, checkpoints.Count);
        return true;
    }

    public Transform GetLastPassedCheckpoint()
    {
        if (checkpoints.Count == 0) return null;
        int index = Mathf.Clamp(NextCheckpointIndex - 1, 0, checkpoints.Count - 1);
        return checkpoints[index].transform;
    }

    // Returns true if every checkpoint had already been passed and the race
    // actually ended.
    public bool TryFinishRace()
    {
        if (CurrentState != RaceState.Racing) return false;
        if (NextCheckpointIndex < checkpoints.Count) return false; // must hit every checkpoint first

        CurrentState = RaceState.Finished;
        if (playerCar != null)
        {
            playerCar.CanDrive = false;
        }
        OnRaceFinished.Invoke(ElapsedTime);
        return true;
    }
}
