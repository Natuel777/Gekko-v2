using UnityEngine;

public class SlipperyPlatform : MonoBehaviour
{
    [SerializeField] private float _slideForce = 500f;

    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
        {
            player.PjController.SetSlipperySurface(true);
        }
    }

    private void OnCollisionExit(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
        {
            player.PjController.SetSlipperySurface(false);
        }
    }

    private void OnCollisionStay(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Rigidbody rb))
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, col.contacts[0].normal).normalized;
            rb.AddForce(slideDir * _slideForce, ForceMode.Acceleration);
        }
    }
}
