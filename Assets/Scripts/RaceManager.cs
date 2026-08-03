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
    [Tooltip("Optional. Doesn't count toward checkpoint progress, but if assigned, its position is used as an extra point when shaping the track curve -- otherwise the final stretch back to Checkpoint 1 ignores where the finish line actually sits and can cut a straighter path than intended.")]
    [SerializeField] private FinishLine finishLine;
    [Tooltip("If true (default), the track forms a closed loop -- the road wraps from the last point back to the first. If false, this is a point-to-point course: the road simply ends at the last waypoint, no loop-back connection.")]
    [SerializeField] private bool closedLoop = true;

    [Header("Countdown")]
    [Tooltip("How long to show a \"Ready\" cue before the numeric countdown starts.")]
    [SerializeField] private float readyDisplaySeconds = 2f;
    [SerializeField] private int countdownSeconds = 3;

    [Header("Path Nodes (optional manual shaping)")]
    [Tooltip("Optional. If this has 3+ entries, the road curve is shaped by these instead of the checkpoints -- lets you fix a section that generated wrong (spline overshoot/bulge on tight turns) without moving gameplay checkpoints. Leave empty to shape the road from checkpoints directly, same as before. Use \"Generate Path Nodes From Checkpoints\" to seed this list, then add/move/remove nodes freely.")]
    [SerializeField] private List<Transform> pathNodes = new List<Transform>();

    [Header("Track Geometry")]
    [SerializeField] private float trackWidth = 14f;
    [SerializeField] private float barrierHeight = 1.5f;
    [SerializeField] private float barrierThickness = 0.5f;
    [Tooltip("How many road segments to generate between each pair of checkpoints. Higher = smoother curve, more GameObjects.")]
    [SerializeField] private int trackSmoothness = 8;
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Material barrierMaterial;
    [Tooltip("Optional. If set, tags the generated road segments with this so CarController's Dirt Road Handling applies (rougher grip/deceleration than asphalt) -- e.g. \"DirtRoad\" for Landfill. The tag must already exist in Project Settings -> Tags and Layers. Leave empty for a course that should drive like normal asphalt.")]
    [SerializeField] private string roadSurfaceTag = "";
    [Tooltip("Optional. If assigned, the generated road/barriers (and Snap Checkpoints To Terrain / Snap Pin Clusters To Track) conform to this terrain's height instead of assuming flat ground.")]
    [SerializeField] private Terrain terrain;
    [Tooltip("If > 0, clamps the road's height to within this many units of its average elevation, then smooths the transitions -- keeps the track relatively flat even if the terrain underneath is bumpy. Set to 0 to let the road fully follow terrain height (needed for intentional elevation, e.g. Mountain Course).")]
    [SerializeField] private float maxTrackElevationDeviation = 0f;
    [SerializeField] private int elevationSmoothingPasses = 2;

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
    [Header("Car Spawn (Editor Tool)")]
    [Tooltip("How far behind Check1 to actually place the car -- spawning it exactly on the checkpoint risks it already overlapping the trigger before the race starts, so it can't register a fresh OnTriggerEnter once Racing begins.")]
    [SerializeField] private float carSpawnBackDistance = 6f;
    [Tooltip("Extra height above Check1's position to spawn the car at, so it doesn't spawn embedded in the ground -- checkpoints sit at road/ground height, not at the car's resting height above it.")]
    [SerializeField] private float carSpawnHeightOffset = 1f;

    // Editor-only convenience: move AND rotate the car to a proper spawn
    // point just behind Check1, facing toward Check2 -- no more hand-placing
    // the car for every new level. Re-run any time the checkpoint layout
    // changes (including right after duplicating a scene for a new level,
    // since the car is still sitting wherever the old track had it).
    [ContextMenu("Align Car To Track Start")]
    private void AlignCarToTrackStart()
    {
        if (playerCar == null || checkpoints.Count < 2 || checkpoints[0] == null || checkpoints[1] == null)
        {
            Debug.LogWarning("RaceManager: assign Player Car and at least 2 checkpoints before aligning.");
            return;
        }

        Vector3 direction = checkpoints[1].transform.position - checkpoints[0].transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("RaceManager: Check1 and Check2 are at the same position, can't infer a direction.");
            return;
        }
        direction.Normalize();

        Vector3 spawnPosition = checkpoints[0].transform.position - direction * carSpawnBackDistance;
        spawnPosition.y = checkpoints[0].transform.position.y + carSpawnHeightOffset;

        Transform carTransform = playerCar.transform;
        Undo.RecordObject(carTransform, "Align Car To Track Start");
        carTransform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
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

        List<Vector3> path = BuildFinalTrackPath();
        if (path.Count < 2)
        {
            Debug.LogWarning("RaceManager: couldn't build a track path.");
            return;
        }

        TrackGeometryGenerator.Generate(transform, path, trackWidth, barrierHeight, barrierThickness, roadMaterial, barrierMaterial, closedLoop, roadSurfaceTag);
    }

    // Includes finishLine (if assigned) as an extra point after the last
    // checkpoint -- used only for shaping the track curve. RegisterCheckpointPassed
    // and TotalCheckpoints are unaffected since they read the checkpoints
    // list directly, not this.
    private List<Transform> GetCheckpointTransforms()
    {
        var waypoints = new List<Transform>(checkpoints.Count + 1);
        foreach (Checkpoint checkpoint in checkpoints)
        {
            waypoints.Add(checkpoint != null ? checkpoint.transform : null);
        }
        if (finishLine != null)
        {
            waypoints.Add(finishLine.transform);
        }
        return waypoints;
    }

    // Path Nodes (if you've populated 3+) take over shaping the curve;
    // otherwise falls back to checkpoints + finish line, same as before
    // Path Nodes existed. This is the one place that decides which source
    // of truth the road shape comes from.
    private List<Transform> GetTrackShapeWaypoints()
    {
        var validNodes = new List<Transform>();
        foreach (Transform node in pathNodes)
        {
            if (node != null) validNodes.Add(node);
        }

        return validNodes.Count >= 3 ? validNodes : GetCheckpointTransforms();
    }

    // Editor-only: seeds Path Nodes from the current checkpoints (+ finish
    // line) as a starting point -- from here, freely add extra nodes
    // between existing ones, or drag any node, to fix a section of the
    // curve without touching checkpoint gameplay at all. Safe to re-run;
    // clears and regenerates rather than appending duplicates.
    [ContextMenu("Generate Path Nodes From Checkpoints")]
    private void GeneratePathNodesFromCheckpoints()
    {
        Transform existingRoot = transform.Find("PathNodes");
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot.gameObject);
        }

        GameObject nodesRoot = new GameObject("PathNodes");
        nodesRoot.transform.SetParent(transform);
        Undo.RegisterCreatedObjectUndo(nodesRoot, "Generate Path Nodes");

        pathNodes.Clear();
        List<Transform> source = GetCheckpointTransforms();
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] == null) continue;

            GameObject node = new GameObject($"Node_{i}");
            node.transform.SetParent(nodesRoot.transform);
            node.transform.position = source[i].position;
            Undo.RegisterCreatedObjectUndo(node, "Generate Path Nodes");
            pathNodes.Add(node.transform);
        }

        Debug.Log($"RaceManager: generated {pathNodes.Count} path node(s) from checkpoints. Add/move/remove nodes freely, then re-run Build Track Geometry -- checkpoint gameplay is untouched.");
    }

    // Editor-only: reverts to shaping the road directly from checkpoints --
    // deletes the PathNodes group and clears the list.
    [ContextMenu("Clear Path Nodes")]
    private void ClearPathNodes()
    {
        Transform existingRoot = transform.Find("PathNodes");
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot.gameObject);
        }
        pathNodes.Clear();
    }

    // The single source of truth for the track's path: smoothed curve
    // through Path Nodes (if set) or checkpoints otherwise, conformed to
    // the terrain (if assigned), then elevation-clamped/smoothed (if
    // enabled). Road geometry, checkpoint snapping, and pin snapping all
    // build from this exact path so they never end up at slightly
    // different heights -- or shapes -- from each other.
    private List<Vector3> BuildFinalTrackPath()
    {
        List<Transform> waypoints = GetTrackShapeWaypoints();
        List<Vector3> path = TrackGeometryGenerator.BuildSmoothPath(waypoints, trackSmoothness, closedLoop);

        if (terrain != null)
        {
            TrackGeometryGenerator.ConformPathToTerrain(path, terrain);

            if (maxTrackElevationDeviation > 0f)
            {
                TrackGeometryGenerator.ClampPathElevation(path, maxTrackElevationDeviation);
                TrackGeometryGenerator.SmoothPathElevation(path, elevationSmoothingPasses, closedLoop);
            }
        }

        return path;
    }

    // Editor-only: moves every checkpoint's Y position to match the final
    // track path's height at its XZ position (terrain-conformed and, if
    // enabled, elevation-clamped) -- keeps checkpoints sitting exactly on
    // the actual road surface instead of raw, possibly-bumpier terrain
    // height. Re-run any time the terrain or checkpoint layout changes.
    [ContextMenu("Snap Checkpoints To Terrain")]
    private void SnapCheckpointsToTerrain()
    {
        if (terrain == null)
        {
            Debug.LogWarning("RaceManager: assign a Terrain reference before snapping checkpoints.");
            return;
        }

        List<Vector3> path = BuildFinalTrackPath();
        if (path.Count < 2)
        {
            Debug.LogWarning("RaceManager: couldn't build a track path to snap to.");
            return;
        }

        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint == null) continue;

            TrackGeometryGenerator.GetClosestPointOnPath(path, checkpoint.transform.position, out Vector3 closest, out _, closedLoop);
            Vector3 position = checkpoint.transform.position;
            position.y = closest.y;

            Undo.RecordObject(checkpoint.transform, "Snap Checkpoints To Terrain");
            checkpoint.transform.position = position;
        }

        if (finishLine != null)
        {
            TrackGeometryGenerator.GetClosestPointOnPath(path, finishLine.transform.position, out Vector3 closestToFinish, out _, closedLoop);
            Vector3 finishPosition = finishLine.transform.position;
            finishPosition.y = closestToFinish.y;

            Undo.RecordObject(finishLine.transform, "Snap Checkpoints To Terrain");
            finishLine.transform.position = finishPosition;
        }

        Debug.Log($"RaceManager: snapped {checkpoints.Count} checkpoint(s) to terrain height.");
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
            // On a point-to-point course, the very first and last checkpoints
            // have no real "incoming"/"outgoing" segment to compare (nothing
            // wraps around) -- skip corner detection there rather than
            // comparing against a bogus wrapped neighbor from the other end
            // of the course.
            if (!closedLoop && (i == 0 || i == checkpoints.Count - 1)) continue;

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

        List<Vector3> path = BuildFinalTrackPath();
        if (path.Count < 2)
        {
            Debug.LogWarning("RaceManager: couldn't build a track path to snap to.");
            return;
        }

        PinFormationSpawner[] spawners = FindObjectsByType<PinFormationSpawner>(FindObjectsSortMode.None);
        foreach (PinFormationSpawner spawner in spawners)
        {
            TrackGeometryGenerator.GetClosestPointOnPath(path, spawner.transform.position, out Vector3 closest, out Vector3 forward, closedLoop);

            Undo.RecordObject(spawner.transform, "Snap Pin Cluster To Track");
            spawner.transform.SetPositionAndRotation(closest, Quaternion.LookRotation(forward, Vector3.up));
            spawner.SpawnFormation();
        }

        Debug.Log($"RaceManager: snapped {spawners.Length} pin cluster(s) onto the track.");
    }

    // Builds four invisible (collider-only, no renderer) walls around the
    // assigned Terrain's actual bounds so the car physically can't drive off
    // the edge into the void. Reads the terrain's real size/position, so
    // this works correctly regardless of how big or where each level's
    // terrain ends up being -- re-run any time the terrain is resized.
    [ContextMenu("Build Terrain Boundary Walls")]
    private void BuildTerrainBoundaryWalls()
    {
        if (terrain == null)
        {
            Debug.LogWarning("RaceManager: assign a Terrain reference before building boundary walls.");
            return;
        }

        Transform existing = transform.Find("BoundaryWalls");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject wallsRoot = new GameObject("BoundaryWalls");
        wallsRoot.transform.SetParent(transform);
        Undo.RegisterCreatedObjectUndo(wallsRoot, "Build Terrain Boundary Walls");

        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        const float wallHeight = 20f;
        const float wallThickness = 4f;
        float centerY = origin.y + wallHeight * 0.5f;

        // South/North walls run along X, sitting at the min/max Z edge.
        CreateBoundaryWall(wallsRoot.transform, "Boundary_South",
            new Vector3(origin.x + size.x * 0.5f, centerY, origin.z),
            new Vector3(size.x + wallThickness * 2f, wallHeight, wallThickness));
        CreateBoundaryWall(wallsRoot.transform, "Boundary_North",
            new Vector3(origin.x + size.x * 0.5f, centerY, origin.z + size.z),
            new Vector3(size.x + wallThickness * 2f, wallHeight, wallThickness));

        // West/East walls run along Z, sitting at the min/max X edge.
        CreateBoundaryWall(wallsRoot.transform, "Boundary_West",
            new Vector3(origin.x, centerY, origin.z + size.z * 0.5f),
            new Vector3(wallThickness, wallHeight, size.z + wallThickness * 2f));
        CreateBoundaryWall(wallsRoot.transform, "Boundary_East",
            new Vector3(origin.x + size.x, centerY, origin.z + size.z * 0.5f),
            new Vector3(wallThickness, wallHeight, size.z + wallThickness * 2f));

        Debug.Log("RaceManager: built invisible boundary walls around the terrain.");
    }

    private void CreateBoundaryWall(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.tag = "Barrier"; // driving into it counts as a crash, same as any other barrier

        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
        // Deliberately no MeshRenderer/MeshFilter -- a collider with
        // nothing to render is invisible by construction.

        Undo.RegisterCreatedObjectUndo(wall, "Build Terrain Boundary Walls");
    }
#endif
}
