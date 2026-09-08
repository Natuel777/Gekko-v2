using UnityEngine;

// Detecta si hay piso adelante de un sensor (_detectGroundPosition) y, si no lo hay,
// calcula una dirección segura hacia la que sí hay piso. Pensada para NPCs cuyo
// movimiento no corrige Y por sí solo (Rigidbody kinematic, o transform.position manual)
// y que por eso pueden seguir caminando de largo por un borde/hueco del terreno.
public class GroundCheck
{
    private readonly Transform _transform;
    private readonly Transform _detectGroundPosition;
    private readonly float _groundCheckDistance;
    private readonly LayerMask _groundMask;

    private const float GroundCheckEpsilon = 0.0001f;
    private Vector3 _lastPosition;

    public GroundCheck(Transform transform, Transform detectGroundPosition, float groundCheckDistance, LayerMask groundMask)
    {
        _transform = transform;
        _detectGroundPosition = detectGroundPosition;
        _groundCheckDistance = groundCheckDistance;
        _groundMask = groundMask;
        _lastPosition = transform.position;
    }

    // Cada frame que la posición cambió desde el anterior, chequea que haya piso
    // debajo del sensor. Si no hay, devuelve true junto con una dirección segura
    // hacia la que sí hay piso (para que el caller redirija su movimiento activo).
    public bool TryGetSafeDirection(out Vector3 safeDirection)
    {
        safeDirection = Vector3.zero;

        if(_detectGroundPosition == null) return false;

        Vector3 pos = _transform.position;

        if((pos - _lastPosition).sqrMagnitude <= GroundCheckEpsilon * GroundCheckEpsilon)
            return false;

        _lastPosition = pos;

        if(Physics.Raycast(_detectGroundPosition.position, Vector3.down,
                _groundCheckDistance, _groundMask, QueryTriggerInteraction.Ignore))
            return false;

        safeDirection = FindGroundedDirection();
        return true;
    }

    private Vector3 FindGroundedDirection()
    {
        Vector3 fwd = _transform.forward;
        fwd.y = 0f;

        if(fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;

        fwd.Normalize();
        float ahead = 0.75f;
        float up = Mathf.Max(0.1f, _detectGroundPosition.position.y - _transform.position.y + 0.3f);
        float[] yaws = { 90f, -90f, 135f, -135f, 180f };

        foreach(float yaw in yaws)
        {
            Vector3 d = Quaternion.Euler(0f, yaw, 0f) * fwd;
            Vector3 origin = _transform.position + d * ahead + Vector3.up * up;

            if(Physics.Raycast(origin, Vector3.down, _groundCheckDistance + up,
                    _groundMask, QueryTriggerInteraction.Ignore))
                return d;
        }

        return -fwd;
    }
}
