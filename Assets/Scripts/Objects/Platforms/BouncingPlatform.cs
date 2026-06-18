using UnityEngine;

public class BouncingPlatform : MonoBehaviour
{
    [SerializeField] private float _jumpBoost;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.TryGetComponent(out Player pj)) pj.PjController.Jump(_jumpBoost);
        else if (collision.transform.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity += transform.up * _jumpBoost;
        }
    }
}
