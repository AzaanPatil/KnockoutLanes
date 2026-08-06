using UnityEngine;

// Third-person chase camera (GDD 6.1). Runs in LateUpdate so it reacts
// after the car has finished moving for the frame, avoiding jitter.
//
// Target can be set in the Inspector for a fixed scene, or reassigned at
// runtime via SetTarget -- useful once cars are spawned from a prefab
// rather than placed by hand.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -8f);
    [SerializeField] private float positionSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothSpeed = 6f;

    private Vector3 velocity;
    private float sensitivity = 1f;

    public void SetTarget(Transform newTarget) => target = newTarget;

    private void Start()
    {
        // Higher sensitivity = camera reacts faster (less smoothing time,
        // faster rotation catch-up), matching what a player expects from a
        // "sensitivity" slider.
        sensitivity = Mathf.Max(0.1f, GameSettings.CameraSensitivity);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.TransformPoint(offset);
        float effectiveSmoothTime = positionSmoothTime / sensitivity;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, effectiveSmoothTime);

        Vector3 lookPoint = target.position + Vector3.up;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * sensitivity * Time.deltaTime);
    }
}
