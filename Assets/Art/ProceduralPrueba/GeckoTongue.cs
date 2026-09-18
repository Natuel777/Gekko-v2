using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// La lengua del gecko, TODO por código sobre los huesos <c>Gecko_Tongue1..7</c>
/// (deforman el skinned mesh <c>lengua_lp</c>, así que se estira sola).
///
/// Un solo botón — CLICK IZQUIERDO / E / gatillo derecho — dispara la lengua al objetivo
/// interactuable MÁS CERCANO dentro del alcance (<see cref="TargetingMode.AutoNearest"/>,
/// por defecto). Qué pasa al llegar depende del objetivo:
///
///  • Collect  (IGeckoEdible, Collectible del juego, capa de comida) -> lo arrastra a la boca y se lo come.
///  • Grapple  (GeckoGrapplePoint)                                   -> soga con física de péndulo.
///  • Damage   (IDamageable: enemigos corruptos, núcleos de purificación) -> la punta golpea.
///  • Reach    (GeckoTongueTarget, IGeckoTongueReachable o un tag)   -> llega, avisa y vuelve (puzzles).
///
/// Si NO hay ningún objetivo válido en el alcance la lengua no sale. Un objetivo es válido si
/// tiene un componente interactuable (o un tag configurado), está dentro del cono de
/// detección y no hay geometría de <see cref="_obstacleMask"/> en el camino.
///
/// <see cref="TargetingMode.MouseAim"/> conserva el apuntado viejo con mouse/centro de pantalla
/// (también engancha superficies de <see cref="_grappleMask"/> para el parkour de soga).
///
/// Corre después de GeckoSecondaryMotion / GeckoIdlePose para posar la lengua sobre un cuello
/// ya animado. Comparte el Rigidbody con GeckoMover: mientras cuelga, GeckoMover se hace a un
/// lado (<see cref="GeckoMover.SetTethered"/>).
///
/// Extensión: para un tipo nuevo de interacción alcanza con un componente que implemente
/// <see cref="IGeckoTongueTarget"/> (+ <see cref="IGeckoTongueReachable"/> si quiere reaccionar
/// al toque), o con <see cref="GeckoTongueTarget"/> sin escribir código.
/// </summary>
[DefaultExecutionOrder(310)]
[RequireComponent(typeof(GeckoMover))]
public class GeckoTongue : MonoBehaviour
{
    public enum State { Idle, Reaching, Eating, Attached, Retracting }

    public enum TargetingMode
    {
        /// <summary> Elige solo el interactuable más cercano en alcance (sin mouse). </summary>
        AutoNearest,
        /// <summary> Apunta con el mouse (o el centro de pantalla si el cursor está bloqueado). </summary>
        MouseAim,
    }

    /// <summary> A qué le está apuntando la lengua AHORA MISMO, sin disparar — para la retícula. </summary>
    public enum AimTargetKind { None, Edible, GrapplePoint, InvalidSurface, Interactable }

    [Serializable] public class GameObjectEvent : UnityEvent<GameObject> { }

    #region Inspector
    [Header("Huesos (se autocompletan: Gecko_Tongue1..7)")]
    [SerializeField] private Transform[] _bones;
    [Tooltip("Punto de la boca de donde sale la lengua. Vacío = se deriva de la base de " +
             "la cadena (Gecko_Tongue1) siguiendo la cabeza.")]
    [SerializeField] private Transform _mouth;

    [Header("Refs")]
    [SerializeField] private GeckoMover _mover;
    [Tooltip("Cámara para apuntar con el mouse (solo modo MouseAim). Vacío = Camera.main.")]
    [SerializeField] private Camera _camera;

    [Header("Detección de objetivos")]
    [Tooltip("AutoNearest: elige sola el interactuable más cercano. MouseAim: apuntás vos.")]
    [SerializeField] private TargetingMode _targetingMode = TargetingMode.AutoNearest;
    [Tooltip("Alcance máximo de la lengua desde la boca (metros).")]
    [SerializeField] private float _maxDistance = 3.2f;
    [Tooltip("Capas que se escanean en busca de objetivos (modo AutoNearest). Recomendado: " +
             "'TongueTarget' + las capas de coleccionables. Cuanto más angosta, más barato el escaneo.")]
    [SerializeField] private LayerMask _interactableMask = ~0;
    [Tooltip("Capas que cuentan como comida aunque el objeto no traiga IGeckoEdible (se destruye al comerlo).")]
    [SerializeField] private LayerMask _edibleMask = 0;
    [Tooltip("Tags que convierten a un collider en objetivo de tipo Reach sin necesitar componentes.")]
    [SerializeField] private string[] _interactableTags;
    [Tooltip("Si está activo, cualquier IDamageable en alcance es objetivo (enemigos, núcleos de purificación).")]
    [SerializeField] private bool _acceptDamageables = true;
    [Tooltip("Ángulo TOTAL del cono de detección alrededor del frente del gecko. 360 = esfera completa.")]
    [Range(20f, 360f)]
    [SerializeField] private float _detectionAngle = 220f;
    [Tooltip("Capas que la lengua no puede atravesar. Si algo de estas capas queda entre la boca y " +
             "el objetivo, ese objetivo no es válido.")]
    [SerializeField] private LayerMask _obstacleMask = (1 << 0) | (1 << 6) | (1 << 7) | (1 << 8) | (1 << 12) | (1 << 13) | (1 << 14);
    [Tooltip("Cada punto de prioridad (IGeckoTongueTarget.Priority) descuenta esta distancia (m) al elegir.")]
    [SerializeField] private float _priorityWeight = 0.5f;
    [Tooltip("Ventaja (m) del objetivo actual sobre otro casi igual de cerca, para que la selección no parpadee.")]
    [SerializeField] private float _targetStickiness = 0.15f;
    [Tooltip("Cada cuánto se reescanea mientras la lengua está libre (segundos). Al disparar siempre se reescanea.")]
    [SerializeField] private float _scanInterval = 0.05f;

    [Header("Apuntado con mouse (solo MouseAim)")]
    [Tooltip("Capas contra las que apunta el rayo (insectos + superficies).")]
    [SerializeField] private LayerMask _aimMask = ~0;
    [Tooltip("Capas a las que la soga se puede enganchar aunque no tengan GeckoGrapplePoint.")]
    [SerializeField] private LayerMask _grappleMask = (1 << 6) | (1 << 7) | (1 << 8);
    [Tooltip("Si está, SOLO se engancha a objetos con GeckoGrapplePoint.")]
    [SerializeField] private bool _requireGrapplePoint = false;

    [Header("Lengua — extensión y retracción")]
    [Tooltip("Velocidad media de salida de la punta hacia el objetivo (m/s). La duración = distancia / velocidad.")]
    [SerializeField] private float _shootSpeed = 22f;
    [Tooltip("Velocidad media de retracción de la lengua (m/s).")]
    [SerializeField] private float _retractSpeed = 16f;
    [Tooltip("Duración mínima de la extensión, para que un objetivo pegado igual se vea animado (s).")]
    [SerializeField] private float _minExtendTime = 0.07f;
    [Tooltip("Duración mínima de la retracción (s).")]
    [SerializeField] private float _minRetractTime = 0.06f;
    [Tooltip("Progreso de la punta (0 = boca, 1 = objetivo) a lo largo del tiempo de extensión.")]
    [SerializeField] private AnimationCurve _extendCurve =
        new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0.15f, 0f));
    [Tooltip("Progreso de la retracción (0 = afuera, 1 = en la boca) a lo largo del tiempo.")]
    [SerializeField] private AnimationCurve _retractCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Velocidad con la que el objeto es arrastrado a la boca (m/s).")]
    [SerializeField] private float _pullSpeed = 9f;
    [Tooltip("Cooldown entre usos de la lengua (segundos).")]
    [SerializeField] private float _cooldown = 0.12f;

    [Header("Soga — péndulo (estilo Big Hops)")]
    [Tooltip("Largo mínimo al que se puede acortar la soga.")]
    [SerializeField] private float _minRopeLength = 0.5f;
    [Tooltip("Aceleración lateral que da el stick para bombear el balanceo (m/s²).")]
    [SerializeField] private float _swingAccel = 14f;
    [Tooltip("Rozamiento del balanceo (fracción de velocidad por segundo). Bajo = swing largo.")]
    [SerializeField] private float _swingDamping = 0.35f;
    [Tooltip("Tope de velocidad mientras cuelga (m/s), red de seguridad.")]
    [SerializeField] private float _maxSwingSpeed = 16f;
    [Tooltip("Velocidad a la que se acorta la soga mientras mantenés el botón de tirar " +
             "(botón derecho del mouse / LeftShift / gatillo izquierdo).")]
    [SerializeField] private float _reelSpeed = 3.5f;
    [Tooltip("Qué tan rápido el cuerpo se orienta hacia la dirección del swing.")]
    [SerializeField] private float _faceSpeed = 10f;
    [Tooltip("Ventana tras enganchar en la que un click rápido NO te suelta (segundos). " +
             "Deja que un click normal (no sostenido) igual quede enganchado un toque.")]
    [SerializeField] private float _attachGrace = 0.12f;

    [Header("Soga — lanzamiento al saltar")]
    [Tooltip("Empujón hacia arriba al soltarse con ESPACIO (m/s).")]
    [SerializeField] private float _launchUp = 3.5f;
    [Tooltip("Empujón extra en la dirección en la que venías moviéndote (m/s).")]
    [SerializeField] private float _launchForward = 2.5f;

    [Header("Visual")]
    [Tooltip("Panza de la lengua cuando está floja (metros). Se aplana casi del todo al colgar.")]
    [SerializeField] private float _slack = 0.06f;
    [Tooltip("Auto-calcular la corrección de orientación de los huesos desde la pose de bind.")]
    [SerializeField] private bool _autoBoneAlign = true;
    [Tooltip("Corrección manual de orientación de los huesos (si el mesh queda retorcido).")]
    [SerializeField] private Vector3 _boneAlignEuler;
    [Tooltip("Qué tan rápido la lengua vuelve a su pose de reposo al terminar.")]
    [SerializeField] private float _restReturnSpeed = 14f;

    [Header("Depuración")]
    [Tooltip("Dibuja en la Scene view: alcance, cono, cada candidato (verde = válido, gris = fuera del " +
             "cono, rojo = tapado por geometría) y el objetivo elegido en amarillo.")]
    [SerializeField] private bool _debugGizmos = true;
    [Tooltip("Muestra en pantalla el estado de la lengua y el objetivo detectado (útil en el Game view).")]
    [SerializeField] private bool _showDebugHud = false;
    [Tooltip("Loguea en consola cuando cambia el objetivo, se dispara o no hay objetivo.")]
    [SerializeField] private bool _debugLogs = false;
    [SerializeField] private bool _useDebugTrigger;
    [SerializeField] private bool _debugFire;

    [Header("Eventos")]
    public GameObjectEvent OnAte;
    public UnityEvent OnGrappleAttached;
    public UnityEvent OnGrappleReleased;
    [Tooltip("La lengua eligió un objetivo nuevo (antes de disparar).")]
    public GameObjectEvent OnTargetAcquired;
    [Tooltip("Ya no hay objetivo en alcance.")]
    public UnityEvent OnTargetLost;
    [Tooltip("La punta llegó a un objetivo (de cualquier tipo).")]
    public GameObjectEvent OnTongueReached;
    [Tooltip("Se intentó disparar sin ningún objetivo válido: la lengua no sale.")]
    public UnityEvent OnNoTarget;
    #endregion

    #region Estado
    private State _state = State.Idle;
    private float _cooldownTimer;
    private float _scanTimer;

    private GeckoTongueTargetScanner _scanner;
    private GeckoTongueCandidate _target;    // mejor objetivo actual (mientras está libre)
    private GeckoTongueCandidate _active;    // objetivo de la lengua en uso

    private Vector3 _tipPos;                 // posición world de la punta de la lengua
    private Vector3 _targetPos;              // a dónde va la punta al salir
    private IGeckoEdible _edible;
    private Transform _edibleTf;

    private Transform _anchorTf;             // objeto al que se enganchó (puede moverse)
    private Vector3 _anchorLocal;            // punto de anclaje en local del objeto
    private Vector3 _anchorStatic;           // si el objeto no tiene transform (estático)
    private float _ropeLength;

    private float _reachT, _reachDuration;
    private float _retractT, _retractDuration;
    private Vector3 _retractOffset;          // punta relativa a la boca al empezar a retraer

    private Vector3 _mouthLocalInParent;     // boca relativa al padre de la cadena
    private Transform _chainParent;

    private Quaternion[] _restLocalRot;
    private Vector3[] _restLocalPos;
    private Quaternion _boneAlign = Quaternion.identity;
    private bool _settlingToRest;

    private bool _actionPressed, _actionHeld, _actionReleased, _jumpPressed, _reelHeld, _cancelPressed;
    private float _attachedTimer;

    private AimTargetKind _currentAimTarget = AimTargetKind.None;
    private GUIStyle _hudStyle;
    #endregion

    #region API pública (para mecánicas futuras)
    public State CurrentState => _state;
    public bool Busy => _state != State.Idle;
    public bool IsGrappling => _state == State.Attached;

    /// <summary> Apagalo desde diálogos, cinemáticas, etc. para ignorar el input de la lengua. </summary>
    public bool InputEnabled { get; set; } = true;

    public TargetingMode Mode { get => _targetingMode; set => _targetingMode = value; }
    public float MaxDistance { get => _maxDistance; set => _maxDistance = Mathf.Max(0f, value); }

    /// <summary> Mejor objetivo detectado ahora (válido solo si <see cref="HasTarget"/>). </summary>
    public GeckoTongueCandidate CurrentTarget => _target;
    public bool HasTarget => _target.IsValid;
    /// <summary> Objetivo con el que la lengua está trabajando (solo si <see cref="Busy"/>). </summary>
    public GeckoTongueCandidate ActiveTarget => _active;
    public Vector3 TipPosition => _tipPos;
    public Vector3 MouthPosition => MouthPos();

    /// <summary> A qué apunta la lengua en este instante (para la retícula de UI). Solo tiene
    /// sentido mientras <see cref="Busy"/> es false — apuntar mientras la lengua ya está en uso
    /// no tiene efecto en el disparo, así que se reporta None. </summary>
    public AimTargetKind CurrentAimTarget => _currentAimTarget;
    #endregion

    private void Awake()
    {
        if (_mover == null) _mover = GetComponent<GeckoMover>();
        if (_camera == null) _camera = Camera.main;
        _scanner = new GeckoTongueTargetScanner { Self = transform };
        AutoFillBones();

        if (_bones == null || _bones.Length == 0)
        {
            Debug.LogError("[GeckoTongue] No encontré los huesos Gecko_Tongue1..N.", this);
            enabled = false;
            return;
        }

        _chainParent = _bones[0].parent;
        _mouthLocalInParent = _chainParent.InverseTransformPoint(
            _mouth != null ? _mouth.position : _bones[0].position);

        _restLocalRot = new Quaternion[_bones.Length];
        _restLocalPos = new Vector3[_bones.Length];
        for (int i = 0; i < _bones.Length; i++)
        {
            _restLocalRot[i] = _bones[i].localRotation;
            _restLocalPos[i] = _bones[i].localPosition;
        }

        if (_autoBoneAlign && _bones.Length >= 2)
        {
            Vector3 dir = (_bones[1].position - _bones[0].position);
            if (dir.sqrMagnitude > 1e-8f)
                _boneAlign = Quaternion.Inverse(Quaternion.LookRotation(dir.normalized, Vector3.up)) * _bones[0].rotation;
        }
        else
        {
            _boneAlign = Quaternion.Euler(_boneAlignEuler);
        }

        _tipPos = MouthPos();
    }

    private void OnValidate()
    {
        _maxDistance = Mathf.Max(0f, _maxDistance);
        _scanInterval = Mathf.Max(0.01f, _scanInterval);
        _minExtendTime = Mathf.Max(0.01f, _minExtendTime);
        _minRetractTime = Mathf.Max(0.01f, _minRetractTime);
    }

    private void AutoFillBones()
    {
        if (_bones != null && _bones.Length > 0) return;
        var all = GetComponentsInChildren<Transform>(true);
        var list = new System.Collections.Generic.List<Transform>();
        for (int n = 1; n <= 12; n++)
        {
            Transform found = null;
            foreach (var t in all) if (t.name == "Gecko_Tongue" + n) { found = t; break; }
            if (found == null) break;
            list.Add(found);
        }
        _bones = list.ToArray();
    }

    // ---------------------------------------------------------------- INPUT / FSM
    private void Update()
    {
        ReadInput();

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if (_state == State.Idle) UpdateTargeting();
        else _currentAimTarget = AimTargetKind.None;

        switch (_state)
        {
            case State.Idle:       TickIdle();          break;
            case State.Reaching:   TickReaching();      break;
            case State.Eating:     TickEating();        break;
            case State.Attached:   TickAttachedInput(); break;
            case State.Retracting: TickRetracting();    break;
        }
    }

    private void ReadInput()
    {
        _actionPressed = _actionReleased = _jumpPressed = _cancelPressed = false;
        _actionHeld = false;
        _reelHeld = false;

        if (InputEnabled)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) _actionPressed = true;
                if (mouse.leftButton.isPressed) _actionHeld = true;
                if (mouse.leftButton.wasReleasedThisFrame) _actionReleased = true;
                if (mouse.rightButton.isPressed) _reelHeld = true;       // tirar hacia el ancla
                if (mouse.rightButton.wasPressedThisFrame) _cancelPressed = true;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) _jumpPressed = true;
                if (kb.eKey.wasPressedThisFrame) _actionPressed = true;   // fallback teclado
                if (kb.eKey.isPressed) _actionHeld = true;
                if (kb.eKey.wasReleasedThisFrame) _actionReleased = true;
                if (kb.leftShiftKey.isPressed || kb.leftCtrlKey.isPressed) _reelHeld = true;
                if (kb.qKey.wasPressedThisFrame) _cancelPressed = true;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.rightTrigger.wasPressedThisFrame) _actionPressed = true;
                if (gp.rightTrigger.isPressed) _actionHeld = true;
                if (gp.rightTrigger.wasReleasedThisFrame) _actionReleased = true;
                if (gp.buttonSouth.wasPressedThisFrame) _jumpPressed = true;
                if (gp.leftTrigger.isPressed) _reelHeld = true;
                if (gp.buttonEast.wasPressedThisFrame) _cancelPressed = true;
            }
        }

        if (_useDebugTrigger && _debugFire) { _actionPressed = true; _actionHeld = true; _debugFire = false; }
    }

    // ---------------------------------------------------------------- OBJETIVO
    private void UpdateTargeting()
    {
        if (_targetingMode == TargetingMode.AutoNearest)
        {
            // Un objetivo destruido / desactivado se descarta enseguida, sin esperar al escaneo.
            if (_target.IsValid && (!_target.Collider.enabled || !_target.Collider.gameObject.activeInHierarchy))
                SetTarget(default);

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f || !_target.IsValid)
            {
                if (_scanTimer <= 0f) _scanTimer = _scanInterval;
                RefreshAutoTarget();
            }

            _currentAimTarget = _target.IsValid ? KindToAim(_target.Kind) : AimTargetKind.None;
        }
        else
        {
            if (_target.IsValid) SetTarget(default);
            _currentAimTarget = PeekMouseAim();
        }
    }

    private void ConfigureScanner()
    {
        _scanner.ScanMask = _interactableMask;
        _scanner.EdibleMask = _edibleMask;
        _scanner.ObstacleMask = _obstacleMask;
        _scanner.Tags = _interactableTags;
        _scanner.AcceptDamageables = _acceptDamageables;
        _scanner.ConeAngle = _detectionAngle;
        _scanner.PriorityWeight = _priorityWeight;
        _scanner.Stickiness = _targetStickiness;
        _scanner.Record = _debugGizmos;
    }

    private void RefreshAutoTarget()
    {
        ConfigureScanner();
        Transform previous = _target.IsValid ? _target.Transform : null;
        GeckoTongueCandidate found = _scanner.Scan(MouthPos(), transform.forward, _maxDistance, previous);
        SetTarget(found);
    }

    private void SetTarget(GeckoTongueCandidate next)
    {
        Transform before = _target.IsValid ? _target.Transform : null;
        Transform after = next.IsValid ? next.Transform : null;
        _target = next;
        if (before == after) return;

        if (after != null)
        {
            if (_debugLogs)
                Debug.Log($"[GeckoTongue] Objetivo: '{next.Label}' ({next.Kind}) a {next.Distance:F2} m", this);
            OnTargetAcquired?.Invoke(after.gameObject);
        }
        else
        {
            if (_debugLogs) Debug.Log("[GeckoTongue] Sin objetivo en alcance.", this);
            OnTargetLost?.Invoke();
        }
    }

    private static AimTargetKind KindToAim(GeckoTongueInteraction kind)
    {
        switch (kind)
        {
            case GeckoTongueInteraction.Collect: return AimTargetKind.Edible;
            case GeckoTongueInteraction.Grapple: return AimTargetKind.GrapplePoint;
            default: return AimTargetKind.Interactable;
        }
    }

    // ---------------------------------------------------------------- DISPARO
    private void TickIdle()
    {
        if (_actionPressed) TryFire();
    }

    /// <summary>
    /// Dispara la lengua al mejor objetivo actual. Devuelve false (y la lengua NO sale) si no hay
    /// ninguno válido, si está ocupada / en cooldown o si el gecko cuelga de una soga.
    /// Lo usa el input del jugador y sirve para dispararla desde otros sistemas.
    /// </summary>
    public bool TryFire()
    {
        if (_state != State.Idle || _cooldownTimer > 0f || _mover.Tethered) return false;

        GeckoTongueCandidate candidate;
        if (_targetingMode == TargetingMode.AutoNearest)
        {
            RefreshAutoTarget();   // reescaneo justo al disparar: la elección nunca está vieja
            candidate = _target;
        }
        else if (ResolveMouseAim(out candidate) != MouseAimResult.Valid)
        {
            candidate = default;
        }

        if (!candidate.IsValid)
        {
            if (_debugLogs) Debug.Log("[GeckoTongue] Disparo sin objetivo válido: la lengua no sale.", this);
            OnNoTarget?.Invoke();
            return false;
        }

        BeginReach(candidate);
        return true;
    }

    /// <summary> Corta lo que esté haciendo la lengua: retrae si está saliendo, suelta si cuelga. </summary>
    public void Cancel()
    {
        if (_state == State.Reaching) StartRetract();
        else if (_state == State.Attached) Detach();
    }

    private void BeginReach(GeckoTongueCandidate c)
    {
        _active = c;
        _edible = null; _edibleTf = null; _anchorTf = null;
        _anchorStatic = c.Point;
        _anchorLocal = Vector3.zero;

        if (c.Kind == GeckoTongueInteraction.Collect)
        {
            if (c.Edible != null) _edible = c.Edible;
            else if (c.Collectible != null) _edible = new CollectibleEdible(c.Collectible, this);
            else _edible = new NullEdible(c.Transform);

            _edibleTf = _edible.Transform;
            _targetPos = _edibleTf.position;
        }
        else
        {
            _anchorTf = c.Transform;
            if (_anchorTf != null) _anchorLocal = _anchorTf.InverseTransformPoint(c.Point);
            _targetPos = c.Point;
        }

        _tipPos = MouthPos();
        _reachT = 0f;
        _reachDuration = Mathf.Max(_minExtendTime, Vector3.Distance(_tipPos, _targetPos) / Mathf.Max(_shootSpeed, 0.01f));
        _state = State.Reaching;

        if (_debugLogs)
            Debug.Log($"[GeckoTongue] Disparo a '{c.Label}' ({c.Kind}), {c.Distance:F2} m, {_reachDuration:F2} s.", this);
        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerTongueOut);
    }

    private void TickReaching()
    {
        if (_cancelPressed) { StartRetract(); return; }

        // el objetivo puede moverse (insecto vivo / plataforma)
        if (_edible != null)
        {
            if (_edibleTf == null) { StartRetract(); return; }
            _targetPos = _edibleTf.position;
        }
        else if (_anchorTf != null)
        {
            _targetPos = _anchorTf.TransformPoint(_anchorLocal);
        }

        Vector3 mouth = MouthPos();
        _reachT += Time.deltaTime / _reachDuration;
        float k = _extendCurve.Evaluate(Mathf.Clamp01(_reachT));
        _tipPos = Vector3.LerpUnclamped(mouth, _targetPos, k);

        // Algo se cruzó en el camino (una pared que se movió, otro objeto): la punta se frena ahí
        // y vuelve, en vez de atravesar la geometría.
        if (_scanner.IsPathBlocked(mouth, _tipPos, _active.Collider, _active.Transform, out Vector3 blockPoint))
        {
            _tipPos = blockPoint;
            StartRetract();
            return;
        }

        if (_reachT < 1f) return;

        _tipPos = _targetPos;
        OnTipArrived();
    }

    private void OnTipArrived()
    {
        OnTongueReached?.Invoke(_active.Transform != null ? _active.Transform.gameObject : null);

        switch (_active.Kind)
        {
            case GeckoTongueInteraction.Collect:
                _edible.OnHooked();
                _state = State.Eating;
                break;

            case GeckoTongueInteraction.Grapple:
                Attach();
                break;

            case GeckoTongueInteraction.Damage:
                if (_active.Collider != null) _active.Damageable?.Damage(1f);
                StartRetract();
                break;

            default: // Reach
                if (_active.Collider != null) _active.Reachable?.OnTongueReached(this);
                StartRetract();
                break;
        }
    }

    private void TickEating()
    {
        if (_edibleTf == null) { StartRetract(); return; }

        Vector3 mouth = MouthPos();
        _edibleTf.position = Vector3.MoveTowards(_edibleTf.position, mouth, _pullSpeed * Time.deltaTime);
        _tipPos = _edibleTf.position;

        if (Vector3.Distance(_edibleTf.position, mouth) < 0.06f)
        {
            var e = _edible;
            GameObject go = _edibleTf.gameObject;
            _edible = null; _edibleTf = null;
            e.Eat();
            if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerSlurp);
            OnAte?.Invoke(go);
            StartRetract();
        }
    }

    private void TickAttachedInput()
    {
        _attachedTimer += Time.deltaTime;

        // la punta sigue al ancla si es una plataforma que se mueve
        _tipPos = CurrentAnchor();

        if (_jumpPressed) { LaunchOff(); return; }

        // el ancla desapareció (plataforma destruida, etc.)
        if (_anchorTf == null && _anchorStatic == Vector3.zero) { Detach(); return; }

        // Se mantiene mientras tengas el botón apretado; se suelta al soltarlo. La
        // ventana _attachGrace evita que un click normal (cortito) te suelte al toque.
        if (!_actionHeld && _attachedTimer > _attachGrace)
            Detach();
    }

    private void StartRetract()
    {
        _retractOffset = _tipPos - MouthPos();
        _retractT = 0f;
        _retractDuration = Mathf.Max(_minRetractTime, _retractOffset.magnitude / Mathf.Max(_retractSpeed, 0.01f));
        _state = State.Retracting;
    }

    private void TickRetracting()
    {
        _retractT += Time.deltaTime / _retractDuration;
        float e = _retractCurve.Evaluate(Mathf.Clamp01(_retractT));
        _tipPos = MouthPos() + _retractOffset * (1f - e);

        if (_retractT >= 1f)
        {
            _tipPos = MouthPos();
            _state = State.Idle;
            _cooldownTimer = _cooldown;
            _settlingToRest = true;
            _active = default;
        }
    }

    // ---------------------------------------------------------------- SOGA (física)
    private void FixedUpdate()
    {
        if (_state != State.Attached) return;

        Rigidbody rb = _mover.Body;
        if (rb == null) return;
        float dt = Time.fixedDeltaTime;

        Vector3 anchor = CurrentAnchor();
        Vector3 pos = rb.position;
        Vector3 v = rb.linearVelocity;

        // gravedad (GeckoMover no la aplica mientras estamos tethered)
        v += Vector3.down * _mover.GravityMagnitude * dt;

        // timón del balanceo
        Vector2 inp = _mover.SmoothInput;
        Transform cam = _mover.CamTransform;
        if (cam != null && inp.sqrMagnitude > 0.01f)
        {
            Vector3 cf = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (cf.sqrMagnitude < 1e-4f) cf = Vector3.ProjectOnPlane(cam.up, Vector3.up).normalized;
            Vector3 cr = Vector3.Cross(Vector3.up, cf);
            Vector3 wish = cf * inp.y + cr * inp.x;
            Vector3 ropeDir = (pos - anchor).normalized;
            Vector3 tang = Vector3.ProjectOnPlane(wish, ropeDir);
            if (tang.sqrMagnitude > 1e-5f)
                v += tang.normalized * _swingAccel * dt;
        }

        // acortar la soga con el botón de tirar (RMB / Shift / gatillo izq)
        if (_reelHeld)
            _ropeLength = Mathf.MoveTowards(_ropeLength, _minRopeLength, _reelSpeed * dt);

        // restricción de soga: solo TIRA hacia adentro, nunca empuja. Cuando estás al
        // largo o más lejos, se quita la velocidad radial hacia afuera y se corrige la
        // posición. Adentro del largo caés libre (es soga, no varilla).
        Vector3 toGecko = pos - anchor;
        float dist = toGecko.magnitude;
        if (dist > 1e-4f && dist >= _ropeLength)
        {
            Vector3 dir = toGecko / dist;
            float radialOut = Vector3.Dot(v, dir);
            if (radialOut > 0f) v -= dir * radialOut;

            if (dist > _ropeLength)
            {
                Vector3 corrected = anchor + dir * _ropeLength;
                rb.position = corrected;      // set duro, estable frame a frame
            }
        }

        v *= Mathf.Clamp01(1f - _swingDamping * dt);
        v = Vector3.ClampMagnitude(v, _maxSwingSpeed);
        rb.linearVelocity = v;

        Vector3 look = Vector3.ProjectOnPlane(v, Vector3.up);
        if (look.sqrMagnitude > 0.03f)
        {
            Quaternion tgt = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, tgt, _faceSpeed * dt);
        }
    }

    private void Attach()
    {
        _ropeLength = Mathf.Clamp(
            Vector3.Distance(MouthPos(), CurrentAnchor()), _minRopeLength, _maxDistance);
        _attachedTimer = 0f;
        _state = State.Attached;
        _mover.SetTethered(true);
        AnchorGrapplePoint()?.FlashAttach();
        OnGrappleAttached?.Invoke();
    }

    private void Detach()
    {
        if (_state == State.Attached)
        {
            _mover.SetTethered(false);
            AnchorGrapplePoint()?.FlashRelease();
            OnGrappleReleased?.Invoke();
        }
        StartRetract();
    }

    private void LaunchOff()
    {
        Rigidbody rb = _mover.Body;
        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 boost = Vector3.up * _launchUp;
        if (v.sqrMagnitude > 0.05f) boost += v.normalized * _launchForward;
        _mover.LaunchFromTether(v + boost);
        AnchorGrapplePoint()?.FlashRelease();
        OnGrappleReleased?.Invoke();
        StartRetract();
    }

    /// <summary> El GeckoGrapplePoint del ancla actual, si tiene uno (para el destello de feedback). </summary>
    private GeckoGrapplePoint AnchorGrapplePoint()
    {
        return _anchorTf != null ? _anchorTf.GetComponentInParent<GeckoGrapplePoint>() : null;
    }

    private Vector3 CurrentAnchor()
    {
        if (_anchorTf != null) return _anchorTf.TransformPoint(_anchorLocal);
        return _anchorStatic;
    }

    // ---------------------------------------------------------------- APUNTADO CON MOUSE
    private enum MouseAimResult { None, Valid, Invalid }

    private Ray AimRay()
    {
        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null) return new Ray(MouthPos(), transform.forward);

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
            return cam.ScreenPointToRay(mouse.position.ReadValue());
        return cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
    }

    /// <summary> Resuelve el apuntado con mouse. Solo lee: no toca ningún estado de la lengua. </summary>
    private MouseAimResult ResolveMouseAim(out GeckoTongueCandidate candidate)
    {
        candidate = default;

        Ray ray = AimRay();
        // el rayo sale de la cámara; el alcance real se mide desde la boca.
        if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance * 4f, _aimMask, QueryTriggerInteraction.Collide))
            return MouseAimResult.None;

        Vector3 mouth = MouthPos();
        if (Vector3.Distance(mouth, hit.point) > _maxDistance) return MouseAimResult.None;

        ConfigureScanner();
        if (_scanner.TryResolve(hit.collider, mouth, hit.point, out candidate))
            return MouseAimResult.Valid;

        // Superficie enganchable por capa (parkour de soga): sin componente, solo si la capa lo permite.
        bool maskOk = (_grappleMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        if (_requireGrapplePoint || !maskOk) return MouseAimResult.Invalid;

        candidate = new GeckoTongueCandidate
        {
            Collider = hit.collider,
            Transform = hit.collider.attachedRigidbody != null
                ? hit.collider.attachedRigidbody.transform
                : hit.collider.transform,
            Point = hit.point,
            Kind = GeckoTongueInteraction.Grapple,
            Distance = Vector3.Distance(mouth, hit.point),
        };
        return MouseAimResult.Valid;
    }

    private AimTargetKind PeekMouseAim()
    {
        switch (ResolveMouseAim(out GeckoTongueCandidate c))
        {
            case MouseAimResult.Valid: return KindToAim(c.Kind);
            case MouseAimResult.Invalid: return AimTargetKind.InvalidSurface;
            default: return AimTargetKind.None;
        }
    }

    // ---------------------------------------------------------------- VISUAL
    private void LateUpdate()
    {
        if (_bones == null || _bones.Length == 0) return;

        if (_state == State.Idle)
        {
            if (_settlingToRest) ReturnBonesToRest();
            return;
        }

        Vector3 mouth = MouthPos();
        Vector3 tip = _tipPos;
        int n = _bones.Length;

        float slackNow = _state == State.Attached ? _slack * 0.12f : _slack;
        float len = Vector3.Distance(mouth, tip);
        slackNow *= Mathf.Clamp01(len / Mathf.Max(_maxDistance * 0.5f, 0.01f));

        Vector3 prev = mouth;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 1f) / n;
            Vector3 p = Vector3.Lerp(mouth, tip, t);
            p += Vector3.down * (Mathf.Sin(t * Mathf.PI) * slackNow);

            Vector3 fwd = p - prev;
            if (fwd.sqrMagnitude > 1e-8f)
                _bones[i].rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up) * _boneAlign;
            _bones[i].position = p;
            prev = p;
        }
    }

    private void ReturnBonesToRest()
    {
        float k = 1f - Mathf.Exp(-_restReturnSpeed * Time.deltaTime);
        bool done = true;
        for (int i = 0; i < _bones.Length; i++)
        {
            _bones[i].localRotation = Quaternion.Slerp(_bones[i].localRotation, _restLocalRot[i], k);
            _bones[i].localPosition = Vector3.Lerp(_bones[i].localPosition, _restLocalPos[i], k);
            if (Quaternion.Angle(_bones[i].localRotation, _restLocalRot[i]) > 0.5f) done = false;
        }
        if (done)
        {
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localRotation = _restLocalRot[i];
                _bones[i].localPosition = _restLocalPos[i];
            }
            _settlingToRest = false;
        }
    }

    private Vector3 MouthPos()
    {
        if (_mouth != null) return _mouth.position;
        if (_chainParent != null) return _chainParent.TransformPoint(_mouthLocalInParent);
        return _bones != null && _bones.Length > 0 ? _bones[0].position : transform.position;
    }

    private void OnDisable()
    {
        if (_mover != null && _mover.Tethered) _mover.SetTethered(false);
        if (_restLocalRot != null)
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i] != null) { _bones[i].localRotation = _restLocalRot[i]; _bones[i].localPosition = _restLocalPos[i]; }
    }

    // ---------------------------------------------------------------- DEPURACIÓN
    private void OnDrawGizmos()
    {
        if (!_debugGizmos) return;

        Vector3 mouth = Application.isPlaying ? MouthPos()
            : (_mouth != null ? _mouth.position : transform.position);

        // alcance máximo
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.5f);
        Gizmos.DrawWireSphere(mouth, _maxDistance);
        Gizmos.DrawWireSphere(mouth, 0.03f);

        // cono de detección (solo AutoNearest)
        if (_targetingMode == TargetingMode.AutoNearest && _detectionAngle < 359f)
        {
            float half = _detectionAngle * 0.5f;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Vector3 left = Quaternion.AngleAxis(-half, transform.up) * transform.forward;
            Vector3 right = Quaternion.AngleAxis(half, transform.up) * transform.forward;
            Gizmos.DrawLine(mouth, mouth + left * _maxDistance);
            Gizmos.DrawLine(mouth, mouth + right * _maxDistance);
        }

        if (!Application.isPlaying || _scanner == null) return;

        // candidatos del último escaneo
        foreach (GeckoTongueTargetScanner.Entry e in _scanner.Entries)
        {
            Vector3 p = e.Candidate.Point;
            switch (e.Status)
            {
                case GeckoTongueTargetScanner.Status.Valid:
                    Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                    Gizmos.DrawLine(mouth, p);
                    break;
                case GeckoTongueTargetScanner.Status.OutOfCone:
                    Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                    break;
                default: // Obstructed
                    Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                    Gizmos.DrawLine(mouth, e.BlockPoint);
                    Gizmos.DrawWireCube(e.BlockPoint, Vector3.one * 0.05f);
                    break;
            }
            Gizmos.DrawWireSphere(p, 0.04f);
        }

        // objetivo elegido
        GeckoTongueCandidate shown = Busy ? _active : _target;
        if (shown.IsValid)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(mouth, shown.Point);
            Gizmos.DrawLine(mouth + Vector3.up * 0.004f, shown.Point + Vector3.up * 0.004f);
            Gizmos.DrawSphere(shown.Point, 0.05f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(shown.Point + Vector3.up * 0.12f,
                $"{shown.Label}  [{shown.Kind}]  {shown.Distance:F2} m");
#endif
        }

        if (Busy)
        {
            Gizmos.color = _state == State.Attached ? Color.green : new Color(1f, 0.5f, 0f);
            Gizmos.DrawSphere(_tipPos, 0.03f);
        }
    }

    private void OnGUI()
    {
        if (!_showDebugHud) return;

        _hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };

        GeckoTongueCandidate shown = Busy ? _active : _target;
        string targetText = shown.IsValid
            ? $"{shown.Label}  [{shown.Kind}]  {shown.Distance:F2} m"
            : "ninguno en alcance";

        GUI.color = shown.IsValid ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
        GUI.Label(new Rect(14f, 12f, 700f, 26f), $"Lengua: {_state}   Modo: {_targetingMode}", _hudStyle);
        GUI.Label(new Rect(14f, 36f, 700f, 26f), $"Objetivo: {targetText}", _hudStyle);
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- ADAPTADORES DE COMIDA
    /// <summary> Adaptador para comida detectada solo por capa (sin IGeckoEdible propio). </summary>
    private class NullEdible : IGeckoEdible
    {
        private readonly Transform _tf;
        public NullEdible(Transform tf) { _tf = tf; }
        public Transform Transform => _tf;
        public bool CanBeEaten => _tf != null;
        public void OnHooked()
        {
            if (_tf != null && _tf.TryGetComponent(out Collider c)) c.enabled = false;
        }
        public void Eat()
        {
            if (_tf != null) UnityEngine.Object.Destroy(_tf.gameObject);
        }
    }

    /// <summary>
    /// Deja que la lengua coma los <see cref="Collectible"/> REALES del juego (arándanos, frutillas)
    /// sin tener que agregarles ningún componente. Con GameManager en escena delega en
    /// <c>Collectible.Grab()</c> (misma lógica que la lengua del PJ real). En una escena sandbox,
    /// donde no hay GameManager, hace el equivalente autónomo: cuenta el coleccionable y, si es un
    /// arándano, suma al combo de GeckoBlueberryCombo.
    /// </summary>
    private class CollectibleEdible : IGeckoEdible
    {
        private readonly Collectible _collectible;
        private readonly GeckoTongue _owner;
        public CollectibleEdible(Collectible collectible, GeckoTongue owner)
        {
            _collectible = collectible;
            _owner = owner;
        }

        public Transform Transform => _collectible != null ? _collectible.transform : null;
        public bool CanBeEaten => _collectible != null;

        public void OnHooked()
        {
            // Sin esto, el trigger de contacto (GeckoBlueberryPickup) lo levantaría dos veces.
            if (_collectible != null)
                foreach (Collider c in _collectible.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        public void Eat()
        {
            if (_collectible == null) return;

            if (GameManager.Instance != null)
            {
                _collectible.Grab();
                return;
            }

            CollectiblesRegister.RegisterCollectible(_collectible.CollectibleName ?? _collectible.name);
            if (_collectible is Blueberry) _owner.GetComponent<GeckoBlueberryCombo>()?.OnCollect();
            UnityEngine.Object.Destroy(_collectible.gameObject);
        }
    }
}
