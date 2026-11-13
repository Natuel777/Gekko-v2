using UnityEngine;

public class TurretShootBehaviour : IShooteable, IActivable, IServiceConsumer
{
    private readonly float _xRotation, _fireRate, _detectionRange;
    private BugDetection _detection;
    private GameManager _gm;
    private bool _initialized = false;
    private Transform _transform, _target, _muzzle;
    private ShooteableObjectFactory _bulletFactory;
    private float _timer = 0f;
    private TurretBullet _bulletPrefab;

    public bool Initialized => _initialized;


    public TurretShootBehaviour(float xRot, Transform t, float detectionRange, Transform muzzle, float fireRate,
                                TurretBullet prefab)
    {
        _xRotation = xRot;
        _transform = t;
        _muzzle = muzzle;
        _fireRate = fireRate;
        _detectionRange = detectionRange;
        _bulletPrefab = prefab;
    }

    private bool TryResolveTarget()
    {
        if(_detection != null) return true;

        if(!TryResolveGameManager() || _gm.Pj == null) return false;

        _target = _gm.Pj.transform;
        _detection = new BugDetection(_transform, _target, _detectionRange);
        return true;
    }

    public void ArtificialUpdate()
    {
        if(!_initialized || !TryResolveTarget()) return;

        if(_detection.IsTargetInRange())
        {
            //LookAtTarget();

            if(_timer >= _fireRate)
            {
                //Play PS
                Vector3 dir = CalculateDirection(_transform.position, _target.position);
                Shoot(dir.normalized);
                _timer = 0f;
                return;
            }

            _timer += Time.deltaTime;
        }
    }

    private void LookAtTarget()
    {
        Vector3 toTarget = CalculateDirection(_transform.position, _target.position);
        toTarget.y = 0f;

        if(toTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);

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

    private Vector3 CalculateDirection(Vector3 a, Vector3 b) => b - a;
}
