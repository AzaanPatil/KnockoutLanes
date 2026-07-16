#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Generates a closed-loop road (flattened box segments) with solid barrier
// walls along both edges, following a smoothed curve through a sequence of
// waypoints. Editor-only level-design aid -- replaces a flat placeholder
// ground plane with real track geometry and a physical boundary, without
// needing to hand-model a curve in ProBuilder. Re-run any time the
// waypoints move.
//
// The waypoints (checkpoints) are used as spline control points (Catmull-
// Rom) rather than being connected with straight lines -- with only a
// handful of checkpoints, straight segments make the track look like a
// polygon instead of an oval. Smoothing is purely geometric; it doesn't
// change checkpoint gameplay at all.
public static class TrackGeometryGenerator
{
    public static void Generate(Transform parent, IReadOnlyList<Transform> waypoints, float trackWidth, float barrierHeight, float barrierThickness, int subdivisionsPerSegment, Material roadMaterial, Material barrierMaterial)
    {
        Clear(parent);

        if (waypoints.Count < 3) return;

        List<Vector3> path = BuildSmoothPath(waypoints, Mathf.Max(1, subdivisionsPerSegment));
        if (path.Count < 2) return;

        GameObject trackRoot = new GameObject("Track");
        trackRoot.transform.SetParent(parent);
        trackRoot.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(trackRoot, "Build Track Geometry");

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 from = path[i];
            Vector3 to = path[(i + 1) % path.Count];
            BuildSegment(trackRoot.transform, from, to, i, trackWidth, barrierHeight, barrierThickness, roadMaterial, barrierMaterial);
        }
    }

    public static void Clear(Transform parent)
    {
        Transform existing = parent.Find("Track");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }

    // Public so other tools (e.g. RaceManager's pin-cluster snapping) can
    // reuse the exact same curve the road itself is built from.
    public static List<Vector3> BuildSmoothPath(IReadOnlyList<Transform> waypoints, int subdivisionsPerSegment)
    {
        var path = new List<Vector3>();
        int count = waypoints.Count;

        for (int i = 0; i < count; i++)
        {
            Transform p0t = waypoints[(i - 1 + count) % count];
            Transform p1t = waypoints[i];
            Transform p2t = waypoints[(i + 1) % count];
            Transform p3t = waypoints[(i + 2) % count];
            if (p0t == null || p1t == null || p2t == null || p3t == null) continue;

            Vector3 p0 = p0t.position, p1 = p1t.position, p2 = p2t.position, p3 = p3t.position;

            for (int s = 0; s < subdivisionsPerSegment; s++)
            {
                float t = s / (float)subdivisionsPerSegment;
                path.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        return path;
    }

    // Nearest point on the path to an arbitrary position, plus the path's
    // forward direction there (direction to the next path point) -- lets
    // callers both reposition something onto the track and orient it
    // sensibly along the driving direction.
    public static void GetClosestPointOnPath(List<Vector3> path, Vector3 position, out Vector3 closestPoint, out Vector3 forwardDirection)
    {
        int closestIndex = 0;
        float closestDistSqr = (path[0] - position).sqrMagnitude;

        for (int i = 1; i < path.Count; i++)
        {
            float distSqr = (path[i] - position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestIndex = i;
            }
        }

        closestPoint = path[closestIndex];

        Vector3 next = path[(closestIndex + 1) % path.Count];
        Vector3 forward = next - closestPoint;
        forward.y = 0f;
        forwardDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static void BuildSegment(Transform parent, Vector3 from, Vector3 to, int index, float trackWidth, float barrierHeight, float barrierThickness, Material roadMaterial, Material barrierMaterial)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length < 0.01f) return;
        direction.Normalize();

        Vector3 midpoint = (from + to) * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Both road and barriers get a small length overlap so consecutive
        // segments blend together instead of leaving gaps. The road can
        // take a generous overlap with no visual cost; barriers get a much
        // smaller one -- a large overlap on a thin vertical wall bulges
        // visibly at every bend once the curve is finely subdivided, which
        // reads as a lumpy "spine" instead of a smooth rail.
        float roadOverlap = trackWidth * 0.5f;
        float barrierOverlap = barrierThickness * 2f;

        CreateBox(parent, $"Road_{index}", midpoint + Vector3.up * 0.05f, rotation,
            new Vector3(trackWidth, 0.1f, length + roadOverlap), roadMaterial);

        CreateBarrierBox(parent, midpoint, rotation, length + barrierOverlap, trackWidth * 0.5f, barrierHeight, barrierThickness, index, "L", barrierMaterial);
        CreateBarrierBox(parent, midpoint, rotation, length + barrierOverlap, -trackWidth * 0.5f, barrierHeight, barrierThickness, index, "R", barrierMaterial);
    }

    private static void CreateBarrierBox(Transform parent, Vector3 segmentCenter, Quaternion rotation, float length, float sideOffset, float barrierHeight, float barrierThickness, int index, string side, Material material)
    {
        Vector3 localOffset = new Vector3(sideOffset, barrierHeight * 0.5f, 0f);
        Vector3 position = segmentCenter + rotation * localOffset;

        GameObject barrier = CreateBox(parent, $"Barrier_{index}_{side}", position, rotation,
            new Vector3(barrierThickness, barrierHeight, length), material);

        // Tagged so BarrierImpactPenalty (on the car) can recognize a
        // barrier hit specifically. Requires a "Barrier" tag to already
        // exist in Project Settings -> Tags and Layers.
        barrier.tag = "Barrier";
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent);
        box.transform.SetPositionAndRotation(position, rotation);
        box.transform.localScale = scale;

        if (material != null)
        {
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        Undo.RegisterCreatedObjectUndo(box, "Build Track Geometry");
        return box;
    }
}
#endif
