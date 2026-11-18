using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;


public class SaveManager : MonoBehaviour, ISaveLoad
{
    public static SaveManager Instance { get; private set; }
    [SerializeField] public SaveData saveData = new SaveData();
    private HashSet<string> _flags = new HashSet<string>();
    protected string path;

    public delegate void SavingGame();
    public event SavingGame OnSave;

    public delegate void LoadingGame();
    public event LoadingGame OnLoad;

    public delegate void DeletingData();
    public event DeletingData OnDelete;

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            path = Application.persistentDataPath + "/Hola.Hola";
        }
        else
        {
            Destroy(gameObject);
        }

    }
    private void Start()
    {
        LoadGame();
    }
    public bool Has(string id) => _flags.Contains(id);
    public void Set(string id) => _flags.Add(id);

    // Foto de las marcas (frutillas agarradas, eventos vistos): GameManager la restaura al reiniciar el nivel.
    public List<string> GetFlags() => new List<string>(_flags);
    public void SetFlags(IEnumerable<string> flags) => _flags = new HashSet<string>(flags ?? new List<string>());
    public void SaveGame()
    {
        OnSave?.Invoke();
        saveData.flags = new List<string>(_flags);
        saveData.blueberryCount = CollectiblesRegister.GetCollectibleCount("BlueBerry");
        saveData.strawberryCount = CollectiblesRegister.GetCollectibleCount("RaspBerry");
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(path, json);
    }
    public void LoadGame()
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, saveData);
            _flags = new HashSet<string>(saveData.flags);
            CollectiblesRegister.SetCollectibleCount("BlueBerry", saveData.blueberryCount);
            CollectiblesRegister.SetCollectibleCount("RaspBerry", saveData.strawberryCount);
        }
        OnLoad?.Invoke();
    }
    // Avisa que se borró el progreso (ej. Opciones muestra "Datos borrados").
    public static event System.Action OnProgressDeleted;

    // Borra SOLO el progreso del jugador (archivo de partida, arándanos/frutillas, eventos vistos y zonas purificadas)
    // y deja la configuración (volumen, idioma, gráficos, teclas, sensibilidad) intacta. Funciona aunque no haya
    // SaveManager en la escena. A diferencia de DeleteData, también limpia los contadores en memoria y no recarga escena.
    public static void DeleteProgress()
    {
        string file = Instance != null ? Instance.path : Application.persistentDataPath + "/Hola.Hola";
        if (File.Exists(file)) File.Delete(file);

        if (Instance != null)
        {
            Instance._flags.Clear();
            Instance.saveData = new SaveData();
            Instance.OnDelete?.Invoke();
        }

        CollectiblesRegister.Clear();
        PlayerPrefs.DeleteKey(PlayerPrefsKeys.aldeaZonasPurificadasKey);
        PlayerPrefs.Save();

        Debug.Log("[SaveManager] Progreso del jugador borrado (la configuración se mantiene).");
        OnProgressDeleted?.Invoke();
    }

    public void DeleteData()
    {
        File.Delete(path);
        PlayerPrefs.DeleteAll();
        OnDelete?.Invoke();
        //GameManager.Instance.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    //protected void OnApplicationPause(bool pause)
    //{
    //    if (pause) SaveGame();
    //}
    //protected void OnApplicationQuit()
    //{
    //    SaveGame();
    //}
}
