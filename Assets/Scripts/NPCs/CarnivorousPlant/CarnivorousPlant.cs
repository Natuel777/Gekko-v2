using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CarnivorousPlant : MonoBehaviour, IDamageable, IHitOncePerLick, IParticleSystemTarget
{
    public CarnivorousPlantDataSO data;
    public Transform playerTransform;
    [SerializeField] private BugBullet _venomPrefab;

    [Header("Transforms")]
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _firePoint;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem _purifiedParticle;
    [SerializeField] private AudioSource _purifiedSound;
    [SerializeField] private Animator _animatorCarnivorousPlant;

    [Tooltip("The reference for the 'Selected' Particle System. Variable inherited by IParticleSystemTarget")]
    [SerializeField] private ParticleSystem _indicator;
    public bool CanBeTargeted => !IsPurified;
    public ParticleSystem Indicator => _indicator;

    private bool _playerInRange = false;
    private bool _purified = false;
    private int _hits;
    private float _staggerUntil;
    private bool _hasHurtTrigger;
    private SkinnedMeshRenderer[] _skinnedMeshRenderers;

    public bool IsPurified => _purified;
    public bool IsShielded { get; private set; }
    // (golpes recibidos, golpes necesarios)
    public event System.Action<int, int> Hit;
    public event System.Action Purified;

    #region FSM
    public PlantSpitBehaviour spitBehaviour;
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
    #endregion

    private void Awake()
    {
        _eventFSM = new StateMachine();
        CacheHurtTrigger();
        _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        IdleState = new PlantIdleState(this);
        AlertState = new PlantAlertState(this);
        AttackState = new PlantAttackState(this);
        BiteState = new PlantBiteState(this);
        PurifiedState = new PlantPurifiedState(this);

        spitBehaviour = new PlantSpitBehaviour(_head, _firePoint, _venomPrefab, data.shootInterval, data.rotateToPlayer, data.rotationSpeed);

        if(_head != null)
        {
            _headStartLocalPos = _head.localPosition;
            _headStartLocalRot = _head.localRotation;
        }
    }

    private void Start()
    {
        // Se resuelve acá (no en Awake) para garantizar que el Player ya seteó GameManager.Instance.Pj.
        playerTransform = GameManager.Instance.Pj.transform;
        detection = new BugDetection(transform, playerTransform, data.detectionRange);
        biteDetection = new BugDetection(transform, playerTransform, data.biteRange);
        SetState(IdleState);
    }

    public bool IsPlayerInBiteRange() => biteDetection.IsTargetInRange();

    private void Update()
    {
        if(_purified) return;

        if(Time.time < _staggerUntil) return;

        _eventFSM.UpdateState();
        UpdateDetection();
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

    // Purificación con la lengua: TongueManager llama Damage() sobre cualquier IDamageable.
    public void Damage(float dmg)
    {
        if(_purified) return;

        if(IsShielded) return;

        int hitsNeeded = Mathf.Max(1, data.hitsToPurify);
        _hits++;
        Hit?.Invoke(_hits, hitsNeeded);

        if(_hits < hitsNeeded)
        {
            if(data.hitHealthCost > 0f)
                EventManager.Trigger<float>("OnPlayerDamaged", data.hitHealthCost);

            PlayHurt();

            if(data.hurtStaggerSeconds > 0f)
                _staggerUntil = Time.time + data.hurtStaggerSeconds;

            return;
        }

        _purified = true;
        EventManager.Trigger<float>("OnPlayerDamaged", data.purifyHealthCost);
        SetState(PurifiedState);
        Purified?.Invoke();
    }

    // Lanza la embestida del head. Solo el _head se mueve; la base queda quieta.
    public void Bite()
    {
        if(_purified || _head == null || _isLunging) return;
        StartCoroutine(BiteLungeRoutine());
        if(_animatorCarnivorousPlant != null) _animatorCarnivorousPlant.SetTrigger("nibble");
    }

    private IEnumerator BiteLungeRoutine()
    {
        _isLunging = true;

        Vector3 toPlayer = (playerTransform.position - _head.position);
        toPlayer.Normalize();

        // Avance objetivo en espacio local del padre del head.
        Vector3 localOffset = _head.parent != null
            ? _head.parent.InverseTransformDirection(toPlayer) * data.biteLungeDistance
            : toPlayer * data.biteLungeDistance;

        Vector3 lungePos = _headStartLocalPos + localOffset;
        Quaternion lungeRot = Quaternion.LookRotation(toPlayer);

        float half = data.biteLungeDuration * 0.5f;
        bool hitApplied = false;

        // Fase ida: avanza y rota hacia el player.
        float t = 0f;
        while(t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            _head.localPosition = Vector3.Lerp(_headStartLocalPos, lungePos, k);
            _head.rotation = Quaternion.Slerp(_head.rotation, lungeRot, k);
            yield return null;
        }

        // Pico de la embestida: daño + knockback una sola vez.
        if(!hitApplied)
        {
            ApplyBiteHit();
            hitApplied = true;
        }

        // Fase vuelta: regresa a la pose original.
        t = 0f;
        while(t < half)
        {
            t += Time.deltaTime;
            float k = half > 0f ? t / half : 1f;
            _head.localPosition = Vector3.Lerp(lungePos, _headStartLocalPos, k);
            _head.localRotation = Quaternion.Slerp(_head.localRotation, _headStartLocalRot, k);
            yield return null;
        }

        // Asegurar la pose base exacta al terminar.
        _head.localPosition = _headStartLocalPos;
        _head.localRotation = _headStartLocalRot;
        _isLunging = false;
    }

    private void ApplyBiteHit()
    {
        Player player = GameManager.Instance.Pj;
        if(_purified || player == null) return;

        if(player.TryGetComponent(out Rigidbody rb))
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            rb.AddForce(dir * data.biteKnockbackForce, ForceMode.Impulse);
        }

        EventManager.Trigger<float>("OnPlayerDamaged", data.biteDamage);
    }

    // El escudo del desafío de purificación bloquea los golpes hasta que se rompen todos los núcleos.
    public void SetShielded(bool shielded) => IsShielded = shielded;

    // El Animator original solo tiene "nibble": el trigger "hurt" es opcional.
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
    }

    public void PlayPurifiedFeedback()
    {
        if(_purifiedParticle != null) _purifiedParticle.Play();
        if(_purifiedSound != null) _purifiedSound.Play();
        ApplyPurifiedMaterial();
    }

    // Cambio de material unidireccional: la purificación de la planta es un estado terminal, no se revierte.
    private void ApplyPurifiedMaterial()
    {
        if(data.purifiedMaterial == null || _skinnedMeshRenderers == null) return;

        foreach(SkinnedMeshRenderer skinnedMesh in _skinnedMeshRenderers)
            if(skinnedMesh != null) skinnedMesh.sharedMaterial = data.purifiedMaterial;
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if(data == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.biteRange);
    }
    #endif
}
