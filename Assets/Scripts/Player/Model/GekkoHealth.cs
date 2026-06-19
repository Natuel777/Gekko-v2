using UnityEngine;

public class GekkoHealth : IDamageable
{
    private float _maxHealth = 100f, _currentHealth;
    private HealthBar _healthBar;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;

    public GekkoHealth(HealthBar healthBar)
    {
        _currentHealth = _maxHealth;
        _healthBar = healthBar;
        _healthBar.Initialize();
    }

    public void ArtificialUpdate() => _healthBar.ArtificialUpdate();

    public void ArtificialOnEnable()
    {
        EventManager.Subscribe<float>("OnPlayerDamaged", Damage);
        EventManager.Subscribe<float>("OnPlayerDamagedByBug", Damage);
    }

    public void ArtificialOnDisable()
    {
        EventManager.Unsubscribe<float>("OnPlayerDamaged", Damage);
        EventManager.Unsubscribe<float>("OnPlayerDamagedByBug", Damage);
    }

    public void Damage(float dmg)
    {
        Debug.Log("Gekko took " + dmg + " damage!");
        _currentHealth -= dmg;
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
        EventManager.Trigger<float>("OnCameraShake", 1f);
        _healthBar.TriggerDamagePulse();

        if(_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            GameManager.Instance.checkpointManager.Respawn();
        }
    }

    public void SetHealth(float value)
    {
        _currentHealth = Mathf.Clamp(value, 0f, _maxHealth);
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
    }

    public void Heal(float amount)
    {
        SetHealth(_currentHealth + amount);
    }
}
