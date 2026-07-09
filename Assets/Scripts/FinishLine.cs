using UnityEngine;

// Trigger volume that ends the race, but only once every Checkpoint has
// been passed in order -- see RaceManager.TryFinishRace.
[RequireComponent(typeof(Collider))]
public class FinishLine : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        RaceManager.Instance.TryFinishRace();
    }
}
