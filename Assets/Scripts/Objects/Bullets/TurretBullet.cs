using UnityEngine;

public class TurretBullet : ShooteableObject
{
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;

    private void OnEnable() {_currentLifetime = _lifetime;}

    public void Shoot(Vector3 direction)
    {
        _direction = direction;
    }

    private void Update()
    {
        transform.position += _direction * data.speed * Time.deltaTime;
        _currentLifetime -= Time.deltaTime;

        if(_currentLifetime <= 0 && TryResolveBulletFactory())
        {
            //Llamar a PSFactory para hit effect
            _bulletFactory.ReturnBullet(this);
        }
    }

    private bool TryResolveBulletFactory()
    {
        if(_bulletFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _bulletFactory);
    }
}
