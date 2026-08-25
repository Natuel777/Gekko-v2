using UnityEngine;

public class HeavyBeetle : MonoBehaviour, IDamageable, IParticleSystemTarget
{
    public HeavyBeetledataSO data;
    public Transform playerTransform;
    public BeetleGroupSO group;
    private bool _playerInRange = false;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem _angryParticle;

    [Tooltip("The reference for the 'Selected' Particle System. Variable inherited by IParticleSystemTarget")]
    [SerializeField] private ParticleSystem _indicator;

    public ParticleSystem Indicator => _indicator;

    [Header("Ground Check")]
    [SerializeField] private Transform _detectGroundPosition;
    [SerializeField] private LayerMask _groundMask;

    [Header("Obstacles")]
    [SerializeField] private LayerMask _obstacleLayers; // capas que el escarabajo esquiva y contra las que queda dazed al embestir
    // Si la máscara quedó vacía en el Inspector, cae a "Wall" para no romper el comportamiento histórico.
    public LayerMask ObstacleLayers => _obstacleLayers.value != 0 ? _obstacleLayers : (1 << LayerMask.NameToLayer("Wall"));

    private Vector3 _lastPosition;
    private const float GroundCheckEpsilon = 0.0001f;

    private Camera _mainCamera;

    // El flip de mareo ahora es por código (DazedFlip), no por Animator.
    // private Animator _anim;
    // private static readonly int TurnedInsideOutHash = Animator.StringToHash("IsturnedInsideOut");

    #region FSM
    public WanderMovement wanderMovement;
    public ChargeMovement chargeMovement;
    public LookAtTarget lookAt;
    public DazedFlip dazedFlip;
    public BugDetection detection;
    private HeavyBeetleCollision _collision;
    private StateMachine _eventFSM;
    #endregion

    #region States
    public BeetlePatrolState PatrolState { get; private set; }
    public BeetleAlertState AlertState { get; private set; }
    public BeetleChargeState ChargeState { get; private set; }
    public BeetleRecalibrateState RecalibrateState { get; private set; }
    public BeetleDazedState DazedState { get; private set; }
    #endregion

    #region Getters
    public bool IsCharging { get; private set; }
    public bool IsDazed { get; private set; }
    #endregion

    #region Initialization
    private void Awake()
    {
        _eventFSM = new StateMachine();
        // _anim = GetComponentInChildren<Animator>();

        #region State initialization
        PatrolState = new BeetlePatrolState(this);
        AlertState = new BeetleAlertState(this);
        ChargeState = new BeetleChargeState(this);
        RecalibrateState = new BeetleRecalibrateState(this);
        DazedState = new BeetleDazedState(this);
        #endregion

        #region Strategy
        ObstacleAvoidance avoidance = new ObstacleAvoidance(transform, 1.5f, ObstacleLayers);
        wanderMovement = new WanderMovement(data.wanderSpeed, data.changeDirTime, transform, avoidance, data.rotationSpeed);
        chargeMovement = new ChargeMovement(GetComponent<Rigidbody>(), transform, data.chargeSpeed, data.chargeMaxDist, avoidance);
        lookAt = new LookAtTarget(data.rotationSpeed, transform);
        dazedFlip = new DazedFlip(transform, data.flipSpeed);
        #endregion

        _collision = new HeavyBeetleCollision(this);
    }

    private void OnEnable()
    {
        group?.Register(this);
    }

    private void OnDisable()
    {
        group?.Unregister(this);
    }

    private void Start()
    {
        playerTransform = GameManager.Instance.Pj.transform;
        detection = new BugDetection(transform, playerTransform, data.detectionRange);
        SetState(PatrolState);
        _lastPosition = transform.position;
    }
    #endregion

    private void Update()
    {
        _eventFSM.UpdateState();
        UpdateDetection();
        UpdateGroundCheck();
        UpdateCanvasPosition();
    }

    private void UpdateCanvasPosition()
    {
        if (_indicator == null) return;

        if (_mainCamera == null)
            _mainCamera = Camera.main;

        _indicator.transform.position = transform.position + Vector3.up * 1.76f;
        if (_mainCamera != null)
            _indicator.transform.rotation = _mainCamera.transform.rotation;
    }

    private void UpdateDetection()
    {
        bool inRange = detection.IsTargetInRange();

        if(inRange && !_playerInRange)
        {
            _playerInRange = true;
            group?.AlertAll(this, playerTransform);
            SendEvent(CreatureEvent.GekkoEnter);
        }

        else if(!inRange && _playerInRange)
        {
            _playerInRange = false;
            SendEvent(CreatureEvent.GekkoExit);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        _collision.ArtificialTriggerEnter(other);
    }

    public void SetState(IState state) => _eventFSM.SetState(state);
    public void SendEvent(CreatureEvent e) => _eventFSM.SendEvent(e);
    public void SetCharging(bool v) => IsCharging = v;
    public void SetDazed(bool v) => IsDazed = v;

    // Reemplazado por el flip por código (DazedFlip). Se deja comentado por si se
    // retoma la animación del Animator más adelante.
    // public void SetTurnedInsideOut(bool value)
    // {
    //     if (_anim == null) return;
    //     _anim.SetBool(TurnedInsideOutHash, value);
    // }

    public void SetAngry(bool value)
    {
        if(_angryParticle == null) return;

        if(value)
        {
            if(!_angryParticle.gameObject.activeSelf)
                _angryParticle.gameObject.SetActive(true);
            
            _angryParticle.Play();
        }
        
        else
        {
            _angryParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _angryParticle.gameObject.SetActive(false);
        }
    }

    // Cada frame que el escarabajo se movió, chequea que haya piso adelante (en
    // _detectGroundPosition). Si no hay, redirige la estrategia activa para no caer.
    private void UpdateGroundCheck()
    {
        if(_detectGroundPosition == null) return;

        // Solo chequear si la posición cambió desde el frame anterior.
        Vector3 pos = transform.position;
        
        if((pos - _lastPosition).sqrMagnitude <= GroundCheckEpsilon * GroundCheckEpsilon)
            return;
        
        _lastPosition = pos;

        // ¿Hay piso debajo del sensor?
        if(Physics.Raycast(_detectGroundPosition.position, Vector3.down,
                data.groundCheckDistance, _groundMask, QueryTriggerInteraction.Ignore))
            return;

        Vector3 safeDir = FindGroundedDirection();
        
        if(IsCharging) chargeMovement.Redirect(safeDir);
        
        else wanderMovement.Redirect(safeDir);
    }

    // Prueba un abanico de direcciones y devuelve la primera con piso; si ninguna, reversa.
    private Vector3 FindGroundedDirection()
    {
        Vector3 fwd = transform.forward; fwd.y = 0f;
        
        if(fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        
        fwd.Normalize();
        float ahead = 0.75f;
        float up = Mathf.Max(0.1f, _detectGroundPosition.position.y - transform.position.y + 0.3f);
        float[] yaws = { 90f, -90f, 135f, -135f, 180f };
        
        foreach(float yaw in yaws)
        {
            Vector3 d = Quaternion.Euler(0f, yaw, 0f) * fwd;
            Vector3 origin = transform.position + d * ahead + Vector3.up * up;
            
            if(Physics.Raycast(origin, Vector3.down, data.groundCheckDistance + up,
                    _groundMask, QueryTriggerInteraction.Ignore))
                return d;
        }
        
        return -fwd; // sin salida segura: reversa para no caminar al vacío
    }
    
    public void ReceiveGroupAlert(Transform player)
    {
        _playerInRange = true;
        playerTransform = player;
        SendEvent(CreatureEvent.GekkoEnter);
    }
    
    public void Damage(float dmg)
    {
        //Expandir a pool
        if(IsDazed) Destroy(gameObject);
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos() 
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.detectionRange);
    }
    #endif
}
