using UnityEngine;

public sealed class GekkoGroundCheck
{
    private readonly Transform _transform;
    private readonly CapsuleCollider _collider;
    private readonly LayerMask _groundMask;
    private readonly LayerMask _blockMask;

    public GekkoGroundCheck(Transform transform, CapsuleCollider collider, LayerMask groundMask, LayerMask blockMask)
    {
        _transform = transform;
        _collider = collider;
        _groundMask = groundMask;
        _blockMask = blockMask;
    }

    // Tres SphereCast hacia "abajo" (-currentUp, o sea hacia la superficie en la que está parado): uno adelante, uno al
    // centro y uno atrás del cuerpo. Está en el piso si alguno pega en una capa de piso y esa superficie no es muy
    // empinada (menos de 60° respecto del mundo).
    public bool IsGrounded(Vector3 currentUp)
    {
        float scale = _transform.lossyScale.x;
        float half = ((_collider.height / 2f) - _collider.radius) * scale;
        Vector3 center = _transform.TransformPoint(_collider.center);

        Vector3 front = center + _transform.forward * half;
        Vector3 back = center - _transform.forward * half;
        float dist = 0.15f * scale;
        float radius = _collider.radius * 0.9f * scale;

        bool g1 = CastSurface(front, radius, -currentUp, dist, _groundMask, out RaycastHit h1);
        bool g2 = CastSurface(center, radius, -currentUp, dist, _groundMask, out RaycastHit h2);
        bool g3 = CastSurface(back, radius, -currentUp, dist, _groundMask, out RaycastHit h3);

        if(!g1 && !g2 && !g3) return false;

        RaycastHit validHit = g2 ? h2 : (g1 ? h1 : h3);
        float angle = Vector3.Angle(validHit.normal, Vector3.up);
        return angle < 60f;
    }

    // SphereCast que solo cuenta si lo PRIMERO que golpea está en validMask. Es público porque PlayerController.DetectSurface
    // lo usa con otras máscaras (piso + paredes trepables).
    public bool CastSurface(Vector3 origin, float radius, Vector3 dir, float dist, LayerMask validMask, out RaycastHit hit)
    {
        if(Physics.SphereCast(origin, radius, dir, out hit, dist, _blockMask, QueryTriggerInteraction.Ignore))
        {
            return (validMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        }
        return false;
    }
}
