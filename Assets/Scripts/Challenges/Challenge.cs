using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// Base de los desafíos de la planta (PurificationChallenge, MirrorChallenge). Es pasiva: no tiene trigger de zona, reacciona
// a eventos. Reúne lo que todos comparten:
//  - Flujo: Active -> (cae el escudo) ShieldDown -> (planta purificada) Completed.
//  - Escudo de la planta (PurificationBarrier y/o VineShield): cada desafío decide cuándo baja, con DissolveShield (por
//    tramos) y DropShield (el último tramo, que además deja la planta golpeable).
//  - Cierre: al purificar la planta cae la barrera del camino, se apagan los visuales corrompidos, se prenden los
//    purificados y se corre la cinemática de cierre (con el input bloqueado, salteable manteniendo Interactuar).
// Lo que cambia entre desafíos (qué hay que lograr para que caiga el escudo) lo implementan las hijas con
// InitializeChallenge, OnSubscribe / OnUnsubscribe y OnCompleted. Los mensajes de Unity son virtuales: la hija que los
// sobreescribe tiene que llamar a base.
public abstract class Challenge : MonoBehaviour
{
    public enum ChallengeState { Active, ShieldDown, Completed }

    // Propiedad del shader S_Enredaderas (la usa M_Enredaderas): 0 = entero, 1 = disuelto.
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    [Header("Data")]
    [SerializeField] private ChallengeDataSO _data;

    [Header("Plant")]
    [SerializeField] private CarnivorousPlant _plant;

    [Header("Barriers")]
    [Tooltip("Barrera alrededor de la planta: se disuelve por tramos según el desafío (PurificationChallenge: un tramo por núcleo roto) y cae del todo cuando se cumple su condición.")]
    [SerializeField] private PurificationBarrier _shield;
    [Tooltip("Escudo de enredaderas (Escudo_Enredadera): se retrae por tramos igual que _shield. Se puede usar en lugar de _shield o junto con él.")]
    [SerializeField] private VineShield _vineShield;
    [Tooltip("Barrera que tapa el camino: cae al purificar la planta.")]
    [SerializeField] private PurificationBarrier _pathBarrier;

    [Header("Art")]
    [Tooltip("Se apagan al completar el desafío (zona corrompida). No pueden ser este objeto ni un ancestro. Los renderers con _Dissolve (M_Enredaderas) de ellos y de sus hijos se disuelven antes de apagarse.")]
    [SerializeField] private GameObject[] _corruptedVisuals;
    [Tooltip("Se prenden al completar el desafío (zona purificada).")]
    [SerializeField] private GameObject[] _purifiedVisuals;
    [Tooltip("Velocidad del disolve de los visuales corrompidos en unidades de _Dissolve por segundo: 0.5 = tarda 2 s en ir de 0 a 1.")]
    [Min(0.01f)]
    [SerializeField] private float _dissolveSpeed = 0.5f;

    [Header("HUD")]
    [SerializeField] private Transform _canvas;

    [Header("Shield Down")]
    [Tooltip("Timeline que se reproduce al caer el escudo, si el desafío lo pide con PlayShieldDownTimeline (PurificationChallenge: al romper el último núcleo; MirrorChallenge: al purificar la última torreta).")]
    [SerializeField] private PlayableDirector _shieldDownDirector;

    [Header("Finish")]
    [Tooltip("Timeline de cierre: cámara de la planta purificada, diálogo y fade a negro.")]
    [SerializeField] private PlayableDirector _plantPurifiedDirector;

    [Header("Skip")]
    [Tooltip("Mantener Interactuar (E) este tiempo salta el timeline que se esté reproduciendo.")]
    [Min(0.1f)]
    [SerializeField] private float _skipHoldSeconds = 3f;
    [SerializeField] private SkipCinematicPrompt _skipPrompt;

    private readonly List<Material> _dissolveMaterials = new();
    private Camera _mainCamera;
    private bool _beatActive, _inputLocked, _subscribed, _completionBeatStarted;
    private bool _suscribed;
    private CinematicSkipHandler _skipHandler;
    private Coroutine _completionBeatRoutine;

    public ChallengeState State { get; protected set; } = ChallengeState.Active;

    public event Action ShieldDropped;
    public event Action Completed;

    // Terminó la cinemática de cierre (la de la planta purificada): ya se devolvió el input. Lo usa lo que quiera mostrarse
    // recién después, como las enredaderas del árbol de LvlTwo (VinesRevealCinematic).
    public event Action CompletionBeatFinished;

    // Planta purificada y su cinemática de cierre ya terminada.
    public bool CompletionBeatDone => State == ChallengeState.Completed && !_beatActive && _completionBeatStarted;

    // Se rompió un destructor de escudo (núcleo, torreta...): (el destructor, restantes, total). Llega ANTES de que el escudo
    // baje su tramo, así quien muestre la cinemática (ShieldBreakCinematic) puede retener el VineShield hasta encuadrarlo.
    public event Action<IShieldBreaker, int, int> BreakerBroken;

    protected ChallengeDataSO Data => _data;
    public CarnivorousPlant Plant => _plant;
    public VineShield VineShield => _vineShield;

    // Duración de la cinemática de cierre cuando no hay timeline.
    private float CompletionBeatSeconds => _data != null ? _data.completionBeatSeconds : 3f;

    #region Unity messages
    protected virtual void Awake()
    {
        _skipHandler = new CinematicSkipHandler(_skipHoldSeconds, _skipPrompt);
    }

    protected virtual void OnEnable()
    {
        Subscribe();
        if (_suscribed) SuscribeToEvent();
    }

    protected virtual void Start()
    {
        if(_plant == null)
        {
            Debug.LogError($"[{name}] {GetType().Name} sin planta asignada.", this);
            enabled = false;
            return;
        }

        if(_shield != null || _vineShield != null) _plant.SetShielded(true);

        InitializeChallenge();

        _suscribed = true;
        SuscribeToEvent();
    }

    protected virtual void Update()
    {
        PlayableDirector active = GetActiveDirector();
        _skipHandler.ArtificialUpdate(active, () => OnCinematicSkipped(active));
    }

    // El HUD world-space mira a la cámara.
    protected virtual void LateUpdate()
    {
        if(_canvas == null || !_canvas.gameObject.activeInHierarchy) return;

        if(_mainCamera == null) _mainCamera = Camera.main;
        if(_mainCamera != null) _canvas.rotation = _mainCamera.transform.rotation;
    }

    protected virtual void OnDisable()
    {
        Unsubscribe();
        if(_shieldDownDirector != null) _shieldDownDirector.Stop();
        EndBeat();
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnChallengeStart -= OnChallengeStarted;
    }

    // Las instancias creadas por r.materials no las libera Unity solas hasta cambiar de escena.
    protected virtual void OnDestroy()
    {
        foreach(Material material in _dissolveMaterials)
            if(material != null) Destroy(material);
    }
    #endregion

    #region Hooks
    // Corre en Start, con la planta ya validada y blindada y antes de suscribirse al inicio del desafío. Acá cada desafío
    // arma su condición y resuelve el caso de arrancar sin nada que cumplir (con DropShield).
    protected abstract void InitializeChallenge();

    // Suscripciones propias de cada desafío (la planta ya la maneja la base). Se llaman desde OnEnable / OnDisable.
    protected virtual void OnSubscribe() { }
    protected virtual void OnUnsubscribe() { }

    // La planta acaba de purificarse y el estado ya es Completed. Corre antes de cortar el timeline del escudo, la barrera del
    // camino y el cierre: es el lugar para limpiar lo propio del desafío.
    protected virtual void OnCompleted() { }

    // El nivel avisó que arrancó el desafío (ChallengeTrigger -> LevelOneManager).
    protected virtual void OnChallengeStarted()
    {
        ActivateCorruptedVisuals();
    }
    #endregion

    #region Subscriptions
    private void Subscribe()
    {
        if(_subscribed) return;
        _subscribed = true;

        if(_plant != null) _plant.Purified += OnPlantPurified;

        OnSubscribe();
    }

    private void SuscribeToEvent()
    {
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnChallengeStart -= OnChallengeStarted;
        LevelOneManager.Instance.OnChallengeStart += OnChallengeStarted;
    }

    private void Unsubscribe()
    {
        if(!_subscribed) return;
        _subscribed = false;

        if(_plant != null) _plant.Purified -= OnPlantPurified;

        OnUnsubscribe();
    }
    #endregion

    #region Shield
    // Disuelve el escudo hasta la fase step de steps (0 = entero, steps = disuelto del todo). Las dos barreras llevan el mismo
    // avance, y las dos son opcionales.
    protected void DissolveShield(int step, int steps)
    {
        if(_shield != null) _shield.DissolveStep(step, steps);
        if(_vineShield != null) _vineShield.DissolveStep(step, steps);
    }

    // Último tramo: el escudo termina de disolverse (con el mismo ritmo que los anteriores, steps es cuántos tramos tenía
    // el desafío) y cae, y la planta pasa a ser golpeable. Siempre hace falta al menos un tramo para disolverse.
    protected void DropShield(int steps)
    {
        if(State != ChallengeState.Active) return;

        State = ChallengeState.ShieldDown;

        steps = Mathf.Max(1, steps);
        DissolveShield(steps, steps);

        if(_plant != null) _plant.SetShielded(false);

        ShieldDropped?.Invoke();
    }

    // Aparte de DropShield: no todos los desafíos lo usan, y si arrancan sin nada que cumplir no hay nada que mostrar.
    protected void PlayShieldDownTimeline()
    {
        if(_shieldDownDirector != null) _shieldDownDirector.Play();
    }

    // Las hijas lo llaman al romperse un destructor, antes de DissolveShield / DropShield.
    protected void NotifyBreakerBroken(IShieldBreaker breaker, int remaining, int total)
    {
        BreakerBroken?.Invoke(breaker, remaining, total);
    }
    #endregion

    #region Flow
    private void OnPlantPurified()
    {
        if(State == ChallengeState.Completed) return;

        // Primero el estado, así cualquier re-corrupción pendiente se descarta.
        State = ChallengeState.Completed;
        OnCompleted();
        if(_shieldDownDirector != null) _shieldDownDirector.Stop();

        if(_pathBarrier != null) _pathBarrier.Drop();

        HideCorruptedVisuals();
        SetActiveAll(_purifiedVisuals, true);

        Completed?.Invoke();

        _completionBeatRoutine = StartCoroutine(CompletionBeat());
    }

    private PlayableDirector GetActiveDirector()
    {
        if(_shieldDownDirector != null && _shieldDownDirector.state == PlayState.Playing) return _shieldDownDirector;
        if(_plantPurifiedDirector != null && _plantPurifiedDirector.state == PlayState.Playing) return _plantPurifiedDirector;
        return null;
    }

    // El director ya esta detenido (lo para CinematicSkipHandler); acá solo falta la limpieza que haria
    // el flujo normal (cerrar dialogo, cortar la corrutina de espera y restaurar input/HUD).
    private void OnCinematicSkipped(PlayableDirector director)
    {
        if(director != _plantPurifiedDirector) return;

        if(UIManager.Instance != null && UIManager.Instance.HasActiveDialogue()) UIManager.Instance.EndDialogue();

        if(_completionBeatRoutine != null)
        {
            StopCoroutine(_completionBeatRoutine);
            _completionBeatRoutine = null;
        }

        EndBeat();
    }

    // El input solo se bloquea acá, cuando ya no quedan hostiles activos (al caer el escudo sí los hay).
    private IEnumerator CompletionBeat()
    {
        _beatActive = true;
        _completionBeatStarted = true;

        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;

        if(pj != null)
        {
            pj.Inputs(false);
            _inputLocked = true;
        }

        if(_plantPurifiedDirector != null)
        {
            _plantPurifiedDirector.Play();
            yield return new WaitForSeconds((float)_plantPurifiedDirector.duration);
        }
        else
        {
            yield return new WaitForSeconds(CompletionBeatSeconds);
        }

        EndBeat();
    }

    // Idempotente: también se llama desde OnDisable, porque si Gekko muere (o se recarga la escena) a mitad
    // de la cinemática la corrutina se corta y el input/cámara quedarían bloqueados.
    private void EndBeat()
    {
        if(!_beatActive) return;
        _beatActive = false;
        _completionBeatRoutine = null;

        if(_plantPurifiedDirector != null) _plantPurifiedDirector.Stop();

        if(_inputLocked)
        {
            _inputLocked = false;

            Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
            if(pj != null) pj.Inputs(true);
        }

        if(_canvas != null) _canvas.gameObject.SetActive(false);

        CompletionBeatFinished?.Invoke();
    }
    #endregion

    #region Dissolve
    private void ActivateCorruptedVisuals()
    {
        foreach (var item in _corruptedVisuals)
        {
            item.SetActive(true);
        }
    }

    // Los renderers con _Dissolve bajo los visuales corrompidos (M_Enredaderas) se disuelven de 0 a 1 y recién después
    // se apaga todo; si ninguno tiene la propiedad, se apagan al instante como antes.
    private void HideCorruptedVisuals()
    {
        List<Material> materials = GetDissolveMaterials(_corruptedVisuals);

        if(materials.Count > 0) StartCoroutine(DissolveRoutine(materials));
        else SetActiveAll(_corruptedVisuals, false);
    }

    // Sube _Dissolve de 0 a 1 a _dissolveSpeed por segundo (0.5 = 2 s). Corre una sola vez, al purificarse la planta.
    private IEnumerator DissolveRoutine(List<Material> materials)
    {
        float dissolve = 0f;
        float speed = Mathf.Max(_dissolveSpeed, 0.01f);

        SetDissolve(materials, dissolve);

        while(dissolve < 1f)
        {
            dissolve = Mathf.MoveTowards(dissolve, 1f, speed * Time.deltaTime);
            SetDissolve(materials, dissolve);
            yield return null;
        }

        SetActiveAll(_corruptedVisuals, false);
    }

    // r.materials da una instancia propia de cada renderer: M_Enredaderas es un asset compartido y escribir en él
    // disolvería todas las enredaderas del nivel. No se usa MaterialPropertyBlock porque el GPU Resident Drawer
    // (activo en PC_RPAsset) no es compatible con property blocks.
    private List<Material> GetDissolveMaterials(GameObject[] roots)
    {
        List<Material> materials = new();
        HashSet<Renderer> visited = new();

        if(roots == null) return materials;

        foreach(GameObject root in roots)
        {
            // Mismo límite que SetActiveAll: este objeto o un ancestro no se apaga, así que tampoco se disuelve.
            if(root == null || transform.IsChildOf(root.transform)) continue;

            foreach(Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if(!visited.Add(r) || !HasDissolve(r)) continue;

                foreach(Material material in r.materials)
                    if(material != null && material.HasProperty(DissolveId)) materials.Add(material);
            }
        }

        _dissolveMaterials.AddRange(materials);

        return materials;
    }

    private static bool HasDissolve(Renderer r)
    {
        foreach(Material material in r.sharedMaterials)
            if(material != null && material.HasProperty(DissolveId)) return true;

        return false;
    }

    private static void SetDissolve(List<Material> materials, float value)
    {
        foreach(Material material in materials)
            if(material != null) material.SetFloat(DissolveId, value);
    }
    #endregion

    #region Helpers
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
