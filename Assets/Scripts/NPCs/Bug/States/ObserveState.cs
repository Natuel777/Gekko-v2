using UnityEngine;

public class ObserveState : IState
{
    private Bug _bug;

    public ObserveState(Bug bug) {_bug = bug;}

    public void Enter() {_bug.lookAt?.StartLooking(_bug.PlayerTransform);}

    public void Exit() {_bug.lookAt?.StopLooking();}

    public void Update() {_bug.lookAt?.ArtificialUpdate();}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _bug.SetState(_bug.IdleState);
    }
}
