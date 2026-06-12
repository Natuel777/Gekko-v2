using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int _index = -1;
    [SerializeField] private Transform _respawnPoint;
    public int Index => _index;

    // Punto fijo de aparición: el hijo _respawnPoint si está asignado, si no la posición del checkpoint.
    public Vector3 RespawnPosition => _respawnPoint != null ? _respawnPoint.position : transform.position;

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out Player player))
            GameManager.Instance.checkpointManager.SaveCheckpoint(
                RespawnPosition,
                player.health.CurrentHealth,
                _index);
    }
}
