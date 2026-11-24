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
        // If charging has already finished it does nothing; if it exits early (purified mid-charge) it cuts the animation and leaves the crystal as it was.
        _parent.view.StopCharge();
    }

    public void Update()
    {
        // When the charge animation has finished, the turret goes back to shooting. With no animation assigned, charging takes 0 s.
        if(!_parent.view.IsCharging) _parent.SendEvent(CreatureEvent.ChallengeTurretCharged);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.ChallengeTurretCharged)
            _parent.SetState(_parent.activeState);

        // The bullet the mirror sends back can arrive right while it is charging (the last bullets of the burst are still flying).
        // Without this the event was lost: the turret finished charging and went back to shooting while already purified.
        if(evt == CreatureEvent.ChallengeTurretPurified)
            _parent.SetState(_parent.purifiedState);
    }
}
