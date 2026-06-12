using UnityEngine;

public class PlantAttackState : IState
{
    private readonly CarnivorousPlant _plant;

    public PlantAttackState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter() {_plant.spitBehaviour.StartSpitting(_plant.playerTransform);}

    public void Exit() {_plant.spitBehaviour.StopSpitting();}

    public void Update()
    {
        if(_plant.IsPlayerInBiteRange())
        {
            _plant.SetState(_plant.BiteState);
            return;
        }

        _plant.spitBehaviour.ArtificialUpdate();
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
