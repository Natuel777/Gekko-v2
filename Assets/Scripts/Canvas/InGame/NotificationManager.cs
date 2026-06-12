using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
using System.Collections;

[Serializable]
public class NotificationManager
{
    public event Action OnBlueberryWindowClosed;
    [SerializeField] private GameObject _notificationParent;
    [SerializeField] private TMP_Text _notificationText;
    [SerializeField] private Image _notificationImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    private Queue<NotificationSO> _notificationQueue = new Queue<NotificationSO>();
    private bool _isDisplaying = false;
    private bool _isAccumulating = false;
    private float _cooldownTimer = 0f;
    private Func<IEnumerator , Coroutine> _startCoroutine;

    [Header("Raspberry Counter")]
    [SerializeField] private CanvasGroup _raspberryCanvasGroup;
    [SerializeField] private TMP_Text _raspberryCountText;
    [SerializeField] private Image _raspberryImage;
    [SerializeField] private CanvasGroup[] _contextualGroups;
    [SerializeField] private float _contextualDisplayDuration = 2f;
    [SerializeField] private float _contextualFadeDuration = 0.23f;
    private bool _isRaspberryAccumulating = false;
    private float _raspberryCooldownTimer = 0f;
    private float[] _contextualDefaultAlphas;

    public void Initialize(Func<IEnumerator , Coroutine> startCoroutine)
    {
        // El contador de blueberry descansa en alpha 0; solo sube a 1 al agarrar.
        if(_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        if(_raspberryCanvasGroup != null)
            _raspberryCanvasGroup.alpha = 0f;

        // Evita el "New Text" (default de TMP) al iniciar el nivel.
        if(_notificationText != null)
            _notificationText.text = string.Empty;
        if(_raspberryCountText != null)
            _raspberryCountText.text = "0";

        if(_contextualGroups != null)
        {
            _contextualDefaultAlphas = new float[_contextualGroups.Length];
            for(int i = 0; i < _contextualGroups.Length; i++)
                _contextualDefaultAlphas[i] = _contextualGroups[i].alpha;
        }

        _startCoroutine = startCoroutine;
    }

    public void ShowOrUpdate(NotificationSO data, string messageOverride = null)
    {
        _notificationText.text = messageOverride ?? data.Message;

        if(_notificationImage != null && _notificationImage.sprite != data.Icon)
            _notificationImage.sprite = data.Icon;

        if(!_isAccumulating)
        {
            _isAccumulating = true;
            _cooldownTimer = _contextualDisplayDuration;
            _startCoroutine(AccumulatingNotification(data));
        }
        else
        {
            _cooldownTimer = _contextualDisplayDuration;
        }
    }

    private IEnumerator AccumulatingNotification(NotificationSO data)
    {
        yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, true, data.FadeDuration));

        if(_contextualGroups != null)
            foreach(var group in _contextualGroups)
                _startCoroutine(FadeCanvasGroup(group, true, _contextualFadeDuration));

        while(_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            yield return null;
        }

        if(_contextualGroups != null)
            for(int i = 0; i < _contextualGroups.Length; i++)
                _startCoroutine(FadeCanvasGroupTo(_contextualGroups[i], _contextualDefaultAlphas[i], _contextualFadeDuration));

        yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, false, data.FadeDuration));
        _isAccumulating = false;
        OnBlueberryWindowClosed?.Invoke();

        if(_notificationQueue.Count > 0 && !_isDisplaying)
            _startCoroutine(DisplayNotification());
    }

    public void ShowNotification(NotificationSO notification)
    {
        _notificationQueue.Enqueue(notification);

        if(!_isDisplaying)
            _startCoroutine(DisplayNotification());
    }

    private IEnumerator DisplayNotification()
    {
        _isDisplaying = true;

        while(_notificationQueue.Count > 0)
        {
            NotificationSO current = _notificationQueue.Dequeue();
            _notificationText.text = current.Message;

            if(_notificationImage != null && _notificationImage.sprite != current.Icon)
                _notificationImage.sprite = current.Icon;

            yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, true, current.FadeDuration));
            yield return new WaitForSeconds(current.DisplayDuration);
            yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, false, current.FadeDuration));
        }

        _isDisplaying = false;
    }

    public void ShowRaspberryCollectible(NotificationSO data, int count)
    {
        _raspberryCountText.text = count.ToString();

        if(_raspberryImage == null || _raspberryImage.sprite != data.Icon)
            _raspberryImage.sprite = data.Icon;

        if(!_isRaspberryAccumulating)
        {
            _isRaspberryAccumulating = true;
            _raspberryCooldownTimer = data.CooldownDuration;
            _startCoroutine(RaspberryAccumulatingNotification(data));
        }
        else
        {
            _raspberryCooldownTimer = data.CooldownDuration;
        }
    }

    private IEnumerator RaspberryAccumulatingNotification(NotificationSO data)
    {
        yield return _startCoroutine(FadeCanvasGroup(_raspberryCanvasGroup, true, data.FadeDuration));

        // El contador de blueberry también aparece junto con los grupos contextuales.
        _startCoroutine(FadeCanvasGroup(_canvasGroup, true, _contextualFadeDuration));

        if(_contextualGroups != null)
            foreach(var group in _contextualGroups)
                _startCoroutine(FadeCanvasGroup(group, true, _contextualFadeDuration));

        while(_raspberryCooldownTimer > 0f)
        {
            _raspberryCooldownTimer -= Time.deltaTime;
            yield return null;
        }

        yield return _startCoroutine(FadeCanvasGroup(_raspberryCanvasGroup, false, data.FadeDuration));
        yield return new WaitForSeconds(_contextualDisplayDuration);

        // Restaurar: blueberry vuelve a 0 y los contextuales a su alpha original.
        _startCoroutine(FadeCanvasGroup(_canvasGroup, false, _contextualFadeDuration));

        if(_contextualGroups != null)
            for(int i = 0; i < _contextualGroups.Length; i++)
                _startCoroutine(FadeCanvasGroupTo(_contextualGroups[i], _contextualDefaultAlphas[i], _contextualFadeDuration));

        _isRaspberryAccumulating = false;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, bool fadeIn, float duration)
    {
        float targetAlpha = fadeIn ? 1f : 0f;
        float initialAlpha = canvasGroup.alpha;
        float enlapsedTime = 0f;

        while(enlapsedTime < duration)
        {
            enlapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(initialAlpha, targetAlpha, enlapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeCanvasGroupTo(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float initialAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while(elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(initialAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}
