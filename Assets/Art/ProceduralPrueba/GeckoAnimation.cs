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
    [Tooltip("Suavizado de la velocidad medida. Más alto = más suave pero con más retraso.")]
    [SerializeField] private float _velocitySmoothing = 0.1f;

    private GeckoLeg[] _diagonalA;   // FL + BR
    private GeckoLeg[] _diagonalB;   // FR + BL

    private Vector3 _lastBodyPos;
    private Vector3 _velocity;
    private Vector3 _velocitySmoothVel;
    #endregion

    private void Awake()
    {
        if (_body == null) _body = transform;

        _diagonalA = new[] { _frontLeft, _backRight };
        _diagonalB = new[] { _frontRight, _backLeft };

        _lastBodyPos = _body.position;
    }

    private void Update()
    {
        // 1. Velocidad del cuerpo (sirve con cualquier sistema de movimiento).
        Vector3 rawVelocity = (_body.position - _lastBodyPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastBodyPos = _body.position;
        _velocity = Vector3.SmoothDamp(_velocity, rawVelocity, ref _velocitySmoothVel, _velocitySmoothing);

        // 2. Cada pata recalcula su punto ideal y avanza el paso en curso.
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

            if (aUrgency > 0f || bUrgency > 0f)
                StepPair(aUrgency >= bUrgency ? _diagonalA : _diagonalB);
        }
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
