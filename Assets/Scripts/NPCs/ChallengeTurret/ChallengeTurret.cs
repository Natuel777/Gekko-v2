using UnityEngine;

public class ChallengeTurret : MonoBehaviour
{
    [SerializeField] private ChallengeTurretSO _data;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private TurretBullet _bulletPrefab;

    private StateMachine _stateMachine;

    #region States
    public TurretActiveState activeState;
    public TurretPurifiedState purifiedState;
    #endregion

    #region Behabiours
    public TurretShootBehaviour shoot;
    #endregion

    #region Purification
    public bool IsPurified { get; private set; }

    // Para que un desafío (MirrorChallenge) cuente las torretas purificadas.
    public event System.Action<ChallengeTurret> Purified;
    #endregion

    #region Initialization
    private void Awake()
    {
        _stateMachine = new StateMachine();

        // Dsp agregar yRotation de ser necesario
        shoot = new TurretShootBehaviour(_data.yRotationSpeed, transform, _data.detectionRange, _muzzle, _data.fireRate,
                                        _bulletPrefab);

        activeState = new TurretActiveState(this);
        purifiedState = new TurretPurifiedState();
    }

    private void Start() 
    {
        // Si la purificaron antes de arrancar, nace ya purificada y no dispara.
        _stateMachine.SetState(IsPurified ? purifiedState : activeState);
    }
    #endregion

    private void Update() 
    {
        _stateMachine.UpdateState();
    }

    public void SetState(IState state) => _stateMachine.SetState(state);
    public void SendEvent(CreatureEvent e) => _stateMachine.SendEvent(e);

    // La purifica quien la alcance (más adelante, la bala que le devuelve el espejo). Pasa a TurretPurifiedState: al salir de
    // TurretActiveState deja de disparar. Una sola vez.
    public void Purify()
    {
        if(IsPurified) return;

        IsPurified = true;
        SendEvent(CreatureEvent.ChallengeTurretPurified);
        Purified?.Invoke(this);
    }

    // Para probarlo desde el Inspector (menú de tres puntos del componente) sin la bala devuelta.
    [ContextMenu("Purificar (Play Mode)")]
    private void TestPurify()
    {
        if(Application.isPlaying) Purify();
    }
}
