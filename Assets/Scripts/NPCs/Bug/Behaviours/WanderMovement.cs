using UnityEngine;

public class WanderMovement
{
    private float _speed, _changeDirTime, _timer, _rotationSpeed;
    private Vector3 _direction;
    private Transform _transform;
    private bool _active;
    private ObstacleAvoidance _avoidance;

    public WanderMovement(float speed, float changeDir, Transform t, ObstacleAvoidance avoidance, float rotationSpeed)
    {
        _speed = speed;
        _changeDirTime = changeDir;
        _transform = t;
        _avoidance = avoidance;
        _rotationSpeed = rotationSpeed;
    }

    public void Active(bool value) { _active = value; }

    // Fuerza una dirección concreta (la calcula HeavyBeetle al detectar un borde sin piso)
    // y reinicia el timer. Complementa el re-pick por IsBlocked, no lo reemplaza.
    public void Redirect(Vector3 newDirection)
    {
        newDirection.y = 0f;
        if (newDirection.sqrMagnitude < 0.001f) return;
        _direction = newDirection.normalized;
        _timer = 0f;
    }

    public void ArtificialUpdate()
    {
        if (_transform == null) return;

        _timer += Time.deltaTime;

        bool blocked = _avoidance != null && _avoidance.IsBlocked(_transform.forward);

        if (_timer > _changeDirTime || blocked || _direction.sqrMagnitude < 0.001f)
        {
            PickNewDirection();
            _timer = 0f;
        }

        RotateTowards(_direction);
        _transform.position += _direction * _speed * Time.deltaTime;
    }

    // Elige una dirección random; intenta varias veces encontrar una libre para no caminar hacia la pared.
    private void PickNewDirection()
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 candidate = Random.insideUnitSphere;
            candidate.y = 0f;
            if (candidate.sqrMagnitude < 0.001f) continue;
            candidate.Normalize();

            _direction = candidate;
            if (_avoidance == null || !_avoidance.IsBlocked(candidate)) return;
        }
    }

    private void RotateTowards(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        _transform.rotation = Quaternion.Slerp(_transform.rotation, target, Time.deltaTime * _rotationSpeed);
    }
}
