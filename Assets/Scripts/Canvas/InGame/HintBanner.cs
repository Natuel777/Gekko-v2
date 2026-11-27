using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cartel corto de pista en el HUD (ej. el desafío de espejos de LvlTwo): aparece abajo al centro con un fundido, el texto
// se escribe letra por letra (como los diálogos), se queda unos segundos y se va. No frena el juego. El texto sale del
// Localizer (clave), así cambia si se cambia el idioma. Arma su propio Canvas al primer uso: no depende del prefab del Canvas.
public class HintBanner : MonoBehaviour
{
    [Header("Estilo")]
    [Tooltip("Fuente del texto. Vacío = la fuente por defecto de TextMeshPro.")]
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("Fondo del cartel (ej. el del panel de diálogo). Vacío = un rectángulo liso.")]
    [SerializeField] private Sprite _background;
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.09f, 0.1f, 0.82f);
    [SerializeField] private Color _textColor = new Color(0.85f, 1f, 0.98f, 1f);
    [SerializeField] private float _fontSize = 34f;
    [Tooltip("Ancho máximo del cartel, en píxeles de una pantalla de 1920x1080.")]
    [SerializeField] private float _maxWidth = 1100f;
    [Tooltip("Distancia desde el borde de abajo de la pantalla (píxeles de 1920x1080). Abajo al centro, como un subtítulo: no tapa los corazones.")]
    [SerializeField] private float _bottomMargin = 130f;

    [Header("Tiempos")]
    [Min(0.05f)]
    [SerializeField] private float _fadeSeconds = 0.35f;

    [Header("Escritura")]
    [Tooltip("Letras por segundo al escribirse. 0 = aparece todo de una.")]
    [Min(0f)]
    [SerializeField] private float _charsPerSecond = 45f;
    [Tooltip("Opcional: sonido de tipeo (como el habla de los diálogos). Vacío = sin sonido.")]
    [SerializeField] private AudioClip _typeSound;
    [Tooltip("Suena cada esta cantidad de letras (sin contar espacios).")]
    [Min(1)]
    [SerializeField] private int _soundEveryChars = 2;
    [Range(0f, 1f)]
    [SerializeField] private float _soundVolume = 0.5f;
    [Tooltip("Opcional: grupo del mixer para el sonido de tipeo (ej. SFX).")]
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup _soundMixerGroup;

    private CanvasGroup _group;
    private AudioSource _audio;
    private bool _typing;
    private RectTransform _panel;
    private TMP_Text _text;
    private LayoutElement _textLayout;
    private HorizontalLayoutGroup _layout;
    private string _currentKey;
    private Coroutine _routine;

    private void OnEnable() => Localizer.OnLanguageChanged += RefreshText;
    private void OnDisable() => Localizer.OnLanguageChanged -= RefreshText;

    private void OnDestroy()
    {
        if(_group != null) Destroy(_group.transform.root.gameObject);
    }

    // Muestra el texto de la clave durante seconds (desde que termina de escribirse, sin contar los fundidos). Si ya había
    // otro, lo reemplaza.
    public void Show(string localizationKey, float seconds)
    {
        Build();
        StopAllCoroutines();
        _currentKey = localizationKey;
        _typing = _charsPerSecond > 0f;
        RefreshText();
        if(_typing) _text.maxVisibleCharacters = 0;

        _routine = StartCoroutine(ShowRoutine(seconds));
    }

    public void Hide()
    {
        if(_group == null || _group.alpha <= 0f) return;
        StopAllCoroutines();
        _typing = false;
        _routine = StartCoroutine(Fade(0f));
    }

    // La caja entra con su tamaño final (el layout mide el texto completo) y las letras se escriben adentro mientras aparece.
    private IEnumerator ShowRoutine(float seconds)
    {
        if(_typing) StartCoroutine(TypeRoutine());
        yield return Fade(1f);
        while(_typing) yield return null;
        yield return new WaitForSeconds(seconds);
        yield return Fade(0f);
        _routine = null;
    }

    private IEnumerator TypeRoutine()
    {
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;
        float shown = 0f;
        int letters = 0;

        while(_typing && _text.maxVisibleCharacters < total)
        {
            shown += Time.unscaledDeltaTime * _charsPerSecond;
            int target = Mathf.Min(total, Mathf.FloorToInt(shown));

            for(int i = _text.maxVisibleCharacters; i < target; i++)
            {
                if(char.IsWhiteSpace(_text.textInfo.characterInfo[i].character)) continue;
                if(++letters % _soundEveryChars == 0) PlayTypeSound();
            }

            _text.maxVisibleCharacters = target;
            yield return null;

            // Si cambió el idioma mientras escribía, el texto es otro: se sigue con el largo nuevo.
            total = _text.textInfo.characterCount;
        }

        _text.maxVisibleCharacters = int.MaxValue;
        _typing = false;
    }

    private void PlayTypeSound()
    {
        if(_typeSound == null) return;

        if(_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.outputAudioMixerGroup = _soundMixerGroup;
        }

        _audio.pitch = Random.Range(0.9f, 1.1f);
        _audio.PlayOneShot(_typeSound, _soundVolume);
    }

    // Con un pequeño desplazamiento hacia arriba al entrar, para que el ojo lo note.
    private IEnumerator Fade(float target)
    {
        float from = _group.alpha;
        Vector2 basePos = new Vector2(0f, _bottomMargin);
        for(float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _fadeSeconds)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            _group.alpha = Mathf.Lerp(from, target, k);
            if(target > from) _panel.anchoredPosition = basePos + Vector2.down * (18f * (1f - k));
            yield return null;
        }
        _group.alpha = target;
        _panel.anchoredPosition = basePos;
    }

    private void RefreshText()
    {
        if(_text == null || string.IsNullOrEmpty(_currentKey)) return;

        string text = Localizer.Get(_currentKey);
        _text.text = text;
        // Ya escrito: todo visible. Escribiéndose: TypeRoutine sigue desde donde iba.
        if(!_typing) _text.maxVisibleCharacters = int.MaxValue;
        _text.ForceMeshUpdate();

        // Lo que ocupa en una línea, hasta _maxWidth: los textos cortos no quedan en una caja enorme.
        float maxTextWidth = _maxWidth - _layout.padding.horizontal;
        _textLayout.preferredWidth = Mathf.Min(_text.GetPreferredValues(text).x + 2f, maxTextWidth);
    }

    private void Build()
    {
        if(_group != null) return;

        var canvasGo = new GameObject("HintBanner_Canvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Panel abajo al centro que se ajusta al texto.
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup),
                                     typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        _panel = panelGo.GetComponent<RectTransform>();
        _panel.SetParent(canvasGo.transform, false);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
        _panel.pivot = new Vector2(0.5f, 0f);
        _panel.anchoredPosition = new Vector2(0f, _bottomMargin);

        // El fondo va en un hijo estirado que no participa del layout: si estuviera en el panel, el tamaño propio del sprite
        // haría el cartel más grande que el texto.
        var bgGo = new GameObject("Fondo", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.SetParent(_panel, false);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        bgGo.GetComponent<LayoutElement>().ignoreLayout = true;

        var image = bgGo.GetComponent<Image>();
        image.sprite = _background;
        image.type = _background != null && _background.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = _backgroundColor;
        image.raycastTarget = false;

        var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
        _layout = layout;
        layout.padding = new RectOffset(48, 48, 22, 24);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textGo.transform.SetParent(panelGo.transform, false);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        if(_font != null) tmp.font = _font;
        tmp.fontSize = _fontSize;
        tmp.color = _textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        // El ancho lo fija RefreshText según el texto (hasta _maxWidth; ahí corta en otra línea).
        _textLayout = textGo.GetComponent<LayoutElement>();
        _text = tmp;

        _group = panelGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;
    }
}
