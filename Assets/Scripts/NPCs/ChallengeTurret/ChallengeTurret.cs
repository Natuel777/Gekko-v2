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
        _stateMachine.SetState(activeState);
    }
    #endregion

    private void Update() 
    {
        _stateMachine.UpdateState();
    }

    public void SetState(IState state) => _stateMachine.SetState(state);
    public void SendEvent(CreatureEvent e) => _stateMachine.SendEvent(e);
}
