using UnityEngine;

public class TurretBullet : ShooteableObject
{
    private Vector3 _direction;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;
    
    [SerializeField] private float _speed;

    public void Shoot(Vector3 direction)
    {
        _direction = direction;
    }

    private void Update()
    {
        transform.position += _direction * _speed * Time.deltaTime;
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
