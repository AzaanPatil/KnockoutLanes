using UnityEngine;
using UnityEngine.Events;

// One trigger volume along the course. Place these in driving order around
// the oval and assign them to RaceManager's checkpoint list in that same
// order -- RaceManager enforces sequencing.
//
// OnCheckpointReached is a free hook for anything extra you want when a
// checkpoint is legitimately passed (a ping sound, a flash, a UI pulse) --
// wire it up in the Inspector without touching this script.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    public UnityEvent OnCheckpointReached = new UnityEvent();

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (RaceManager.Instance.RegisterCheckpointPassed(this))
        {
            OnCheckpointReached.Invoke();
        }
    }
}
