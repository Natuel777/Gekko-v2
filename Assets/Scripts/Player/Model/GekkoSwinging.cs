using UnityEngine;

public class GekkoSwinging
{
    private LayerMask _grappableLayers;
    private LineRenderer _lineRenderer;
    private Vector3 _grapplePoint = Vector3.zero, _previousHitPosition, _currentGrapplePoint;
    private SpringJoint _joint;
    private Spring _spring;
    private Rigidbody _rb;
    private float _forwardThrustForce, _predictionSphereRadius, _horizontalThrustForce,
                _extendCableSpeed, _maxTongueDistance;
    private Vector2 _thrustInput;
    private bool _shortenCablePressed;
    private RaycastHit _predictionHit;
    private Transform _lastHitObject, _cachedGrapplePoint, _predictionPoint,
                    _camera, _transform, _tongueTip;
    
    #region Tongue Visual
    // Cómo se dibuja la lengua: dos efectos que se SUMAN.
    //   1) LA SOGA FÍSICA (RopeSimulation): decide la forma base. Cuelga por su peso, se queda atrás cuando Gekko se
    //      mueve y rebota al frenar. La explicación completa está arriba de esa clase.
    //   2) EL LATIGAZO (Spring + onda senoidal): una ondulación "artística" que aparece al enganchar y se apaga sola.
    // La física manda y el latigazo se le suma por encima recién al dibujar (ver UpdateLineRenderer).

    // Cantidad de hilos de la soga. La simulación y el LineRenderer tienen _quality + 1 puntos.
    private int _quality;

    // Parámetros del latigazo (llegan desde el Inspector de Player):
    //   _springStrength : qué tan rápido oscila.       _springDamper : qué tan rápido se apaga.
    //   _springVelocity : empujón inicial al enganchar.  _waveCount    : cantidad de crestas de la onda.
    //   _waveHeight     : altura máxima de la onda.      _waveAffectCurve : cuánto se nota la onda en cada punto de la
    //                     soga (tiene que valer 0 en las dos puntas para que la lengua y el ancla no se despeguen).
    private float _springDamper, _springStrength, _springVelocity, _waveCount, _waveHeight;
    private AnimationCurve _waveAffectCurve;

    // La simulación de la soga (ver RopeSimulation.cs).
    private RopeSimulation _rope;

    // Lista de puntos que se le entrega al LineRenderer (puntos de la soga + latigazo). Se crea una sola vez y se
    // reutiliza en cada frame, así dibujar no genera basura (GC).
    private Vector3[] _ropePoints;

    // Soga de sobra respecto de la distancia real entre sus puntas (0.05 = 5% más larga). Es lo que hace que cuelgue:
    // con 0 la soga quedaría recta como una barra y sin movimiento.
    private float _ropeSlack;
    #endregion
    
    private RotateGekkoWhileSwingin _gekkoRotation;
    
    #region Properties
    public bool IsSwinging { get; private set; }
    public Vector2 ThrustInput { set { _thrustInput = value; } }
    public bool ShortenCablePressed { set { _shortenCablePressed = value; } }
    public Vector3 GrapplePoint => _grapplePoint;
    #endregion

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam,
                        float forwardThrustForce, float horizontalThrustForce, float extendCableSpeed,
                        Transform predictionPoint, float predictionSphereRadius, float maxTongueDistance,
                        int quality, float springDamper, float springStrength, float springVelocity,
                        float waveCount, float waveHeight, AnimationCurve waveAffectCurve,
                        float ropeGravity, float ropeDamping, int ropeIterations, float ropeSlack)
    {
        _lineRenderer = lineRenderer;
        _tongueTip = tongue;
        _grappableLayers = layers;
        _transform = transform;
        _camera = cam;
        _rb = transform.GetComponent<Rigidbody>();
        _forwardThrustForce = forwardThrustForce;
        _horizontalThrustForce = horizontalThrustForce;
        _extendCableSpeed = extendCableSpeed;
        _predictionPoint = predictionPoint;
        _predictionSphereRadius = predictionSphereRadius;
        _maxTongueDistance = maxTongueDistance;

        // Mathf.Max(1, ...) garantiza al menos un hilo y evita dividir por cero en UpdateLineRenderer (delta = i / _quality).
        _quality = Mathf.Max(1, quality);
        _springDamper = springDamper;
        _springStrength = springStrength;
        _springVelocity = springVelocity;
        _waveCount = waveCount;
        _waveHeight = waveHeight;
        _waveAffectCurve = waveAffectCurve;

        // El Spring del latigazo se configura una sola vez, porque estos valores no cambian mientras se juega.
        // Su "target" es 0: siempre quiere volver a "sin onda", por eso el latigazo se apaga solo.
        _spring = new Spring();
        _spring.SetTarget(0);
        _spring.SetDamper(_springDamper);
        _spring.SetStrength(_springStrength);

        // Se crean, una sola vez, la simulación de la soga y la lista de puntos para dibujar. Los dos tienen
        // _quality + 1 puntos (siempre uno más que hilos). Se reservan acá para no crear arrays nuevos en cada frame.
        _rope = new RopeSimulation(_quality, ropeDamping, ropeGravity, ropeIterations);
        _ropePoints = new Vector3[_quality + 1];

        // El sobrante de soga no puede ser negativo: una soga más corta que la distancia entre sus puntas quedaría
        // siempre estirada, sin colgar.
        _ropeSlack = Mathf.Max(0f, ropeSlack);

        _gekkoRotation = new RotateGekkoWhileSwingin(this, _rb);
    }

    public void StartGrapple()
    {
        if(!TryFindSwingPoint(out RaycastHit hit)) return;

        Transform grapplePointTransform = GetGrapplePoint(hit.transform);

        if(grapplePointTransform == null) return;

        IsSwinging = true;
        _grapplePoint = grapplePointTransform.position;
        _joint = _transform.gameObject.AddComponent<SpringJoint>();
        _joint.autoConfigureConnectedAnchor = false;
        _joint.connectedAnchor = _grapplePoint;
        //Optiomizar con el sqrmagnitude
        float distanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint);
        _joint.maxDistance = distanceFromPoint * 0.8f; //Consultar motivo del valor
        _joint.minDistance = distanceFromPoint * 0.25f; //Consultar motivo del valor
        _joint.spring = 4.5f; //Consultar motivo del valor
        _joint.damper = 7f; //Consultar motivo del valor
        _joint.massScale = 4.5f; //Consultar motivo del valor

        // ---- Arranque de la soga visual ----
        // Cada enganche empieza de cero, sin "memoria" del anterior:
        //  1) _currentGrapplePoint (la punta de la soga que viaja hacia el ancla) sale desde la lengua.
        //  2) Reset(): toda la soga arranca colapsada en la lengua y quieta. Sin esto heredaría la forma y el
        //     movimiento del enganche anterior. Después, mientras la punta viaja hacia el ancla (ver DrawTongue), la
        //     soga se va desenrollando detrás de ella.
        //  3) El Spring recibe su empujón inicial: es lo que dispara el latigazo.
        _currentGrapplePoint = _tongueTip.position;
        _rope.Reset(_currentGrapplePoint);
        _spring.SetVelocity(_springVelocity);

        // El LineRenderer necesita tener un punto por cada punto de la soga (hilos + 1) antes de recibir posiciones.
        _lineRenderer.positionCount = _quality + 1;

        // Se dibuja ya la pose inicial (sin avanzar la simulación) para que el primer frame se vea bien, aunque este
        // método corra antes o después de ArtificialLateUpdate.
        UpdateLineRenderer();
    }

    public void ArtificialUpdate()
    {
        CheckForSwingPoints();

        if(!_joint) return;

        // Acá queda solo lo que EMPUJA a Gekko (la física real del swing). Simular y dibujar la soga se hace en
        // ArtificialLateUpdate.
        ODMGearMovement();
    }

    // Lo llama Player.LateUpdate() una vez por frame. La soga se simula y se dibuja acá y no en ArtificialUpdate porque:
    //  1) LateUpdate corre cuando todo ya se movió en este frame (el Rigidbody interpolado, las animaciones). Así se
    //     lee la posición FINAL de la lengua. Si se leyera antes, la punta de la soga iría un frame atrasada respecto
    //     de la boca y se vería temblar.
    //  2) Es lo último que pasa antes de dibujar la pantalla, así que se muestra exactamente lo que se acaba de simular.
    public void ArtificialLateUpdate()
    {
        // Sin joint no hay soga (StopGrapple pone _joint = null). Es el mismo guard que usa ArtificialUpdate.
        if(!_joint) return;

        DrawTongue();
    }

    public void ArtificialFixedUpdate()
    {
        if(!_joint) return;

        _gekkoRotation.ArtificialFixedUpdate();
    }

    // Un frame de la lengua. Son 5 pasos, siempre en este orden:
    //   1) avanza el latigazo   2) se ubican las dos puntas   3) se calcula el largo de la soga
    //   4) avanza la simulación   5) se dibuja
    private void DrawTongue()
    {
        float deltaTime = Time.deltaTime;

        // PASO 1 - El latigazo avanza un poquito: el Spring va perdiendo fuerza y la onda se achica hasta desaparecer.
        // (StartGrapple le dio el empujón inicial con SetVelocity.)
        _spring.Update(deltaTime);

        // PASO 2 - ¿Dónde están las dos puntas de la soga?
        //  - La boca: la posición real de la lengua de Gekko en este frame. Se mueve con él, incluso cuando se balancea.
        //  - El ancla: no salta de golpe al objetivo. _currentGrapplePoint arranca en la lengua y se acerca a
        //    _grapplePoint (el punto de enganche real) un poco en cada frame (Lerp, unas 8 veces por segundo). Da el
        //    efecto de lengua "disparada", y la soga se va desenrollando detrás.
        Vector3 tongueTipPos = _tongueTip.position;
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, deltaTime * 8f);

        // PASO 3 - ¿Cuánto mide la soga? La distancia entre sus puntas más un sobrante (_ropeSlack).
        //  - Ese sobrante es lo que la hace colgar y ondular. Ejemplo: puntas a 5 m y slack 0.05 -> la soga mide 5.25 m.
        //  - NO se usa _joint.maxDistance: el joint se configura en 0.8 x la distancia inicial, o sea MÁS CORTO que el
        //    hueco real entre las puntas. Una soga de ese largo estaría siempre estirada y recta, y no se vería el efecto.
        //  - Como sigue a la distancia actual, acortar o alargar el cable (ODMGearMovement) no necesita ningún caso
        //    especial: la soga se acomoda sola.
        float visibleDistance = Vector3.Distance(tongueTipPos, _currentGrapplePoint);
        float restLength = visibleDistance * (1f + _ropeSlack);

        // PASO 4 - La simulación avanza: los puntos de en medio se mueven por inercia y gravedad, y los hilos
        // conservan su largo (el detalle está explicado arriba de RopeSimulation).
        _rope.Simulate(deltaTime, tongueTipPos, _currentGrapplePoint, restLength);

        // PASO 5 - Se dibuja: se copian los puntos al LineRenderer sumándoles el latigazo.
        UpdateLineRenderer();
    }

    // Copia los puntos de la soga al LineRenderer y les suma el latigazo. No hace avanzar nada: solo dibuja el estado
    // actual (por eso StartGrapple también lo puede usar para mostrar la pose inicial).
    private void UpdateLineRenderer()
    {
        // Los puntos que calculó la simulación: el 0 es la lengua y el último es el ancla.
        Vector3[] simulated = _rope.Points;

        // Hacia dónde se curva el latigazo: una dirección "arriba" que sea perpendicular a la soga. Se obtiene girando
        // un "arriba" común para que mire a lo largo de la soga (LookRotation).
        // La dirección de la soga se calcula con _grapplePoint (el ancla real) y NUNCA con _currentGrapplePoint: en el
        // primer frame de cada enganche _currentGrapplePoint es igual a la lengua, la dirección daría vector cero y
        // LookRotation(Vector3.zero) tira un warning. El if es una red de seguridad por si lengua y ancla coinciden.
        Vector3 ropeDir = _grapplePoint - simulated[0];
        Vector3 up = ropeDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(ropeDir.normalized) * Vector3.up
            : Vector3.up;

        for(int i = 0; i <= _quality; i++)
        {
            // Posición relativa del punto dentro de la soga: 0 en la lengua, 0.5 en el medio, 1 en el ancla.
            float delta = i / (float)_quality;

            // El latigazo en este punto = altura * onda * fuerza actual del spring * curva de atenuación:
            //  - onda senoidal: sube y baja _waveCount veces a lo largo de la soga.
            //  - _spring.Value: arranca alto al enganchar y se apaga solo (por eso el latigazo desaparece).
            //  - curva: vale 0 en las dos puntas, así la lengua y el ancla no se despegan aunque la onda esté al máximo.
            Vector3 whip = up * _waveHeight * Mathf.Sin(delta * _waveCount * Mathf.PI) * _spring.Value * _waveAffectCurve.Evaluate(delta);

            // Punto final = punto de la simulación + latigazo. El latigazo se suma recién acá, al dibujar, y NO dentro
            // de la simulación: si entrara ahí, la soga lo trataría como movimiento real y lo arrastraría consigo. Así
            // el latigazo sigue siendo un efecto controlable y la física aporta el movimiento natural.
            _ropePoints[i] = simulated[i] + whip;
        }

        // Se entregan todos los puntos al LineRenderer de una sola vez (más barato que un SetPosition por punto).
        _lineRenderer.SetPositions(_ropePoints);
    }

    //Requiere muchísima optimización
    public void StopGrapple()
    {
        IsSwinging = false;
        _lineRenderer.positionCount = 0;
        // La soga no hace falta resetearla acá: cada enganche nuevo empieza con _rope.Reset() en StartGrapple.
        _spring.Reset();
        Object.Destroy(_joint);
        // Destroy es diferido (se ejecuta al final del frame): sin esta línea, ArtificialUpdate y ArtificialLateUpdate
        // seguirían viendo el joint "vivo" durante el resto de este frame y dibujarían la lengua sobre un LineRenderer
        // que ya quedó con 0 puntos.
        _joint = null;
    }

    private void ODMGearMovement()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized;
        _rb.AddForce(flatRight * _thrustInput.x * _horizontalThrustForce * Time.deltaTime);

        if(_thrustInput.y > 0f)
            _rb.AddForce(flatForward * _thrustInput.y * _forwardThrustForce * Time.deltaTime);

        if(_shortenCablePressed)
        {
            Vector3 directionToPoint = _grapplePoint - _transform.position;
            _rb.AddForce(directionToPoint.normalized * _forwardThrustForce * Time.deltaTime);
            float distanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint);
            _joint.maxDistance = distanceFromPoint * 0.8f;
            _joint.minDistance = distanceFromPoint * 0.25f;
        }

        if(_thrustInput.y < 0f)
        {
            float extendedDistanceFromPoint = Vector3.Distance(_transform.position, _grapplePoint) + _extendCableSpeed;
            _joint.maxDistance = extendedDistanceFromPoint * 0.8f;
            _joint.minDistance = extendedDistanceFromPoint * 0.25f;
        }
    }

    private void CheckForSwingPoints()
    {
        bool foundPoint = TryFindSwingPoint(out RaycastHit hit);

        if(!foundPoint)
        {
            _predictionPoint.gameObject.SetActive(false);
            return;
        }

        Transform hitTransform = GetGrapplePoint(hit.transform);

        if(hitTransform == null)
        {
            _predictionPoint.gameObject.SetActive(false);
            return;
        }

        _predictionPoint.gameObject.SetActive(true);

        if(hitTransform.position != _previousHitPosition)
            _predictionPoint.position = hitTransform.position;

        _previousHitPosition = hitTransform.position;
    }

    private Transform GetGrapplePoint(Transform hitObject)
    {
        if(hitObject == _lastHitObject) return _cachedGrapplePoint;

        _lastHitObject = hitObject;
        _cachedGrapplePoint = hitObject.Find("GrapplePoint");
        return _cachedGrapplePoint;
    }

    private bool TryFindSwingPoint(out RaycastHit hit)
    {
        return Physics.SphereCast(_camera.position, _predictionSphereRadius, _camera.forward, out hit, _maxTongueDistance, _grappableLayers);
    }
}
