using UnityEngine;

public class GekkoSwinging
{
    private LayerMask _grappableLayers;
    private LineRenderer _lineRenderer;
    private Vector3 _grapplePoint = Vector3.zero, _previousHitPosition, _currentGrapplePoint;
    private SpringJoint _joint;
    private Spring _spring;
    private Rigidbody _rb;
    private float _forwardThrustForce, _predictionSphereRadius, _horizontalThrustForce,
                _extendCableSpeed, _maxTongueDistance;
    private Vector2 _thrustInput;
    private bool _shortenCablePressed;
    private RaycastHit _predictionHit;
    private Transform _lastHitObject, _cachedGrapplePoint, _predictionPoint,
                    _camera, _transform, _tongueTip;
    
    #region Tongue Visual
    private int _quality;
    private float _springDamper, _springStrength, _springVelocity, _waveCount, _waveHeight;
    private AnimationCurve _waveAffectCurve;
    #endregion
    
    private RotateGekkoWhileSwingin _gekkoRotation;
    
    #region Properties
    public bool IsSwinging { get; private set; }
    public Vector2 ThrustInput { set { _thrustInput = value; } }
    public bool ShortenCablePressed { set { _shortenCablePressed = value; } }
    public Vector3 GrapplePoint => _grapplePoint;
    #endregion

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam,
                        float forwardThrustForce, float horizontalThrustForce, float extendCableSpeed,
                        Transform predictionPoint, float predictionSphereRadius, float maxTongueDistance,
                        int quality, float springDamper, float springStrength, float springVelocity,
                        float waveCount, float waveHeight, AnimationCurve waveAffectCurve)
    {
        _lineRenderer = lineRenderer;
        _tongueTip = tongue;
        _grappableLayers = layers;
        _transform = transform;
        _camera = cam;
        _rb = transform.GetComponent<Rigidbody>();
        _forwardThrustForce = forwardThrustForce;
        _horizontalThrustForce = horizontalThrustForce;
        _extendCableSpeed = extendCableSpeed;
        _predictionPoint = predictionPoint;
        _predictionSphereRadius = predictionSphereRadius;
        _maxTongueDistance = maxTongueDistance;

        // Mathf.Max evita división por cero en el loop de DrawTongue (delta = i / _quality).
        _quality = Mathf.Max(1, quality);
        _springDamper = springDamper;
        _springStrength = springStrength;
        _springVelocity = springVelocity;
        _waveCount = waveCount;
        _waveHeight = waveHeight;
        _waveAffectCurve = waveAffectCurve;
        _spring = new Spring();
        _spring.SetTarget(0);

        _gekkoRotation = new RotateGekkoWhileSwingin(this, _rb);
    }

    public void StartGrapple()
    {
        if(!TryFindSwingPoint(out RaycastHit hit)) return;

        Transform grapplePointTransform = GetGrapplePoint(hit.transform);

        if(grapplePointTransform == null) return;

        IsSwinging = true;
        _grapplePoint = grapplePointTransform.position;
        _joint = _transform.gameObject.AddComponent<SpringJoint>();
        _joint.autoConfigureConnectedAnchor = false;
        _joint.connectedAnchor = _grapplePoint;
        //Optiomizar con el sqrmagnitude
        float distanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint);
        _joint.maxDistance = distanceFromPoint * 0.8f; //Consultar motivo del valor
        _joint.minDistance = distanceFromPoint * 0.25f; //Consultar motivo del valor
        _joint.spring = 4.5f; //Consultar motivo del valor
        _joint.damper = 7f; //Consultar motivo del valor
        _joint.massScale = 4.5f; //Consultar motivo del valor

        // La cuerda "arranca colapsada" en la punta y el spring recibe el empujón inicial del latigazo.
        _currentGrapplePoint = _tongueTip.position;
        _spring.SetVelocity(_springVelocity);
        _lineRenderer.positionCount = _quality + 1;
        DrawTongue(); // primer frame ya coherente, sin depender de si esto corrió antes o después de ArtificialUpdate
    }

    public void ArtificialUpdate()
    {
        CheckForSwingPoints();
        
        if(!_joint) return;
        
        ODMGearMovement();
        DrawTongue();
    }

    public void ArtificialFixedUpdate()
    {
        if(!_joint) return;

        _gekkoRotation.ArtificialFixedUpdate();
    }

    private void DrawTongue()
    {
        _spring.SetDamper(_springDamper);
        _spring.SetStrength(_springStrength);
        _spring.Update(Time.deltaTime);

        Vector3 tongueTipPos = _tongueTip.position;
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, Time.deltaTime * 8f);

        // 'up' sale de _grapplePoint crudo, NUNCA de _currentGrapplePoint: en el primer frame de cada
        // enganche _currentGrapplePoint todavía es igual a tongueTipPos (recién seteado en StartGrapple),
        // lo que forzaría el caso degenerado de LookRotation(Vector3.zero) en cada enganche.
        Vector3 ropeDir = _grapplePoint - tongueTipPos;
        Vector3 up = ropeDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(ropeDir.normalized) * Vector3.up
            : Vector3.up;

        for(int i = 0; i <= _quality; i++)
        {
            float delta = i / (float)_quality;
            Vector3 offset = up * _waveHeight * Mathf.Sin(delta * _waveCount * Mathf.PI) * _spring.Value * _waveAffectCurve.Evaluate(delta);
            _lineRenderer.SetPosition(i, Vector3.Lerp(tongueTipPos, _currentGrapplePoint, delta) + offset);
        }
    }

    //Requiere muchísima optimización
    public void StopGrapple()
    {
        IsSwinging = false;
        _lineRenderer.positionCount = 0;
        _spring.Reset();
        Object.Destroy(_joint);
        // Destroy es diferido (fin de frame): sin esto el guard de ArtificialUpdate sigue viendo el joint
        // vivo ese frame y dibuja la lengua sobre un LineRenderer que ya quedó con 0 posiciones.
        _joint = null;
    }

    private void ODMGearMovement()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized;
        _rb.AddForce(flatRight * _thrustInput.x * _horizontalThrustForce * Time.deltaTime);

        if(_thrustInput.y > 0f)
            _rb.AddForce(flatForward * _thrustInput.y * _forwardThrustForce * Time.deltaTime);

        if(_shortenCablePressed)
        {
            Vector3 directionToPoint = _grapplePoint - _transform.position;
            _rb.AddForce(directionToPoint.normalized * _forwardThrustForce * Time.deltaTime);
            float distanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint);
            _joint.maxDistance = distanceFromPoint * 0.8f;
            _joint.minDistance = distanceFromPoint * 0.25f;
        }

        if(_thrustInput.y < 0f)
        {
            float extendedDistanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint) + _extendCableSpeed;
            _joint.maxDistance = extendedDistanceFromPoint * 0.8f;
            _joint.minDistance = extendedDistanceFromPoint * 0.25f;
        }
    }

    private void CheckForSwingPoints()
    {
        bool foundPoint = TryFindSwingPoint(out RaycastHit hit);

        if(!foundPoint)
        {
            _predictionPoint.gameObject.SetActive(false);
            return;
        }

        Transform hitTransform = GetGrapplePoint(hit.transform);

        if(hitTransform == null)
        {
            _predictionPoint.gameObject.SetActive(false);
            return;
        }

        _predictionPoint.gameObject.SetActive(true);

        if(hitTransform.position != _previousHitPosition)
            _predictionPoint.position = hitTransform.position;

        _previousHitPosition = hitTransform.position;
    }

    // Cachea el hijo "GrapplePoint" del último collider golpeado para no repetir el Find()
    // (búsqueda por string) todos los frames mientras se sigue apuntando al mismo objeto.
    private Transform GetGrapplePoint(Transform hitObject)
    {
        if(hitObject == _lastHitObject) return _cachedGrapplePoint;

        _lastHitObject = hitObject;
        _cachedGrapplePoint = hitObject.Find("GrapplePoint");
        return _cachedGrapplePoint;
    }

    private bool TryFindSwingPoint(out RaycastHit hit)
    {
        return Physics.SphereCast(_camera.position, _predictionSphereRadius, _camera.forward, out hit, _maxTongueDistance, _grappableLayers);
    }
}
