using Unity.Cinemachine;
using UnityEngine;

// Cámara de tercera persona 100% manejada a mano: posición y rotación se calculan acá cada frame y se escriben
// directo en CameraState.RawPosition/RawOrientation (ver PostPipelineStageCallback). No hay CinemachineOrbitalFollow
// ni CinemachineInputAxisController en el vcam: Cinemachine solo aporta el CinemachineCamera (así
// CameraStateManager puede seguir cambiando entre cámaras para cinemáticas/boss, y el blend sigue andando) y el
// CinemachineImpulseListener (shake), que son independientes de cómo se calculó la posición/rotación.
//
// Por qué: CinemachineOrbitalFollow recalculaba su órbita en coordenadas esféricas relativas a un "world up" que le
// cambiábamos por abajo (_worldUpReference) para acompañar paredes/techos, y ese recálculo no lo controlábamos -
// cualquier ajuste terminaba siendo un parche. Acá el "up" de la cámara (_cameraUp, mismo sistema ya probado) entra
// directo en la fórmula de nuestra propia rotación, sin una capa intermedia reinterpretando ejes.
public class CameraFollow : CinemachineExtension
{
    public Transform _pj;
    private PlayerController _pjC;
    [SerializeField] private LayerMask _cameraBlockers;
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    private CinemachineCamera _vcam;

    [Header("Brazo de la cámara")]
    [Tooltip("Altura sobre la posición de Gekko a la que pivotea el brazo (reemplaza al perfil de 3 anillos de OrbitalFollow; v1 usa un solo valor, no varía con el ángulo).")]
    [SerializeField] private Vector3 _pivotOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Distancia base de la cámara al pivote.")]
    [SerializeField, Min(0.1f)] private float _baseRadius = 3f;
    [Tooltip("Multiplica el radio base mientras Gekko está colgado del swing o en pleno grapple, para que se vea el arco/vuelo entero. La colisión de abajo lo sigue recortando si hay geometría de por medio.")]
    [SerializeField, Min(1f)] private float _swingRadiusMultiplier = 1.5f;
    private float _radius;
    private const float MaxPitch = 80f;
    private float _pitch = 35f;

    [Header("Mirar hacia arriba desde el piso")]
    [Tooltip("Máximo que se inclina la vista hacia arriba (grados) con la cámara a ras del piso.")]
    [SerializeField, Range(0f, 89f)] private float _maxLookUpAngle = 45f;
    [Tooltip("Grados que se inclina la vista por cada grado que el input intenta bajar más allá del piso. 1 = misma sensibilidad que mirar normal.")]
    [SerializeField, Min(0f)] private float _lookUpSensitivity = 1f;
    [Tooltip("Velocidad (grados/s) a la que la vista vuelve a 0 cuando ya no se puede mirar hacia arriba (por ejemplo, al saltar).")]
    [SerializeField, Min(0f)] private float _lookUpReturnSpeed = 120f;
    private float _lookUpTilt;

    [Header("FOV dinámico por velocidad")]
    [Tooltip("Grados que se le suman al FOV base cuando Gekko va a _fovReferenceSpeed o más.")]
    [SerializeField] private float _speedFovBoost = 8f;
    [Tooltip("Velocidad horizontal (m/s) a la que el FOV llega a su boost máximo. Un poco por encima de la velocidad normal de caminata, así se nota más con el boost de arándanos o corriendo a fondo.")]
    [SerializeField, Min(0.01f)] private float _fovReferenceSpeed = 6f;
    [SerializeField, Min(0.01f)] private float _fovLerpSpeed = 3f;
    private float _baseFov;

    [SerializeField] private float _upThreshold = 0.98f;

    // Paneo vertical (ver UpdateVerticalRange). Grados: 0 = a la altura de Gekko, negativo = por debajo.
    [Tooltip("Cuánto puede bajar la cámara (en grados, por debajo de Gekko) cuando Gekko está en el aire.")]
    [SerializeField] private float _airMinVerticalAngle = -30f;
    [Tooltip("Velocidad (grados/s) a la que sube el límite de abajo al apoyarse, por ejemplo al aterrizar con la cámara por debajo de Gekko.")]
    [SerializeField, Min(0.01f)] private float _verticalRangeSpeed = 90f;
    private float _minVerticalAngle;

    public float cameraUpLerpSpeed = 2f;
    [Tooltip("Hasta dónde se acomoda el 'up' de la cámara hacia el de la pared/techo al trepar: 0 = nunca se mueve del horizonte del mundo, 1 = coincide del todo con la superficie. Un valor alto en superficies chicas (donde el up cambia mucho de golpe) se siente muy brusco.")]
    [SerializeField, Range(0f, 1f)] private float _maxCameraUpBlend = 0.45f;
    [Tooltip("Segundos que IsClimbing tiene que estar sostenido antes de que la cámara empiece a inclinarse. En el borde de una plataforma, agarrarse al piso o a la pared puede dudar frame a frame; sin este delay esa duda se le contagia a la cámara.")]
    [SerializeField, Min(0f)] private float _climbTiltDelay = 0.15f;
    private float _climbingDuration;
    private Vector3 _cameraUp = Vector3.up;
    private Vector3 _lastCameraUp = Vector3.up;

    // Reemplaza al "yaw" de OrbitalFollow: en vez de un ángulo contra un eje mundial fijo (que se rompía en
    // techo/pared), es un vector que se transporta frame a frame siguiendo a _cameraUp (ver SmoothCameraUp) antes de
    // sumarle el input — la misma técnica que ya usa PlayerController.GetClimbDirection para _refRight/_refForward.
    private Vector3 _cameraForwardRef = Vector3.forward;

    private bool _movingCamera;
    private Vector2 _lookDelta;
    private bool _inputEnabled = true;

    public bool MovingCamera { set { _movingCamera = value; } }
    public Vector3 CameraUp { get { return _cameraUp; } }
    // Límite de abajo real de la órbita (grados). Con Gekko en el piso es 0°: la cámara a ras del piso.
    public float VerticalBottom => _minVerticalAngle;
    public bool LookUpAllowed { get; private set; }

    private bool _shakeEnabled = true;

    [Header("Sensibilidad de cámara (Opciones)")]
    [Tooltip("Grados de giro por cada unidad de delta de mouse (píxeles), antes de aplicar la sensibilidad.")]
    [SerializeField, Min(0.001f)] private float _lookSpeed = 0.15f;
    private float _sensitivity = 1f;
    private bool _invertY;

    public float CameraSensitivity => _sensitivity;
    public bool InvertY => _invertY;

    // Lo llama PlayerInputs en cada evento de la action "Rotation" (delta de mouse real, ver RotateInput/RotateCancel).
    public Vector2 LookDelta { set { _lookDelta = value; } }

    private Vector3 _finalPosition;
    private Quaternion _finalRotation;

    private void OnEnable() => EventManager.Subscribe<float>("OnCameraShake", ShakeCamera);

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
        PlayerPrefs.SetFloat(PlayerPrefsKeys.cameraSensitivityKey, value);
        PlayerPrefs.Save();
    }

    public void SetInvertY(bool value)
    {
        _invertY = value;
        PlayerPrefs.SetInt(PlayerPrefsKeys.invertYKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void Start()
    {
        _shakeEnabled = PlayerPrefs.GetInt(PlayerPrefsKeys.cameraShakeKey, 1) == 1;

        _pj = GameManager.Instance.Pj.transform;
        _vcam = GetComponent<CinemachineCamera>();
        _baseFov = _vcam.Lens.FieldOfView;
        _radius = _baseRadius;
        _minVerticalAngle = 0f;

        Vector3 initialForward = Vector3.ProjectOnPlane(_pj.forward, Vector3.up);
        _cameraForwardRef = (initialForward.sqrMagnitude > 0.0001f ? initialForward : Vector3.forward).normalized;

        _sensitivity = PlayerPrefs.GetFloat(PlayerPrefsKeys.cameraSensitivityKey, 1f);
        _invertY = PlayerPrefs.GetInt(PlayerPrefsKeys.invertYKey, 0) == 1;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnActivatingDialogue += DeactivateInput;
            UIManager.Instance.OnDeactivatingDialogue += ActivateInput;
        }

        // Al pausar, PlayerInputs cambia Cursor.lockState (Locked -> Confined) para poder clickear el menú: ese
        // cambio le manda al mouse delta un salto grande que la cámara interpretaba como un giro brusco. Se
        // desactiva el input de la cámara acá, antes de que el lockState cambie (PauseEvent dispara primero).
        EventManager.Subscribe("PauseEvent", DeactivateInput);
        EventManager.Subscribe("UnPauseEvent", ActivateInput);
    }

    private void Update()
    {
        SmoothCameraUp();
        ReadLookInput();
    }

    private void LateUpdate()
    {
        UpdateVerticalRange();
        ComputeOrbit();
        UpdateSpeedFov();
    }

    // Suma el delta de mouse de este frame a _cameraForwardRef (yaw) y a _pitch, escalado por sensibilidad/invertY.
    // _lookDelta ya viene en 0 si el mouse no se movió este frame (ver RotateCancel en PlayerInputs), así que no
    // hace falta resetearlo acá.
    private void ReadLookInput()
    {
        if (!_inputEnabled || _lookDelta.sqrMagnitude < 0.0000001f) return;

        float yawDelta = _lookDelta.x * _lookSpeed * _sensitivity;
        float pitchDelta = _lookDelta.y * _lookSpeed * _sensitivity * (_invertY ? -1f : 1f);

        _cameraForwardRef = Quaternion.AngleAxis(yawDelta, _cameraUp) * _cameraForwardRef;
        _pitch += pitchDelta;
    }

    // El "up" de la cámara acompaña a la superficie donde Gekko está parado: en el piso queda como el up del mundo,
    // y se inclina hacia el up de la pared/techo mientras lo trepa. _upThreshold (el mismo que separa piso de pared
    // en UpdateVerticalRange) decide el peso del blend. Solo sigue a la superficie con IsClimbing sostenido
    // (_climbTiltDelay): un roce breve contra una pared en pleno salto no tiene que inclinar la cámara.
    // _maxCameraUpBlend tapa qué tan lejos llega ese acompañamiento (nunca al 100%): en superficies chicas el up
    // real puede cambiar mucho de golpe, y seguirlo del todo se siente muy brusco.
    private void SmoothCameraUp()
    {
        Vector3 targetUp = _pjC.CurrentUp;
        float alignment = Vector3.Dot(targetUp.normalized, Vector3.up);

        _climbingDuration = _pjC.IsClimbing ? _climbingDuration + Time.deltaTime : 0f;
        float climbRamp = Mathf.Clamp01(_climbingDuration / Mathf.Max(0.0001f, _climbTiltDelay));

        float blend = climbRamp > 0f
            ? (1f - Mathf.Clamp01(Mathf.InverseLerp(0f, _upThreshold, alignment))) * _maxCameraUpBlend * climbRamp
            : 0f;

        Vector3 targetCameraUp = TiltUpTowards(targetUp, blend);
        _lastCameraUp = _cameraUp;
        _cameraUp = Vector3.Slerp(_cameraUp, targetCameraUp, cameraUpLerpSpeed * Time.deltaTime);

        // Transporta la referencia de yaw junto con el cambio de "up" de este frame, para que el input de mouse
        // (sumado después en ReadLookInput) siga girando alrededor del up actual y no se desalinee.
        if (Vector3.Angle(_lastCameraUp, _cameraUp) > 0.001f)
        {
            Quaternion delta = Quaternion.FromToRotation(_lastCameraUp, _cameraUp);
            _cameraForwardRef = delta * _cameraForwardRef;
        }
        _cameraForwardRef = Vector3.ProjectOnPlane(_cameraForwardRef, _cameraUp).normalized;
    }

    // Inclina Vector3.up un 'blend' del ángulo total hacia targetUp, pero SIEMPRE alrededor del eje horizontal
    // perpendicular a hacia dónde YA está mirando la cámara (su forward actual, proyectado al piso), nunca con el
    // eje que elegiría Vector3.Slerp(Vector3.up, targetUp, blend) por su cuenta ni con el forward de Gekko:
    // trepando una pared, el forward de Gekko apunta a lo largo de la pared (hacia donde está subiendo), no en una
    // dirección horizontal estable. Usando el forward de la cámara el giro siempre pasa por lo que ya se está
    // viendo en pantalla: se inclina adelante/atrás (pitch) respecto de lo que el jugador ya tiene encuadrado,
    // nunca un giro lateral random.
    private Vector3 TiltUpTowards(Vector3 targetUp, float blend)
    {
        if (blend <= 0f) return Vector3.up;

        Vector3 facing = Vector3.ProjectOnPlane(_cameraForwardRef, Vector3.up);
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
        facing.Normalize();

        Vector3 axis = Vector3.Cross(Vector3.up, facing).normalized;
        float angle = Vector3.Angle(Vector3.up, targetUp) * blend;

        // Las dos inclinaciones posibles alrededor de ese eje (adelante o atrás); nos quedamos con la que de verdad
        // se acerca a targetUp, así no depende de la convención de mano del producto cruz.
        Vector3 tiltForward = Quaternion.AngleAxis(angle, axis) * Vector3.up;
        Vector3 tiltBackward = Quaternion.AngleAxis(-angle, axis) * Vector3.up;
        return Vector3.Dot(tiltForward, targetUp) >= Vector3.Dot(tiltBackward, targetUp) ? tiltForward : tiltBackward;
    }

    // Límite de abajo del paneo vertical (grados; 0 = a la altura de Gekko):
    //  - En el aire (salto, caída, swing, grapple): hasta _airMinVerticalAngle por debajo de Gekko. Lo que frena a
    //    la cámara contra el piso es la colisión (ComputeOrbit), no este límite.
    //  - Apoyado en el piso: no baja de 0°. Apoyado en una pared o techo: hasta -80°.
    // Bajar el límite es instantáneo (no mueve la cámara). Subirlo es de a poco, para no pegar un salto al aterrizar
    // con la cámara por debajo de 0°.
    //
    // Con Gekko en el piso, y el límite ya asentado en 0°, se habilita mirar hacia arriba (LookUpAllowed): seguir
    // empujando el pitch más abajo del piso se convierte en _lookUpTilt en vez de bajar la cámara del piso.
    private void UpdateVerticalRange()
    {
        float targetMin;
        bool onFloor = false;

        if (_pjC.InAir)
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
        LookUpAllowed = onFloor && Mathf.Approximately(_minVerticalAngle, targetMin);

        if (!LookUpAllowed)
        {
            _lookUpTilt = Mathf.MoveTowards(_lookUpTilt, 0f, _lookUpReturnSpeed * Time.deltaTime);
        }
        else if (_pitch < _minVerticalAngle)
        {
            // El input quiso bajar la cámara más allá del piso: eso se vuelve inclinación y la cámara se queda en el piso.
            _lookUpTilt += (_minVerticalAngle - _pitch) * _lookUpSensitivity;
            _pitch = _minVerticalAngle;
        }
        else if (_pitch > _minVerticalAngle && _lookUpTilt > 0f && _lookUpSensitivity > 0f)
        {
            // El input sube la cámara: primero se gasta en deshacer la inclinación, y solo lo que sobra orbita.
            float undo = Mathf.Min(_lookUpTilt, (_pitch - _minVerticalAngle) * _lookUpSensitivity);
            _lookUpTilt -= undo;
            _pitch -= undo / _lookUpSensitivity;
        }
        _lookUpTilt = Mathf.Clamp(_lookUpTilt, 0f, _maxLookUpAngle);

        _pitch = Mathf.Clamp(_pitch, _minVerticalAngle, MaxPitch);
    }

    // Arma la rotación/posición final del brazo y resuelve la colisión contra geometría. Reemplaza tanto a
    // OrbitalFollow (posición) como al Aim stage (rotación) de Cinemachine.
    private void ComputeOrbit()
    {
        Quaternion baseRot = Quaternion.LookRotation(_cameraForwardRef, _cameraUp);
        // Positivo en X mira hacia abajo en Unity, por eso el ángulo total (pitch + lo que sobró como tilt) va con
        // signo negativo para mirar hacia arriba.
        _finalRotation = baseRot * Quaternion.AngleAxis(-(_pitch + _lookUpTilt), Vector3.right);

        Vector3 pivot = _pj.position + _pivotOffset;
        Vector3 camDir = -(_finalRotation * Vector3.forward);

        // Colgado del swing o en pleno grapple el radio objetivo se abre (_swingRadiusMultiplier) para mostrar el
        // arco/vuelo entero; el SphereCast de acá abajo lo sigue recortando si hay geometría de por medio, así el
        // zoom-out nunca atraviesa una pared.
        float desiredRadius = _pjC != null && _pjC.IsSwingingOrGrappling ? _baseRadius * _swingRadiusMultiplier : _baseRadius;

        // El margen (0.4) queda un poco por encima del Near Clip Plane de la cámara (0.3): con los dos iguales el
        // colchón era casi nulo y en ángulos rasantes contra una pared la cámara igual llegaba a clipear.
        const float geometryMargin = 0.4f;
        if (Physics.SphereCast(pivot, geometryMargin, camDir, out RaycastHit hit, desiredRadius, _cameraBlockers, QueryTriggerInteraction.Ignore))
        {
            float safeRadius = Mathf.Max(hit.distance - geometryMargin, 1f);
            _radius = Mathf.Lerp(_radius, safeRadius, 10f * Time.deltaTime);
        }
        else
        {
            _radius = Mathf.Lerp(_radius, desiredRadius, 2f * Time.deltaTime);
        }

        _finalPosition = pivot + camDir * _radius;
    }

    // Abre un poco el FOV con la velocidad horizontal de Gekko (caminar/correr, boost de arándanos), para que se
    // note la velocidad como en Spyro. Nunca lo bajamos por debajo de _baseFov: parado, el FOV queda como siempre.
    private void UpdateSpeedFov()
    {
        float speedT = Mathf.Clamp01(_pjC.HorizontalSpeed / _fovReferenceSpeed);
        float targetFov = _baseFov + _speedFovBoost * speedT;

        LensSettings lens = _vcam.Lens;
        lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, _fovLerpSpeed * Time.deltaTime);
        _vcam.Lens = lens;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Body) state.RawPosition = _finalPosition;
        else if (stage == CinemachineCore.Stage.Aim) state.RawOrientation = _finalRotation;
    }

    private void DeactivateInput() { _inputEnabled = false; _lookDelta = Vector2.zero; }
    private void ActivateInput() { _inputEnabled = true; }

    public void SetPJC(PlayerController pjc)
    {
        _pjC = pjc;
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe<float>("OnCameraShake", ShakeCamera);
        EventManager.Unsubscribe("PauseEvent", DeactivateInput);
        EventManager.Unsubscribe("UnPauseEvent", ActivateInput);
        if (UIManager.Instance == null) return;
        UIManager.Instance.OnActivatingDialogue -= DeactivateInput;
        UIManager.Instance.OnDeactivatingDialogue -= ActivateInput;
    }
}
