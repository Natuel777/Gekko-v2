using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class HealthBar
{
    [SerializeField] private Slider _healthBar;
    [SerializeField] private Image _fillImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField, Range(0f, 1f)] private float _idleOpacity = 0.5f;
    [SerializeField] private float _lerpSpeed = 5f;
    [SerializeField] private float _damageFadeDelay = 1f;
    [SerializeField] private float _opacityLerpSpeed = 3f;
    [SerializeField] private float _pulsePeriod = 1f;
    [SerializeField] private float _pulseScale = 0.1f;
    [SerializeField] private Color _damageColor = Color.red;
    [SerializeField] private Color _healColor = Color.green;

    private const float _criticalThreshold = 0.25f;
    private float _targetValue = 1f;
    private float _currentDisplayValue = 1f;
    private bool _isDamaged = false;
    private float _damageTimer = 0f;
    private float _pulseTime = 0f;
    private Vector3 _baseScale;
    private Color _fillBaseColor;
    private bool _isPulsing = false;
    private float _pulseTimer = 0f;
    private bool _isHealPulsing = false;
    private float _healPulseTimer = 0f;

    public void Initialize()
    {
        if (_healthBar == null) return;
        _baseScale = _healthBar.transform.localScale;
        if (_fillImage != null) _fillBaseColor = _fillImage.color;
        _targetValue = 1f;
        _currentDisplayValue = 1f;
        _healthBar.value = 1f;
        if (_canvasGroup != null) _canvasGroup.alpha = _idleOpacity;
    }

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if(_healthBar == null) return;

        _targetValue = currentHealth / maxHealth;

        if(currentHealth >= maxHealth)
        {
            _isDamaged = false;
            return;
        }

        _isDamaged = true;
        _damageTimer = _damageFadeDelay;

        if(_canvasGroup != null) _canvasGroup.alpha = 1f;
    }

    public void ArtificialUpdate()
    {
        if(_healthBar == null) return;

        _currentDisplayValue = Mathf.Lerp(_currentDisplayValue, _targetValue, Time.deltaTime * _lerpSpeed);
        
        if(Mathf.Abs(_currentDisplayValue - _targetValue) < 0.001f)
            _currentDisplayValue = _targetValue;
        
        _healthBar.value = _currentDisplayValue;
        bool isCritical = _targetValue < _criticalThreshold && _targetValue > 0f;

        if(_canvasGroup != null)
        {
            if(_isDamaged)
            {
                _damageTimer -= Time.deltaTime;
                
                if(_damageTimer <= 0f) _isDamaged = false;
            }

            float targetOpacity = (isCritical || _isDamaged) ? 1f : _idleOpacity;
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetOpacity, Time.deltaTime * _opacityLerpSpeed);
        }

        if(_isPulsing)
        {
            _pulseTimer += Time.deltaTime;
            float smoothT = OscillationMath.PulseT(_pulseTimer, _pulsePeriod);
            _healthBar.transform.localScale = _baseScale + Vector3.one * (smoothT * _pulseScale);

            if(_pulseTimer >= _pulsePeriod)
            {
                _isPulsing = false;
                _pulseTimer = 0f;
                
                if(_fillImage != null) _fillImage.color = _fillBaseColor;
                
                if(!isCritical) _healthBar.transform.localScale = _baseScale;
            }
        }

        else if(_isHealPulsing)
        {
            _healPulseTimer += Time.deltaTime;
            float smoothT = OscillationMath.PulseT(_healPulseTimer, _pulsePeriod);
            _healthBar.transform.localScale = _baseScale + Vector3.one * (smoothT * _pulseScale);

            if(_healPulseTimer >= _pulsePeriod)
            {
                _isHealPulsing = false;
                _healPulseTimer = 0f;
                
                if(_fillImage != null) _fillImage.color = _fillBaseColor;
                
                if(!isCritical) _healthBar.transform.localScale = _baseScale;
            }
        }

        else if(isCritical)
        {
            _pulseTime += Time.deltaTime;
            float smoothT = OscillationMath.PulseT(_pulseTime, _pulsePeriod);
            _healthBar.transform.localScale = _baseScale + Vector3.one * (smoothT * _pulseScale);
        }

        else
        {
            _pulseTime = 0f;
            _healthBar.transform.localScale = _baseScale;
        }
    }

    public void TriggerDamagePulse()
    {
        _isPulsing = true;
        _pulseTimer = 0f;

        if(_fillImage != null) _fillImage.color = _damageColor;
    }

    public void TriggerHealPulse()
    {
        _isHealPulsing = true;
        _healPulseTimer = 0f;
        
        if(_fillImage != null) _fillImage.color = _healColor;
    }
}
