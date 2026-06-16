using UnityEngine;

public class LookAtTarget
{
    //Implementar acá animación de enrrollarse

    private float _rotationSpeed;
    private Transform _target, _transform;
    private bool _active;
    private bool _lockYAxis;

    public LookAtTarget(float rotationSpeed, Transform t, bool lockYAxis = false)
    {
        _rotationSpeed = rotationSpeed;
        _transform = t;
        _lockYAxis = lockYAxis;
    }

    public void StartLooking(Transform target)
    {
        _target = target;
        _active = true;
    }

    public void StopLooking() {_active = false;}

    public void ArtificialUpdate()
    {
        if(!_active || _target == null) return;

        Vector3 dir = _target.position - _transform.position;
        if(_lockYAxis) dir.y = 0f;
        dir.Normalize();
        if(dir.sqrMagnitude < 0.0001f) return;

        _transform.forward = Vector3.Lerp(
            _transform.forward,
            dir,
            Time.deltaTime * _rotationSpeed
        );
    }
}
