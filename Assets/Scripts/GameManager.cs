using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Player Pj;
    public LayerMask GroundLayer;
    public LayerMask ClimbLayer;
    public LayerMask Surfaces;
    public bool IsPause;
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
        }

        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        factory?.ClearPools();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_screenPause != null)
            _screenPause.gameObject.SetActive(false);

        IsPause = false;
    }

    private void Start()
    {
        checkpointManager.InitializeDebugLevelsDictionary(_levels);

        _screenPause = Instantiate(Resources.Load<ScreenPause>("Canvas_Pause"));
        _screenPause.Initialize();
        _screenPause.gameObject.SetActive(false);
        DontDestroyOnLoad(_screenPause.gameObject);
    }

    public void Respawn() {checkpointManager.Respawn();}

    public void RespawnAt(int index) {checkpointManager.RespawnAt(index);}

    public void LoadDebugLevel(int index)
    {
        if(index < 1 || index > _levels.Length)
        {
            Debug.LogWarning($"Índice de nivel {index} fuera de rango.");
            return;
        }

        checkpointManager.LoadDebugLevel(index);
    }

    public void RestartLvl()
    {
        CollectiblesRegister.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Pause()
    {
        if(IsPause) return;

        ScreenManager.Instance.Push(_screenPause);
        List<SoundNames> soundsToNotPause = new List<SoundNames>{ SoundNames.Menu, SoundNames.LvlOne};
        AudioManager.instance.PauseAll(soundsToNotPause);
        EventManager.Trigger("PauseEvent");
        IsPause = true;
    }

    public void DestroyObject(GameObject obj) {Destroy(obj);}
}
