using UnityEngine;

public class TurretShootBehaviour : IShooteable, IActivable, IServiceConsumer
{
    private readonly float _xRotation, _fireRate;
    private BugDetection _detection;
    private GameManager _gm;
    private bool _initialized = false;
    private Transform _transform, _target, _muzzle;
    private ShooteableObjectFactory _bulletFactory;
    private Vector3 _direction = Vector3.zero;
    private float _timer = 0f;
    private TurretBullet _bulletPrefab;

    public bool Initialized => _initialized;


    public TurretShootBehaviour(float xRot, Transform t, float detectionRange, Transform muzzle, float fireRate,
                                TurretBullet prefab)
    {
        _xRotation = xRot;
        _transform = t;
        _target = _gm.Pj.transform;
        _muzzle = muzzle;
        _fireRate = fireRate;
        _bulletPrefab = prefab;

        if(TryResolveService())
            _detection = new BugDetection(_transform, _target, detectionRange);
    }

    public void ArtificialUpdate()
    {
        if(!_initialized) return;

        if(_detection.IsTargetInRange())
        {
            LookAtTarget();

            if(_timer >= _fireRate)
            {
                //Play PS
                Shoot(_direction);
                _timer = 0f;
                return;
            }

            _timer += Time.deltaTime;
        }
    }

    private void LookAtTarget()
    {
        Vector3 _direction = _target.position - _transform.position;
        _direction.y = 0f;

        if(_direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_direction);

            _transform.rotation = Quaternion.RotateTowards(
                _transform.rotation,
                targetRotation,
                _xRotation
            );
        }
    }

    public void Shoot(Vector3 direction)
    {
        if(!_initialized || !TryResolveService()) return;

        TurretBullet bullet = _bulletFactory.GetBullet<TurretBullet>(_bulletPrefab, _muzzle.position, _transform.rotation);
        bullet.Shoot(direction);
    }
 
    public void Activated(bool active = true) => _initialized = active;

    public bool TryResolveGameManager()
    {
        if(_gm != null && _gm is GameManager) return true;

        if(ServiceLocator.Instance.TryGet(out IGameManager gm))
        {
            if(gm is GameManager manager)
            {
                _gm = manager;
                return true;
            } 
        }

        return false;
    }


    public bool TryResolveService()
    {
        if(_bulletFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _bulletFactory);
    }
}
