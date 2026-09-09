using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// La lengua del gecko, TODO por código sobre los huesos <c>Gecko_Tongue1..7</c>
/// (deforman el skinned mesh <c>lengua_lp</c>, así que se estira sola).
///
/// Un solo botón — CLICK IZQUIERDO — hace las dos cosas según a qué apuntes:
///
///  • Apuntás a un insecto / collectible (algo con <see cref="IGeckoEdible"/> o en
///    <see cref="_edibleMask"/>)  ->  la lengua sale, lo engancha, lo trae a la boca
///    y se lo come.
///  • Apuntás a una superficie / punto de gancho válido (<see cref="_grappleMask"/>,
///    opcionalmente con <see cref="GeckoGrapplePoint"/>)  ->  la lengua se clava ahí y
///    el gecko queda colgando de una SOGA con física de péndulo, estilo Big Hops:
///      - stick / WASD  ->  bombea el balanceo
///      - mantener CLICK ->  se acorta la soga (te acercás al ancla)
///      - soltar CLICK   ->  te soltás
///      - ESPACIO        ->  te lanzás con el impulso del swing
///
/// Corre después de GeckoSecondaryMotion / GeckoIdlePose para posar la lengua sobre
/// un cuello ya animado. Comparte el Rigidbody con GeckoMover: mientras cuelga,
/// GeckoMover se hace a un lado (<see cref="GeckoMover.SetTethered"/>).
/// </summary>
[DefaultExecutionOrder(310)]
[RequireComponent(typeof(GeckoMover))]
public class GeckoTongue : MonoBehaviour
{
    private enum State { Idle, Reaching, Eating, Attached, Retracting }

    [Serializable] public class GameObjectEvent : UnityEvent<GameObject> { }

    #region Inspector
    [Header("Huesos (se autocompletan: Gecko_Tongue1..7)")]
    [SerializeField] private Transform[] _bones;
    [Tooltip("Punto de la boca de donde sale la lengua. Vacío = se deriva de la base de " +
             "la cadena (Gecko_Tongue1) siguiendo la cabeza.")]
    [SerializeField] private Transform _mouth;

    [Header("Refs")]
    [SerializeField] private GeckoMover _mover;
    [Tooltip("Cámara para apuntar con el mouse. Vacío = Camera.main.")]
    [SerializeField] private Camera _camera;

    [Header("Apuntado")]
    [Tooltip("Alcance máximo de la lengua desde la boca (metros).")]
    [SerializeField] private float _maxDistance = 3.2f;
    [Tooltip("Capas contra las que apunta el rayo (insectos + superficies).")]
    [SerializeField] private LayerMask _aimMask = ~0;
    [Tooltip("Capas que cuentan como comida si el objeto no trae IGeckoEdible.")]
    [SerializeField] private LayerMask _edibleMask = 0;
    [Tooltip("Capas a las que la soga se puede enganchar.")]
    [SerializeField] private LayerMask _grappleMask = (1 << 6) | (1 << 7) | (1 << 8);
    [Tooltip("Si está, SOLO se engancha a objetos con GeckoGrapplePoint.")]
    [SerializeField] private bool _requireGrapplePoint = false;

    [Header("Lengua — velocidades")]
    [Tooltip("Velocidad de salida de la punta hacia el objetivo (m/s).")]
    [SerializeField] private float _shootSpeed = 22f;
    [Tooltip("Velocidad de retracción de la lengua (m/s).")]
    [SerializeField] private float _retractSpeed = 16f;
    [Tooltip("Velocidad con la que el insecto es arrastrado a la boca (m/s).")]
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

    [Header("Debug")]
    [SerializeField] private bool _useDebugTrigger;
    [SerializeField] private bool _debugFire;

    [Header("Eventos")]
    public GameObjectEvent OnAte;
    public UnityEvent OnGrappleAttached;
    public UnityEvent OnGrappleReleased;
    #endregion

    #region Estado
    private State _state = State.Idle;
    private float _cooldownTimer;

    private Vector3 _tipPos;                 // posición world de la punta de la lengua
    private Vector3 _targetPos;              // a dónde va la punta al salir
    private IGeckoEdible _edible;
    private Transform _edibleTf;

    private Transform _anchorTf;             // objeto al que se enganchó (puede moverse)
    private Vector3 _anchorLocal;            // punto de anclaje en local del objeto
    private Vector3 _anchorStatic;           // si el objeto no tiene transform (estático)
    private float _ropeLength;

    private Vector3 _mouthLocalInParent;     // boca relativa al padre de la cadena
    private Transform _chainParent;

    private Quaternion[] _restLocalRot;
    private Vector3[] _restLocalPos;
    private Quaternion _boneAlign = Quaternion.identity;
    private bool _settlingToRest;

    private bool _actionPressed, _actionHeld, _actionReleased, _jumpPressed, _reelHeld;
    private float _attachedTimer;
    private bool _hadAnchor;
    #endregion

    public bool Busy => _state != State.Idle;
    public bool IsGrappling => _state == State.Attached;

    private void Awake()
    {
        if (_mover == null) _mover = GetComponent<GeckoMover>();
        if (_camera == null) _camera = Camera.main;
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

        switch (_state)
        {
            case State.Idle:      TickIdle();      break;
            case State.Reaching:  TickReaching();  break;
            case State.Eating:    TickEating();    break;
            case State.Attached:  TickAttachedInput(); break;
            case State.Retracting: TickRetracting(); break;
        }
    }

    private void ReadInput()
    {
        _actionPressed = _actionReleased = _jumpPressed = false;
        _actionHeld = false;
        _reelHeld = false;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) _actionPressed = true;
            if (mouse.leftButton.isPressed) _actionHeld = true;
            if (mouse.leftButton.wasReleasedThisFrame) _actionReleased = true;
            if (mouse.rightButton.isPressed) _reelHeld = true;       // tirar hacia el ancla
        }

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) _jumpPressed = true;
            if (kb.eKey.wasPressedThisFrame) _actionPressed = true;   // fallback teclado
            if (kb.eKey.isPressed) _actionHeld = true;
            if (kb.eKey.wasReleasedThisFrame) _actionReleased = true;
            if (kb.leftShiftKey.isPressed || kb.leftCtrlKey.isPressed) _reelHeld = true;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.rightTrigger.wasPressedThisFrame) _actionPressed = true;
            if (gp.rightTrigger.isPressed) _actionHeld = true;
            if (gp.rightTrigger.wasReleasedThisFrame) _actionReleased = true;
            if (gp.buttonSouth.wasPressedThisFrame) _jumpPressed = true;
            if (gp.leftTrigger.isPressed) _reelHeld = true;
        }

        if (_useDebugTrigger && _debugFire) { _actionPressed = true; _actionHeld = true; _debugFire = false; }
    }

    private void TickIdle()
    {
        if (!_actionPressed || _cooldownTimer > 0f || _mover.Tethered) return;

        _edible = null; _edibleTf = null; _anchorTf = null; _hadAnchor = false;
        _tipPos = MouthPos();

        if (TryAim(out _, out bool isEdible, out IGeckoEdible edible, out Vector3 anchor, out Transform anchorTf))
        {
            if (isEdible)
            {
                _edible = edible;
                _edibleTf = edible.Transform;
                _targetPos = _edibleTf.position;
                edible.OnHooked();
            }
            else
            {
                _targetPos = anchor;
                _anchorTf = anchorTf;
                _anchorStatic = anchor;
                if (_anchorTf != null) _anchorLocal = _anchorTf.InverseTransformPoint(anchor);
            }
        }
        else
        {
            // lick al aire: la punta sale un poco y vuelve, feedback de que no había nada.
            _targetPos = MouthPos() + AimRay().direction.normalized * (_maxDistance * 0.45f);
        }

        _state = State.Reaching;
    }

    private void TickReaching()
    {
        // el objetivo puede moverse (insecto vivo / plataforma)
        if (_edibleTf != null) _targetPos = _edibleTf.position;
        else if (_anchorTf != null) _targetPos = _anchorTf.TransformPoint(_anchorLocal);

        _tipPos = Vector3.MoveTowards(_tipPos, _targetPos, _shootSpeed * Time.deltaTime);
        if (Vector3.Distance(_tipPos, _targetPos) > 0.03f) return;

        if (_edible != null)      _state = State.Eating;
        else if (_hadAnchor)      Attach();
        else                      _state = State.Retracting;   // lick al aire
    }

    private void TickEating()
    {
        if (_edibleTf == null) { _state = State.Retracting; return; }

        Vector3 mouth = MouthPos();
        _edibleTf.position = Vector3.MoveTowards(_edibleTf.position, mouth, _pullSpeed * Time.deltaTime);
        _tipPos = _edibleTf.position;

        if (Vector3.Distance(_edibleTf.position, mouth) < 0.06f)
        {
            var e = _edible;
            GameObject go = _edibleTf.gameObject;
            _edible = null; _edibleTf = null;
            e.Eat();
            OnAte?.Invoke(go);
            _state = State.Retracting;
        }
    }

    private void TickAttachedInput()
    {
        _attachedTimer += Time.deltaTime;

        if (_jumpPressed) { LaunchOff(); return; }

        // el ancla desapareció (plataforma destruida, etc.)
        if (_anchorTf == null && _anchorStatic == Vector3.zero) { Detach(); return; }

        // Se mantiene mientras tengas el botón apretado; se suelta al soltarlo. La
        // ventana _attachGrace evita que un click normal (cortito) te suelte al toque.
        if (!_actionHeld && _attachedTimer > _attachGrace)
            Detach();
    }

    private void TickRetracting()
    {
        _tipPos = Vector3.MoveTowards(_tipPos, MouthPos(), _retractSpeed * Time.deltaTime);
        if (Vector3.Distance(_tipPos, MouthPos()) < 0.02f)
        {
            _state = State.Idle;
            _cooldownTimer = _cooldown;
            _hadAnchor = false;
            _settlingToRest = true;
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
        OnGrappleAttached?.Invoke();
    }

    private void Detach()
    {
        if (_state == State.Attached)
        {
            _mover.SetTethered(false);
            OnGrappleReleased?.Invoke();
        }
        _state = State.Retracting;
    }

    private void LaunchOff()
    {
        Rigidbody rb = _mover.Body;
        Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 boost = Vector3.up * _launchUp;
        if (v.sqrMagnitude > 0.05f) boost += v.normalized * _launchForward;
        _mover.LaunchFromTether(v + boost);
        OnGrappleReleased?.Invoke();
        _state = State.Retracting;
    }

    private Vector3 CurrentAnchor()
    {
        if (_anchorTf != null) return _anchorTf.TransformPoint(_anchorLocal);
        return _anchorStatic;
    }

    // ---------------------------------------------------------------- APUNTADO
    private Ray AimRay()
    {
        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null) return new Ray(MouthPos(), transform.forward);

        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked)
            return cam.ScreenPointToRay(mouse.position.ReadValue());
        return cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
    }

    private bool TryAim(out RaycastHit hit, out bool isEdible, out IGeckoEdible edible,
                        out Vector3 anchor, out Transform anchorTf)
    {
        isEdible = false; edible = null; anchor = Vector3.zero; anchorTf = null;
        _hadAnchor = false;

        Ray ray = AimRay();
        // el rayo sale de la cámara; el alcance real se mide desde la boca.
        if (!Physics.Raycast(ray, out hit, _maxDistance * 4f, _aimMask, QueryTriggerInteraction.Collide))
            return false;

        Vector3 mouth = MouthPos();
        if (Vector3.Distance(mouth, hit.point) > _maxDistance) return false;

        // ¿comida?
        var e = hit.collider.GetComponentInParent<IGeckoEdible>();
        if (e == null) e = hit.collider.GetComponent<IGeckoEdible>();
        bool edibleByMask = (_edibleMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        if ((e != null && e.CanBeEaten) || edibleByMask)
        {
            isEdible = true;
            edible = e ?? new NullEdible(hit.collider.transform);
            return true;
        }

        // ¿gancho?
        var gp = hit.collider.GetComponentInParent<GeckoGrapplePoint>();
        bool maskOk = (_grappleMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        if ((_requireGrapplePoint && gp == null) || (!_requireGrapplePoint && !maskOk && gp == null))
            return false;

        anchor = (gp != null && gp.SnapToCenter) ? gp.AnchorPosition : hit.point;
        anchorTf = hit.collider.attachedRigidbody != null
            ? hit.collider.attachedRigidbody.transform
            : (gp != null ? gp.transform : hit.collider.transform);
        _hadAnchor = true;
        return true;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 m = Application.isPlaying ? MouthPos()
            : (_mouth != null ? _mouth.position : transform.position);
        Gizmos.DrawWireSphere(m, 0.03f);
        Gizmos.DrawWireSphere(m, _maxDistance);
        if (Application.isPlaying && _state != State.Idle)
        {
            Gizmos.color = _state == State.Attached ? Color.green : Color.yellow;
            Gizmos.DrawLine(m, _tipPos);
            Gizmos.DrawSphere(_tipPos, 0.03f);
        }
    }

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
}
