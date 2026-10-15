using Unity.Cinemachine;
using UnityEngine;

// Extensión de Cinemachine de la cámara de tercera persona. Va en el mismo GameObject que la CinemachineCamera, la
// CinemachineOrbitalFollow y CameraFollow (ThirdPersonManual). Hace dos cosas:
//
//  1) MIRAR HACIA ARRIBA DESDE EL PISO. Con Gekko en el piso la órbita no baja de 0° (ver
//     CameraFollow.UpdateVerticalRange): la cámara queda a ras del piso. Si con la cámara ahí abajo el jugador sigue
//     moviendo el mouse para mirar hacia arriba, la cámara ya no orbita: se queda donde está (puede seguir girando
//     horizontalmente) e inclina la vista hacia arriba, hasta _maxLookUpAngle. Al mover el mouse para el otro lado,
//     primero se deshace esa inclinación y recién después la cámara vuelve a orbitar hacia arriba de Gekko, como siempre.
//
//     Cómo: CameraFollow le abre al eje vertical un margen por debajo del piso (LookUpAllowed). Lo que el input baja el
//     eje por debajo del piso se le devuelve acá, antes de que la órbita lo use, y se convierte en inclinación. Así el
//     input pasa por el mismo camino de siempre (misma sensibilidad y aceleración del CinemachineInputAxisController) y
//     la cámara nunca llega a bajar del piso.
//
//  2) CHEQUEO DE PISO DE LA CÁMARA (CameraOnFloor): un rayo desde la cámara hacia "abajo" que dice si hay piso a menos de
//     _floorCheckDistance.
//
// La inclinación se aplica sobre la orientación de la cámara virtual, que es la misma que usa el gameplay (la dirección
// de la cámara para el swing, el segundo salto y el movimiento): mirar hacia arriba también apunta hacia arriba.
public class CameraRotation : CinemachineExtension
{
    [Header("Mirar hacia arriba desde el piso")]
    [Tooltip("Máximo que se inclina la vista hacia arriba (grados) con la cámara a ras del piso.")]
    [SerializeField, Range(0f, 89f)] private float _maxLookUpAngle = 45f;
    [Tooltip("Grados que se inclina la vista por cada grado que el mouse intenta bajar la órbita. 1 = la misma sensibilidad que la órbita.")]
    [SerializeField, Min(0f)] private float _lookUpSensitivity = 1f;
    [Tooltip("Velocidad (grados/s) a la que la vista vuelve a 0 cuando ya no se puede mirar hacia arriba (por ejemplo, al saltar).")]
    [SerializeField, Min(0f)] private float _lookUpReturnSpeed = 120f;

    [Header("Chequeo de piso")]
    [SerializeField] private float _floorCheckDistance = 0.8f;
    private LayerMask _floorLayers;
    private bool _cameraOnFloor;

    private CameraFollow _follow;
    private CinemachineOrbitalFollow _orbital;

    // Inclinación actual hacia arriba, en grados (0 = mirando a Gekko, como siempre).
    private float _lookUpAngle;

    public bool CameraOnFloor => _cameraOnFloor;
    public float LookUpAngle => _lookUpAngle;

    protected override void Awake()
    {
        base.Awake();
        _follow = GetComponent<CameraFollow>();
        _orbital = GetComponent<CinemachineOrbitalFollow>();
        _floorLayers = LayerMask.GetMask("Ground");
    }

    // Corre en cada actualización de la cámara, ANTES de que la órbita lea el eje vertical: acá se pasa a inclinación lo
    // que el input bajó el eje por debajo del piso (o se deshace la inclinación si el input lo sube).
    public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam, ref CameraState curState, float deltaTime)
    {
        if (_follow == null || _orbital == null) return;

        // deltaTime negativo = Cinemachine reinicia la cámara (se activó, teletransporte): arranca sin inclinación.
        if (deltaTime < 0f)
        {
            _lookUpAngle = 0f;
            return;
        }

        // Gekko en el aire o en una pared: no hay piso contra el que mirar hacia arriba. La vista vuelve sola a 0.
        if (!_follow.LookUpAllowed)
        {
            _lookUpAngle = Mathf.MoveTowards(_lookUpAngle, 0f, _lookUpReturnSpeed * deltaTime);
            return;
        }

        float floor = _follow.VerticalBottom;
        float value = _orbital.VerticalAxis.Value;

        if (value < floor)
        {
            // El input quiso bajar la cámara más allá del piso: eso se vuelve inclinación y la cámara se queda en el piso.
            _lookUpAngle += (floor - value) * _lookUpSensitivity;
            value = floor;
        }
        else if (value > floor && _lookUpAngle > 0f && _lookUpSensitivity > 0f)
        {
            // El input sube la cámara: primero se gasta en deshacer la inclinación, y solo lo que sobra la hace orbitar.
            float undo = Mathf.Min(_lookUpAngle, (value - floor) * _lookUpSensitivity);
            _lookUpAngle -= undo;
            value -= undo / _lookUpSensitivity;
        }

        // Pasado el máximo, el resto del input se descarta: la cámara queda en el piso y la vista no sube más.
        _lookUpAngle = Mathf.Clamp(_lookUpAngle, 0f, _maxLookUpAngle);
        _orbital.VerticalAxis.Value = value;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Aim) return;

        // Después del Aim (el HardLookAt ya apuntó a Gekko) se inclina la vista hacia arriba sobre el eje X de la cámara.
        // En Unity un ángulo positivo en X mira hacia abajo, por eso va con signo negativo.
        if (_lookUpAngle > 0f)
            state.RawOrientation *= Quaternion.AngleAxis(-_lookUpAngle, Vector3.right);

        // Chequeo de piso desde la posición final de la cámara (con las correcciones del Decollider), hacia abajo según el
        // "arriba" de la cámara.
        _cameraOnFloor = Physics.Raycast(state.GetFinalPosition(), -state.ReferenceUp,
            _floorCheckDistance, _floorLayers, QueryTriggerInteraction.Ignore);
    }
}
