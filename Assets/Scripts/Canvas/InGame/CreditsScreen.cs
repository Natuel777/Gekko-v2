using System.Collections;
using UnityEngine;

// Cierre de nivel: fade a negro que se queda (a diferencia de ScreenFader, no vuelve a la jugabilidad)
// y despues deja correr el scroll vertical de creditos.
public class CreditsScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup _blackGroup;
    [SerializeField] private float _fadeInSeconds = 1.5f;
    [SerializeField] private RectTransform _creditsContent;
    [SerializeField] private float _scrollSpeed = 40f;

    private bool _scrolling;

    public void Play()
    {
        gameObject.SetActive(true);
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if(_blackGroup != null)
        {
            _blackGroup.alpha = 0f;

            float t = 0f;
            while(t < _fadeInSeconds)
            {
                t += Time.deltaTime;
                _blackGroup.alpha = Mathf.Clamp01(t / _fadeInSeconds);
                yield return null;
            }

            _blackGroup.alpha = 1f;
        }

        _scrolling = true;
    }

    private void Update()
    {
        if(!_scrolling || _creditsContent == null) return;

        _creditsContent.anchoredPosition += Vector2.up * _scrollSpeed * Time.deltaTime;
    }
}
