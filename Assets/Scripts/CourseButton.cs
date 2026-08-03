using System;
using UnityEngine;
using UnityEngine.UI;

// One button on the course-select screen -- shows the course's best star
// result, or a locked state if CourseSelectManager says it isn't unlocked
// yet. Configure() is called by CourseSelectManager at runtime; this script
// doesn't know or care about unlock rules itself.
public class CourseButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("In star order -- starIcons[0] is the first star, etc.")]
    [SerializeField] private GameObject[] starIcons;
    [Tooltip("Optional. Shown instead of/over the button art while this course is locked.")]
    [SerializeField] private GameObject lockedOverlay;

    private Action onSelected;

    public void Configure(int bestStars, bool unlocked, Action onSelectedCallback)
    {
        onSelected = onSelectedCallback;

        button.interactable = unlocked;
        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!unlocked);
        }

        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].SetActive(unlocked && i < bestStars);
        }
    }

    // Wire this to the Button component's OnClick in the Inspector.
    public void OnClicked()
    {
        onSelected?.Invoke();
    }
}
