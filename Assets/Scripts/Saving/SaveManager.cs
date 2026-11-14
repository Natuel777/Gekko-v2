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
        Debug.Log(CollectiblesRegister.GetCollectibleCount("RaspBerry"));
        OnLoad?.Invoke();
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
