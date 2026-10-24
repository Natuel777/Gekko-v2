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

    private void OnEnable()  => EventManager.Subscribe<float>("OnCameraShake", ShakeCamera);
   
    private void ShakeCamera(float force) => _impulseSource?.GenerateImpulse(force);

    private void Start()
    {
        _pj = GameManager.Instance.Pj.transform;
        _obstacle = GameManager.Instance.Surfaces;
        _baseRadius = _camera.Radius;
        _minVerticalAngle = _camera.VerticalAxis.Range.x;
        _input = GetComponent<CinemachineInputAxisController>();
        _rotation = GetComponent<CameraRotation>();

        if(UIManager.Instance != null)
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


    void Update()
    {
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
        LookUpAllowed = onFloor && Mathf.Approximately(_minVerticalAngle, targetMin)
                        && _rotation != null && _rotation.isActiveAndEnabled;

        float rangeMin = LookUpAllowed ? _minVerticalAngle - LookUpInputMargin : _minVerticalAngle;
        _camera.VerticalAxis.Range = new Vector2(rangeMin, 80f);
    }
    private void DeactivateInput() { _input.enabled = false; }
    private void ActivateInput() { _input.enabled = true; }
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
