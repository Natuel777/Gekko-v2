using UnityEngine;

// El grapple de Gekko: colgado de la lengua (swing), Space lo suelta y lo dispara en una parábola hacia el punto de
// enganche. Está portado del repo de Dave / GameDevelopment (Grappling.cs + PlayerMovementGrappling.cs). La diferencia es
// que el repo programa todo con Invoke y flags de un MonoBehaviour; acá son timers propios (ArtificialUpdate), porque esto
// es una clase plana que maneja GekkoSwinging.
//
// El grapple tiene dos fases:
//   1) Freezing (solo si _delayTime > 0): Gekko queda clavado en el aire un instante antes de salir disparado. Es el
//      "freeze" del repo. Con _delayTime = 0 se saltea y sale directo.
//   2) Flying: se le setea la velocidad UNA sola vez y después no se la toca más: la parábola la dibuja la gravedad.
//      Termina al primer choque sólido o, si no choca con nada, a los pocos segundos (ver FlightTimeMargin).
//
// Mientras dura (en cualquiera de las dos fases) IsGrappling es true. Eso es lo que hace de "activeGrapple" del repo:
//   - PlayerController no toca el Rigidbody (ver su ArtificialFixedUpdate): sin eso le borraría la velocidad a la parábola.
//   - GekkoSwinging no tiene joint, ni techo de altura, ni empuje: ya no se balancea. Solo sigue dibujando la lengua.
public sealed class GekkoGrapple
{
    private enum Phase { Idle, Freezing, Flying }

    // Metros que se toman por debajo de Gekko como "punto más bajo" para calcular la altura del arco (el "- 1" del repo,
    // que está pensado para un jugador de ~2 m). Se escala con el tamaño de Gekko, igual que _overshootYAxis.
    private const float LowestPointOffset = 1f;

    // Segundos de más, sobre el tiempo de vuelo calculado, que se espera antes de cortar el vuelo si no chocó con nada.
    // El vuelo normal termina antes, al chocar (ver ArtificialOnCollisionEnter): esto es solo la red de seguridad que en
    // el repo es el ResetRestrictions a los 3 s, pero ajustada al vuelo real en vez de un valor fijo.
    private const float FlightTimeMargin = 0.5f;

    private readonly GekkoSwinging _swinging;
    private readonly Rigidbody _rb;
    private readonly Transform _transform;
    private readonly float _overshootYAxis, _grapplingCD, _delayTime;

    private Phase _phase = Phase.Idle;
    private float _cooldownTimer, _phaseTimer;
    private Vector3 _grapplePoint;

    // true desde que arranca la espera hasta que termina el vuelo.
    public bool IsGrappling => _phase != Phase.Idle;

    public GekkoGrapple(GekkoSwinging swinging, Rigidbody rb, Transform t, float overshootYAxis, float grapplingCD, float delayTime)
    {
        _swinging = swinging;
        _rb = rb;
        _transform = t;
        _overshootYAxis = overshootYAxis;
        _grapplingCD = grapplingCD;
        _delayTime = Mathf.Max(0f, delayTime);
    }

    // Lo llama GekkoSwinging.ArtificialUpdate en cada frame, haya swing o no: el cooldown y los timers del vuelo tienen
    // que seguir corriendo después de soltar el swing, cuando ya no hay joint.
    public void ArtificialUpdate()
    {
        if(_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        if(_phase == Phase.Idle) return;

        _phaseTimer -= Time.deltaTime;

        if(_phaseTimer > 0f) return;

        // Se acabó la espera: sale disparado. Se acabó el vuelo sin chocar con nada: se corta igual.
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
        if(!TryCalculateLaunch(out Vector3 velocity, out float flightTime)) return false;

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

    // El primer choque sólido corta el vuelo (el OnCollisionEnter del repo). Solo cuenta en Flying, o sea desde que ya se
    // aplicó la velocidad (el enableMovementOnNextTouch del repo): durante la espera tocar algo no lo cancela.
    public void ArtificialOnCollisionEnter()
    {
        if(_phase == Phase.Flying) Finish(true);
    }

    // Corta el grapple sin cooldown. Lo usa el teletransporte (PlayerController.Teleport): un grapple en curso apunta a un
    // ancla que ahora quedó lejos, y si estaba esperando saldría disparado hacia ella desde el lugar nuevo.
    public void Cancel()
    {
        if(_phase != Phase.Idle) Finish(false);
    }

    // Fin de la espera: se recalcula la parábola desde donde Gekko quedó y sale disparado.
    private void Launch()
    {
        if(!TryCalculateLaunch(out Vector3 velocity, out float flightTime))
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

    // Único lugar donde termina un grapple (por choque, por tiempo, por cancelación o por un cálculo inválido).
    private void Finish(bool startCooldown)
    {
        _phase = Phase.Idle;
        _phaseTimer = 0f;

        if(startCooldown) _cooldownTimer = _grapplingCD;

        // Si cortó durante la espera la gravedad estaba apagada. Si no, ya estaba prendida y esto no cambia nada. En los
        // dos casos PlayerController la vuelve a decidir en su próximo FixedUpdate.
        _rb.useGravity = true;

        // Termina el vuelo, termina la lengua. El controller se despierta solo: ya no hay IsGrappling.
        _swinging.HideTongue();
    }

    // Arma la parábola. La altura del arco sale igual que en el ExecuteGrapple del repo: se parte de un punto más bajo que
    // Gekko y se llega hasta el punto de enganche más un margen (_overshootYAxis), para que el arco pase por ENCIMA del
    // punto y no lo roce. Los metros valen para un Gekko de escala 1 y se escalan con su tamaño real.
    // Devuelve false si el resultado no sirve (ver el return).
    private bool TryCalculateLaunch(out Vector3 velocity, out float flightTime)
    {
        float scale = _transform.lossyScale.y;
        Vector3 start = _transform.position;
        float lowestY = start.y - LowestPointOffset * scale;
        float overshoot = _overshootYAxis * scale;

        float grapplePointRelativeYPos = _grapplePoint.y - lowestY;
        float highestPointOnArc = grapplePointRelativeYPos + overshoot;

        // Si el punto está por debajo del punto más bajo, el arco sube solo el margen.
        if(grapplePointRelativeYPos < 0f) highestPointOnArc = overshoot;

        velocity = CalculateJumpVelocity(start, _grapplePoint, highestPointOnArc, out flightTime);

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
