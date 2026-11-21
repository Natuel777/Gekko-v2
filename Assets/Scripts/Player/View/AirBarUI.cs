using UnityEngine;
using UnityEngine.UI;

// Barra de aire bajo el agua. Escucha "OnPlayerAirChanged" (0..1, lo dispara GekkoSwimming) y solo se ve mientras la barra
// no está llena. Con poco aire cambia de color y late.
//
// Para el arte final: agregar este componente a un objeto del HUD con un CanvasGroup y asignarle _fill (y opcionalmente
// _background). Si _fill es una Image tipo Filled con sprite, se usa fillAmount; si no, se estira su RectTransform. Si la
// escena no tiene ninguna AirBarUI, Player arma una provisoria (EnsureExists) abajo al centro de la pantalla.
[RequireComponent(typeof(CanvasGroup))]
public class AirBarUI : MonoBehaviour
{
    private const string AirEvent = "OnPlayerAirChanged";

    [SerializeField] private Image _fill;
    [SerializeField] private Color _fullColor = new Color(0.55f, 0.9f, 1f);
    [SerializeField] private Color _lowColor = new Color(1f, 0.35f, 0.3f);
    [Tooltip("Por debajo de este aire la barra se pone roja y late.")]
    [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.3f;
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 4f;

    private CanvasGroup _group;
    private float _air = 1f;
    private bool _useFillAmount;

    public static void EnsureExists()
    {
        if(FindAnyObjectByType<AirBarUI>(FindObjectsInactive.Include) != null) return;
        BuildPlaceholder();
    }

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _useFillAmount = _fill != null && _fill.type == Image.Type.Filled && _fill.sprite != null;
    }

    private void OnEnable() => EventManager.Subscribe<float>(AirEvent, OnAirChanged);
    private void OnDisable() => EventManager.Unsubscribe<float>(AirEvent, OnAirChanged);

    private void OnAirChanged(float air) => _air = Mathf.Clamp01(air);

    private void Update()
    {
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        float targetAlpha = (!paused && _air < 0.999f) ? 1f : 0f;
        _group.alpha = Mathf.MoveTowards(_group.alpha, targetAlpha, _fadeSpeed * Time.unscaledDeltaTime);

        if(_fill == null) return;

        if(_useFillAmount) _fill.fillAmount = _air;
        else
        {
            RectTransform rt = _fill.rectTransform;
            rt.anchorMax = new Vector2(_air, rt.anchorMax.y);
        }

        bool low = _air < _lowThreshold;
        Color c = low ? _lowColor : _fullColor;
        if(low) c.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
        _fill.color = c;
    }

    // Barra provisoria hasta que arte haga la definitiva: fondo oscuro con borde y relleno celeste, abajo al centro.
    private static void BuildPlaceholder()
    {
        var root = new GameObject("AirBar (provisoria)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        frame.transform.SetParent(root.transform, false);
        var frameRt = (RectTransform)frame.transform;
        frameRt.anchorMin = frameRt.anchorMax = new Vector2(0.5f, 0f);
        frameRt.pivot = new Vector2(0.5f, 0f);
        frameRt.anchoredPosition = new Vector2(0f, 120f);
        frameRt.sizeDelta = new Vector2(360f, 22f);
        frame.GetComponent<Image>().color = new Color(0.03f, 0.1f, 0.15f, 0.75f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(frame.transform, false);
        var fillRt = (RectTransform)fill.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);
        // El relleno se estira con anchorMax.x: el offset derecho tiene que ser 0 para que 0..1 vaya de vacía a llena.
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMax = new Vector2(0f, -3f);

        var bar = frame.AddComponent<AirBarUI>();
        bar._fill = fill.GetComponent<Image>();
        bar._useFillAmount = false;
    }
}
