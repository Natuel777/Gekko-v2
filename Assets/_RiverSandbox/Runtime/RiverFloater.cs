using UnityEngine;

// Hace flotar un Rigidbody en un SplineRiver y lo deja llevar por la corriente.
// El rio lo registra solo cuando el objeto entra a sus volumenes trigger.
[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("Gekko/River/River Floater")]
public class RiverFloater : MonoBehaviour
{
    [SerializeField, Tooltip(">1 flota, 1 queda suspendido, <1 se hunde despacio.")]
    float _buoyancy = 1.8f;

    [SerializeField, Min(0.01f), Tooltip("Mitad de la altura del objeto (para calcular cuanto esta sumergido).")]
    float _halfHeight = 0.15f;

    [SerializeField, Tooltip("Corrimiento de la linea de flotacion respecto del centro de masa.")]
    float _waterlineOffset;

    [SerializeField, Min(0f), Tooltip("Que tan rapido iguala la velocidad de la corriente.")]
    float _currentFollow = 1.2f;

    [SerializeField, Min(0f), Tooltip("Freno vertical dentro del agua.")]
    float _verticalDrag = 3f;

    [SerializeField, Min(0f), Tooltip("Freno de rotacion dentro del agua.")]
    float _angularDrag = 1.5f;

    [SerializeField, Min(0f), Tooltip("Giro lento sobre Y mientras flota (rad/s).")]
    float _spin = 0.25f;

    [SerializeField, Min(0f), Tooltip("Balanceo arriba/abajo en metros.")]
    float _bobAmplitude = 0.03f;

    [SerializeField, Min(0f), Tooltip("Fuerza que lo devuelve al centro cuando se acerca al borde.")]
    float _centering = 1f;

    Rigidbody _rb;
    SplineRiver _river;
    float _phase;

    public bool InWater { get; private set; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _phase = Random.value * 10f;
        if(Random.value < 0.5f)
            _spin = -_spin;
    }

    public void EnterRiver(SplineRiver river)
    {
        _river = river;
    }

    public void ExitRiver(SplineRiver river)
    {
        if(_river == river)
        {
            _river = null;
            InWater = false;
        }
    }

    void FixedUpdate()
    {
        InWater = false;
        if(_river == null || _rb.isKinematic)
            return;

        Vector3 center = _rb.worldCenterOfMass;
        if(!_river.Sample(center, out SplineRiver.RiverSample sample) || !sample.InsideWithMargin(SplineRiver.EdgeTolerance))
            return;

        float surface = sample.SurfaceY + Mathf.Sin(Time.time * 1.7f + _phase) * _bobAmplitude;
        float bottom = center.y + _waterlineOffset - _halfHeight;
        float submerged = Mathf.Clamp01((surface - bottom) / (_halfHeight * 2f));
        if(submerged <= 0f)
            return;

        InWater = true;

        _rb.AddForce(-Physics.gravity * (_buoyancy * submerged), ForceMode.Acceleration);

        Vector3 relative = sample.FlowVelocity - _rb.linearVelocity;
        Vector3 drag = new Vector3(relative.x * _currentFollow, relative.y * _verticalDrag, relative.z * _currentFollow);
        _rb.AddForce(drag * submerged, ForceMode.Acceleration);

        // En las curvas la inercia lleva los objetos hacia afuera: los devolvemos al centro antes de
        // que se salgan del agua (y en rios sin cauce, se caigan).
        _rb.AddForce(SplineRiver.CenteringAcceleration(sample) * _centering, ForceMode.Acceleration);

        Vector3 angular = -_rb.angularVelocity * _angularDrag;
        angular.y += (_spin - _rb.angularVelocity.y) * 0.5f;
        _rb.AddTorque(angular * submerged, ForceMode.Acceleration);
    }
}
