using UnityEngine;

public class BeetlePatrolState : IState
{
    private readonly HeavyBeetle _beetle;

    public BeetlePatrolState(HeavyBeetle beetle) {_beetle = beetle;}

    public void Enter()
    {
        _beetle.SetCharging(false);
        _beetle.SetDazed(false);
        _beetle.SetAngry(false);
    }

    public void Exit(){}
    
    public void Update() {_beetle.wanderMovement.ArtificialUpdate();}

    public void HandleEvent(CreatureEvent evt, object data = null) 
    {
        if(_beetle.IsPurified) return;
        
        if(evt == CreatureEvent.GekkoEnter)
            _beetle.SetState(_beetle.AlertState);
    }
}
