using UnityEngine;

public class BeetleChargeState : IState
{
    private readonly HeavyBeetle _beetle;

    public BeetleChargeState(HeavyBeetle beetle) {_beetle = beetle;}
    public void Enter()
    {
        _beetle.SetCharging(true);
        _beetle.chargeMovement.StartCharge(_beetle.playerTransform.position);
    }

    public void Exit()
    {
        _beetle.SetCharging(false);
    }
    
    public void Update() 
    {
        _beetle.chargeMovement.ArtificialUpdate();

        if(_beetle.chargeMovement.ChargeDone) _beetle.SendEvent(CreatureEvent.ChargeMissed);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.ChargeHitPlayer)
            _beetle.SetState(_beetle.PatrolState);
        
        else if(evt == CreatureEvent.ChargeHitWall)
            _beetle.SetState(_beetle.DazedState);
        
        else if(evt == CreatureEvent.ChargeMissed)
            _beetle.SetState(_beetle.RecalibrateState);
    }
}
