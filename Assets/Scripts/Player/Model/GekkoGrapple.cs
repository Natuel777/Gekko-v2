using UnityEngine;

public sealed class GekkoGrapple
{
    private readonly float _grapplingCD, _delayTime, _maxDistance;
    private float _grapplingCDTimer;
    private bool _isGrappling = false;
    private GekkoSwinging _swinging;
    private Vector3 _velocityToSet = Vector3.zero;
    private Rigidbody _rb;
    private Transform _transform;

    public GekkoGrapple(GekkoSwinging swinging, Rigidbody rb, Transform t)
    {
        _swinging = swinging;
        _rb = rb;
        _transform = t;
    }

    public void ArtificialUpdate()
    {
        if(_grapplingCDTimer > 0) _grapplingCDTimer -= Time.deltaTime;
    }

    private void StartGrapple(Vector3 grapplePoint)
    {
        if(_grapplingCDTimer > 0) return;

        _isGrappling = true;
        
        if(grapplePoint != Vector3.zero)
        {
            //Dsp agregar un timer manual
            ExecuteGrapple(grapplePoint);
        }

        else StopGrapple();
    }

    private void ExecuteGrapple(Vector3 grapplePoint)
    {
        Vector3 lowestPoint = new Vector3(_transform.position.x,
                                            _transform.position.y - 1,
                                            _transform.position.z);
        float grapplePointRelativeYPos = grapplePoint.y - lowestPoint.y;
    }

    private void StopGrapple()
    {
        _isGrappling = false;
        _grapplingCDTimer = _grapplingCD;
        _swinging.StopGrapple();
    }

    public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravity) 
            + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravity));

        return velocityXZ + velocityY;
    }

    private void SetVelocity() => _rb.linearVelocity = _velocityToSet;
}
