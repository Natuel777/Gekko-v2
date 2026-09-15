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
    [SerializeField] private float _speed = 1.8f;
    [Tooltip("Multiplicador de velocidad en vivo (ej. boost de arándanos). 1 = normal.")]
    private float _speedMultiplier = 1f;
    [Tooltip("Que tan rapido el cuerpo gira hacia la direccion de movimiento y se alinea a " +
             "la superficie. Por debajo de ~6 el bicho tarda demasiado en acomodarse a una " +
             "pared y da la sensacion de que no puede trepar.")]
    [SerializeField] private float _rotationSpeed = 14f;
    [SerializeField] private float _jumpForce = 2.6f;
    [Tooltip("Suavizado del input de movimiento (segundos).")]
    [SerializeField] private float _inputSmoothing = 0.10f;
    [Tooltip("Cuánto controla el jugador el movimiento mientras está en el aire.")]
    [SerializeField] private float _airControl = 6f;

    [Header("Aceleración (feel)")]
    [Tooltip("m/s de velocidad planar que gana por segundo al arrancar. Alto = respuesta " +
             "instantánea (arcade); bajo = arranque con peso. Para un plataformero rápido va " +
             "alto pero no infinito, así el paso procedural no 'salta'.")]
    [SerializeField] private float _acceleration = 14f;
    [Tooltip("m/s que pierde por segundo al soltar el stick. Un poco más alto que la " +
             "aceleración para que frene firme sin patinar.")]
    [SerializeField] private float _deceleration = 20f;
    [Tooltip("Aceleración planar mientras trepa una pared. Suele querer ser más baja que en " +
             "piso para que el agarre se vea seguro.")]
    [SerializeField] private float _climbAcceleration = 10f;
    [Tooltip("m/s que pierde por segundo al soltar el stick mientras trepa. Antes compartía " +
             "el mismo valor que la aceleración de trepada; separado para poder frenar más " +
             "firme en la pared sin tener que tocar qué tan rápido arranca.")]
    [SerializeField] private float _climbDeceleration = 14f;
    [Tooltip("Velocidad tope al trepar. Antes usaba la misma velocidad que el piso, lo que " +
             "hacía que trepar se sintiera 'como caminar con otra textura'. Un poco más lenta " +
             "que _speed para que se note que está trepando, no corriendo en vertical. Ojo: " +
             "es un valor absoluto (m/s), no un porcentaje — si tocás _speed, revisá que siga " +
             "siendo más baja.")]
    [SerializeField] private float _climbSpeed = 0.65f;

    [Header("Gravedad / adherencia")]
    [SerializeField] private float _gravity = 9.81f;
    [Tooltip("Multiplica la gravedad al caer para que el salto no se sienta flotante.")]
    [SerializeField] private float _fallMultiplier = 2.2f;
    [Tooltip("Fuerza que mantiene al Gecko pegado a paredes y techo.")]
    [SerializeField] private float _stickForce = 18f;
    [Tooltip("Qué tan rápido gira el 'up' del cuerpo hacia la normal de la superficie.")]
    [SerializeField] private float _alignSpeed = 8f;
    [Tooltip("Bonus de puntaje para la MISMA superficie en la que ya está parado (histéresis). " +
             "Sin esto, cerca de una esquina dos superficies pueden quedar con puntajes casi " +
             "empatados y el personaje 'caza' entre las dos cada frame — ej. al coronar una " +
             "pared, sube/baja en bucle con saltos de rotación en cada cambio. Con este bonus, " +
             "una superficie candidata nueva tiene que ganarle CLARO a la actual para reemplazarla.")]
    [SerializeField] private float _surfaceStickiness = 0.1f;

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
    [Tooltip("Ventana ANTES de tocar superficie en la que un salto presionado queda guardado " +
             "y se ejecuta apenas aterrizás. Sin esto, saltar un instante antes de tocar el " +
             "piso (algo muy común apretando rápido) se perdía en silencio.")]
    [SerializeField] private float _jumpBufferTime = 0.12f;

    private Rigidbody _rb;
    private LayerMask _surfaceMask;
    private Vector3 _surfacePoint;
    private bool _hasSurfacePoint;

    private Vector3 _currentUp = Vector3.up;
    private Vector3 _surfaceNormal = Vector3.up;
    private Vector2 _rawInput;
    private Vector2 _smoothInput;
    private Vector2 _smoothInputVel;
    private Vector3 _lastDir;
    private Vector3 _planarVel;   // velocidad planar suavizada (accel/decel) — el "feel"

    private bool _isSurface;   // tocando cualquier superficie
    private bool _isGround;    // la superficie actual es piso
    private bool _isClimbing;  // la superficie actual es pared / techo
    private bool _isGrounded;  // hay piso justo debajo (salto + animación)
    private bool _isMoving;

    // Para detectar el instante exacto en que cambia el "modo" de movimiento (aire/piso/pared)
    // y resembrar _planarVel desde la velocidad REAL del rigidbody en ese frame — sin esto,
    // Move() pisaba la velocidad con un _planarVel que venía de OTRO modo y podía no estar
    // perfectamente sincronizado, perdiendo un poco de momentum justo en la transición.
    private bool _prevIsClimbing;
    private bool _prevIsGrounded;

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _jumpGrace;  // tiempo tras saltar en el que no se re-adhiere

    // Hook de prueba: si _useDebugInput está activo se ignora el teclado y se
    // usa _debugInput. Sirve para testear el movimiento sin foco de ventana.
    [Header("Debug")]
    [SerializeField] private bool _useDebugInput;
    [SerializeField] private Vector2 _debugInput;

    public Vector3 CurrentUp => _currentUp;
    public bool Grounded => _isGrounded;
    public bool IsMoving => _isMoving;
    public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
    /// <summary> True cuando la superficie actual es pared / techo (no piso). </summary>
    public bool IsClimbing => _isClimbing;
    /// <summary> True mientras toca cualquier superficie (piso, pared o techo). </summary>
    public bool OnSurface => _isSurface;
    /// <summary> Normal de la superficie que está pisando ahora (o Vector3.up en el aire). </summary>
    public Vector3 SurfaceNormal => _surfaceNormal;

    /// <summary> True mientras GeckoTongue tiene al gecko colgando de la soga. </summary>
    public bool Tethered => _tethered;
    /// <summary> Input de movimiento ya suavizado (stick/teclas), para que el tongue timonee el balanceo. </summary>
    public Vector2 SmoothInput => _smoothInput;
    /// <summary> Rigidbody del cuerpo (lo comparte con GeckoTongue para resolver la soga). </summary>
    public Rigidbody Body => _rb;
    /// <summary> Cámara de referencia para movimiento relativo. </summary>
    public Transform CamTransform => _camTransform;
    /// <summary> Gravedad configurada (m/s²), para que el tongue use la misma al colgar. </summary>
    public float GravityMagnitude => _gravity;
    private bool _tethered;

    /// <summary> Multiplicador de velocidad en vivo (ej. GeckoBlueberryCombo). 1 = normal. </summary>
    public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = multiplier;

    /// <summary>
    /// Lo llama GeckoTongue al enganchar / soltar la soga. Con la soga activa, GeckoMover
    /// deja de tocar el Rigidbody y el tongue maneja gravedad + restricción + balanceo.
    /// </summary>
    public void SetTethered(bool active)
    {
        _tethered = active;
        _rb.useGravity = false;
        if (!active)
            _planarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up);
    }

    /// <summary>
    /// Salto desde la soga: suelta el tether y arranca en el aire con la velocidad dada
    /// (normalmente la del balanceo + un empujón). Deja ventana de jumpGrace para despegar.
    /// </summary>
    public void LaunchFromTether(Vector3 velocity)
    {
        _tethered = false;
        _isSurface = _isGround = _isClimbing = _isGrounded = false;
        _coyoteTimer = 0f;
        _jumpGrace = 0.25f;
        _rb.linearVelocity = velocity;
        _planarVel = Vector3.ProjectOnPlane(velocity, Vector3.up);
    }
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
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;

        // Mientras cuelga de la lengua-soga, GeckoTongue maneja TODA la física del
        // cuerpo (gravedad, restricción de soga, balanceo, lanzamiento). Acá solo se
        // sigue leyendo input (en Update) para que el tongue lo use como timón.
        if (_tethered)
        {
            _isSurface = _isGround = _isClimbing = _isGrounded = false;
            return;
        }

        DetectSurface(dt);

        if (_isSurface) _coyoteTimer = _coyoteTime;
        else _coyoteTimer -= dt;

        // Salto = coyote (superficie) Y buffer (input) activos a la vez. Cubre los dos
        // casos clásicos: apretar salto un toque tarde (coyote) y apretarlo un toque
        // antes de aterrizar (buffer) — ninguno de los dos debería sentirse "perdido".
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            DoJump();
            _jumpBufferTimer = 0f;
        }

        // Resembrado de momentum: si el modo de movimiento (aire/piso/pared) cambió este
        // frame, _planarVel puede no estar sincronizado con la velocidad real (venía de
        // OTRO modo, con su propia lógica de tracking). Lo resembramos desde la velocidad
        // real del rigidbody ANTES de que Move() la pise, para no perder momentum justo en
        // la transición (ej. correr -> pared, pared -> piso).
        if (_isClimbing != _prevIsClimbing || _isGrounded != _prevIsGrounded)
            _planarVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _currentUp);

        // Sonido de aterrizaje: mismo cue que usa el personaje principal (PJ_Land), en el
        // instante exacto en que se pasa de no-piso a piso.
        if (_isGrounded && !_prevIsGrounded && AudioManager.instance != null)
            AudioManager.instance.Play(SoundNames.PlayerLanding);

        _prevIsClimbing = _isClimbing;
        _prevIsGrounded = _isGrounded;

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
            if (kb.spaceKey.wasPressedThisFrame) _jumpBufferTimer = _jumpBufferTime;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 ls = gp.leftStick.ReadValue();
            if (ls.sqrMagnitude > _rawInput.sqrMagnitude) _rawInput = ls;
            if (gp.buttonSouth.wasPressedThisFrame) _jumpBufferTimer = _jumpBufferTime;
        }

        if (_rawInput.sqrMagnitude > 1f) _rawInput.Normalize();
    }

    private void DetectSurface(float dt)
    {
        // Justo después de saltar ignoramos las superficies para poder despegar.
        if (_jumpGrace > 0f)
        {
            _isSurface = _isGround = _isClimbing = _isGrounded = false;
            _hasSurfacePoint = false;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, 1f - Mathf.Exp(-_alignSpeed * dt)).normalized;
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
                // Descarta caras vistas DESDE ATRAS. Si la normal no apunta hacia el
                // origen del rayo, estamos del lado de adentro del collider; engancharse
                // ahi es lo que mandaba al Gecko a la cara opuesta de la pared al llegar
                // rapido y meterse en la geometria durante el giro.
                if (Vector3.Dot(hit.normal, origin - hit.point) <= 0f) continue;

                float distScore = 1f - hit.distance / _castDistance;
                float downPenalty = Mathf.Clamp01(Vector3.Dot(hit.normal, Vector3.up)) * 0.15f;
                float alignBonus = Vector3.Dot(hit.normal, _currentUp) * 0.15f;
                // superficie contra la que estamos caminando de frente (pared que se
                // quiere trepar): su normal apunta hacia -forward. Le damos prioridad
                // fuerte para que la transición piso -> pared no la gane siempre el piso.
                float intoBonus = Mathf.Clamp01(Vector3.Dot(hit.normal, -transform.forward)) * 0.7f;
                // Histéresis: si este candidato ES (aprox.) la superficie donde ya estábamos
                // parados el frame pasado, le damos un empujón para que gane los empates —
                // evita la "caza" entre dos superficies con puntaje casi igual en una esquina.
                float stickBonus = (_hasSurfacePoint && Vector3.Dot(hit.normal, _surfaceNormal) > 0.97f)
                    ? _surfaceStickiness : 0f;
                float score = distScore - downPenalty + alignBonus + intoBonus + stickBonus;

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
            _hasSurfacePoint = false;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, 1f - Mathf.Exp(-_alignSpeed * dt)).normalized;
            return;
        }

        _surfaceNormal = best.normal;
        _surfacePoint = best.point;
        _hasSurfacePoint = true;
        _isSurface = true;

        int layerBit = 1 << best.collider.gameObject.layer;
        _isGround = (_groundMask.value & layerBit) != 0;
        _isClimbing = !_isGround;

        _currentUp = Vector3.Slerp(_currentUp, _surfaceNormal, 1f - Mathf.Exp(-_alignSpeed * dt)).normalized;

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

        // Suavizado exponencial en vez de Slerp con factor lineal. El factor lineal
        // ademas de depender del framerate hacia que, con _rotationSpeed bajo, el cuerpo
        // tardara SEGUNDOS en alinearse a una pared: por eso costaba tanto trepar.
        float k = 1f - Mathf.Exp(-_rotationSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, k);
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

        // --- velocidad planar con aceleración / desaceleración: el corazón del "feel".
        //     Antes la velocidad se seteaba de golpe (0 -> tope en un frame) y el paso
        //     procedural no llegaba a acompañar el arranque: se veía un tirón y las patas
        //     patinaban. Ahora sube y baja suave y el gait la sigue parejo. ---
        Vector3 targetPlanar = Vector3.ClampMagnitude(wish, 1f) * (_isClimbing ? _climbSpeed : _speed) * _speedMultiplier;
        bool speedingUp = targetPlanar.sqrMagnitude >= _planarVel.sqrMagnitude;
        float accelRate = _isClimbing
            ? (speedingUp ? _climbAcceleration : _climbDeceleration)
            : (speedingUp ? _acceleration : _deceleration);
        _planarVel = Vector3.MoveTowards(_planarVel, targetPlanar, accelRate * dt);

        if (_isClimbing)
        {
            _rb.linearVelocity = _planarVel;
            _rb.AddForce(-up * _stickForce, ForceMode.Acceleration);

            // Se mantiene una separacion fija de la pared. Sin esto, en la transicion
            // piso -> pared el capsule gira, se mete dentro del collider, y la
            // depenetracion de PhysX lo escupe por el otro lado.
            if (_hasSurfacePoint)
            {
                float along = Vector3.Dot(_rb.position - _surfacePoint, up);
                float error = _bodyOffset - along;
                if (Mathf.Abs(error) > 0.002f)
                    _rb.position += up * Mathf.Clamp(error, -0.08f, 0.08f) * 0.35f;
            }
        }
        else if (_isGrounded)
        {
            float downV = Mathf.Min(Vector3.Dot(_rb.linearVelocity, up), 0f);
            _rb.linearVelocity = _planarVel + up * downV;
            _rb.AddForce(-up * (_gravity * 0.5f), ForceMode.Acceleration); // mantiene contacto
        }
        else
        {
            float g = _gravity;
            if (Vector3.Dot(_rb.linearVelocity, Vector3.up) < 0f) g *= _fallMultiplier;

            Vector3 vertical = Vector3.Project(_rb.linearVelocity, Vector3.up);
            Vector3 horizontal = _rb.linearVelocity - vertical;
            horizontal = Vector3.Lerp(horizontal, targetPlanar, _airControl * dt);

            _rb.linearVelocity = horizontal + vertical;
            _rb.AddForce(Vector3.down * g, ForceMode.Acceleration);
            _planarVel = horizontal; // al reaterrizar, el arranque no pega un salto
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

        if (AudioManager.instance != null) AudioManager.instance.Play(SoundNames.PlayerJump);
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
