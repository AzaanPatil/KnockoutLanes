using UnityEngine;

// Central store for player-adjustable settings (GDD 3.3: engine volume,
// crash/SFX volume, master volume, camera sensitivity). Persists via
// PlayerPrefs. Master Volume applies itself automatically after every
// scene load (via RuntimeInitializeOnLoadMethod) so it takes effect
// everywhere without needing a settings GameObject placed in each scene.
// Engine/SFX volume and camera sensitivity are read directly by the
// scripts they affect (EngineAudio/DriftAudio, AudioManager, CameraFollow).
public static class GameSettings
{
    private const string MasterVolumeKey = "Settings_MasterVolume";
    private const string EngineVolumeKey = "Settings_EngineVolume";
    private const string SfxVolumeKey = "Settings_SfxVolume";
    private const string CameraSensitivityKey = "Settings_CameraSensitivity";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            AudioListener.volume = value;
        }
    }

    public static float EngineVolume
    {
        get => PlayerPrefs.GetFloat(EngineVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(EngineVolumeKey, value);
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        set => PlayerPrefs.SetFloat(SfxVolumeKey, value);
    }

    public static float CameraSensitivity
    {
        get => PlayerPrefs.GetFloat(CameraSensitivityKey, 1f);
        set => PlayerPrefs.SetFloat(CameraSensitivityKey, value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyOnSceneLoad()
    {
        AudioListener.volume = MasterVolume;
    }
}
