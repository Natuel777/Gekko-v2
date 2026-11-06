using System;
using UnityEngine;

public class MisionCollectible : MonoBehaviour
{
    public event Action OnCollected;
    private bool _collected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (other.GetComponent<Player>())
        {
            OnCollected?.Invoke();
            _collected = true;
            Destroy(gameObject);
        }
    }
}
