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
    [Tooltip("How long to show a \"Ready\" cue before the numeric countdown starts.")]
    [SerializeField] private float readyDisplaySeconds = 2f;
    [SerializeField] private int countdownSeconds = 3;

    [Header("Track Geometry")]
    [SerializeField] private float trackWidth = 14f;
    [SerializeField] private float barrierHeight = 1.5f;
    [SerializeField] private float barrierThickness = 0.5f;
    [Tooltip("How many road segments to generate between each pair of checkpoints. Higher = smoother curve, more GameObjects.")]
    [SerializeField] private int trackSmoothness = 8;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material barrierMaterial;

    [Header("Pin Cluster Auto-Placement")]
    [SerializeField] private GameObject pinPrefabForCorners;
    [Tooltip("A checkpoint counts as a \"corner\" once the road bends by at least this many degrees.")]
    [SerializeField] private float cornerAngleThreshold = 20f;
    [Tooltip("Rows for the big corner clusters -- Forza-style, this is the dramatic one.")]
    [SerializeField] private int cornerPinRows = 6;
    [SerializeField] private float cornerPinSpacing = 0.6f;
    [Tooltip("How far back along the approach (before the corner) to place the cluster, so it's not sitting exactly on the checkpoint gate.")]
    [SerializeField] private float cornerApproachOffset = 4f;
    [Tooltip("Rows for the smaller clusters placed on straight sections. Set to 0 to skip straights entirely.")]
    [SerializeField] private int straightPinRows = 2;
    [SerializeField] private float straightPinSpacing = 0.6f;

    [Header("Events")]
    [Tooltip("Fires once, before the numeric countdown begins, to show a \"Ready\" cue.")]
    public UnityEvent OnCountdownReady = new UnityEvent();
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
        OnCountdownReady.Invoke();
        yield return new WaitForSeconds(readyDisplaySeconds);

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
        if (checkpoints.Count < 3)
        {
            Debug.LogWarning("RaceManager: need at least 3 checkpoints to build a smoothed track (the curve needs neighbors on both sides of each point).");
            return;
        }

        var waypoints = new List<Transform>(checkpoints.Count);
        foreach (Checkpoint checkpoint in checkpoints)
        {
            waypoints.Add(checkpoint != null ? checkpoint.transform : null);
        }

        TrackGeometryGenerator.Generate(transform, waypoints, trackWidth, barrierHeight, barrierThickness, trackSmoothness, roadMaterial, barrierMaterial);
    }

    [ContextMenu("Clear Track Geometry")]
    private void ClearTrackGeometry()
    {
        TrackGeometryGenerator.Clear(transform);
    }

    // Places a big pin cluster approaching every detected corner, plus a
    // smaller one at every straight checkpoint -- Forza-style: the dramatic
    // knockdowns happen in corners, with lighter pins scattered on the
    // straights for variety. A checkpoint counts as a corner when the road
    // direction changes sharply between the segment leading into it and the
    // segment leading out. Re-run any time the layout changes.
    [ContextMenu("Auto-Place Pin Clusters")]
    private void AutoPlacePinClusters()
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
        int straightCount = 0;
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
            bool isCorner = turnAngle >= cornerAngleThreshold;

            if (isCorner)
            {
                Vector3 placement = current.transform.position - incoming.normalized * cornerApproachOffset;
                PlaceCluster(clusterRoot.transform, placement, incoming.normalized, cornerPinRows, cornerPinSpacing, $"CornerPins_{i}");
                cornerCount++;
            }
            else if (straightPinRows > 0)
            {
                PlaceCluster(clusterRoot.transform, current.transform.position, incoming.normalized, straightPinRows, straightPinSpacing, $"StraightPins_{i}");
                straightCount++;
            }
        }

        Debug.Log($"RaceManager: placed {cornerCount} corner cluster(s) and {straightCount} straight cluster(s).");
    }

    private void PlaceCluster(Transform parent, Vector3 position, Vector3 facingDirection, int rows, float spacing, string name)
    {
        GameObject anchor = new GameObject(name);
        anchor.transform.SetParent(parent);
        anchor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facingDirection, Vector3.up));
        Undo.RegisterCreatedObjectUndo(anchor, "Auto-Place Pin Clusters");

        PinFormationSpawner spawner = anchor.AddComponent<PinFormationSpawner>();
        spawner.Configure(pinPrefabForCorners, rows, spacing);
        spawner.SpawnFormation();
    }

    // Finds every PinFormationSpawner in the scene (whether placed by hand,
    // by Auto-Place Pin Clusters At Corners, or anywhere else) and snaps
    // each one onto the nearest point of the same smoothed curve the road
    // is built from -- fixes clusters that ended up outside the track
    // margins after the layout changed. Re-spawns each one afterward so the
    // actual pins move too, not just the anchor.
    [ContextMenu("Snap Pin Clusters To Track")]
    private void SnapPinClustersToTrack()
    {
        if (checkpoints.Count < 3)
        {
            Debug.LogWarning("RaceManager: need at least 3 checkpoints to compute the track path.");
            return;
        }

        var waypoints = new List<Transform>(checkpoints.Count);
        foreach (Checkpoint checkpoint in checkpoints)
        {
            waypoints.Add(checkpoint != null ? checkpoint.transform : null);
        }

        List<Vector3> path = TrackGeometryGenerator.BuildSmoothPath(waypoints, trackSmoothness);
        if (path.Count < 2)
        {
            Debug.LogWarning("RaceManager: couldn't build a track path to snap to.");
            return;
        }

        PinFormationSpawner[] spawners = FindObjectsByType<PinFormationSpawner>(FindObjectsSortMode.None);
        foreach (PinFormationSpawner spawner in spawners)
        {
            TrackGeometryGenerator.GetClosestPointOnPath(path, spawner.transform.position, out Vector3 closest, out Vector3 forward);

            Undo.RecordObject(spawner.transform, "Snap Pin Cluster To Track");
            spawner.transform.SetPositionAndRotation(closest, Quaternion.LookRotation(forward, Vector3.up));
            spawner.SpawnFormation();
        }

        Debug.Log($"RaceManager: snapped {spawners.Length} pin cluster(s) onto the track.");
    }
#endif
}
