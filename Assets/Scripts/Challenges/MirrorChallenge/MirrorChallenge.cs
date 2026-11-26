// Desafío del campamento: las torretas (ChallengeTurret) son los destructores de escudo. Cada torreta purificada baja un
// tramo de las raíces de la planta (con 4 torretas, 4 tramos) y con la última cae el escudo. Toda esa lógica es la genérica
// de ShieldBreakerChallenge; acá va lo propio del campamento cuando haga falta. Las torretas se asignan en la escena (campo
// "Shield Breakers", que antes se llamaba _turrets: Unity conserva las que ya estaban).
using UnityEngine;

public class MirrorChallenge : ShieldBreakerChallenge
{
    [Header("Planta encerrada")]
    [Tooltip("La planta no ataca (ni escupe ni muerde) mientras está encerrada por las enredaderas (VineShield). Arranca a atacar cuando terminan de bajar del todo.")]
    [SerializeField] private bool _plantWaitsForVines = true;

    private VineShield _waitingShield;

    protected override void Start()
    {
        base.Start();

        if(!_plantWaitsForVines || Plant == null || VineShield == null || VineShield.IsDown) return;

        Plant.SetAttackLocked(true);
        _waitingShield = VineShield;
        _waitingShield.Dropped += OnVinesDown;
    }

    private void OnVinesDown(VineShield shield)
    {
        shield.Dropped -= OnVinesDown;
        _waitingShield = null;
        if(Plant != null) Plant.SetAttackLocked(false);
    }

    protected override void OnDestroy()
    {
        if(_waitingShield != null) _waitingShield.Dropped -= OnVinesDown;
        base.OnDestroy();
    }
}
