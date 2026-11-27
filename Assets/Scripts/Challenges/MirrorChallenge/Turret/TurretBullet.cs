using UnityEngine;
using UnityEngine.VFX;

public class TurretBullet : ShooteableObject, IReflectable
{
    private Vector3 _direction;
    [SerializeField] private float _lifetime = 3.5f;
    private float _currentLifetime;
    private ShooteableObjectFactory _bulletFactory;
    private ParticleSystemFactory _psFactory;
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

    [Header("Purified flight")]
    [Tooltip("Cuánto se frena la bala al purificarse (cuando un espejo la redirige): 0.25 = va al 75% de la velocidad que tenía corrupta. 0 = no se frena.")]
    [Range(0f, 1f)]
    [SerializeField] private float _purifiedSpeedReduction = 0.25f;

    private VisualEffectAsset _vfxOriginal;
    private Material _trailOriginal;
    private bool _reflected;

    // The turret that fired it (see Shoot). The muzzle is inside that turret's collider, so the bullet is born touching it: while it is
    // corrupted the owner's colliders are ignored. Once a mirror sends the bullet back the owner is a target like any other turret,
    // which is why this is decided here and not with layers (a layer would also ignore the purified bullet).
    private Transform _owner;

    // Already handed back to the pool during this life (see ReturnToPool).
    private bool _returned;

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

    // While a cinematic is running (OnCinematicStart/End, e.g. ShieldBreakCinematic when a turret is purified) Gekko is frozen and
    // the turrets stop shooting, but the bullets that were already flying used to keep going and hit him: he couldn't dodge them.
    private bool _frozenForCinematic;

    private void OnEnable()
    {
        _currentLifetime = _lifetime;
        _reflected = false;
        _returned = false;
        _owner = null;
        _frozenForCinematic = false;
        AplicarLook(false);

        EventManager.Subscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Subscribe("OnCinematicEnd", UnfreezeForCinematic);
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Unsubscribe("OnCinematicEnd", UnfreezeForCinematic);
    }

    // A corrupted bullet dissipates: if it only paused, it would hit Gekko right as he gets the controls back. A bullet already
    // sent back by a mirror only pauses and goes on afterwards, so the turret it is heading to still gets purified.
    private void FreezeForCinematic()
    {
        if(!_reflected)
        {
            ReturnToPool();
            return;
        }

        _frozenForCinematic = true;
    }

    private void UnfreezeForCinematic() => _frozenForCinematic = false;

    public void Shoot(Vector3 direction) => Shoot(direction, null);

    // owner: the turret that fires it (its root). The pool reuses bullets, so it is set on every shot.
    public void Shoot(Vector3 direction, Transform owner)
    {
        _owner = owner;
        _direction = direction;
        FaceDirection();
    }

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

    // For visual-only copies of the bullet (MirrorTutorial's demo shot): switches between the corrupted and purified look.
    public void ShowPurifiedLook(bool purified) => AplicarLook(purified);

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

    // Corrupted: the full speed of its data. Purified (a mirror sent it back): slowed down by _purifiedSpeedReduction. The pool reuses the
    // bullet, but OnEnable puts _reflected back to false, so every new shot starts at full speed.
    private float CurrentSpeed => _reflected ? data.speed * (1f - _purifiedSpeedReduction) : data.speed;

    private void Update()
    {
        if(_frozenForCinematic) return;

        transform.position += _direction * CurrentSpeed * Time.deltaTime;
        _currentLifetime -= Time.deltaTime;

        if(_currentLifetime <= 0)
        {
            // Call PSFactory for the hit effect
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other) => HandleContact(other);

    // Only fires if the bullet's collider is made solid (today it is a trigger): it gets exactly the same treatment.
    private void OnCollisionEnter(Collision other) => HandleContact(other.collider);

    private void HandleContact(Collider other)
    {
        // Several colliders can notify in the same physics step: the first one decides.
        if(_returned) return;

        // Sent back by a mirror: it is already purified, so it doesn't damage Gekko. If it reaches a turret (the collider may be
        // in a child) it purifies it, the shooter included, and the bullet goes back to the pool. Anything else is ignored.
        if(_reflected)
        {
            ChallengeTurret turret = other.GetComponentInParent<ChallengeTurret>();

            if(turret == null) return;

            turret.Purify();
            ReturnToPool();
            return;
        }

        if(other.TryGetComponent(out Player _))
        {
            Debug.Log("The cops, the cops...");
            EventManager.Trigger<float>("OnPlayerDamaged", data.damage);
            //if(_hitEffect != null) _hitEffect.Play();
            ReturnToPool();
            return;
        }

        //if(_hitEffect != null) _hitEffect.Play();
        if(StopsBullet(other)) Impact();
    }

    // What a corrupted bullet crashes into: solid colliders, and nothing else.
    //  - Triggers (zones, checkpoints, cameras...): the turret can stand inside one, and the bullet would die the moment it is born.
    //  - Its own turret: the muzzle is inside its collider (see _owner).
    //  - Mirrors: the mirror is the one that sends it back, and an inactive one lets it through. If the bullet died here, whether it
    //    reflects or not would depend on which of the two OnTriggerEnter Unity calls first.
    //  - Gekko's other colliders: only the one with the Player script hurts him, but none of them is "environment".
    private bool StopsBullet(Collider other)
    {
        if(other.isTrigger) return false;
        if(_owner != null && other.transform.IsChildOf(_owner)) return false;
        if(other.TryGetComponent(out MirrorSurface _) || other.TryGetComponent(out Mirror _)) return false;
        if(other.GetComponentInParent<Player>() != null) return false;

        return true;
    }

    // Hit something solid: it dissipates.
    private void Impact()
    {
        //if(TryResolvePSFactory())
        //{
        //    GenericPS ps = PSFactory.Create(PSType.VenomHit, transform.position, transform.rotation);
        //    ps?.Initialize();
        //}

        ReturnToPool();
    }

    // Once per life: two contacts in the same step (or a contact and the end of its lifetime) must not hand the bullet over twice. The
    // pool doesn't check it, so the bullet would end up twice in its stock and be given to two different shots at once.
    private void ReturnToPool()
    {
        if(_returned || !TryResolveBulletFactory()) return;

        _returned = true;
        _bulletFactory.ReturnBullet(this);
    }

    private bool TryResolveBulletFactory()
    {
        if(_bulletFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _bulletFactory);
    }

    private bool TryResolvePSFactory()
    {
        if(_psFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _psFactory);
    }
}
