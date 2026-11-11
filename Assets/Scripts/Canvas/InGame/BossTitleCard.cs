using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

// Cartel con el nombre del boss durante su cinemática de presentación (estilo "nombre + apodo" abajo a la
// izquierda, con franjas negras de cine). Sigue el tiempo del PlayableDirector: se muestra entre _showAt y _hideAt
// y se apaga solo si el timeline termina o se saltea (el director deja de estar en Playing).
// Arma su propio Canvas en Awake para no depender de un prefab de UI.
public class BossTitleCard : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector _director;
    [Tooltip("Segundo del timeline en que aparece el cartel.")]
    [SerializeField] private float _showAt = 1.1f;
    [Tooltip("Segundo del timeline en que empieza a irse el cartel.")]
    [SerializeField] private float _hideAt = 5.8f;

    [Header("Texto (claves de Localizer)")]
    [SerializeField] private string _nameKey = "boss1_name";
    [SerializeField] private string _subtitleKey = "boss1_subtitle";
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Color _nameColor = new Color(1f, 0.38f, 0.62f);
    [SerializeField] private Color _subtitleColor = new Color(1f, 0.95f, 0.88f);
    [SerializeField] private Color _outlineColor = new Color(0.12f, 0.03f, 0.12f);

    [Header("Animación")]
    [Min(0.01f)]
    [SerializeField] private float _fadeIn = 0.45f;
    [Min(0.01f)]
    [SerializeField] private float _fadeOut = 0.5f;
    [Tooltip("Franjas negras arriba/abajo mientras está el cartel (fracción del alto de pantalla).")]
    [Range(0f, 0.2f)]
    [SerializeField] private float _letterbox = 0.1f;
    [Tooltip("Orden del Canvas: por debajo del fader (10) para que los fundidos lo tapen.")]
    [SerializeField] private int _sortingOrder = 9;

    private CanvasGroup _group;
    private RectTransform _barTop;
    private RectTransform _barBottom;
    private RectTransform _nameRect;
    private RectTransform _line;
    private CanvasGroup _subtitleGroup;
    private TextMeshProUGUI _name;
    private TextMeshProUGUI _subtitle;
    private float _visible;   // 0..1, avance de la entrada/salida
    private float _shownFor;  // segundos desde que empezó a mostrarse (para escalonar nombre/línea/apodo)

    private void Awake()
    {
        BuildUI();
        Apply(0f);
    }

    private void OnEnable()
    {
        Localizer.OnLanguageChanged += RefreshTexts;
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= RefreshTexts;
    }

    private void Update()
    {
        bool playing = _director != null && _director.state == PlayState.Playing;
        double t = playing ? _director.time : -1d;
        bool show = playing && t >= _showAt && t < _hideAt;

        // unscaledDeltaTime: el hitstop o una pausa no deben congelar el cartel a medio aparecer.
        float dt = Time.unscaledDeltaTime;

        if(show)
        {
            if(_visible <= 0f)
            {
                RefreshTexts();
                _shownFor = 0f;
            }
            _shownFor += dt;
            _visible = Mathf.MoveTowards(_visible, 1f, dt / _fadeIn);
        }
        else
        {
            // Si se saltea la cinemática se va enseguida, sin esperar el fade.
            _visible = playing ? Mathf.MoveTowards(_visible, 0f, dt / _fadeOut) : 0f;
        }

        Apply(_visible);
    }

    private void Apply(float v)
    {
        if(_group == null) return;

        _group.alpha = v;
        _group.gameObject.SetActive(v > 0f);
        if(v <= 0f) return;

        float e = 1f - Mathf.Pow(1f - v, 3f); // ease out

        float bar = _letterbox * e;
        _barTop.anchorMin = new Vector2(0f, 1f - bar);
        _barBottom.anchorMax = new Vector2(1f, bar);

        // Escalonado: primero el nombre entra deslizando, después la línea se estira y por último el apodo.
        float nameT = Smooth(_shownFor / 0.5f);
        float lineT = Smooth((_shownFor - 0.2f) / 0.5f);
        float subT = Smooth((_shownFor - 0.45f) / 0.45f);

        _nameRect.anchoredPosition = new Vector2(Mathf.Lerp(-60f, 0f, nameT), 0f);
        _line.localScale = new Vector3(lineT, 1f, 1f);
        _subtitleGroup.alpha = subT;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private void RefreshTexts()
    {
        if(_name != null) _name.text = Localizer.Get(_nameKey).ToUpperInvariant();
        if(_subtitle != null) _subtitle.text = Localizer.Get(_subtitleKey);
    }

    // ------------------------------------------------------------------ UI

    private void BuildUI()
    {
        var canvasGo = new GameObject("BossTitleCard_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _group = canvasGo.GetComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        _barTop = Bar("BarraArriba", canvasGo.transform, new Vector2(0f, 1f), Vector2.one);
        _barBottom = Bar("BarraAbajo", canvasGo.transform, Vector2.zero, new Vector2(1f, 0f));

        // Bloque del cartel: abajo a la izquierda, por encima de la franja inferior.
        var block = new GameObject("Cartel", typeof(RectTransform)).GetComponent<RectTransform>();
        block.SetParent(canvasGo.transform, false);
        block.anchorMin = block.anchorMax = new Vector2(0.07f, 0.17f);
        block.pivot = new Vector2(0f, 0f);
        block.sizeDelta = new Vector2(1100f, 220f);

        // Nombre
        var nameHolder = new GameObject("Nombre", typeof(RectTransform)).GetComponent<RectTransform>();
        nameHolder.SetParent(block, false);
        nameHolder.anchorMin = new Vector2(0f, 0.42f);
        nameHolder.anchorMax = new Vector2(1f, 1f);
        nameHolder.offsetMin = nameHolder.offsetMax = Vector2.zero;
        _nameRect = nameHolder;
        _name = Text(nameHolder, 96f, _nameColor, 0.28f);
        _name.characterSpacing = 4f;

        // Línea
        _line = new GameObject("Linea", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        _line.SetParent(block, false);
        _line.anchorMin = new Vector2(0f, 0.38f);
        _line.anchorMax = new Vector2(0f, 0.38f);
        _line.pivot = new Vector2(0f, 0.5f);
        _line.sizeDelta = new Vector2(620f, 5f);
        _line.GetComponent<Image>().color = new Color(_nameColor.r, _nameColor.g, _nameColor.b, 0.9f);
        _line.GetComponent<Image>().raycastTarget = false;

        // Apodo
        var subHolder = new GameObject("Apodo", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        subHolder.SetParent(block, false);
        subHolder.anchorMin = new Vector2(0f, 0f);
        subHolder.anchorMax = new Vector2(1f, 0.34f);
        subHolder.offsetMin = new Vector2(6f, 0f);
        subHolder.offsetMax = Vector2.zero;
        _subtitleGroup = subHolder.GetComponent<CanvasGroup>();
        _subtitle = Text(subHolder, 46f, _subtitleColor, 0.2f);
        _subtitle.fontStyle = FontStyles.Italic;

        RefreshTexts();
    }

    private static RectTransform Bar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var img = rect.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rect;
    }

    private TextMeshProUGUI Text(RectTransform parent, float size, Color color, float outline)
    {
        var tmp = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = tmp.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        if(_font != null) tmp.font = _font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.outlineWidth = outline;
        tmp.outlineColor = _outlineColor;
        return tmp;
    }
}
