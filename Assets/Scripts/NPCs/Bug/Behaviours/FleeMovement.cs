using UnityEngine;

public class FleeMovement
{
    private float _speed;
    private float _randomness;
    private float _rotationSpeed;
    private Transform _target, _transform;
    private bool _isRunning, _active;
    private ObstacleAvoidance _avoidance;
    private Vector3? _redirectOverride;

    public bool IsActive => _active;

    public FleeMovement(float speed, float randomness, Transform t, ObstacleAvoidance avoidance, float rotationSpeed)
    {
        _speed = speed;
        _randomness = randomness;
        _transform = t;
        _avoidance = avoidance;
        _rotationSpeed = rotationSpeed;
    }

    public void StartFlee(Transform target) { _target = target; _isRunning = true; }
    public void StopFlee() { _isRunning = false; }
    public void Active(bool value) => _active = value;

    // Fuerza la dirección del próximo frame (la calcula GroundCheck al detectar un
    // borde sin piso). A diferencia de Wander/Charge, Flee recalcula su dirección
    // desde cero todos los frames, así que el override se consume una sola vez.
    public void Redirect(Vector3 newDirection)
    {
        newDirection.y = 0f;
        if (newDirection.sqrMagnitude < 0.001f) return;
        _redirectOverride = newDirection.normalized;
    }

    public void ArtificialUpdate()
    {
        if (!_isRunning || !_active || _target == null || _transform == null) return;

        Vector3 dir;

        if (_redirectOverride.HasValue)
        {
            dir = _redirectOverride.Value;
            _redirectOverride = null;
        }

        else
        {
            dir = _transform.position - _target.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 randomOffset = Random.insideUnitSphere;
            randomOffset.y = 0f;
            dir += randomOffset * _randomness;
            dir.y = 0f;
            dir.Normalize();

            // Si hay pared adelante, bordearla girando la dirección de huida.
            if (_avoidance != null && _avoidance.IsBlocked(_transform.forward))
                dir = Sidestep(dir);
        }

        RotateTowards(dir);
        _transform.position += dir * _speed * Time.deltaTime;
    }

    private Vector3 Sidestep(Vector3 dir)
    {
        if (_avoidance == null) return dir;

        Vector3 left  = Quaternion.Euler(0f, -90f, 0f) * dir;
        Vector3 right = Quaternion.Euler(0f,  90f, 0f) * dir;

        if (!_avoidance.IsBlocked(left))  return left;
        if (!_avoidance.IsBlocked(right)) return right;
        return -dir; // todo bloqueado: darse vuelta
    }

    private void RotateTowards(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        _transform.rotation = Quaternion.Slerp(_transform.rotation, target, Time.deltaTime * _rotationSpeed);
    }
}
