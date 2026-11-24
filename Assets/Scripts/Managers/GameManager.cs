using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[DefaultExecutionOrder(-500)]
public class GameManager : MonoBehaviour , IGameManager
{
    public static GameManager Instance;
    public Player Pj;
    public LayerMask GroundLayer;
    public LayerMask ClimbLayer;
    public LayerMask Surfaces;
    public LayerMask Obstacle;
    public bool CanPause;
    public CheckpointManager checkpointManager;

    public CollectiblesFactory factory;

    private ScreenPause _screenPause;

    [Header("Environment")]
    public float lavaDamage;

    [Header("Level Management")]
    [SerializeField] private string[] _levels;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            checkpointManager = new CheckpointManager();
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventManager.Subscribe("OnLevelFinished", ShowFinishScreen);
        }

        else
        {
            // Duplicate: the previous scene's GameManager is still alive (DontDestroyOnLoad). It is destroyed and not registered; otherwise
            // the ServiceLocator warned "IGameManager is already registered" on every scene change.
            Destroy(gameObject);
            return;
        }

        ServiceLocator.Instance.Register<IGameManager>(this);
    }

    private void OnDestroy()
    {
        factory?.ClearPools();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventManager.Unsubscribe("OnLevelFinished", ShowFinishScreen);
    }

    // Triggered by FinishLevelTrigger when the ending cinematic finishes (or is skipped).
    private void ShowFinishScreen()
    {
        var screenFinish = Instantiate(Resources.Load<ScreenFinishLevel>("Canvas_Finish"));
        ScreenManager.Instance.Push(screenFinish);
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_screenPause != null)
            _screenPause.gameObject.SetActive(false);

        CanPause = false;
        Time.timeScale = 1f;

        // Snapshot of the progress when each game scene starts: restarting the level goes back to this.
        bool isGameplay = scene.name != ScenesDictionary.SceneName[ScenesNames.Menu]
                       && scene.name != ScenesDictionary.SceneName[ScenesNames.LoadScene];
        if (isGameplay)
        {
            _levelStartCounts = CollectiblesRegister.Snapshot();
            _levelStartFlags = SaveManager.Instance != null ? SaveManager.Instance.GetFlags() : null;
            _hasLevelSnapshot = true;
        }
    }

    private Dictionary<string, int> _levelStartCounts;
    private List<string> _levelStartFlags;
    private bool _hasLevelSnapshot;

    // Leaves everything as it was when the scene started: blueberry/strawberry counters, strawberries and events marked as done,
    // the attempt's checkpoint and what LevelStats counted. Progress from previous levels is kept.
    private void ResetLevelProgress()
    {
        if (_hasLevelSnapshot)
        {
            CollectiblesRegister.Restore(_levelStartCounts);
            if (SaveManager.Instance != null && _levelStartFlags != null) SaveManager.Instance.SetFlags(_levelStartFlags);
        }
        checkpointManager?.ClearCheckpoint();
        if (LevelStats.Instance != null) LevelStats.Instance.RestoreSceneStart();
    }

    private void Start()
    {
        checkpointManager.InitializeDebugLevelsDictionary(_levels);

        _screenPause = Instantiate(Resources.Load<ScreenPause>("Canvas_Pause"));
        _screenPause.Initialize();
        _screenPause.gameObject.SetActive(false);
        DontDestroyOnLoad(_screenPause.gameObject);
    }

    public void Respawn()
    {
        if(AudioManager.instance)
        AudioManager.instance.ResetAudio();
        RestartLevelSong();

        if(RespawnController.Instance != null) RespawnController.Instance.TriggerRespawn(false);
        else checkpointManager.Respawn();
    }

    // ResetAudio stops everything, including the level's music: here the current level's music is started again. Each level has its own manager
    // (LvlOne: LevelOneManager; LvlTwo: LevelTwoManager, which inherits from LevelManager) and the one that isn't in this scene is skipped. Unity's
    // "!= null" also discards a previous level's manager that was already destroyed (the static Instance keeps pointing to it).
    // Before, LevelOneManager.Instance was called directly: in LvlTwo, opened directly, it gave a NullReferenceException and the
    // respawn didn't continue.
    private void RestartLevelSong()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.StartSong();
        else if (LevelOneManager.Instance != null) LevelOneManager.Instance.StartSong();
    }

    // The pause menu is really open. It is not the same as CanPause: that one is also true during ScreenFader's fades, the respawn and
    // MomDuck's sequences, where nothing is paused (see Pause). This is what systems use that need to know whether the game is paused
    // without depending on having received PauseEvent / UnPauseEvent.
    public bool IsPaused => _screenPause != null && _screenPause.isActiveAndEnabled;

    public void RespawnAt(int index) {checkpointManager.RespawnAt(index);}

    public void LoadDebugLevel(int index)
    {
        if(index < 1)
        {
            Debug.LogWarning($"Level index {index} out of range.");
            return;
        }

        // The audio is only reset if a level is really being loaded (LoadScene only changes the scene at the end of the frame).
        if (checkpointManager.LoadDebugLevel(index) && AudioManager.instance) AudioManager.instance.ResetAudio();
    }

    public void RestartLvl()
    {
        if (AudioManager.instance) AudioManager.instance.ResetAudio();
        ResetLevelProgress();

        if(AsyncLoader.sceneToLoad == ScenesNames.LoadScene) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        else SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.LoadScene]);
    }
    public void NextLevel()
    {
        if (AudioManager.instance) AudioManager.instance.ResetAudio();
        //CollectiblesRegister.Clear();

        string current = SceneManager.GetActiveScene().name;

        if (ScenesDictionary.TryGetNextLevel(current, out ScenesNames next))
            AsyncLoader.sceneToLoad = next;
        else
            AsyncLoader.sceneToLoad = ScenesNames.Menu;

        SceneManager.LoadScene(ScenesDictionary.SceneName[ScenesNames.LoadScene]);
    }

    public void Pause()
    {
        if(CanPause)
        {
            if (_screenPause.isActiveAndEnabled)
            {
                CanPause = false;
                Pj.ActivateInputs();
                Cursor.lockState = CursorLockMode.Locked;
                Time.timeScale = 1f;
                EventManager.Trigger("UnPauseEvent");
            }
            ScreenManager.Instance.Pop();
            return;
        }

        ScreenManager.Instance.Push(_screenPause);
        List<SoundNames> soundsToNotPause = new List<SoundNames>{ SoundNames.Menu, SoundNames.LvlOne, SoundNames.LvlTwo};
        if(AudioManager.instance)
        AudioManager.instance.PauseAll(soundsToNotPause);
        Time.timeScale = 0f;
        EventManager.Trigger("PauseEvent");
        CanPause = true;
    }

    public void DestroyObject(GameObject obj) {Destroy(obj);}
}
