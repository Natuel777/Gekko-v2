using UnityEngine;

public class GekkoHealth : IDamageable
{
    private float _maxHealth = 5f, _currentHealth;
    private HealthBar _healthBar;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;

    // For the health HUD (e.g. withering the matching leaf).
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
        // DamageFeedback's invulnerability window: neither health nor effects.
        if(DamageFeedback.Instance != null && DamageFeedback.Instance.IsInvulnerable) return;

        // Frozen for a cinematic (OnCinematicStart): he can't move or dodge, so nothing can hurt him (a turret bullet, a bite...).
        Player pj = GameManager.Instance != null ? GameManager.Instance.Pj : null;
        if(pj != null && pj.IsFrozenForCinematic) return;

        // Per-hit system: every hit costs a whole heart, no matter how much
        // "damage" the source declares (lava, bug, etc.) — the dmg value is no longer used.
        Debug.Log("Gekko took a hit!");
        bool wasAlive = _currentHealth > 0f;
        _currentHealth -= 1f;
        _currentHealth = Mathf.Max(_currentHealth, 0f);
        _healthBar.UpdateHealthBar(_currentHealth, _maxHealth);
        _healthBar.TriggerDamagePulse();
        OnDamaged?.Invoke(_currentHealth);

        bool isLethal = _currentHealth <= 0f;
        bool isRespawning = RespawnController.Instance != null && RespawnController.Instance.IsRunning;

        // Lost the last life: the defeat sound plays (SoundNames.PlayerLoose), once per death. It goes before the two paths below because
        // health can reach 0 with a normal hit (isLethal) or with the life cost of falling into a DeathZone, which arrives with the
        // respawn already running (isRespawning) and returns early. "wasAlive" prevents it from playing again if another hit comes in while
        // the respawn is still running and health is already 0 (there isLethal is true again).
        if(wasAlive && isLethal && AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerLoose);

        // Hit during the respawn (e.g. the life cost of falling into a DeathZone): it only lowers health. DamageFeedback doesn't belong here:
        // its hitstop leaves timeScale at 0 (the respawn has CanPause set to true and takes it as a pause) and its knockback gives the inputs back
        // in the middle of the transition. If it is lethal, the respawn that is already running restores the checkpoint's health.
        if(isRespawning)
        {
            if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlayerHurt);
            return;
        }

        // Killing hit: straight to the respawn transition (iris), without DamageFeedback's combo of
        // hitstop/knockback/shake/blink (pointless if it is already dying).
        if(isLethal)
        {
            if(RespawnController.Instance != null) RespawnController.Instance.TriggerRespawn(false);
            else GameManager.Instance.checkpointManager.Respawn();
        }
        else
        {
            // The hitstop/vignette/knockback/shake/invulnerability lives in DamageFeedback;
            // it replaces the loose shake that used to be here (it now uses a real Cinemachine Impulse).
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
