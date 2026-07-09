using UnityEngine;
 
// Custom arcade-style vehicle controller built on Unity's built-in WheelCollider
// physics component (not a downloaded asset). Rear-wheel drive, with a handbrake
// on Space that both brakes the rear wheels and loosens their sideways grip to
// produce a drift, matching the Knockout Lanes GDD control scheme.
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider wheelColliderFL;
    [SerializeField] private WheelCollider wheelColliderFR;
    [SerializeField] private WheelCollider wheelColliderRL;
    [SerializeField] private WheelCollider wheelColliderRR;
 
    [Header("Wheel Meshes (visual only, no physics)")]
    [SerializeField] private Transform wheelMeshFL;
    [SerializeField] private Transform wheelMeshFR;
    [SerializeField] private Transform wheelMeshRL;
    [SerializeField] private Transform wheelMeshRR;
 
    [Header("Driving Settings")]
    [SerializeField] private float maxMotorTorque = 1500f;
    [SerializeField] private float maxSteerAngle = 30f;
 
    [Header("Handbrake / Drift Settings")]
    [SerializeField] private float handbrakeBrakeTorque = 5000f;
    [SerializeField] private float handbrakeSidewaysStiffness = 0.5f;
 
    [Header("Stability")]
    [Tooltip("Lowers the Rigidbody's center of mass relative to its default, which helps stop the car from tipping over during hard turns or pin collisions.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
 
    // Gated off by RaceManager during the pre-race countdown so the car
    // can't jump the start.
    public bool CanDrive { get; set; } = true;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool handbrakeInput;
    private float defaultRearSidewaysStiffness;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;
 
        // Cache the rear wheels' default sideways grip so we can restore it
        // once the handbrake is released.
        defaultRearSidewaysStiffness = wheelColliderRL.sidewaysFriction.stiffness;
    }
 
    private void Update()
    {
        // Read input every frame (not FixedUpdate) so quick taps of a key
        // between physics steps don't get missed.
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");
        handbrakeInput = Input.GetKey(KeyCode.Space);
 
        UpdateWheelVisual(wheelColliderFL, wheelMeshFL);
        UpdateWheelVisual(wheelColliderFR, wheelMeshFR);
        UpdateWheelVisual(wheelColliderRL, wheelMeshRL);
        UpdateWheelVisual(wheelColliderRR, wheelMeshRR);
    }
 
    private void FixedUpdate()
    {
        if (!CanDrive)
        {
            HoldForCountdown();
            return;
        }

        HandleMotor();
        HandleSteering();
        HandleHandbrake();
    }

    private void HoldForCountdown()
    {
        wheelColliderRL.motorTorque = 0f;
        wheelColliderRR.motorTorque = 0f;
        wheelColliderFL.brakeTorque = handbrakeBrakeTorque;
        wheelColliderFR.brakeTorque = handbrakeBrakeTorque;
        wheelColliderRL.brakeTorque = handbrakeBrakeTorque;
        wheelColliderRR.brakeTorque = handbrakeBrakeTorque;
    }
 
    private void HandleMotor()
    {
        float motorTorque = verticalInput * maxMotorTorque;
 
        // Rear-wheel drive. If the car feels underpowered once you're testing
        // with real weight/scale, you can also apply a (smaller) torque to the
        // front pair for all-wheel drive.
        wheelColliderRL.motorTorque = motorTorque;
        wheelColliderRR.motorTorque = motorTorque;
    }
 
    private void HandleSteering()
    {
        float steerAngle = horizontalInput * maxSteerAngle;
 
        wheelColliderFL.steerAngle = steerAngle;
        wheelColliderFR.steerAngle = steerAngle;
    }
 
    private void HandleHandbrake()
    {
        WheelFrictionCurve rearFriction = wheelColliderRL.sidewaysFriction;
 
        if (handbrakeInput)
        {
            // Lock the rear brakes and loosen rear sideways grip so the back
            // end steps out — this combination is what produces the drift.
            wheelColliderRL.brakeTorque = handbrakeBrakeTorque;
            wheelColliderRR.brakeTorque = handbrakeBrakeTorque;
            rearFriction.stiffness = handbrakeSidewaysStiffness;
        }
        else
        {
            wheelColliderRL.brakeTorque = 0f;
            wheelColliderRR.brakeTorque = 0f;
            rearFriction.stiffness = defaultRearSidewaysStiffness;
        }
 
        wheelColliderRL.sidewaysFriction = rearFriction;
        wheelColliderRR.sidewaysFriction = rearFriction;
    }
 
    private void UpdateWheelVisual(WheelCollider collider, Transform wheelMesh)
    {
        if (wheelMesh == null) return;
 
        // WheelCollider has no mesh of its own — GetWorldPose() gives us where
        // it actually is after suspension/physics, so we can move a visual
        // wheel mesh to match it every frame.
        collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
        wheelMesh.SetPositionAndRotation(position, rotation);
    }
}
 