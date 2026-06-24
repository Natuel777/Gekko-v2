using UnityEngine;

public class ObstacleAvoidance
{
    private readonly Transform _transform;
    private readonly float _range;

    private readonly LayerMask _obstacleMask;

    // obstacleMask: capas que cuentan como obstáculo a esquivar. Si se pasa vacía (0),
    // cae al comportamiento histórico (solo "Wall") para no romper a otros usuarios.
    public ObstacleAvoidance(Transform t, float range, LayerMask obstacleMask = default)
    {
        _transform = t;
        _range = range;
        _obstacleMask = obstacleMask.value != 0 ? obstacleMask : (1 << LayerMask.NameToLayer("Wall"));
    }

    public Vector3 Avoid(Vector3 desiredDir)
    {
        desiredDir.y = 0f;
        
        if(desiredDir.sqrMagnitude < 0.001f) return desiredDir;
        
        desiredDir.Normalize();
        Vector3 origin = _transform.position + Vector3.up * 0.3f;
        Vector3 leftDir  = Quaternion.Euler(0, -45f, 0) * desiredDir;
        Vector3 rightDir = Quaternion.Euler(0,  45f, 0) * desiredDir;
        Vector3 steer = Vector3.zero;
        steer += GetSteer(origin, desiredDir, 1f);
        steer += GetSteer(origin, leftDir,    0.5f);
        steer += GetSteer(origin, rightDir,   0.5f);

        if(steer.sqrMagnitude < 0.001f) return desiredDir;

        Vector3 result = desiredDir + steer;
        result.y = 0f;
        return result.sqrMagnitude > 0.001f ? result.normalized : desiredDir;
    }

    // Raycast hacia 'dir': true si hay un obstáculo (cualquier collider sólido que no sea
    // el propio bug ni el Player) dentro del rango. Usado por Wander/Flee para cambiar de rumbo.
    public bool IsBlocked(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return false;
        dir.Normalize();

        Vector3 origin = _transform.position + Vector3.up * 0.3f;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, _range, ~0, QueryTriggerInteraction.Ignore))
            return !hit.transform.IsChildOf(_transform) && !hit.transform.CompareTag("Player");

        return false;
    }

    private Vector3 GetSteer(Vector3 origin, Vector3 dir, float weight)
    {
        if(!Physics.Raycast(origin, dir, out RaycastHit hit, _range, _obstacleMask, QueryTriggerInteraction.Ignore))
            return Vector3.zero;
        Vector3 normal = hit.normal;
        normal.y = 0f;
        return normal.normalized * (1f - hit.distance / _range) * weight;
    }
}