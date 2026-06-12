using UnityEngine;

public class WallMovement : MovementBase
{
    private Vector2 _smoothedInput, _smoothedVelocity;
    private bool _isGrounded = false, _isClimbing = false;
    private float _fallMultiplier = 2.5f, _lowJumpMultiplier = 3f, _smoothInputSpeed = 0.2f;
    private float _speed = 9f, _rotationSpeed = 12f, _jumpForce = 6.25f;
    private Vector3 _currentUp;

    // 🔥 NUEVO: sistema de referencia estable
    private Vector3 _surfaceForward;
    private bool _hasSurfaceBasis = false;

    public WallMovement()
    {
        _cameraTag = "WideShot";
    }

    public override void Initialize(Rigidbody rb, Transform pjTransform, CapsuleCollider collider, Transform camTransform, LayerMask groundLayer, LayerMask climbLayer, Transform head, PlayerViewer pjviewer)
    {
        base.Initialize(rb, pjTransform, collider, camTransform, groundLayer, climbLayer, head, pjviewer);
        _smoothedInput = Vector2.zero;
        _currentUp = Vector3.up;
        _hasSurfaceBasis = false;
    }

    public override void ArtificialUpdate()
    {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _rawInput, ref _smoothedVelocity, _smoothInputSpeed);
    }

    public override void ArtificialFixedUpdate()
    {
        DetectSurface();
        _isGrounded = IsGrounded();

        if(_rawInput.sqrMagnitude > 0.01f)
        {
            if (_target == null) Move(_smoothedInput);
            else MoveLocked();
        }

        if (_isClimbing)
        {
            _rb.AddForce(-_currentUp * 30f, ForceMode.Acceleration);
        }
        else
        {
            float verticalVel = Vector3.Dot(_rb.linearVelocity, Vector3.up);

            if(verticalVel < 0)
                _rb.AddForce(Vector3.up * Physics.gravity.y * (_fallMultiplier - 1), ForceMode.Acceleration);
            else if(verticalVel > 0 && !_jumpPressed)
                _rb.AddForce(Vector3.up * Physics.gravity.y * (_lowJumpMultiplier - 1), ForceMode.Acceleration);
        }
    }

    private void Move(Vector2 input)
    {
        if (_camTransform == null) return;

        // 🔥 Crear basis estable SOLO al entrar en pared
        if(!_hasSurfaceBasis)
        {
            _surfaceForward = Vector3.ProjectOnPlane(_camTransform.forward, _currentUp).normalized;

            // fallback por si queda degenerado
            if(_surfaceForward.sqrMagnitude < 0.01f)
                _surfaceForward = Vector3.ProjectOnPlane(_pjTransform.forward, _currentUp).normalized;

            _hasSurfaceBasis = true;
        }

        // 🔥 Sistema de ejes consistente
        Vector3 forward = _surfaceForward;
        Vector3 right = Vector3.Cross(_currentUp, forward).normalized;

        Vector3 dir = (forward * input.y + right * input.x);

        // Proyectar en el plano de la pared para que no se despegue
        dir = Vector3.ProjectOnPlane(dir, _currentUp).normalized;

        if (dir.sqrMagnitude > 0.01f)
        {
            dir.Normalize();
            Rotate(dir);
        }

        _rb.MovePosition(_rb.position + dir * _speed * Time.fixedDeltaTime);
    }

    private void Rotate(Vector3 dir)
    {
        if(dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir, _currentUp);

            _pjTransform.rotation = Quaternion.Slerp(
                _pjTransform.rotation,
                rot,
                _rotationSpeed * Time.fixedDeltaTime
            );
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
        if (_target == null) return;
        Vector3 dir = (_target.position - _pjTransform.position);
        dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        _pjTransform.rotation = Quaternion.Slerp(_pjTransform.rotation, rot, 10f * Time.deltaTime);
        _head.LookAt(_target.position);
    }

    public override void Jump()
    {
        if(!_isGrounded) return;

        _rb.linearVelocity = Vector3.zero;
        _isClimbing = false;
        _rb.useGravity = true;

        // reset basis al salir
        _hasSurfaceBasis = false;

        _rb.AddForce(_currentUp * _jumpForce, ForceMode.Impulse);
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

        bool g1 = Physics.SphereCast(front, radius, -_currentUp, out _, dist, _groundRayMask);
        bool g2 = Physics.SphereCast(mid, radius, -_currentUp, out _, dist, _groundRayMask);
        bool g3 = Physics.SphereCast(back, radius, -_currentUp, out _, dist, _groundRayMask);

        return g1 || g2 || g3;
    }

    private void DetectSurface()
    {
        Vector3[] directions =
        {
            -_currentUp,
            _pjTransform.forward,
            -_pjTransform.forward,
            _pjTransform.right,
            -_pjTransform.right
        };

        RaycastHit hit;
        RaycastHit bestHit = default;
        bool found = false;
        float bestScore = -999f;

        float half = (_collider.height / 2f) - _collider.radius;
        Vector3 center = _pjTransform.TransformPoint(_collider.center);
        Vector3 front = center + _pjTransform.forward * half;

        foreach(var dir in directions)
        {
            if(Physics.SphereCast(front, _collider.radius * 0.8f, dir, out hit, 0.5f, _climbRayMask))
            {
                float score = Vector3.Dot(hit.normal, -_currentUp);

                if(score > bestScore)
                {
                    bestScore = score;
                    bestHit = hit;
                    found = true;
                }
            }
        }

        if(found)
        {
            _currentUp = Vector3.Slerp(_currentUp, bestHit.normal, 10f * Time.deltaTime);

            if(!_isClimbing)
            {
                // 🔥 reiniciar basis al entrar
                _hasSurfaceBasis = false;
            }

            _isClimbing = true;
            _rb.useGravity = false;
        }
        else
        {
            _isClimbing = false;
            _rb.useGravity = true;
            _currentUp = Vector3.up;

            // 🔥 reset basis al salir
            _hasSurfaceBasis = false;
        }

        // alinear up sin romper rotación forward
        Quaternion upRotation = Quaternion.FromToRotation(_pjTransform.up, _currentUp);
        _pjTransform.rotation = upRotation * _pjTransform.rotation;

        if(_isGrounded)
            _rb.AddForce(-_currentUp * 40f, ForceMode.Acceleration);
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