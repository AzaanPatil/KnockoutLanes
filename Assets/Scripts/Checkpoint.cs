using UnityEngine;

// One trigger volume along the course. Place these in driving order around
// the oval and assign them to RaceManager's checkpoint list in that same
// order -- RaceManager enforces sequencing, this component just reports
// "the player reached me."
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        RaceManager.Instance.RegisterCheckpointPassed(this);
    }
}
