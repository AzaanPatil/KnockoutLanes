using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float goDisplaySeconds = 1f;

    private void Start()
    {
        RaceManager.Instance.OnCountdownTick.AddListener(HandleCountdownTick);
        RaceManager.Instance.OnRaceStart.AddListener(HandleRaceStart);
    }

    private void OnDisable()
    {
        if (RaceManager.Instance == null) return;
        RaceManager.Instance.OnCountdownTick.RemoveListener(HandleCountdownTick);
        RaceManager.Instance.OnRaceStart.RemoveListener(HandleRaceStart);
    }

    private void HandleCountdownTick(int remaining)
    {
        countdownPanel.SetActive(true);
        countdownText.text = remaining > 0 ? remaining.ToString() : "GO!";
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
