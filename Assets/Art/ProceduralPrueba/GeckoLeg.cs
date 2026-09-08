using UnityEngine;

/// <summary>
/// Planificador de apoyo de UNA pata. Se coloca en el empty "Home_*" (el punto
/// de reposo de la pata). No tiene Update() propio: GeckoAnimation le maneja el
/// ciclo con ArtificialUpdate(), siguiendo el patrón del resto del proyecto.
///
/// Idea general:
///  - Cada frame se tira un raycast desde el Home hacia -body.up para saber
///    dónde DEBERÍA apoyar el pie (punto "ideal").
///  - Mientras el pie no da un paso, su posición world queda clavada (no desliza).
///  - Cuando el pie quedó demasiado lejos del ideal, GeckoAnimation llama a
///    TryStep() y la pata interpola de la posición vieja a la nueva en un arco.
/// </summary>
public class GeckoLeg : MonoBehaviour
{
    #region Variables
    [Header("Referencias")]
    [Tooltip("El Target del Two Bone IK Constraint de esta pata.")]
    [SerializeField] private Transform _ikTarget;
    [Tooltip("Transform del cuerpo del gecko. Define qué dirección es 'abajo'.")]
    [SerializeField] private Transform _body;
    [Tooltip("Capas contra las que la pata detecta piso / pared / techo.")]
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("Paso")]
    [Tooltip("Distancia que el pie se puede alejar del punto ideal antes de dar un paso.")]
    [SerializeField] private float _stepDistance = 0.3f;
    [Tooltip("Altura del arco que describe el pie al dar el paso.")]
    [SerializeField] private float _stepHeight = 0.15f;
    [Tooltip("Duración del paso en segundos. Más chico = más rápido.")]
    [SerializeField] private float _stepDuration = 0.18f;
    [Tooltip("Cuánto se adelanta el pie en la dirección del movimiento (multiplica la velocidad del cuerpo).")]
    [SerializeField] private float _overshoot = 0.35f;

    [Header("Adaptación a la velocidad")]
    [Tooltip("Velocidad del cuerpo (m/s) para la que están calibrados _stepDuration y la zancada. " +
             "Por encima de este valor los pasos se hacen más rápidos y más largos para que las " +
             "patas acompañen y no se queden atrás.")]
    [SerializeField] private float _referenceSpeed = 0.6f;
    [Tooltip("Duración mínima del paso a alta velocidad (segundos). Es el tope de qué tan rápido puede pisar.")]
    [SerializeField] private float _minStepDuration = 0.045f;
    [Tooltip("Cuánto se alarga la zancada por cada m/s de velocidad del cuerpo.")]
    [SerializeField] private float _stepStretch = 0.02f;
    [Tooltip("Tope de la zancada (umbral de disparo del paso). Limitado por el alcance real de la pata.")]
    [SerializeField] private float _maxStepDistance = 0.06f;
    [Tooltip("Tope de cuánto se adelanta el pie respecto al cuerpo. Limitado por el alcance real de la pata.")]
    [SerializeField] private float _maxLead = 0.045f;
    [Tooltip("Distancia máxima a la que se permite que el pie se aleje del Home. Si por ir muy " +
             "rápido la pata no llega, el pie DESLIZA en vez de estirar la pata hasta romperla. " +
             "Ponelo cerca del largo real de la pata.")]
    [SerializeField] private float _maxReach = 0.07f;
    [Tooltip("A alta velocidad el pie NO sigue al milímetro: el IK target persigue la posición " +
             "del pie con un RETARDO suave. Este es el retardo máximo (segundos), que se alcanza a " +
             "_lagSpeedRange m/s por encima de _referenceSpeed. Da la sensación de ir rápido sin " +
             "que se sienta roto (bicho chico, plataformero).")]
    [SerializeField] private float _maxFootLag = 0.12f;
    [Tooltip("Cuántos m/s por encima de _referenceSpeed hacen falta para llegar al retardo máximo.")]
    [SerializeField] private float _lagSpeedRange = 1.5f;

    [Header("Alcance real de la cadena IK")]
    [Tooltip("Hueso raiz de la cadena IK: el muslo. Si se asigna, la pata mide su alcance " +
             "REAL (muslo-rodilla-tobillo) y nunca deja que el pie se aleje mas de lo que el " +
             "IK puede resolver. Es distinto de Max Reach, que se mide desde el Home: el Home " +
             "no es el hombro, asi que un pie dentro de ese limite puede quedar mucho mas lejos " +
             "del muslo de lo que la pata da, y ahi el IK se satura, la rodilla no puede doblar " +
             "y la pata tiembla en cada paso.")]
    [SerializeField] private Transform _ikRoot;
    [Tooltip("Fraccion del alcance real que se permite usar. En 1 la pata se estira recta; " +
             "por debajo queda margen para que la rodilla doble.")]
    [Range(0.5f, 1f)]
    [SerializeField] private float _reachUsage = 0.9f;

    [Header("Raycast de detección")]
    [Tooltip("Cuánto por encima del Home arranca el rayo.")]
    [SerializeField] private float _rayUpOffset = 0.35f;
    [Tooltip("Largo total del rayo hacia -body.up.")]
    [SerializeField] private float _rayLength = 1.2f;

    [Header("Extras")]
    [Tooltip("Si está activo, el pie rota para acompañar la normal de la superficie.")]
    [SerializeField] private bool _matchSurfaceRotation = true;
    [Tooltip("Corrección de orientación del pie (Euler, en el espacio del target). Se usa para " +
             "compensar la pose de bind del hueso: el mesh tiene las patas en T, así que hay que " +
             "rotar el target para que la planta mire al piso. Las patas izq/der suelen necesitar " +
             "valores espejados.")]
    [SerializeField] private Vector3 _footRotationOffset = Vector3.zero;

    // --- Estado interno ---
    private Vector3 _currentPos;          // posición world del pie (lo que recibe el IK target)
    private Quaternion _currentRot;
    private Vector3 _stepFromPos, _stepToPos;
    private Quaternion _stepFromRot, _stepToRot;
    private float _stepLerp = 1f;         // progreso del paso [0..1]. 1 = plantado.

    private Vector3 _idealPos;            // dónde debería estar el pie ahora mismo
    private Quaternion _idealRot;
    private bool _hasGround;

    // Valores efectivos recalculados cada frame según la velocidad del cuerpo.
    private float _effStepDuration;
    private float _effStepDistance;
    private float _bodySpeed;
    private float _chainReach = -1f;
    private Vector3 _targetPosVel;   // estado interno del SmoothDamp del IK target
    #endregion

    #region Propiedades públicas
    /// <summary>
    /// Escala de CADENCIA. La setea GeckoAnimation cada frame. 1 = la pata usa la
    /// velocidad real del cuerpo. 0.8 = la pata pisa como si el cuerpo fuera al 80%,
    /// aunque en realidad se mueva más rápido: pasos más lentos y más largos.
    /// </summary>
    public float GaitSpeedScale { get; set; } = 1f;

    /// <summary>
    /// Si está activo, bajar la cadencia ALARGA la zancada en la misma proporción, para
    /// que el pie siga cayendo donde corresponde y no patine. Es lo que hace que bajar
    /// la cadencia se vea como zancadas largas y no como el bicho deslizándose.
    /// </summary>
    public bool CompensateStride { get; set; } = true;

    public bool IsStepping => _stepLerp < 1f;
    public bool HasGround => _hasGround;
    public Vector3 CurrentPosition => _currentPos;

    /// <summary> True si el pie está lejos del punto ideal y conviene dar un paso. </summary>
    public bool WantsToStep
    {
        get
        {
            if (IsStepping || !_hasGround) return false;
            return Vector3.Distance(_currentPos, _idealPos) > _effStepDistance;
        }
    }

    /// <summary>
    /// Cuánto se pasó el pie del umbral de paso (metros). Negativo = todavía no
    /// necesita moverse. GeckoAnimation lo usa para elegir qué par de patas mover
    /// primero y así alternar en vez de que un par acapare los pasos.
    /// </summary>
    public float StepUrgency
    {
        get
        {
            if (IsStepping || !_hasGround) return -999f;
            return Vector3.Distance(_currentPos, _idealPos) - _effStepDistance;
        }
    }
    #endregion

    private void Start()
    {
        if (_body == null)
        {
            Debug.LogError($"[GeckoLeg] '{name}' no tiene asignado el campo Body.", this);
            enabled = false;
            return;
        }

        MeasureChainReach();

        _effStepDuration = _stepDuration;
        _effStepDistance = _stepDistance;

        // Arrancamos con el pie ya apoyado en su punto ideal.
        RecalculateIdeal(Vector3.zero);
        _currentPos = _hasGround ? _idealPos : transform.position;
        _currentRot = _hasGround ? _idealRot : _body.rotation;
        ApplyToTarget();
    }

    /// <summary>
    /// Llamado por GeckoAnimation cada frame ANTES de decidir pasos.
    /// Recalcula el punto ideal y avanza el paso si hay uno en curso.
    /// </summary>
    public void ArtificialUpdate(Vector3 bodyVelocity)
    {
        RecalculateIdeal(bodyVelocity);

        if (IsStepping)
        {
            _stepLerp += Time.deltaTime / Mathf.Max(_effStepDuration, 0.0001f);
            float clamped = Mathf.Clamp01(_stepLerp);
            float t = Mathf.SmoothStep(0f, 1f, clamped);

            Vector3 flat = Vector3.Lerp(_stepFromPos, _stepToPos, t);
            float arc = Mathf.Sin(clamped * Mathf.PI) * _stepHeight;
            _currentPos = flat + _body.up * arc;
            _currentRot = Quaternion.Slerp(_stepFromRot, _stepToRot, t);

            if (_stepLerp >= 1f)
            {
                _stepLerp = 1f;
                _currentPos = _stepToPos;
                _currentRot = _stepToRot;
            }
        }
        // Si NO está dando un paso, _currentPos queda clavado en el mundo (el pie no desliza).

        // Red de seguridad: si el pie quedó más lejos del Home que _maxReach (porque el
        // cuerpo va más rápido de lo que la pata puede seguir), acercamos SOLO la parte
        // "horizontal" (perpendicular a body.up) al borde del alcance. El pie desliza un
        // poco pero la pata nunca se estira de más ni se da vuelta el IK; la altura queda
        // como estaba (no lo levantamos del piso / la pared).
        Vector3 fromHome = _currentPos - transform.position;
        Vector3 upComp = Vector3.Project(fromHome, _body.up);
        Vector3 flatComp = fromHome - upComp;
        if (flatComp.magnitude > _maxReach)
            _currentPos = transform.position + upComp + flatComp.normalized * _maxReach;

        // Tope duro por el alcance REAL de la cadena IK, medido desde el muslo. Sin esto
        // el pie puede pedirle a la pata mas de lo que da: el IK queda saturado, la
        // rodilla no puede doblar y aparece el temblequeo al caminar.
        if (_ikRoot != null && _chainReach > 0f)
        {
            Vector3 fromRoot = _currentPos - _ikRoot.position;
            Vector3 upPart = Vector3.Project(fromRoot, _body.up);
            Vector3 flatPart = fromRoot - upPart;

            // Solo se recorta la parte HORIZONTAL. Escalar el vector entero acercaba el
            // pie al muslo tambien en vertical y lo levantaba del piso: el bicho quedaba
            // en puntas de pie. Lo que se achica es el ancho de la parada, no la altura.
            float limit = _chainReach * _reachUsage;
            float maxFlat = Mathf.Sqrt(Mathf.Max(limit * limit - upPart.sqrMagnitude, 0f));
            if (flatPart.magnitude > maxFlat)
                _currentPos = _ikRoot.position + upPart + flatPart.normalized * maxFlat;
        }

        ApplyToTarget();
    }

    /// <summary> Arranca un paso hacia el punto ideal actual (si corresponde). </summary>
    public void TryStep()
    {
        if (IsStepping || !_hasGround) return;

        _stepFromPos = _currentPos;
        _stepFromRot = _currentRot;
        _stepToPos = _idealPos;
        _stepToRot = _idealRot;
        _stepLerp = 0f;
    }

    /// <summary>
    /// Suma los dos segmentos de la cadena IK (muslo->rodilla->tobillo). Se mide una sola
    /// vez, en la pose de bind, que es cuando la jerarquia todavia no fue tocada por nada.
    /// </summary>
    private void MeasureChainReach()
    {
        _chainReach = -1f;
        if (_ikRoot == null || _ikRoot.childCount == 0) return;

        Transform mid = _ikRoot.GetChild(0);
        if (mid.childCount == 0) return;
        Transform tip = mid.GetChild(0);

        _chainReach = Vector3.Distance(_ikRoot.position, mid.position)
                    + Vector3.Distance(mid.position, tip.position);
    }

    private void RecalculateIdeal(Vector3 bodyVelocity)
    {
        Vector3 origin = transform.position + _body.up * _rayUpOffset;
        _hasGround = Physics.Raycast(origin, -_body.up, out RaycastHit hit,
            _rayLength, _groundMask, QueryTriggerInteraction.Ignore);

        // Gait adaptativo: cuanto más rápido va el cuerpo, más corto y más largo el paso,
        // para que las patas acompañen la velocidad y no se queden atrás.
        float speed = bodyVelocity.magnitude;
        _bodySpeed = speed;

        // La CADENCIA se calcula con la velocidad escalada: es lo que permite que el
        // bicho corra rápido pero pise con el ritmo de una caminata.
        float scale = Mathf.Max(GaitSpeedScale, 0.05f);
        // Primero la cadencia que corresponde a la velocidad real (el gait adaptativo de
        // siempre): cuanto mas rapido va el cuerpo, mas corto el paso.
        float baseDuration = Mathf.Max(_minStepDuration,
            _stepDuration * Mathf.Clamp01(_referenceSpeed / Mathf.Max(speed, 0.001f)));

        // Y encima el multiplicador de "velocidad de animacion". Va DESPUES del clamp a
        // proposito: aplicado antes, el piso _minStepDuration se comia el efecto y a alta
        // velocidad bajar la cadencia casi no se notaba.
        // 0.8 = el paso dura 1/0.8 = 25% mas, igual que poner un clip al 80%.
        _effStepDuration = baseDuration / scale;

        // La ZANCADA se calcula con la velocidad REAL, y se alarga al bajar la cadencia.
        // Si no se compensara, pisar más lento mientras el cuerpo va igual de rápido
        // haría que el pie se quede atrás y termine deslizando contra _maxReach.
        float strideScale = CompensateStride ? 1f / scale : 1f;
        _effStepDistance = Mathf.Min(_maxStepDistance, (_stepDistance + speed * _stepStretch) * strideScale);

        if (!_hasGround) return;

        // Adelanto en la dirección del movimiento, proyectado sobre la superficie
        // para que el pie no se clave dentro ni flote sobre la pared. Se limita a
        // _maxLead para no pedirle a la pata más de lo que su largo permite.
        Vector3 lead = Vector3.ClampMagnitude(
            Vector3.ProjectOnPlane(bodyVelocity, hit.normal) * _overshoot, _maxLead);
        _idealPos = hit.point + lead;

        if (_matchSurfaceRotation)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(_body.forward, hit.normal);
            if (fwd.sqrMagnitude < 0.0001f) fwd = _body.forward;
            _idealRot = Quaternion.LookRotation(fwd, hit.normal) * Quaternion.Euler(_footRotationOffset);
        }
        else
        {
            _idealRot = _body.rotation * Quaternion.Euler(_footRotationOffset);
        }
    }

    private void ApplyToTarget()
    {
        if (_ikTarget == null) return;

        // Retardo proporcional a la velocidad: a velocidad normal el pie sigue exacto;
        // yendo rápido, el IK target persigue la posición del pie con un SmoothDamp, así
        // las patas "arrastran" suave detrás del cuerpo en vez de ir clavadas al compás.
        float lag = _maxFootLag *
            Mathf.Clamp01((_bodySpeed - _referenceSpeed) / Mathf.Max(_lagSpeedRange, 0.001f));

        if (lag > 0.0005f)
        {
            _ikTarget.position = Vector3.SmoothDamp(_ikTarget.position, _currentPos, ref _targetPosVel, lag);
            if (_matchSurfaceRotation)
            {
                float rotBlend = 1f - Mathf.Exp(-Time.deltaTime / lag);
                _ikTarget.rotation = Quaternion.Slerp(_ikTarget.rotation, _currentRot, rotBlend);
            }
        }
        else
        {
            _ikTarget.position = _currentPos;
            _targetPosVel = Vector3.zero;
            if (_matchSurfaceRotation) _ikTarget.rotation = _currentRot;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_body == null) return;

        Vector3 origin = transform.position + _body.up * _rayUpOffset;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(origin, origin - _body.up * _rayLength);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_idealPos, 0.03f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(_idealPos, _stepDistance);

            Gizmos.color = IsStepping ? Color.red : Color.green;
            Gizmos.DrawSphere(_currentPos, 0.03f);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.03f);
        }
    }
}
