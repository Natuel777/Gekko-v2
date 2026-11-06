using UnityEngine;

// Lógica del espejo: si está activo, devuelve (Reflect) lo reflejable que lo toca. No sabe de desafíos ni de balas concretas.
public class MirrorReflectBehaviour : IActivable
{
    private bool _active;

    public bool IsActive => _active;

    public void Activated(bool active = true) => _active = active;

    // true si devolvió algo. Los triggers que no son reflejables (Gekko, otros) se ignoran.
    public bool TryReflect(Collider other)
    {
        if(!_active) return false;

        if(!other.TryGetComponent(out IReflectable reflectable)) return false;

        reflectable.Reflect();
        return true;
    }
}
