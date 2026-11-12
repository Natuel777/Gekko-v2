using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Desafío genérico de "destructores de escudo": el escudo de la planta (VineShield y/o PurificationBarrier) tiene tantos
// tramos como destructores haya asignados, y cada uno que se rompe baja un tramo. Con 4 torretas: 1 -> 3/4 -> 1/2 -> 1/4 -> 0.
// Al romperse el último el escudo cae, la planta pasa a ser golpeable y corre el timeline de "escudo caído" (si hay uno).
// El resto del flujo (cierre al purificar la planta) lo hereda de Challenge.
// Destructor = cualquier componente que implemente IShieldBreaker (ChallengeTurret, PurificationCore...), y se pueden mezclar.
// La cinemática de cada ruptura (cámara a la planta mientras bajan las raíces) es aparte: ShieldBreakCinematic.
public class ShieldBreakerChallenge : Challenge
{
    [Header("Shield Breakers")]
    [Tooltip("Destructores de escudo: cualquier componente que implemente IShieldBreaker (torreta, núcleo...). El escudo baja un tramo por cada uno que se rompe, así que la cantidad de tramos es la cantidad de destructores. Se puede arrastrar el GameObject: si el componente que toma Unity no es un destructor, se busca uno en el mismo objeto.")]
    [FormerlySerializedAs("_turrets")]
    [SerializeField] private MonoBehaviour[] _shieldBreakers;

    private readonly List<IShieldBreaker> _breakers = new();
    private readonly HashSet<IShieldBreaker> _counted = new();
    private int _total, _remaining;

    public IReadOnlyList<IShieldBreaker> ShieldBreakers => _breakers;
    public int TotalBreakers => _total;
    public int RemainingBreakers => _remaining;

    protected override void Awake()
    {
        CollectBreakers();
        base.Awake();
    }

    private void CollectBreakers()
    {
        if(_shieldBreakers == null) return;

        foreach(MonoBehaviour component in _shieldBreakers)
        {
            IShieldBreaker breaker = Resolve(component);

            if(breaker == null)
            {
                if(component != null)
                    Debug.LogWarning($"[{name}] '{component.name}' no tiene ningún componente IShieldBreaker: no cuenta como destructor.", this);
                continue;
            }

            if(_breakers.Contains(breaker)) continue;

            _breakers.Add(breaker);
            _total++;

            // Los que ya arrancan rotos cuentan como tramos bajados (ver InitializeChallenge).
            if(breaker.IsBroken) _counted.Add(breaker);
            else _remaining++;
        }
    }

    private static IShieldBreaker Resolve(MonoBehaviour component)
    {
        if(component == null) return null;
        if(component is IShieldBreaker breaker) return breaker;

        return component.GetComponent<IShieldBreaker>();
    }

    protected override void InitializeChallenge()
    {
        // Sin destructores (o todos ya rotos) el escudo no tendría cómo caer.
        if(_remaining == 0)
        {
            DropShield(_total);
            return;
        }

        // Si alguno arrancó roto, el escudo arranca con esos tramos ya bajados.
        if(_remaining < _total) DissolveShield(_total - _remaining, _total);
    }

    protected override void OnSubscribe()
    {
        foreach(IShieldBreaker breaker in _breakers)
            breaker.ShieldBreakerBroken += OnBreakerBroken;
    }

    protected override void OnUnsubscribe()
    {
        foreach(IShieldBreaker breaker in _breakers)
            if(breaker as Object != null) breaker.ShieldBreakerBroken -= OnBreakerBroken;
    }

    private void OnBreakerBroken(IShieldBreaker breaker)
    {
        if(State != ChallengeState.Active || !_counted.Add(breaker)) return;

        _remaining = Mathf.Max(0, _remaining - 1);

        // Primero el aviso (la cinemática retiene el VineShield) y después el tramo.
        NotifyBreakerBroken(breaker, _remaining, _total);

        if(_remaining > 0)
        {
            DissolveShield(_total - _remaining, _total);
            return;
        }

        // El último tramo, al mismo ritmo que los anteriores.
        DropShield(_total);
        PlayShieldDownTimeline();
    }

    // Arrastrar un GameObject a un array de MonoBehaviour toma su primer componente: se cambia por el destructor del objeto.
    protected virtual void OnValidate()
    {
        if(_shieldBreakers == null) return;

        for(int i = 0; i < _shieldBreakers.Length; i++)
        {
            MonoBehaviour component = _shieldBreakers[i];
            if(component == null || component is IShieldBreaker) continue;

            if(component.GetComponent<IShieldBreaker>() is MonoBehaviour breaker) _shieldBreakers[i] = breaker;
            else Debug.LogWarning($"[{name}] '{component.name}' no tiene ningún componente IShieldBreaker (torreta, núcleo...).", this);
        }
    }
}
