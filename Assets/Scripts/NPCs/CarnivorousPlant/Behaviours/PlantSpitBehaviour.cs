using UnityEngine;

public class PlantSpitBehaviour
{
    private readonly Transform _head;
    private readonly Transform _firePoint;
    private readonly BugBullet _venomPrefab;
    private readonly float _initialShootInterval;
    private readonly bool _rotateToPlayer;
    private readonly float _rotationSpeed;
    private readonly System.Action _onShoot;

    private Transform _target;
    private GameObject _muzzlePS;
    private float _shootInterval;

    public PlantSpitBehaviour(Transform head, Transform firePoint,
                                BugBullet venomPrefab, float shootInterval,
                                bool rotateToPlayer, float rotationSpeed,
                                GameObject muzzlePS, System.Action onShoot = null)
    {
        _onShoot = onShoot;
        _head = head;
        _firePoint = firePoint;
        _venomPrefab = venomPrefab;
        _initialShootInterval = shootInterval;
        _rotateToPlayer = rotateToPlayer;
        _rotationSpeed = rotationSpeed;
        _shootInterval = shootInterval;
        _muzzlePS = muzzlePS;
    }

    // La planta le está apuntando y disparando a alguien (entre StartSpitting y StopSpitting).
    public bool IsSpitting => _target != null;

    public void StartSpitting(Transform target)
    {
        _target = target;
        _shootInterval = _initialShootInterval;
    }

    public void StopSpitting()
    {
        _target = null;
    }

    public void ArtificialUpdate()
    {
        if(_target == null || _firePoint == null) return;

        Vector3 toTarget = _target.position - _firePoint.position;
        Vector3 direction = toTarget.normalized;

        if(_rotateToPlayer && _head != null && direction.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            _head.rotation = Quaternion.Slerp(_head.rotation, lookRot, _rotationSpeed * Time.deltaTime);
        }

        ShootSequence(direction);
    }

    private void ShootSequence(Vector3 direction)
    {
        if(ShooteableObjectFactory.Instance == null || _venomPrefab == null) return;

        if(_shootInterval > 0)
        {
            _shootInterval -= Time.deltaTime;
            return;
        }
        BugBullet venom = ShooteableObjectFactory.Instance.GetBullet<BugBullet>(_venomPrefab, _firePoint.position, Quaternion.identity);

        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.PlantShoot);
        
        if(_muzzlePS != null)
        {
            foreach(ParticleSystem item in _muzzlePS.GetComponentsInChildren<ParticleSystem>())
                item.Play();
        }

        if(venom == null) return;

        venom.SetDirection(direction);
        _onShoot?.Invoke();
        _shootInterval = _initialShootInterval;
    }
}
