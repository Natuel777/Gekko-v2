using UnityEngine;

public class TurretCharginState : IState
{
    private ChallengeTurret _parent;

    public TurretCharginState(ChallengeTurret p)
    {
        _parent = p;
    }

    public void Enter()
    {
        _parent.view.PlayCharge();
    }

    public void Exit()
    {
        // Si la carga ya terminó no hace nada; si se sale antes (la purificaron en plena carga) la corta y deja el cristal como estaba.
        _parent.view.StopCharge();
    }

    public void Update()
    {
        // Cuando la animación de carga terminó, la torreta vuelve a disparar. Sin animación asignada la carga dura 0 s.
        if(!_parent.view.IsCharging) _parent.SendEvent(CreatureEvent.ChallengeTurretCharged);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.ChallengeTurretCharged)
            _parent.SetState(_parent.activeState);

        // La bala que le devuelve el espejo puede llegar justo mientras carga (las últimas balas de la ráfaga todavía vuelan).
        // Sin esto el evento se perdía, la torreta terminaba de cargar y volvía a disparar estando purificada.
        if(evt == CreatureEvent.ChallengeTurretPurified)
            _parent.SetState(_parent.purifiedState);
    }
}
