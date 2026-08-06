using UnityEngine;
using UnityEngine.UI;

// Options menu (GDD 3.3): engine volume, crash/SFX volume, master volume,
// camera sensitivity. Wire each Slider's OnValueChanged to the matching
// Set method below; OnEnable initializes them from the saved values so
// reopening the menu shows whatever was last set, not always defaults.
public class OptionsMenu : MonoBehaviour
{
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider engineVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider cameraSensitivitySlider;

    private void OnEnable()
    {
        masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        engineVolumeSlider.SetValueWithoutNotify(GameSettings.EngineVolume);
        sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        cameraSensitivitySlider.SetValueWithoutNotify(GameSettings.CameraSensitivity);
    }

    public void SetMasterVolume(float value) => GameSettings.MasterVolume = value;
    public void SetEngineVolume(float value) => GameSettings.EngineVolume = value;
    public void SetSfxVolume(float value) => GameSettings.SfxVolume = value;
    public void SetCameraSensitivity(float value) => GameSettings.CameraSensitivity = value;
}
