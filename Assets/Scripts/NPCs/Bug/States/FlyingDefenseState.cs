using UnityEngine;

public class FlyingDefenseState : IState
{
    private Bug _bug;
    private float _timer;

    public FlyingDefenseState(Bug bug) {_bug = bug;}

    public void Enter()
    {
        _timer = 0f;
        _bug.defensiveFlyingBehaviour.StartFlyingDefense(_bug.PlayerTransform);
    }

    public void Exit()
    {
        _bug.SetBugAttacking(false);
        _bug.defensiveFlyingBehaviour.StopFlyingDefense();
    }

    public void Update()
    {
        _timer += Time.deltaTime;
        _bug.defensiveFlyingBehaviour.ArtificialUpdate();

        if(_timer >= _bug.bugData.defenseDuration) DecideNextState();
    }

    private void DecideNextState()
    {
        float distance = (_bug.PlayerTransform.position - _bug.transform.position).sqrMagnitude;

        if(distance < 4f)
            _bug.SetState(_bug.FleeState);
        
        else _bug.SetState(_bug.ObserveState);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _bug.SetState(_bug.IdleState);
    }
}
