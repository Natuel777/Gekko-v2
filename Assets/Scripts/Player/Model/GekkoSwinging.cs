using UnityEngine;

public class GekkoSwinging
{
    private LayerMask _grappableLayers;
    private Transform _camera, _transform, _tongueTip;
    private LineRenderer _lineRenderer;
    private float _maxTongueDistance;
    private Vector3 _grapplePoint = Vector3.zero, _previousHitPosition, _currentGrapplePoint;
    private SpringJoint _joint;
    private Rigidbody _rb;
    private float _forwardThrustForce;
    private float _horizontalThrustForce;
    private float _extendCableSpeed;
    private Vector2 _thrustInput;
    private bool _shortenCablePressed;
    private RaycastHit _predictionHit;
    private float _predictionSphereRadius;
    private Transform _lastHitObject, _cachedGrapplePoint, _predictionPoint;
    private int _quality;
    private float _lastVerticalVelocity = 0f;
    private bool _whooshPlayedThisFall = false;
    [SerializeField] private float _whooshVelocityThreshold = 4.55f;

    #region Properties
    public bool IsSwinging { get; private set; }
    public Vector2 ThrustInput { set { _thrustInput = value; } }
    public bool ShortenCablePressed { set { _shortenCablePressed = value; } }
    #endregion

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam,
                        float forwardThrustForce, float horizontalThrustForce, float extendCableSpeed,
                        Transform predictionPoint, float predictionSphereRadius, float maxTongueDistance)
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
        _lineRenderer.positionCount = 2;
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerSwingAttach);
    }

    public void ArtificialUpdate()
    {
        CheckForSwingPoints();
        
        if(!_joint) return;
        
        ODMGearMovement();
        DrawTongue();
    }

    private void DrawTongue()
    {
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, Time.deltaTime * 8f);
        _lineRenderer.SetPosition(0, _tongueTip.position);
        _lineRenderer.SetPosition(1, _grapplePoint);
    }

    //Requiere muchísima optimización
    public void StopGrapple()
    {
        IsSwinging = false;
        _lineRenderer.positionCount = 0;
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
        float currentVerticalVelocity = _rb.linearVelocity.y;
        //Debug.Log(currentVerticalVelocity);
        if (_lastVerticalVelocity >= 0f && currentVerticalVelocity < 0f)
        {
            _whooshPlayedThisFall = false;
        }
        if (currentVerticalVelocity < -_whooshVelocityThreshold && !_whooshPlayedThisFall)
        {
            AudioManager.instance.Play(SoundNames.PlayerSwing); 
            _whooshPlayedThisFall = true;
        }
        _lastVerticalVelocity = currentVerticalVelocity;
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
