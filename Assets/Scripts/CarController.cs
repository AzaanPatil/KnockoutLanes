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

    [Header("Boost")]
    [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;
    [Tooltip("Motor torque multiplier while boosting.")]
    [SerializeField] private float boostTorqueMultiplier = 1.6f;
    [SerializeField] private float maxBoostMeter = 100f;
    [Tooltip("How fast the meter drains per second while actively boosting -- at the default, a full meter lasts 2.5 seconds.")]
    [SerializeField] private float boostDrainPerSecond = 40f;
    [Tooltip("How fast the meter refills per second once you stop boosting.")]
    [SerializeField] private float boostRegenPerSecond = 20f;
    [Tooltip("Seconds after releasing boost before regen starts -- stops rapid tap-release-tap from feeling free.")]
    [SerializeField] private float boostRegenDelay = 1f;

    [Header("Off-Track Handling")]
    [Tooltip("Tag used to mark ground outside the track (e.g. the grass plane). A wheel touching it loses grip and adds drag.")]
    [SerializeField] private string offTrackTag = "OffTrack";
    [Range(0f, 1f)]
    [Tooltip("Grip multiplier applied to a wheel's forward/sideways friction while it's off-track. Lower = more slippery.")]
    [SerializeField] private float offTrackGripMultiplier = 0.4f;
    [Tooltip("Extra Rigidbody linear drag added per wheel currently off-track -- this is what makes off-track \"slow you down significantly,\" separate from the grip loss.")]
    [SerializeField] private float offTrackDragPerWheel = 1.5f;

    [Header("Dirt Road Handling")]
    [Tooltip("Tag used to mark road segments that are a rougher dirt surface rather than smooth asphalt (e.g. Landfill's road). RaceManager can tag its generated road with this. A course whose road doesn't use this tag drives like normal asphalt.")]
    [SerializeField] private string dirtRoadTag = "DirtRoad";
    [Range(0f, 1f)]
    [Tooltip("Grip multiplier applied to a wheel's forward/sideways friction while it's on a dirt road segment -- between full asphalt grip (1) and the harsher off-track penalty.")]
    [SerializeField] private float dirtRoadGripMultiplier = 0.75f;
    [Tooltip("Extra Rigidbody linear drag added per wheel currently on a dirt road segment -- smaller than the off-track penalty, but enough that dirt roads noticeably decelerate faster than asphalt.")]
    [SerializeField] private float dirtRoadDragPerWheel = 0.6f;

    // Gated off by RaceManager during the pre-race countdown so the car
    // can't jump the start.
    public bool CanDrive { get; set; } = true;

    // True only while the handbrake is held AND the car is genuinely
    // sliding sideways (not just sitting still or braking in a straight
    // line). Read by DriftStyleTracker to build the style multiplier.
    public bool IsDrifting { get; private set; }

    // True while boost is actively applying extra torque (key held, meter
    // not empty, throttle pressed). Read by RaceHUD for the boost meter bar.
    public bool IsBoosting { get; private set; }
    public float BoostMeter01 => boostMeter / maxBoostMeter;

    // Read by VehicleReset to detect the player holding the accelerator
    // while flipped, to trigger a manual self-right.
    public float ThrottleInput => verticalInput;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool handbrakeInput;
    private bool boostInput;
    private float boostMeter;
    private float timeSinceBoostReleased;
    private float defaultSidewaysStiffness;
    private float defaultForwardStiffness;
    private float baseLinearDamping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;
        boostMeter = maxBoostMeter;

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
        boostInput = Input.GetKey(boostKey);

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
        UpdateSurfaceHandling();
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
        UpdateBoost();

        float torqueMultiplier = IsBoosting ? boostTorqueMultiplier : 1f;
        float motorTorque = verticalInput * maxMotorTorque * torqueMultiplier;

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
 
    private void UpdateBoost()
    {
        // Only while actually accelerating forward -- no boosting while
        // idle, coasting, or in reverse.
        bool wantsBoost = boostInput && boostMeter > 0f && verticalInput > 0.1f;

        if (wantsBoost)
        {
            IsBoosting = true;
            boostMeter = Mathf.Max(0f, boostMeter - boostDrainPerSecond * Time.fixedDeltaTime);
            timeSinceBoostReleased = 0f;
        }
        else
        {
            IsBoosting = false;
            timeSinceBoostReleased += Time.fixedDeltaTime;
            if (timeSinceBoostReleased >= boostRegenDelay)
            {
                boostMeter = Mathf.Min(maxBoostMeter, boostMeter + boostRegenPerSecond * Time.fixedDeltaTime);
            }
        }
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

    // Asphalt is the default -- a wheel only counts as Dirt or OffTrack if
    // it's touching a collider tagged accordingly. Dirt is a middle tier:
    // rougher and slower than asphalt, but not as punishing as driving off
    // the course entirely.
    private enum WheelSurface { Asphalt, Dirt, OffTrack }

    private void UpdateSurfaceHandling()
    {
        WheelSurface surfaceFL = GetWheelSurface(wheelColliderFL);
        WheelSurface surfaceFR = GetWheelSurface(wheelColliderFR);
        WheelSurface surfaceRL = GetWheelSurface(wheelColliderRL);
        WheelSurface surfaceRR = GetWheelSurface(wheelColliderRR);

        ApplyWheelFriction(wheelColliderFL, defaultSidewaysStiffness, surfaceFL);
        ApplyWheelFriction(wheelColliderFR, defaultSidewaysStiffness, surfaceFR);

        // Rear sideways grip starts from the handbrake-loosened value while
        // drifting, or the normal default otherwise -- surface grip loss
        // then applies on top of whichever of those is currently active.
        float rearBaseSideways = handbrakeInput ? handbrakeSidewaysStiffness : defaultSidewaysStiffness;
        ApplyWheelFriction(wheelColliderRL, rearBaseSideways, surfaceRL);
        ApplyWheelFriction(wheelColliderRR, rearBaseSideways, surfaceRR);

        float extraDrag = SurfaceDragPerWheel(surfaceFL) + SurfaceDragPerWheel(surfaceFR)
            + SurfaceDragPerWheel(surfaceRL) + SurfaceDragPerWheel(surfaceRR);
        rb.linearDamping = baseLinearDamping + extraDrag;
    }

    private WheelSurface GetWheelSurface(WheelCollider wheel)
    {
        if (!wheel.GetGroundHit(out WheelHit hit) || hit.collider == null) return WheelSurface.Asphalt;
        if (hit.collider.CompareTag(offTrackTag)) return WheelSurface.OffTrack;
        if (hit.collider.CompareTag(dirtRoadTag)) return WheelSurface.Dirt;
        return WheelSurface.Asphalt;
    }

    private float SurfaceDragPerWheel(WheelSurface surface)
    {
        return surface switch
        {
            WheelSurface.OffTrack => offTrackDragPerWheel,
            WheelSurface.Dirt => dirtRoadDragPerWheel,
            _ => 0f,
        };
    }

    private void ApplyWheelFriction(WheelCollider wheel, float baseSidewaysStiffness, WheelSurface surface)
    {
        float gripMultiplier = surface switch
        {
            WheelSurface.OffTrack => offTrackGripMultiplier,
            WheelSurface.Dirt => dirtRoadGripMultiplier,
            _ => 1f,
        };

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
 