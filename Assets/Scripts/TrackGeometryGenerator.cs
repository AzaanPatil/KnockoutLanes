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
    // Takes an already-built path (see BuildSmoothPath / ConformPathToTerrain
    // / ClampPathElevation) rather than building one internally, so callers
    // can apply the exact same path -- terrain-conformed, elevation-clamped,
    // whatever -- to the road, checkpoints, and pins consistently instead of
    // each one computing a slightly different version.
    public static void Generate(Transform parent, List<Vector3> path, float trackWidth, float barrierHeight, float barrierThickness, Material roadMaterial, Material barrierMaterial, bool closedLoop = true, string roadTag = null)
    {
        Clear(parent);

        if (path.Count < 2) return;

        GameObject trackRoot = new GameObject("Track");
        trackRoot.transform.SetParent(parent);
        trackRoot.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(trackRoot, "Build Track Geometry");

        // Point-to-point (open) tracks stop one segment short -- no wrap-
        // around connecting the last point back to the first.
        int segmentCount = closedLoop ? path.Count : path.Count - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 from = path[i];
            Vector3 to = path[(i + 1) % path.Count];
            BuildSegment(trackRoot.transform, from, to, i, trackWidth, barrierHeight, barrierThickness, roadMaterial, barrierMaterial, roadTag);
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
    // reuse the exact same curve the road itself is built from. closedLoop
    // = false gives a point-to-point path: no segment wraps from the last
    // waypoint back to the first, and the end points reuse themselves as
    // their own "phantom" spline control point instead of wrapping around
    // (the standard trick for an open Catmull-Rom curve).
    public static List<Vector3> BuildSmoothPath(IReadOnlyList<Transform> waypoints, int subdivisionsPerSegment, bool closedLoop = true)
    {
        var path = new List<Vector3>();
        int count = waypoints.Count;
        int segmentCount = closedLoop ? count : count - 1;

        for (int i = 0; i < segmentCount; i++)
        {
            Transform p0t = waypoints[WrapOrClampIndex(i - 1, count, closedLoop)];
            Transform p1t = waypoints[WrapOrClampIndex(i, count, closedLoop)];
            Transform p2t = waypoints[WrapOrClampIndex(i + 1, count, closedLoop)];
            Transform p3t = waypoints[WrapOrClampIndex(i + 2, count, closedLoop)];
            if (p0t == null || p1t == null || p2t == null || p3t == null) continue;

            Vector3 p0 = p0t.position, p1 = p1t.position, p2 = p2t.position, p3 = p3t.position;

            // The final segment of an open path needs to sample all the way
            // to t = 1 to actually reach the last waypoint -- for a closed
            // loop that point is naturally covered by segment 0's t = 0.
            bool includeEndpoint = !closedLoop && i == segmentCount - 1;
            int sampleCount = includeEndpoint ? subdivisionsPerSegment + 1 : subdivisionsPerSegment;

            for (int s = 0; s < sampleCount; s++)
            {
                float t = s / (float)subdivisionsPerSegment;
                path.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        return path;
    }

    private static int WrapOrClampIndex(int index, int count, bool wrap)
    {
        return wrap ? (index % count + count) % count : Mathf.Clamp(index, 0, count - 1);
    }

    // Nearest point on the path to an arbitrary position, plus the path's
    // forward direction there -- lets callers both reposition something
    // onto the track and orient it sensibly along the driving direction.
    // For an open path's very last point, "forward" continues the direction
    // arriving from the previous point rather than wrapping to point 0.
    public static void GetClosestPointOnPath(List<Vector3> path, Vector3 position, out Vector3 closestPoint, out Vector3 forwardDirection, bool closedLoop = true)
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

        Vector3 forward;
        bool isOpenPathEnd = !closedLoop && closestIndex == path.Count - 1;
        if (isOpenPathEnd)
        {
            Vector3 prevPoint = path[Mathf.Max(closestIndex - 1, 0)];
            forward = closestPoint - prevPoint;
        }
        else
        {
            Vector3 next = path[(closestIndex + 1) % path.Count];
            forward = next - closestPoint;
        }

        forward.y = 0f;
        forwardDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    // Public so RaceManager's checkpoint-snapping tool can sample the same
    // terrain the road itself conforms to.
    public static float SampleTerrainHeight(Terrain terrain, Vector3 worldPosition)
    {
        return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
    }

    // Public so RaceManager's checkpoint-snapping tool can reuse the exact
    // same conforming logic the road path uses.
    public static void ConformPathToTerrain(List<Vector3> path, Terrain terrain)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i];
            point.y = SampleTerrainHeight(terrain, point);
            path[i] = point;
        }
    }

    // Clamps every point's height to within maxDeviation of the path's
    // average height -- keeps the track relatively flat even if the terrain
    // underneath has bumps/dips, without needing to hand-flatten the ground.
    // Pass maxDeviation <= 0 to skip (e.g. Mountain Course, which wants the
    // road to actually follow real elevation).
    public static void ClampPathElevation(List<Vector3> path, float maxDeviation)
    {
        if (path.Count == 0 || maxDeviation <= 0f) return;

        float averageHeight = 0f;
        foreach (Vector3 point in path)
        {
            averageHeight += point.y;
        }
        averageHeight /= path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 point = path[i];
            point.y = Mathf.Clamp(point.y, averageHeight - maxDeviation, averageHeight + maxDeviation);
            path[i] = point;
        }
    }

    // Averages each point's height with its neighbors, a few passes --
    // smooths over the sharp steps that clamping alone can introduce at the
    // boundary between "was clamped" and "wasn't." closedLoop = false keeps
    // the two ends from being smoothed against each other, since they're
    // not actually adjacent on a point-to-point track.
    public static void SmoothPathElevation(List<Vector3> path, int passes, bool closedLoop = true)
    {
        if (path.Count < 3) return;

        for (int pass = 0; pass < passes; pass++)
        {
            var smoothedHeights = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                float prevY = path[WrapOrClampIndex(i - 1, path.Count, closedLoop)].y;
                float currentY = path[i].y;
                float nextY = path[WrapOrClampIndex(i + 1, path.Count, closedLoop)].y;
                smoothedHeights[i] = (prevY + currentY + nextY) / 3f;
            }

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 point = path[i];
                point.y = smoothedHeights[i];
                path[i] = point;
            }
        }
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

    private static void BuildSegment(Transform parent, Vector3 from, Vector3 to, int index, float trackWidth, float barrierHeight, float barrierThickness, Material roadMaterial, Material barrierMaterial, string roadTag = null)
    {
        // Uses the full 3D direction (not flattened to horizontal) so each
        // segment tilts to match the actual slope between its two points --
        // on relatively flat courses this is indistinguishable from the old
        // horizontal-only behavior, but on real elevation (Mountain Course)
        // flattening it produced a "staircase of level shingles" instead of
        // a ramp that hugs the incline.
        Vector3 direction = to - from;
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

        GameObject road = CreateBox(parent, $"Road_{index}", midpoint + Vector3.up * 0.05f, rotation,
            new Vector3(trackWidth, 0.1f, length + roadOverlap), roadMaterial);
        if (!string.IsNullOrEmpty(roadTag))
        {
            road.tag = roadTag;
        }

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
