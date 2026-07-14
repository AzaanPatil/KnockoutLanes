#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Shared by Checkpoint and FinishLine to stamp a pair of vertical light
// beams marking a trigger gate's left/right edges, Forza-Horizon style --
// purely a level-design aid so gates are visible while laying out a track.
// Editor-only; nothing about this runs or costs anything at runtime.
public static class BeamMarkerGenerator
{
    public static void Generate(Transform parent, BoxCollider box, Material material, float height, float thickness)
    {
        Clear(parent);

        float halfWidth = box.size.x * 0.5f;
        CreateBeam(parent, "BeamLeft", -halfWidth, material, height, thickness);
        CreateBeam(parent, "BeamRight", halfWidth, material, height, thickness);
    }

    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == "BeamLeft" || child.name == "BeamRight")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
    }

    private static void CreateBeam(Transform parent, string name, float localX, Material material, float height, float thickness)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = name;
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(parent);
        beam.transform.localPosition = new Vector3(localX, height * 0.5f, 0f);
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = new Vector3(thickness, height * 0.5f, thickness);

        if (material != null)
        {
            beam.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        Undo.RegisterCreatedObjectUndo(beam, "Generate Beam Marker");
    }
}
#endif
