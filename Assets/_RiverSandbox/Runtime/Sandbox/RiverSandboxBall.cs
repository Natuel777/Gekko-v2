using UnityEngine;

// Pelota controlable para probar la interaccion con el agua (solo sandbox).
//   WASD: moverse (relativo a la camara)   Espacio: saltar   R: volver al inicio
[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("Gekko/River/Sandbox/River Sandbox Ball")]
public class RiverSandboxBall : MonoBehaviour
{
    [SerializeField] float _acceleration = 18f;
    [SerializeField] float _waterAcceleration = 9f;
    [SerializeField] float _maxSpeed = 7f;
    [SerializeField] float _jumpSpeed = 7f;
    [SerializeField] float _groundCheckDistance = 0.15f;

    Rigidbody _rb;
    RiverFloater _floater;
    Vector3 _spawn;
    Vector3 _input;
    bool _jumpQueued;
    float _radius = 0.5f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _floater = GetComponent<RiverFloater>();
        _spawn = transform.position;
        if(TryGetComponent(out SphereCollider sphere))
            _radius = sphere.radius * transform.lossyScale.x;
    }

    // Mueve la pelota (y su punto de reinicio) a otra posicion.
    public void Teleport(Vector3 position)
    {
        if(_rb == null)
            _rb = GetComponent<Rigidbody>();
        _spawn = position;
        _rb.position = position;
        transform.position = position;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    void Update()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
        _input = Vector3.ClampMagnitude(forward * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal"), 1f);

        if(Input.GetKeyDown(KeyCode.Space))
            _jumpQueued = true;

        if(Input.GetKeyDown(KeyCode.R) || transform.position.y < -30f)
        {
            _rb.position = _spawn;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    void FixedUpdate()
    {
        bool inWater = _floater != null && _floater.InWater;
        bool grounded = Physics.Raycast(_rb.position, Vector3.down, _radius + _groundCheckDistance, ~0, QueryTriggerInteraction.Ignore);

        Vector3 horizontal = _rb.linearVelocity;
        horizontal.y = 0f;
        if(Vector3.Dot(horizontal, _input) < _maxSpeed)
            _rb.AddForce(_input * (inWater ? _waterAcceleration : _acceleration), ForceMode.Acceleration);

        if(_jumpQueued && (grounded || inWater))
        {
            Vector3 v = _rb.linearVelocity;
            v.y = _jumpSpeed;
            _rb.linearVelocity = v;
        }
        _jumpQueued = false;
    }
}
