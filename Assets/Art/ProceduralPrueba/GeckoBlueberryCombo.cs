using UnityEngine;

/// <summary>
/// Versión autónoma (sin GameManager) del combo de arándanos del juego real
/// (<see cref="BlueberryComboTracker"/>): comer 5 seguidos activa un boost de velocidad
/// temporal, extendible comiendo más mientras dura. Mismos números que
/// <c>BlueberryCollectedSO</c> (multiplicador 1.3, 5s, +2s por arándano extra) — no es un
/// diseño nuevo, es el mismo mecanismo portado a GeckoMover para poder probarlo en esta
/// escena sandbox, que no tiene GameManager/PlayerController.
///
/// No depende de la lengua: los arándanos se levantan por CONTACTO (GeckoBlueberryPickup,
/// que detecta el trigger), igual que en el juego real (Gekko camina sobre ellos, no los
/// come con la lengua).
/// </summary>
[RequireComponent(typeof(GeckoMover))]
public class GeckoBlueberryCombo : MonoBehaviour
{
    [SerializeField] private GeckoMover _mover;

    [Header("Combo (mismos valores que BlueberryCollectedSO)")]
    [SerializeField] private int _comboTarget = 5;
    [SerializeField] private float _speedBoostMultiplier = 1.3f;
    [SerializeField] private float _speedBoostDuration = 5f;
    [SerializeField] private float _boostExtendPerBerry = 2f;

    private int _comboCount;
    private bool _boostActive;
    private float _boostTimeRemaining;

    public int ComboCount => _comboCount;
    public bool BoostActive => _boostActive;
    public float BoostTimeRemaining => _boostTimeRemaining;

    private void Awake()
    {
        if (_mover == null) _mover = GetComponent<GeckoMover>();
    }

    private void Update()
    {
        if (_boostTimeRemaining <= 0f) return;

        _boostTimeRemaining -= Time.deltaTime;
        if (_boostTimeRemaining <= 0f)
        {
            _boostTimeRemaining = 0f;
            _boostActive = false;
            _mover.SetSpeedMultiplier(1f);
            WindEffectController.SetActive(false);
        }
    }

    /// <summary> Lo llama GeckoBlueberryPickup al agarrar un arándano. </summary>
    public void OnCollect()
    {
        if (_boostActive)
        {
            _boostTimeRemaining += _boostExtendPerBerry;
            return;
        }

        _comboCount++;
        if (_comboCount >= _comboTarget) ActivateBoost();
    }

    private void ActivateBoost()
    {
        _boostActive = true;
        _comboCount = 0;
        _boostTimeRemaining = _speedBoostDuration;
        _mover.SetSpeedMultiplier(_speedBoostMultiplier);
        WindEffectController.SetActive(true);
    }
}
