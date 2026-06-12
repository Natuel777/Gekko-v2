using UnityEngine;

public class PlantIdleState : IState
{
    private readonly CarnivorousPlant _plant;

    public PlantIdleState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter() {}

    public void Exit() {}

    public void Update() {}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoEnter)
            _plant.SetState(_plant.AlertState);
    }
}
