using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Drives the course-select screen: shows each course's best star result,
// locks any course whose predecessor hasn't earned enough stars yet (see
// CourseProgress), and loads the chosen course's scene on click.
public class CourseSelectManager : MonoBehaviour
{
    [System.Serializable]
    public class CourseEntry
    {
        [Tooltip("Must exactly match this course's actual Scene file name -- it's used both as the PlayerPrefs key (via ResultsScreen's matching courseId) and as the scene to load.")]
        public string courseId;
        [Tooltip("The button in this screen representing this course.")]
        public CourseButton button;
    }

    [Tooltip("Courses in unlock order -- the first is always available; each later one requires the previous course's best result to meet the star threshold set in CourseProgress.")]
    [SerializeField] private List<CourseEntry> courses = new List<CourseEntry>();

    private void Start()
    {
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        string previousCourseId = null;
        foreach (CourseEntry course in courses)
        {
            if (course.button == null || string.IsNullOrEmpty(course.courseId)) continue;

            int bestStars = CourseProgress.GetBestStars(course.courseId);
            bool unlocked = CourseProgress.IsUnlocked(course.courseId, previousCourseId);
            string courseIdToLoad = course.courseId;
            course.button.Configure(bestStars, unlocked, () => SelectCourse(courseIdToLoad));

            previousCourseId = course.courseId;
        }
    }

    private void SelectCourse(string courseId)
    {
        SceneManager.LoadScene(courseId);
    }
}
