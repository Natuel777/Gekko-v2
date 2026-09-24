using UnityEngine;

// Oscilador armónico amortiguado (target fijo en 0). Lo usa GekkoSwinging para el "empujón" visual
// de la cuerda al engancharse: SetVelocity() dispara la oscilación, Update() la hace decaer con el tiempo.
public class Spring
{
    private float _strength;
    private float _damper;
    private float _target;
    private float _velocity;
    private float _value;
    public float Value => _value;

    public void Update(float deltaTime)
    {
        float direction = _target - _value >= 0f ? 1f : -1f;
        float force = Mathf.Abs(_target - _value) * _strength;
        _velocity += (force * direction - _velocity * _damper) * deltaTime;
        _value += _velocity * deltaTime;
    }

    public void Reset()
    {
        _velocity = 0f;
        _value = 0f;
    }

    public void SetTarget(float target) => _target = target;
    public void SetDamper(float damper) => _damper = damper;
    public void SetStrength(float strength) => _strength = strength;
    public void SetVelocity(float velocity) => _velocity = velocity;
}
