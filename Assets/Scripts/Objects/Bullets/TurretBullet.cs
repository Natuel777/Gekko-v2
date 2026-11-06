using UnityEngine;

public class TurretBullet : ShooteableObject, IReflectable
{
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;
    private int _lastReflectFrame = -1;

    // La bala es un trigger que se mueve por transform: sin un Rigidbody de por medio Unity no avisa (OnTriggerEnter) cuando
    // toca un collider estático, como el de un espejo. Cinemático y sin gravedad: no se mueve por física, solo habilita los eventos.
    private void Awake()
    {
        if(!TryGetComponent(out Rigidbody rb)) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnEnable() {_currentLifetime = _lifetime;}

    public void Shoot(Vector3 direction)
    {
        _direction = direction;
    }

    // Invierte la dirección por completo (no es un rebote contra la normal). Si dos colliders del mismo espejo avisan en el
    // mismo frame solo cuenta una vez: invertir dos veces la dejaría como estaba.
    public void Reflect()
    {
        if(_lastReflectFrame == Time.frameCount) return;

        _lastReflectFrame = Time.frameCount;
        _direction = -_direction;
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

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out Player player))
        {
            Debug.Log("La yuta, la yuta...");
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);

            //if(_hitEffect != null) _hitEffect.Play();
            
            // _bulletFactory recién se resuelve en Update cuando se acaba la vida: si pega antes, acá todavía es null.
            if(TryResolveBulletFactory()) _bulletFactory.ReturnBullet(this);
        }
    }

    private bool TryResolveBulletFactory()
    {
        if(_bulletFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _bulletFactory);
    }
}
