using System;
using UnityEngine;

public class MisionCollectible : MonoBehaviour
{
    public event Action OnCollected;
    private bool _collected = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (_collected) return;
        if (collision.gameObject.GetComponent<Player>())
        {
            OnCollected?.Invoke();
            _collected = true;
            Destroy(gameObject);
        }
    }
}
