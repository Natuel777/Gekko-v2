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

    [Header("Contador de frutillas: entrada llamativa")]
    [Tooltip("Brillo detrás del ícono de la frutilla (opcional); late mientras el contador está visible.")]
    [SerializeField] private Image _raspberryGlow;
    [Tooltip("Cuánto baja desde arriba al aparecer (unidades de canvas).")]
    [SerializeField] private float _raspberryEnterOffset = 90f;
    [SerializeField] private float _raspberryEnterDuration = 0.45f;
    private RectTransform _raspberryRect;
    private Vector2 _raspberryBasePos;

    [Header("Golpe del contador al sumar")]
    [SerializeField] private float _punchScale = 0.35f;
    [SerializeField] private float _punchDuration = 0.25f;
    // Escala original de cada elemento que hace "punch" y un id para cortar el punch anterior si llega otro encima.
    private readonly Dictionary<Transform, Vector3> _punchBaseScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, int> _punchIds = new Dictionary<Transform, int>();

    public void Initialize(Func<IEnumerator , Coroutine> startCoroutine)
    {
        // El contador de blueberry descansa en alpha 0; solo sube a 1 al agarrar.
        if(_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        if(_raspberryCanvasGroup != null)
        {
            _raspberryCanvasGroup.alpha = 0f;
            _raspberryRect = _raspberryCanvasGroup.transform as RectTransform;
            if(_raspberryRect != null) _raspberryBasePos = _raspberryRect.anchoredPosition;
        }
        if(_raspberryGlow != null)
            _raspberryGlow.color = new Color(_raspberryGlow.color.r, _raspberryGlow.color.g, _raspberryGlow.color.b, 0f);

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
        Punch(_notificationText ? _notificationText.transform : null);
        Punch(_notificationImage ? _notificationImage.transform : null);

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

    // El contador de frutillas baja desde arriba con un rebote (ease-out back) al aparecer.
    private IEnumerator RaspberryEnter()
    {
        if(_raspberryRect == null) yield break;
        float t = 0f;
        while(t < _raspberryEnterDuration)
        {
            float u = t / _raspberryEnterDuration;
            const float overshoot = 1.70158f;
            float v = u - 1f;
            float back = 1f + (overshoot + 1f) * v * v * v + overshoot * v * v;
            _raspberryRect.anchoredPosition = _raspberryBasePos + Vector2.up * _raspberryEnterOffset * (1f - back);
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        _raspberryRect.anchoredPosition = _raspberryBasePos;
    }

    // Brillo dorado detrás de la frutilla: late mientras el contador está visible y se apaga con él.
    private IEnumerator RaspberryGlowPulse()
    {
        if(_raspberryGlow == null) yield break;
        float t = 0f;
        Color c = _raspberryGlow.color;
        while(_isRaspberryAccumulating)
        {
            float pulse = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / 0.8f);
            float visible = _raspberryCanvasGroup != null ? _raspberryCanvasGroup.alpha : 1f;
            _raspberryGlow.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.35f, 0.9f, pulse) * visible);
            _raspberryGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.15f, pulse);
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        _raspberryGlow.color = new Color(c.r, c.g, c.b, 0f);
    }

    // "Golpe" del contador: crece un poco y vuelve, para que el ojo note que sumó. Tiempo sin escala para que se vea
    // aunque haya un hitstop (timeScale 0) justo al agarrar la frutilla.
    private void Punch(Transform target)
    {
        if (target == null || _startCoroutine == null) return;
        if (!_punchBaseScales.ContainsKey(target)) _punchBaseScales[target] = target.localScale;
        _punchIds.TryGetValue(target, out int id);
        _punchIds[target] = ++id;
        _startCoroutine(PunchRoutine(target, id));
    }

    private IEnumerator PunchRoutine(Transform target, int id)
    {
        Vector3 baseScale = _punchBaseScales[target];
        float t = 0f;
        while (t < _punchDuration)
        {
            if (target == null || _punchIds[target] != id) yield break; // llegó otro punch: ese sigue
            float u = t / _punchDuration;
            float bump = Mathf.Sin(u * Mathf.PI) * (1f - u * 0.3f); // sube rápido y baja con un leve rebote
            target.localScale = baseScale * (1f + _punchScale * bump);
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        if (target != null && _punchIds[target] == id) target.localScale = baseScale;
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

            yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, true, current.FadeDuration));
            yield return new WaitForSeconds(current.DisplayDuration);
            yield return _startCoroutine(FadeCanvasGroup(_canvasGroup, false, current.FadeDuration));
        }

        _isDisplaying = false;
    }

    public void ShowRaspberryCollectible(NotificationSO data, int count)
    {
        _raspberryCountText.text = count.ToString();
        Punch(_raspberryCountText ? _raspberryCountText.transform : null);
        Punch(_raspberryImage ? _raspberryImage.transform : null);

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
        _startCoroutine(RaspberryEnter());
        _startCoroutine(RaspberryGlowPulse());
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
