using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CheckpointManager
{
    private Vector3 _respawnPos;
    private Quaternion _respawnRot = Quaternion.identity;
    private float _savedHealth;
    private bool _hasCheckpoint = false;

    private Dictionary<int, (Vector3 pos, float health)> _debugCheckpoints = new Dictionary<int, (Vector3, float)>();
    private Dictionary<int, string> _debugLevels = new Dictionary<int, string>();

    public bool HasCheckpoint => _hasCheckpoint;

    // Inicio del nivel: lo registra Player al aparecer. Se usa como checkpoint cuando todavía no se pisó ninguno, así
    // la R (o una caída) vuelve al principio con el iris en vez de recargar la escena por la pantalla de carga.
    private Vector3 _startPos;
    private Quaternion _startRot = Quaternion.identity;
    private float _startHealth;
    private bool _hasLevelStart;

    public void SetLevelStart(Vector3 pos, Quaternion rot, float health)
    {
        _startPos = pos;
        _startRot = rot;
        _startHealth = health;
        _hasLevelStart = true;
    }

    // Checkpoint pisado o, si no hay, el inicio del nivel.
    public bool TryGetRespawnOrStartPose(out Vector3 pos, out Quaternion rot, out float health)
    {
        if (TryGetRespawnPose(out pos, out rot, out health)) return true;
        pos = _startPos;
        rot = _startRot;
        health = _startHealth;
        return _hasLevelStart;
    }

    // Reiniciar nivel: olvidar el checkpoint del intento (si no, al morir o con R se volvía a ese punto viejo).
    public void ClearCheckpoint()
    {
        _hasCheckpoint = false;
        _respawnPos = Vector3.zero;
        _respawnRot = Quaternion.identity;
        _savedHealth = 0f;
    }

    public void SaveCheckpoint(Vector3 pos, Quaternion rot, float health, int debugIndex = -1)
    {
        _respawnPos = pos;
        _respawnRot = rot;
        _savedHealth = health;
        _hasCheckpoint = true;

        if(debugIndex >= 0)
        {
            _debugCheckpoints[debugIndex] = (pos, health);
            Debug.Log($"Checkpoint {debugIndex} guardado en {pos} con vida {health}");
        }
    }

    // Para RespawnController: le da la pose a usar sin disparar el teleport instantáneo de Respawn().
    public bool TryGetRespawnPose(out Vector3 pos, out Quaternion rot, out float health)
    {
        pos = _respawnPos;
        rot = _respawnRot;
        health = _savedHealth;
        return _hasCheckpoint;
    }

    // Pre-registra un checkpoint SOLO para el teleport de debug (teclas 1/2/3).
    // No toca _respawnPos ni _hasCheckpoint: el punto de respawn real solo lo fija
    // SaveCheckpoint cuando el jugador pisa físicamente un checkpoint.
    public void RegisterDebugCheckpoint(int index, Vector3 pos, float health)
    {
        if(index < 0) return;
        _debugCheckpoints[index] = (pos, health);
    }

    public void Respawn()
    {
        // Sin checkpoint pisado: reiniciar la escena (resetea coleccionables) en vez de
        // teleportar a un punto que nunca se guardó.
        // Ahora, si no hay checkpoint, se vuelve al inicio del nivel (sin recargar). Solo si ni eso hay, reinicia.
        if(!TryGetRespawnOrStartPose(out Vector3 pos, out Quaternion rot, out float health))
        {
            GameManager.Instance.RestartLvl();
            return;
        }

        Player pj = GameManager.Instance.Pj;
        pj.PjController.Teleport(pos);
        pj.transform.rotation = rot;
        pj.health.SetHealth(health);
        pj.PjTongue.ResetTongue();
    }

    public void RespawnAt(int index)
    {
        if(_debugCheckpoints.TryGetValue(index, out var checkpoint))
        {
            Player pj = GameManager.Instance.Pj;
            pj.PjController.Teleport(checkpoint.pos);
            pj.health.SetHealth(checkpoint.health);
            pj.PjTongue.ResetTongue();
        }
        else
            Debug.LogWarning($"No hay checkpoint de debug en el índice {index}.");
    }

    // Los nombres vacíos se saltean: la lista _levels del GameManager está vacía en las escenas, y con un nombre vacío
    // SceneManager.LoadScene("") tiraba "Cannot load scene: Invalid scene name" cada vez que se apretaba Alt+1/2/3.
    public void InitializeDebugLevelsDictionary(string[] sceneNames)
    {
        if(sceneNames == null) return;

        for(int i = 0; i < sceneNames.Length; i++)
            if(!string.IsNullOrWhiteSpace(sceneNames[i])) _debugLevels[i + 1] = sceneNames[i].Trim();
    }

    // Devuelve si cargó algo. Sin nombre propio para ese índice usa el nivel número 'index' de ScenesDictionary
    // (1 = LvlOne, 2 = LvlTwo). Si la escena no existe o no está en Build Settings, solo avisa.
    public bool LoadDebugLevel(int index)
    {
        if(!_debugLevels.TryGetValue(index, out string sceneName) && ScenesDictionary.LevelIndex.TryGetValue(index, out ScenesNames level))
            sceneName = ScenesDictionary.SceneName[level];

        if(string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"No hay nivel de debug en el índice {index}.");
            return false;
        }

        if(!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Nivel de debug '{sceneName}' (índice {index}) no está en Build Settings.");
            return false;
        }

        SceneManager.LoadScene(sceneName);
        Debug.Log($"Nivel de debug '{sceneName}' cargado.");
        return true;
    }
}
