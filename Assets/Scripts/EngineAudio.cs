using UnityEngine;

// Loops an engine sound whose pitch rises with the car's current speed.
// No clip assigned by default -- drop an engine loop into this component's
// AudioSource once you've downloaded one.
[RequireComponent(typeof(AudioSource))]
public class EngineAudio : MonoBehaviour
{
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 2.5f;
    [SerializeField] private float speedForMaxPitch = 30f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
    }

    private void Start()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceStart.AddListener(HandleRaceStart);
        }
    }

    private void OnDisable()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceStart.RemoveListener(HandleRaceStart);
        }
    }

    // Waits for the race to actually start (i.e. "GO") rather than playing
    // the moment the scene loads -- otherwise the engine would be audibly
    // running while the car's still sitting still on the start line during
    // the countdown.
    private void HandleRaceStart()
    {
        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (carRigidbody == null || audioSource.clip == null) return;

        float speed01 = Mathf.Clamp01(carRigidbody.linearVelocity.magnitude / speedForMaxPitch);
        audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);
        audioSource.volume = GameSettings.EngineVolume;
    }
}
