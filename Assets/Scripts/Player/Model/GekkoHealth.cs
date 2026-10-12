using UnityEngine;

public class GekkoHealth : IDamageable
{
    private float _maxHealth = 100f, _currentHealth;
    private HealthBar _healthBar;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;

    // Para el HUD de vida (ej. marchitar la hoja correspondiente).
    public event System.Action<float> OnDamaged;

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
        // Ventana de invulnerabilidad del DamageFeedback: ni vida ni efectos.
        if(DamageFeedback.Instance != null && DamageFeedback.Instance.IsInvulnerable) return;

        Debug.Log("Gekko took " + dmg + " damage!");
        _currentHealth -= dmg;
        _currentHealth = Mathf.Max(_currentHealth, 0f);
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
        _healthBar.TriggerDamagePulse();
        OnDamaged?.Invoke(_currentHealth);

        bool isLethal = _currentHealth <= 0f;

        // Golpe que mata: directo a la transición de respawn (iris), sin el combo de
        // hitstop/empujón/temblor/parpadeo del DamageFeedback (no tiene sentido si ya se muere).
        if(isLethal)
        {
            if(RespawnController.Instance != null) RespawnController.Instance.TriggerRespawn(false);
            else GameManager.Instance.checkpointManager.Respawn();
        }
        else
        {
            // El hitstop/vignette/empujón/temblor/invulnerabilidad vive en DamageFeedback;
            // reemplaza el shake suelto que había acá (ahora usa Cinemachine Impulse de verdad).
            DamageFeedback.Instance?.TriggerHit();
        }

        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerHurt);
    }

    public void SetHealth(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, _maxHealth);
        if (clamped > _currentHealth) _healthBar.TriggerHealPulse();
        _currentHealth = clamped;
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
    }

    public void Heal(float amount)
    {
        SetHealth(_currentHealth + amount);
    }
}
