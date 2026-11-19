using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Stats de UN nivel: tiempo jugado, cuánto se recogió de cada coleccionable y cuántos había.
// Va puesto en la escena donde arranca el nivel (LvlOne, LvlTwo) y sobrevive a las escenas que siguen siendo parte
// del mismo nivel (LvlOne -> BossLvlOne -> Aldea), así el Canvas_Finish que abre la Aldea muestra lo del nivel 1.
// Cada nivel cuenta por separado: cuando carga otra escena con LevelStats (otro nivel o reiniciar) la nueva reemplaza
// a la vieja, y al volver al menú se descarta. Lo recogido se cuenta acá, no con el acumulado de CollectiblesRegister
// (ese suma todos los niveles y se carga del guardado).
public class LevelStats : MonoBehaviour
{
    public static LevelStats Instance { get; private set; }

    [Tooltip("Escenas que siguen siendo parte de este nivel: el tiempo sigue corriendo y sus coleccionables se suman al total.")]
    [SerializeField] private string[] _continuationScenes = { "BossLvlOne" };
    [Tooltip("Al cargar alguna de estas escenas las stats del nivel se descartan.")]
    [SerializeField] private string[] _discardScenes = { "Menu" };

    private string _homeScene;
    private float _elapsed;
    private bool _timing = true;   // false en escenas que no son del nivel (SceneLoader, Aldea)
    private bool _stopped;         // el nivel terminó
    private readonly Dictionary<string, int> _totals = new();
    private readonly Dictionary<string, int> _collected = new();
    private readonly HashSet<string> _countedScenes = new();

    public float ElapsedTime => _elapsed;

    // Escena donde arrancó este nivel (ej. "LvlOne"): la Aldea la usa para saber qué nivel se acaba de ganar.
    public string HomeScene => _homeScene;

    // Número del nivel (1, 2...) según ScenesDictionary.LevelIndex. 0 = no es un nivel (ej. se abrió la Aldea directo).
    public int LevelNumber
    {
        get
        {
            foreach(var kvp in ScenesDictionary.LevelIndex)
                if(ScenesDictionary.SceneName[kvp.Value] == _homeScene) return kvp.Key;
            return 0;
        }
    }

    public string FormattedTime
    {
        get { int t = Mathf.FloorToInt(_elapsed); return $"{t / 60:00}:{t % 60:00}"; }
    }

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            // Un nivel anterior todavía no mostró su pantalla final (ej. LvlOne -> Aldea: la Aldea tiene su propio
            // LevelStats, pero el Canvas_Finish que abre tiene que mostrar lo de LvlOne). Este se retira y sigue el viejo.
            // Si es el mismo nivel (reiniciar) o el anterior ya terminó, el nuevo lo reemplaza como siempre.
            bool sameLevel = Instance._homeScene == gameObject.scene.name;
            if(!sameLevel && !Instance._stopped)
            {
                enabled = false; // que no llegue a correr Start/Update antes de destruirse
                Destroy(gameObject);
                return;
            }
            Destroy(Instance.gameObject);
        }
        Instance = this;

        _homeScene = gameObject.scene.name;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        CollectiblesRegister.OnRegistered += OnCollected;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Por si sceneLoaded de la escena inicial llegó antes de suscribirse.
        CountTotals(SceneManager.GetSceneByName(_homeScene));
    }

    private void OnDestroy()
    {
        CollectiblesRegister.OnRegistered -= OnCollected;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if(Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if(System.Array.IndexOf(_discardScenes, scene.name) >= 0)
        {
            Destroy(gameObject);
            return;
        }

        _timing = scene.name == _homeScene || System.Array.IndexOf(_continuationScenes, scene.name) >= 0;
        if(_timing)
        {
            CountTotals(scene);
            _collectedAtSceneStart = new Dictionary<string, int>(_collected);
        }
    }

    // Reiniciar nivel (ver GameManager.RestartLvl): descarta lo recogido en el intento de la escena actual.
    private Dictionary<string, int> _collectedAtSceneStart = new();
    public void RestoreSceneStart()
    {
        _collected.Clear();
        foreach(var kv in _collectedAtSceneStart) _collected[kv.Key] = kv.Value;
    }

    private void CountTotals(Scene scene)
    {
        if(!scene.IsValid() || !_countedScenes.Add(scene.name)) return;

        foreach(var c in FindObjectsByType<Collectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if(c.gameObject.scene != scene || string.IsNullOrEmpty(c.CollectibleName)) continue;
            _totals.TryGetValue(c.CollectibleName, out int n);
            _totals[c.CollectibleName] = n + 1;
        }

        int bugCount = 0;
        foreach(var b in FindObjectsByType<Bug>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if(b.gameObject.scene == scene) bugCount++;

        if(bugCount > 0)
        {
            _totals.TryGetValue("Bug", out int n);
            _totals["Bug"] = n + bugCount;
        }
    }

    private void OnCollected(string key)
    {
        if(_stopped) return;
        _collected.TryGetValue(key, out int n);
        _collected[key] = n + 1;
    }

    private void Update()
    {
        if(_timing && !_stopped) _elapsed += Time.deltaTime;
    }

    public void StopTimer() => _stopped = true;

    public int GetTotal(string key) => _totals.TryGetValue(key, out var n) ? n : 0;

    // Recogidos en este nivel (nunca más que el total, por si algo reaparece al morir).
    public int GetCollected(string key)
    {
        int got = _collected.TryGetValue(key, out var n) ? n : 0;
        int total = GetTotal(key);
        return total > 0 ? Mathf.Min(got, total) : got;
    }
}
