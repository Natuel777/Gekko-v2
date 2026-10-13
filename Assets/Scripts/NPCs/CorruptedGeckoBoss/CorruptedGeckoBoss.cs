using System;
using UnityEngine;

// Jefe de la cueva (el amigo gecko corrompido). Pelea por fases: tanda de ataque -> ventana de
// purificación expuesta por tiempo limitado. Si la ventana se cierra sin los golpes necesarios, arranca
// otra tanda de la MISMA fase (no se pierde la fase, pero el conteo de golpes de esa ventana sí). Al
// limpiar la última fase, el jefe queda derrotado (estado terminal).
//
// Etapa 1: solo el esqueleto de fases/tandas/ventana. El ataque de la tanda es un placeholder
// (evento AttackBeat) hasta definir el ataque real. Los muérdagos, el gate de arándanos y las
// cinemáticas/Gekko amigo se enganchan después vía los eventos públicos de esta clase, sin tocarla de nuevo.
[RequireComponent(typeof(Collider))]
public class CorruptedGeckoBoss : MonoBehaviour, IDamageable, IHitOncePerLick, IParticleSystemTarget
{
    [Header("Data")]
    [SerializeField] private CorruptedGeckoBossDataSO _data;

    [Header("Detección")]
    [Tooltip("Si se deja vacío, se resuelve solo con GameManager.Instance.Pj en Start.")]
    public Transform playerTransform;

    [Header("Feedback (opcional)")]
    [SerializeField] private ParticleSystem _indicator;
    [SerializeField] private ParticleSystem _defeatedParticle;
    [SerializeField] private AudioSource _defeatedSound;

    private BugDetection _detection;
    private StateMachine _eventFSM;
    private bool _playerInRange;
    private bool _defeated;
    private int _hitsThisWindow;

    public CorruptedGeckoBossDataSO Data => _data;
    public CorruptedGeckoBossDataSO.PhaseConfig CurrentPhase => _data.phases[CurrentPhaseIndex];
    public int CurrentPhaseIndex { get; private set; }
    public bool IsExposed { get; private set; }
    public bool IsDefeated => _defeated;
    public int HitsThisWindow => _hitsThisWindow;
    public int HitsNeededThisWindow => Mathf.Max(1, CurrentPhase.hitsRequired - BlueberryHitReduction);

    // Lo fija el gate de arándanos (etapa 3) una sola vez, al cruzar hacia la arena.
    public int BlueberryHitReduction { get; set; }

    public bool CanBeTargeted => IsExposed && !_defeated;
    public ParticleSystem Indicator => _indicator;

    #region States
    public BossIdleState IdleState { get; private set; }
    public BossTandaState TandaState { get; private set; }
    public BossExposedState ExposedState { get; private set; }
    public BossDefeatedState DefeatedState { get; private set; }
    #endregion

    // (índice de fase que arranca)
    public event Action<int> PhaseStarted;
    // (índice de fase que se acaba de limpiar)
    public event Action<int> PhaseCleared;
    public event Action WindowFailed;
    // (golpes recibidos, golpes necesarios) en la ventana actual
    public event Action<int, int> Hit;
    public event Action Defeated;
    // Placeholder: la tanda de ataque todavía no tiene un ataque real definido.
    public event Action AttackBeat;
    // Arranca una tanda nueva (la primera de la fase o un reintento tras fallar la ventana). Lo usa el
    // spawner de muérdagos (etapa 2) para saber cuándo largar nuevos.
    public event Action TandaStarted;

    private void Awake()
    {
        _eventFSM = new StateMachine();

        IdleState = new BossIdleState(this);
        TandaState = new BossTandaState(this);
        ExposedState = new BossExposedState(this);
        DefeatedState = new BossDefeatedState(this);
    }

    private void Start()
    {
        if(playerTransform == null)
            playerTransform = GameManager.Instance.Pj.transform;

        _detection = new BugDetection(transform, playerTransform, _data.detectionRange);
        SetState(IdleState);
    }

    private void Update()
    {
        if(_defeated) return;

        _eventFSM.UpdateState();
        UpdateDetection();
    }

    private void UpdateDetection()
    {
        bool inRange = _detection.IsTargetInRange();

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

    public void RaiseAttackBeat() => AttackBeat?.Invoke();
    public void RaiseTandaStarted() => TandaStarted?.Invoke();

    public void SetExposed(bool exposed)
    {
        IsExposed = exposed;
        _hitsThisWindow = 0;
    }

    public void BeginPhase(int index)
    {
        CurrentPhaseIndex = index;
        _hitsThisWindow = 0;
        PhaseStarted?.Invoke(index);
    }

    public void ClearCurrentWindow()
    {
        PhaseCleared?.Invoke(CurrentPhaseIndex);

        if(CurrentPhaseIndex >= _data.phases.Length - 1)
        {
            SetState(DefeatedState);
            return;
        }

        BeginPhase(CurrentPhaseIndex + 1);
        SetState(TandaState);
    }

    public void FailCurrentWindow()
    {
        WindowFailed?.Invoke();
        SetState(TandaState);
    }

    public void MarkDefeated()
    {
        _defeated = true;

        if(_defeatedParticle != null) _defeatedParticle.Play();
        if(_defeatedSound != null) _defeatedSound.Play();
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.Purify);

        Defeated?.Invoke();
    }

    // Purificación con la lengua: TongueManager llama Damage() sobre cualquier IDamageable. Solo cuenta
    // mientras el jefe está expuesto (ventana de purificación abierta); fuera de la ventana el golpe no hace nada.
    public void Damage(float dmg)
    {
        if(_defeated || !IsExposed) return;

        _hitsThisWindow++;
        Hit?.Invoke(_hitsThisWindow, HitsNeededThisWindow);
    }
}
