using System;
using UnityEngine;

public class DamCollider : MonoBehaviour
{
    public event Action OnTouched;

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.GetComponent<Player>()) OnTouched?.Invoke();
    }
}
