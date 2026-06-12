using UnityEngine;

public class HeavyBeetle : MonoBehaviour, IDamageable
{
    public HeavyBeetledataSO data;
    public Transform playerTransform;
    public BeetleGroupSO group;
    private bool _playerInRange = false;

    #region FSM
    public WanderMovement wanderMovement;
    public ChargeMovement chargeMovement;
    public LookAtTarget lookAt;
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

        #region State initialization
        PatrolState = new BeetlePatrolState(this);
        AlertState = new BeetleAlertState(this);
        ChargeState = new BeetleChargeState(this);
        RecalibrateState = new BeetleRecalibrateState(this);
        DazedState = new BeetleDazedState(this);
        #endregion

        #region Strategy
        ObstacleAvoidance avoidance = new ObstacleAvoidance(transform, 1.5f);
        wanderMovement = new WanderMovement(data.wanderSpeed, data.changeDirTime, transform, avoidance, data.rotationSpeed);
        chargeMovement = new ChargeMovement(GetComponent<Rigidbody>(), transform, data.chargeSpeed, data.chargeMaxDist, avoidance);
        lookAt = new LookAtTarget(data.rotationSpeed, transform);
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
        detection = new BugDetection(this, playerTransform, data.detectionRange);
        SetState(PatrolState);
    }
    #endregion
    
    private void Update()
    {
        _eventFSM.UpdateState();
        UpdateDetection();
    }

    private void UpdateDetection()
    {
        bool inRange = detection.IsTargetInRange();

        if(inRange && !_playerInRange)
        {
            _playerInRange = true;
            group?.AlertAll(this, playerTransform);
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
    
    private void OnTriggerEnter(Collider other)
    {
        _collision.ArtificialTriggerEnter(other);
    }

    public void SetState(IState state) => _eventFSM.SetState(state);
    public void SendEvent(CreatureEvent e) => _eventFSM.SendEvent(e);
    public void SetCharging(bool v) => IsCharging = v;
    public void SetDazed(bool v) => IsDazed = v;
    private void DelayedEnter() => SendEvent(CreatureEvent.GekkoEnter);
    
    public void ReceiveGroupAlert(Transform player)
    {
        playerTransform = player;
        CancelInvoke(nameof(DelayedEnter));
        Invoke(nameof(DelayedEnter), data.reactionDelay);
    }
    
    public void Damage(float dmg)
    {
        //Expandir a pool
        if(IsDazed) Destroy(gameObject);
    }
}
