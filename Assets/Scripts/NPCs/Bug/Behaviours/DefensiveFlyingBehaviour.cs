using UnityEngine;

public class DefensiveFlyingBehaviour
{
    private Transform _target, _transform;
    private float _defenseThreshold, _speed, _shootInterval, _initialShootInterval;
    private Bug _owner;
    private BugBullet _bulletPrefab;

    public DefensiveFlyingBehaviour(float threshold, Transform t, float speed, BugBullet bullet, Bug bug)
    {
        _transform = t;
        _defenseThreshold = threshold;
        _speed = speed;
        _shootInterval = 1f; // Example interval, adjust as needed
        _bulletPrefab = bullet;
        _owner = bug;
        _initialShootInterval = _shootInterval;
    }

    public void StartFlyingDefense(Transform target)
    {
        _target = target;
    }

    public void StopFlyingDefense()
    {
        _target = null;
    }

    public void ArtificialUpdate()
    {
        if(_target == null) return;

        Vector3 toTarget = _target.position - _transform.position;
        Vector3 direction = toTarget.normalized;
        float sqrDistance = toTarget.sqrMagnitude;

        if(direction.sqrMagnitude > 0.0001f)
            _transform.rotation = Quaternion.LookRotation(direction);

        if(sqrDistance >= _defenseThreshold * _defenseThreshold)
        {
            Vector3 move = direction;
            move.y = 0f;
            _transform.position += move.normalized * _speed * Time.deltaTime;
        }

        ShootSequence(direction);
    }

    private void ShootSequence(Vector3 direction)
    {
        if(ShooteableObjectFactory.Instance == null || _bulletPrefab == null) return;

        if(_shootInterval > 0)
        {
            _shootInterval -= Time.deltaTime;
            return;
        }

        BugBullet newBullet = ShooteableObjectFactory.Instance.GetBullet<BugBullet>(_bulletPrefab, _transform.position, Quaternion.identity);

        if(newBullet == null)
            return;

        newBullet.SetDirection(direction);
        _shootInterval = _initialShootInterval;
    }
}
