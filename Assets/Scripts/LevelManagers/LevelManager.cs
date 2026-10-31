using UnityEngine;

public abstract class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    protected void Awake()
    {
        Instance = this;
    }
    protected void Start()
    {
        StartSong();
    }
    public abstract void StartSong();
}
