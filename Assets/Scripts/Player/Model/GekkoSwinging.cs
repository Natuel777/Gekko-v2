using UnityEngine;

public class GekkoSwinging
{
    private LayerMask _grappableLayers;
    private Transform _camera, _transform, _tongueTip;
    private LineRenderer _lineRenderer;
    private float _maxTongueDistance = 100f;
    private Vector3 _grapplePoint = Vector3.zero;
    private SpringJoint _joint;

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam)
    {    
        _lineRenderer = lineRenderer;
        _tongueTip = tongue;
        _grappableLayers = layers;
        _transform = transform;
        _camera = cam;
    }

    public void StartGrapple()
    {
        RaycastHit hit;

        if(Physics.Raycast(_camera.position, _camera.forward, out hit, _maxTongueDistance, _grappableLayers))
        {
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
    }

    public void ArtificialLateUpdate()
    {
        if(!_joint) return;

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
        _lineRenderer.positionCount = 0;
        Object.Destroy(_joint);
    }
}
