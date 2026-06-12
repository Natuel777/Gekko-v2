using Unity.Cinemachine;
using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    public CinemachineFollow _camera;
    public Transform _pj;
    private PlayerController _pjC;

    public float _autoAlignSpeed = 30f;
    public float _alignDeadzone = 15f;
    public float _alignDelay = 0.5f;

    private float _timeSincePjMoved = 0f;

    public float cameraUpLerpSpeed = 2f;
    public float maxCameraRollSpeed = 30f;
    private Vector3 _cameraUp = Vector3.up;
    private float timeSincePlayerMoved = 0f;

    private bool _movingCamera;
    public Transform _worldUpReference;

    private LayerMask _obstacle;
    //private float _baseRadius;
    public bool MovingCamera { set { _movingCamera = value; } }
    public Vector3 CameraUp { get { return _cameraUp; } }

    private void Awake()
    {
        _pj = GameManager.Instance.Pj.transform;
        _obstacle = GameManager.Instance.Surfaces;
        //_baseRadius = _camera.Radius;
    }


    void Update()
    {
        bool playerIsMoving = _pjC.IsMoving;

        if (playerIsMoving)
            _timeSincePjMoved = 0f;
        else
            _timeSincePjMoved += Time.deltaTime;

        //if (!_movingCamera && _timeSincePjMoved < _alignDelay)
        //{
        //    AutoAlign();
        //}
        //PushCameraFromGeometry();
        SmoothCameraUp();
    }
    public float distance = 5f;
    public float height = 2f;
    public float smoothSpeed = 10f;

    private void LateUpdate()
    {
        // Posición deseada: detrás y arriba del personaje según su orientación
        Vector3 targetPos = _pj.position
            - _pj.forward * distance
            + _pjC.CurrentUp * height;

        // Movemos suave
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);

        // Miramos al personaje
        transform.LookAt(_pj.position + _pjC.CurrentUp * 1f, _pjC.CurrentUp);
    }
    private void SmoothCameraUp()
    {
        Vector3 targetUp = _pjC.CurrentUp; // exponé _currentUp como propiedad

        // Interpolamos el up de la cámara lentamente
        _cameraUp = Vector3.Slerp(
            _cameraUp,
            targetUp,
            cameraUpLerpSpeed * Time.deltaTime
        );
        _worldUpReference.up = _cameraUp;
        
    }
    //void AutoAlign()
    //{
    //    // Ángulo actual de la cámara en Y (world space)
    //    float cameraAngle = _camera.HorizontalAxis.Value;
    //
    //    // Ángulo al que mira el jugador
    //    float playerAngle = _pj.eulerAngles.y;
    //
    //    // Diferencia angular (teniendo en cuenta el wrap de 360°)
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
    //    // Chequeamos si hay geometría en la dirección actual de la cámara
    //    if (Physics.SphereCast(_pj.position, 0.3f, dirToCamera,
    //        out RaycastHit hit, _baseRadius, _obstacle))
    //    {
    //        float targetRadius = Mathf.Max(hit.distance - 0.3f, 1f); // mínimo radio de 1
    //        _camera.Radius = Mathf.Lerp(_camera.Radius, targetRadius, 10f * Time.deltaTime);
    //    }
    //    else
    //    {
    //        // Restauramos hacia el radio base pero más lento para no hacer zoom brusco
    //        _camera.Radius = Mathf.Lerp(_camera.Radius, _baseRadius, 2f * Time.deltaTime);
    //    }
    //}
    public void SetPJC(PlayerController pjc)
    {
        _pjC = pjc;
    }
}
