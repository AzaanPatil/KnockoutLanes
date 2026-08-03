using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float goDisplaySeconds = 1f;

    [Header("Audio")]
    [Tooltip("Played once when \"READY...\" first appears.")]
    [SerializeField] private AudioClip readySfx;
    [Tooltip("Played on each numeric tick (3, 2, 1).")]
    [SerializeField] private AudioClip tickSfx;
    [Tooltip("Played once on \"GO!\"")]
    [SerializeField] private AudioClip goSfx;

    private void Start()
    {
        RaceManager.Instance.OnCountdownReady.AddListener(HandleCountdownReady);
        RaceManager.Instance.OnCountdownTick.AddListener(HandleCountdownTick);
        RaceManager.Instance.OnRaceStart.AddListener(HandleRaceStart);
    }

    private void OnDisable()
    {
        if (RaceManager.Instance == null) return;
        RaceManager.Instance.OnCountdownReady.RemoveListener(HandleCountdownReady);
        RaceManager.Instance.OnCountdownTick.RemoveListener(HandleCountdownTick);
        RaceManager.Instance.OnRaceStart.RemoveListener(HandleRaceStart);
    }

    private void HandleCountdownReady()
    {
        countdownPanel.SetActive(true);
        countdownText.text = "READY...";
        PlaySfx(readySfx);
    }

    private void HandleCountdownTick(int remaining)
    {
        countdownPanel.SetActive(true);
        countdownText.text = remaining > 0 ? remaining.ToString() : "GO!";
        PlaySfx(remaining > 0 ? tickSfx : goSfx);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private void HandleRaceStart()
    {
        Invoke(nameof(HidePanel), goDisplaySeconds);
    }

    private void HidePanel()
    {
        countdownPanel.SetActive(false);
    }
}
