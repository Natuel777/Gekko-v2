using UnityEngine;

// Estado terminal: la planta queda neutralizada permanentemente (no se destruye).
public class PlantPurifiedState : IState
{
    private readonly CarnivorousPlant _plant;

    public PlantPurifiedState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter()
    {
        _plant.spitBehaviour.StopSpitting();
        _plant.PlayPurifiedFeedback();
    }

    public void Exit() {}

    public void Update() {}

    public void HandleEvent(CreatureEvent evt, object data = null) {}
}
