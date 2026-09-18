using UnityEngine;

public class LevelCheckpointRegistrar : MonoBehaviour
{
    [SerializeField] private Checkpoint[] _checkpoints;

    private void Start()
    {
        // Sin PJ real (escena sandbox del gecko) no hay a quién registrarle los checkpoints. Ojo:
        // Pj puede estar asignado pero DESACTIVADO en escena, y entonces su Awake nunca creó
        // 'health'. Por eso se chequea también health y no solo Pj.
        if (GameManager.Instance == null || GameManager.Instance.Pj == null
            || GameManager.Instance.Pj.health == null) return;

        float maxHealth = GameManager.Instance.Pj.health.MaxHealth;
        foreach (var cp in _checkpoints)
        {
            if (cp == null) continue;
            // Solo pre-registra para el teleport de debug; NO fija el respawn real.
            GameManager.Instance.checkpointManager.RegisterDebugCheckpoint(
                cp.Index,
                cp.RespawnPosition,
                maxHealth);
        }
    }
}
