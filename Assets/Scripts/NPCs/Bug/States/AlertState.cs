using UnityEngine;

public class AlertState : IState
{
    private Bug _bug;
    private float _lastStateChangeTime;
 
    public AlertState(Bug bug) {_bug = bug;}
 
    public void Enter()
    {
        if(Time.time - _lastStateChangeTime < 0.2f)
            return;
 
        var reaction = PickReaction();
 
        switch(reaction)
        {
            case ReactionType.Flee :
                _bug.SetState(_bug.FleeState);
                break;
            /*case ReactionType.Defensive :
                _bug.SetState(_bug.DefensiveState);
                break;
            case ReactionType.DefensiveFlying :
                _bug.SetState(_bug.FlyingDefenseState);
                break;*/
            case ReactionType.Observe :
                _bug.SetState(_bug.ObserveState);
                break;
        }
        
        _lastStateChangeTime = Time.time;
    }
 
    public void Exit(){}
 
    public void Update(){}
 
    public void HandleEvent(CreatureEvent evt, object data = null) {}
 
    private ReactionType PickReaction()
    {
        float r = Random.value;

        if(_bug.IsFlyingBug)
        {
            if(r < _bug.bugData.fleeChance) return ReactionType.Flee;

            //if(r < _bug.bugData.fleeChance + _bug.bugData.defensiveFlyingChance)
                //return ReactionType.DefensiveFlying;

            return ReactionType.Observe;
        }

        else
        {
            if(r < _bug.bugData.fleeChance) return ReactionType.Flee;

            //if(r < _bug.bugData.fleeChance + _bug.bugData.defensiveChance)
                //return ReactionType.Defensive;

            return ReactionType.Observe;
        }
    }
}
 
public enum ReactionType
{
    Flee,
    //Defensive,
    Observe
    //DefensiveFlying
}
