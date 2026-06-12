using UnityEngine;

public class LookAtTarget
{
    //Implementar acá animación de enrrollarse

    private float _rotationSpeed;
    private Transform _target, _transform;
    private bool _active;

    public LookAtTarget(float rotationSpeed, Transform t) 
    {
        _rotationSpeed = rotationSpeed;
        _transform = t;
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

        Vector3 dir = (_target.position - _transform.position).normalized;
        if(dir.sqrMagnitude < 0.0001f) return;

        _transform.forward = Vector3.Lerp(
            _transform.forward,
            dir,
            Time.deltaTime * _rotationSpeed
        );
    }
}
