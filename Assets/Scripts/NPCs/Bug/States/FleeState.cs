using UnityEngine;

public class FleeState : IState
{
    private Bug _bug;

    public FleeState(Bug bug) {_bug = bug;}

    public void Enter() 
    {
        _bug.fleeMovement?.StartFlee(_bug.PlayerTransform);
        _bug.wanderMovement?.Active(false);
        _bug.fleeMovement.Active(true);
    }

    public void Exit() 
    {
        _bug.fleeMovement?.StopFlee();
        _bug.fleeMovement.Active(false);
    }

    public void Update() {_bug.fleeMovement?.ArtificialUpdate();}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _bug.SetState(_bug.IdleState);
    }
}
