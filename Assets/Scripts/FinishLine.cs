using UnityEngine;
using UnityEngine.Events;

// Trigger volume that ends the race, but only once every Checkpoint has
// been passed in order -- see RaceManager.TryFinishRace.
//
// OnFinished and OnIncomplete are free hooks for feedback: e.g. a fanfare
// on a real finish, or a "not yet!" sound if the player crosses the line
// early. Wire either in the Inspector -- neither is required.
[RequireComponent(typeof(BoxCollider))]
public class FinishLine : MonoBehaviour
{
    public UnityEvent OnFinished = new UnityEvent();
    public UnityEvent OnIncomplete = new UnityEvent();

    [Header("Visual Marker")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private float beamHeight = 25f;
    [SerializeField] private float beamThickness = 0.3f;

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
        if (!other.CompareTag("Player")) return;

        if (RaceManager.Instance.TryFinishRace())
        {
            OnFinished.Invoke();
        }
        else if (RaceManager.Instance.CurrentState == RaceManager.RaceState.Racing)
        {
            OnIncomplete.Invoke();
        }
    }
}
