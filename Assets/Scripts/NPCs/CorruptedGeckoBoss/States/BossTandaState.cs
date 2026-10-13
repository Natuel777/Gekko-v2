using UnityEngine;

// Tanda de ataque: golpea CurrentPhase.attacksPerTanda veces cada attackInterval y pasa a la ventana de
// exposición. El ataque en sí es un placeholder (RaiseAttackBeat) hasta definir el ataque real del jefe.
public class BossTandaState : IState
{
    private readonly CorruptedGeckoBoss _boss;
    private int _attacksDone;
    private float _timer;

    public BossTandaState(CorruptedGeckoBoss boss) {_boss = boss;}

    public void Enter()
    {
        _attacksDone = 0;
        _timer = 0f; // golpea ni bien entra a la tanda
        _boss.RaiseTandaStarted();
    }

    public void Exit() {}

    public void Update()
    {
        _timer -= Time.deltaTime;

        if(_timer > 0f) return;

        _boss.RaiseAttackBeat();
        _attacksDone++;
        _timer = _boss.CurrentPhase.attackInterval;

        if(_attacksDone >= _boss.CurrentPhase.attacksPerTanda)
            _boss.SetState(_boss.ExposedState);
    }

    public void HandleEvent(CreatureEvent evt, object data = null) {}
}
