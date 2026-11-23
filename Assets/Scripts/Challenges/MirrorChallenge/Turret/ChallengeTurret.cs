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

    // Para que un desafío (MirrorChallenge) cuente las torretas purificadas.
    public event System.Action<ChallengeTurret> Purified;

    // Como destructor de escudo, "rota" = purificada.
    bool IShieldBreaker.IsBroken => IsPurified;
    event System.Action<IShieldBreaker> IShieldBreaker.ShieldBreakerBroken
    {
        add => _shieldBreakerBroken += value;
        remove => _shieldBreakerBroken -= value;
    }
    private System.Action<IShieldBreaker> _shieldBreakerBroken;
    #endregion

    // Mientras corre una cinemática (la cámara mostrando la planta al purificar una torreta) no dispara, igual que la planta:
    // si no, le tiraba a Gekko congelado con la cámara en otro lado.
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

        // Dsp agregar yRotation de ser necesario
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

        // Suena una sola vez, cuando la purifican (la guarda de arriba corta las demás llamadas). Antes de avisar a los demás para
        // que no dependa de lo que hagan los suscriptores.
        if(AudioManager.instance) AudioManager.instance.Play(SoundNames.TurretBullet);

        SendEvent(CreatureEvent.ChallengeTurretPurified);
        Purified?.Invoke(this);
        _shieldBreakerBroken?.Invoke(this);
    }

    [ContextMenu("Purificar (Play Mode)")]
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
