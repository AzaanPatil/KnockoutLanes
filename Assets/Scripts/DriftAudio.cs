using UnityEngine;

// Loops a tire-screech sound while the car is actively drifting (per
// CarController.IsDrifting), fading volume in/out rather than a hard
// start/stop for a smoother feel. No clip assigned by default -- drop a
// screech loop into this component's AudioSource once you've got one.
[RequireComponent(typeof(AudioSource))]
public class DriftAudio : MonoBehaviour
{
    [SerializeField] private CarController carController;
    [SerializeField] private float fadeSpeed = 4f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = 0f;
    }

    private void Start()
    {
        if (audioSource.clip != null)
        {
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (carController == null || audioSource.clip == null) return;

        float targetVolume = carController.IsDrifting ? 1f : 0f;
        audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, fadeSpeed * Time.deltaTime);
    }
}
