using UnityEngine;

public class GekkoSwinging
{
    private LayerMask _grappableLayers;
    private Transform _camera, _transform, _tongueTip;
    private LineRenderer _lineRenderer;
    private float _maxTongueDistance;
    private Vector3 _grapplePoint = Vector3.zero;
    private SpringJoint _joint;
    private Rigidbody _rb;
    private float _forwardThrustForce;
    private float _horizontalThrustForce;
    private float _extendCableSpeed;
    private Vector2 _thrustInput;
    private bool _shortenCablePressed;
    private RaycastHit _predictionHit;
    private bool _hasSwingPoint;
    private bool _hadSwingPointLastCheck;
    private float _predictionSphereRadius;
    private Transform _predictionPoint;
    private float _predictionInterval = 0.05f; //~20 chequeos/seg, sólo para el indicador visual
    private float _predictionTimer;
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

        IsSwinging = true;
        _grapplePoint = hit.point;
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
    }

    public void ArtificialLateUpdate()
    {
        CheckForSwingPoints();
        
        if(!_joint) return;
        
        ODMGearMovement();
        DrawTongue();
    }

    private void DrawTongue()
    {
        _lineRenderer.SetPosition(0, _tongueTip.position);
        _lineRenderer.SetPosition(1, _grapplePoint);
    }

    //Requiere muchísima optimización
    public void StopGrapple()
    {
        IsSwinging = false;
        _lineRenderer.positionCount = 0;
        Object.Destroy(_joint);
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
        _predictionTimer -= Time.deltaTime;
        if(_predictionTimer > 0f) return;
        _predictionTimer = _predictionInterval;

        bool foundPoint = TryFindSwingPoint(out RaycastHit hit);

        // Sólo lockea _predictionHit (y de ahí el indicador) al entrar en hit (flanco de
        // subida); mientras el hit se mantiene entre chequeos consecutivos, ninguno de los
        // dos se reasigna, quedan fijos en el primero.
        if(foundPoint && !_hadSwingPointLastCheck)
        {
            _predictionHit = hit;
            if(_predictionPoint != null)
                _predictionPoint.position = hit.point;
        }

        _hasSwingPoint = foundPoint;
        _hadSwingPointLastCheck = foundPoint;
    }

    private bool TryFindSwingPoint(out RaycastHit hit)
    {
        return Physics.SphereCast(_transform.position, _predictionSphereRadius, _transform.forward, out hit, _maxTongueDistance, _grappableLayers);
    }
}
