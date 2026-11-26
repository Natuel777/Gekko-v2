using UnityEngine;

// Proximity melee attack (second threshold, biteRange). It doesn't apply damage directly: it triggers the head's lunge, and the damage +
// knockback is applied by the HitBox (which the plant turns on during the lunge) if Gekko is inside it at that moment. Being in
// biteRange is enough for the plant to attack, but not for it to hit: Gekko also has to be inside the HitBox.
//
// If the plant has a PlantBiteWindup (today, the boss), each bite is preceded by its preparation animation: the bite only
// comes out when that one ends. Without the component (or turned off) it bites right away, as usual.
//
// It only lives while the plant can bite (CarnivorousPlant.CanMelee: Melee Attack on and no shield): if that stops being true, it goes back to spitting.
public class PlantBiteState : IState
{
    private readonly CarnivorousPlant _plant;
    private float _biteTimer;
    private bool _windingUp;

    public PlantBiteState(CarnivorousPlant plant) {_plant = plant;}

    public void Enter()
    {
        _plant.spitBehaviour.StopSpitting();
        _biteTimer = 0f; // the bite (with its preparation, if it has one) starts as soon as it enters the range
        _windingUp = false;
        _plant.SetInBiteInterval(false);
    }

    public void Exit()
    {
        _plant.SetInBiteInterval(false);

        // Gekko left the range (or the plant changed state) in the middle of the preparation: no bite, and the pose goes back to the base by itself.
        if(!_windingUp) return;

        _windingUp = false;

        PlantBiteWindup windup = _plant.BiteWindup;
        if(windup != null) windup.Cancel();
    }

    public void Update()
    {
        // If Gekko moved away to mid range, or the plant can't bite anymore (the shield went up, Melee Attack was turned off),
        // it goes back to spitting. Leaving cuts a preparation in progress (see Exit).
        if(!_plant.IsPlayerInBiteRange() || !_plant.CanMelee)
        {
            _plant.SetState(_plant.AttackState);
            return;
        }

        PlantBiteWindup windup = _plant.BiteWindup;

        if(_windingUp)
        {
            // The bite waits for the whole preparation to end (Windup + Return).
            if(windup != null && windup.IsPlaying) return;

            _windingUp = false;

            if(windup != null && windup.Completed) BiteNow();

            else _biteTimer = 0f;

            return;
        }

        _biteTimer -= Time.deltaTime;

        if(_biteTimer > 0f) return;

        // The interval is over: from here until the next bite (preparation + lunge) the plant can't be damaged.
        _plant.SetInBiteInterval(false);

        // Without a preparation it bites right away. With one it starts it; if Play() says no (it is still returning from a cut
        // one) it is retried on the next frame.
        if(windup == null) BiteNow();
        
        else _windingUp = windup.Play(_plant.playerTransform);
    }

    private void BiteNow()
    {
        _plant.Bite();
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlantGrowl);
        _biteTimer = _plant.data.biteInterval;

        // Starts the Bite Interval: the window in which Gekko can damage it (the lunge itself is still untouchable).
        _plant.SetInBiteInterval(true);
    }

    public void HandleEvent(CreatureEvent evt, object data = null)
    {
        if(evt == CreatureEvent.GekkoExit)
            _plant.SetState(_plant.IdleState);
    }
}
