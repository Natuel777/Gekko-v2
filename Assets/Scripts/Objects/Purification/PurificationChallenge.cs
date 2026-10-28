using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

// Controlador de un desafío de purificación (uno por zona). Es pasivo: no tiene trigger de zona, reacciona
// a eventos. Flujo: Active -> (todos los núcleos rotos) ShieldDown -> (planta purificada) Completed.
//  - Núcleos rotos: cada uno disuelve un tramo más del escudo de la planta (con 3 núcleos, 3 fases). Al romper el último
//    el escudo termina de disolverse y la planta pasa a ser golpeable.
//  - Planta purificada: cae la barrera del camino, todos los escarabajos del desafío quedan purificados
//    de forma permanente y se corre la cinemática de cierre.
//  - Mientras la planta viva, los escarabajos DE ESTE desafío que se purifican se re-corrompen a los pocos
//    segundos. Los escarabajos que no están registrados acá no se ven afectados.
public class PurificationChallenge : MonoBehaviour
{
    public enum ChallengeState { Active, ShieldDown, Completed }

    // Propiedad del shader S_Enredaderas (la usa M_Enredaderas): 0 = entero, 1 = disuelto.
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

    [Header("Data")]
    [SerializeField] private PurificationChallengeDataSO _data;

    [Header("Plant & Cores")]
    [SerializeField] private CarnivorousPlant _plant;
    [SerializeField] private PurificationCore[] _cores;

    [Header("Barriers")]
    [Tooltip("Barrera alrededor de la planta: se disuelve un tramo por cada núcleo roto y cae del todo al romper el último.")]
    [SerializeField] private PurificationBarrier _shield;
    [Tooltip("Barrera que tapa el camino: cae al purificar la planta.")]
    [SerializeField] private PurificationBarrier _pathBarrier;

    [Header("Re-corrupting Enemies")]
    private Transform _enemiesRoot;
    [SerializeField] private HeavyBeetle[] _enemies;

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
    [SerializeField] private TextMeshProUGUI _textCount;
    [Tooltip("Barra de runas del desafío (opcional). Se le avisa cada vez que se rompe un núcleo; la barra pasa sola a 'purificada' al quedarse sin runas corruptas, así que hace falta una runa por núcleo.")]
    [SerializeField] private PurificationChallengeHealthbar _healthbar;

    [Header("Shield Down")]
    [Tooltip("Timeline que, al romper el último núcleo, encadena la cámara de 'núcleo roto' con la de la planta enojada (con movimiento propio en vez de un blend automático).")]
    [SerializeField] private PlayableDirector _shieldDownDirector;

    [Header("Finish")]
    [Tooltip("Timeline de cierre: cámara de la planta purificada, diálogo y fade a negro.")]
    [SerializeField] private PlayableDirector _plantPurifiedDirector;

    [Header("Skip")]
    [Tooltip("Mantener Interactuar (E) este tiempo salta el timeline que se esté reproduciendo.")]
    [Min(0.1f)]
    [SerializeField] private float _skipHoldSeconds = 3f;
    [SerializeField] private SkipCinematicPrompt _skipPrompt;

    private readonly List<HeavyBeetle> _allEnemies = new();
    private readonly HashSet<HeavyBeetle> _enemySet = new();
    private readonly List<Material> _dissolveMaterials = new();
    private RecorruptScheduler _scheduler;
    private Camera _mainCamera;
    private int _total, _remaining;
    private bool _beatActive, _inputLocked, _subscribed;
    private bool _suscribed;
    private CinematicSkipHandler _skipHandler;
    private Coroutine _completionBeatRoutine;
    public ChallengeState State { get; private set; } = ChallengeState.Active;

    // (núcleos restantes, núcleos totales)
    public event Action<int, int> CoreBroken;
    public event Action ShieldDropped;
    public event Action Completed;

    private void Awake()
    {
        BuildEnemyList();
        _skipHandler = new CinematicSkipHandler(_skipHoldSeconds, _skipPrompt);

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
        if (_suscribed) SuscribeToEvent();
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

        _suscribed = true;
        SuscribeToEvent();
    }

    private void Update()
    {
        if(State != ChallengeState.Completed) _scheduler.ArtificialUpdate(Time.deltaTime);

        PlayableDirector active = GetActiveDirector();
        _skipHandler.ArtificialUpdate(active, () => OnCinematicSkipped(active));
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
        if(_shieldDownDirector != null) _shieldDownDirector.Stop();
        EndBeat();
        if (LevelOneManager.Instance == null) return;
        LevelOneManager.Instance.OnChallengeStart -= OnChallengeStarted;
    }

    // Las instancias creadas por r.materials no las libera Unity solas hasta cambiar de escena.
    private void OnDestroy()
    {
        foreach(Material material in _dissolveMaterials)
            if(material != null) Destroy(material);
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
            if(enemy == null) continue;

            enemy.PurifiedChanged += OnEnemyPurifiedChanged;

            // Si el controlador se reactiva y algún escarabajo quedó purificado, retoma su cuenta regresiva.
            if(enemy.IsPurified && State != ChallengeState.Completed)
                _scheduler.Schedule(enemy);
        }
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
        if(State == ChallengeState.Completed || _plant == null || _plant.IsPurified) return;

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
            if(_shield != null) _shield.DissolveStep(_total - _remaining, _total);
        }
        else
        {
            DropShield();
            // Solo acá y no en DropShield: si arranca sin núcleos (Start) no hay nada que mostrar.
            // El timeline encadena la cámara de "núcleo roto" (BossPlantPhases) con la de la planta enojada,
            // con movimiento propio en vez de un blend automático del CinemachineBrain.
            if(_shieldDownDirector != null) _shieldDownDirector.Play();
        }
    }

    private void DropShield()
    {
        if(State != ChallengeState.Active) return;

        State = ChallengeState.ShieldDown;

        // Último tramo: el escudo termina de disolverse (con el mismo ritmo que los anteriores) y cae. Con _total en 0 (sin
        // núcleos) la barrera igual necesita al menos un tramo para disolverse.
        if(_shield != null)
        {
            int steps = Mathf.Max(1, _total);
            _shield.DissolveStep(steps, steps);
        }

        if(_plant != null) _plant.SetShielded(false);

        ShieldDropped?.Invoke();
    }

    private void OnPlantPurified()
    {
        if(State == ChallengeState.Completed) return;

        // Primero el estado, así cualquier re-corrupción pendiente se descarta.
        State = ChallengeState.Completed;
        _scheduler.CancelAll();
        if(_shieldDownDirector != null) _shieldDownDirector.Stop();

        // Un escarabajo que nunca se activó no tiene FSM inicializada (su Awake no corrió).
        foreach(HeavyBeetle enemy in _allEnemies)
            if(enemy != null && enemy.gameObject.activeInHierarchy) enemy.SetPurified(true);

        if(_pathBarrier != null) _pathBarrier.Drop();

        HideCorruptedVisuals();
        SetActiveAll(_purifiedVisuals, true);

        Completed?.Invoke();

        _completionBeatRoutine = StartCoroutine(CompletionBeat());
    }

    // El input solo se bloquea acá, cuando ya no quedan hostiles activos (al caer el escudo sí los hay).
    private IEnumerator CompletionBeat()
    {
        _beatActive = true;

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
            float seconds = _data != null ? _data.completionBeatSeconds : 3f;
            yield return new WaitForSeconds(seconds);
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
    }
    #endregion

    private void OnChallengeStarted()
    {
        ActivateCorruptedVisuals();

        if(_healthbar != null && State != ChallengeState.Completed)
        {
            _healthbar.gameObject.SetActive(true);
            StartCoroutine(_healthbar.FadeRoutine(true)); 
        } 
    }

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
