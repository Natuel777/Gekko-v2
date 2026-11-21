using Unity.Cinemachine;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public CinemachineOrbitalFollow _camera;
    public Transform _pj;
    private PlayerController _pjC;
    [SerializeField] private LayerMask _cameraBlockers;
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    private CinemachineInputAxisController _input;
    //public float _autoAlignSpeed = 30f;
    //public float _alignDeadzone = 15f;
    //public float _alignDelay = 0.5f;
    [SerializeField] private float _upThreshold = 0.98f;

    // Paneo vertical (ver UpdateVerticalRange). Grados: 0 = a la altura de Gekko (su transform.position), negativo = por debajo.
    [Tooltip("Cuánto puede bajar la cámara (en grados, por debajo de Gekko) cuando Gekko está en el aire.")]
    [SerializeField] private float _airMinVerticalAngle = -30f;
    [Tooltip("Velocidad (grados/s) a la que sube el límite de abajo al apoyarse, por ejemplo al aterrizar con la cámara por debajo de Gekko.")]
    [SerializeField, Min(0.01f)] private float _verticalRangeSpeed = 90f;
    [Tooltip("Límite de abajo (grados) mientras Gekko nada en la superficie: la cámara queda un poco por encima del agua.")]
    [SerializeField] private float _swimMinVerticalAngle = 10f;
    private float _minVerticalAngle;

    // Mirar hacia arriba desde el piso (lo hace CameraRotation). Mientras se puede, el eje vertical tiene este margen de
    // grados por debajo del piso: así el input puede "empujar" más allá y CameraRotation lo convierte en inclinación.
    // Alcanza con que sea mayor a lo que el mouse puede mover el eje en un frame.
    private const float LookUpInputMargin = 90f;
    private CameraRotation _rotation;

    public float cameraUpLerpSpeed = 2f;
    [SerializeField] private float _angleChange = 120f;
    [SerializeField] private float _newAngle = 90f;
    private Vector3 _cameraUp = Vector3.up;

    private bool _movingCamera;
    public Transform _worldUpReference;

    private LayerMask _obstacle;
    private float _baseRadius;
    public bool MovingCamera { set { _movingCamera = value; } }
    public Vector3 CameraUp { get { return _cameraUp; } }
    // Límite de abajo real de la órbita (grados). Con Gekko en el piso es 0°: la cámara a ras del piso.
    public float VerticalBottom => _minVerticalAngle;
    // Gekko en el piso y la cámara puede mirar hacia arriba desde ahí (ver CameraRotation).
    public bool LookUpAllowed { get; private set; }

    private bool _shakeEnabled = true;

    // Nombres de los ejes tal como aparecen en el inspector del CinemachineInputAxisController de esta cámara.
    [Header("Sensibilidad de cámara (Opciones)")]
    [SerializeField] private string _horizontalAxisName = "Horizontal Axis";
    [SerializeField] private string _verticalAxisName = "Vertical Axis";
    private float _baseHorizontalGain = 1f;
    private float _baseVerticalGain = 1f;
    // La cámara lee el mouse por el Input clásico ("Mouse X"/"Mouse Y"), que usa LegacyGain (no Gain): se escalan los dos.
    private float _baseHorizontalLegacyGain = 1f;
    private float _baseVerticalLegacyGain = 1f;
    private bool _baseGainsCaptured;
    private float _sensitivity = 1f;
    private bool _invertY;

    public float CameraSensitivity => _sensitivity;
    public bool InvertY => _invertY;

    private void OnEnable()  => EventManager.Subscribe<float>("OnCameraShake", ShakeCamera);

    private void ShakeCamera(float force)
    {
        if (!_shakeEnabled) return;
        _impulseSource?.GenerateImpulse(force);
    }

    public bool ShakeEnabled => _shakeEnabled;

    public void SetShakeEnabled(bool value)
    {
        _shakeEnabled = value;
        PlayerPrefs.SetInt(PlayerPrefsKeys.cameraShakeKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetCameraSensitivity(float value)
    {
        _sensitivity = value;
        ApplyAxisGains();
        PlayerPrefs.SetFloat(PlayerPrefsKeys.cameraSensitivityKey, value);
        PlayerPrefs.Save();
    }

    public void SetInvertY(bool value)
    {
        _invertY = value;
        ApplyAxisGains();
        PlayerPrefs.SetInt(PlayerPrefsKeys.invertYKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Multiplica el Gain base de cada eje (tal como está seteado en el inspector) por la sensibilidad guardada, e
    // invierte el signo del eje vertical si corresponde (Gain negativo = input invertido, ver CinemachineInputAxisController.Reader).
    private void ApplyAxisGains()
    {
        if (!_input || !_baseGainsCaptured) return;
        float invert = _invertY ? -1f : 1f;

        var horizontal = FindAxis(_horizontalAxisName, "Look Orbit X");
        if (horizontal != null)
        {
            horizontal.Input.Gain = _baseHorizontalGain * _sensitivity;
            horizontal.Input.LegacyGain = _baseHorizontalLegacyGain * _sensitivity;
        }

        var vertical = FindAxis(_verticalAxisName, "Look Orbit Y");
        if (vertical != null)
        {
            vertical.Input.Gain = _baseVerticalGain * _sensitivity * invert;
            // El LegacyGain vertical ya viene con su signo del inspector (ej. -400): se respeta y solo se invierte si se pide.
            vertical.Input.LegacyGain = _baseVerticalLegacyGain * _sensitivity * invert;
        }
    }

    // Los ejes de la OrbitalFollow se llaman "Look Orbit X/Y" en el CinemachineInputAxisController; el nombre del
    // inspector queda como primera opción por si alguna cámara los tiene renombrados.
    private InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller FindAxis(string name, string fallback)
    {
        if (!_input) return null;
        return _input.GetController(name) ?? _input.GetController(fallback);
    }

    private void Start()
    {
        _shakeEnabled = PlayerPrefs.GetInt(PlayerPrefsKeys.cameraShakeKey, 1) == 1;

        _pj = GameManager.Instance.Pj.transform;
        _obstacle = GameManager.Instance.Surfaces;
        _baseRadius = _camera.Radius;
        _minVerticalAngle = _camera.VerticalAxis.Range.x;
        _input = GetComponent<CinemachineInputAxisController>();
        _rotation = GetComponent<CameraRotation>();

        // Gain base de cada eje tal como quedó configurado en el inspector, antes de aplicarle la sensibilidad guardada.
        var horizontal = FindAxis(_horizontalAxisName, "Look Orbit X");
        var vertical = FindAxis(_verticalAxisName, "Look Orbit Y");
        if (horizontal != null)
        {
            _baseHorizontalGain = horizontal.Input.Gain;
            _baseHorizontalLegacyGain = horizontal.Input.LegacyGain;
        }
        if (vertical != null)
        {
            _baseVerticalGain = Mathf.Abs(vertical.Input.Gain);
            _baseVerticalLegacyGain = vertical.Input.LegacyGain;
        }
        _baseGainsCaptured = true;
        if (horizontal == null || vertical == null)
            Debug.LogWarning($"[CameraFollow] No se encontraron los ejes de cámara para aplicar la sensibilidad (H: {horizontal != null}, V: {vertical != null}).");

        _sensitivity = PlayerPrefs.GetFloat(PlayerPrefsKeys.cameraSensitivityKey, 1f);
        _invertY = PlayerPrefs.GetInt(PlayerPrefsKeys.invertYKey, 0) == 1;
        ApplyAxisGains();

        // El input de órbita ya no depende de eventos (diálogo, PauseEvent, UnPauseEvent): ver SyncOrbitInput.
    }


    void Update()
    {
        // Primero, antes de cualquier otra cosa: si algo de abajo falla, el input igual queda en el estado correcto.
        SyncOrbitInput();

        //bool playerIsMoving = _pjC.IsMoving;
        //
        //if (playerIsMoving)
        //    _timeSincePjMoved = 0f;
        //else
        //    _timeSincePjMoved += Time.deltaTime;

        //if (!_movingCamera && _timeSincePjMoved < _alignDelay)
        //{
        //    AutoAlign();
        //}
        SmoothCameraUp();
    }
    private void LateUpdate()
    {
        PushCameraFromGeometry();
        UpdateVerticalRange();
    }
    private void SmoothCameraUp()
    {
        Vector3 targetUp = _pjC.CurrentUp;

        float angleDiff = Vector3.Angle(Vector3.up, targetUp);
        float angleFromWorld = Vector3.Angle(Vector3.up, targetUp);
        // Solo rotamos si el �ngulo es mayor a X grados
        if (angleDiff > _angleChange) // ajust� este valor
        {
            Vector3 clampedUp = Vector3.Slerp(Vector3.up, targetUp, _newAngle / angleFromWorld);
            _cameraUp = Vector3.Slerp(
                _cameraUp,
                clampedUp,
                cameraUpLerpSpeed * Time.deltaTime
            );
            _worldUpReference.up = _cameraUp;
        }
        else
        {
            _cameraUp = Vector3.Slerp(
                _cameraUp,
                Vector3.up,
                cameraUpLerpSpeed * Time.deltaTime
            );
            _worldUpReference.up = _cameraUp;
        }
    }
    //void AutoAlign()
    //{
    //    // �ngulo actual de la c�mara en Y (world space)
    //    float cameraAngle = _camera.HorizontalAxis.Value;
    //
    //    // �ngulo al que mira el jugador
    //    float playerAngle = _pj.eulerAngles.y;
    //
    //    // Diferencia angular (teniendo en cuenta el wrap de 360�)
    //    float delta = Mathf.DeltaAngle(cameraAngle, playerAngle);
    //
    //    // Solo corregir si supera la deadzone
    //    if (Mathf.Abs(delta) > _alignDeadzone)
    //    {
    //        float correction = Mathf.Sign(delta)
    //            * Mathf.Min(_autoAlignSpeed * Time.deltaTime, Mathf.Abs(delta));
    //
    //        _camera.HorizontalAxis.Value += correction;
    //    }
    //}
    //void PushCameraFromGeometry()
    //{
    //    Vector3 dirToCamera = (_camera.transform.position - _pj.position).normalized;
    //
    //    if (dirToCamera.sqrMagnitude < 0.01f) return;
    //
    //    // Chequeamos si hay geometr�a en la direcci�n actual de la c�mara
    //    if (Physics.SphereCast(_pj.position, 0.3f, dirToCamera,
    //        out RaycastHit hit, _baseRadius, _obstacle))
    //    {
    //        float targetRadius = Mathf.Max(hit.distance - 0.3f, 1f); // m�nimo radio de 1
    //        _camera.Radius = Mathf.Lerp(_camera.Radius, targetRadius, 10f * Time.deltaTime);
    //    }
    //    else
    //    {
    //        // Restauramos hacia el radio base pero m�s lento para no hacer zoom brusco
    //        _camera.Radius = Mathf.Lerp(_camera.Radius, _baseRadius, 2f * Time.deltaTime);
    //    }
    //}
    private void PushCameraFromGeometry()
    {
        Vector3 dirToCamera = (_camera.transform.position - _pj.position).normalized;

        if (Physics.SphereCast(_pj.position, 0.3f, dirToCamera,
            out RaycastHit hit, _baseRadius, _cameraBlockers, QueryTriggerInteraction.Ignore))
        {
            // Forzar la cámara antes del hit
            float safeRadius = Mathf.Max(hit.distance - 0.3f, 1f);
            _camera.Radius = Mathf.Lerp(_camera.Radius, safeRadius, 10f * Time.deltaTime);
        }
        else
        {
            _camera.Radius = Mathf.Lerp(_camera.Radius, _baseRadius, 2f * Time.deltaTime);
        }
    }
    // Límite de abajo del paneo vertical (VerticalAxis de la órbita, en grados; 0 = a la altura de Gekko):
    //  - En el aire (salto, caída, swing, grapple): hasta _airMinVerticalAngle por debajo de Gekko. Lo que frena a la
    //    cámara contra el piso es la colisión (PushCameraFromGeometry y el CinemachineDecollider), no este límite.
    //  - Apoyado en el piso: igual que antes, no baja de 0°.
    //  - Apoyado en una pared o techo: igual que antes, hasta -80°.
    // Bajar el límite es instantáneo (no mueve la cámara). Subirlo es de a poco: Cinemachine recorta el valor del eje al
    // rango en cada frame, y si el límite saltara de golpe (al aterrizar con la cámara por debajo de 0°) la cámara pegaría
    // un salto. Así sube acompañando al límite.
    //
    // Con Gekko en el piso, y ya con el límite asentado en 0°, se habilita mirar hacia arriba (LookUpAllowed): el rango
    // real del eje se abre LookUpInputMargin por debajo del piso y CameraRotation se encarga de que la cámara no baje de
    // ahí (convierte ese extra en inclinación). Solo si CameraRotation está presente y activa: sin ella nadie frenaría
    // a la cámara y bajaría del piso.
    private void UpdateVerticalRange()
    {
        float targetMin;
        bool onFloor = false;

        if (_pjC.Swimming)
        {
            // Nadando en la superficie la cámara no baja del agua (si no, entraría y saldría del efecto bajo el agua todo el
            // tiempo). Buceando sí puede bajar, como en el aire.
            targetMin = _pjC.Diving ? _airMinVerticalAngle : _swimMinVerticalAngle;
        }
        else if (_pjC.InAir)
        {
            targetMin = _airMinVerticalAngle;
        }
        else
        {
            float alignment = Vector3.Dot(_pjC.CurrentUp.normalized, Vector3.up);
            onFloor = alignment >= _upThreshold || _pjC.Grounded;
            targetMin = onFloor ? 0f : -80f;
        }

        if (targetMin < _minVerticalAngle) _minVerticalAngle = targetMin;
        else _minVerticalAngle = Mathf.MoveTowards(_minVerticalAngle, targetMin, _verticalRangeSpeed * Time.deltaTime);

        // Mientras el límite todavía sube (recién aterrizado) no: la cámara tiene que subir con él, no inclinarse.
        LookUpAllowed = onFloor && Mathf.Approximately(_minVerticalAngle, targetMin)
                        && _rotation != null && _rotation.isActiveAndEnabled;

        float rangeMin = LookUpAllowed ? _minVerticalAngle - LookUpInputMargin : _minVerticalAngle;
        _camera.VerticalAxis.Range = new Vector2(rangeMin, 80f);
    }
    // El input de órbita (CinemachineInputAxisController) se DERIVA del estado del juego en cada frame, en vez de prenderse y
    // apagarse con eventos. Antes se apagaba con OnActivatingDialogue / PauseEvent y se prendía con OnDeactivatingDialogue /
    // UnPauseEvent, pero esos avisos se enganchaban una sola vez, en Start, y OnDisable los soltaba: al pausar, ScreenGO apaga
    // todos los Behaviour de la escena (CameraFollow incluido) y después de la primera pausa ya no escuchaba nada. Pausar en
    // medio de un diálogo dejaba la órbita apagada (ScreenGO se acuerda de ese estado y lo restaura al despausar) y nunca
    // llegaba el aviso de que el diálogo terminó: el mouse no volvía a girar la cámara hasta recargar la escena. Con el estado
    // derivado, aunque se pierda cualquier aviso, el frame siguiente lo corrige solo.
    //
    // El mouse solo gira la cámara cuando no hay un diálogo en curso (la cámara del NPC encuadra la escena) ni el menú de pausa
    // abierto (al pausar PlayerInputs cambia Cursor.lockState de Locked a Confined, y ese cambio le mete al mouse delta un
    // salto grande que la cámara tomaba como un giro brusco).
    private void SyncOrbitInput()
    {
        if(_input == null) return;

        bool dialogue = UIManager.Instance != null && UIManager.Instance.HasActiveDialogue();
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        bool allowed = !dialogue && !paused;
        if(_input.enabled == allowed) return;

        _input.enabled = allowed;
        // Solo se escribe al cambiar de estado (pocas veces por partida): si la órbita llega a trabarse en una build, el log
        // del jugador dice cuándo y por qué se apagó o se volvió a prender.
        Debug.Log($"[CameraFollow] Órbita del mouse {(allowed ? "activada" : "desactivada")} (diálogo: {dialogue}, pausa: {paused}).");
    }

    public void SetPJC(PlayerController pjc)
    {
        _pjC = pjc;
    }
    private void OnDisable()
    {
        EventManager.Unsubscribe<float>("OnCameraShake", ShakeCamera);
    }
}
