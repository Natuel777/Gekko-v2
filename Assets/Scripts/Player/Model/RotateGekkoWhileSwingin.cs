using UnityEngine;

public sealed class RotateGekkoWhileSwingin
{
    private float _rotationSpeed = 5f;

    // Distancia horizontal al punto de enganche (en metros) por debajo de la cual Gekko deja de seguir la dirección del
    // punto y se va alineando con el rumbo del swing. Cerca de la vertical del punto la dirección horizontal es casi
    // aleatoria (moverse unos centímetros la cambia por completo), y seguirla haría girar a Gekko de golpe.
    private float _followMinDistance = 2f;

    private GekkoSwinging _swinging;
    private Rigidbody _rb;

    // El "adelante" del swing: dirección horizontal (unitaria) hacia donde quedaba el punto de enganche al engancharse.
    private Vector3 _heading = Vector3.forward;

    public RotateGekkoWhileSwingin(GekkoSwinging swinging, Rigidbody rb)
    {
        _swinging = swinging;
        _rb = rb;
    }

    // Lo llama GekkoSwinging.StartGrapple al engancharse. Fija el "adelante" del swing (ver GetDesiredRotation).
    public void StartSwing()
    {
        Vector3 toGrapplePoint = _swinging.GrapplePoint - _rb.position;
        Vector3 flat = new Vector3(toGrapplePoint.x, 0f, toGrapplePoint.z);

        // Si se engancha justo debajo del punto no hay dirección horizontal hacia él: se usa hacia donde mira Gekko.
        if(flat.sqrMagnitude <= 0.0001f)
            flat = Vector3.ProjectOnPlane(_rb.rotation * Vector3.forward, Vector3.up);

        _heading = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
    }

    // Corre en FixedUpdate y rota por el Rigidbody: el del Player usa Interpolate, y escribir su transform desde
    // Update pelea con la interpolación (el Rigidbody avanza a saltos) y hace temblar la cámara que lo sigue.
    public void ArtificialFixedUpdate()
    {
        if(!_swinging.IsSwinging) return;

        Vector3 toGrapplePoint = _swinging.GrapplePoint - _rb.position;

        // Si estamos encima del punto no hay hacia dónde mirar: mantenemos la rotación actual.
        if(toGrapplePoint.sqrMagnitude <= 0.0001f) return;

        Quaternion desiredRotation = GetDesiredRotation(toGrapplePoint);
        _rb.MoveRotation(Quaternion.Lerp(_rb.rotation, desiredRotation, Time.fixedDeltaTime * _rotationSpeed));
    }

    // Gekko mira hacia el punto de enganche, pero SIN darse vuelta: nunca mira a más de 90° del rumbo del swing.
    // Antes miraba siempre directo al punto (LookRotation). Al pasar por debajo, el punto pasaba de estar adelante a estar
    // atrás y Gekko pegaba un giro de 180° para seguir mirándolo.
    //
    // Cómo se evita, con la dirección al punto separada en horizontal (giro) y altura (cabeceo):
    //  1) Giro: si la parte horizontal apunta hacia atrás (respecto de _heading), se refleja hacia adelante. Es continuo:
    //     en el borde (punto justo al costado) no cambia nada, y con el punto justo atrás queda mirando al rumbo. Por
    //     eso no hay saltos al pasar el punto: Gekko sigue mirando hacia adelante.
    //  2) Cerca de la vertical del punto (ver _followMinDistance) el giro se mezcla con el rumbo, porque ahí la dirección
    //     horizontal al punto es ruido.
    //  3) Cabeceo: sigue mirando hacia arriba, al ángulo de elevación del punto, igual que antes.
    // La rotación se arma con giro y cabeceo por separado y no con LookRotation(dirección al punto): así no hay
    // singularidad cuando el punto queda justo arriba, y Gekko nunca se inclina de costado (sin giro en su eje Z).
    private Quaternion GetDesiredRotation(Vector3 toGrapplePoint)
    {
        Vector3 flat = new Vector3(toGrapplePoint.x, 0f, toGrapplePoint.z);

        float along = Vector3.Dot(flat, _heading);
        if(along < 0f)
            flat -= 2f * along * _heading;

        float flatDistance = flat.magnitude;

        Vector3 yawDirection = _heading;
        if(flatDistance > 0.0001f)
            yawDirection = Vector3.Lerp(_heading, flat / flatDistance, Mathf.Clamp01(flatDistance / _followMinDistance));

        float pitch = Mathf.Atan2(toGrapplePoint.y, flatDistance) * Mathf.Rad2Deg;

        // En Unity un ángulo positivo en X mira hacia abajo, por eso el cabeceo hacia arriba va con signo negativo.
        return Quaternion.LookRotation(yawDirection, Vector3.up) * Quaternion.AngleAxis(-pitch, Vector3.right);
    }
}
