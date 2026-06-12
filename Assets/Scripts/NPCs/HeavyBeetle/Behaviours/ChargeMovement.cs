using UnityEngine;

public class ChargeMovement
{
    private readonly Rigidbody _rb;
    private readonly Transform _transform;
    private readonly ObstacleAvoidance _oa;
    private Vector3 _chargeDirection;
    private float _distanceTraveled, _chargeMaxDistance, _chargeSpeed;
    public bool ChargeDone => _distanceTraveled >= _chargeMaxDistance;
    public Vector3 ChargeDirection => _chargeDirection;

    public ChargeMovement(Rigidbody rb, Transform transform, float chargeSpeed, float chargeMaxDist, ObstacleAvoidance oa)
    {
        _rb = rb;
        _transform = transform;
        _oa = oa;
        _chargeSpeed = chargeSpeed;
        _chargeMaxDistance = chargeMaxDist;
        _distanceTraveled = 0f;
    }

    public void StartCharge(Vector3 playerPosition)
    {
        _chargeDirection = (playerPosition - _transform.position).normalized;
        _chargeDirection.y = 0f;
        _distanceTraveled = 0f;
    }

    public void ArtificialUpdate()
    {
        float step = _chargeSpeed * Time.deltaTime;
        _rb.MovePosition(_transform.position + _chargeDirection * step);
        _transform.rotation = Quaternion.LookRotation(_chargeDirection);
        _distanceTraveled += step;
    }
}