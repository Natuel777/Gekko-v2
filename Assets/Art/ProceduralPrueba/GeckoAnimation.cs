using UnityEngine;

/// <summary>
/// Coordina las 4 patas procedurales del gecko.
///
/// Mueve las patas en PARES DIAGONALES:
///   - Diagonal A: delantera-izquierda + trasera-derecha
///   - Diagonal B: delantera-derecha  + trasera-izquierda
/// Solo un par puede estar dando un paso a la vez, así siempre quedan al menos
/// 3 patas apoyadas (marcha estable tipo trípode).
///
/// Se coloca en el GameObject raíz del Gecko. No depende de GeckoMover: mide la
/// velocidad comparando la posición del cuerpo entre frames, así funciona con
/// cualquier sistema de movimiento que uses después.
/// </summary>
public class GeckoAnimation : MonoBehaviour
{
    #region Variables
    [Header("Patas")]
    [SerializeField] private GeckoLeg _frontLeft;
    [SerializeField] private GeckoLeg _frontRight;
    [SerializeField] private GeckoLeg _backLeft;
    [SerializeField] private GeckoLeg _backRight;

    [Header("Cuerpo")]
    [Tooltip("Transform del cuerpo. Si se deja vacío, usa este mismo transform.")]
    [SerializeField] private Transform _body;
    [Tooltip("Suavizado de la velocidad medida, EN SEGUNDOS. Más alto = más suave pero con " +
             "más retraso. Por encima de ~0.15 las patas se enteran tarde de que el cuerpo " +
             "arrancó o frenó y el paso se descoordina. 0.05–0.08 es lo sano.")]
    [SerializeField] private float _velocitySmoothing = 0.06f;
    [Tooltip("Si está en el mismo GameObject, se usa su velocidad real en vez de medirla por " +
             "diferencia de posición: señal más limpia para el gait. Se engancha solo.")]
    [SerializeField] private GeckoMover _mover;

    [Header("Cadencia del paso")]
    [Tooltip("Desacopla el ritmo de las patas de la velocidad real del cuerpo. " +
             "1 = pisan al ritmo que corresponde a lo que se mueve. " +
             "0.8 = pisan como si fuera al 80%: mas lento y con zancadas mas largas, " +
             "aunque el personaje se mueva rapido. 1.5 = pasitos cortos y rapidos.")]
    [Range(0.2f, 3f)]
    [SerializeField] private float _gaitSpeedScale = 1f;

    [Tooltip("Al bajar la cadencia, alargar la zancada para que el pie no patine. " +
             "Apagalo solo si queres el deslizamiento a proposito.")]
    [SerializeField] private bool _compensateStride = true;

    [Tooltip("Sesgo de alternancia (metros). Al par que pisó último se le descuenta esta " +
             "urgencia para que el otro par tome el turno: da el trote parejo A-B-A-B en vez " +
             "de que un par acapare los pasos y el bicho renguee. 0 = elección pura por " +
             "urgencia. ~0.015 anda bien para este tamaño.")]
    [SerializeField] private float _alternationBias = 0.015f;

    private GeckoLeg[] _diagonalA;   // FL + BR
    private GeckoLeg[] _diagonalB;   // FR + BL

    private Vector3 _lastBodyPos;
    private Vector3 _velocity;
    private Vector3 _velocitySmoothVel;
    private int _lastPair = -1;      // 0 = A, 1 = B. Para alternar parejo y que un par no acapare.
    #endregion

    /// <summary>
    /// Ritmo de las patas respecto de la velocidad real. Se puede cambiar en vivo desde
    /// gameplay (por ejemplo, cadencia calmada al caminar y agitada al huir) sin tocar
    /// para nada la velocidad a la que se desplaza el personaje.
    /// </summary>
    public float GaitSpeedScale
    {
        get => _gaitSpeedScale;
        set => _gaitSpeedScale = Mathf.Clamp(value, 0.2f, 3f);
    }

    private void Awake()
    {
        if (_body == null) _body = transform;
        if (_mover == null) _mover = GetComponent<GeckoMover>();

        _diagonalA = new[] { _frontLeft, _backRight };
        _diagonalB = new[] { _frontRight, _backLeft };

        _lastBodyPos = _body.position;
    }

    private void Update()
    {
        // 1. Velocidad del cuerpo. Si hay GeckoMover se usa su velocidad real (señal
        //    limpia); si no, se mide por diferencia de posición (sirve con cualquier
        //    sistema de movimiento).
        Vector3 rawVelocity = _mover != null
            ? _mover.Velocity
            : (_body.position - _lastBodyPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastBodyPos = _body.position;
        _velocity = Vector3.SmoothDamp(_velocity, rawVelocity, ref _velocitySmoothVel, _velocitySmoothing);

        // 2. Cada pata recalcula su punto ideal y avanza el paso en curso.
        //    La cadencia se empuja cada frame para poder cambiarla en vivo.
        PushGait(_frontLeft); PushGait(_frontRight); PushGait(_backLeft); PushGait(_backRight);

        _frontLeft.ArtificialUpdate(_velocity);
        _frontRight.ArtificialUpdate(_velocity);
        _backLeft.ArtificialUpdate(_velocity);
        _backRight.ArtificialUpdate(_velocity);

        // 3. Si ningún par está en movimiento, arranca el que MÁS lo necesita.
        //    Elegir por urgencia (y no "siempre A primero") evita que un par
        //    acapare todos los pasos y el otro se quede clavado atrás.
        bool aStepping = IsPairStepping(_diagonalA);
        bool bStepping = IsPairStepping(_diagonalB);

        if (!aStepping && !bStepping)
        {
            float aUrgency = PairUrgency(_diagonalA);
            float bUrgency = PairUrgency(_diagonalB);

            // Sesgo de alternancia: al par que pisó último se le descuenta urgencia para
            // que el otro tome el turno. Trote parejo A-B-A-B en vez de renguera.
            if (_lastPair == 0) aUrgency -= _alternationBias;
            else if (_lastPair == 1) bUrgency -= _alternationBias;

            if (aUrgency > 0f || bUrgency > 0f)
            {
                bool stepA = aUrgency >= bUrgency;
                StepPair(stepA ? _diagonalA : _diagonalB);
                _lastPair = stepA ? 0 : 1;
            }
        }
    }

    private void PushGait(GeckoLeg leg)
    {
        if (leg == null) return;
        leg.GaitSpeedScale = _gaitSpeedScale;
        leg.CompensateStride = _compensateStride;
    }

    private static float PairUrgency(GeckoLeg[] pair)
    {
        float max = float.NegativeInfinity;
        foreach (var leg in pair)
            if (leg.StepUrgency > max) max = leg.StepUrgency;
        return max;
    }

    private static bool IsPairStepping(GeckoLeg[] pair)
    {
        foreach (var leg in pair)
            if (leg.IsStepping) return true;
        return false;
    }

    private static void StepPair(GeckoLeg[] pair)
    {
        foreach (var leg in pair)
            leg.TryStep();
    }
}
