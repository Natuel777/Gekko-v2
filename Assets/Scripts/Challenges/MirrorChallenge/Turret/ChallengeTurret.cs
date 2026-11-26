using UnityEngine;

public class ChallengeTurret : MonoBehaviour, IShieldBreaker
{
    [SerializeField] private ChallengeTurretSO _data;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private TurretBullet _bulletPrefab;

    [Header("View")]
    [Tooltip("Animación de carga del cristal (EnergyBurstAnimation, en Crystal_lp): corre mientras la torreta está en TurretCharginState. Vacío = se busca en los hijos de la torreta.")]
    [SerializeField] private EnergyBurstAnimation _chargeAnimation;

    private StateMachine _stateMachine;

    #region States
    public TurretActiveState activeState;
    public TurretPurifiedState purifiedState;
    public TurretCharginState charginState;
    #endregion

    public ChallengeTurretView view;

    #region Behabiours
    public TurretShootBehaviour shoot;
    #endregion

    #region Purification
    public bool IsPurified { get; private set; }

    // So a challenge (MirrorChallenge) can count the purified turrets.
    public event System.Action<ChallengeTurret> Purified;

    // As a shield breaker, "broken" = purified.
    bool IShieldBreaker.IsBroken => IsPurified;

    event System.Action<IShieldBreaker> IShieldBreaker.ShieldBreakerBroken
    {
        add => _shieldBreakerBroken += value;
        remove => _shieldBreakerBroken -= value;
    }
    
    private System.Action<IShieldBreaker> _shieldBreakerBroken;
    #endregion

    // While a cinematic is running (the camera showing the plant when a turret is purified) it doesn't shoot, just like the plant:
    // otherwise it would shoot at a frozen Gekko with the camera elsewhere.
    private bool _frozenForCinematic;

    private void OnEnable()
    {
        EventManager.Subscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Subscribe("OnCinematicEnd", UnfreezeForCinematic);
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe("OnCinematicStart", FreezeForCinematic);
        EventManager.Unsubscribe("OnCinematicEnd", UnfreezeForCinematic);
        _frozenForCinematic = false;
    }

    private void FreezeForCinematic() => _frozenForCinematic = true;
    private void UnfreezeForCinematic() => _frozenForCinematic = false;

    #region Initialization
    private void Awake()
    {
        _stateMachine = new StateMachine();

        // Add yRotation later if needed
        shoot = new TurretShootBehaviour(_data.yRotationSpeed, transform, _data.detectionRange, _muzzle, _data.fireRate,
                                        _bulletPrefab);
        if(_chargeAnimation == null) _chargeAnimation = GetComponentInChildren<EnergyBurstAnimation>(true);
        view = new ChallengeTurretView(_chargeAnimation);

        activeState = new TurretActiveState(this);
        purifiedState = new TurretPurifiedState();
        charginState = new TurretCharginState(this);
    }

    private void Start() 
    {
        _stateMachine.SetState(IsPurified ? purifiedState : activeState);
    }
    #endregion

    private void Update()
    {
        if(_frozenForCinematic) return;

        _stateMachine.UpdateState();
    }

    public void SetState(IState state) => _stateMachine.SetState(state);
    public void SendEvent(CreatureEvent e) => _stateMachine.SendEvent(e);

    public void Purify()
    {
        if(IsPurified) return;

        IsPurified = true;

        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.TurretBullet);

        // Each step is protected: if one listener throws an exception, the rest (e.g. TurretPurifiedMaterialSwap, which
        // paints the turret) still run. Before, an exception in an earlier listener cut the invocation list and the turret
        // stayed purified but with the corrupt material.
        try { SendEvent(CreatureEvent.ChallengeTurretPurified); }
        catch(System.Exception e) { Debug.LogException(e, this); }

        InvokeSafely(Purified);
        InvokeSafely(_shieldBreakerBroken);
    }

    private void InvokeSafely<T>(System.Action<T> evt) where T : class
    {
        if(evt == null) return;

        foreach(System.Delegate listener in evt.GetInvocationList())
        {
            try { ((System.Action<T>)listener)(this as T); }
            catch(System.Exception e) { Debug.LogException(e, this); }
        }
    }

    [ContextMenu("Purify (Play Mode)")]
    private void TestPurify()
    {
        if(Application.isPlaying) Purify();
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos() 
    {
        Gizmos.DrawWireSphere(transform.position, _data.detectionRange);
    }
    #endif
}
