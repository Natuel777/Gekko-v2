using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CheckpointManager
{
    private Vector3 _respawnPos;
    private float _savedHealth;
    private bool _hasCheckpoint = false;

    private Dictionary<int, (Vector3 pos, float health)> _debugCheckpoints = new Dictionary<int, (Vector3, float)>();
    private Dictionary<int, string> _debugLevels = new Dictionary<int, string>();

    public void SaveCheckpoint(Vector3 pos, float health, int debugIndex = -1)
    {
        _respawnPos = pos;
        _savedHealth = health;
        _hasCheckpoint = true;

        if(debugIndex >= 0)
        {
            _debugCheckpoints[debugIndex] = (pos, health);
            Debug.Log($"Checkpoint {debugIndex} guardado en {pos} con vida {health}");
        }
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
        if(!_hasCheckpoint)
        {
            GameManager.Instance.RestartLvl();
            return;
        }

        Player pj = GameManager.Instance.Pj;
        pj.PjController.Teleport(_respawnPos);
        pj.health.SetHealth(_savedHealth);
    }

    public void RespawnAt(int index)
    {
        if(_debugCheckpoints.TryGetValue(index, out var checkpoint))
        {
            Player pj = GameManager.Instance.Pj;
            pj.PjController.Teleport(checkpoint.pos);
            pj.health.SetHealth(checkpoint.health);
        }
        else
            Debug.LogWarning($"No hay checkpoint de debug en el índice {index}.");
    }

    public void InitializeDebugLevelsDictionary(string[] sceneNames)
    {
        for(int i = 0; i < sceneNames.Length; i++)
            _debugLevels[i + 1] = sceneNames[i];
    }

    public void LoadDebugLevel(int index)
    {
        if(_debugLevels.TryGetValue(index, out var sceneName))
        {
            SceneManager.LoadScene(sceneName);
            Debug.Log($"Nivel de debug '{sceneName}' cargado.");
        }
        else
            Debug.LogWarning($"No hay nivel de debug en el índice {index}.");
    }
}
