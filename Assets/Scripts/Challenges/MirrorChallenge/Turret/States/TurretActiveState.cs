using UnityEngine;

public class TurretActiveState : IState
{
    private ChallengeTurret _parent;

    public TurretActiveState(ChallengeTurret p)
    {
        _parent = p;
    }

    public void Enter()
    {
        _parent.shoot.Activated(true);
    }

    public void Exit()
    {
        _parent.shoot.Activated(false);
    }

    public void Update()
    {
        _parent.shoot.ArtificialUpdate();

        if(_parent.shoot.BurstIndex >= 5) _parent.SendEvent(CreatureEvent.ChallengeTurretDischarged);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.ChallengeTurretPurified)
            _parent.SetState(_parent.purifiedState);

        if(evt == CreatureEvent.ChallengeTurretDischarged)
            _parent.SetState(_parent.charginState);
    }
}
