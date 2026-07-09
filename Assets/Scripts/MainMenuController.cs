using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string trainingCourseSceneName = "TrainingCourse";

    public void PlayTrainingCourse()
    {
        SceneManager.LoadScene(trainingCourseSceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }
}
