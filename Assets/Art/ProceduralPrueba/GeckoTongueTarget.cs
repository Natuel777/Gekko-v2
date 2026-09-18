using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marca un objeto como objetivo de la lengua SIN escribir código. Se pone en el mismo
/// GameObject del collider (o en un padre). El collider tiene que estar en una capa que
/// GeckoTongue escanee (por defecto la capa "TongueTarget").
///
///  - Interaction = Reach: la lengua llega, dispara <see cref="_onTongueReached"/> y vuelve
///    (palancas, botones, puzzles).
///  - Interaction = Auto/Collect/Grapple/Damage: este componente solo ajusta punto y
///    prioridad; el comportamiento sale de IGeckoEdible / GeckoGrapplePoint / IDamageable.
/// </summary>
public class GeckoTongueTarget : MonoBehaviour, IGeckoTongueTarget, IGeckoTongueReachable
{
    [SerializeField] private GeckoTongueInteraction _interaction = GeckoTongueInteraction.Reach;
    [Tooltip("Punto exacto donde termina la punta de la lengua. Vacío = el punto más cercano del collider.")]
    [SerializeField] private Transform _tonguePoint;
    [Tooltip("Cada punto le resta este peso (metros) a la distancia al elegir el objetivo más cercano.")]
    [SerializeField] private float _priority;
    [SerializeField] private bool _targetable = true;
    [SerializeField] private UnityEvent _onTongueReached = new UnityEvent();

    public GeckoTongueInteraction Interaction => _interaction;
    public bool IsTargetable => _targetable && isActiveAndEnabled;
    public float Priority => _priority;

    public bool TryGetTonguePoint(out Vector3 point)
    {
        point = _tonguePoint != null ? _tonguePoint.position : default;
        return _tonguePoint != null;
    }

    public void SetTargetable(bool value) => _targetable = value;

    public void OnTongueReached(GeckoTongue tongue) => _onTongueReached?.Invoke();

    private void OnDrawGizmosSelected()
    {
        if (_tonguePoint == null) return;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(_tonguePoint.position, 0.05f);
    }
}
