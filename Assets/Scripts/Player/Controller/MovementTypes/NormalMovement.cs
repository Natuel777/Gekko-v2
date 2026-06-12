using UnityEngine;

public class NormalMovement : MovementBase
{
    private Vector2 _smoothedInput, _smoothedVelocity;
    private bool _isGrounded = false;
    private float _fallMultiplier = 2.5f, _lowJumpMultiplier = 3f, _smoothInputSpeed = 0.2f;
    private float _speed = 13f, _rotationSpeed = 25f, _jumpForce = 6f;

    public NormalMovement()
    {
        _cameraTag = "ThirdPersonManual";
    }

    public override void Initialize(Rigidbody rb, Transform pjTransform, CapsuleCollider collider, Transform camTransform, LayerMask groundLayer, LayerMask climbLayer, Transform head, PlayerViewer pjViewer)
    {
        base.Initialize(rb, pjTransform, collider, camTransform, groundLayer, climbLayer, head, pjViewer);
        _smoothedInput = Vector2.zero;
    }

    public override void ArtificialFixedUpdate()
    {
        if(_camTransform == null) return;

        _isGrounded = IsGrounded();

        if(_rawInput.sqrMagnitude > 0.01f)
        {
            if(_target == null) Move();
            else MoveLocked();
        }

        if(_rb.linearVelocity.y < 0)
            _rb.AddForce(Vector3.up * Physics.gravity.y * (_fallMultiplier - 1), ForceMode.Acceleration);
        else if(_rb.linearVelocity.y > 0 && !_jumpPressed)
            _rb.AddForce(Vector3.up * Physics.gravity.y * (_lowJumpMultiplier - 1), ForceMode.Acceleration);
    }

    public override void ArtificialUpdate()
    {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _rawInput, ref _smoothedVelocity, _smoothInputSpeed);
        _pjViewer.Floor(_isGrounded);
    }

    private void Move()
    {
        if (_smoothedInput.x != 0 || _smoothedInput.y != 0)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(_camTransform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(_camTransform.right, Vector3.up).normalized;
            Vector3 direction = (camForward * _smoothedInput.y + camRight * _smoothedInput.x);

            if (direction.sqrMagnitude > 0.01f)
            {
                direction.Normalize();
                Rotate(direction);
            }
            _pjViewer.Move(true);

            _rb.MovePosition(_rb.position + direction * _speed * Time.fixedDeltaTime);
        }
    }
    

    private void MoveLocked()
    {
        Vector3 toTarget = (_target.position - _pjTransform.position);
        toTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toTarget);
        Vector3 moveDir = (toTarget * _smoothedInput.y + right * _smoothedInput.x).normalized;
        _rb.MovePosition(_rb.position + moveDir * _speed * Time.fixedDeltaTime);
        RotateToTarget();
    }

    private void RotateToTarget()
    {
        if(_target == null) return;
        Vector3 dir = (_target.position - _pjTransform.position);
        dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        _pjTransform.rotation = Quaternion.Slerp(_pjTransform.rotation, rot, 10f * Time.deltaTime);
        _head.LookAt(_target.position);
    }

    private void Rotate(Vector3 direction)
    {
        if(direction.sqrMagnitude > 0f)
        {
            Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
            float rotSpeed = _rotationSpeed * Time.deltaTime;
            if (_tongueOut) rotSpeed *= _tongueSlowness;
            _pjTransform.rotation = Quaternion.Slerp(_pjTransform.rotation, rot, rotSpeed);
        }
    }

    public override void Jump()
    {
        if(!_isGrounded) return;

        _rb.linearVelocity = Vector3.zero;
        _rb.useGravity = true;
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _pjViewer.Jump(true);
    }
    private bool IsGrounded()
    {
        float half = (_collider.height / 2f) - _collider.radius;
        Vector3 center = _pjTransform.TransformPoint(_collider.center);
        Vector3 front = center + _pjTransform.forward * half;
        Vector3 back = center - _pjTransform.forward * half;
        Vector3 mid = center;
        float dist = 0.15f;
        float radius = _collider.radius * 0.8f;
        
        bool g1 = Physics.SphereCast(front, radius, -Vector3.up, out _, dist, _groundRayMask);
        bool g2 = Physics.SphereCast(mid, radius, -Vector3.up, out _, dist, _groundRayMask);
        bool g3 = Physics.SphereCast(back, radius, -Vector3.up, out _, dist, _groundRayMask);

        return g1 || g2 || g3;
    }

    public override void TargetAquired(Transform target)
    {
        _target = target;
    }

    public override void TargetLost()
    {
        _target = null;
    }
    public override void ChangeValues(float speed, float jump, float rotSpeed, float fallMulti, float lowmulti)
    {
        _speed = speed;
        _jumpForce = jump;
        _rotationSpeed = rotSpeed;
        _fallMultiplier = fallMulti;
        _lowJumpMultiplier = lowmulti;
    }

}