using System;
using UnityEngine;

// "Destructor de escudo": algo que, al romperse (núcleo) o purificarse (torreta), baja un tramo del escudo de la planta de un
// Challenge. ShieldBreakerChallenge acepta cualquier componente que lo implemente, así un desafío puede mezclar tipos.
// transform lo implementa solo cualquier MonoBehaviour (lo usa la cámara para encuadrar al que se rompió).
public interface IShieldBreaker
{
    bool IsBroken { get; }
    event Action<IShieldBreaker> ShieldBreakerBroken;
    Transform transform { get; }
}
