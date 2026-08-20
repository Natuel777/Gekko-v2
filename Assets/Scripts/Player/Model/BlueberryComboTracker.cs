using UnityEngine;

public class BlueberryComboTracker
{
    private int _comboCount;
    private bool _boostActive;

    private const int ComboTarget = 5;
    private const float BoostExtendPerBerry = 2f;
    private readonly NotificationSO _blueberry;

    private readonly PlayerController _controller;
    private PlayerViewer _pjViewer;
    private readonly GekkoHealth      _health;
    private float _boostTimeRemaining = 0f;

    public int ComboCount  => _comboCount;
    public bool BoostActive => _boostActive;

    public BlueberryComboTracker(PlayerController controller, GekkoHealth health, NotificationSO blueberr, PlayerViewer pjViewer)
    {
        _controller = controller;
        _health = health;
        _blueberry = blueberr;
        _pjViewer = pjViewer;
    }

    public void ArtificialOnEnable()
    {
        EventManager.Subscribe("PauseEvent", OnPause);
        EventManager.Subscribe("UnPauseEvent", OnUnpause);
    }

    public void ArtificialOnDisable()
    {
        EventManager.Unsubscribe("PauseEvent", OnPause);
        EventManager.Unsubscribe("UnPauseEvent", OnUnpause);
    }

    private void OnPause() => WindEffectController.SetActive(false);
    private void OnUnpause() { if (CheckBoost()) WindEffectController.SetActive(true); }

    public void ArtificialUpdate()
    {
        if (_boostTimeRemaining > 0f)
        {
            _boostTimeRemaining -= Time.deltaTime;
            if (_controller.IsMoving) if (!_pjViewer.IsTrailPlaying()) _pjViewer.PlayTrail();
            
            if(_boostTimeRemaining <= 0f)
            {
                _boostTimeRemaining = 0f;
                _controller.SetSpeedMultiplier(1f);
                if (_pjViewer.IsTrailPlaying()) _pjViewer.StopTrail();
                WindEffectController.SetActive(false);
            }
        }

        if (_boostActive && _boostTimeRemaining <= 0f)
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
            ExtendSpeedBoost(BoostExtendPerBerry);
            return;
        }

        _comboCount++;

        if (_comboCount >= ComboTarget) ActivateBoost(_blueberry.SpeedBoostMultiplier, _blueberry.SpeedBoostTimer);
    }

    public void ActivateBoost(float multiplier, float duration)
    {
        _boostActive = true;
        _comboCount  = 0;
        _boostTimeRemaining = duration;
        _controller.SetSpeedMultiplier(multiplier);
        WindEffectController.SetActive(true);
    }

    public void ExtendSpeedBoost(float extraSeconds)
    {
        _boostTimeRemaining += extraSeconds;
    }

    public void ResetSpeedBoost()
    {
        _controller.SetSpeedMultiplier(1f);
        _boostTimeRemaining = 0f;
        WindEffectController.SetActive(false);
    }

    public bool CheckBoost() => BoostActive;
}
