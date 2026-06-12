using UnityEngine;

[CreateAssetMenu(fileName = "NotificationSO", menuName = "Scriptable Objects/NotificationSO")]
public class NotificationSO : ScriptableObject
{
    [SerializeField, TextArea] private string _message;
    [SerializeField] private float _displayDuration;
    [SerializeField] private float _fadeDuration;
    [SerializeField] private float _cooldownDuration;
    [SerializeField] private Sprite _icon;

    #region Getters
    public string Message { get => _message; set => _message = value; }
    public float DisplayDuration => _displayDuration;
    public float FadeDuration => _fadeDuration;
    public float CooldownDuration => _cooldownDuration;
    public Sprite Icon => _icon;
    #endregion
}
