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

    private float _timeSincePjMoved = 0f;

    public float cameraUpLerpSpeed = 2f;
    [SerializeField] private float _angleChange = 120f;
    [SerializeField] private float _newAngle = 90f;
    private Vector3 _cameraUp = Vector3.up;
    private float timeSincePlayerMoved = 0f;

    private bool _movingCamera;
    public Transform _worldUpReference;

    private LayerMask _obstacle;
    private float _baseRadius;
    public bool MovingCamera { set { _movingCamera = value; } }
    public Vector3 CameraUp { get { return _cameraUp; } }

    private void OnEnable()  => EventManager.Subscribe<float>("OnCameraShake", ShakeCamera);
   
    private void ShakeCamera(float force) => _impulseSource?.GenerateImpulse(force);

    private void Start()
    {
        _pj = GameManager.Instance.Pj.transform;
        _obstacle = GameManager.Instance.Surfaces;
        _baseRadius = _camera.Radius;
        _input = GetComponent<CinemachineInputAxisController>();

        if(UIManager.Instance != null)
        {
            UIManager.Instance.OnActivatingDialogue += DeactivateInput;
            UIManager.Instance.OnDeactivatingDialogue += ActivateInput;
        }
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
    private void DeactivateInput() { _input.enabled = false; }
    private void ActivateInput() { _input.enabled = true; }
    public void SetPJC(PlayerController pjc)
    {
        _pjC = pjc;
    }
    private void OnDisable()
    {
        EventManager.Unsubscribe<float>("OnCameraShake", ShakeCamera);
        if (UIManager.Instance == null) return;
        UIManager.Instance.OnActivatingDialogue -= DeactivateInput;
        UIManager.Instance.OnDeactivatingDialogue -= ActivateInput;
    }
}
