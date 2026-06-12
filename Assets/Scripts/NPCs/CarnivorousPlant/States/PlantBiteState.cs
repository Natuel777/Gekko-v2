using UnityEngine;

// Ataque melee por proximidad (segundo umbral). No aplica daño directo: dispara la
// embestida del head (el daño + knockback ocurre en el pico del lunge, en el host).
public class PlantBiteState : IState
{
    private readonly CarnivorousPlant _plant;
    private float _biteTimer;

    public PlantBiteState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter()
    {
        _plant.spitBehaviour.StopSpitting();
        _biteTimer = 0f; // muerde ni bien entra al rango
    }

    public void Exit() {}

    public void Update()
    {
        // Si el player se alejó al rango medio, vuelve a escupir.
        if(!_plant.IsPlayerInBiteRange())
        {
            _plant.SetState(_plant.AttackState);
            return;
        }

        _biteTimer -= Time.deltaTime;

        if(_biteTimer <= 0f)
        {
            _plant.Bite();
            _biteTimer = _plant.data.biteInterval;
        }
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
