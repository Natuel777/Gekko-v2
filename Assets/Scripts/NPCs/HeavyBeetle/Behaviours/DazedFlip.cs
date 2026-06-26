using UnityEngine;

// Flip "panza arriba" por código para el HeavyBeetle mareado. Rota suave 180° sobre
// el eje Z LOCAL (se da vuelta), se mantiene, y al recibir la orden se endereza a 0°.
// Sólo toca Z (roll); el yaw/pitch que trae de la embestida se preservan.
// Mismo estilo plain-C# que ChargeMovement/WanderMovement: lo construye HeavyBeetle
// y lo maneja BeetleDazedState vía ArtificialUpdate().
public class DazedFlip
{
    private const float FlippedZ = 180f;
    private const float UprightZ = 0f;
    private const float DoneEpsilon = 0.5f; // localEulerAngles está cuantizado; nunca cae exacto

    private readonly Transform _transform;
    private readonly float _flipSpeed; // grados / segundo

    private float _targetZ;

    public bool RecoverDone { get; private set; }

    public DazedFlip(Transform transform, float flipSpeed)
    {
        _transform = transform;
        _flipSpeed = flipSpeed;
    }

    public void StartFlip()
    {
        _targetZ = FlippedZ;
        RecoverDone = false;
    }

    public void StartRecover()
    {
        _targetZ = UprightZ;
        RecoverDone = false;
    }

    public void ArtificialUpdate()
    {
        Vector3 euler = _transform.localEulerAngles;
        float newZ = Mathf.MoveTowardsAngle(euler.z, _targetZ, _flipSpeed * Time.deltaTime);
        euler.z = newZ;
        _transform.localEulerAngles = euler;

        if (!RecoverDone && Mathf.Approximately(_targetZ, UprightZ) &&
            Mathf.Abs(Mathf.DeltaAngle(newZ, UprightZ)) <= DoneEpsilon)
        {
            euler.z = UprightZ; // snap exacto a 0 para handoff limpio al wander
            _transform.localEulerAngles = euler;
            RecoverDone = true;
        }
    }
}
