using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;


public class SaveManager : MonoBehaviour, ISaveLoad
{
    public static SaveManager Instance { get; private set; }
    [SerializeField] public SaveData saveData = new SaveData();
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

    public void SaveGame()
    {

        OnSave?.Invoke();
        string json = JsonUtility.ToJson(saveData, true);
        File.WriteAllText(path, json);
    }
    public void LoadGame()
    {

        if (File.Exists(path))
        {

            string json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, saveData);
        }
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
    protected void OnApplicationPause(bool pause)
    {
        if (pause) SaveGame();
    }
    protected void OnApplicationQuit()
    {
        SaveGame();
    }
}
