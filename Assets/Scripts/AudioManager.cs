using UnityEngine;

// Central one-shot SFX player. Lets any script -- including prefabs, which
// can't hold a serialized reference to a scene object -- play a sound via
// AudioManager.Instance.PlaySFX(clip) without needing their own AudioSource.
[RequireComponent(typeof(AudioSource))]
public class AudioManager : Singleton<AudioManager>
{
    private AudioSource audioSource;

    protected override void Awake()
    {
        base.Awake();
        audioSource = GetComponent<AudioSource>();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip, volume);
    }
}
