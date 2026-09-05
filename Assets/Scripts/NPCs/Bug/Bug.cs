using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Bug : MonoBehaviour , IDamageable
{
    #region Model
    private StateMachine _eventFSM;
    private BugCollision _collision;
    public FleeMovement fleeMovement;
    public WanderMovement wanderMovement;
    public LookAtTarget lookAt;
    //public DefensiveBehaviour defensiveBehaviour;
    public BugDetection detection;
    public DefensiveFlyingBehaviour defensiveFlyingBehaviour;
    #endregion

    #region Cached states
    public IdleState IdleState { get; private set; }
    public AlertState AlertState { get; private set; }
    public FleeState FleeState { get; private set; }
    public DefensiveState DefensiveState { get; private set; }
    public ObserveState ObserveState { get; private set; }
    public FlyingDefenseState FlyingDefenseState { get; private set; }
    #endregion

    public BugDataSO bugData;
    public Transform PlayerTransform;

    [Header("Flying Bug Settings")]
    [SerializeField] private bool _isFlyingBug = false;
    [SerializeField] private BugBullet _bulletPrefab;
    private bool _isAttacking = false;
    private bool _absorbed = false;
    public bool Absorbed { set { _absorbed = value; } }
    private bool _playerInRange = false;

    [Header("Ground Check")]
    [SerializeField] private Transform _detectGroundPosition;
    [SerializeField] private LayerMask _groundMask;
    private GroundCheck _groundCheck;

    #region Getters
    public bool IsAttacking => _isAttacking;
    public float AttackDamage => bugData.attackDamage;
    public bool IsFlyingBug => _isFlyingBug;
    #endregion

    private void Awake() 
    {
        _eventFSM = new StateMachine();

        #region State initialization
        IdleState = new IdleState(this);
        AlertState = new AlertState(this);
        FleeState = new FleeState(this);
        ObserveState = new ObserveState(this);
        //DefensiveState = new DefensiveState(this);
        FlyingDefenseState = new FlyingDefenseState(this);
        #endregion

        #region Strategy
        var avoidance = new ObstacleAvoidance(transform, 1.5f);
        fleeMovement = new FleeMovement(bugData.fleeSpeed, bugData.fleeRandomness, transform, avoidance, bugData.observeRotationSpeed);
        wanderMovement = new WanderMovement(bugData.wanderSpeed, bugData.changeDirTime, transform, avoidance, bugData.observeRotationSpeed);
        lookAt = new LookAtTarget(bugData.observeRotationSpeed, transform);
        //defensiveBehaviour = new DefensiveBehaviour(bugData.defenseThreshold, transform, bugData.defensiveSpeed, this, bugData.observeRotationSpeed);
        defensiveFlyingBehaviour = new DefensiveFlyingBehaviour(bugData.defenseThreshold, transform, bugData.defensiveSpeed, _bulletPrefab, this);
        #endregion

        _collision = new BugCollision(this);
        _groundCheck = new GroundCheck(transform, _detectGroundPosition, bugData.groundCheckDistance, _groundMask);
    }

    private void OnEnable() 
    {
        EventManager.Subscribe<(Bug, Transform)>("OnPlayerDetected", OnPlayerDetected);
        EventManager.Subscribe<Bug>("OnPlayerLost", OnPlayerLost);
    }

    private void OnDisable() 
    {
        EventManager.Unsubscribe<(Bug, Transform)>("OnPlayerDetected", OnPlayerDetected);
        EventManager.Unsubscribe<Bug>("OnPlayerLost", OnPlayerLost);
    }

    private void Start()
    {
        PlayerTransform = GameManager.Instance.Pj.transform;
        detection = new BugDetection(transform, PlayerTransform, bugData.detectionRange);
        SetState(IdleState);
    }

    private void Update()
    {
        _eventFSM.UpdateState();
        UpdateDetection();
        UpdateGroundCheck();
    }

    private void UpdateDetection()
    {
        bool inRange = detection.IsTargetInRange();

        if(inRange && !_playerInRange)
        {
            _playerInRange = true;
            EventManager.Trigger<(Bug, Transform)>("OnPlayerDetected", (this, PlayerTransform));
        }
        
        else if(!inRange && _playerInRange)
        {
            _playerInRange = false;
            EventManager.Trigger<Bug>("OnPlayerLost", this);
        }
    }

    public void SetState(IState state) {_eventFSM.SetState(state);}

    public void SendEvent(CreatureEvent evt, object data = null) {_eventFSM.SendEvent(evt, data);}

    private void OnPlayerDetected((Bug sender, Transform player) data)
    {
        if(data.sender != this) return;

        PlayerTransform = data.player;
        SendEvent(CreatureEvent.GekkoEnter);
    }

    private void OnPlayerLost(Bug sender)
    {
        if(sender != this) return;

        SendEvent(CreatureEvent.GekkoExit);
    }

    public void SetBugAttacking(bool value) {_isAttacking = value;}

    private void OnTriggerEnter(Collider other) {_collision.ArtificialTriggerEnter(other);}

    public void Damage(float dmg) {Destroy(gameObject);}

    // Cada frame que el bug se movió, chequea que haya piso adelante (vía GroundCheck).
    // Si no hay, redirige el movimiento activo (flee o wander) para no caminar en el aire.
    private void UpdateGroundCheck()
    {
        if(_groundCheck.TryGetSafeDirection(out Vector3 safeDir))
        {
            if(fleeMovement.IsActive) fleeMovement.Redirect(safeDir);

            else wanderMovement.Redirect(safeDir);
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos() 
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bugData.detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bugData.defenseThreshold);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(_detectGroundPosition.position, Vector3.down * bugData.groundCheckDistance);
    }
    #endif
}