using System.Collections;
using UnityEngine;

// Barras de cine (arriba y abajo) y cartel de título para las cinemáticas. Va en un Canvas propio que solo está prendido
// mientras corre la cinemática (ParkourCinematicTrigger._cinematicOnlyObjects): al prenderse, las barras entran solas;
// HideBars las saca y ShowTitle muestra el cartel un rato. Si la cinemática se saltea, el trigger apaga el Canvas y todo
// desaparece de golpe.
// Se dibuja debajo del Canvas del HUD para que el aviso de "mantener E para saltear" (que está abajo) siga visible.
[RequireComponent(typeof(Canvas))]
public class CinematicLetterbox : MonoBehaviour
{
    [Header("Barras")]
    [SerializeField] private RectTransform _topBar;
    [SerializeField] private RectTransform _bottomBar;
    [Tooltip("Alto de cada barra, como fracción del alto de la pantalla.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float _barHeight = 0.11f;
    [Tooltip("Segundos que tardan en entrar o salir.")]
    [Min(0.01f)]
    [SerializeField] private float _slideSeconds = 0.6f;

    [Header("Título")]
    [SerializeField] private CanvasGroup _title;
    [Min(0.01f)]
    [SerializeField] private float _titleFadeSeconds = 0.5f;
    [Tooltip("Segundos que queda el cartel a pleno antes de desvanecerse.")]
    [Min(0f)]
    [SerializeField] private float _titleHoldSeconds = 2f;
    [Tooltip("Escala con la que arranca el cartel (crece hasta 1 al aparecer).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float _titleStartScale = 0.9f;

    private Coroutine _barsRoutine, _titleRoutine;
    private float _barsAmount;

    private void OnEnable()
    {
        PlaceBelowHud();

        SetBars(0f);
        SetTitleAlpha(0f);
        _barsRoutine = StartCoroutine(SlideBars(1f));
    }

    private void OnDisable()
    {
        _barsRoutine = null;
        _titleRoutine = null; // las corrutinas mueren con el objeto
    }

    // Señal del Timeline: las barras salen un poco antes de que termine, mientras la cámara vuelve a Gekko.
    public void HideBars()
    {
        if(!isActiveAndEnabled) return;
        if(_barsRoutine != null) StopCoroutine(_barsRoutine);
        _barsRoutine = StartCoroutine(SlideBars(0f));
    }

    // Señal del Timeline: aparece el cartel, se queda _titleHoldSeconds y se desvanece.
    public void ShowTitle()
    {
        if(!isActiveAndEnabled || _title == null) return;
        if(_titleRoutine != null) StopCoroutine(_titleRoutine);
        _titleRoutine = StartCoroutine(TitleRoutine());
    }

    private void PlaceBelowHud()
    {
        Canvas canvas = GetComponent<Canvas>();
        Canvas hud = UIManager.Instance != null ? UIManager.Instance.GetComponentInParent<Canvas>() : null;
        if(hud == null) return;

        hud = hud.rootCanvas;
        canvas.renderMode = hud.renderMode;
        canvas.sortingOrder = hud.sortingOrder - 1;
    }

    private IEnumerator SlideBars(float target)
    {
        float from = _barsAmount;
        for(float t = 0f; t < 1f; t += Time.deltaTime / _slideSeconds)
        {
            SetBars(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetBars(target);
        _barsRoutine = null;
    }

    private void SetBars(float amount)
    {
        _barsAmount = amount;
        float h = _barHeight * amount;
        if(_topBar != null)
        {
            _topBar.anchorMin = new Vector2(0f, 1f - h);
            _topBar.anchorMax = Vector2.one;
        }
        if(_bottomBar != null)
        {
            _bottomBar.anchorMin = Vector2.zero;
            _bottomBar.anchorMax = new Vector2(1f, h);
        }
    }

    private IEnumerator TitleRoutine()
    {
        for(float t = 0f; t < 1f; t += Time.deltaTime / _titleFadeSeconds)
        {
            SetTitleAlpha(t);
            _title.transform.localScale = Vector3.one * Mathf.Lerp(_titleStartScale, 1f, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        SetTitleAlpha(1f);
        _title.transform.localScale = Vector3.one;

        if(_titleHoldSeconds > 0f) yield return new WaitForSeconds(_titleHoldSeconds);

        for(float t = 0f; t < 1f; t += Time.deltaTime / _titleFadeSeconds)
        {
            SetTitleAlpha(1f - t);
            yield return null;
        }
        SetTitleAlpha(0f);
        _titleRoutine = null;
    }

    private void SetTitleAlpha(float alpha)
    {
        if(_title != null) _title.alpha = alpha;
    }
}
