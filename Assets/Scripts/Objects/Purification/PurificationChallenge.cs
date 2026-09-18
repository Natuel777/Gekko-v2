using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

// Controlador de un desafío de purificación (uno por zona). Es pasivo: no tiene trigger de zona, reacciona
// a eventos. Flujo: Active -> (todos los núcleos rotos) ShieldDown -> (planta purificada) Completed.
//  - Núcleos rotos: cae el escudo de la planta y esta pasa a ser golpeable.
//  - Planta purificada: cae la barrera del camino, todos los escarabajos del desafío quedan purificados
//    de forma permanente y se corre la cinemática de cierre.
//  - Mientras la planta viva, los escarabajos DE ESTE desafío que se purifican se re-corrompen a los pocos
//    segundos. Los escarabajos que no están registrados acá no se ven afectados.
public class PurificationChallenge : MonoBehaviour
{
    public enum ChallengeState { Active, ShieldDown, Completed }

    [Header("Data")]
    [SerializeField] private PurificationChallengeDataSO _data;

    [Header("Planta y núcleos")]
    [SerializeField] private CarnivorousPlant _plant;
    [SerializeField] private PurificationCore[] _cores;

    [Header("Barreras")]
    [Tooltip("Barrera alrededor de la planta: cae al romper todos los núcleos.")]
    [SerializeField] private PurificationBarrier _shield;
    [Tooltip("Barrera que tapa el camino: cae al purificar la planta.")]
    [SerializeField] private PurificationBarrier _pathBarrier;

    [Header("Enemigos que se re-corrompen")]
    [Tooltip("Opcional: se toman todos los HeavyBeetle hijos de este Transform.")]
    [SerializeField] private Transform _enemiesRoot;
    [SerializeField] private HeavyBeetle[] _enemies;

    [Header("Arte")]
    [Tooltip("Se apagan al completar el desafío (zona corrompida). No pueden ser este objeto ni un ancestro.")]
    [SerializeField] private GameObject[] _corruptedVisuals;
    [Tooltip("Se prenden al completar el desafío (zona purificada).")]
    [SerializeField] private GameObject[] _purifiedVisuals;

    [Header("HUD")]
    [SerializeField] private Transform _canvas;
    [SerializeField] private TextMeshProUGUI _textCount;

    [Header("Cierre")]
    [SerializeField] private CinemachineCamera _camFinish;

    private readonly List<HeavyBeetle> _allEnemies = new();
    private readonly HashSet<HeavyBeetle> _enemySet = new();
    private RecorruptScheduler _scheduler;
    private Camera _mainCamera;
    private int _total;
    private int _remaining;
    private bool _beatActive;
    private bool _inputLocked;
    private bool _subscribed;

    public ChallengeState State { get; private set; } = ChallengeState.Active;

    // (núcleos restantes, núcleos totales)
    public event Action<int, int> CoreBroken;
    public event Action ShieldDropped;
    public event Action Completed;

    private void Awake()
    {
        BuildEnemyList();

        float min = _data != null ? _data.recorruptDelayMin : 3f;
        float max = _data != null ? _data.recorruptDelayMax : 5f;
        _scheduler = new RecorruptScheduler(min, max, OnRecorruptElapsed);

        foreach(PurificationCore core in _cores)
        {
            if(core == null) continue;

            _total++;
            if(!core.IsBroken) _remaining++;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if(_plant == null)
        {
            Debug.LogError($"[{name}] PurificationChallenge sin planta asignada.", this);
            enabled = false;
            return;
        }

        WarnSharedEnemies();

        if(_shield != null) _plant.SetShielded(true);

        UpdateText();

        // Sin núcleos (o ya rotos) el escudo no tendría cómo caer.
        if(_remaining == 0) DropShield();
    }

    private void Update()
    {
        if(State == ChallengeState.Completed) return;

        _scheduler.ArtificialUpdate(Time.deltaTime);
    }

    // El HUD world-space mira a la cámara.
    private void LateUpdate()
    {
        if(_canvas == null || !_canvas.gameObject.activeInHierarchy) return;

        if(_mainCamera == null) _mainCamera = Camera.main;
        if(_mainCamera != null) _canvas.rotation = _mainCamera.transform.rotation;
    }

    private void OnDisable()
    {
        Unsubscribe();
        EndBeat();
    }

    #region Subscriptions
    private void Subscribe()
    {
        if(_subscribed) return;
        _subscribed = true;

        if(_plant != null) _plant.Purified += OnPlantPurified;

        foreach(PurificationCore core in _cores)
            if(core != null) core.Broken += OnCoreBroken;

        foreach(HeavyBeetle enemy in _allEnemies)
        {
            enemy.PurifiedChanged += OnEnemyPurifiedChanged;

            // Si el controlador se reactiva y algún escarabajo quedó purificado, retoma su cuenta regresiva.
            if(enemy.IsPurified && State != ChallengeState.Completed)
                _scheduler.Schedule(enemy);
        }
    }

    private void Unsubscribe()
    {
        if(!_subscribed) return;
        _subscribed = false;

        if(_plant != null) _plant.Purified -= OnPlantPurified;

        foreach(PurificationCore core in _cores)
            if(core != null) core.Broken -= OnCoreBroken;

        foreach(HeavyBeetle enemy in _allEnemies)
            if(enemy != null) enemy.PurifiedChanged -= OnEnemyPurifiedChanged;
    }
    #endregion

    #region Enemies
    private void BuildEnemyList()
    {
        void Add(HeavyBeetle b)
        {
            if(b != null && _enemySet.Add(b))
                _allEnemies.Add(b);
        }

        if(_enemies != null)
            foreach(HeavyBeetle b in _enemies) Add(b);

        if(_enemiesRoot != null)
            foreach(HeavyBeetle b in _enemiesRoot.GetComponentsInChildren<HeavyBeetle>(true)) Add(b);
    }

    // Un escarabajo en dos desafíos programaría dos re-corrupciones y se pisarían entre sí.
    private void WarnSharedEnemies()
    {
        foreach(PurificationChallenge other in FindObjectsByType<PurificationChallenge>(FindObjectsSortMode.None))
        {
            if(other == this) continue;

            foreach(HeavyBeetle b in _allEnemies)
            {
                if(other._enemySet.Contains(b))
                    Debug.LogWarning($"[{name}] El escarabajo '{b.name}' también pertenece a '{other.name}'.", this);
            }
        }
    }

    private void OnEnemyPurifiedChanged(IPurifiable purifiable)
    {
        if(State == ChallengeState.Completed) return;

        HeavyBeetle beetle = purifiable as HeavyBeetle;
        if(beetle == null) return;

        if(purifiable.IsPurified) _scheduler.Schedule(beetle);
        else _scheduler.Cancel(beetle);
    }

    private void OnRecorruptElapsed(HeavyBeetle beetle)
    {
        // Carrera con el golpe final: si la planta ya murió (o el desafío cerró) no se re-corrompe nada.
        if(State == ChallengeState.Completed || _plant.IsPurified) return;

        beetle.SetPurified(false);
    }
    #endregion

    #region Flow
    private void OnCoreBroken(PurificationCore core)
    {
        if(State != ChallengeState.Active) return;

        _remaining = Mathf.Max(0, _remaining - 1);
        UpdateText();
        CoreBroken?.Invoke(_remaining, _total);

        if(_remaining == 0) DropShield();
    }

    private void DropShield()
    {
        if(State != ChallengeState.Active) return;

        State = ChallengeState.ShieldDown;

        if(_shield != null) _shield.Drop();
        _plant.SetShielded(false);

        ShieldDropped?.Invoke();
    }

    private void OnPlantPurified()
    {
        if(State == ChallengeState.Completed) return;

        // Primero el estado, así cualquier re-corrupción pendiente se descarta.
        State = ChallengeState.Completed;
        _scheduler.CancelAll();

        // Un escarabajo que nunca se activó no tiene FSM inicializada (su Awake no corrió).
        foreach(HeavyBeetle enemy in _allEnemies)
            if(enemy != null && enemy.gameObject.activeInHierarchy) enemy.SetPurified(true);

        if(_pathBarrier != null) _pathBarrier.Drop();

        SetActiveAll(_corruptedVisuals, false);
        SetActiveAll(_purifiedVisuals, true);

        Completed?.Invoke();

        StartCoroutine(CompletionBeat());
    }

    // El input solo se bloquea acá, cuando ya no quedan hostiles activos (al caer el escudo sí los hay).
    private IEnumerator CompletionBeat()
    {
        _beatActive = true;

        if(_camFinish != null) _camFinish.Priority = 30;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;

        if(pj != null)
        {
            pj.Inputs(false);
            _inputLocked = true;
        }

        float seconds = _data != null ? _data.completionBeatSeconds : 3f;
        yield return new WaitForSeconds(seconds);

        EndBeat();
    }

    // Idempotente: también se llama desde OnDisable, porque si Gekko muere (o se recarga la escena) a mitad
    // de la cinemática la corrutina se corta y el input/cámara quedarían bloqueados.
    private void EndBeat()
    {
        if(!_beatActive) return;
        _beatActive = false;

        if(_camFinish != null) _camFinish.Priority = 0;

        if(_inputLocked)
        {
            _inputLocked = false;

            Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
            if(pj != null) pj.Inputs(true);
        }

        if(_canvas != null) _canvas.gameObject.SetActive(false);
    }
    #endregion

    #region Helpers
    private void UpdateText()
    {
        if(_textCount != null)
            _textCount.text = $"{_remaining} / {_total}";
    }

    private void SetActiveAll(GameObject[] objects, bool active)
    {
        if(objects == null) return;

        foreach(GameObject go in objects)
        {
            if(go == null) continue;

            // Apagar este objeto o un ancestro cortaría el controlador (y la cinemática de cierre).
            if(!active && transform.IsChildOf(go.transform))
            {
                Debug.LogWarning($"[{name}] '{go.name}' es este objeto o un ancestro; no se puede apagar.", this);
                continue;
            }

            go.SetActive(active);
        }
    }
    #endregion
}
