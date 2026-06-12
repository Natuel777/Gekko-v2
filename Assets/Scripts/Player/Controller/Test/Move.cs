using UnityEngine;

public class Move : MonoBehaviour
{
    #region Variables
    private TestInputs _pjInputs;
    private Rigidbody _rb;
    [SerializeField] LegsManager _pjLegs;
    [SerializeField] private Transform _camTransform;
    [SerializeField] private Transform _cuello;

    [Header("<color=green>Moving</color>")]
    [SerializeField] private float _speed;
    [SerializeField] private float _rotationSpeed;
    private Vector3 _currentUp;
    private Vector2 _rawInput = new(), _smoothedInput = new(), _smoothedVelocity = new();
    private float _smoothInputSpeed = 0.2f;
    [SerializeField] private float _gravityForce = 20f;
    private float _gravityVelocity;
    private float _gravitySurfaceGrace = 0.2f;
    private float _timeSinceSurface = 0f;

    [Header("<color=green>Jump</color>")]
    [SerializeField] private float _jumpForce = 3f;
    [SerializeField] private float _fallMultiplier = 2.5f;
    [SerializeField] private float _lowJumpMultiplier = 3f;
    [SerializeField] private float _jumpDelayTimer = 0.5f;
    private float _jumpDelayCount;
    private bool _jumpPressed;

    [Header("<color=green>Bools</color>")]
    private bool _isSurface = false;
    private bool _wasSurface = false;

    public bool JumpPressed { set { _jumpPressed = value; } }
    public Vector2 RawInput { get { return _rawInput; } set { _rawInput = value; } }
    #endregion
    private void Start()
    {
        _currentUp = Vector3.up;
        _pjInputs = new TestInputs(this,_pjLegs);
        _rb = GetComponent<Rigidbody>();
        ActivateInputs();
        _rb.useGravity = false;
    }
    private void Update()
    {
        _smoothedInput = Vector2.SmoothDamp(_smoothedInput, _rawInput, ref _smoothedVelocity, _smoothInputSpeed);
    }
    private void FixedUpdate()
    {
        _isSurface = _pjLegs.IsOnSurface;

        if (_isSurface)
        {
            if (_gravityVelocity > 0)
                _gravityVelocity = 0;
            _timeSinceSurface = 0f;
            Vector3 targetUp = _pjLegs.SurfaceNormal;
            if (targetUp != Vector3.zero)
                _currentUp = Vector3.Slerp(_currentUp, targetUp, 10f * Time.fixedDeltaTime);

            if (!_wasSurface)
            {
                if (_jumpPressed) _jumpDelayCount = _jumpDelayTimer;
            }
        }
        else
        {
            _timeSinceSurface += Time.fixedDeltaTime;
                _gravityVelocity = Mathf.Clamp(_gravityVelocity, -50f, 50f);
            _currentUp = Vector3.Slerp(_currentUp, Vector3.up, 5f * Time.fixedDeltaTime);
        }

        _wasSurface = _isSurface;

        if (_jumpPressed && _isSurface)
        {
            _jumpDelayCount -= Time.deltaTime;

            if (_jumpDelayCount <= 0)
            {
                _jumpDelayCount = 0;
                Jump();
            }
        }

        if (_rawInput.sqrMagnitude > 0.01f)
        {
            Moving(_smoothedInput);
        }

        ApplyGravity();
        if (!_isSurface)
        {
            // Caída más rápida
            if (_rb.linearVelocity.y < 0)
                _gravityVelocity += _gravityForce * (_fallMultiplier - 1) * Time.fixedDeltaTime;
            // Salto corto si soltás el botón
            else if (_rb.linearVelocity.y > 0 && !_jumpPressed)
                _gravityVelocity += _gravityForce * (_lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }
    #region Move
    private void Moving(Vector2 input)
    {
        if (_camTransform == null) return;


        Vector3 camForward = Vector3.Cross(_camTransform.right, _currentUp).normalized;
        Vector3 camRight = Vector3.Cross(_currentUp, camForward).normalized;

        Vector3 dir = (camForward * input.y + camRight * input.x).normalized;
        dir = Vector3.ProjectOnPlane(dir, _currentUp).normalized;

        if (dir.sqrMagnitude < 0.01f) return;

        Rotate(dir);

        Vector3 moveVelocity = dir * _speed;
        Vector3 gravityVelocity = Vector3.Project(_rb.linearVelocity, -_currentUp);

        _rb.linearVelocity = moveVelocity + gravityVelocity;
        _pjLegs.IsMoving = true;
    }
    public void CancelMovement()
    {
        if (_isSurface)
        {
            Vector3 vel = _rb.linearVelocity;
            Vector3 verticalVel = Vector3.Project(vel, _currentUp);
            Vector3 horizontalVel = vel - verticalVel;
            horizontalVel = Vector3.Lerp(horizontalVel, Vector3.zero, 50 * Time.fixedDeltaTime);
            _rb.linearVelocity = horizontalVel + verticalVel;
        }
        _pjLegs.IsMoving = false;
    }
    private void Rotate(Vector3 dir)
    {
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 projectedDir = Vector3.ProjectOnPlane(dir, _currentUp).normalized;
            if (projectedDir.sqrMagnitude < 0.01f)
                projectedDir = dir;

            Quaternion rot = Quaternion.LookRotation(projectedDir, _currentUp);
            Quaternion newRot = Quaternion.Slerp(transform.rotation, rot, _rotationSpeed);

            transform.rotation = newRot;
        }
    }
    #endregion
    #region Jump
    public void Jump()
    {
        if (!_isSurface) return;

        _gravityVelocity = -_jumpForce;

        _rb.linearVelocity += _currentUp * _jumpForce;
    }
    public void CancelJump() {  }
    #endregion
    private void ApplyGravity()
    {
        float force = _gravityForce;
        //if (_timeSinceSurface < _gravitySurfaceGrace) force = 10000f;

        if (!_isSurface)
        {
            _gravityVelocity += force * Time.fixedDeltaTime;
            _gravityVelocity = Mathf.Clamp(_gravityVelocity, 0f, 50f);
            _rb.linearVelocity += -Vector3.up * _gravityVelocity * Time.fixedDeltaTime;
        }
        else
        {
            _gravityVelocity = 0f;
            _rb.linearVelocity += -_currentUp * 2f * Time.fixedDeltaTime;
        }
    }
    public void ActivateInputs()
    {
        _pjInputs.ArtificialEnable();
    }
}
