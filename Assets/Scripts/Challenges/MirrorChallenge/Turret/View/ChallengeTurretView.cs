using UnityEngine;

// Parte visual de la torreta. Clase plana: la crea ChallengeTurret en Awake y la usan los estados (la lógica no toca el cristal).
// Hoy solo tiene la animación de carga del cristal (EnergyBurstAnimation, en Crystal_lp), que corre mientras la torreta está en
// TurretCharginState. Sin animación asignada la torreta igual funciona: la carga dura 0 s.
public class ChallengeTurretView
{
    private readonly EnergyBurstAnimation _chargeAnimation;

    // Mientras la animación de carga está corriendo. Es lo que mira TurretCharginState para saber cuándo terminó la carga.
    public bool IsCharging => _chargeAnimation != null && _chargeAnimation.IsPlaying;

    public ChallengeTurretView(EnergyBurstAnimation chargeAnimation)
    {
        _chargeAnimation = chargeAnimation;
    }

    public void PlayCharge()
    {
        if(_chargeAnimation != null) _chargeAnimation.Play();
    }

    // Si la carga ya terminó no hace nada. Si no, la corta y deja el cristal como estaba.
    public void StopCharge()
    {
        if(_chargeAnimation != null) _chargeAnimation.Stop();
    }
}
