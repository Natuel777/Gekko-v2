using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Muestra consejos en la pantalla de carga: uno a la vez, con fade in/out, cambiando cada <see cref="_interval"/>
/// segundos. El orden es aleatorio pero sin repetir hasta haber mostrado todos (y nunca el mismo dos veces seguidas).
/// Usa tiempo sin escalar para que siga funcionando aunque Time.timeScale sea 0.
/// </summary>
public class LoadingTips : MonoBehaviour
{
    [SerializeField] private TMP_Text _tipText;
    [SerializeField] private CanvasGroup _group;

    [SerializeField, TextArea(2, 4)] private string[] _tips;

    [Header("Tiempos")]
    [Tooltip("Segundos que se ve cada consejo (sin contar el fade).")]
    [SerializeField] private float _interval = 6f;
    [SerializeField] private float _fadeDuration = 0.6f;

    private readonly List<int> _bag = new List<int>();
    private int _lastIndex = -1;

    private void OnEnable()
    {
        if (_tips == null || _tips.Length == 0 || !_tipText) return;

        if (_group) _group.alpha = 0f;
        StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            _tipText.text = _tips[NextIndex()];

            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(_interval);
            yield return Fade(1f, 0f);
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (!_group) yield break;

        for (float t = 0f; t < _fadeDuration; t += Time.unscaledDeltaTime)
        {
            _group.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
            yield return null;
        }
        _group.alpha = to;
    }

    // "Bolsa" mezclada: recorre todos los consejos en orden aleatorio antes de volver a mezclar.
    private int NextIndex()
    {
        if (_tips.Length == 1) return 0;

        if (_bag.Count == 0)
        {
            for (int i = 0; i < _tips.Length; i++) _bag.Add(i);

            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }

            // que el primero de la nueva bolsa no repita el último mostrado
            if (_bag[_bag.Count - 1] == _lastIndex)
                (_bag[0], _bag[_bag.Count - 1]) = (_bag[_bag.Count - 1], _bag[0]);
        }

        int next = _bag[_bag.Count - 1];
        _bag.RemoveAt(_bag.Count - 1);
        _lastIndex = next;
        return next;
    }
}
