using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador jugable del Gecko para un plataformero 3D "trepador".
///
/// El Gecko se mueve RELATIVO A LA CÁMARA y se pega a piso, paredes y techo:
/// alinea su 'up' con la normal de la superficie y, cuando la superficie no es
/// piso, corta la gravedad y aplica una fuerza de adherencia.
///
///   Stick izquierdo / WASD / flechas -> mover (relativo a la cámara)
///   Espacio / botón sur del gamepad  -> saltar (en la dirección del 'up' actual)
///
/// Es autónomo: NO depende de GameManager ni del resto del sistema Player, así
/// funciona en la escena de prueba tal cual. GeckoAnimation (las patas
/// procedurales) sigue andando igual porque mide la velocidad del cuerpo sola.
///
/// Expone CurrentUp / Grounded / IsMoving con la misma forma que PlayerController
/// por si más adelante se enchufan las cámaras del juego principal.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GeckoMover : MonoBehaviour
{
    #region Variables
    [Header("Cámara")]
    [Tooltip("Referencia para el movimiento relativo a cámara. Si se deja vacío usa Camera.main.")]
    [SerializeField] private Transform _camTransform;

    [Header("Movimiento")]
    [SerializeField] private float _speed = 1.6f;
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _jumpForce = 2.6f;
    [Tooltip("Suavizado del input de movimiento (segundos).")]
    [SerializeField] private float _inputSmoothing = 0.12f;
    [Tooltip("Cuánto controla el jugador el movimiento mientras está en el aire.")]
    [SerializeField] private float _airControl = 6f;

    [Header("Gravedad / adherencia")]
    [SerializeField] private float _gravity = 9.81f;
    [Tooltip("Multiplica la gravedad al caer para que el salto no se sienta flotante.")]
    [SerializeField] private float _fallMultiplier = 2.2f;
    [Tooltip("Fuerza que mantiene al Gecko pegado a paredes y techo.")]
    [SerializeField] private float _stickForce = 18f;
    [Tooltip("Qué tan rápido gira el 'up' del cuerpo hacia la normal de la superficie.")]
    [SerializeField] private float _alignSpeed = 8f;

    [Header("Detección de superficie")]
    [Tooltip("Capas que cuentan como PISO (gravedad normal).")]
    [SerializeField] private LayerMask _groundMask = 1 << 6;                // Ground
    [Tooltip("Capas que cuentan como TREPABLES (pared / techo).")]
    [SerializeField] private LayerMask _climbMask = (1 << 7) | (1 << 8);   // Wall + Ceiling
    [Tooltip("Radio del spherecast de detección. Chico, acorde al tamaño del Gecko.")]
    [SerializeField] private float _castRadius = 0.05f;
    [Tooltip("Distancia del spherecast de detección.")]
    [SerializeField] private float _castDistance = 0.22f;
    [Tooltip("Offset del origen de los rayos sobre el pivote del Gecko.")]
    [SerializeField] private float _bodyOffset = 0.08f;

    [Header("Salto")]
    [Tooltip("Ventana tras dejar una superficie en la que todavía se puede saltar.")]
    [SerializeField] private float _coyoteTime = 0.15f;

    private Rigidbody _rb;
    private LayerMask _surfaceMask;

    private Vector3 _currentUp = Vector3.up;
    private Vector3 _surfaceNormal = Vector3.up;
    private Vector2 _rawInput;
    private Vector2 _smoothInput;
    private Vector2 _smoothInputVel;
    private Vector3 _lastDir;

    private bool _isSurface;   // tocando cualquier superficie
    private bool _isGround;    // la superficie actual es piso
    private bool _isClimbing;  // la superficie actual es pared / techo
    private bool _isGrounded;  // hay piso justo debajo (salto + animación)
    private bool _isMoving;

    private float _coyoteTimer;
    private float _jumpGrace;  // tiempo tras saltar en el que no se re-adhiere
    private bool _jumpQueued;

    // Hook de prueba: si _useDebugInput está activo se ignora el teclado y se
    // usa _debugInput. Sirve para testear el movimiento sin foco de ventana.
    [Header("Debug")]
    [SerializeField] private bool _useDebugInput;
    [SerializeField] private Vector2 _debugInput;

    public Vector3 CurrentUp => _currentUp;
    public bool Grounded => _isGrounded;
    public bool IsMoving => _isMoving;
    public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    #endregion

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;                 // la gravedad la manejamos nosotros
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _surfaceMask = _groundMask | _climbMask;

        if (_camTransform == null && Camera.main != null)
            _camTransform = Camera.main.transform;
    }

    private void Update()
    {
        ReadInput();
        _smoothInput = Vector2.SmoothDamp(_smoothInput, _rawInput, ref _smoothInputVel, _inputSmoothing);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (_jumpGrace > 0f) _jumpGrace -= dt;

        DetectSurface(dt);

        if (_isSurface) _coyoteTimer = _coyoteTime;
        else _coyoteTimer -= dt;

        if (_jumpQueued && _coyoteTimer > 0f)
            DoJump();
        _jumpQueued = false;

        UpdateRotation(dt);
        Move(dt);
    }

    private void ReadInput()
    {
        _rawInput = Vector2.zero;

        if (_useDebugInput)
        {
            _rawInput = Vector2.ClampMagnitude(_debugInput, 1f);
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) _rawInput.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) _rawInput.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _rawInput.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) _rawInput.x -= 1f;
            if (kb.spaceKey.wasPressedThisFrame) _jumpQueued = true;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 ls = gp.leftStick.ReadValue();
            if (ls.sqrMagnitude > _rawInput.sqrMagnitude) _rawInput = ls;
            if (gp.buttonSouth.wasPressedThisFrame) _jumpQueued = true;
        }

        if (_rawInput.sqrMagnitude > 1f) _rawInput.Normalize();
    }

    private void DetectSurface(float dt)
    {
        // Justo después de saltar ignoramos las superficies para poder despegar.
        if (_jumpGrace > 0f)
        {
            _isSurface = _isGround = _isClimbing = _isGrounded = false;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, _alignSpeed * dt).normalized;
            return;
        }

        Vector3 origin = transform.position + _currentUp * _bodyOffset;
        Vector3[] dirs =
        {
            -_currentUp,
            Vector3.down,
            transform.forward,
            -transform.forward,
            transform.right,
            -transform.right,
        };

        bool found = false;
        RaycastHit best = default;
        float bestScore = float.NegativeInfinity;

        foreach (Vector3 d in dirs)
        {
            if (Physics.SphereCast(origin, _castRadius, d, out RaycastHit hit,
                    _castDistance, _surfaceMask, QueryTriggerInteraction.Ignore))
            {
                float distScore = 1f - hit.distance / _castDistance;
                float downPenalty = Mathf.Clamp01(Vector3.Dot(hit.normal, Vector3.up)) * 0.15f;
                float alignBonus = Vector3.Dot(hit.normal, _currentUp) * 0.15f;
                // superficie contra la que estamos caminando de frente (pared que se
                // quiere trepar): su normal apunta hacia -forward. Le damos prioridad
                // fuerte para que la transición piso -> pared no la gane siempre el piso.
                float intoBonus = Mathf.Clamp01(Vector3.Dot(hit.normal, -transform.forward)) * 0.7f;
                float score = distScore - downPenalty + alignBonus + intoBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit;
                    found = true;
                }
            }
        }

        if (!found)
        {
            _isSurface = _isGround = _isClimbing = _isGrounded = false;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, _alignSpeed * dt).normalized;
            return;
        }

        _surfaceNormal = best.normal;
        _isSurface = true;

        int layerBit = 1 << best.collider.gameObject.layer;
        _isGround = (_groundMask.value & layerBit) != 0;
        _isClimbing = !_isGround;

        _currentUp = Vector3.Slerp(_currentUp, _surfaceNormal, _alignSpeed * dt).normalized;

        _isGrounded = _isGround && (best.distance < _castDistance * 0.85f ||
            Physics.SphereCast(origin, _castRadius, -_currentUp, out _,
                _castDistance, _groundMask, QueryTriggerInteraction.Ignore));
    }

    private void UpdateRotation(float dt)
    {
        Vector3 desiredForward = _lastDir.sqrMagnitude > 0.0001f
            ? Vector3.ProjectOnPlane(_lastDir, _currentUp)
            : Vector3.ProjectOnPlane(transform.forward, _currentUp);

        if (desiredForward.sqrMagnitude < 0.0001f)
            desiredForward = Vector3.ProjectOnPlane(transform.forward, _currentUp);
        if (desiredForward.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(desiredForward.normalized, _currentUp);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, _rotationSpeed * dt);
    }

    private void Move(float dt)
    {
        Vector3 up = _currentUp;
        Transform camRef = _camTransform != null ? _camTransform : transform;

        Vector3 camForward = Vector3.ProjectOnPlane(camRef.forward, up).normalized;
        if (camForward.sqrMagnitude < 0.0001f)
            camForward = Vector3.ProjectOnPlane(camRef.up, up).normalized;
        Vector3 camRight = Vector3.Cross(up, camForward).normalized;

        Vector3 wish = camForward * _smoothInput.y + camRight * _smoothInput.x;
        wish = Vector3.ProjectOnPlane(wish, up);

        _isMoving = wish.sqrMagnitude > 0.0025f;
        if (_isMoving) _lastDir = wish.normalized;

        Vector3 planarVel = Vector3.ClampMagnitude(wish, 1f) * _speed;

        if (_isClimbing)
        {
            _rb.linearVelocity = planarVel;
            _rb.AddForce(-up * _stickForce, ForceMode.Acceleration);
        }
        else if (_isGrounded)
        {
            float downV = Mathf.Min(Vector3.Dot(_rb.linearVelocity, up), 0f);
            _rb.linearVelocity = planarVel + up * downV;
            _rb.AddForce(-up * (_gravity * 0.5f), ForceMode.Acceleration); // mantiene contacto
        }
        else
        {
            float g = _gravity;
            if (Vector3.Dot(_rb.linearVelocity, Vector3.up) < 0f) g *= _fallMultiplier;

            Vector3 vertical = Vector3.Project(_rb.linearVelocity, Vector3.up);
            Vector3 horizontal = _rb.linearVelocity - vertical;
            horizontal = Vector3.Lerp(horizontal, planarVel, _airControl * dt);

            _rb.linearVelocity = horizontal + vertical;
            _rb.AddForce(Vector3.down * g, ForceMode.Acceleration);
        }

        // Red de seguridad: la depenetración de colliders convexos en esquinas
        // (sobre todo al coronar una pared) puede lanzar al Gecko. Nunca dejamos
        // que la velocidad supere un múltiplo razonable de la de movimiento.
        float maxSpeed = Mathf.Max(_speed, _jumpForce) * 4f + _gravity;
        if (_rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
    }

    private void DoJump()
    {
        _isClimbing = false;
        _isSurface = false;
        _isGrounded = false;
        _coyoteTimer = 0f;
        _jumpGrace = 0.25f;

        Vector3 v = _rb.linearVelocity;
        v -= Vector3.Project(v, _currentUp);   // limpia la componente vertical previa
        v += _currentUp * _jumpForce;
        _rb.linearVelocity = v;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + _currentUp * _bodyOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin - _currentUp * _castDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + transform.forward * _castDistance);
    }
}
