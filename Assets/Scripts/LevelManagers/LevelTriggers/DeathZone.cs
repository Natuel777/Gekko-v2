using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Player>()) GameManager.Instance.Respawn();

        if (other.TryGetComponent(out IRespawneable objec)) objec.Respawn();
    }
}
