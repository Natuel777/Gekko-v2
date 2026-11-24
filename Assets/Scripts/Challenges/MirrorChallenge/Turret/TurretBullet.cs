using UnityEngine;
using UnityEngine.VFX;

public class TurretBullet : ShooteableObject, IReflectable
{
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;
    private int _lastReflectFrame = -1;

    [Header("Purified look (when bouncing off a mirror)")]
    [Tooltip("VFX de la bala (VFX_BulletTurret). Al rebotar pasa a usar el asset purificado.")]
    [SerializeField] private VisualEffect _vfx;
    [Tooltip("Mismo VFX con los colores turquesa de la purificación.")]
    [SerializeField] private VisualEffectAsset _vfxPurificada;
    [Tooltip("Trail de la bala. Al rebotar pasa a usar el material purificado.")]
    [SerializeField] private TrailRenderer _trail;
    [Tooltip("Mismo material del trail con los colores turquesa de la purificación.")]
    [SerializeField] private Material _trailPurificado;

    // The originals, to go back to the corrupted look when the pool reuses the bullet.
    private VisualEffectAsset _vfxOriginal;
    private Material _trailOriginal;

    // A mirror sent it back (see Reflect): from then on it is a purified bullet. It doesn't damage Gekko and, if it reaches a turret,
    // it purifies it. Turned off in OnEnable because the pool reuses bullets.
    private bool _reflected;

    // The bullet is a trigger that moves by transform: without a Rigidbody around, Unity doesn't notify (OnTriggerEnter) when it
    // touches a static collider, like a mirror's. Kinematic and without gravity: it isn't moved by physics, it only enables the events.
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

    // Mirror without a bounce point: sends it back the way it came, fully inverting the direction (it is not a bounce off the normal).
    public void Reflect() => ReflectTowards(-_direction);

    // Mirror with a bounce point: it goes towards direction (unit, non-zero; see Mirror). From here on it is a purified bullet.
    // If two colliders of the same mirror notify in the same frame it only counts once: with Reflect(), inverting twice would leave it
    // as it was.
    public void ReflectTowards(Vector3 direction)
    {
        if(_lastReflectFrame == Time.frameCount) return;

        _lastReflectFrame = Time.frameCount;
        _direction = direction.normalized;
        _reflected = true;

        // Now it flies another way: the smoke must stay behind and the arrow must point towards where it is going.
        FaceDirection();
        AplicarLook(true);
    }

    // Corrupted (violet) or purified (turquoise) look. Changing the VFX asset restarts it: the old smoke disappears and the
    // turquoise one starts from the bounce point. The trail changes color entirely, including what was already drawn.
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

            // Reused by the pool: without this it would draw a trail from where it died last time.
            if(!purificada) _trail.Clear();
        }
    }

    // The bullet looks where it flies. The VFX is oriented with the transform (the arrow runs along its Z axis and the smoke comes out of -Z),
    // so without this the bullet is born with the turret's rotation, which doesn't turn towards Gekko, and doesn't point where it is going. With a
    // null direction the rotation is left alone (LookRotation warns about it).
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
            // Call PSFactory for the hit effect
            _bulletFactory.ReturnBullet(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Sent back by a mirror: it is already purified, so it doesn't damage Gekko. If it reaches a turret (the collider may be
        // in a child), it purifies it and the bullet goes back to the pool. Anything else is ignored.
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
            Debug.Log("The cops, the cops...");
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);

            //if(_hitEffect != null) _hitEffect.Play();

            ReturnToPool();
        }
    }

    // _bulletFactory is only resolved in Update when its life runs out: if it hits before that, it is still null.
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
