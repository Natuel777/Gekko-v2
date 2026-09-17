using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Sincroniza el "arriba" que usa Cinemachine (World Up Override, el objeto "worldUp") con
/// el '_currentUp' REAL de GeckoMover — pero SIN rolear la cámara en cada pared normal.
///
/// Primera versión de este script: seguía _currentUp al 100%. Eso hacía que al trepar una
/// pared (90° de diferencia con el mundo) la cámara ROLEE toda la vista con la pared —
/// el jugador ve el mundo entero inclinado, mareoso, injugable.
///
/// Réplica de CameraFollow.SmoothCameraUp() del personaje principal (que SÍ está probado
/// y anda bien en el juego real): para desviaciones normales (paredes, hasta
/// _angleThreshold° de diferencia con el mundo) la referencia de "arriba" se queda fija en
/// el mundo — la cámara NO rolea. Solo para casos extremos (casi boca abajo, tipo techo)
/// se adopta una versión recortada del up real, y aun así solo hasta _maxTiltAngle° de giro.
/// </summary>
public class GeckoCameraWorldUp : MonoBehaviour
{
    [Tooltip("Si se deja vacío, se busca solo el primer GeckoMover de la escena.")]
    [SerializeField] private GeckoMover _mover;
    [Tooltip("Qué tan rápido el 'arriba' de la cámara se ajusta.")]
    [SerializeField] private float _followSpeed = 2f;
    [Tooltip("Por debajo de esta diferencia (grados) respecto del 'arriba' del mundo, la " +
             "cámara NO rolea — se queda con el arriba del mundo. Una pared normal (90°) " +
             "cae en este caso a propósito, para no marear.")]
    [SerializeField] private float _angleThreshold = 120f;
    [Tooltip("Por encima del umbral, cuánto rota como máximo (grados) hacia el up real.")]
    [SerializeField] private float _maxTiltAngle = 90f;

    [Header("Rango vertical de cámara al trepar")]
    [Tooltip("El Cinemachine Orbital Follow que maneja el pitch de la cámara. Vacío = se " +
             "busca solo en la escena.")]
    [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
    [Tooltip("Rango del eje vertical mientras estás parado/en el piso (grados).")]
    [SerializeField] private Vector2 _groundedVerticalRange = new Vector2(0f, 80f);
    [Tooltip("Rango del eje vertical mientras trepás — más amplio, para poder mirar la " +
             "pared/techo de frente en vez de quedar clavado con el mismo pitch de siempre.")]
    [SerializeField] private Vector2 _climbingVerticalRange = new Vector2(-80f, 80f);
    [Tooltip("Umbral (0-1, dot con el up de mundo) por debajo del cual se considera " +
             "'suficientemente inclinado' como para ampliar el rango.")]
    [SerializeField] private float _uprightThreshold = 0.98f;

    private Vector3 _cameraUp = Vector3.up;

    private void Awake()
    {
        if (_mover == null) _mover = FindAnyObjectByType<GeckoMover>();
        if (_orbitalFollow == null) _orbitalFollow = FindAnyObjectByType<CinemachineOrbitalFollow>();
    }

    // En Update (no LateUpdate): tiene que estar listo ANTES de que Cinemachine posicione
    // la cámara ese mismo frame, igual que CameraFollow.SmoothCameraUp() en el juego real.
    private void Update()
    {
        if (_mover == null) return;

        Vector3 targetUp = _mover.CurrentUp;
        float angleFromWorld = Vector3.Angle(Vector3.up, targetUp);

        Vector3 blendTarget;
        if (angleFromWorld > _angleThreshold)
        {
            blendTarget = Vector3.Slerp(Vector3.up, targetUp, _maxTiltAngle / angleFromWorld);
        }
        else
        {
            blendTarget = Vector3.up;
        }

        _cameraUp = Vector3.Slerp(_cameraUp, blendTarget, _followSpeed * Time.deltaTime);
        transform.up = _cameraUp;

        UpdateVerticalRange(targetUp);
    }

    // Réplica de CameraFollow.UpdateVerticalRange() del personaje principal: mientras estás
    // parado o casi vertical, el pitch de cámara queda acotado (0-80°, mirar hacia arriba
    // nomás). Trepando, se abre a un rango completo para poder encarar la pared/techo.
    private void UpdateVerticalRange(Vector3 currentUp)
    {
        if (_orbitalFollow == null) return;
        float alignment = Vector3.Dot(currentUp.normalized, Vector3.up);
        bool grounded = _mover != null && _mover.Grounded;
        var axis = _orbitalFollow.VerticalAxis;
        axis.Range = (alignment >= _uprightThreshold || grounded) ? _groundedVerticalRange : _climbingVerticalRange;
        _orbitalFollow.VerticalAxis = axis;
    }
}
