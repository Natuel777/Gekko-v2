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

    public override void Drop()
    {
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
