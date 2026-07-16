#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Generates a closed-loop road (flattened box segments) with solid barrier
// walls along both edges, connecting a sequence of waypoints in order.
// Editor-only level-design aid -- replaces a flat placeholder ground plane
// with real track geometry and a physical boundary, without needing to
// hand-model a curve in ProBuilder. Re-run any time the waypoints move.
public static class TrackGeometryGenerator
{
    public static void Generate(Transform parent, IReadOnlyList<Transform> waypoints, float trackWidth, float barrierHeight, float barrierThickness, Material roadMaterial, Material barrierMaterial)
    {
        Clear(parent);

        if (waypoints.Count < 2) return;

        GameObject trackRoot = new GameObject("Track");
        trackRoot.transform.SetParent(parent);
        trackRoot.transform.localPosition = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(trackRoot, "Build Track Geometry");

        for (int i = 0; i < waypoints.Count; i++)
        {
            Transform from = waypoints[i];
            Transform to = waypoints[(i + 1) % waypoints.Count];
            if (from == null || to == null) continue;

            BuildSegment(trackRoot.transform, from.position, to.position, i, trackWidth, barrierHeight, barrierThickness, roadMaterial, barrierMaterial);
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

    private static void BuildSegment(Transform parent, Vector3 from, Vector3 to, int index, float trackWidth, float barrierHeight, float barrierThickness, Material roadMaterial, Material barrierMaterial)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length < 0.01f) return;
        direction.Normalize();

        Vector3 midpoint = (from + to) * 0.5f;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Extend segments a bit past their true length so consecutive
        // segments overlap at corners instead of leaving visible gaps.
        float overlap = trackWidth * 0.5f;
        float segmentLength = length + overlap;

        CreateBox(parent, $"Road_{index}", midpoint + Vector3.up * 0.05f, rotation,
            new Vector3(trackWidth, 0.1f, segmentLength), roadMaterial);

        CreateBarrier(parent, midpoint, rotation, segmentLength, trackWidth * 0.5f, barrierHeight, barrierThickness, index, "L", barrierMaterial);
        CreateBarrier(parent, midpoint, rotation, segmentLength, -trackWidth * 0.5f, barrierHeight, barrierThickness, index, "R", barrierMaterial);
    }

    private static void CreateBarrier(Transform parent, Vector3 segmentCenter, Quaternion rotation, float length, float sideOffset, float barrierHeight, float barrierThickness, int index, string side, Material material)
    {
        Vector3 localOffset = new Vector3(sideOffset, barrierHeight * 0.5f, 0f);
        Vector3 position = segmentCenter + rotation * localOffset;

        GameObject barrier = CreateBox(parent, $"Barrier_{index}_{side}", position, rotation,
            new Vector3(barrierThickness, barrierHeight, length), material);

        // Tagged so BarrierImpactPenalty (on the car) can recognize a
        // barrier hit specifically, distinct from hitting a pin or another
        // Rigidbody. Requires a "Barrier" tag to already exist in
        // Project Settings -> Tags and Layers.
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
