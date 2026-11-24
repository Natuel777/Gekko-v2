using UnityEngine;

// Mirror logic: if active, it sends back (Reflect) whatever reflectable touches it. It knows nothing about challenges or specific bullets.
public class MirrorReflectBehaviour : IActivable
{
    // Squared distance, in meters, below which the bullet is already at the bounce point and there is nowhere to aim it.
    private const float MinAimSqrDistance = 0.0001f;

    private readonly Transform _bounceTarget;
    private bool _active;

    public bool IsActive => _active;

    // bounceTarget: the point the mirror aims whatever it sends back at. Null = it goes back the way it came (inverts its direction).
    public MirrorReflectBehaviour(Transform bounceTarget)
    {
        _bounceTarget = bounceTarget;
    }

    public void Activated(bool active = true) => _active = active;

    // true if it sent something back. Triggers that are not reflectable (Gekko, others) are ignored.
    public bool TryReflect(Collider other)
    {
        if(!_active) return false;

        if(!other.TryGetComponent(out IReflectable reflectable)) return false;

        // Towards the bounce point, from where it touched (the bullet is the collider's GameObject). With no point, or if it is already on top
        // of it, it goes back the way it came.
        if(TryGetBounceDirection(other.transform.position, out Vector3 direction)) reflectable.ReflectTowards(direction);
        else reflectable.Reflect();

        return true;
    }

    private bool TryGetBounceDirection(Vector3 from, out Vector3 direction)
    {
        direction = Vector3.zero;

        if(_bounceTarget == null) return false;

        Vector3 toTarget = _bounceTarget.position - from;

        if(toTarget.sqrMagnitude < MinAimSqrDistance) return false;

        direction = toTarget.normalized;
        return true;
    }
}
