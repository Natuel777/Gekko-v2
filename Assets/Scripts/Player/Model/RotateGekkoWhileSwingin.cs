using UnityEngine;

public sealed class RotateGekkoWhileSwingin
{
    private float _rotationSpeed = 5f;
    private GekkoSwinging _swinging;
    private Rigidbody _rb;

    public RotateGekkoWhileSwingin(GekkoSwinging swinging, Rigidbody rb)
    {
        _swinging = swinging;
        _rb = rb;
    }

    // Corre en FixedUpdate y rota por el Rigidbody: el del Player usa Interpolate, y escribir su transform desde
    // Update pelea con la interpolación (el Rigidbody avanza a saltos) y hace temblar la cámara que lo sigue.
    public void ArtificialFixedUpdate()
    {
        if(!_swinging.IsSwinging) return;

        Vector3 toGrapplePoint = _swinging.GrapplePoint - _rb.position;

        // LookRotation con vector nulo loguea warning: si estamos encima del punto, mantenemos la rotación actual.
        if(toGrapplePoint.sqrMagnitude <= 0.0001f) return;

        Quaternion desiredRotation = Quaternion.LookRotation(toGrapplePoint);
        _rb.MoveRotation(Quaternion.Lerp(_rb.rotation, desiredRotation, Time.fixedDeltaTime * _rotationSpeed));
    }
}
