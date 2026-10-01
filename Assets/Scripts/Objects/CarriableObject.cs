using UnityEngine;

public class CarriableObject : InteractableObject
{
    [SerializeField] private Transform _GrabbedPoint;
    public Transform GrabbedPoint { get { return _GrabbedPoint; } }
    protected Collider _col;
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
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
    }

    public override void Drop(Vector3 dropPos, float rotationY)
    {
        Transform t = null;
        if (ScreenManager.Instance) t = ScreenManager.Instance.GetComponent<ConfigGameScene>().mainGame;
        transform.SetParent(t);
        transform.position = dropPos;
        transform.rotation = new Quaternion(0, rotationY, 0, 1);
        transform.localScale = _originalScale;
        _col.enabled = true;
        _rb.isKinematic = false;
    }
}
