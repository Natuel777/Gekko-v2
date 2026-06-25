using UnityEngine;

public class PushPlatform : SlipperyPlatform
{
    [SerializeField] private Vector3 _direction;
    protected override void OnCollisionStay(Collision col)
    {
        if (col.gameObject.TryGetComponent(out Rigidbody rb))
        {
            if(_direction != Vector3.zero)
                rb.AddForce(_direction.normalized * _slideForce, ForceMode.Acceleration);
            else
                rb.AddForce(transform.forward * _slideForce, ForceMode.Acceleration);
        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + _direction * 10);
    }
}
