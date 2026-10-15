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

    // Giro durante el grapple y el segundo salto (ver StartLookAtGrapplePoint y StartLookInDirection). Es independiente del
    // giro del swing. Hay dos formas de decir hacia dónde mirar: un PUNTO (el gancho; la dirección se recalcula en cada
    // paso porque Gekko se mueve) o una DIRECCIÓN fija (la de la cámara al saltar).
    private bool _grappleLookActive;
    private bool _grappleLookIsDirection;
    private Vector3 _grappleLookPoint;
    private Vector3 _grappleLookDirection;
    private float _grappleLookSpeed;

    public RotateGekkoWhileSwingin(GekkoSwinging swinging, Rigidbody rb)
    {
        _swinging = swinging;
        _rb = rb;
    }

    // Lo llama GekkoSwinging.StartSwing al engancharse. Fija el "adelante" del swing (ver GetDesiredRotation).
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
        // Durante el grapple ya no hay swing: Gekko gira solo para mirar al punto de enganche (o, en el segundo salto,
        // hacia la dirección en la que salta).
        if(_grappleLookActive)
        {
            RotateTowardsGrappleTarget();
            return;
        }

        if(!_swinging.IsSwinging) return;

        Vector3 toGrapplePoint = _swinging.GrapplePoint - _rb.position;

        // Si estamos encima del punto no hay hacia dónde mirar: mantenemos la rotación actual.
        if(toGrapplePoint.sqrMagnitude <= 0.0001f) return;

        Quaternion desiredRotation = GetDesiredRotation(toGrapplePoint);
        _rb.MoveRotation(Quaternion.Lerp(_rb.rotation, desiredRotation, Time.fixedDeltaTime * _rotationSpeed));
    }

    // Lo llama GekkoGrapple al arrancar el grapple: desde ahora, y hasta StopLookAtGrapplePoint, Gekko gira de a poco para
    // mirar al punto de enganche. degreesPerSecond es la velocidad angular máxima (0 = no gira).
    public void StartLookAtGrapplePoint(Vector3 point, float degreesPerSecond)
    {
        _grappleLookPoint = point;
        _grappleLookIsDirection = false;
        _grappleLookSpeed = Mathf.Max(0f, degreesPerSecond);
        _grappleLookActive = true;
    }

    // Lo llama GekkoGrapple al arrancar el segundo salto: Gekko gira de a poco hacia direction (la dirección en la que
    // mira la cámara, que es hacia donde salta) y la mantiene fija hasta StopLookAtGrapplePoint. Misma rotación
    // progresiva que StartLookAtGrapplePoint; solo cambia a dónde apunta. Si ya había un giro en curso lo reemplaza.
    public void StartLookInDirection(Vector3 direction, float degreesPerSecond)
    {
        // Sin dirección no hay hacia dónde girar: se deja el giro como estaba.
        if(direction.sqrMagnitude <= 0.0001f) return;

        _grappleLookDirection = direction.normalized;
        _grappleLookIsDirection = true;
        _grappleLookSpeed = Mathf.Max(0f, degreesPerSecond);
        _grappleLookActive = true;
    }

    // Lo llama GekkoGrapple al terminar el grapple o el segundo salto. Después PlayerController se despierta y endereza a
    // Gekko solo.
    public void StopLookAtGrapplePoint() => _grappleLookActive = false;

    // A diferencia del swing (que nunca lo deja darse vuelta, ver GetDesiredRotation), acá Gekko mira DIRECTO al punto y sin
    // limitar ejes: giro, cabeceo y, si hace falta, hasta pasar de la vertical. RotateTowards gira a velocidad angular
    // constante, así que el giro es progresivo (no salta) y dura lo que tenga que durar según cuánto haya que girar.
    // El "arriba" de referencia es el del mundo, así Gekko no se inclina de costado. Salvo cuando el punto queda casi recto
    // arriba o abajo: ahí el "arriba" del mundo es paralelo a la mirada y no define el giro, y se usa el "arriba" actual.
    // Corre en FixedUpdate y rota por el Rigidbody, por el mismo motivo que el giro del swing (ver ArtificialFixedUpdate).
    private void RotateTowardsGrappleTarget()
    {
        // Hacia dónde mirar: la dirección fija del segundo salto, o la que va de Gekko al punto de enganche.
        Vector3 toPoint = _grappleLookIsDirection ? _grappleLookDirection : _grappleLookPoint - _rb.position;

        // Encima del punto no hay hacia dónde mirar: se mantiene la rotación actual.
        if(toPoint.sqrMagnitude <= 0.0001f) return;

        Vector3 up = Mathf.Abs(toPoint.normalized.y) > 0.99f ? _rb.rotation * Vector3.up : Vector3.up;
        Quaternion target = Quaternion.LookRotation(toPoint, up);

        _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, target, _grappleLookSpeed * Time.fixedDeltaTime));
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
