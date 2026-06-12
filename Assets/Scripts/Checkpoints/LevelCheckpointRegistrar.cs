using UnityEngine;

public class LevelCheckpointRegistrar : MonoBehaviour
{
    [SerializeField] private Checkpoint[] _checkpoints;

    private void Start()
    {
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
