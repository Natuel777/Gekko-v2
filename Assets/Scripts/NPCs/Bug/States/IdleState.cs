using UnityEngine;

public class IdleState : IState
{
    private Bug _bug;

    public IdleState(Bug bug) {_bug = bug;}

    public void Enter() {_bug.wanderMovement?.Active(true);}

    public void Exit() {_bug.wanderMovement?.Active(false);}

    public void Update() {_bug.wanderMovement?.ArtificialUpdate();}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoEnter)
            _bug.SetState(_bug.AlertState);
    }
}