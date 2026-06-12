using UnityEngine;

public class OldBridge_Plank : BringgableObject
{
    [SerializeField] private Transform _finalPos;
    private bool _onPosition;
    public bool OnPosition => _onPosition;

    public void Positioned()
    {
        CanMove = false;
        gameObject.layer = 7;
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _onPosition = true;
        GetComponent<AudioSource>().Play();
        transform.position = _finalPos.position;
        transform.rotation = _finalPos.rotation;
    }
}
