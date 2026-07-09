using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Drives the Countdown -> Racing -> Finished flow for a single course and
// enforces that checkpoints are passed in order (GDD Level 1 intros:
// steering, scoring, checkpoints). Strict order stops a lap of the oval
// from being cut short by weaving between checkpoints out of sequence.
public class RaceManager : Singleton<RaceManager>
{
    public enum RaceState { Countdown, Racing, Finished }

    [Header("Course Setup")]
    [Tooltip("Checkpoints in the order the player must pass through them.")]
    [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
    [SerializeField] private CarController playerCar;

    [Header("Countdown")]
    [SerializeField] private int countdownSeconds = 3;

    public RaceState CurrentState { get; private set; } = RaceState.Countdown;
    public float ElapsedTime { get; private set; }
    public int NextCheckpointIndex { get; private set; }
    public int TotalCheckpoints => checkpoints.Count;

    public event Action<int> OnCountdownTick; // 0 means "GO"
    public event Action OnRaceStart;
    public event Action<int, int> OnCheckpointPassed; // (passedCount, total)
    public event Action<float> OnRaceFinished; // finalTime

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
            OnCountdownTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
        }

        OnCountdownTick?.Invoke(0);
        CurrentState = RaceState.Racing;
        if (playerCar != null)
        {
            playerCar.CanDrive = true;
        }
        OnRaceStart?.Invoke();
    }

    public void RegisterCheckpointPassed(Checkpoint checkpoint)
    {
        if (CurrentState != RaceState.Racing) return;
        if (NextCheckpointIndex >= checkpoints.Count) return;
        if (checkpoints[NextCheckpointIndex] != checkpoint) return; // out of order, ignore

        NextCheckpointIndex++;
        OnCheckpointPassed?.Invoke(NextCheckpointIndex, checkpoints.Count);
    }

    public Transform GetLastPassedCheckpoint()
    {
        if (checkpoints.Count == 0) return null;
        int index = Mathf.Clamp(NextCheckpointIndex - 1, 0, checkpoints.Count - 1);
        return checkpoints[index].transform;
    }

    public void TryFinishRace()
    {
        if (CurrentState != RaceState.Racing) return;
        if (NextCheckpointIndex < checkpoints.Count) return; // must hit every checkpoint first

        CurrentState = RaceState.Finished;
        if (playerCar != null)
        {
            playerCar.CanDrive = false;
        }
        OnRaceFinished?.Invoke(ElapsedTime);
    }
}
