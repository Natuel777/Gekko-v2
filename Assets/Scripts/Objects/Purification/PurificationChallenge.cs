using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Controlador de un desafío de purificación (uno por zona). Hereda de Challenge el flujo común (escudo, estado y cierre) y
// agrega lo propio de los núcleos y los escarabajos. Flujo: Active -> (todos los núcleos rotos) ShieldDown -> (planta
// purificada) Completed.
//  - Núcleos rotos: cada uno disuelve un tramo más del escudo de la planta (con 3 núcleos, 3 fases). Al romper el último
//    el escudo termina de disolverse y la planta pasa a ser golpeable.
//  - Planta purificada: cae la barrera del camino, todos los escarabajos del desafío quedan purificados
//    de forma permanente y se corre la cinemática de cierre.
//  - Mientras la planta viva, los escarabajos DE ESTE desafío que se purifican se re-corrompen a los pocos
//    segundos. Los escarabajos que no están registrados acá no se ven afectados.
public class PurificationChallenge : Challenge
{
    [Header("Cores")]
    [SerializeField] private PurificationCore[] _cores;

    [Header("Re-corrupting Enemies")]
    private Transform _enemiesRoot;
    [SerializeField] private HeavyBeetle[] _enemies;

    [Header("HUD (cores)")]
    [SerializeField] private TextMeshProUGUI _textCount;
    [Tooltip("Barra de runas del desafío (opcional). Se le avisa cada vez que se rompe un núcleo; la barra pasa sola a 'purificada' al quedarse sin runas corruptas, así que hace falta una runa por núcleo.")]
    [SerializeField] private PurificationChallengeHealthbar _healthbar;

    private readonly List<HeavyBeetle> _allEnemies = new();
    private readonly HashSet<HeavyBeetle> _enemySet = new();
    private RecorruptScheduler _scheduler;
    private int _total, _remaining;

    public IReadOnlyList<PurificationCore> Cores => _cores;

    // (núcleos restantes, núcleos totales)
    public event Action<int, int> CoreBroken;

    protected override void Awake()
    {
        BuildEnemyList();
        base.Awake();

        // Las demoras de re-corrupción viven en PurificationChallengeDataSO; con otro SO (o sin SO) valen las de siempre.
        PurificationChallengeDataSO data = Data as PurificationChallengeDataSO;
        float min = data != null ? data.recorruptDelayMin : 3f;
        float max = data != null ? data.recorruptDelayMax : 5f;
        _scheduler = new RecorruptScheduler(min, max, OnRecorruptElapsed);

        foreach(PurificationCore core in _cores)
        {
            if(core == null) continue;

            _total++;

            if(!core.IsBroken) _remaining++;
        }
    }

    protected override void InitializeChallenge()
    {
        WarnSharedEnemies();

        UpdateText();

        // Sin núcleos (o ya rotos) el escudo no tendría cómo caer.
        if(_remaining == 0) DropShield(_total);
    }

    protected override void Update()
    {
        if(State != ChallengeState.Completed) _scheduler.ArtificialUpdate(Time.deltaTime);

        base.Update();
    }

    #region Subscriptions
    protected override void OnSubscribe()
    {
        foreach(PurificationCore core in _cores)
            if(core != null) core.Broken += OnCoreBroken;

        foreach(HeavyBeetle enemy in _allEnemies)
        {
            if(enemy == null) continue;

            enemy.PurifiedChanged += OnEnemyPurifiedChanged;

            // Si el controlador se reactiva y algún escarabajo quedó purificado, retoma su cuenta regresiva.
            if(enemy.IsPurified && State != ChallengeState.Completed)
                _scheduler.Schedule(enemy);
        }
    }

    protected override void OnUnsubscribe()
    {
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
        if(State == ChallengeState.Completed || Plant == null || Plant.IsPurified) return;

        beetle.SetPurified(false);
    }
    #endregion

    #region Flow
    private void OnCoreBroken(PurificationCore core)
    {
        if(State != ChallengeState.Active) return;

        _remaining = Mathf.Max(0, _remaining - 1);
        UpdateText();
        if(_healthbar != null) _healthbar.RemoveCorruptedRune();
        CoreBroken?.Invoke(_remaining, _total);

        if(_remaining > 0)
        {
            // Cada núcleo roto disuelve un tramo más del escudo: con 3 núcleos, el primero lo lleva a 1/3 y el segundo a
            // 2/3. El tramo final (el último núcleo) lo hace DropShield.
            DissolveShield(_total - _remaining, _total);
        }
        else
        {
            DropShield(_total);
            // Solo acá y no en DropShield: si arranca sin núcleos (Start) no hay nada que mostrar.
            PlayShieldDownTimeline();
        }
    }

    // La base ya dejó el estado en Completed: se descartan las re-corrupciones pendientes y los escarabajos del desafío
    // quedan purificados para siempre.
    protected override void OnCompleted()
    {
        _scheduler.CancelAll();

        // Un escarabajo que nunca se activó no tiene FSM inicializada (su Awake no corrió).
        foreach(HeavyBeetle enemy in _allEnemies)
            if(enemy != null && enemy.gameObject.activeInHierarchy) enemy.SetPurified(true);
    }
    #endregion

    protected override void OnChallengeStarted()
    {
        base.OnChallengeStarted();

        if(_healthbar != null && State != ChallengeState.Completed)
        {
            _healthbar.gameObject.SetActive(true);
            StartCoroutine(_healthbar.FadeRoutine(true));
        }
    }

    #region Helpers
    private void UpdateText()
    {
        if(_textCount != null)
            _textCount.text = $"{_remaining} / {_total}";
    }
    #endregion
}
