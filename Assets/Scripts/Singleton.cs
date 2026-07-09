using UnityEngine;

// Shared by RaceManager and ScoreManager, both of which are one-per-scene
// managers that other scripts look up by Instance.
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
    }
}
