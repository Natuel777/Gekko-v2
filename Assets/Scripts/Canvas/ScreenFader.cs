using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeDuration = 0.5f;

    public delegate void FadeMiddle();
    public event FadeMiddle OnFadeMiddle;
    public delegate void FadeCompleted();
    public event FadeCompleted OnFadeCompleted;

    public void FadeToTeleport(Transform player, Vector3 destination)
    {
        StartCoroutine(FadeRoutine(player, destination));
    }

    private IEnumerator FadeRoutine(Transform player, Vector3 destination)
    {
        // Fade a negro (cerrar los ojos)
        yield return StartCoroutine(Fade(0f, 1f));

        // Mientras la pantalla está negra, teletransportamos
        player.position = destination;

        // Fade de vuelta a transparente (abrir los ojos)
        yield return StartCoroutine(Fade(1f, 0f));
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