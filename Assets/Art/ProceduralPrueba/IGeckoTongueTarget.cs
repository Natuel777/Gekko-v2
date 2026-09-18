using UnityEngine;

/// <summary>
/// Qué hace la lengua al llegar a un objetivo. <c>Auto</c> deja que GeckoTongue lo deduzca de
/// los componentes que tenga el objeto (IGeckoEdible / Collectible -> Collect,
/// GeckoGrapplePoint -> Grapple, IDamageable -> Damage, y si nada aplica pero el objeto es un
/// IGeckoTongueTarget o tiene un tag configurado -> Reach).
/// </summary>
public enum GeckoTongueInteraction
{
    Auto,
    /// <summary> Se arrastra hasta la boca y se come (IGeckoEdible, Collectible del juego). </summary>
    Collect,
    /// <summary> La lengua se ancla y el gecko queda colgando (péndulo). </summary>
    Grapple,
    /// <summary> La punta golpea un IDamageable (enemigos corruptos, núcleos de purificación). </summary>
    Damage,
    /// <summary> La lengua solo llega y avisa (palancas, botones, puzzles). Extensible sin tocar GeckoTongue. </summary>
    Reach,
}

/// <summary>
/// Contrato OPCIONAL para que cualquier objeto del mundo sea un objetivo de la lengua y
/// controle cómo lo ve: punto de interacción propio, prioridad y si se puede apuntar ahora.
/// No hace falta para los tipos que la lengua ya entiende (IGeckoEdible, Collectible,
/// GeckoGrapplePoint, IDamageable): sirve para ajustarlos o para agregar tipos nuevos.
/// </summary>
public interface IGeckoTongueTarget
{
    /// <summary> Auto = deducir por los demás componentes del objeto. </summary>
    GeckoTongueInteraction Interaction { get; }

    /// <summary> Si false, la lengua lo ignora (ya usado, desactivado por un puzzle, etc.). </summary>
    bool IsTargetable { get; }

    /// <summary> Sesgo de selección: cada punto resta <c>PriorityWeight</c> metros a la distancia. </summary>
    float Priority { get; }

    /// <summary> Punto de interacción propio, si el objeto define uno. false = usar el punto por defecto. </summary>
    bool TryGetTonguePoint(out Vector3 point);
}

/// <summary>
/// Se llama cuando la punta de la lengua llega a un objetivo de tipo Reach. Es el gancho de
/// extensión para mecánicas futuras (puzzles, botones, palancas...).
/// </summary>
public interface IGeckoTongueReachable
{
    void OnTongueReached(GeckoTongue tongue);
}
