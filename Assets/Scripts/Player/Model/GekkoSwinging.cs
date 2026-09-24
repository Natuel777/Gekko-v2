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
    // Latigazo al enganchar: un oscilador amortiguado (Spring) que curva la soga con una onda senoidal y se apaga solo.
    // _quality es la cantidad de hilos de la soga: el LineRenderer y la simulación tienen _quality + 1 puntos.
    private int _quality;
    private float _springDamper, _springStrength, _springVelocity, _waveCount, _waveHeight;
    private AnimationCurve _waveAffectCurve;

    // Soga física: RopeSimulation (Verlet) calcula la forma y el movimiento; el latigazo se suma recién al dibujar.
    private RopeSimulation _rope;
    // Buffer de dibujo (puntos de la simulación + latigazo). Se crea una sola vez y se reusa en cada frame: sin GC.
    private Vector3[] _ropePoints;
    // Largo extra de la soga sobre la distancia real entre sus puntas (0.05 = 5%). Es lo que la hace colgar.
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

        // Mathf.Max evita división por cero en el loop de UpdateLineRenderer (delta = i / _quality).
        _quality = Mathf.Max(1, quality);
        _springDamper = springDamper;
        _springStrength = springStrength;
        _springVelocity = springVelocity;
        _waveCount = waveCount;
        _waveHeight = waveHeight;
        _waveAffectCurve = waveAffectCurve;

        // El spring del latigazo se configura una sola vez: estos valores no cambian mientras se juega.
        _spring = new Spring();
        _spring.SetTarget(0);
        _spring.SetDamper(_springDamper);
        _spring.SetStrength(_springStrength);

        // La soga tiene un punto más que hilos (el mismo conteo que el LineRenderer). El buffer de dibujo se
        // reserva acá para no crear un array nuevo en cada frame.
        _rope = new RopeSimulation(_quality, ropeDamping, ropeGravity, ropeIterations);
        _ropePoints = new Vector3[_quality + 1];

        // Un slack negativo dejaría la soga más corta que la distancia entre sus puntas: quedaría siempre estirada.
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

        // Cada enganche arranca de cero, sin "memoria" del anterior:
        //  - La punta de la soga (_currentGrapplePoint) sale desde la lengua y después viaja hasta el ancla.
        //  - La soga arranca colapsada en la lengua y sin velocidad: sin el Reset heredaría la forma y el movimiento
        //    del enganche anterior. Al viajar la punta, la soga se desenrolla sola detrás de ella.
        //  - El spring recibe el empujón inicial que dispara el latigazo.
        _currentGrapplePoint = _tongueTip.position;
        _rope.Reset(_currentGrapplePoint);
        _spring.SetVelocity(_springVelocity);

        // El LineRenderer tiene que tener un punto por cada punto de la soga (hilos + 1) antes de escribirle posiciones.
        _lineRenderer.positionCount = _quality + 1;

        // Se vuelca la soga ya en este momento (solo la pose, sin avanzar la simulación) para que el primer frame
        // sea coherente, sin depender de si esto corrió antes o después de ArtificialLateUpdate.
        UpdateLineRenderer();
    }

    public void ArtificialUpdate()
    {
        CheckForSwingPoints();

        if(!_joint) return;

        // Acá solo va la parte que empuja a Gekko. La soga (simulación + dibujo) corre en ArtificialLateUpdate.
        ODMGearMovement();
    }

    // Lo llama Player.LateUpdate. La soga se simula y se dibuja acá y no en ArtificialUpdate por dos motivos:
    //  - En LateUpdate la lengua ya tiene su posición final del frame: el Rigidbody de Gekko está interpolado y, si la
    //    lengua es un hueso animado, el Animator recién la actualiza después de Update. Leerla en Update dejaría
    //    la punta de la soga un frame atrasada respecto de la boca y se vería temblar.
    //  - Es lo último que ocurre antes de renderizar, así que se dibuja exactamente lo que se acaba de simular.
    public void ArtificialLateUpdate()
    {
        // Mismo guard que ArtificialUpdate: sin joint no hay soga (StopGrapple pone _joint = null).
        if(!_joint) return;

        DrawTongue();
    }

    public void ArtificialFixedUpdate()
    {
        if(!_joint) return;

        _gekkoRotation.ArtificialFixedUpdate();
    }

    // Un frame de la lengua: avanza el latigazo, simula la soga y la vuelca al LineRenderer.
    private void DrawTongue()
    {
        float deltaTime = Time.deltaTime;

        // 1) Latigazo: hace decaer la oscilación que StartGrapple disparó con SetVelocity().
        _spring.Update(deltaTime);

        // 2) Las dos puntas de la soga:
        //  - Boca: la posición real de la lengua de Gekko en este frame. Se mueve con él (y con su balanceo).
        //  - Ancla: no salta directo al objetivo. _currentGrapplePoint viaja desde la lengua hasta _grapplePoint
        //    (Lerp, ~8 por segundo), lo que da el efecto de lengua "disparada" mientras la soga se desenrolla tras ella.
        Vector3 tongueTipPos = _tongueTip.position;
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, deltaTime * 8f);

        // 3) Largo de la soga en reposo: la distancia visible entre sus puntas más un pequeño sobrante (_ropeSlack).
        //  - El sobrante es lo que la hace colgar y ondular. Con 0 quedaría recta como una barra, sin movimiento.
        //  - NO se ata a _joint.maxDistance: el joint se configura en 0.8 x la distancia inicial, o sea MÁS CORTO que
        //    el hueco real entre las puntas. Una soga de ese largo estaría estirada y recta casi todo el swing, y no
        //    se vería el efecto.
        //  - Como sigue a la distancia actual, acortar o alargar el cable (ODMGearMovement) no necesita ningún caso especial.
        float visibleDistance = Vector3.Distance(tongueTipPos, _currentGrapplePoint);
        float restLength = visibleDistance * (1f + _ropeSlack);

        // 4) Simulación Verlet: mueve los puntos de en medio por inercia y gravedad y mantiene el largo de los hilos.
        _rope.Simulate(deltaTime, tongueTipPos, _currentGrapplePoint, restLength);

        // 5) Se vuelca al LineRenderer, con el latigazo encima.
        UpdateLineRenderer();
    }

    // Copia la soga simulada al LineRenderer sumándole el latigazo del Spring. No avanza ninguna simulación:
    // solo dibuja el estado actual (por eso también lo puede usar StartGrapple para la pose inicial).
    private void UpdateLineRenderer()
    {
        Vector3[] simulated = _rope.Points;

        // Dirección 'up' perpendicular a la soga: hacia ahí se curva el latigazo.
        // Sale de _grapplePoint crudo, NUNCA de _currentGrapplePoint: en el primer frame de cada
        // enganche _currentGrapplePoint todavía es igual a la lengua (recién seteado en StartGrapple),
        // lo que forzaría el caso degenerado de LookRotation(Vector3.zero) en cada enganche.
        // simulated[0] es la lengua: es la punta clavada de la soga.
        Vector3 ropeDir = _grapplePoint - simulated[0];
        Vector3 up = ropeDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(ropeDir.normalized) * Vector3.up
            : Vector3.up;

        for(int i = 0; i <= _quality; i++)
        {
            // delta = 0 en la lengua, 1 en el ancla, y crece parejo entre los puntos.
            float delta = i / (float)_quality;

            // Latigazo: una onda senoidal (_waveCount crestas) multiplicada por el valor actual del spring, que
            // arranca alto al enganchar y se apaga solo. La curva vale 0 en delta = 0 y en delta = 1, así que las
            // puntas quedan clavadas aunque la onda esté al máximo.
            Vector3 whip = up * _waveHeight * Mathf.Sin(delta * _waveCount * Mathf.PI) * _spring.Value * _waveAffectCurve.Evaluate(delta);

            // El latigazo se suma solo acá, al dibujar, y NO dentro de la simulación: si entrara ahí la soga lo trataría
            // como movimiento real y lo arrastraría consigo. Así el spring sigue siendo un efecto artístico
            // controlable y la física aporta la parte orgánica.
            _ropePoints[i] = simulated[i] + whip;
        }

        // Una sola llamada para todos los puntos (en vez de un SetPosition por punto).
        _lineRenderer.SetPositions(_ropePoints);
    }

    //Requiere muchísima optimización
    public void StopGrapple()
    {
        IsSwinging = false;
        _lineRenderer.positionCount = 0;
        // La soga no se resetea acá: cada enganche nuevo arranca con _rope.Reset() en StartGrapple.
        _spring.Reset();
        Object.Destroy(_joint);
        // Destroy es diferido (fin de frame): sin esto los guards de ArtificialUpdate y ArtificialLateUpdate siguen
        // viendo el joint vivo ese frame y dibujan la lengua sobre un LineRenderer que ya quedó con 0 posiciones.
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
