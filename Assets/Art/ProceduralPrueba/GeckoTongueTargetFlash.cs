using System.Collections;
using UnityEngine;

/// <summary>
/// Feedback visual mínimo para objetivos de tipo Reach: destella el Renderer cuando la lengua
/// lo toca. Se conecta al evento <c>On Tongue Reached</c> de un GeckoTongueTarget llamando a
/// <see cref="Flash"/>. Sirve para probar / demostrar la mecánica sin armar arte ni lógica.
/// </summary>
public class GeckoTongueTargetFlash : MonoBehaviour
{
    [SerializeField] private Color _flashColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float _duration = 0.35f;

    private Renderer _renderer;
    private Color _baseColor;
    private Coroutine _routine;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null) _baseColor = _renderer.material.color;
    }

    public void Flash()
    {
        if (_renderer == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            _renderer.material.color = Color.Lerp(_flashColor, _baseColor, t / _duration);
            yield return null;
        }
        _renderer.material.color = _baseColor;
        _routine = null;
    }
}
