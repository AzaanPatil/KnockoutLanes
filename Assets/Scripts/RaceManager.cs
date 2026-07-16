using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Track Geometry")]
    [SerializeField] private float trackWidth = 14f;
    [SerializeField] private float barrierHeight = 1.5f;
    [SerializeField] private float barrierThickness = 0.5f;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material barrierMaterial;

    [Header("Pin Cluster Auto-Placement")]
    [SerializeField] private GameObject pinPrefabForCorners;
    [Tooltip("A checkpoint counts as a \"corner\" once the road bends by at least this many degrees.")]
    [SerializeField] private float cornerAngleThreshold = 20f;
    [SerializeField] private int cornerPinRows = 3;
    [SerializeField] private float cornerPinSpacing = 0.6f;
    [Tooltip("How far back along the approach (before the corner) to place the cluster, so it's not sitting exactly on the checkpoint gate.")]
    [SerializeField] private float cornerApproachOffset = 4f;

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

    // Null before the first checkpoint has actually been passed.
    public Transform GetLastPassedCheckpoint()
    {
        if (NextCheckpointIndex == 0) return null;
        int index = Mathf.Clamp(NextCheckpointIndex - 1, 0, checkpoints.Count - 1);
        return checkpoints[index].transform;
    }

    // Null once every checkpoint has already been passed.
    public Transform GetNextCheckpoint()
    {
        if (NextCheckpointIndex >= checkpoints.Count) return null;
        return checkpoints[NextCheckpointIndex].transform;
    }

    // Facing direction to use when resetting the car onto the checkpoint at
    // the given index -- derived from the direction between the previous
    // checkpoint and this one (or the car's spawn point, for index 0), so it
    // stays correct automatically as the track layout changes rather than
    // depending on each checkpoint's own rotation being set up by hand.
    public Quaternion GetCheckpointFacingRotation(int index)
    {
        if (index < 0 || index >= checkpoints.Count) return Quaternion.identity;

        Vector3 from = index > 0 && checkpoints[index - 1] != null
            ? checkpoints[index - 1].transform.position
            : (playerCar != null ? playerCar.transform.position : checkpoints[index].transform.position);

        Vector3 direction = checkpoints[index].transform.position - from;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : checkpoints[index].transform.rotation;
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

#if UNITY_EDITOR
    // Editor-only convenience: point the car at Check1 so its spawn facing
    // always matches the track direction, no matter how much the layout
    // gets reshuffled during level design. Re-run any time the loop changes.
    [ContextMenu("Align Car To Track Start")]
    private void AlignCarToTrackStart()
    {
        if (playerCar == null || checkpoints.Count == 0 || checkpoints[0] == null)
        {
            Debug.LogWarning("RaceManager: assign Player Car and at least one checkpoint before aligning.");
            return;
        }

        Transform carTransform = playerCar.transform;
        Vector3 direction = checkpoints[0].transform.position - carTransform.position;
        direction.y = 0f; // keep the car level rather than pitching it up/down

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("RaceManager: Car is spawned right on top of Check1, can't infer a direction.");
            return;
        }

        Undo.RecordObject(carTransform, "Align Car To Track Start");
        carTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    // Editor-only: builds a closed-loop road + barrier walls connecting the
    // checkpoints in order, replacing the flat placeholder ground plane with
    // real track geometry. Re-run any time the checkpoint layout changes.
    [ContextMenu("Build Track Geometry")]
    private void BuildTrackGeometry()
    {
        if (checkpoints.Count < 2)
        {
            Debug.LogWarning("RaceManager: need at least 2 checkpoints to build track geometry.");
            return;
        }

        var waypoints = new List<Transform>(checkpoints.Count);
        foreach (Checkpoint checkpoint in checkpoints)
        {
            waypoints.Add(checkpoint != null ? checkpoint.transform : null);
        }

        TrackGeometryGenerator.Generate(transform, waypoints, trackWidth, barrierHeight, barrierThickness, roadMaterial, barrierMaterial);
    }

    [ContextMenu("Clear Track Geometry")]
    private void ClearTrackGeometry()
    {
        TrackGeometryGenerator.Clear(transform);
    }

    // Places a pin cluster approaching every detected corner along the
    // track -- a checkpoint counts as a corner when the road direction
    // changes sharply between the segment leading into it and the segment
    // leading out. Placement sits on the track's centerline (safely within
    // Track Width for any normal cluster size) a bit before the checkpoint,
    // facing into the turn. Re-run any time the layout changes.
    [ContextMenu("Auto-Place Pin Clusters At Corners")]
    private void AutoPlacePinClustersAtCorners()
    {
        if (pinPrefabForCorners == null)
        {
            Debug.LogWarning("RaceManager: assign Pin Prefab For Corners before auto-placing.");
            return;
        }

        if (checkpoints.Count < 3)
        {
            Debug.LogWarning("RaceManager: need at least 3 checkpoints to detect corners.");
            return;
        }

        Transform existing = transform.Find("AutoPinClusters");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject clusterRoot = new GameObject("AutoPinClusters");
        clusterRoot.transform.SetParent(transform);
        Undo.RegisterCreatedObjectUndo(clusterRoot, "Auto-Place Pin Clusters");

        int cornerCount = 0;
        for (int i = 0; i < checkpoints.Count; i++)
        {
            Checkpoint prev = checkpoints[(i - 1 + checkpoints.Count) % checkpoints.Count];
            Checkpoint current = checkpoints[i];
            Checkpoint next = checkpoints[(i + 1) % checkpoints.Count];
            if (prev == null || current == null || next == null) continue;

            Vector3 incoming = current.transform.position - prev.transform.position;
            Vector3 outgoing = next.transform.position - current.transform.position;
            incoming.y = 0f;
            outgoing.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f || outgoing.sqrMagnitude < 0.0001f) continue;

            float turnAngle = Vector3.Angle(incoming.normalized, outgoing.normalized);
            if (turnAngle < cornerAngleThreshold) continue; // straight enough, not a corner

            Vector3 placement = current.transform.position - incoming.normalized * cornerApproachOffset;

            GameObject anchor = new GameObject($"CornerPins_{i}");
            anchor.transform.SetParent(clusterRoot.transform);
            anchor.transform.SetPositionAndRotation(placement, Quaternion.LookRotation(incoming.normalized, Vector3.up));
            Undo.RegisterCreatedObjectUndo(anchor, "Auto-Place Pin Clusters");

            PinFormationSpawner spawner = anchor.AddComponent<PinFormationSpawner>();
            spawner.Configure(pinPrefabForCorners, cornerPinRows, cornerPinSpacing);
            spawner.SpawnFormation();

            cornerCount++;
        }

        Debug.Log($"RaceManager: placed pin clusters at {cornerCount} detected corner(s).");
    }
#endif
}
