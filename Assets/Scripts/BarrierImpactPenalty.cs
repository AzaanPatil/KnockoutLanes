using UnityEngine;

// Immediately resets the style multiplier back to 1.0 when the car hits a
// track barrier -- crashing isn't stylish, whatever combo was built up
// gets wiped instantly rather than left to decay away naturally.
public class BarrierImpactPenalty : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Barrier")) return;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetStyleMultiplier();
        }
    }
}
