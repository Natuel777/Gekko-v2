using UnityEngine;

// Hace que un hueso del esqueleto de la planta (en el boss, el joint7_R) mire al player mientras la planta le apunta y le
// dispara.
//
// Se aplica en LateUpdate, DESPUÉS del Animator: el Animator reescribe la pose de los huesos en cada frame, así que un giro
// hecho en Update se pisaría enseguida. Por eso el giro se calcula siempre sobre la pose que acaba de dejar la animación:
// se toma hacia dónde apunta hoy el eje del hueso y se lo gira lo justo para que apunte al player. Lo demás de la animación
// (el giro alrededor del eje, los huesos hijos como el Muzzle_VFX) se conserva.
public class PlantAimBehaviour
{
    private readonly Transform _joint;
    private readonly Vector3 _localAxis;
    private readonly float _turnSpeed;
    private readonly float _blendTime;

    // 0 = pose de la animación, 1 = mirando al player. Sube mientras apunta y baja al dejar de apuntar, así el hueso no pega
    // un salto al empezar ni al terminar.
    private float _weight;

    // Hacia dónde apunta el eje, en el mundo. Se acerca al player de a poco (ver ArtificialLateUpdate) en vez de clavarse en
    // él, para que la cabeza gire y no se teletransporte cuando el player se mueve rápido.
    private Vector3 _direction;
    private bool _hasDirection;

    // localAxis es el eje LOCAL del hueso que tiene que apuntar al player. turnSpeed es qué tan rápido se acerca (el mismo
    // Rotation Speed de la data que usa el _head).
    public PlantAimBehaviour(Transform joint, Vector3 localAxis, float turnSpeed, float blendTime)
    {
        _joint = joint;
        _localAxis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward;
        _turnSpeed = Mathf.Max(0f, turnSpeed);
        _blendTime = Mathf.Max(0f, blendTime);
    }

    // Lo llama CarnivorousPlant.LateUpdate en cada frame. aiming = la planta le está apuntando y disparando a target.
    public void ArtificialLateUpdate(Transform target, bool aiming)
    {
        if(_joint == null) return;

        bool active = aiming && target != null;
        float deltaTime = Time.deltaTime;

        _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, _blendTime > 0f ? deltaTime / _blendTime : 1f);

        if(_weight <= 0f)
        {
            // Sin aim: la próxima vez arranca desde donde la animación tenga el eje, sin saltos.
            _hasDirection = false;
            return;
        }

        // Hacia dónde apunta el eje con la pose que dejó la animación en este frame.
        Vector3 animatedAxis = _joint.TransformDirection(_localAxis);

        if(!_hasDirection)
        {
            _direction = animatedAxis;
            _hasDirection = true;
        }

        // Al dejar de apuntar la dirección se queda quieta mientras el peso baja: el hueso vuelve a la animación.
        if(active)
        {
            Vector3 toTarget = target.position - _joint.position;

            if(toTarget.sqrMagnitude > 0.0001f)
            {
                // 1 - e^(-v*t) se acerca al player igual de rápido con cualquier framerate.
                float k = 1f - Mathf.Exp(-_turnSpeed * deltaTime);
                _direction = Vector3.Slerp(_direction, toTarget.normalized, k);
            }
        }

        // El giro mínimo que lleva el eje de la animación hasta la dirección buscada, mezclado según el peso.
        Quaternion aimed = Quaternion.FromToRotation(animatedAxis, _direction) * _joint.rotation;
        _joint.rotation = Quaternion.Slerp(_joint.rotation, aimed, _weight);
    }
}
