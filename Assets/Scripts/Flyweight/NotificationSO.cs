using UnityEngine;

[CreateAssetMenu(fileName = "NotificationSO", menuName = "Scriptable Objects/NotificationSO")]
public class NotificationSO : ScriptableObject
{
    [SerializeField, TextArea] private string _message;
    [SerializeField] private string _name;
    [SerializeField] private float _displayDuration;
    [SerializeField] private float _fadeDuration;
    [SerializeField] private float _cooldownDuration;
    [SerializeField] private float _speedBoostMultiplier;
    [SerializeField] private float _speedBoostTimer;
    [SerializeField] private Sprite _icon;

    #region Getters
    public string Message { get => _message; set => _message = value; }
    public string Name => _name;
    public float DisplayDuration => _displayDuration;
    public float FadeDuration => _fadeDuration;
    public float CooldownDuration => _cooldownDuration;
    public float SpeedBoostMultiplier => _speedBoostMultiplier;
    public float SpeedBoostTimer => _speedBoostTimer;
    public Sprite Icon => _icon;
    #endregion
}
