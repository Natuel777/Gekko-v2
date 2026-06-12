using UnityEngine;

public class PlantAlertState : IState
{
    private readonly CarnivorousPlant _plant;

    public PlantAlertState(CarnivorousPlant plant) {_plant = plant;}

    // La planta no tiene reacción probabilística como el Bug: siempre ataca.
    // El estado se mantiene por fidelidad al patrón (punto de extensión para
    // una animación de "despertar" antes de escupir).
    public void Enter() {_plant.SetState(_plant.AttackState);}

    public void Exit() {}

    public void Update() {}

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
