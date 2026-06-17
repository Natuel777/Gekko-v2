using UnityEngine;

public class BlueberryComboTracker
{
    private int   _comboCount;
    private bool  _boostActive;

    private const int   ComboTarget         = 5;
    private const float BoostExtendPerBerry = 2f;
    private readonly NotificationSO _blueberry;

    private readonly PlayerController _controller;
    private readonly GekkoHealth      _health;

    public int  ComboCount  => _comboCount;
    public bool BoostActive => _boostActive;

    public BlueberryComboTracker(PlayerController controller, GekkoHealth health, NotificationSO blueberr)
    {
        _controller = controller;
        _health     = health;
        _blueberry = blueberr;
    }

    public void ArtificialUpdate()
    {
        if (_boostActive && _controller.BoostTimeRemaining <= 0f)
            _boostActive = false;
    }

    public void ResetCombo()
    {
        _comboCount = 0;
    }

    public void OnCollect()
    {
        bool atFullHealth = Mathf.Approximately(_health.CurrentHealth, _health.MaxHealth);

        if (!atFullHealth)
        {
            _health.Heal(_health.MaxHealth * 0.2f);
            ResetCombo();
            return;
        }

        if (_boostActive)
        {
            _controller.ExtendSpeedBoost(BoostExtendPerBerry);
            return;
        }

        _comboCount++;

        if (_comboCount >= ComboTarget) ActivateBoost();
    }

    private void ActivateBoost()
    {
        _controller.ApplySpeedBoost(_blueberry.SpeedBoostMultiplier, _blueberry.SpeedBoostTimer);
        _boostActive = true;
        _comboCount  = 0;
    }

}
