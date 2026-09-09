using UnityEngine;

/// <summary>
/// Algo que el gecko puede comer con la lengua (un insecto, un collectible...).
/// GeckoTongue dispara la lengua, lo arrastra hasta la boca y llama a <see cref="Eat"/>.
/// </summary>
public interface IGeckoEdible
{
    /// <summary> Transform del bicho (para arrastrarlo). </summary>
    Transform Transform { get; }

    /// <summary> Se lo puede comer ahora mismo (no está ya siendo comido, etc.). </summary>
    bool CanBeEaten { get; }

    /// <summary> Lo agarró la lengua: dejá de moverte solo, apagá colisiones molestas, etc. </summary>
    void OnHooked();

    /// <summary> Llegó a la boca. Consumite (sumar score, VFX, Destroy...). </summary>
    void Eat();
}
