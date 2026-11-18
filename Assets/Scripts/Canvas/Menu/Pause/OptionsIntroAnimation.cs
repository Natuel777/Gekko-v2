using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animación de entrada/salida de menús (Opciones y Pausa): unos elementos suben desde abajo y otros bajan desde arriba
// hasta su posición original, y opcionalmente un fondo hace fade. Usa tiempo sin escala porque estos menús funcionan
// con el juego pausado (timeScale = 0).
// La entrada corre sola en OnEnable. PlayIn/PlayOut sirven para pantallas que se reutilizan (Pausa) o que se van
// sin destruirse (Pausa al abrir Opciones).
public class OptionsIntroAnimation : MonoBehaviour
{
    [Tooltip("Entran desde abajo de la pantalla (ej. Categories y Content).")]
    [SerializeField] private RectTransform[] _fromBottom;

    [Tooltip("Entran desde arriba de la pantalla (ej. el banner de Opciones).")]
    [SerializeField] private RectTransform[] _fromTop;

    [Tooltip("Aparecen creciendo desde el centro con un pequeño rebote (ej. un cartel de confirmación).")]
    [SerializeField] private RectTransform[] _pop;
    [SerializeField, Range(0f, 1f)] private float _popFromScale = 0.6f;
    [Tooltip("Fade del contenido que hace 'pop' (opcional): un CanvasGroup en el cartel.")]
    [SerializeField] private CanvasGroup _popGroup;

    [Tooltip("Fondo que aparece/desaparece con fade junto con la animación (opcional). Se respeta su alpha original.")]
    [SerializeField] private Graphic _fadeGraphic;

    [SerializeField, Min(0.01f)] private float _duration = 0.5f;
    [Tooltip("Duración de la salida. 0 = igual que la entrada.")]
    [SerializeField, Min(0f)] private float _outDuration = 0f;
    [Tooltip("Demora entre un elemento y el siguiente de la misma lista.")]
    [SerializeField, Min(0f)] private float _stagger = 0.06f;
    [Tooltip("Distancia de partida, en unidades del canvas (1080 = una pantalla de alto).")]
    [SerializeField] private float _bottomDistance = 1080f;
    [SerializeField] private float _topDistance = 500f;

    private Vector2[] _bottomTargets;
    private Vector2[] _topTargets;
    private Vector3[] _popScales;
    private float _fadeAlpha = 1f;
    private Coroutine _routine;
    private bool _exiting;

    public bool IsOut => _exiting;

    private void Awake()
    {
        _bottomTargets = Capture(_fromBottom);
        _topTargets = Capture(_fromTop);
        _popScales = new Vector3[Length(_pop)];
        for (int i = 0; i < _popScales.Length; i++) _popScales[i] = _pop[i] ? _pop[i].localScale : Vector3.one;
        if (_fadeGraphic) _fadeAlpha = _fadeGraphic.color.a;
    }

    private void OnEnable() => PlayIn();

    private void OnDisable()
    {
        _routine = null;
        // Si la salida ya arrancó, el canvas se está yendo: no volver a la posición final (se vería un cuadro "de golpe").
        if (_exiting) return;
        // Si se corta a mitad de camino, que no quede nada desplazado.
        Restore(_fromBottom, _bottomTargets);
        Restore(_fromTop, _topTargets);
        SetPop(1f, true);
        SetFade(1f);
    }

    // Entrada (desde afuera hacia su lugar). Se llama sola en OnEnable; Pausa la vuelve a llamar al volver de Opciones.
    public void PlayIn()
    {
        if (!isActiveAndEnabled) return;
        if (_routine != null) StopCoroutine(_routine);
        _exiting = false;
        _routine = StartCoroutine(Play(true, null));
    }

    // Salida: el camino inverso de la entrada. onComplete se llama al terminar (ej. destruir/ocultar el canvas).
    // Sin onComplete los elementos quedan afuera de la pantalla hasta el próximo PlayIn.
    public void PlayOut(System.Action onComplete)
    {
        if (_routine != null) StopCoroutine(_routine);
        if (!isActiveAndEnabled) { onComplete?.Invoke(); return; }
        _exiting = true;
        _routine = StartCoroutine(Play(false, onComplete));
    }

    private IEnumerator Play(bool intro, System.Action onComplete)
    {
        float duration = intro || _outDuration <= 0f ? _duration : _outDuration;
        float total = duration + _stagger * Mathf.Max(Length(_fromBottom), Length(_fromTop));
        float time = 0f;

        while (time < total)
        {
            Step(time, duration, intro);
            yield return null;
            time += Time.unscaledDeltaTime;
        }

        if (intro)
        {
            Restore(_fromBottom, _bottomTargets);
            Restore(_fromTop, _topTargets);
            SetPop(1f, true);
            SetFade(1f);
        }
        else Step(total, duration, false);

        _routine = null;
        onComplete?.Invoke();
    }

    private void Step(float time, float duration, bool intro)
    {
        Apply(_fromBottom, _bottomTargets, -_bottomDistance, time, duration, intro);
        Apply(_fromTop, _topTargets, _topDistance, time, duration, intro);
        float f = Mathf.Clamp01(time / duration);
        SetFade(intro ? f : 1f - f);
        SetPop(f, intro);
    }

    // t = progreso 0..1. Entrada: crece desde _popFromScale con un rebote (ease-out back).
    // Salida: se achica un poco y se desvanece (ease-in).
    private void SetPop(float t, bool intro)
    {
        if (_popScales == null) return;
        float scale, alpha;
        if (intro)
        {
            const float overshoot = 1.70158f;
            float u = t - 1f;
            float back = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            scale = Mathf.LerpUnclamped(_popFromScale, 1f, back);
            alpha = Mathf.Clamp01(t * 2f); // el fade termina antes que el rebote
        }
        else
        {
            scale = Mathf.Lerp(1f, Mathf.Lerp(_popFromScale, 1f, 0.5f), t * t);
            alpha = 1f - t;
        }
        for (int i = 0; i < _popScales.Length; i++) if (_pop[i]) _pop[i].localScale = _popScales[i] * scale;
        if (_popGroup) _popGroup.alpha = alpha;
    }

    private void Apply(RectTransform[] items, Vector2[] targets, float offsetY, float time, float duration, bool intro)
    {
        if (items == null || targets == null) return;
        for (int i = 0; i < items.Length && i < targets.Length; i++)
        {
            if (!items[i]) continue;
            float t = Mathf.Clamp01((time - _stagger * i) / duration);
            // Entrada: ease-out cúbico (rápido al principio, frena al llegar).
            // Salida: ease-in cúbico (arranca suave y acelera al irse) = la entrada al revés.
            float away = intro ? Mathf.Pow(1f - t, 3f) : t * t * t;
            items[i].anchoredPosition = targets[i] + new Vector2(0f, offsetY * away);
        }
    }

    private void SetFade(float amount)
    {
        if (!_fadeGraphic) return;
        var c = _fadeGraphic.color;
        c.a = _fadeAlpha * amount;
        _fadeGraphic.color = c;
    }

    private static Vector2[] Capture(RectTransform[] items)
    {
        if (items == null) return new Vector2[0];
        var result = new Vector2[items.Length];
        for (int i = 0; i < items.Length; i++)
            if (items[i]) result[i] = items[i].anchoredPosition;
        return result;
    }

    private static void Restore(RectTransform[] items, Vector2[] targets)
    {
        if (items == null || targets == null) return;
        for (int i = 0; i < items.Length && i < targets.Length; i++)
            if (items[i]) items[i].anchoredPosition = targets[i];
    }

    private static int Length(RectTransform[] items) => items == null ? 0 : items.Length;
}
