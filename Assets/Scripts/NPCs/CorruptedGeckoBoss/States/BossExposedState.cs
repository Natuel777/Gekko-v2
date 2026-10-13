using UnityEngine;

// Ventana de purificación: el jefe queda golpeable por CurrentPhase.windowDuration segundos. Si Gekko
// llega a los golpes necesarios (HitsNeededThisWindow, ya descontada la reducción por arándanos) antes de
// que se acabe el tiempo, limpia la fase; si no, vuelve a la tanda de ataque sin perder la fase.
public class BossExposedState : IState
{
    private readonly CorruptedGeckoBoss _boss;
    private float _timer;

    public BossExposedState(CorruptedGeckoBoss boss) {_boss = boss;}

    public void Enter()
    {
        _boss.SetExposed(true);
        _timer = _boss.CurrentPhase.windowDuration;
    }

    public void Exit() {_boss.SetExposed(false);}

    public void Update()
    {
        if(_boss.HitsThisWindow >= _boss.HitsNeededThisWindow)
        {
            _boss.ClearCurrentWindow();
            return;
        }

        _timer -= Time.deltaTime;

        if(_timer <= 0f)
            _boss.FailCurrentWindow();
    }

    public void HandleEvent(CreatureEvent evt, object data = null) {}
}
