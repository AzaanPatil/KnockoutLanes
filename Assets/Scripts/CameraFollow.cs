using UnityEngine;

// Third-person chase camera (GDD 6.1). Runs in LateUpdate so it reacts
// after the car has finished moving for the frame, avoiding jitter.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -8f);
    [SerializeField] private float positionSmoothTime = 0.15f;
    [SerializeField] private float rotationSmoothSpeed = 6f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        Vector3 lookPoint = target.position + Vector3.up;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}
