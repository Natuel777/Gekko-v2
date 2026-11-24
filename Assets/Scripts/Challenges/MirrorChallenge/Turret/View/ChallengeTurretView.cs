using UnityEngine;

// Visual part of the turret. Plain class: ChallengeTurret creates it in Awake and the states use it (the logic doesn't touch the crystal).
// For now it only has the crystal's charge animation (EnergyBurstAnimation, on Crystal_lp), which runs while the turret is in
// TurretCharginState. The turret still works with no animation assigned: charging takes 0 s.
public class ChallengeTurretView
{
    private readonly EnergyBurstAnimation _chargeAnimation;

    // While the charge animation is running. TurretCharginState checks this to know when charging has finished.
    public bool IsCharging => _chargeAnimation != null && _chargeAnimation.IsPlaying;

    public ChallengeTurretView(EnergyBurstAnimation chargeAnimation)
    {
        _chargeAnimation = chargeAnimation;
    }

    public void PlayCharge()
    {
        if(_chargeAnimation != null) _chargeAnimation.Play();
    }

    // If charging has already finished it does nothing. Otherwise it cuts the animation and leaves the crystal as it was.
    public void StopCharge()
    {
        if(_chargeAnimation != null) _chargeAnimation.Stop();
    }
}
