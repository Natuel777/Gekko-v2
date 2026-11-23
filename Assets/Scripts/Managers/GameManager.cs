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
            // Duplicado: el GameManager de la escena anterior sigue vivo (DontDestroyOnLoad). Se destruye y no se registra; si no,
            // el ServiceLocator avisaba "IGameManager is already registered" en cada cambio de escena.
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

    // Lo dispara FinishLevelTrigger cuando termina (o se saltea) la cinemática de final.
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

        // Foto del progreso al empezar cada escena de juego: al reiniciar el nivel se vuelve a esto.
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

    // Deja todo como al empezar la escena: contadores de arándanos/frutillas, frutillas y eventos marcados como hechos,
    // checkpoint del intento y lo contado por LevelStats. Lo de niveles anteriores se conserva.
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

    // ResetAudio corta todo, también la música del nivel: acá se vuelve a arrancar la del nivel actual. Cada nivel tiene su manager
    // (LvlOne: LevelOneManager; LvlTwo: LevelTwoManager, que hereda de LevelManager) y el que no está en esta escena se saltea. El
    // "!= null" de Unity también descarta al manager de un nivel anterior que ya se destruyó (el Instance estático queda apuntando
    // a él). Antes se llamaba a LevelOneManager.Instance a secas: en LvlTwo, abierta directo, daba NullReferenceException y el
    // respawn no seguía.
    private void RestartLevelSong()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.StartSong();
        else if (LevelOneManager.Instance != null) LevelOneManager.Instance.StartSong();
    }

    // El menú de pausa está abierto de verdad. No es lo mismo que CanPause: ese queda en true también durante los fundidos de
    // ScreenFader, el respawn y las secuencias de MomDuck, donde no se pausa nada (ver Pause). Es lo que usan los sistemas que
    // tienen que saber si el juego está pausado sin depender de haber recibido PauseEvent / UnPauseEvent.
    public bool IsPaused => _screenPause != null && _screenPause.isActiveAndEnabled;

    public void RespawnAt(int index) {checkpointManager.RespawnAt(index);}

    public void LoadDebugLevel(int index)
    {
        if(index < 1)
        {
            Debug.LogWarning($"Índice de nivel {index} fuera de rango.");
            return;
        }

        // El audio se resetea solo si de verdad se carga un nivel (LoadScene recién cambia de escena al final del frame).
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
        List<SoundNames> soundsToNotPause = new List<SoundNames>{ SoundNames.Menu, SoundNames.LvlOne};
        if(AudioManager.instance)
        AudioManager.instance.PauseAll(soundsToNotPause);
        Time.timeScale = 0f;
        EventManager.Trigger("PauseEvent");
        CanPause = true;
    }

    public void DestroyObject(GameObject obj) {Destroy(obj);}
}
