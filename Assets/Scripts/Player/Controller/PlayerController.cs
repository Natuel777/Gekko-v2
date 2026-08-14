using System;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerController
{
    #region Variables
    private Rigidbody _rb;
    private CapsuleCollider _collider;
    private PlayerViewer _pjViewer;
    private TongueManager _tongueM;
    private Transform _pjTransform;
    private Transform _camTransform;
    private Transform _head;

    private LayerMask _groundRayMask;
    private LayerMask _climbRayMask;
    private LayerMask _surfaces;

    private float _speed = 11f;
    private float _jumpForce = 6f;
    private float _coyoteTime = 0.3f;
    private float _coyoteTimer;
    private float _fallMultiplier = 2.5f;
    private float _lowJumpMultiplier = 3f;
    private float _rotationSpeed = 10f;
    private float _speedMultiplier = 1f;
    
    private bool _isGrounded = false;
    private bool _isSurface = false;
    private bool _wasSurface = false;
    private bool _isClimbing;
    private bool _wasClimbing = false;
    private bool _isMoving = false;
    private bool _nearGround = false;
    private bool _canRotate = true;
    private bool _jumpPressed;
    private bool _canJump;
    private bool _tongueOut;
    private bool _talking;
    private bool _isIcySurface = false;
    private float _icyDriftForce = 200f;
    private bool _isSlipperySurface = false;
    private float _slipperyForce = 0.25f;
    
    private Vector3 _currentUp;
    private Vector2 _rawInput = new(), _smoothedInput = new(), _smoothedVelocity = new();
    private float _smoothInputSpeed = 0.2f;
    private float _tongueSlowness = 0.05f;
    
    private Vector3 _lastValidDir = Vector3.zero;
    private float _jumpGraceTime = 0f;
    private float _jumpGraceDuration = 0.4f;


    public bool JumpPressed { set { _jumpPressed = value; } }
    public bool IsMoving { get { return _isMoving; } }
    public bool TongueOut { get { return _tongueOut; } set { _tongueOut = value; } }
    public bool CanRotate { set { _canRotate = value; } }
    public bool Talking { set { _talking = value; } }
    public Vector3 CurrentUp { get { return _currentUp; } }
    public Vector2 RawInput { get { return _rawInput; } set { _rawInput = value; } }
    public bool CanInteract => !_talking && !TongueOut && _isSurface;
    #endregion

    public PlayerController(Rigidbody rb, Transform pjTransform, CapsuleCollider col, Transform camTransform, PlayerViewer pjV, Transform head)
    {
        _rb = rb;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY| RigidbodyConstraints.FreezeRotationZ;
        _pjTransform = pjTransform;
        _camTransform = camTransform;
        _groundRayMask = GameManager.Instance.GroundLayer;
        _climbRayMask = GameManager.Instance.ClimbLayer;
        _collider = col;
        _currentUp = Vector3.up;
        _head = head;
        _pjViewer = pjV;
        _surfaces = _climbRayMask | _groundRayMask;
        // Arrancamos sin boost: el post proceso de viento empieza apagado.
        WindEffectController.SetActive(false);
    }

    public void Teleport(Vector3 position)
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.position = position;
        _pjTransform.position = position;
    }

    public void ArtificialUpdate()
    {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _rawInput, ref _smoothedVelocity, _smoothInputSpeed);
        _pjViewer.Floor(_isSurface);
    }
    public void ArtificialLateUpdate()
    {
        if (_tongueM != null && _tongueM.IsAttached)
            _tongueM.MoveObject();
    }
    public void ArtificialFixedUpdate()
    {
        if (_jumpGraceTime > 0f)
            _jumpGraceTime -= Time.fixedDeltaTime;

        _wasClimbing = _isClimbing;
        if (_jumpGraceTime <= 0f)
            DetectSurface();

        _isGrounded = IsGrounded();

        _tongueM.CanUseTongue = _isSurface;

        if (_wasClimbing && !_isClimbing)
        {
            _rb.linearVelocity = Vector3.zero;
        }
        if (_isSurface && !_wasSurface)
        {
            _pjViewer.LandingSoundPlay();
        }
        _wasSurface = _isSurface;

        if (_isSurface) _coyoteTimer = _coyoteTime;
        else  _coyoteTimer -= Time.deltaTime;

        if (_coyoteTimer > 0f && _jumpPressed && _canJump && !TongueOut)
        {
            Jump(_jumpForce);
            _coyoteTimer = 0f;
            _pjViewer.JumpSoundPlay();
        }
        if (_isClimbing)
        {
            Vector3 projectedForward = Vector3.ProjectOnPlane(_pjTransform.forward, _currentUp).normalized;
            if (projectedForward.sqrMagnitude > 0.01f)
            {
                float rotDiff = Quaternion.Angle(_pjTransform.rotation,
                    Quaternion.LookRotation(projectedForward, _currentUp));

                if (rotDiff > 1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(projectedForward, _currentUp);
                    _pjTransform.rotation = Quaternion.Slerp(_pjTransform.rotation, targetRot, 8f * Time.deltaTime);
                }
            }
            if (!_nearGround && _jumpGraceTime <= 0f)
            {
                float stickyForce = 20f + _speed * 2f;
                if (_isIcySurface)
                    stickyForce *= 0.05f;
                _rb.AddForce(-_currentUp * stickyForce * 2, ForceMode.Acceleration);
            }
        }
        else
        {
            float verticalSpeed = Vector3.Dot(_rb.linearVelocity, _currentUp);
            if (verticalSpeed < 0)
                _rb.AddForce(-_currentUp * Mathf.Abs(Physics.gravity.y) * (_fallMultiplier - 1), ForceMode.Acceleration);
            else if (verticalSpeed > 0 && !_jumpPressed)
                _rb.AddForce(-_currentUp * Mathf.Abs(Physics.gravity.y) * (_lowJumpMultiplier - 1), ForceMode.Acceleration);

            if (_jumpGraceTime <= 0f)
            {
                Vector3 projectedForward = Vector3.ProjectOnPlane(_pjTransform.forward, _currentUp).normalized;
                if (projectedForward.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(projectedForward, _currentUp);
                    Quaternion newRot = Quaternion.Slerp(_pjTransform.rotation, targetRot, 5f * Time.deltaTime);

                    // Mismo chequeo
                    if (_tongueM != null && _tongueM.IsAttached)
                    {
                        Vector3 newForward = newRot * Vector3.forward;
                        Vector3 desiredObjPos = _tongueM.MouthPos + newForward * (_tongueM.ObjectRadius + 0.5f);
                        LayerMask blockMask = ~(1 << _pjTransform.gameObject.layer) & ~(1 << _tongueM.ObjectLayer);
                        Vector3 halfExtents = _tongueM.ObjectExtents * 0.9f;
                        Vector3 moveDir = desiredObjPos - _tongueM.ObjectPosition;
                        float moveDist = moveDir.magnitude;
                        if (moveDist > 0.001f && Physics.BoxCast(_tongueM.ObjectPosition, halfExtents, moveDir.normalized, out RaycastHit boxHit, newRot, moveDist, blockMask, QueryTriggerInteraction.Ignore))
                        {
                            if (boxHit.distance < moveDist)
                                return; // bloqueado, no rotar
                        }
                    }
                    else
                    {
                        _pjTransform.rotation = newRot;
                    }

                    
                }
            }
        }
        
        if (_rawInput.sqrMagnitude > 0.01f)
        {
            Move(_smoothedInput);
        }

    }


    private void Move(Vector2 input)
    {
        if(_camTransform == null) return;
        Vector3 camForward = Vector3.Cross(_camTransform.right, _currentUp).normalized;
        Vector3 camRight = Vector3.Cross(_currentUp, camForward).normalized;

        Vector3 dir = (camForward * input.y + camRight * input.x).normalized;
        dir = Vector3.ProjectOnPlane(dir, _currentUp).normalized;

        if (dir.sqrMagnitude > 0.0001f)
        {
            dir.Normalize();
            _lastValidDir = dir;
            Rotate(dir);
        }
        else if(_lastValidDir.sqrMagnitude > 0.0001f)
        {
            float drift = _isIcySurface ? 0.1f : 0.3f;
            dir = Vector3.Slerp(_lastValidDir, dir.normalized, drift).normalized;
            Rotate(dir);
        }

        _pjViewer.Move(true);
        _isMoving = true;

        float currentSpeed = (_tongueM != null && _tongueOut ? _speed * 0.8f : _speed) * _speedMultiplier;
        Vector3 targetPos = _rb.position + dir * currentSpeed * Time.fixedDeltaTime;

        LayerMask blockMask = ~(1 << _pjTransform.gameObject.layer);

       
        if (Physics.SphereCast(_rb.position, 0.3f, dir, out RaycastHit hit, currentSpeed * Time.fixedDeltaTime + 0.1f, blockMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(dir, hit.normal).normalized;
            targetPos = _rb.position + slideDir * currentSpeed * Time.fixedDeltaTime;
        }

        if (_isGrounded && !_isClimbing)
        {
            Vector3 toTarget = targetPos - _rb.position;
            toTarget = Vector3.ProjectOnPlane(toTarget, _currentUp);
            targetPos = _rb.position + toTarget;
        }
        if (_tongueM != null && _tongueM.IsAttached)
        {
            Vector3 currentObjPos = _tongueM.ObjectPosition;
            Vector3 desiredObjPos = currentObjPos + (targetPos - _rb.position);
            Vector3 moveDir = (desiredObjPos - currentObjPos);
            float moveDist = moveDir.magnitude;
            LayerMask moveMask = ~(1 << _pjTransform.gameObject.layer) & ~(1 << _tongueM.ObjectLayer);
            Vector3 halfExtents = _tongueM.ObjectExtents * 0.9f;

            if (moveDist > 0.001f)
            {
                if (Physics.BoxCast(currentObjPos, halfExtents, moveDir.normalized,
                    out RaycastHit boxHit, _pjTransform.rotation, moveDist, moveMask, QueryTriggerInteraction.Ignore))
                {
                    // Choca en el camino, proyectar
                    targetPos = _rb.position + Vector3.ProjectOnPlane(targetPos - _rb.position, boxHit.normal);
                }
            }

            // Chequeo de penetración actual (por si ya está adentro)
            Collider[] currentOverlaps = Physics.OverlapBox(
                currentObjPos, halfExtents,
                _pjTransform.rotation, moveMask, QueryTriggerInteraction.Ignore);

            if (currentOverlaps.Length > 0)
            {
                foreach (var col in currentOverlaps)
                {
                    if (Physics.ComputePenetration(
                        _tongueM.HeldCollider, currentObjPos, _pjTransform.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 exitDir, out float exitDist))
                    {
                        if (exitDist > 0.01f)
                        {
                            targetPos = _rb.position + exitDir * exitDist;
                            break;
                        }
                    }
                }
            }
        }
        Vector3 moveVel = (targetPos - _rb.position) / Time.fixedDeltaTime;

        if (_isIcySurface && input.magnitude > 0.01f)
        {
            // Derrape en la dirección del movimiento
            _rb.AddForce(dir.normalized * _icyDriftForce, ForceMode.Acceleration);

            // Derrape lateral al girar
            if (Mathf.Abs(input.x) > 0.01f)
            {
                Vector3 lateralDir = Vector3.Cross(_currentUp, dir.normalized).normalized;
                float lateralForce = _icyDriftForce * Mathf.Abs(input.x) * 0.5f;
                _rb.AddForce(lateralDir * lateralForce * Mathf.Sign(input.x), ForceMode.Acceleration);
            }
        }

        if (!_pjViewer.IsWalkSoundPlaying() && _isSurface) _pjViewer.WalkSoundPlay();
        else if (_pjViewer.IsWalkSoundPlaying() && !_isSurface) _pjViewer.WalkSoundStop();


        if (_isClimbing)
        {
            if (_jumpGraceTime <= 0f)
            {
                _rb.linearVelocity = moveVel;
            }
        }
        else
        {
            if (_isGrounded && !_isClimbing && _jumpGraceTime <= 0)
            {
                // Mantenemos la Y del moveVel para poder subir rampas
                Vector3 horizontalVel = new Vector3(moveVel.x, moveVel.y, moveVel.z);
                float currentY = _rb.linearVelocity.y;
                if (currentY < -0.5f)
                    horizontalVel.y = currentY;
                if (horizontalVel.magnitude > _speed * _speedMultiplier)
                    horizontalVel = horizontalVel.normalized * _speed * _speedMultiplier;
                _rb.linearVelocity = horizontalVel;
            }
            else
            {
                float speed = _speed;
                if (_isSlipperySurface) speed *= _slipperyForce;
                float currentY = _wasClimbing && _jumpGraceTime <= 0 ? 0f : _rb.linearVelocity.y;
                moveVel.y = currentY;
                Vector3 horizontalVel = new Vector3(moveVel.x, 0, moveVel.z);
                if (horizontalVel.magnitude > speed * _speedMultiplier)
                    horizontalVel = horizontalVel.normalized * speed * _speedMultiplier;
                moveVel.x = horizontalVel.x;
                moveVel.z = horizontalVel.z;
                _rb.linearVelocity = moveVel;
            }
        }
    }
    private void Rotate(Vector3 dir)
    {
        if (!_canRotate) return;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 projectedDir = Vector3.ProjectOnPlane(dir, _currentUp).normalized;
            if (projectedDir.sqrMagnitude < 0.01f)
                projectedDir = dir;

            Quaternion rot = Quaternion.LookRotation(projectedDir, _currentUp);
            float rotSpeed = _rotationSpeed * Time.deltaTime;
            if (_tongueOut && !_tongueM.IsAttached) rotSpeed *= _tongueSlowness;

            // Calculá la rotación futura SIN aplicarla todavía
            Quaternion newRot = Quaternion.Slerp(_pjTransform.rotation, rot, rotSpeed);

            if (_tongueM != null && _tongueM.IsAttached)
            {
                Vector3 newForward = newRot * Vector3.forward;
                Vector3 currentObjPos = _tongueM.ObjectPosition;
                Vector3 desiredObjPos = _tongueM.MouthPos + newForward * (_tongueM.ObjectRadius + 0.5f);
                Vector3 moveDir = desiredObjPos - currentObjPos;
                float moveDist = moveDir.magnitude;
                LayerMask blockMask = ~(1 << _pjTransform.gameObject.layer) & ~(1 << _tongueM.ObjectLayer);
                Vector3 halfExtents = _tongueM.ObjectExtents * 0.9f;

                if (moveDist > 0.001f && Physics.BoxCast(
                    currentObjPos, halfExtents, moveDir.normalized,
                    out RaycastHit boxHit, newRot, moveDist, blockMask, QueryTriggerInteraction.Ignore))
                {
                    if (boxHit.distance < moveDist)
                        return; 
                }
            }

             _pjTransform.rotation = newRot;
        }
    }


    public void Jump(float force)
    {
        _isClimbing = false;
        _rb.useGravity = true;
        _jumpGraceTime = _jumpGraceDuration;
        _pjViewer.Jump(true);

        _rb.linearVelocity += _currentUp * force;
        _canJump = false;
    }

    private bool IsGrounded()
    {
        float half = (_collider.height / 2f) - _collider.radius;
        Vector3 center = _pjTransform.TransformPoint(_collider.center);
        Vector3 front = center + _pjTransform.forward * half;
        Vector3 back = center - _pjTransform.forward * half;
        float dist = 0.15f;
        float radius = _collider.radius * 0.9f;

        bool g1 = Physics.SphereCast(front, radius, -_currentUp, out RaycastHit h1, dist, _groundRayMask, QueryTriggerInteraction.Ignore);
        bool g2 = Physics.SphereCast(center, radius, -_currentUp, out RaycastHit h2, dist, _groundRayMask, QueryTriggerInteraction.Ignore);
        bool g3 = Physics.SphereCast(back, radius, -_currentUp, out RaycastHit h3, dist, _groundRayMask, QueryTriggerInteraction.Ignore);

        if (!g1 && !g2 && !g3) return false;

        RaycastHit validHit = g2 ? h2 : (g1 ? h1 : h3);
        float angle = Vector3.Angle(validHit.normal, Vector3.up);
        return angle < 60f;
    }

    private void DetectSurface()
    {
            Vector3[] directions =
            {
            -_currentUp,
            Vector3.down,
            _pjTransform.forward,
            Vector3.right,      
            -Vector3.right,
            Vector3.forward,
            -Vector3.forward,
            };
        RaycastHit bestHit = default;
        bool found = false;
        float bestScore = -999f;

        float half = (_collider.height / 2f) - _collider.radius;
        Vector3 center = _pjTransform.TransformPoint(_collider.center);
        Vector3 front = center + _pjTransform.forward * half;
        Vector3 back = center - _pjTransform.forward * half;

        Vector3[] origins = { front, center, back };
        _nearGround = Physics.Raycast(_pjTransform.position, Vector3.down, 1f, _groundRayMask, QueryTriggerInteraction.Ignore);
        foreach (var origin in origins)
        {
            for (int d = 0; d < directions.Length; d++)
            {
                Vector3 dir = directions[d];
                float castDist = 0.4f;
                if (_isClimbing && !_nearGround)
                {
                    float upDot = Vector3.Dot(dir, -_currentUp);
                    if (upDot < 0.5f)
                    {
                        bool isLongDir = d == 2;
                        castDist = isLongDir ? 2f : 0.6f;
                    }
                }
                if (Physics.SphereCast(origin, _collider.radius * 0.4f, dir, out RaycastHit hit, castDist, _surfaces, QueryTriggerInteraction.Ignore))
                {
                    float normalUpDot = Vector3.Dot(hit.normal, Vector3.up);
                    float groundBonus = 0f;
                    if (_isClimbing && !_nearGround && normalUpDot > 0.7f)
                        groundBonus = 0.5f;
                    float distScore = 1f - (hit.distance / castDist);
                    float farPenalty = castDist > 0.6f ? hit.distance * 0.3f : 0f;
                    float downPenalty = Mathf.Clamp01(normalUpDot);
                    float forwardBonus = Vector3.Dot(hit.normal, -_pjTransform.forward) > 0.5f ? 0.3f : 0f;
                    float score = distScore - downPenalty * 0.3f + groundBonus - farPenalty + forwardBonus;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestHit = hit;
                        found = true;
                    }
                }
            }
        }
        bool nearEdge = false;
        if (_isClimbing)
        {
            nearEdge = !Physics.SphereCast(front, _collider.radius * 0.4f,
                _pjTransform.forward, out _, 0.4f, _surfaces, QueryTriggerInteraction.Ignore);
        }
        if (!_isGrounded && !_nearGround && nearEdge)
        {
            Vector3 headPos = _head.position;

            Vector3 forwardDown = (-_pjTransform.forward - _currentUp).normalized;

            if (Physics.SphereCast(headPos, _collider.radius * 0.4f, forwardDown,
                out RaycastHit hitDown, 4f, _surfaces, QueryTriggerInteraction.Ignore))
            {
                float distScore = 1f - (hitDown.distance / 4f);
                float score = distScore - Mathf.Clamp01(Vector3.Dot(hitDown.normal, Vector3.up)) * 0.3f + 0.4f;

                if (score > bestScore)
                {
                    bestHit = hitDown;
                    found = true;
                }

            }
        }
        

        if (found)
        {
            bool isGround = ((_groundRayMask.value & (1 << bestHit.collider.gameObject.layer)) != 0);
            bool isSurface = ((_surfaces.value & (1 << bestHit.collider.gameObject.layer)) != 0);

            if (_jumpGraceTime > 0f)
            {
                _isClimbing = false;
                _rb.useGravity = true;
                _canJump = false;
                return;
            }

            float angleDiff = Vector3.Angle(_currentUp, bestHit.normal);
            if (angleDiff > 0.5f)
            {
                _currentUp = Vector3.Slerp(_currentUp, bestHit.normal, 5f * Time.deltaTime);
            }

            if (isGround)
            {
                _isClimbing = false;
                _rb.useGravity = true;
            }
            else
            {
                _isClimbing = true;
                _rb.useGravity = false;
            }
            _canJump = true;
            _isSurface = true;
        }
        else
        {
            _isSurface = false;
            _isClimbing = false;
            _rb.useGravity = true;
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, 5f * Time.deltaTime);
            Vector3 verticalVelocity = Vector3.Project(_rb.linearVelocity, Vector3.up);
            _rb.linearVelocity = verticalVelocity;
            if(_coyoteTimer <0f) _canJump = false;
        }
    }

    public void SetIcySurface(bool slippery)
    {
        _isIcySurface = slippery;
    }
    public void SetSlipperySurface(bool slippery)
    {
        _isSlipperySurface = slippery;
    }
    public void HeadLocate()
    {
        _head.localRotation = Quaternion.identity;
    }
    public void ChangeValues(float speed, float jump, float rotSpeed, float fallMulti, float lowmulti)
    {
        _speed = speed;
        _jumpForce = jump;
        _rotationSpeed = rotSpeed;
        _fallMultiplier = fallMulti;
        _lowJumpMultiplier = lowmulti;
    }
    public void CancelJump() { _pjViewer.Jump(false); }
    public void CancelMovement() 
    {
        if (_isSurface && !_isIcySurface && !_isSlipperySurface)
        {
            _rb.linearVelocity = Vector3.zero;
        }
        else
        {
            Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            horizontalVel *= 0.995f;
            _rb.linearVelocity = new Vector3(horizontalVel.x, _rb.linearVelocity.y, horizontalVel.z);
        }
        _pjViewer.Move(false);
        _isMoving = false;
        _lastValidDir = Vector3.zero;
        if (_pjViewer.IsWalkSoundPlaying()) _pjViewer.WalkSoundStop();
        if (_pjViewer.IsTrailPlaying()) _pjViewer.StopTrail();
    }

    public void GetTongueManager(TongueManager tongue) { _tongueM = tongue; }
    public void SetSpeedMultiplier(float speed) => _speedMultiplier = speed;

}
