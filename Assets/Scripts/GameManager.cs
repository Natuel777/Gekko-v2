using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

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
        }

        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        factory?.ClearPools();
    }

    private void Start()
    {
        checkpointManager.InitializeDebugLevelsDictionary(_levels);
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

        var screenPause = Instantiate(Resources.Load<ScreenPause>("Canvas_Pause"));
        ScreenManager.Instance.Push(screenPause);
        IsPause = true;
    }

    public void DestroyObject(GameObject obj) {Destroy(obj);}
}
