using UnityEngine;

public sealed class GekkoGrapple
{
    private enum Phase { Idle, Freezing, Flying }

    // Metros que se toman por debajo de Gekko como "punto más bajo" para calcular la altura del arco (el "- 1" del repo,
    // que está pensado para un jugador de ~2 m). Se escala con el tamaño de Gekko, igual que _overshootYAxis.
    private const float LowestPointOffset = 1f;

    // Segundos de más, sobre el tiempo de vuelo calculado, que se espera antes de cortar el vuelo si no chocó con nada.
    // El vuelo normal termina antes, al chocar (ver ArtificialOnCollisionEnter) o al llegar al punto (ver
    // ReachedGrapplePoint): esto es solo la red de seguridad que en el repo es el ResetRestrictions a los 3 s, pero
    // ajustada al vuelo real en vez de un valor fijo.
    private const float FlightTimeMargin = 0.5f;

    // Layer SwingAttach, la de los ganchos (Gancho.prefab). Su trigger abre la ventana del segundo salto; no es un peligro,
    // así que GekkoCollision no lo trata como tal (no corta el swing).
    public const int SwingAttachLayer = 16;

    private readonly GekkoSwinging _swinging;
    private readonly Rigidbody _rb;
    private readonly Transform _transform;
    private readonly RotateGekkoWhileSwingin _rotation;
    private readonly float _overshootYAxis, _grapplingCD, _delayTime, _finishDistance, _lookSpeed, _secondJumpMultiplier;

    private Phase _phase = Phase.Idle;
    private float _cooldownTimer, _phaseTimer;
    private Vector3 _grapplePoint;
    private bool _secondJumpEnabled = false;

    // Segundo salto (ver TrySecondJump). _secondJumpActive es true mientras el vuelo en curso es uno de esos saltos.
    // _grappleDistance es lo que midió el último grapple (de donde arrancó al gancho): la base del alcance del salto.
    private bool _secondJumpActive;
    private float _grappleDistance;

    // true desde que arranca la espera hasta que termina el vuelo.
    public bool IsGrappling => _phase != Phase.Idle;
    public bool SecondJumpEnabled => _secondJumpEnabled;

    // La lengua está enganchada al gancho: durante la espera y el vuelo del grapple. El segundo salto la suelta, aunque
    // siga habiendo vuelo (IsGrappling). GekkoSwinging lo usa para saber si tiene que dibujarla.
    public bool IsTongueOut => _phase != Phase.Idle && !_secondJumpActive;

    // El vuelo en curso es el de un segundo salto. A diferencia del grapple, a este vuelo se lo puede interrumpir enganchando
    // un swing nuevo (ver EndSecondJump): ya no hay lengua ni punto que alcanzar.
    public bool IsSecondJumpFlying => _phase == Phase.Flying && _secondJumpActive;

    public GekkoGrapple(GekkoSwinging swinging, Rigidbody rb, Transform t, float overshootYAxis, float grapplingCD, float delayTime,
                        float finishDistance, RotateGekkoWhileSwingin rotation, float lookSpeed, float secondJumpMultiplier)
    {
        _swinging = swinging;
        _rb = rb;
        _transform = t;
        _overshootYAxis = overshootYAxis;
        _grapplingCD = grapplingCD;
        _delayTime = Mathf.Max(0f, delayTime);
        _finishDistance = Mathf.Max(0f, finishDistance);
        _rotation = rotation;
        _lookSpeed = Mathf.Max(0f, lookSpeed);
        _secondJumpMultiplier = Mathf.Max(0f, secondJumpMultiplier);
    }

    // Lo llama GekkoSwinging.ArtificialUpdate en cada frame, haya swing o no: el cooldown y los timers del vuelo tienen
    // que seguir corriendo después de soltar el swing, cuando ya no hay joint.
    public void ArtificialUpdate()
    {
        if(_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if(_phase == Phase.Idle) return;

        // En el segundo salto no hay punto que alcanzar (el destino es solo una referencia en el aire): termina al chocar
        // o por tiempo.
        if(_phase == Phase.Flying && !_secondJumpActive && ReachedGrapplePoint())
        {
            Finish(true);
            return;
        }

        _phaseTimer -= Time.deltaTime;

        if(_phaseTimer > 0f) return;

        if(_phase == Phase.Freezing) Launch();

        else Finish(true);
    }

    // Lo llama GekkoSwinging.ArtificialFixedUpdate. Solo hace algo mientras Gekko espera para salir disparado: lo sostiene
    // quieto (velocidad 0; la gravedad ya la apagó TryStart). Es el "freeze" del repo, que ahí es rb.velocity = zero.
    public void ArtificialFixedUpdate()
    {
        if(_phase != Phase.Freezing) return;

        _rb.linearVelocity = Vector3.zero;
    }

    // Intenta arrancar el grapple hacia grapplePoint. Devuelve false, sin cambiar nada, si todavía hay cooldown, si ya hay
    // un grapple en curso o si la parábola hacia ese punto no se puede calcular. GekkoSwinging solo suelta el swing si
    // esto devuelve true.
    public bool TryStart(Vector3 grapplePoint)
    {
        if(_phase != Phase.Idle || _cooldownTimer > 0f) return false;

        _grapplePoint = grapplePoint;

        // Se calcula ya, aunque con espera la velocidad recién se use en Launch: así, si el punto no tiene una parábola
        // válida, el swing no llega a soltarse.
        if(!TryCalculateLaunch(_grapplePoint, out Vector3 velocity, out float flightTime)) return false;

        // Lo que mide este grapple (de donde arranca hasta el gancho) es la base del alcance del segundo salto. Y como
        // arranca un grapple nuevo, el vuelo deja de ser un segundo salto.
        _grappleDistance = Vector3.Distance(_transform.position, _grapplePoint);
        _secondJumpActive = false;

        // Gekko empieza a girar, de a poco, para mirar al punto: desde ya, o sea también durante la espera. Se corta en
        // Finish.
        _rotation.StartLookAtGrapplePoint(_grapplePoint, _lookSpeed);

        if(_delayTime <= 0f)
        {
            StartFlight(velocity, flightTime);
            return true;
        }

        // Espera: sin velocidad y sin gravedad, así queda clavado en el aire y no se va hundiendo de a poco.
        _phase = Phase.Freezing;
        _phaseTimer = _delayTime;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;
        return true;
    }


    // Segundo salto: un salto igual al del grapple (la misma parábola y el mismo arco), pero hacia donde mira la cámara en
    // vez de hacia el gancho. Solo se puede dar mientras la ventana está abierta (_secondJumpEnabled: desde que Gekko
    // entra al trigger del gancho volando hasta que sale), y se gasta al usarlo.
    //
    // A dónde llega: se apunta a un destino sobre la línea de la cámara, a (distancia del último grapple x
    // _secondJumpMultiplier) de Gekko. Con multiplicador 1 salta tan lejos como saltó el grapple; con 2, el doble; con 0,
    // derecho hacia arriba. La dirección es la de la cámara completa, con cabeceo: con la cámara mirando hacia abajo el
    // destino queda más bajo y el salto aterriza antes; mirando hacia arriba, sube.
    //
    // direction es hacia donde mira la cámara (la pasa GekkoSwinging). Devuelve false, sin cambiar nada, si la ventana
    // está cerrada o si el salto no se puede calcular. Ignora el cooldown: justo se acaba de arrancar, al terminar el
    // grapple que abrió la ventana.
    public bool TrySecondJump(Vector3 direction)
    {
        if(!_secondJumpEnabled) return false;

        // Sin dirección, o sin un grapple previo que haya medido algo, no hay alcance.
        if(direction.sqrMagnitude <= 0.0001f || _grappleDistance <= 0.0001f) return false;

        direction.Normalize();
        Vector3 target = _transform.position + direction * (_grappleDistance * _secondJumpMultiplier);

        if(!TryCalculateLaunch(target, out Vector3 velocity, out float flightTime)) return false;

        // Se gasta: un segundo salto por ventana. El vuelo pasa a ser un segundo salto (sin lengua y sin "llegar al punto").
        _secondJumpEnabled = false;
        _secondJumpActive = true;

        // El destino reemplaza al gancho como punto de la parábola, la lengua se suelta y Gekko gira hacia donde salta.
        _grapplePoint = target;
        _swinging.HideTongue();
        _rotation.StartLookInDirection(direction, _lookSpeed);

        StartFlight(velocity, flightTime);
        return true;
    }

    // Lo llama GekkoSwinging.StartSwing cuando Gekko engancha un swing en pleno segundo salto: el vuelo se corta ahí y el
    // swing toma el control, conservando la velocidad que llevaba. Cuenta como cualquier otro fin de vuelo (arranca el
    // cooldown del grapple). No hace nada si el vuelo no es un segundo salto: al grapple no se lo interrumpe así.
    public void EndSecondJump()
    {
        if(IsSecondJumpFlying) Finish(true);
    }

    public void ArtificialOnCollisionEnter()
    {
        if(_phase == Phase.Flying) Finish(true);
    }

    public void ArtificialOnTriggerEnter(Collider other)
    {
        if(other.gameObject.layer == SwingAttachLayer && _phase == Phase.Flying)
        {
            ParticleSystem ps = other.GetComponentInChildren<ParticleSystem>();
            ps?.Play(); 
            _secondJumpEnabled = true;
        }

        else Debug.Log("Jovani gran Jovani Vasquez");
    }

    public void ArtificialOnTriggerExit(Collider other)
    {
        if(other.gameObject.layer == SwingAttachLayer)
        {
            ParticleSystem ps = other.GetComponentInChildren<ParticleSystem>();
            
            // La ventana del segundo salto se cierra al salir del trigger, haya o no partícula que apagar (antes solo se
            // cerraba si el gancho tenía partícula).
            _secondJumpEnabled = false;

            if(ps != null)
            {
                Debug.Log("Este es Jovaniiiii");
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); 
            } 
        }

        else Debug.Log("Eldeeeeennn riiinggg, sqrrrrrrrrr");
    }

    // Gekko ya está a _finishDistance o menos del punto de enganche. Los metros valen para un Gekko de escala 1 y se
    // escalan con su tamaño real, igual que _overshootYAxis.
    private bool ReachedGrapplePoint()
    {
        float distance = _finishDistance * _transform.lossyScale.y;
        return (_grapplePoint - _transform.position).sqrMagnitude <= distance * distance;
    }

    // Corta el grapple sin cooldown. Lo usa el teletransporte (PlayerController.Teleport): un grapple en curso apunta a un
    // ancla que ahora quedó lejos, y si estaba esperando saldría disparado hacia ella desde el lugar nuevo.
    public void Cancel()
    {
        // La ventana del segundo salto tampoco sobrevive al teletransporte: Gekko queda lejos del gancho.
        _secondJumpEnabled = false;

        if(_phase != Phase.Idle) Finish(false);
    }

    // Fin de la espera: se recalcula la parábola desde donde Gekko quedó y sale disparado.
    private void Launch()
    {
        if(!TryCalculateLaunch(_grapplePoint, out Vector3 velocity, out float flightTime))
        {
            // No debería pasar (TryStart ya lo validó), pero si pasa no se puede quedar clavado: se corta.
            Finish(true);
            return;
        }

        StartFlight(velocity, flightTime);
    }

    // El equivalente a SetVelocity del repo: la velocidad se asigna UNA vez. El Invoke de 0.1 s que usa el repo acá no hace
    // falta: el drag del Rigidbody es 0 y el controller ya está dormido (IsGrappling), así que nada la pisa.
    private void StartFlight(Vector3 velocity, float flightTime)
    {
        _phase = Phase.Flying;
        _phaseTimer = flightTime + FlightTimeMargin;
        _rb.useGravity = true;
        _rb.linearVelocity = velocity;
    }

    // Único lugar donde termina un grapple (por choque, por llegar al punto, por tiempo, por cancelación o por un
    // cálculo inválido).
    private void Finish(bool startCooldown)
    {
        _phase = Phase.Idle;
        _phaseTimer = 0f;
        _secondJumpActive = false;

        if(startCooldown) _cooldownTimer = _grapplingCD;

        // Si cortó durante la espera la gravedad estaba apagada. Si no, ya estaba prendida y esto no cambia nada. En los
        // dos casos PlayerController la vuelve a decidir en su próximo FixedUpdate.
        _rb.useGravity = true;

        // Termina el vuelo, termina la lengua. El controller se despierta solo: ya no hay IsGrappling.
        _swinging.HideTongue();

        // Ya no hay nada que mirar: PlayerController se despierta y endereza a Gekko solo.
        _rotation.StopLookAtGrapplePoint();
    }

    // Arma la parábola. La altura del arco sale igual que en el ExecuteGrapple del repo: se parte de un punto más bajo que
    // Gekko y se llega hasta el punto de enganche más un margen (_overshootYAxis), para que el arco pase por ENCIMA del
    // punto y no lo roce. Los metros valen para un Gekko de escala 1 y se escalan con su tamaño real.
    // Devuelve false si el resultado no sirve (ver el return).
    // target es el punto por el que tiene que pasar la parábola: el gancho (grapple) o el destino del segundo salto.
    private bool TryCalculateLaunch(Vector3 target, out Vector3 velocity, out float flightTime)
    {
        float scale = _transform.lossyScale.y;
        Vector3 start = _transform.position;
        float lowestY = start.y - LowestPointOffset * scale;
        float overshoot = _overshootYAxis * scale;

        float grapplePointRelativeYPos = target.y - lowestY;
        float highestPointOnArc = grapplePointRelativeYPos + overshoot;

        // Si el punto está por debajo del punto más bajo, el arco sube solo el margen.
        if(grapplePointRelativeYPos < 0f) highestPointOnArc = overshoot;

        velocity = CalculateJumpVelocity(start, target, highestPointOnArc, out flightTime);

        // Un NaN o infinito no se le puede asignar a un Rigidbody (Unity tira error y no lo aplica), y un tiempo de vuelo
        // así dejaría el vuelo sin fin. Pasa, por ejemplo, con un _overshootYAxis negativo.
        return IsValid(velocity) && IsValid(flightTime) && flightTime > 0f;
    }

    // Velocidad inicial para ir de startPoint a endPoint en una parábola cuyo punto más alto queda trajectoryHeight por
    // encima de startPoint. Supone que lo único que acelera a Gekko es Physics.gravity.
    // flightTime es el tiempo que tarda en llegar a endPoint: es justo el denominador de velocityXZ, así que sale gratis.
    public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight, out float flightTime)
    {
        float gravity = Physics.gravity.y;
        float displacementY = endPoint.y - startPoint.y;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        // Tiempo en subir hasta el punto más alto + tiempo en bajar desde ahí hasta endPoint.
        flightTime = Mathf.Sqrt(-2f * trajectoryHeight / gravity)
                   + Mathf.Sqrt(2f * (displacementY - trajectoryHeight) / gravity);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2f * gravity * trajectoryHeight);
        Vector3 velocityXZ = displacementXZ / flightTime;

        return velocityXZ + velocityY;
    }

    private static bool IsValid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsValid(Vector3 value) => IsValid(value.x) && IsValid(value.y) && IsValid(value.z);
}
