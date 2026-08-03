using UnityEngine;

// Static helper wrapping PlayerPrefs to track each course's best star result
// and derive unlock state from it. Centralizing this here means the course
// select screen and every course's ResultsScreen agree on the same keys and
// unlock rule without duplicating logic.
public static class CourseProgress
{
    private const string StarsKeyPrefix = "CourseStars_";
    private const int StarsRequiredToUnlockNext = 2;

    public static int GetBestStars(string courseId)
    {
        return PlayerPrefs.GetInt(StarsKeyPrefix + courseId, 0);
    }

    // Only writes if this run's star count beats the existing best, so a
    // worse repeat run can't downgrade previously-earned progress.
    public static void ReportStars(string courseId, int starsEarned)
    {
        int best = GetBestStars(courseId);
        if (starsEarned > best)
        {
            PlayerPrefs.SetInt(StarsKeyPrefix + courseId, starsEarned);
            PlayerPrefs.Save();
        }
    }

    // The first course in a progression is always unlocked (pass null/empty
    // previousCourseId for it); every later one requires the previous
    // course's best result to have earned at least StarsRequiredToUnlockNext
    // stars.
    public static bool IsUnlocked(string courseId, string previousCourseId)
    {
        if (string.IsNullOrEmpty(previousCourseId)) return true;
        return GetBestStars(previousCourseId) >= StarsRequiredToUnlockNext;
    }
}
