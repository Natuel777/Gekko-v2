using UnityEngine;

public class PlantAttackState : IState
{
    private readonly CarnivorousPlant _plant;

    public PlantAttackState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter()
    {
        if(_plant.CanSpit) _plant.spitBehaviour.StartSpitting(_plant.playerTransform);
    }

    public void Exit() {_plant.spitBehaviour.StopSpitting();}

    public void Update()
    {
        // Only bites if it can (Melee Attack on and no shield). If it can't, even point blank the plant keeps spitting, if it can.
        if(_plant.CanMelee && _plant.IsPlayerInBiteRange())
        {
            _plant.SetState(_plant.BiteState);
            return;
        }

        // Can't spit now (shield up or Spit Attack off): no preparation, no shot, and the head doesn't aim. If it was in the middle
        // of a preparation, it is cut. When it can spit again it starts over from a full cooldown, so nothing comes out the
        // very instant the shield falls.
        if(!_plant.CanSpit)
        {
            _plant.spitBehaviour.StopSpitting();
            return;
        }

        // It entered this state without a target (the shield was up): it picks it up as soon as it can spit.
        if(!_plant.spitBehaviour.IsSpitting) _plant.spitBehaviour.StartSpitting(_plant.playerTransform);

        _plant.spitBehaviour.ArtificialUpdate();
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
