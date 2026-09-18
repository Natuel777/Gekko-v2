using UnityEngine;

/// <summary>
/// Movimiento secundario procedural del Gecko, TODO por código (sin clips de animación):
///
///  - Cola con inercia: cadena de resortes, cada segmento arrastra detrás del anterior
///    y hace "latigazo" al girar; balanceo sutil al estar quieto y caída por gravedad.
///  - Columna que se curva hacia adentro del giro (serpenteo) y se endereza al soltar.
///  - Bob vertical + roll lateral al caminar; respiración leve al estar quieto.
///  - Cuello/cabeza que mira hacia donde se mueve (o a un target) con límites de ángulo.
///
/// Corre en LateUpdate, DESPUÉS del rig de Animation Rigging (que solo toca las patas).
/// Guarda su propio estado y NO lee de vuelta los huesos, así la cadena de la cola no se
/// rompe aunque otro sistema toque la jerarquía.
/// </summary>
[DefaultExecutionOrder(100)]
public class GeckoSecondaryMotion : MonoBehaviour
{
    #region Inspector
    [Header("Referencias (se autocompletan por nombre si quedan vacías)")]
    [SerializeField] private Transform _hips;
    [SerializeField] private Transform _spine1;
    [SerializeField] private Transform _spine2;
    [SerializeField] private Transform _neck;
    [SerializeField] private Transform[] _tail;              // Gecko_Tail1 .. Tail4
    [SerializeField] private Rigidbody _bodyRb;              // opcional, solo para robustez
    [Tooltip("Opcional. Si está, se usa para saber cuándo está trepando y curvar la columna " +
             "un poco de más en la transición piso->pared. Se engancha solo.")]
    [SerializeField] private GeckoMover _mover;

    [Header("Cola — inercia / latigazo")]
    [SerializeField] private bool _tailEnabled = true;
    [Tooltip("Rigidez del resorte de cada segmento (más alto = la cola sigue más rápido al cuerpo).")]
    [SerializeField] private float _tailStiffness = 120f;
    [Tooltip("Amortiguación del resorte (más alto = menos rebote / coletazo).")]
    [SerializeField] private float _tailDamping = 14f;
    [Tooltip("Cuánto se retrasa la cola contra el giro del cuerpo (grados de latigazo por rad/s).")]
    [SerializeField] private float _tailWhip = 22f;
    [Tooltip("El latigazo se acumula un poco más en cada segmento hacia la punta.")]
    [SerializeField] private float _tailWhipFalloff = 1.25f;
    [Tooltip("Balanceo idle de la cola: amplitud (grados) y frecuencia (Hz).")]
    [SerializeField] private float _tailIdleSwayDeg = 5f;
    [SerializeField] private float _tailIdleSwayFreq = 0.8f;
    [Tooltip("Caida de la cola por gravedad: grados, acumulados por segmento. En 0 la cola " +
             "no sube ni baja, solo se mueve de lado a lado.")]
    [SerializeField] private float _tailDroopDeg = 2.5f;
    [Tooltip("Vaiven lateral al caminar: grados por segmento, al ritmo del paso.")]
    [SerializeField] private float _tailWalkSwayDeg = 7f;
    [Tooltip("Ciclos de vaiven de cola por metro recorrido.")]
    [SerializeField] private float _tailWalkSwayCyclesPerMeter = 1.1f;
    [Tooltip("Ejes locales del hueso de cola. Se DERIVAN en Awake de la orientacion real " +
             "del hueso; solo tocalos si el resultado no es el esperado.")]
    [SerializeField] private bool _deriveTailAxes = true;
    [SerializeField] private Vector3 _tailSwayAxis = Vector3.up;
    [SerializeField] private Vector3 _tailDroopAxis = Vector3.right;

    [Header("Columna — curva en los giros")]
    [SerializeField] private bool _spineEnabled = true;
    [Tooltip("Grados que se curva la columna por cada rad/s de giro del cuerpo.")]
    [SerializeField] private float _spineBendPerTurn = 7f;
    [SerializeField] private float _spineMaxBendDeg = 16f;
    [Tooltip("Qué tan rápido la columna llega a la curva objetivo y vuelve a la recta.")]
    [SerializeField] private float _spineResponse = 9f;

    [Header("Ondulación al paso (sigue a las patas REALES)")]
    [Tooltip("La columna hace una S al caminar, en fase con los pasos: la cintura delantera gira hacia " +
             "la pata delantera que va adelante y la cadera gira al revés (marcha diagonal de lagarto). " +
             "Se calcula con la posición real de los pies respecto de su Home, no con un reloj: si el " +
             "bicho frena, la S frena con él. Usa las 4 patas de 'Columna — conformado al trepar'.")]
    [SerializeField] private bool _gaitUndulation = true;
    [Tooltip("Amplitud de la S en grados: giro acumulado de la columna entre la cadera y los hombros.")]
    [SerializeField] private float _gaitBendDeg = 12f;
    [Tooltip("Cuánto se separan (metros, a lo largo del cuerpo) el pie izquierdo y el derecho para llegar " +
             "a la amplitud completa. Si la S nunca llega a su máximo bajalo; si siempre está al tope, subilo. " +
             "Tiene que andar por la mitad del alcance real de las patas.")]
    [SerializeField] private float _gaitStride = 0.05f;
    [Tooltip("Velocidad (m/s) a partir de la cual la S tiene su amplitud completa. Parado no hay S, " +
             "aunque los pies hayan quedado uno más adelante que el otro.")]
    [SerializeField] private float _gaitFullSpeed = 0.35f;
    [Tooltip("Qué tan rápido la columna sigue a los pies. Más alto = más pegada a cada paso.")]
    [SerializeField] private float _gaitResponse = 14f;
    [Tooltip("Cuánto de la S se compensa en el cuello para que la cabeza no baile con el cuerpo. 1 = la " +
             "cabeza queda totalmente firme; 0 = acompaña la S entera.")]
    [Range(0f, 1.2f)]
    [SerializeField] private float _headCounter = 0.7f;
    [Tooltip("La cola barre de lado al ritmo de los pasos (en fase con la cadera) en vez de por " +
             "metros recorridos. Usa la amplitud de 'Vaivén lateral al caminar'.")]
    [SerializeField] private bool _tailFollowsGait = true;

    [Header("Columna — conformado al trepar")]
    [Tooltip("Dobla el cuerpo entero cuando las patas de adelante y las de atras estan en " +
             "superficies distintas (por ejemplo al empezar a subir un bloque). Sin esto el " +
             "cuerpo queda rigido y el bicho tiene que treparse 'de una pieza'.")]
    [SerializeField] private bool _conformEnabled = true;
    [Tooltip("Las 4 patas. Se usan las posiciones de los pies para medir la inclinacion real " +
             "del terreno bajo el cuerpo.")]
    [SerializeField] private GeckoLeg _legFL, _legFR, _legBL, _legBR;
    [Tooltip("Tope de cuanto se dobla el cuerpo, en grados repartidos por toda la columna. " +
             "Para trepar paredes de 90° la mitad delantera necesita levantarse bastante " +
             "mientras la trasera sigue en el piso: ~55 deja hacer esa forma sin romper el mesh.")]
    [SerializeField] private float _conformMaxDeg = 55f;
    [Tooltip("Grados de doblado por cada grado de desnivel medido entre patas.")]
    [SerializeField] private float _conformGain = 1.1f;
    [SerializeField] private float _conformResponse = 7f;
    [Tooltip("Grados extra de curva hacia la pared cuando GeckoMover dice que está trepando. " +
             "Rellena el instante en que el cuerpo ya encaró la pared pero las patas todavía " +
             "no mandaron su lectura de desnivel. 0 = solo lectura de patas.")]
    [SerializeField] private float _climbConformBoostDeg = 10f;
    [Tooltip("Eje local sobre el que se dobla la columna. Se deriva en Awake.")]
    [SerializeField] private bool _deriveSpineAxis = true;
    [SerializeField] private Vector3 _spinePitchAxis = Vector3.right;

    [Header("Bob / respiración")]
    [SerializeField] private bool _bobEnabled = true;
    [Tooltip("Altura del bob vertical al caminar (metros).")]
    [SerializeField] private float _bobHeight = 0.006f;
    [Tooltip("Ciclos de bob por metro recorrido.")]
    [SerializeField] private float _bobCyclesPerMeter = 2.2f;
    [Tooltip("Roll lateral del cuerpo al caminar (grados).")]
    [SerializeField] private float _bobRollDeg = 3.5f;
    [Tooltip("Velocidad (m/s) a la que el bob llega a su amplitud máxima.")]
    [SerializeField] private float _bobFullSpeed = 0.6f;
    [Tooltip("Respiración al estar quieto: amplitud (grados de pitch del pecho) y frecuencia (Hz).")]
    [SerializeField] private float _breathDeg = 1.4f;
    [SerializeField] private float _breathFreq = 0.45f;

    [Header("Cabeza / cuello")]
    [SerializeField] private bool _headEnabled = true;
    [Tooltip("A qué mirar. Vacío = mira hacia donde se mueve (y al frente si está quieto).")]
    [SerializeField] private Transform _lookTarget;
    [SerializeField] private float _headMaxYawDeg = 45f;
    [SerializeField] private float _headMaxPitchDeg = 22f;
    [SerializeField] private float _headResponse = 6f;
    #endregion

    #region Estado
    private Quaternion _hipsRest, _spine1Rest, _spine2Rest, _neckRest;
    private Vector3 _hipsRestPos;
    private Quaternion[] _tailRest;
    private Quaternion[] _tailCur;     // rotación LOCAL actual de cada segmento
    private Vector3[] _tailVel;        // velocidad angular (grados/s, en local del padre)

    private Vector3 _lastPos;
    private float _lastYaw;
    private float _bobPhase;
    private float _spineBend;

    private Transform[] _spineChain;
    private Quaternion[] _spineChainRest;
    private Vector3 _pitchAxis = Vector3.right;   // derivados en Awake
    private Vector3 _yawAxis = Vector3.up;
    private float _conformAngle;
    private float _inclineSmoothed;   // ver MeasureIncline: filtra el ruido del pie en pleno paso
    private float _tailSwayPhase;
    private float _headYaw, _headPitch;
    private bool _ready;

    // Ondulación al paso: señales normalizadas [-1,1] de cada cintura (ya suavizadas).
    private float _gaitFront, _gaitBack;
    private float _gaitNetYaw;                 // giro acumulado hasta el último hueso de la columna
    private Vector3 _neckYawAxis = Vector3.up;
    #endregion

    /// <summary> Señal de la cintura delantera: +1 = pata izquierda al máximo adelante de la derecha. </summary>
    public float GaitFrontSignal => _gaitFront;
    /// <summary> Igual para la cadera (patas traseras). En marcha diagonal sale opuesta a la delantera. </summary>
    public float GaitBackSignal => _gaitBack;

    private void Awake()
    {
        AutoFill();

        if (_hips)   { _hipsRest = _hips.localRotation; _hipsRestPos = _hips.localPosition; }
        if (_spine1) _spine1Rest = _spine1.localRotation;
        if (_spine2) _spine2Rest = _spine2.localRotation;
        if (_neck)   _neckRest   = _neck.localRotation;

        if (_tail != null && _tail.Length > 0)
        {
            _tailRest = new Quaternion[_tail.Length];
            _tailCur  = new Quaternion[_tail.Length];
            _tailVel  = new Vector3[_tail.Length];
            for (int i = 0; i < _tail.Length; i++)
            {
                if (_tail[i] == null) continue;
                _tailRest[i] = _tail[i].localRotation;
                _tailCur[i]  = _tail[i].localRotation;
            }
        }

        BuildSpineChain();
        DeriveAxes();

        _lastPos = transform.position;
        _lastYaw = transform.eulerAngles.y;
        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        float dt = Time.deltaTime;
        if (dt <= 0.00001f) return;

        Vector3 vel = (transform.position - _lastPos) / dt;
        _lastPos = transform.position;
        float planarSpeed = Vector3.ProjectOnPlane(vel, transform.up).magnitude;

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) * Mathf.Deg2Rad / dt; // rad/s
        _lastYaw = yaw;

        UpdateGait(dt, planarSpeed);
        if (_spineEnabled) UpdateSpine(dt, yawRate);
        if (_bobEnabled)   UpdateBob(dt, planarSpeed);
        if (_headEnabled)  UpdateHead(dt, vel, planarSpeed);
        if (_tailEnabled)  UpdateTail(dt, yawRate, planarSpeed);
    }

    // -------------------------------------------------------------------------
    private bool GaitLegsReady => _legFL != null && _legFR != null && _legBL != null && _legBR != null;

    /// <summary>
    /// Saca de los pies REALES la fase del paso. Para cada pata se mide cuánto se adelantó su pie
    /// respecto de su Home a lo largo del cuerpo; la diferencia izquierda-derecha de cada par es
    /// la señal de esa cintura. En marcha diagonal (FL+BR, después FR+BL) las dos señales salen
    /// opuestas, y eso es exactamente la S de un lagarto: hombros y cadera giran en sentidos
    /// contrarios, en fase con los pasos y a cualquier velocidad.
    /// </summary>
    private void UpdateGait(float dt, float planarSpeed)
    {
        float front = 0f, back = 0f;

        if (_gaitUndulation && GaitLegsReady)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, transform.up);
            if (fwd.sqrMagnitude > 1e-6f)
            {
                fwd.Normalize();
                float sFL = Vector3.Dot(_legFL.CurrentPosition - _legFL.HomePosition, fwd);
                float sFR = Vector3.Dot(_legFR.CurrentPosition - _legFR.HomePosition, fwd);
                float sBL = Vector3.Dot(_legBL.CurrentPosition - _legBL.HomePosition, fwd);
                float sBR = Vector3.Dot(_legBR.CurrentPosition - _legBR.HomePosition, fwd);

                float amount = Mathf.Clamp01(planarSpeed / Mathf.Max(_gaitFullSpeed, 0.01f));
                float norm = 1f / Mathf.Max(2f * _gaitStride, 0.001f);
                front = Mathf.Clamp((sFL - sFR) * norm, -1f, 1f) * amount;
                back = Mathf.Clamp((sBL - sBR) * norm, -1f, 1f) * amount;
            }
        }

        float k = 1f - Mathf.Exp(-_gaitResponse * dt);
        _gaitFront = Mathf.Lerp(_gaitFront, front, k);
        _gaitBack = Mathf.Lerp(_gaitBack, back, k);
    }

    private void UpdateSpine(float dt, float yawRate)
    {
        float target = Mathf.Clamp(-yawRate * _spineBendPerTurn, -_spineMaxBendDeg, _spineMaxBendDeg);
        _spineBend = Mathf.Lerp(_spineBend, target, 1f - Mathf.Exp(-_spineResponse * dt));

        // Conformado: si las patas de adelante quedaron mas altas que las de atras (por
        // ejemplo empezando a subir un bloque), el cuerpo se dobla para acompanar en vez
        // de quedar rigido y tener que treparse de una pieza.
        float conformTarget = 0f;
        if (_conformEnabled)
        {
            // MeasureIncline() mide con la posición ACTUAL de los pies, y un pie en pleno
            // paso está en el aire, a mitad de camino de su arco — la lectura cruda salta
            // fuerte cada vez que una pata pisa. La suavizamos ANTES de decidir el signo del
            // boost de trepada: sin esto, un salto de ruido momentáneo hacía que el boost
            // "confirmara" la dirección equivocada y la columna se iba de -20° a +10° en
            // menos de un segundo trepando derecho por una pared plana.
            float rawIncline = MeasureIncline();
            _inclineSmoothed = Mathf.Lerp(_inclineSmoothed, rawIncline, 1f - Mathf.Exp(-6f * dt));

            float measured = _inclineSmoothed * _conformGain;
            // Empujón extra al trepar: curva la mitad delantera hacia la pared aunque las
            // patas todavía no hayan mandado el desnivel. Sigue el signo de la lectura de
            // patas si ya hay una; si no, asume "subiendo" (frente para arriba).
            if (_mover != null && _mover.IsClimbing && _climbConformBoostDeg > 0f)
            {
                float sign = Mathf.Abs(measured) > 0.5f ? Mathf.Sign(measured) : 1f;
                measured += sign * _climbConformBoostDeg;
            }
            conformTarget = Mathf.Clamp(measured, -_conformMaxDeg, _conformMaxDeg);
        }
        _conformAngle = Mathf.Lerp(_conformAngle, conformTarget, 1f - Mathf.Exp(-_conformResponse * dt));

        if (_spineChain == null || _spineChain.Length == 0) return;

        // Se reparte entre TODOS los huesos de la columna: el cuerpo se curva parejo en
        // vez de quebrarse en una sola articulacion.
        int count = _spineChain.Length;
        float yawPer = _spineBend / count;
        float pitchPer = _conformAngle / count;

        Quaternion pitch = Quaternion.AngleAxis(pitchPer, _pitchAxis);

        // S de la marcha: el giro ACUMULADO va de la señal de la cadera (Spine1) a la de los
        // hombros (Spine N). Cada hueso aporta solo la diferencia con el anterior, así el
        // cuerpo se curva parejo en vez de quebrarse en una articulación.
        float previousCumulative = 0f;
        for (int i = 0; i < count; i++)
        {
            if (_spineChain[i] == null) continue;

            float cumulative = 0f;
            if (_gaitUndulation && GaitLegsReady)
            {
                float t = (i + 1f) / count;
                cumulative = _gaitBendDeg * (_gaitBack * (1f - t) + _gaitFront * t);
            }

            Quaternion yaw = Quaternion.AngleAxis(yawPer + (cumulative - previousCumulative), _yawAxis);
            previousCumulative = cumulative;
            _gaitNetYaw = cumulative;

            _spineChain[i].localRotation = _spineChainRest[i] * yaw * pitch;
        }
    }

    /// <summary>
    /// Desnivel entre el promedio de los pies de adelante y el de atras, en grados.
    /// Positivo = el frente esta mas alto (subiendo). Se mide con las posiciones REALES
    /// de los pies, que ya siguen el terreno, asi que sirve igual en un escalon, en una
    /// rampa o en el borde de un bloque.
    /// </summary>
    private float MeasureIncline()
    {
        if (_legFL == null || _legFR == null || _legBL == null || _legBR == null) return 0f;

        Vector3 front = (_legFL.CurrentPosition + _legFR.CurrentPosition) * 0.5f;
        Vector3 back = (_legBL.CurrentPosition + _legBR.CurrentPosition) * 0.5f;
        Vector3 delta = front - back;

        float along = Mathf.Abs(Vector3.Dot(delta, transform.forward));
        float up = Vector3.Dot(delta, transform.up);
        if (along < 0.001f) return 0f;

        return Mathf.Atan2(up, along) * Mathf.Rad2Deg;
    }

    private void UpdateBob(float dt, float speed)
    {
        float amt = Mathf.Clamp01(speed / Mathf.Max(_bobFullSpeed, 0.01f));

        // distancia recorrida -> fase (así el bob va "al paso" y no por reloj)
        _bobPhase += speed * _bobCyclesPerMeter * Mathf.PI * 2f * dt;

        float bobY  = Mathf.Sin(_bobPhase) * _bobHeight * amt;
        float roll  = Mathf.Sin(_bobPhase * 0.5f) * _bobRollDeg * amt;
        float breath = Mathf.Sin(Time.time * _breathFreq * Mathf.PI * 2f) * _breathDeg * (1f - amt);

        if (_hips)
        {
            // Bob relativo al 'arriba' ACTUAL del cuerpo, no al de mundo. Con Vector3.up de
            // mundo, al trepar una pared (donde el 'arriba' del cuerpo está rotado 90°) el
            // mismo bob "vertical" empuja las caderas de costado/adelante en vez de hacia
            // afuera de la pared — se veía como un saltito en cada paso.
            Vector3 bobDir = _hips.parent != null
                ? _hips.parent.InverseTransformDirection(transform.up)
                : Vector3.up;
            _hips.localPosition = _hipsRestPos + bobDir * bobY;
            _hips.localRotation = _hipsRest * Quaternion.Euler(0f, 0f, roll);
        }
        if (_spine1 && Mathf.Abs(breath) > 0.0001f)
            _spine1.localRotation = _spine1.localRotation * Quaternion.Euler(breath, 0f, 0f);
    }

    private void UpdateHead(float dt, Vector3 vel, float speed)
    {
        if (_neck == null) return;

        Vector3 aimDir;
        if (_lookTarget != null)
            aimDir = _lookTarget.position - _neck.position;
        else if (speed > 0.05f)
            aimDir = vel;
        else
            aimDir = transform.forward;

        aimDir = aimDir.normalized;
        if (aimDir.sqrMagnitude < 0.001f) aimDir = transform.forward;

        // ángulo del aim relativo al cuerpo
        Vector3 local = transform.InverseTransformDirection(aimDir);
        float targetYaw   = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,  -_headMaxYawDeg,  _headMaxYawDeg);
        float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -_headMaxPitchDeg, _headMaxPitchDeg);

        float k = 1f - Mathf.Exp(-_headResponse * dt);
        _headYaw   = Mathf.Lerp(_headYaw, targetYaw, k);
        _headPitch = Mathf.Lerp(_headPitch, targetPitch, k);

        // Contra-giro: la S de la columna arrastra al cuello; se le resta para que la cabeza no
        // baile con el cuerpo (más firme = más lagarto).
        Quaternion counter = Quaternion.AngleAxis(-_gaitNetYaw * _headCounter, _neckYawAxis);
        _neck.localRotation = _neckRest * counter * Quaternion.Euler(_headPitch, _headYaw, 0f);
    }

    private void UpdateTail(float dt, float yawRate, float planarSpeed)
    {
        if (_tail == null) return;

        // el "latigazo": la cola se retrasa contra el giro del cuerpo
        float whipBase = -yawRate * _tailWhip;
        float idleSway = Mathf.Sin(Time.time * _tailIdleSwayFreq * Mathf.PI * 2f) * _tailIdleSwayDeg;

        // Vaiven lateral al caminar. La fase avanza con la DISTANCIA recorrida, no con el
        // reloj, asi la cola barre al ritmo del paso y no por su cuenta.
        float walkSway = 0f;
        if (_tailWalkSwayDeg > 0f)
        {
            if (_gaitUndulation && _tailFollowsGait && GaitLegsReady)
            {
                // En fase con los pasos: la cola barre al lado contrario del que gira la cadera.
                walkSway = -_gaitBack * _tailWalkSwayDeg;
            }
            else
            {
                _tailSwayPhase += planarSpeed * _tailWalkSwayCyclesPerMeter * Mathf.PI * 2f * dt;
                walkSway = Mathf.Sin(_tailSwayPhase) * _tailWalkSwayDeg;
            }
        }

        for (int i = 0; i < _tail.Length; i++)
        {
            if (_tail[i] == null) continue;

            float seg = i + 1f;
            float whip   = whipBase * Mathf.Pow(_tailWhipFalloff, i);
            float sway   = idleSway * (0.4f + 0.15f * seg);
            float walk   = walkSway * (0.35f + 0.18f * seg);
            float droop  = _tailDroopDeg * seg;

            // Objetivo LOCAL de este segmento respecto de su padre. Antes esto era un
            // Quaternion.Euler(droop, yaw, 0), que asume que el hueso tiene el eje Y
            // "hacia arriba" del bicho. En este modelo no es asi, y por eso la cola se
            // iba para arriba en vez de moverse de lado. Ahora el lateral gira sobre un
            // eje DERIVADO de la orientacion real del hueso, y la caida sobre otro.
            Quaternion lateral = Quaternion.AngleAxis(whip + sway + walk, _tailSwayAxis);
            Quaternion fall = Quaternion.AngleAxis(droop, _tailDroopAxis);
            Quaternion targetLocal = _tailRest[i] * lateral * fall;

            // resorte crítico-ish hacia el objetivo (integración semi-implícita)
            Quaternion diff = targetLocal * Quaternion.Inverse(_tailCur[i]);
            diff.ToAngleAxis(out float ang, out Vector3 axis);
            if (ang > 180f) ang -= 360f;
            if (float.IsInfinity(axis.x) || axis.sqrMagnitude < 0.0001f) { _tailCur[i] = targetLocal; _tailVel[i] = Vector3.zero; }
            else
            {
                Vector3 toTarget = axis.normalized * (ang * Mathf.Deg2Rad);
                Vector3 accel = toTarget * _tailStiffness - _tailVel[i] * _tailDamping;
                _tailVel[i] += accel * dt;
                Vector3 step = _tailVel[i] * dt * Mathf.Rad2Deg;
                _tailCur[i] = Quaternion.AngleAxis(step.magnitude, step.sqrMagnitude > 1e-8f ? step.normalized : Vector3.up) * _tailCur[i];
            }

            _tail[i].localRotation = _tailCur[i];
        }
    }

    // -------------------------------------------------------------------------
    private void AutoFill()
    {
        var all = GetComponentsInChildren<Transform>(true);
        if (_hips == null)   _hips   = Find(all, "Gecko_Hips");
        if (_spine1 == null) _spine1 = Find(all, "Gecko_Spine1");
        if (_spine2 == null) _spine2 = Find(all, "Gecko_Spine2");
        if (_neck == null)   _neck   = Find(all, "Gecko_Neck");
        if (_bodyRb == null) _bodyRb = GetComponent<Rigidbody>();
        if (_mover == null)  _mover  = GetComponent<GeckoMover>();

        if (_tail == null || _tail.Length == 0)
        {
            var list = new System.Collections.Generic.List<Transform>();
            for (int n = 1; n <= 8; n++)
            {
                var t = Find(all, "Gecko_Tail" + n);
                if (t == null) break;
                list.Add(t);
            }
            _tail = list.ToArray();
        }
    }

    /// <summary>Arma la cadena Spine1..SpineN siguiendo los nombres del rig.</summary>
    private void BuildSpineChain()
    {
        var all = GetComponentsInChildren<Transform>(true);
        var list = new System.Collections.Generic.List<Transform>();
        for (int n = 1; n <= 8; n++)
        {
            var t = Find(all, "Gecko_Spine" + n);
            if (t == null) break;
            list.Add(t);
        }

        _spineChain = list.ToArray();
        _spineChainRest = new Quaternion[_spineChain.Length];
        for (int i = 0; i < _spineChain.Length; i++)
            _spineChainRest[i] = _spineChain[i].localRotation;
    }

    /// <summary>
    /// Saca los ejes de giro de la orientacion REAL de los huesos en vez de asumir que
    /// el hueso tiene Y hacia arriba y X hacia el costado. Es lo que hacia que la cola se
    /// fuera para arriba: el Euler(pitch, yaw, 0) daba en los ejes equivocados.
    /// </summary>
    private void DeriveAxes()
    {
        if (_deriveSpineAxis && _spineChain != null && _spineChain.Length > 0)
        {
            Transform bone = _spineChain[0];
            Vector3 upLocal = bone.InverseTransformDirection(transform.up);
            Vector3 alongLocal = bone.childCount > 0
                ? bone.InverseTransformDirection((bone.GetChild(0).position - bone.position).normalized)
                : bone.InverseTransformDirection(transform.forward);

            _yawAxis = upLocal.normalized;                              // serpenteo lateral
            Vector3 pitch = Vector3.Cross(alongLocal, upLocal);         // levantar/bajar el frente
            _pitchAxis = pitch.sqrMagnitude > 1e-6f ? pitch.normalized : Vector3.right;
        }
        else
        {
            _yawAxis = Vector3.up;
            _pitchAxis = _spinePitchAxis.normalized;
        }

        if (_neck != null)
            _neckYawAxis = _neck.InverseTransformDirection(transform.up).normalized;

        if (_deriveTailAxes && _tail != null && _tail.Length > 0 && _tail[0] != null)
        {
            Transform bone = _tail[0];
            Vector3 upLocal = bone.InverseTransformDirection(transform.up);
            Vector3 alongLocal = bone.childCount > 0
                ? bone.InverseTransformDirection((bone.GetChild(0).position - bone.position).normalized)
                : bone.InverseTransformDirection(-transform.forward);

            // Girar sobre el "arriba" del cuerpo mueve la cola de lado a lado, que es lo
            // que se quiere. La caida va sobre el perpendicular.
            _tailSwayAxis = upLocal.normalized;
            Vector3 droop = Vector3.Cross(alongLocal, upLocal);
            _tailDroopAxis = droop.sqrMagnitude > 1e-6f ? droop.normalized : Vector3.right;
        }
        else
        {
            _tailSwayAxis = _tailSwayAxis.normalized;
            _tailDroopAxis = _tailDroopAxis.normalized;
        }
    }

    private static Transform Find(Transform[] all, string n)
    {
        foreach (var t in all) if (t.name == n) return t;
        return null;
    }

    private void OnDisable()
    {
        // volver a la pose de reposo para no dejar el bicho torcido en el editor
        if (!_ready) return;
        if (_hips)   { _hips.localRotation = _hipsRest; _hips.localPosition = _hipsRestPos; }
        if (_spine1) _spine1.localRotation = _spine1Rest;
        if (_spine2) _spine2.localRotation = _spine2Rest;
        if (_neck)   _neck.localRotation = _neckRest;
        if (_spineChain != null)
            for (int i = 0; i < _spineChain.Length; i++)
                if (_spineChain[i]) _spineChain[i].localRotation = _spineChainRest[i];
        if (_tail != null)
            for (int i = 0; i < _tail.Length; i++)
                if (_tail[i]) _tail[i].localRotation = _tailRest[i];
    }
}
