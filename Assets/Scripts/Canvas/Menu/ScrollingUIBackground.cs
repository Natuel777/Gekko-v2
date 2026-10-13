using UnityEngine;
using UnityEngine.UI;

// Fondo de UI en loop infinito (ej. olas tipo seigaiha) que se desplaza lento y en
// diagonal, sin cortes. Mueve uvRect del RawImage en vez del Transform: así el tile
// no tiene que duplicarse ni reposicionarse, solo se corre la "ventana" de muestreo
// sobre una textura con Wrap Mode = Repeat.
[RequireComponent(typeof(RawImage))]
public class ScrollingUIBackground : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Tiles por segundo. X positivo = se ve mover hacia la izquierda; Y positivo = hacia arriba. Si queda al revés, invertí el signo acá.")]
    [SerializeField] private Vector2 _scrollSpeed = new Vector2(0.125f, 0.125f);
    [Tooltip("1 = tamaño normal de tile. Más alto = tiles más grandes (se ven menos).")]
    [SerializeField] private float _scale = 1f;
    [SerializeField] private bool _playOnEnable = true;

    [Header("Tamaño de tile")]
    [Tooltip("Tamaño de cada tile en píxeles de la RESOLUCIÓN DE REFERENCIA del CanvasScaler (no en píxeles de pantalla real) — así el tile mide siempre lo mismo en pantalla sin estirarse, sea cual sea la resolución.")]
    [SerializeField] private Vector2 _tilePixelSize = new Vector2(40f, 20f);

    [Header("Accesibilidad")]
    [Tooltip("Si está activo y UISettings.ReducedMotion es true, el fondo queda quieto.")]
    [SerializeField] private bool _respectReducedMotion = true;

    [Header("Referencias")]
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private UIRadialVignette _vignette;

    private RectTransform _rectTransform;
    private Vector2 _offset;
    private float _speedMultiplier = 1f;
    private bool _isPlaying;

    private void Awake()
    {
        if(_rawImage == null) _rawImage = GetComponent<RawImage>();
        _rectTransform = _rawImage.rectTransform;
    }

    private void OnEnable()
    {
        _isPlaying = _playOnEnable;
        UpdateUvRectSize(); // evita que el primer frame se vea con el tamaño por defecto (1,1)
    }

    private void Update()
    {
        UpdateUvRectSize();

        if(!_isPlaying) return;
        if(_respectReducedMotion && UISettings.ReducedMotion) return;

        // unscaledDeltaTime: en el menú de pausa Time.timeScale = 0 y esto tiene que seguir andando.
        _offset.x = Mathf.Repeat(_offset.x + _scrollSpeed.x * _speedMultiplier * Time.unscaledDeltaTime, 1f);
        _offset.y = Mathf.Repeat(_offset.y + _scrollSpeed.y * _speedMultiplier * Time.unscaledDeltaTime, 1f);

        Rect uv = _rawImage.uvRect;
        uv.x = _offset.x;
        uv.y = _offset.y;
        _rawImage.uvRect = uv;
    }

    // Recalcula cuántas veces se repite el tile (el "width/height" del uvRect) para
    // que cada uno mida siempre _tilePixelSize * _scale en pantalla, sin importar la
    // resolución real: rect.width/height del RectTransform ya viene en unidades de
    // la resolución de referencia del CanvasScaler cuando el modo es Scale With Screen Size.
    private void UpdateUvRectSize()
    {
        Rect rect = _rectTransform.rect;
        float effectiveTileX = Mathf.Max(_tilePixelSize.x * _scale, 0.01f);
        float effectiveTileY = Mathf.Max(_tilePixelSize.y * _scale, 0.01f);

        Rect uv = _rawImage.uvRect;
        uv.width = rect.width / effectiveTileX;
        uv.height = rect.height / effectiveTileY;
        _rawImage.uvRect = uv;
    }

    #region Control público
    public void Pause() => _isPlaying = false;
    public void Resume() => _isPlaying = true;

    // Para animar la velocidad con DOTween (u otro tween) desde afuera, ej.:
    // DOTween.To(() => mult, x => bg.SetSpeedMultiplier(x), 0f, 0.3f) al abrir el menú.
    // El script no depende de DOTween ni de ningún tweener: esto es un setter plano.
    public void SetSpeedMultiplier(float multiplier) => _speedMultiplier = multiplier;
    #endregion

    // Aplica una variante de color (Bosque/Purificado/Miel/Corrupto...): cambia la
    // textura del tile, el tinte de fondo y el color/intensidad de la viñeta.
    public void ApplyTheme(UIBackgroundTheme theme)
    {
        if(theme == null) return;

        if(theme.tileTexture != null) _rawImage.texture = theme.tileTexture;
        _rawImage.color = theme.backgroundColor;

        if(_vignette != null)
        {
            _vignette.SetColor(theme.vignetteColor);
            _vignette.SetIntensity(theme.vignetteIntensity);
        }
    }
}
