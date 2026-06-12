using UnityEngine;

public class GrabbableObject : InteractableObject
{
    [SerializeField] private Transform _GrabbedPoint;
    public Transform GrabbedPoint { get { return _GrabbedPoint; } }
    private Collider _col;
    private Vector3 _originalScale;

    protected override void Awake()
    {
        base.Awake();
        _col = GetComponentInChildren<Collider>();
    }

    public override void Grab()
    {
        _originalScale = transform.localScale;
        _col.enabled = false;
        _rb.isKinematic = true;
    }

    public override void Drop()
    {
        transform.SetParent(null);
        transform.rotation = new Quaternion(0, transform.rotation.y, 0, 1);
        transform.localScale = _originalScale;
        _col.enabled = true;
        _rb.isKinematic = false;
    }
}
