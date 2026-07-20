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

    [Header("Coasting")]
    [Tooltip("Mild brake applied to the driven wheels when there's no throttle input, so the car coasts down instead of holding speed indefinitely.")]
    [SerializeField] private float engineBrakeTorque = 300f;
 
    [Header("Stability")]
    [Tooltip("Lowers the Rigidbody's center of mass relative to its default, which helps stop the car from tipping over during hard turns or pin collisions.")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    [Header("Drift Detection")]
    [Tooltip("Minimum speed before the handbrake counts as an actual drift rather than just sitting still holding it.")]
    [SerializeField] private float driftMinSpeed = 3f;
    [Tooltip("Minimum angle between the car's facing and its actual velocity to count as a genuine sideways slide.")]
    [SerializeField] private float driftSlipAngleThreshold = 15f;

    [Header("Off-Track Handling")]
    [Tooltip("Tag used to mark ground outside the track (e.g. the grass plane). A wheel touching it loses grip and adds drag.")]
    [SerializeField] private string offTrackTag = "OffTrack";
    [Range(0f, 1f)]
    [Tooltip("Grip multiplier applied to a wheel's forward/sideways friction while it's off-track. Lower = more slippery.")]
    [SerializeField] private float offTrackGripMultiplier = 0.4f;
    [Tooltip("Extra Rigidbody linear drag added per wheel currently off-track -- this is what makes off-track \"slow you down significantly,\" separate from the grip loss.")]
    [SerializeField] private float offTrackDragPerWheel = 1.5f;

    // Gated off by RaceManager during the pre-race countdown so the car
    // can't jump the start.
    public bool CanDrive { get; set; } = true;

    // True only while the handbrake is held AND the car is genuinely
    // sliding sideways (not just sitting still or braking in a straight
    // line). Read by DriftStyleTracker to build the style multiplier.
    public bool IsDrifting { get; private set; }

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool handbrakeInput;
    private float defaultSidewaysStiffness;
    private float defaultForwardStiffness;
    private float baseLinearDamping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;

        // Cache the wheels' default friction (assumes all four start with
        // the same values) so off-track handling and the handbrake can each
        // compute a fresh multiplier every frame instead of compounding on
        // top of whatever value happened to be set last frame.
        defaultSidewaysStiffness = wheelColliderFL.sidewaysFriction.stiffness;
        defaultForwardStiffness = wheelColliderFL.forwardFriction.stiffness;
        baseLinearDamping = rb.linearDamping;
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
        UpdateDriftState();
        UpdateOffTrackHandling();
    }

    private void HoldForCountdown()
    {
        // Let the driven rear wheels spin freely under throttle for a
        // burnout, but lock the front wheels with a hard brake so the car
        // doesn't actually creep forward before the race starts. Only the
        // front pair is braked (not all four) so this doesn't reintroduce
        // the four-wheel brake lock that used to fight the suspension while
        // it was still settling right after spawn.
        wheelColliderRL.motorTorque = verticalInput * maxMotorTorque;
        wheelColliderRR.motorTorque = verticalInput * maxMotorTorque;
        wheelColliderFL.brakeTorque = handbrakeBrakeTorque;
        wheelColliderFR.brakeTorque = handbrakeBrakeTorque;
    }
 
    private void HandleMotor()
    {
        float motorTorque = verticalInput * maxMotorTorque;

        // Rear-wheel drive. If the car feels underpowered once you're testing
        // with real weight/scale, you can also apply a (smaller) torque to the
        // front pair for all-wheel drive.
        wheelColliderRL.motorTorque = motorTorque;
        wheelColliderRR.motorTorque = motorTorque;

        // Release the front brakes here in case HoldForCountdown() locked
        // them for a countdown burnout -- otherwise they'd stay locked
        // forever once normal driving resumes.
        wheelColliderFL.brakeTorque = 0f;
        wheelColliderFR.brakeTorque = 0f;

        // Coast down instead of holding speed indefinitely when the throttle
        // is released. HandleHandbrake() overrides this with a much stronger
        // brake if the handbrake is also held.
        bool noThrottleInput = Mathf.Approximately(verticalInput, 0f);
        float coastBrakeTorque = noThrottleInput ? engineBrakeTorque : 0f;
        wheelColliderRL.brakeTorque = coastBrakeTorque;
        wheelColliderRR.brakeTorque = coastBrakeTorque;
    }
 
    private void HandleSteering()
    {
        float steerAngle = horizontalInput * maxSteerAngle;
 
        wheelColliderFL.steerAngle = steerAngle;
        wheelColliderFR.steerAngle = steerAngle;
    }
 
    private void HandleHandbrake()
    {
        // Rear sideways grip while the handbrake is held is now computed in
        // UpdateOffTrackHandling() (it needs to combine with the off-track
        // grip multiplier), so this just handles the brake lock.
        if (handbrakeInput)
        {
            // Lock the rear brakes -- combined with the loosened rear
            // sideways grip, this is what produces the drift.
            wheelColliderRL.brakeTorque = handbrakeBrakeTorque;
            wheelColliderRR.brakeTorque = handbrakeBrakeTorque;
        }
        // When not held, brakeTorque is already whatever HandleMotor() set
        // (0 while accelerating, engineBrakeTorque while coasting).
    }
 
    private void UpdateDriftState()
    {
        if (!handbrakeInput)
        {
            IsDrifting = false;
            return;
        }

        Vector3 horizontalVelocity = rb.linearVelocity;
        horizontalVelocity.y = 0f;

        if (horizontalVelocity.magnitude < driftMinSpeed)
        {
            IsDrifting = false;
            return;
        }

        float slipAngle = Vector3.Angle(transform.forward, horizontalVelocity);
        IsDrifting = slipAngle > driftSlipAngleThreshold;
    }

    private void UpdateOffTrackHandling()
    {
        bool offFL = IsWheelOffTrack(wheelColliderFL);
        bool offFR = IsWheelOffTrack(wheelColliderFR);
        bool offRL = IsWheelOffTrack(wheelColliderRL);
        bool offRR = IsWheelOffTrack(wheelColliderRR);

        ApplyWheelFriction(wheelColliderFL, defaultSidewaysStiffness, offFL);
        ApplyWheelFriction(wheelColliderFR, defaultSidewaysStiffness, offFR);

        // Rear sideways grip starts from the handbrake-loosened value while
        // drifting, or the normal default otherwise -- off-track grip loss
        // then applies on top of whichever of those is currently active.
        float rearBaseSideways = handbrakeInput ? handbrakeSidewaysStiffness : defaultSidewaysStiffness;
        ApplyWheelFriction(wheelColliderRL, rearBaseSideways, offRL);
        ApplyWheelFriction(wheelColliderRR, rearBaseSideways, offRR);

        int offTrackWheelCount = (offFL ? 1 : 0) + (offFR ? 1 : 0) + (offRL ? 1 : 0) + (offRR ? 1 : 0);
        rb.linearDamping = baseLinearDamping + offTrackWheelCount * offTrackDragPerWheel;
    }

    private bool IsWheelOffTrack(WheelCollider wheel)
    {
        return wheel.GetGroundHit(out WheelHit hit) && hit.collider != null && hit.collider.CompareTag(offTrackTag);
    }

    private void ApplyWheelFriction(WheelCollider wheel, float baseSidewaysStiffness, bool offTrack)
    {
        float gripMultiplier = offTrack ? offTrackGripMultiplier : 1f;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = defaultForwardStiffness * gripMultiplier;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = baseSidewaysStiffness * gripMultiplier;
        wheel.sidewaysFriction = sideways;
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
 