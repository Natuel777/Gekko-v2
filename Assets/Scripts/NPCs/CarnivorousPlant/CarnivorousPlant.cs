using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CarnivorousPlant : MonoBehaviour, IDamageable
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

    private bool _playerInRange = false;
    private bool _purified = false;

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
        detection = new BugDetection(this, playerTransform, data.detectionRange);
        biteDetection = new BugDetection(this, playerTransform, data.biteRange);
        SetState(IdleState);
    }

    public bool IsPlayerInBiteRange() => biteDetection.IsTargetInRange();

    private void Update()
    {
        if(_purified) return;

        _eventFSM.UpdateState();
        UpdateDetection();
    }

    private void UpdateDetection()
    {
        bool inRange = detection.IsTargetInRange();

        if(inRange && !_playerInRange)
        {
            _playerInRange = true;
            CancelInvoke(nameof(DelayedEnter));
            Invoke(nameof(DelayedEnter), data.reactionDelay);
        }

        else if(!inRange && _playerInRange)
        {
            _playerInRange = false;
            CancelInvoke(nameof(DelayedEnter));
            SendEvent(CreatureEvent.GekkoExit);
        }
    }

    public void SetState(IState state) => _eventFSM.SetState(state);
    public void SendEvent(CreatureEvent e) => _eventFSM.SendEvent(e);
    private void DelayedEnter() => SendEvent(CreatureEvent.GekkoEnter);

    // Purificación con la lengua: TongueManager llama Damage() sobre cualquier IDamageable.
    public void Damage(float dmg)
    {
        if(_purified) return;

        _purified = true;
        CancelInvoke(nameof(DelayedEnter));
        EventManager.Trigger<float>("OnPlayerDamaged", data.purifyHealthCost);
        SetState(PurifiedState);
    }

    // Lanza la embestida del head. Solo el _head se mueve; la base queda quieta.
    public void Bite()
    {
        if(_head == null || _isLunging) return;
        StartCoroutine(BiteLungeRoutine());
        _animatorCarnivorousPlant.SetTrigger("nibble");
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
        if(player == null) return;

        if(player.TryGetComponent(out Rigidbody rb))
        {
            Vector3 dir = (player.transform.position - transform.position).normalized;
            rb.AddForce(dir * data.biteKnockbackForce, ForceMode.Impulse);
        }

        EventManager.Trigger<float>("OnPlayerDamaged", data.biteDamage);
    }

    public void PlayPurifiedFeedback()
    {
        if(_purifiedParticle != null) _purifiedParticle.Play();
        if(_purifiedSound != null) _purifiedSound.Play();
    }

    private void OnDrawGizmos()
    {
        if(data == null) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.biteRange);
    }
}
