using UnityEngine;
using UnityEngine.UI;

// Drop this on any Button and drag in a clip -- it wires itself to the
// Button's OnClick automatically in Awake, so there's no per-button event
// wiring needed in the Inspector. Reused across every menu (Main Menu,
// Pause Menu, Results Screen, Course Select) instead of each screen having
// its own duplicated click-sound fields/logic.
[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    [SerializeField] private AudioClip clickSfx;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlayClick);
    }

    private void PlayClick()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clickSfx);
        }
    }
}
