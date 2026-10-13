using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Player>())
        {
            if(RespawnController.Instance != null) RespawnController.Instance.TriggerRespawn(true);
            else GameManager.Instance.Respawn();
        }

        if (other.TryGetComponent(out IRespawneable objec)) objec.Respawn();
    }
}
