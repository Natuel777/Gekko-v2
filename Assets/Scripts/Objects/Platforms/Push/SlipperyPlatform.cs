using UnityEngine;

public class SlipperyPlatform : MonoBehaviour
{
    [SerializeField] protected float _slideForce = 500f;


    protected void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
        {
            player.PjController.SetSlipperySurface(true);
        }
    }

    protected void OnCollisionExit(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Player player))
        {
            player.PjController.SetSlipperySurface(false);
        }
    }

    protected virtual void OnCollisionStay(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Rigidbody rb))
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, col.contacts[0].normal).normalized;
            rb.AddForce(slideDir * _slideForce, ForceMode.Acceleration);
        }
    }
    
}
