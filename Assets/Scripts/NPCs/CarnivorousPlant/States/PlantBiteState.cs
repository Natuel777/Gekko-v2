using UnityEngine;

// Ataque melee por proximidad (segundo umbral). No aplica daño directo: dispara la
// embestida del head (el daño + knockback ocurre en el pico del lunge, en el host).
//
// Si la planta tiene PlantBiteWindup (hoy, el boss), cada mordisco va precedido de su animación de preparación: el mordisco
// sale recién cuando ésta termina. Sin el componente (o apagado) muerde de una, como siempre.
public class PlantBiteState : IState
{
    private readonly CarnivorousPlant _plant;
    private float _biteTimer;
    private bool _windingUp;

    public PlantBiteState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter()
    {
        _plant.spitBehaviour.StopSpitting();
        _biteTimer = 0f; // arranca el mordisco (con su preparación, si tiene) ni bien entra al rango
        _windingUp = false;
    }

    public void Exit()
    {
        // Se fue del rango (o la planta cambió de estado) en plena preparación: no hay mordisco y la pose vuelve sola a la base.
        if(!_windingUp) return;

        _windingUp = false;

        PlantBiteWindup windup = _plant.BiteWindup;
        if(windup != null) windup.Cancel();
    }

    public void Update()
    {
        // Si el player se alejó al rango medio, vuelve a escupir.
        if(!_plant.IsPlayerInBiteRange())
        {
            _plant.SetState(_plant.AttackState);
            return;
        }

        PlantBiteWindup windup = _plant.BiteWindup;

        if(_windingUp)
        {
            // El mordisco espera a que termine la preparación entera (Windup + Return).
            if(windup != null && windup.IsPlaying) return;

            _windingUp = false;

            // Si la cortó algo de afuera (un golpe de lengua que aturde) no hay mordisco: vuelve a prepararse.
            if(windup != null && windup.Completed) BiteNow();
            else _biteTimer = 0f;

            return;
        }

        _biteTimer -= Time.deltaTime;

        if(_biteTimer > 0f) return;

        // Sin preparación muerde de una. Con preparación la arranca; si Play() dice que no (todavía vuelve de una cortada)
        // se reintenta en el próximo frame.
        if(windup == null) BiteNow();
        else _windingUp = windup.Play();
    }

    private void BiteNow()
    {
        _plant.Bite();
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlantGrowl);
        _biteTimer = _plant.data.biteInterval;
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
