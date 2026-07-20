using UnityEngine;
using UnityEngine.Events;

// One trigger volume along the course. Place these in driving order around
// the oval and assign them to RaceManager's checkpoint list in that same
// order -- RaceManager enforces sequencing.
//
// OnCheckpointReached is a free hook for anything extra you want when a
// checkpoint is legitimately passed (a ping sound, a flash, a UI pulse) --
// wire it up in the Inspector without touching this script.
[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    public UnityEvent OnCheckpointReached = new UnityEvent();

    [Header("Visual Marker")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private float beamHeight = 25f;
    [SerializeField] private float beamThickness = 0.3f;

    [SerializeField] private AudioClip passedSfx;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Beam Markers")]
    private void GenerateBeamMarkers()
    {
        BeamMarkerGenerator.Generate(transform, GetComponent<BoxCollider>(), beamMaterial, beamHeight, beamThickness);
    }

    [ContextMenu("Clear Beam Markers")]
    private void ClearBeamMarkers()
    {
        BeamMarkerGenerator.Clear(transform);
    }
#endif

    private void OnTriggerEnter(Collider other)
    {
        // Check the attached Rigidbody's tag, not the individual collider's --
        // the car's actual colliders live on child objects (Body, wheels)
        // which aren't themselves tagged, only the root Car object is.
        if (other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag("Player")) return;

        if (RaceManager.Instance.RegisterCheckpointPassed(this))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(passedSfx);
            }

            OnCheckpointReached.Invoke();
        }
    }
}
