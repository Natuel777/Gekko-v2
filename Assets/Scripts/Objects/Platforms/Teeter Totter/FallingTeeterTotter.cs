using UnityEngine;

public class FallingTeeterTotter : TeeterTotterPlatform
{
    private Rigidbody _rb;

    [SerializeField] private float _fallingAngle;
    [SerializeField] private float _maxStillTimer;
    [SerializeField] private float _timeTillRespawn = 5f;

    private float _timeStillCounter;
    private float _timeTillRespawnCounter;
    private bool _falling;

    protected override void Start()
    {
        base.Start();
        _rb = _platformMesh.GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _timeStillCounter = _maxStillTimer;
    }
    protected override void FixedUpdate()
    {
        if(_falling)
        {
            _timeTillRespawnCounter-= Time.deltaTime;
            if (_timeTillRespawnCounter <= 0) Respawn();
        }
        base.FixedUpdate();
    }
    protected override void CalculateAngle()
    {
        if (_falling) return;

        base.CalculateAngle();

        if (_currentAngle >= _fallingAngle || _currentAngle <= -_fallingAngle)
        {
            _timeStillCounter-= Time.deltaTime;
            if (_timeStillCounter <= 0) Fall();
        }
        else _timeStillCounter = _maxStillTimer;
    }
    private void Fall()
    {
        _rb.useGravity = true;
        _rb.constraints = RigidbodyConstraints.None;
        _timeTillRespawnCounter = _timeTillRespawn;
        _falling = true;
    }
    private void Respawn()
    {
        _timeStillCounter = _maxStillTimer;
        _falling = false;
        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _rb.linearVelocity = Vector3.zero;
        _platformMesh.localPosition = Vector3.zero;
        _currentAngle = 0;
        _platformMesh.localRotation = Quaternion.identity;
    }
}
