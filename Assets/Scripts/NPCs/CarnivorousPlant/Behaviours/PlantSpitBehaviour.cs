using UnityEngine;

// Escupe veneno hacia el target cada vez que se cumple el cooldown (shootInterval). Si la planta tiene PlantBiteWindup (hoy, el
// boss), cada escupida va precedida de su animación de preparación, que corre más rápida que la del mordisco:
// cooldown -> preparación -> disparo -> cooldown. La bala sale recién cuando la preparación termina. Sin PlantBiteWindup
// dispara apenas se cumple el cooldown, como siempre.
public class PlantSpitBehaviour : IShooteable
{
    private readonly Transform _head;
    private readonly Transform _firePoint;
    private readonly BugBullet _venom;
    private readonly float _initialShootInterval;
    private readonly bool _rotateToPlayer;
    private readonly float _rotationSpeed;
    private readonly System.Action _onShoot;
    private readonly PlantBiteWindup _windup;

    private Transform _target;
    private GameObject _muzzlePS;
    private float _shootInterval;
    private bool _windingUp;

    public PlantSpitBehaviour(Transform head, Transform firePoint,
                                BugBullet bulletType, float shootInterval,
                                bool rotateToPlayer, float rotationSpeed,
                                GameObject muzzlePS, System.Action onShoot = null,
                                PlantBiteWindup windup = null)
    {
        _onShoot = onShoot;
        _windup = windup;
        _head = head;
        _firePoint = firePoint;
        _venom = bulletType;
        _initialShootInterval = shootInterval;
        _rotateToPlayer = rotateToPlayer;
        _rotationSpeed = rotationSpeed;
        _shootInterval = shootInterval;
        _muzzlePS = muzzlePS;
    }

    public bool IsSpitting => _target != null;

    public void StartSpitting(Transform target)
    {
        _target = target;
        _shootInterval = _initialShootInterval;
        _windingUp = false;
    }

    public void StopSpitting()
    {
        _target = null;

        // Dejó de escupir en plena preparación (el player se acercó al rango del mordisco, se fue, la planta se purificó...):
        // no hay disparo y los huesos vuelven solos a la base.
        if(!_windingUp) return;

        _windingUp = false;
        if(_windup != null) _windup.Cancel();
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
        if(ShooteableObjectFactory.Instance == null || _venom == null) return;

        if(_windingUp)
        {
            // El disparo espera a que termine la preparación entera (Windup + Return).
            if(_windup != null && _windup.IsPlaying) return;

            _windingUp = false;

            // La cortaron (aturdimiento, se apagó el componente...): no hay disparo y vuelve a empezar el cooldown.
            if(_windup == null || !_windup.Completed)
            {
                _shootInterval = _initialShootInterval;
                return;
            }

            Shoot(direction);
            return;
        }

        if(_shootInterval > 0)
        {
            _shootInterval -= Time.deltaTime;
            return;
        }

        // Se acabó el cooldown. Sin preparación dispara de una; con preparación la arranca, y si Play() dice que no (todavía
        // vuelve de una cortada) se reintenta en el próximo frame.
        if(_windup == null || !_windup.IsAvailable)
        {
            Shoot(direction);
            return;
        }

        _windingUp = _windup.Play(_target, true);
    }

    public void Shoot(Vector3 direction)
    {
        BugBullet venom = ShooteableObjectFactory.Instance.GetBullet<BugBullet>(_venom, _firePoint.position, Quaternion.identity);

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
