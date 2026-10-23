using UnityEngine;

public sealed class GekkoSwinging
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
    private RaycastHit _predictionHit;
    private Transform _lastHitObject, _cachedGrapplePoint, _predictionPoint,
                    _camera, _transform, _tongueTip;
    
    #region Tongue Visual
    // Cómo se dibuja la lengua: tres cosas, en este orden.
    //   1) LA SOGA FÍSICA (RopeSimulation): decide la forma base. Se queda atrás cuando Gekko se mueve y rebota al frenar.
    //      Puede colgar (slack positivo) o estar pre-tensada (slack negativo). La explicación completa está arriba de
    //      esa clase.
    //   2) LA TENSIÓN VISUAL (_ropeTautness): al dibujar, la forma de la soga se acerca a la recta boca -> ancla. Es lo que
    //      deja la lengua pareja de punta a punta.
    //   3) EL LATIGAZO (Spring + onda senoidal): una ondulación "artística" que aparece al enganchar y se apaga sola.
    // La física manda, la tensión visual la endereza y el latigazo se le suma por encima recién al dibujar (ver
    // UpdateLineRenderer).

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

    // Largo de la soga respecto de la distancia real entre sus puntas (0.05 = 5% más larga, -0.02 = 2% más corta):
    //   positivo : sobra soga, cuelga y se abomba. Una soga sin tensión se dobla de costado con cualquier acortamiento de
    //              la distancia, así que por poco que sobre el abombado es grande (decenas de cm en una soga de 4 m).
    //   0        : justo. Queda casi recta, pero todavía se abomba un poco cuando las puntas se mueven.
    //   negativo : soga PRE-TENSADA. Siempre está estirada, así que todos los hilos tienen la misma tensión y no hay de
    //              dónde doblarse. Es lo que la deja tensa y pareja de punta a punta.
    private float _ropeSlack;

    // Cuánto se endereza lo que se VE de la soga: 0 = tal cual la simulación, 1 = recta de la boca al ancla. Es solo
    // visual, no cambia la simulación ni el latigazo (ver UpdateLineRenderer).
    private float _ropeTautness;

    // Lo más pre-tensada que puede estar la soga: -0.1 = 10% más corta que la distancia entre sus puntas. Más que eso no
    // se ve mejor, y un valor cercano a -1 dejaría la soga sin largo.
    private const float MinRopeSlack = -0.1f;
    #endregion

    #region Height Limit
    // Techo blando del swing (ver ApplyHeightLimit): Gekko no debería subir más arriba del punto de enganche.
    //   _heightLimitOffset : corre el techo respecto de la altura del punto (0 = justo a su altura).
    //   _heightLimitSpring : cuánto lo tira hacia abajo por cada metro que se pasó del techo (m/s² por metro).
    //   _heightLimitDamper : cuánto frena su velocidad de subida mientras está pasado (m/s² por cada m/s).
    //   _heightLimitMaxPull: tope de la aceleración hacia abajo (m/s²).
    private float _heightLimitOffset, _heightLimitSpring, _heightLimitDamper, _heightLimitMaxPull;
    #endregion

    private RotateGekkoWhileSwingin _gekkoRotation;
    private GekkoGrapple _grappleBehaviour;
    
    #region Properties
    // Colgado de la lengua, balanceándose con el SpringJoint.
    public bool IsSwinging { get; private set; }
    // Durante todo el grapple (la espera y el vuelo). Nunca es true a la vez que IsSwinging: al arrancar el grapple se
    // suelta el swing.
    public bool IsGrappling => _grappleBehaviour != null && _grappleBehaviour.IsGrappling;
    public Vector2 ThrustInput { set { _thrustInput = value; } }
    public Vector3 GrapplePoint => _grapplePoint;
    public GekkoGrapple GrappleBehaviour => _grappleBehaviour;
    #endregion

    public GekkoSwinging(Transform tongue, LayerMask layers, Transform transform, LineRenderer lineRenderer, Transform cam,
                        float forwardThrustForce, float horizontalThrustForce, float extendCableSpeed,
                        Transform predictionPoint, float predictionSphereRadius, float maxTongueDistance,
                        int quality, float springDamper, float springStrength, float springVelocity,
                        float waveCount, float waveHeight, AnimationCurve waveAffectCurve,
                        float ropeGravity, float ropeDamping, int ropeIterations, float ropeSlack, float ropeTautness,
                        float heightLimitOffset, float heightLimitSpring, float heightLimitDamper, float heightLimitMaxPull)
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

        // El sobrante de soga puede ser negativo: una soga más corta que la distancia entre sus puntas queda siempre
        // estirada y recta, con la misma tensión en todos los hilos (soga pre-tensada). El mínimo evita un largo de 0.
        // La tensión visual es un porcentaje: de 0 a 1.
        _ropeSlack = Mathf.Max(MinRopeSlack, ropeSlack);
        _ropeTautness = Mathf.Clamp01(ropeTautness);

        // Resorte, amortiguador y tope no pueden ser negativos: un valor negativo empujaría a Gekko hacia ARRIBA.
        _heightLimitOffset = heightLimitOffset;
        _heightLimitSpring = Mathf.Max(0f, heightLimitSpring);
        _heightLimitDamper = Mathf.Max(0f, heightLimitDamper);
        _heightLimitMaxPull = Mathf.Max(0f, heightLimitMaxPull);

        _gekkoRotation = new RotateGekkoWhileSwingin(this, _rb);
    }

    // Engancha la lengua a un punto y arranca el swing (SpringJoint). Lo llama PlayerInputs al apretar el botón de la lengua.
    public void StartSwing()
    {
        // En pleno grapple no se puede enganchar un swing nuevo: habría dos sistemas manejando a Gekko a la vez. La excepción
        // es el segundo salto: ya no tiene lengua ni punto que alcanzar, así que se lo puede cortar enganchando otro gancho.
        bool interruptsSecondJump = _grappleBehaviour != null && _grappleBehaviour.IsSecondJumpFlying;

        if(IsGrappling && !interruptsSecondJump) return;

        if(!TryFindSwingPoint(out RaycastHit hit)) return;

        Transform grapplePointTransform = GetGrapplePoint(hit.transform);

        if(grapplePointTransform == null) return;

        // Recién con un punto válido se corta el vuelo: si no hay a qué engancharse, el salto sigue como estaba. Va antes de
        // armar el swing porque cortar el vuelo apaga la lengua (HideTongue) y el swing la tiene que prender después.
        if(interruptsSecondJump) _grappleBehaviour.EndSecondJump();

        IsSwinging = true;
        _grapplePoint = grapplePointTransform.position;
        _gekkoRotation.StartSwing();
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
        _grappleBehaviour.ArtificialUpdate();

        CheckForSwingPoints();

        // Sin joint no hay swing. Durante el grapple tampoco hay joint, así que el empuje (ODM) no corre: igual que en el
        // repo, donde MovePlayer corta con activeGrapple.
        if(!_joint) return;

        ODMGearMovement();
    }

    // Lo llama Player.LateUpdate() una vez por frame. La soga se simula y se dibuja acá y no en ArtificialUpdate porque:
    //  1) LateUpdate corre cuando todo ya se movió en este frame (el Rigidbody interpolado, las animaciones). Así se
    //     lee la posición FINAL de la lengua. Si se leyera antes, la punta de la soga iría un frame atrasada respecto
    //     de la boca y se vería temblar.
    //  2) Es lo último que pasa antes de dibujar la pantalla, así que se muestra exactamente lo que se acaba de simular.
    public void ArtificialLateUpdate()
    {
        // La lengua se dibuja mientras haya swing O grapple con la lengua afuera. Durante el grapple ya no hay joint (se
        // soltó el swing), pero la soga sigue yendo de la boca al ancla hasta que termina el vuelo. El segundo salto la
        // suelta (IsTongueOut), aunque siga habiendo vuelo. Sin ninguno de los dos no hay soga.
        if(!IsSwinging && !(_grappleBehaviour != null && _grappleBehaviour.IsTongueOut)) return;

        DrawTongue();
    }

    public void ArtificialFixedUpdate()
    {
        // Antes del guard del joint: la espera del grapple (Gekko clavado en el aire) necesita FixedUpdate y no hay joint.
        _grappleBehaviour.ArtificialFixedUpdate();

        // Antes del guard del joint: durante el grapple no hay joint y Gekko igual tiene que girar hacia el punto.
        // RotateGekkoWhileSwingin se protege sola: solo gira con swing o con grapple.
        _gekkoRotation.ArtificialFixedUpdate();

        if(!_joint) return;

        ApplyHeightLimit();
    }

    // Techo blando del swing: Gekko no puede subir más arriba del punto de enganche (+ _heightLimitOffset). Si se pasa,
    // se lo tira hacia abajo como un resorte. Solo actúa sobre el eje Y, así que el swing horizontal sigue igual.
    // Va en FixedUpdate porque es física real sobre el Rigidbody (a diferencia de ODMGearMovement, que corre en Update).
    //  - Resorte: tira más fuerte cuanto más se pasó del techo.
    //  - Amortiguador: frena la velocidad de SUBIDA. Solo la de subida, para no frenar también la vuelta hacia abajo.
    //  - Tope: la aceleración total no pasa de _heightLimitMaxPull. Sin tope, si Gekko engancha un punto que está muy
    //    por debajo de él, el resorte lo tiraría con una fuerza enorme de golpe.
    private void ApplyHeightLimit()
    {
        float overshoot = _rb.position.y - (_grapplePoint.y + _heightLimitOffset);
        if(overshoot <= 0f) return;

        float upwardSpeed = Mathf.Max(0f, _rb.linearVelocity.y);
        float pull = Mathf.Min(_heightLimitSpring * overshoot + _heightLimitDamper * upwardSpeed, _heightLimitMaxPull);

        // ForceMode.Acceleration: los valores están en m/s² y no dependen de la masa del Rigidbody.
        _rb.AddForce(Vector3.down * pull, ForceMode.Acceleration);
    }

    // Un frame de la lengua. Son 5 pasos, siempre en este orden:
    //   1) avanza el latigazo   2) se ubican las dos puntas   3) se calcula el largo de la soga
    //   4) avanza la simulación   5) se dibuja
    private void DrawTongue()
    {
        float deltaTime = Time.deltaTime;

        // PASO 1 - El latigazo avanza un poquito: el Spring va perdiendo fuerza y la onda se achica hasta desaparecer.
        // (StartSwing le dio el empujón inicial con SetVelocity.)
        _spring.Update(deltaTime);

        // PASO 2 - ¿Dónde están las dos puntas de la soga?
        //  - La boca: la posición real de la lengua de Gekko en este frame. Se mueve con él, incluso cuando se balancea.
        //  - El ancla: no salta de golpe al objetivo. _currentGrapplePoint arranca en la lengua y se acerca a
        //    _grapplePoint (el punto de enganche real) un poco en cada frame (Lerp, unas 8 veces por segundo). Da el
        //    efecto de lengua "disparada", y la soga se va desenrollando detrás.
        Vector3 tongueTipPos = _tongueTip.position;
        _currentGrapplePoint = Vector3.Lerp(_currentGrapplePoint, _grapplePoint, deltaTime * 8f);

        // PASO 3 - ¿Cuánto mide la soga? La distancia entre sus puntas más un sobrante (_ropeSlack), que puede ser negativo.
        //  - Positivo: la soga cuelga y se abomba. Ejemplo: puntas a 5 m y slack 0.05 -> la soga mide 5.25 m.
        //  - Negativo: la soga está pre-tensada. Ejemplo: puntas a 5 m y slack -0.02 -> mide 4.9 m, más corta que el hueco,
        //    así que queda estirada y recta, con la misma tensión en todos los hilos. Se usa un valor chico (-0.02).
        //  - NO se usa _joint.maxDistance: el joint se configura en 0.8 x la distancia inicial, o sea un 20% MÁS CORTO que
        //    el hueco real entre las puntas. Una soga estirada tanto quedaría como una barra.
        //  - Como sigue a la distancia actual, acortar o alargar el cable (ODMGearMovement) no necesita ningún caso
        //    especial: la soga se acomoda sola.
        float visibleDistance = Vector3.Distance(tongueTipPos, _currentGrapplePoint);
        float restLength = visibleDistance * (1f + _ropeSlack);

        // PASO 4 - La simulación avanza: los puntos de en medio se mueven por inercia y gravedad, y los hilos
        // conservan su largo (el detalle está explicado arriba de RopeSimulation).
        _rope.Simulate(deltaTime, tongueTipPos, _currentGrapplePoint, restLength);

        // PASO 5 - Se dibuja: se copian los puntos al LineRenderer, enderezados según _ropeTautness y con el latigazo.
        UpdateLineRenderer();
    }

    // Copia los puntos de la soga al LineRenderer, los endereza según _ropeTautness y les suma el latigazo. No hace
    // avanzar nada: solo dibuja el estado actual (por eso StartSwing también lo puede usar para mostrar la pose inicial).
    private void UpdateLineRenderer()
    {
        // Los puntos que calculó la simulación: el 0 es la lengua y el último es el ancla.
        Vector3[] simulated = _rope.Points;
        Vector3 ropeStart = simulated[0];
        Vector3 ropeEnd = simulated[_quality];

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

            // Tensión visual: el punto simulado se acerca a su lugar sobre la recta lengua -> ancla, que es donde estaría si
            // la soga estuviera perfectamente tensa. Con _ropeTautness en 0 queda donde lo dejó la simulación; con 1,
            // sobre la recta. Las dos puntas ya están sobre la recta, así que no se mueven. Es solo al dibujar: la
            // simulación sigue por su cuenta y el desvío se vuelve a calcular en cada frame, no se acumula.
            Vector3 shapedPoint = Vector3.Lerp(simulated[i], Vector3.Lerp(ropeStart, ropeEnd, delta), _ropeTautness);

            // Punto final = punto enderezado + latigazo. El latigazo se suma recién acá, al dibujar, y NO dentro de la
            // simulación: si entrara ahí, la soga lo trataría como movimiento real y lo arrastraría consigo. Tampoco lo
            // endereza la tensión visual: la onda queda siempre completa. Así el latigazo sigue siendo un efecto
            // controlable y la física aporta el movimiento natural.
            _ropePoints[i] = shapedPoint + whip;
        }

        // Se entregan todos los puntos al LineRenderer de una sola vez (más barato que un SetPosition por punto).
        _lineRenderer.SetPositions(_ropePoints);
    }

    // Space estando colgado de la lengua: suelta el swing y dispara a Gekko hacia el punto de enganche en una parábola (ver
    // GekkoGrapple). Reemplaza al viejo "acortar cable". PlayerInputs lo llama UNA vez por pulsación, no en cada frame que
    // se mantiene apretado. No hace nada si no hay swing (no hay ancla a la que ir), si hay cooldown o si ya hay un
    // grapple en curso.
    public void TryStartGrapple()
    {
        if(!IsSwinging) return;

        // Si el grapple no puede arrancar (cooldown, parábola inválida) el swing sigue como estaba.
        if(!_grappleBehaviour.TryStart(_grapplePoint)) return;

        // El grapple ya tomó el control de Gekko: se suelta el swing. La lengua queda dibujada hasta que termine el vuelo.
        ReleaseSwing();
    }

    public void TrySecondJump() => _grappleBehaviour.TrySecondJump(_camera.forward);

    // Corta un grapple en curso, sin cooldown. Lo llama PlayerController.Teleport: el vuelo apunta a un ancla que con el
    // teletransporte quedó lejos.
    public void CancelGrapple() => _grappleBehaviour?.Cancel();

    // Lo llama Player.OnCollisionEnter con cada choque sólido. Colgado del swing lo corta, como siempre. Si no, el choque
    // le toca al grapple, que lo usa para terminar el vuelo.
    public void ArtificialOnCollisionEnter()
    {
        if(IsSwinging) StopSwing();
        else _grappleBehaviour.ArtificialOnCollisionEnter();
    }

    //Requiere muchísima optimización
    // Corta el swing y apaga la lengua. Lo llaman PlayerInputs (al soltar el botón de la lengua), los choques y los triggers.
    public void StopSwing()
    {
        // Soltar el botón de la lengua en pleno grapple no lo corta: el vuelo termina solo, al chocar, al llegar al punto
        // o por tiempo (ver GekkoGrapple). En el repo tampoco hay nada que corte el vuelo salvo tocar algo.
        if(IsGrappling) return;

        ReleaseSwing();
        HideTongue();
    }

    // Suelta el swing: sin joint, IsSwinging = false (con eso PlayerController, el techo de altura y la rotación hacia el
    // punto dejan de tratar a Gekko como balanceándose). NO toca la lengua: durante el grapple se sigue dibujando.
    private void ReleaseSwing()
    {
        IsSwinging = false;
        Object.Destroy(_joint);
        // Destroy es diferido (se ejecuta al final del frame): sin esta línea, ArtificialUpdate y ArtificialFixedUpdate
        // seguirían viendo el joint "vivo" durante el resto de este frame y le seguirían aplicando empuje, techo y rotación.
        _joint = null;
    }

    // Apaga la lengua: el LineRenderer queda sin puntos y el latigazo se reinicia. La soga no hace falta resetearla acá:
    // cada enganche nuevo empieza con _rope.Reset() en StartSwing. Es público porque GekkoGrapple la llama al terminar el
    // vuelo (mientras dura el grapple la lengua se queda dibujada).
    public void HideTongue()
    {
        _lineRenderer.positionCount = 0;
        _spring.Reset();
    }

    private void ODMGearMovement()
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(_camera.right, Vector3.up).normalized;
        _rb.AddForce(flatRight * _thrustInput.x * _horizontalThrustForce * Time.deltaTime);
        _rb.AddForce(flatForward * _thrustInput.y * _forwardThrustForce * Time.deltaTime);
    }

    private void CheckForSwingPoints()
    {
        bool foundPoint = TryFindSwingPoint(out RaycastHit hit);
        ParticleSystem ps = _predictionPoint.GetComponentInChildren<ParticleSystem>();

        if(ps == null)
        {
            Debug.Log("Saramambiche");
            return;
        } 

        if(!foundPoint)
        {
            HidePredictionParticle(ps);
            return;
        }

        Transform hitTransform = GetGrapplePoint(hit.transform);

        if(hitTransform == null)
        {
            HidePredictionParticle(ps);
            return;
        }

        ps.Play();

        if(hitTransform.position != _previousHitPosition)
            _predictionPoint.position = hitTransform.position;

        _previousHitPosition = hitTransform.position;
    }

    // Ps_Selected tiene una única partícula con startLifetime = 99999 y sin loop. Con un Stop() común (StopEmitting) solo
    // se deja de emitir: la partícula ya viva sigue ahí, congelada en la última posición. Por eso hay que pedir
    // StopEmittingAndClear, que además la elimina. El próximo Play() la vuelve a emitir.
    private void HidePredictionParticle(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    public GekkoSwinging InitializeGrappleBehaviour(float overshootYAxis, float grapplingCD, float delayTime,
                                                    float finishDistance, float lookSpeed, float secondJumpMultiplier)
    {
        _grappleBehaviour = new GekkoGrapple(this, _rb, _transform, overshootYAxis, grapplingCD, delayTime,
                                             finishDistance, _gekkoRotation, lookSpeed, secondJumpMultiplier);
        return this;
    }
}
