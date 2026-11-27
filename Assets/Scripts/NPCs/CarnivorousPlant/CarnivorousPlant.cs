using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CarnivorousPlant : MonoBehaviour, IDamageable, IHitOncePerLick, IParticleSystemTarget, ITongueContactListener, IServiceConsumer
{
    public CarnivorousPlantDataSO data;
    public Transform playerTransform;
    [SerializeField] private BugBullet _venomPrefab;

    [Header("Attacks")]
    [Tooltip("Si está apagado, la planta no ataca cuerpo a cuerpo (no muerde), aunque Gekko esté en el rango del mordisco. Prendido, solo muerde cuando no está protegida por el escudo (Is Shielded).")]
    [SerializeField] private bool _meleeAttack = true;
    [Tooltip("Si está apagado, la planta no escupe veneno. Prendido, solo escupe cuando no está protegida por el escudo (Is Shielded).")]
    [SerializeField] private bool _spitAttack = true;

    [Header("Transforms")]
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _firePoint;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem _purifiedParticle;
    [SerializeField] private GameObject _muzzlePS;
    [SerializeField] private AudioSource _purifiedSound;
    [SerializeField] private Animator _animatorCarnivorousPlant;
    [Tooltip("Objetos a desactivar al purificar la planta (ej. la línea de conexión de corrupción hacia la enredadera).")]
    [SerializeField] private GameObject[] _hideOnPurified;

    [Tooltip("The reference for the 'Selected' Particle System. Variable inherited by IParticleSystemTarget")]
    [SerializeField] private ParticleSystem _indicator;
    public bool CanBeTargeted => !IsPurified;
    public ParticleSystem Indicator => _indicator;

    [Header("Head Aim")]
    [Tooltip("Hueso que rota mirando al player mientras la planta le apunta y le dispara (en el boss, joint7_R). Vacío = la planta no mueve ningún hueso. Usa el Rotation Speed del DataSO y respeta su Rotate To Player.")]
    [SerializeField] private Transform _aimJoint;
    [Tooltip("Eje LOCAL del hueso que tiene que apuntar al player. Si la cabeza mira de costado o para otro lado, probá otro eje (±X, ±Y, ±Z). Al seleccionar la planta se ve como una línea cian en la Scene.")]
    [SerializeField] private Vector3 _aimJointAxis = Vector3.forward;
    [Tooltip("Segundos que tarda el hueso en pasar de la pose de la animación a mirar al player, y en volver.")]
    [Min(0f)]
    [SerializeField] private float _aimBlendTime = 0.25f;

    [Header("Blocked Hit Feedback")]
    [Tooltip("Sprite que aparece donde pega la lengua del Gekko cuando la planta es invulnerable (preparando el mordisco, embistiendo, etc.). Lo crea el SpriteRendererFactory de la escena.")]
    [SerializeField] private SRType _blockedHitMarker = SRType.Cross;
    [Tooltip("Cuánto se adelanta la marca hacia la cámara desde la superficie del collider, para que la malla de la planta no la tape.")]
    [Min(0f)]
    [SerializeField] private float _blockedHitMarkerOffset = 0.2f;

    [Header("Vulnerable Feedback")]
    [Tooltip("Partículas de 'mareada' (ej. Mareado, sobre la cabeza del jefe): se ven solo mientras Gekko le puede pegar (misma condición que el daño de la lengua: sin escudo, sin purificar y en su ventana vulnerable). Vacío = nada.")]
    [SerializeField] private GameObject _vulnerableFx;

    private TrailRenderer[] _vulnerableFxTrails;
    private bool _vulnerableFxOn = true; // el prefab lo trae prendido: Start lo apaga

    private bool _playerInRange = false;
    private bool _purified = false;
    private int _hits;
    private float _staggerUntil;
    private bool _hasHurtTrigger;
    private SkinnedMeshRenderer[] _skinnedMeshRenderers;
    private SpriteRendererFactory _spriteRendererFactory;
    private ParticleSystemFactory _psFactory;
    private PlantDamageFlash _damageFlash;
    private PlantPaintPurify _paint;
    // Where the tongue last hit (OnTongueContact arrives right before Damage): used by the paint sparks.
    private Vector3 _lastHitPoint;
    private float _lastHitTime = -1f;

    public bool IsPurified => _purified;
    public bool IsShielded { get; private set; }
    // Locked in (e.g. LvlTwo: inside the vines): it doesn't attack, it neither spits nor bites. The challenge handles it.
    public bool AttackLocked { get; private set; }
    // What the plant may do right now. The Inspector switches (Melee Attack, Spit Attack) turn an attack off for good; the shield
    // (the roots, see SetShielded) turns both off for as long as it is up, whatever the switches say.
    public bool CanMelee => _meleeAttack && !IsShielded;
    public bool CanSpit => _spitAttack && !IsShielded;
    public Animator Animator => _animatorCarnivorousPlant;
    // (hits received, hits needed)
    public event System.Action<int, int> Hit;
    public event System.Action Purified;

    #region FSM
    public PlantSpitBehaviour spitBehaviour;
    private PlantAimBehaviour _aimBehaviour;
    private PlantBiteWindup _biteWindup;
    private PlantHurtReaction _hurtReaction;
    private PlantTiredAnimation _tired;
    public BugDetection detection;
    public BugDetection biteDetection;
    private StateMachine _eventFSM;
    #endregion

    #region States
    public PlantIdleState IdleState { get; private set; }
    public PlantAlertState AlertState { get; private set; }
    public PlantAttackState AttackState { get; private set; }
    public PlantBiteState BiteState { get; private set; }
    public PlantPurifiedState PurifiedState { get; private set; }
    #endregion

    #region Bite
    private Vector3 _headStartLocalPos;
    private Quaternion _headStartLocalRot;
    private bool _isLunging = false;
    private PlantBiteHitBox _hitBox;
    private bool _biteHitApplied;
    #endregion

    private void Awake()
    {
        _eventFSM = new StateMachine();
        CacheHurtTrigger();
        _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        // Hit-by-hit painted purification (DataSO > Paint On Hit): replaces the damage flash, which would change the transition material
        // in the middle of the painting.
        if(data.paintOnHit && data.paintShader != null && data.purifiedMaterial != null)
            _paint = new PlantPaintPurify(this, _skinnedMeshRenderers, data);

        // The damage flash is optional: it only exists if the DataSO has a Damaged Material (today, the boss's).
        else if(data.damagedMaterial != null)
            _damageFlash = new PlantDamageFlash(_skinnedMeshRenderers, data.damagedMaterial, data.damagedMaterialSeconds);

        IdleState = new PlantIdleState(this);
        AlertState = new PlantAlertState(this);
        AttackState = new PlantAttackState(this);
        BiteState = new PlantBiteState(this);
        PurifiedState = new PlantPurifiedState(this);

        // The bite windup is optional (today, only the boss) and the spit also uses it, which is why it is looked up beforehand.
        _biteWindup = GetComponent<PlantBiteWindup>();

        // The reaction to hits that don't purify is also optional (today, only the boss).
        _hurtReaction = GetComponent<PlantHurtReaction>();

        // And so is the tired-plant pose for the interval between bites (today, only the boss): see SetInBiteInterval.
        _tired = GetComponent<PlantTiredAnimation>();

        spitBehaviour = new PlantSpitBehaviour(_head, _firePoint, _venomPrefab, data.shootInterval, data.rotateToPlayer, data.rotationSpeed, _muzzlePS, PlaySpit, _biteWindup);

        // The bone aim is optional: it only exists if the plant has an _aimJoint assigned (today, the boss).
        if(_aimJoint != null && data.rotateToPlayer)
            _aimBehaviour = new PlantAimBehaviour(_aimJoint, _aimJointAxis, data.rotationSpeed, _aimBlendTime);

        // The bite's HitBox is also optional: it only exists if the prefab has a child with PlantBiteHitBox (today, the boss).
        // Without it the plant bites as usual (damage at the peak of the lunge). It starts off: it is only turned on during
        // the lunge.
        _hitBox = GetComponentInChildren<PlantBiteHitBox>(true);
        if(_hitBox != null)
        {
            _hitBox.Initialize(this);
            _hitBox.gameObject.SetActive(false);
        }

        if(_head != null)
        {
            _headStartLocalPos = _head.localPosition;
            _headStartLocalRot = _head.localRotation;
        }
    }

    private void Start()
    {
        // Resolved here (not in Awake) to guarantee the Player has already set GameManager.Instance.Pj.
        playerTransform = GameManager.Instance.Pj.transform;
        detection = new BugDetection(transform, playerTransform, data.detectionRange);
        biteDetection = new BugDetection(transform, playerTransform, data.biteRange);
        SetState(IdleState);

        if(_vulnerableFx != null)
        {
            _vulnerableFxTrails = _vulnerableFx.GetComponentsInChildren<TrailRenderer>(true);
            SetVulnerableFx(false);
        }
    }

    // The dizzy stars show exactly when a tongue hit does damage (same condition as OnTongueContact). For the boss that is the
    // interval between bites (the tired pose), minus the hit reaction: after a hit they go out and come back when it can be hit again.
    private void UpdateVulnerableFx()
    {
        if(_vulnerableFxTrails == null) return;
        SetVulnerableFx(!_purified && !IsShielded && CanBeHit);
    }

    private void SetVulnerableFx(bool on)
    {
        if(on == _vulnerableFxOn) return;
        _vulnerableFxOn = on;

        // The whole object goes on/off: not just the stars but also their trails (the lines circling over the head) and its
        // spin animation. Off at once: stars or lines still floating while the plant prepares the next bite would say the wrong thing.
        _vulnerableFx.SetActive(on);
        if(!on) return;

        // Turned back on: the stars start over (Play On Awake) and the trails don't draw a stroke from where they were last time.
        foreach(TrailRenderer trail in _vulnerableFxTrails)
            if(trail != null) trail.Clear();
    }

    public bool IsPlayerInBiteRange() => biteDetection.IsTargetInRange();

    // While a cinematic runs (ParkourCinematicTrigger, the boss cameras when a core is broken) the plant doesn't attack:
    // the FSM is skipped, just like HeavyBeetle. Without this it kept spitting at a frozen Gekko with the camera elsewhere.
    private bool _frozenForCinematic;

    private void OnEnable()
    {
        EventManager.Subscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Subscribe("OnCinematicEnd", UnfreezeForCinematic);
    }

    private void FreezeForCinematic() => _frozenForCinematic = true;
    private void UnfreezeForCinematic() => _frozenForCinematic = false;

    private void OnDisable()
    {
        EventManager.Unsubscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Unsubscribe("OnCinematicEnd", UnfreezeForCinematic);
        _frozenForCinematic = false;

        // Turned off mid-lunge: the coroutine dies with it, so the hitbox can't stay on nor the plant stay
        // "lunging" forever.
        _isLunging = false;
        InBiteInterval = false;
        if(_tired != null) _tired.Stop();
        SetHitBoxActive(false);
    }

    private void SetHitBoxActive(bool active)
    {
        if(_hitBox != null) _hitBox.gameObject.SetActive(active);
    }

    public PlantBiteWindup BiteWindup => _biteWindup != null && _biteWindup.IsAvailable ? _biteWindup : null;

    private void Update()
    {
        if(_purified) return;

        if(_frozenForCinematic) return;

        if(Time.time < _staggerUntil) return;

        // Locked in: neither the FSM nor the detection run. When released, if Gekko is already in range, the next
        // UpdateDetection sees Gekko as just entered and the plant starts attacking.
        if(AttackLocked) return;

        _eventFSM.UpdateState();
        UpdateDetection();
    }

    // LateUpdate and not Update: the Animator rewrites the bones' pose after Update, so the bone's rotation has to
    // go afterwards so it doesn't get overwritten (see PlantAimBehaviour).
    private void LateUpdate()
    {
        // Before the return below: the damage flash also runs on plants without aim, and during the stun (Update
        // is stopped).
        if(_damageFlash != null) _damageFlash.ArtificialLateUpdate();
        // Also before the return: it has to update during the stun and when purified (to turn the stars off).
        UpdateVulnerableFx();

        if(_aimBehaviour == null) return;

        // Aims while spitting. Not when stunned by a tongue hit, purified or unable to spit (shield up, Spit Attack off): there
        // the bone goes back to the animation.
        // Nor while the bite windup runs: the bones belong to that animation and the aim resumes when it finishes
        // (rising from zero, without a jump), so they don't fight over joint7_R.
        // spitBehaviour is read every frame because BossPlantPhases replaces it when the phase changes.
        bool windupPlaying = _biteWindup != null && _biteWindup.IsPlaying;
        bool aiming = !_purified && Time.time >= _staggerUntil && !windupPlaying && CanSpit && spitBehaviour != null && spitBehaviour.IsSpitting;

        _aimBehaviour.ArtificialLateUpdate(playerTransform, aiming);
    }

    private void UpdateDetection()
    {
        bool inRange = detection.IsTargetInRange();

        if(inRange && !_playerInRange)
        {
            _playerInRange = true;
            SendEvent(CreatureEvent.GekkoEnter);
        }

        else if(!inRange && _playerInRange)
        {
            _playerInRange = false;
            SendEvent(CreatureEvent.GekkoExit);
        }
    }

    public void SetState(IState state) => _eventFSM.SetState(state);
    public void SendEvent(CreatureEvent e) => _eventFSM.SendEvent(e);

    // Interval between bites (set by PlantBiteState): the only window in which a plant whose data asks for
    // vulnerableOnlyInBiteInterval (today, the boss) can be damaged. The lunge never counts, even if it runs inside the interval.
    public bool InBiteInterval { get; private set; }

    // Entering the interval triggers the "tired plant" animation (PlantTiredAnimation, optional: today, only the boss): it is the signal
    // that it can now be hit. It lasts as long as the interval (the DataSO's Bite Interval) and is cut when leaving it. All
    // paths go through here: time runs out and the next bite's windup starts, the player leaves the range, the
    // plant is purified. It only notifies when the value changes.
    public void SetInBiteInterval(bool value)
    {
        if(InBiteInterval == value) return;

        InBiteInterval = value;

        if(_tired == null) return;

        if(value) _tired.Play(data.biteInterval);
        else _tired.Stop();
    }

    // While a hit reaction (PlantHurtReaction) runs the plant can't be damaged, just like during the bite
    // windup. It also counts for OnTongueContact's blocked-hit mark.
    private bool IsHurtReacting => _hurtReaction != null && _hurtReaction.IsPlaying;

    // Without melee (Melee Attack off) there is no interval between bites to wait for: it can be hit at any time, otherwise a plant
    // that only spits could never be purified.
    private bool CanBeHit => !_isLunging && !IsHurtReacting && (!data.vulnerableOnlyInBiteInterval || InBiteInterval || !_meleeAttack);

    public bool TryResolveService()
    {
        if(_spriteRendererFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _spriteRendererFactory);
    }

    private bool TryResolvePSFactory()
    {
        if(_psFactory != null) return true;

        return ServiceLocator.Instance.TryGet(out _psFactory);
    }

    public void OnTongueContact(Vector3 tongueStart, Vector3 tongueTip)
    {
        if(_purified || IsShielded) return;

        if(CanBeHit)
        {
            _lastHitPoint = TongueEntryPoint(tongueStart, tongueTip);
            _lastHitTime = Time.time;
            ShowHittedPS(_lastHitPoint);
            return;
        }

        ShowBlockedHit(TongueEntryPoint(tongueStart, tongueTip));
    }

    private Vector3 TongueEntryPoint(Vector3 start, Vector3 tip)
    {
        Collider col = GetComponent<Collider>();
        Vector3 path = tip - start;
        float length = path.magnitude;

        if(length > 0.0001f && col.Raycast(new Ray(start, path / length), out RaycastHit hit, length + 0.1f))
            return hit.point;

        return col.ClosestPoint(tip);
    }

    private void ShowBlockedHit(Vector3 point)
    {
        if(!TryResolveService()) return;

        Camera cam = Camera.main;
        Quaternion rotation = Quaternion.identity;

        if(cam != null)
        {
            rotation = cam.transform.rotation;
            point += (cam.transform.position - point).normalized * _blockedHitMarkerOffset;
        }

        _spriteRendererFactory.Create(_blockedHitMarker, point, rotation);
    }

    private void ShowHittedPS(Vector3 point)
    {
        if(!TryResolvePSFactory()) return;

        Camera cam = Camera.main;
        Quaternion rotation = Quaternion.identity;

        if(cam != null)
        {
            rotation = cam.transform.rotation;
            point += (cam.transform.position - point).normalized;
        }

        GenericPS ps = _psFactory.Create(PSType.VenomHitSmall, point, rotation);
        if(ps != null) ps.Initialize();
    }

    public void Damage(float dmg)
    {
        if(_purified || IsShielded || !CanBeHit) return;

        int hitsNeeded = Mathf.Max(1, data.hitsToPurify);
        _hits++;
        Hit?.Invoke(_hits, hitsNeeded);

        if(_hits < hitsNeeded)
        {
            if(_paint != null) _paint.OnHit(_hits, hitsNeeded, RecentHitPoint());

            // Hitting a plant no longer costs Gekko health (commented out, not deleted, in case it needs to be turned on again).
            //if(data.hitHealthCost > 0f)
            //    EventManager.Trigger<float>("OnPlayerDamaged", data.hitHealthCost);

            PlayHurt();

            // Damage material for the DataSO's Damaged Material Seconds. Only on hits that count and don't purify: the final hit
            // is cut by PlayPurifiedFeedback, and blocked ones (invulnerable plant) never even get here.
            if(_damageFlash != null) _damageFlash.Play();

            // With PlantHurtReaction the hit triggers its animation, and the attack is paused for as long as it lasts so they don't fight over
            // the bones (the larger of that and the DataSO's stun is used). While it runs the plant is invulnerable: CanBeHit.
            float pauseSeconds = data.hurtStaggerSeconds;
            if(_hurtReaction != null && _hurtReaction.Play()) pauseSeconds = Mathf.Max(pauseSeconds, _hurtReaction.Duration);

            if(pauseSeconds > 0f)
            {
                _staggerUntil = Time.time + pauseSeconds;

                // Stunned: the FSM stops, so the bite windup is cut (if it was in one). PlantBiteState
                // restarts it from zero when the stun ends.
                if(_biteWindup != null) _biteWindup.Cancel();
            }

            return;
        }

        _purified = true;
        SetHitBoxActive(false);

        // The last hit on a multi-hit plant also plays, with the highest pitch (see Hurt Pitch Step).
        if(hitsNeeded > 1 && data.hurtPitchStep > 0f && AudioManager.instance)
            AudioManager.instance.PlayPitched(SoundNames.PlantHurt, 1f + data.hurtPitchStep * (hitsNeeded - 1));

        // Purifying a plant (the boss included) no longer costs Gekko health (commented out, not deleted, in case it needs to be turned
        // on again). With this, the DataSO's Purify Health Cost and Hit Health Cost have no effect.
        //EventManager.Trigger<float>("OnPlayerDamaged", data.purifyHealthCost);
        SetState(PurifiedState);
        Purified?.Invoke();
    }

    // Debug (Alt+8, see DebugController): purifies right away, without shield, without counting hits and without health cost.
    // It follows the same path as the last hit, so the cinematic, the challenge and the friendly gekko react the same way.
    public void DebugPurify()
    {
        if(_purified) return;

        if(_biteWindup != null) _biteWindup.Cancel();
        if(_hurtReaction != null) _hurtReaction.Cancel();
        _hits = Mathf.Max(1, data.hitsToPurify);
        _purified = true;
        SetHitBoxActive(false);
        SetState(PurifiedState);
        Purified?.Invoke();
    }

    public void Bite()
    {
        if(_purified || _head == null || _isLunging) return;

        StartCoroutine(BiteLungeRoutine());
        
        if(_animatorCarnivorousPlant != null) _animatorCarnivorousPlant.SetTrigger("nibble");
    }

    private IEnumerator BiteLungeRoutine()
    {
        _isLunging = true;
        _biteHitApplied = false;
        SetHitBoxActive(true);

        Vector3 toPlayer = (playerTransform.position - _head.position);
        toPlayer.Normalize();

        // Target advance in the head's parent's local space.
        Vector3 localOffset = _head.parent != null
            ? _head.parent.InverseTransformDirection(toPlayer) * data.biteLungeDistance
            : toPlayer * data.biteLungeDistance;

        Vector3 lungePos = _headStartLocalPos + localOffset;
        Quaternion lungeRot = Quaternion.LookRotation(toPlayer);

        float half = data.biteLungeDuration * 0.5f;

        // Forward phase: advances and rotates towards the player.
        float t = 0f;
        while(t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            _head.localPosition = Vector3.Lerp(_headStartLocalPos, lungePos, k);
            _head.rotation = Quaternion.Slerp(_head.rotation, lungeRot, k);
            yield return null;
        }

        // Peak of the lunge. With HitBox the trigger applies the damage; without it (small plants) it happens here, only once.
        if(_hitBox == null) OnBiteHitBoxContact(GameManager.Instance.Pj);

        // Return phase: goes back to the original pose.
        t = 0f;
        while(t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            _head.localPosition = Vector3.Lerp(lungePos, _headStartLocalPos, k);
            _head.localRotation = Quaternion.Slerp(_head.localRotation, _headStartLocalRot, k);
            yield return null;
        }

        // Make sure the exact base pose is set when finished.
        _head.localPosition = _headStartLocalPos;
        _head.localRotation = _headStartLocalRot;
        SetHitBoxActive(false);
        _isLunging = false;
    }

    public void OnBiteHitBoxContact(Player player)
    {
        if(_purified || _biteHitApplied || player == null) return;

        _biteHitApplied = true;
        ApplyBiteHit(player);
    }

    private void ApplyBiteHit(Player player)
    {
        if(player.TryGetComponent(out Rigidbody rb))
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            rb.AddForce(dir * data.biteKnockbackForce, ForceMode.Impulse);
        }

        EventManager.Trigger<float>("OnPlayerDamaged", data.biteDamage);
    }

    private void PlaySpit()
    {
        if(_animatorCarnivorousPlant != null) _animatorCarnivorousPlant.SetTrigger("splittle");
    }

    public void SetShielded(bool shielded) => IsShielded = shielded;

    public void SetAttackLocked(bool locked)
    {
        if(AttackLocked == locked) return;

        AttackLocked = locked;
        if(!locked) return;

        // Locked in: back to rest, neither spitting nor biting.
        _playerInRange = false;
        if(_biteWindup != null) _biteWindup.Cancel();
        if(IdleState != null) SetState(IdleState);
    }

    // The original Animator only has "nibble": the "hurt" trigger is optional.
    private void CacheHurtTrigger()
    {
        if(_animatorCarnivorousPlant == null || _animatorCarnivorousPlant.runtimeAnimatorController == null) return;

        foreach(AnimatorControllerParameter p in _animatorCarnivorousPlant.parameters)
        {
            if(p.name == "hurt" && p.type == AnimatorControllerParameterType.Trigger)
            {
                _hasHurtTrigger = true;
                return;
            }
        }
    }

    private void PlayHurt()
    {
        if(_hasHurtTrigger) _animatorCarnivorousPlant.SetTrigger("hurt");

        if(!AudioManager.instance) return;

        // With Hurt Pitch Step the pitch rises with each hit (1st low, the last one highest).
        if(data.hurtPitchStep > 0f) AudioManager.instance.PlayPitched(SoundNames.PlantHurt, 1f + data.hurtPitchStep * Mathf.Max(0, _hits - 1));
        else AudioManager.instance.Play(SoundNames.PlantHurt);
    }

    // The tongue's point if it belongs to this hit (it arrives in the same frame as Damage); otherwise null (the sparks go to the center).
    private Vector3? RecentHitPoint() => Time.time - _lastHitTime < 0.25f ? _lastHitPoint : (Vector3?)null;

    private void OnDestroy()
    {
        if(_paint != null) _paint.Dispose();
    }

    public void PlayPurifiedFeedback()
    {
        if(_damageFlash != null) _damageFlash.Cancel();

        if(_purifiedParticle != null) _purifiedParticle.Play();
        if(_purifiedSound != null) _purifiedSound.Play();
        ApplyPurifiedMaterial();
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);

        if(_hideOnPurified != null)
            foreach(GameObject go in _hideOnPurified)
                if(go != null) go.SetActive(false);
    }

    // One-way material change: the plant's purification is a terminal state, it is not reverted.
    private void ApplyPurifiedMaterial()
    {
        // With painted purification the change is not direct: it finishes being painted, flash, and only then Purified Material.
        if(_paint != null)
        {
            _paint.Complete(RecentHitPoint());
            return;
        }

        if(data.purifiedMaterial == null || _skinnedMeshRenderers == null) return;

        foreach(SkinnedMeshRenderer skinnedMesh in _skinnedMeshRenderers)
            if(skinnedMesh != null) skinnedMesh.sharedMaterial = data.purifiedMaterial;
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if(_aimJoint != null && _aimJointAxis.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(_aimJoint.position, _aimJoint.TransformDirection(_aimJointAxis.normalized) * 3f);
        }

        if(data == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.biteRange);

        PlantBiteHitBox hitBox = _hitBox != null ? _hitBox : GetComponentInChildren<PlantBiteHitBox>(true);
        if(hitBox != null) hitBox.DrawGizmo(Color.yellow);
    }
    #endif
}
