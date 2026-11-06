using UnityEngine;

// Desafío del campamento: la barrera que protege a la planta cae cuando todas las torretas están purificadas. Nada más: el
// resto del flujo (planta blindada mientras tanto, cierre al purificarla) lo hereda de Challenge. Las torretas se asignan en
// la escena, así que la cantidad (hoy 4) no está en el código.
public class MirrorChallenge : Challenge
{
    [Header("Turrets")]
    [Tooltip("Torretas que protegen a la planta. La barrera cae cuando todas están purificadas.")]
    [SerializeField] private ChallengeTurret[] _turrets;

    private int _remaining;

    protected override void Awake()
    {
        base.Awake();

        foreach(ChallengeTurret turret in _turrets)
        {
            if(turret != null && !turret.IsPurified) _remaining++;
        }
    }

    protected override void InitializeChallenge()
    {
        // Sin torretas (o ya purificadas) la barrera no tendría cómo caer.
        if(_remaining == 0) DropShield(1);
    }

    protected override void OnSubscribe()
    {
        foreach(ChallengeTurret turret in _turrets)
            if(turret != null) turret.Purified += OnTurretPurified;
    }

    protected override void OnUnsubscribe()
    {
        foreach(ChallengeTurret turret in _turrets)
            if(turret != null) turret.Purified -= OnTurretPurified;
    }

    // Sin tramos intermedios: la barrera cae de una sola vez al purificar la última, en un solo tramo (DropShield(1)).
    private void OnTurretPurified(ChallengeTurret turret)
    {
        if(State != ChallengeState.Active) return;

        _remaining = Mathf.Max(0, _remaining - 1);

        if(_remaining == 0) DropShield(1);
    }
}
