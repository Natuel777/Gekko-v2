using UnityEngine;
using UnityEngine.Animations.Rigging;

public class Leg : MonoBehaviour
{
    #region Variables
    private ChainIKConstraint _constraint;

    [Header("<color=green>Transforms</color>")]
    [SerializeField] private Transform _body;
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _pointer;
    [SerializeField] private Transform _feetPos;
    [SerializeField] private Transform _pointer2;

    [Header("<color=green>Values</color>")]
    [SerializeField] private bool _isFrontLeg = false;
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _maxDistance = 0.5f;
    [SerializeField] private float _maxHeight = 0.3f;
    private float _currentStepHeight = 0.3f;
    [SerializeField] private float _detectRadius = 0.1f;
    [SerializeField] private float _DetectSurfaceDistance = 2f;
    [SerializeField] private float _wallDetectDistance = 0.5f;
    [SerializeField] private float _stepPrediction = 0.1f;
    [SerializeField] private float _velocitySmoothing = 0.15f;
    [SerializeField] private LayerMask _surface;
    private Vector3 _smoothedVelocity;
    private Vector3 _lastPos;
    private Vector3 _currentPos;
    private Vector3 _targetPos;
    private Vector3 _surfaceNormal = Vector3.up;
    private Vector3 _currentUp = Vector3.up;
    private float _lostSurfaceTime = 0f;
    private float _lostSurfaceGrace = 0.3f;

    private bool _moving = false;
    private float _movingProgress = 0f;
    private bool _wasSurface = false;

    public Vector3 SurfaceNormal => _surfaceNormal;
    public bool Moving { get { return _moving; } }
    public bool IsOnSurface => !_moving && Physics.SphereCast(_pointer.position + _currentUp * _detectRadius, _detectRadius, -_currentUp, out _, _DetectSurfaceDistance, _surface,QueryTriggerInteraction.Ignore);
    public float Weight { get { return _constraint.weight; } set { _constraint.weight = value; } }
    public Vector3 TargetPos => _targetPos;
    #endregion
    private void Start()
    {
        _constraint = GetComponent<ChainIKConstraint>();
        _lastPos = _body.position;
        if (Physics.Raycast(_pointer.position, Vector3.down, out RaycastHit hit, 2f, _surface, QueryTriggerInteraction.Ignore))
            _currentPos = hit.point;

        _target.position = _currentPos;
    }
    private void Update()
    {
        Vector3 rawVelocity = (_body.position - _lastPos) / Time.deltaTime;
        _lastPos = _body.position;
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, rawVelocity,
            _velocitySmoothing / Time.deltaTime * Time.deltaTime);

        bool hasSurface = FindBestSurface(out RaycastHit hit);
        if (!hasSurface)
        {
            _lostSurfaceTime += Time.deltaTime;
            if (_lostSurfaceTime < _lostSurfaceGrace)
            {
                _target.position = _currentPos;
                return;
            }
            _lostSurfaceTime = 0f;
            _target.position = _pointer.position;
            _moving = false;
            _wasSurface = false;
            _surfaceNormal = Vector3.up;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, 5f * Time.deltaTime);
            return;
        }

        _lostSurfaceTime = 0f;
        _surfaceNormal = hit.normal;
        _currentUp = Vector3.Slerp(_currentUp, hit.normal, 8f * Time.deltaTime);

        if (!_wasSurface)
        {
            _targetPos = hit.point;
            _currentPos = _pointer.position;
            float angleDiff = Vector3.Angle(_currentUp, hit.normal);
            _currentStepHeight = angleDiff > 45f ? _maxHeight * 0.2f : _maxHeight;
            _moving = true;
            _movingProgress = 0f;
        }
        _wasSurface = true;

        if (_moving)
        {
            _movingProgress += Time.deltaTime * _speed;
            Vector3 newPos = Vector3.Lerp(_currentPos, _targetPos, _movingProgress);
            newPos += _currentUp * Mathf.Sin(_movingProgress * Mathf.PI) * _currentStepHeight;
            _target.position = newPos;

            if (_movingProgress >= 1f)
            {
                _moving = false;
                _currentPos = _targetPos;
                _target.position = _currentPos;
            }
        }
        else
        {
            _target.position = _currentPos;
        }
    }
    public void TryMove()
    {
        if (_moving) return;

        if (FindBestSurface(out RaycastHit hit))
        {
            Vector3 prediction = _smoothedVelocity * _stepPrediction;
            prediction.y = 0f;
            _targetPos = hit.point + prediction;
            _target.rotation = Quaternion.LookRotation( Vector3.ProjectOnPlane(_body.forward, hit.normal), hit.normal);

            float angleDiff = Vector3.Angle(_currentUp, hit.normal);
            _currentStepHeight = angleDiff > 45f ? _maxHeight * 0.2f : _maxHeight;

            float dist = Vector3.Distance(_currentPos, _targetPos);

            // Si hay un cambio grande de superficie, forzamos el paso aunque esté cerca
            bool surfaceChanged = angleDiff > 30f;

            if (dist > _maxDistance || surfaceChanged)
            {
                _moving = true;
                _movingProgress = 0f;
            }
        }
    }
    private bool FindBestSurface(out RaycastHit bestHit)
    {
        bestHit = default;
        float bestScore = -999f;
        bool found = false;

        Vector3[] directions = {
        -_currentUp,                                        // superficie actual
        Vector3.down,                                       // suelo
        _body.forward,                                      // adelante
        (_body.forward + (-_currentUp)).normalized,         // diagonal adelante-abajo
        (_body.forward - Vector3.down).normalized,          // diagonal adelante-arriba
        //(Vector3.down + _body.forward * 0.5f).normalized,   //directo hacia abajo en world space con más alcance
        (-_currentUp + Vector3.down).normalized,
        };
        float movingForward = Mathf.Clamp01(_smoothedVelocity.magnitude);
        foreach (var dir in directions)
        {
            float castDist = _DetectSurfaceDistance;

            if (_isFrontLeg)
                Debug.DrawRay(_pointer.position + Vector3.up * _detectRadius,
                    dir * castDist, Color.yellow);
            if (Physics.SphereCast(_pointer.position + Vector3.up * _detectRadius,
                _detectRadius, dir, out RaycastHit hit,
                castDist, _surface, QueryTriggerInteraction.Ignore))
            {
                if (_isFrontLeg)
                    Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.green);
                // Score: priorizamos la más cercana y la que va en la dirección de movimiento
                float distScore = 1f - (hit.distance / castDist);
                float currentSurfaceScore = Vector3.Dot(hit.normal, _currentUp) * 0.5f;

                float movementScore = 0f;
                if (_smoothedVelocity.magnitude > 0.1f)
                {
                    Vector3 moveDir = _smoothedVelocity.normalized;
                    movementScore = Vector3.Dot(-hit.normal, moveDir) * movingForward;
                }
                float score = distScore + currentSurfaceScore + movementScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHit = hit;
                    found = true;
                }
            }
        }
        if (_isFrontLeg)
        {
            Vector3 toNextSurface = (Vector3.forward - Vector3.up).normalized;
            Vector3 toLastSurface = (-_currentUp).normalized;
            Debug.DrawRay(_pointer2.position + Vector3.up * _detectRadius,
                    toNextSurface * _DetectSurfaceDistance * 1.5f, Color.yellow);
            if (!Physics.SphereCast(_pointer2.position + _currentUp * _detectRadius,
                _detectRadius, toLastSurface, out RaycastHit hiiit,
                _DetectSurfaceDistance, _surface, QueryTriggerInteraction.Ignore) && !Physics.SphereCast(_pointer.position + _currentUp * _detectRadius,
                _detectRadius, _body.forward, out RaycastHit hiit,
                _DetectSurfaceDistance, _surface, QueryTriggerInteraction.Ignore))
            {
                if (Physics.SphereCast(_pointer2.position + _currentUp * _detectRadius,
                _detectRadius, toNextSurface, out RaycastHit specialHit,
                _DetectSurfaceDistance *1.5f, _surface, QueryTriggerInteraction.Ignore))
                {
                    Debug.DrawRay(specialHit.point, specialHit.normal * 0.5f, Color.red);

                    // Chequeamos que sea realmente una superficie diferente a la actual
                    float angleDiff = Vector3.Angle(specialHit.normal, _currentUp);

                    if (angleDiff > 30f) // es una superficie diferente
                    {
                        float distScore = 1f - (specialHit.distance / (_DetectSurfaceDistance*1.5f));
                        // Score fijo alto + distScore para que siempre gane sobre la superficie actual
                        float score = 2f + distScore;

                            bestHit = specialHit;
                            found = true;
                        if (score > bestScore)
                        {
                        }
                    }
                }
            }
        }

        return found;
    }
    public void TrySettle()
    {
        if (_moving) return;

        if (FindBestSurface(out RaycastHit hit))
        {
            // Sin predicción de velocidad
            _targetPos = hit.point;

            float dist = Vector3.Distance(_currentPos, _targetPos);
            float angleDiff = Vector3.Angle(_currentUp, hit.normal);
            _currentStepHeight = angleDiff > 45f ? _maxHeight * 0.2f : _maxHeight;

            // Umbral más pequeño para que se acomode bien al detenerse
            if (dist > _maxDistance * 0.5f || angleDiff > 30f)
            {
                _moving = true;
                _movingProgress = 0f;
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (_pointer == null) return;

        // Punto ideal en el suelo
        Vector3 groundPos = _pointer.position;
        if (Physics.Raycast(_pointer.position, Vector3.down, out RaycastHit hit, 2f, _surface, QueryTriggerInteraction.Ignore))
            groundPos = hit.point;

        // Círculo de distancia máxima antes de dar un paso
        Gizmos.color = Color.yellow;
        DrawCircle(groundPos, _maxDistance, 32);

        // Altura máxima del arco
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(groundPos, groundPos + Vector3.up * _maxHeight);
        Gizmos.DrawSphere(groundPos + Vector3.up * _maxHeight, 0.01f);


        // Línea desde origen al suelo
        Gizmos.color = Color.green;
        Gizmos.DrawLine(_pointer.position, groundPos);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(_pointer.position, _pointer.position + Vector3.down* _DetectSurfaceDistance);
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angle = 0f;
        Vector3 lastPoint = center + new Vector3(Mathf.Cos(0) * radius, 0, Mathf.Sin(0) * radius);

        for (int i = 1; i <= segments; i++)
        {
            angle = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }
}
