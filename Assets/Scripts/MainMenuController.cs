using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string courseSelectSceneName = "CourseSelect";

    // Kept this method's name as-is (rather than renaming to something like
    // "Play") so the Main Menu's existing Play button -- already wired to
    // this method in the Inspector -- doesn't need to be re-wired. It now
    // opens Course Select instead of jumping straight into a single course.
    public void PlayTrainingCourse()
    {
        SceneManager.LoadScene(courseSelectSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
