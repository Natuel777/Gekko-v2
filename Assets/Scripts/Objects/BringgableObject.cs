using UnityEngine;

public class BringgableObject : InteractableObject
{
    public bool CanMove = true;
    public override void Grab()
    {
        _rb.useGravity = false;
        _rb.linearDamping = 10f;
        _rb.angularDamping = 10f;
    }

    public override void Drop(Vector3 dropPos, float rotationY)
    {
        transform.position = dropPos;
        transform.rotation = new Quaternion(0, rotationY, 0, 1);
        _rb.useGravity = true;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0.05f;
        _rb.isKinematic = false;
    }

    public void StartMoving()
    {
        _rb.isKinematic = true;
    }
}
