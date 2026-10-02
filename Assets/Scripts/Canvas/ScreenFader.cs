using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeDuration = 0.5f;

    public event Action OnFadeMiddle;
    public event Action OnFadeCompleted;
    private void Start()
    {
        Instance = this;
    }
    public void StartFade()
    {
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        GameManager.Instance.Pj.Inputs(false);
        GameManager.Instance.CanPause = true;

        // Fade a negro (cerrar los ojos)
        yield return StartCoroutine(Fade(0f, 1f));

        // Mientras la pantalla está negra
        OnFadeMiddle?.Invoke();

        // Fade de vuelta a transparente (abrir los ojos)
        yield return StartCoroutine(Fade(1f, 0f));

        OnFadeCompleted?.Invoke();
        GameManager.Instance.Pj.Inputs(true);
        GameManager.Instance.CanPause = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        Color color = _fadeImage.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            color.a = alpha;
            _fadeImage.color = color;
            yield return null;
        }

        color.a = to;
        _fadeImage.color = color;
    }
}