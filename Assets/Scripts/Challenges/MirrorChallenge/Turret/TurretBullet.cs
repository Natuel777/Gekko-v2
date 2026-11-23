using UnityEngine;
using UnityEngine.VFX;

public class TurretBullet : ShooteableObject, IReflectable
{
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;
    private int _lastReflectFrame = -1;

    [Header("Look purificado (al rebotar en un espejo)")]
    [Tooltip("VFX de la bala (VFX_BulletTurret). Al rebotar pasa a usar el asset purificado.")]
    [SerializeField] private VisualEffect _vfx;
    [Tooltip("Mismo VFX con los colores turquesa de la purificación.")]
    [SerializeField] private VisualEffectAsset _vfxPurificada;
    [Tooltip("Trail de la bala. Al rebotar pasa a usar el material purificado.")]
    [SerializeField] private TrailRenderer _trail;
    [Tooltip("Mismo material del trail con los colores turquesa de la purificación.")]
    [SerializeField] private Material _trailPurificado;

    // Lo original, para volver al look corrupto cuando la pool reutiliza la bala.
    private VisualEffectAsset _vfxOriginal;
    private Material _trailOriginal;

    // Un espejo la devolvió (ver Reflect): desde ahí es una bala purificada. No daña a Gekko y, si alcanza a una torreta, la
    // purifica. Se apaga en OnEnable porque la pool reutiliza las balas.
    private bool _reflected;

    // La bala es un trigger que se mueve por transform: sin un Rigidbody de por medio Unity no avisa (OnTriggerEnter) cuando
    // toca un collider estático, como el de un espejo. Cinemático y sin gravedad: no se mueve por física, solo habilita los eventos.
    private void Awake()
    {
        if(!TryGetComponent(out Rigidbody rb)) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;

        if(_vfx != null) _vfxOriginal = _vfx.visualEffectAsset;
        if(_trail != null) _trailOriginal = _trail.sharedMaterial;
    }

    private void OnEnable()
    {
        _currentLifetime = _lifetime;
        _reflected = false;
        AplicarLook(false);
    }

    public void Shoot(Vector3 direction)
    {
        _direction = direction;
        FaceDirection();
    }

    // Invierte la dirección por completo (no es un rebote contra la normal). Si dos colliders del mismo espejo avisan en el
    // mismo frame solo cuenta una vez: invertir dos veces la dejaría como estaba.
    public void Reflect()
    {
        if(_lastReflectFrame == Time.frameCount) return;

        _lastReflectFrame = Time.frameCount;
        _direction = -_direction;
        _reflected = true;

        // Ahora vuela para el otro lado: el humo tiene que quedar atrás y la flecha de punta hacia donde va.
        FaceDirection();
        AplicarLook(true);
    }

    // Look corrupto (violeta) o purificado (turquesa). Cambiar el asset del VFX lo reinicia: el humo viejo desaparece y
    // arranca el turquesa desde el punto del rebote. El trail cambia de color entero, también lo ya dibujado.
    private void AplicarLook(bool purificada)
    {
        if(_vfx != null && _vfxOriginal != null && _vfxPurificada != null)
        {
            VisualEffectAsset asset = purificada ? _vfxPurificada : _vfxOriginal;
            if(_vfx.visualEffectAsset != asset) _vfx.visualEffectAsset = asset;
        }

        if(_trail != null && _trailOriginal != null && _trailPurificado != null)
        {
            _trail.sharedMaterial = purificada ? _trailPurificado : _trailOriginal;

            // Reutilizada por la pool: sin esto dibujaría un trail desde donde murió la vez anterior.
            if(!purificada) _trail.Clear();
        }
    }

    // La bala mira hacia donde vuela. El VFX se orienta con el transform (la flecha va a lo largo de su eje Z y el humo sale por -Z),
    // así que sin esto la bala nace con la rotación de la torreta, que no gira hacia Gekko, y no apunta hacia donde va. Con una
    // dirección nula no se toca la rotación (LookRotation avisa con un warning).
    private void FaceDirection()
    {
        if(_direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(_direction);
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
        // Devuelta por un espejo: ya está purificada, así que no daña a Gekko. Si alcanza a una torreta (el collider puede estar
        // en un hijo), la purifica y la bala vuelve a la pool. Cualquier otra cosa se ignora.
        if(_reflected)
        {
            ChallengeTurret turret = other.GetComponentInParent<ChallengeTurret>();

            if(turret == null) return;

            turret.Purify();
            ReturnToPool();
            return;
        }

        if(other.TryGetComponent(out Player player))
        {
            Debug.Log("La yuta, la yuta...");
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);

            //if(_hitEffect != null) _hitEffect.Play();

            ReturnToPool();
        }
    }

    // _bulletFactory recién se resuelve en Update cuando se acaba la vida: si pega antes, todavía es null.
    private void ReturnToPool()
    {
        if(TryResolveBulletFactory()) _bulletFactory.ReturnBullet(this);
    }

    private bool TryResolveBulletFactory()
    {
        if(_bulletFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _bulletFactory);
    }
}
