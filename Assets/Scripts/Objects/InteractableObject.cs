using UnityEngine;

public abstract class InteractableObject : BaseObject
{
    protected Rigidbody _rb;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    public abstract void Grab();
    public abstract void Drop();
}
