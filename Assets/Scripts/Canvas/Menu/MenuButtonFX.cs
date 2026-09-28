using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Efectos de los botones del menú principal (estilo Hollow Knight):
///  - Emisivo: el texto tiene un glow suave en reposo y se prende al pasar el mouse / seleccionar.
///  - Hover: sin vibración, solo se prende el emisivo y sube un poco la escala.
///  - Click: el botón "explota" (punch de escala + flash de emisivo + vibración fuerte), los demás se apagan,
///    y recién al terminar se dispara <see cref="_onConfirmed"/>.
/// El onClick del Button apunta a <see cref="Click"/>; las acciones originales viven en <see cref="_onConfirmed"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Referencias")]
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _halo;

    [Header("Confirmación (reemplaza al onClick del Button)")]
    [SerializeField] private UnityEvent _onConfirmed;
    [SerializeField] private float _confirmDuration = 0.45f;
    [SerializeField] private float _confirmPunch = 0.18f;
    [SerializeField] private bool _playClickSound = true;
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.2f;

    [Header("Emisivo")]
    [SerializeField, Range(0f, 1f)] private float _idleEmission = 0.2f;
    [SerializeField] private Color _glowColor = new Color(1f, 0.72f, 0.28f, 1f);
    [SerializeField, Range(0f, 1f)] private float _hotColorBlend = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _haloMaxAlpha = 0.4f;

    [Header("Hover")]
    [SerializeField] private float _hoverScale = 0.07f;
    [SerializeField] private float _hoverSpeed = 9f;

    [Header("Vibración (solo al hacer click)")]
    [SerializeField] private float _clickShake = 8f;
    [SerializeField] private float _clickShakeAngle = 2.5f;
    [SerializeField] private float _shakeRate = 45f;

    public UnityEvent OnConfirmed => _onConfirmed;

    private RectTransform _labelRect;
    private Material _mat;
    private Vector2 _baseLabelPos;
    private Color _baseColor;
    private Color _hotColor;

    private bool _hovered;
    private bool _selected;
    private bool _dimmed;
    private bool _confirming;
    private bool _confirmed;
    private float _confirmTime;
    private float _hover;
    private float _alpha = 1f;
    private float _nextShake;
    private Vector2 _shakeDir;
    private float _shakeRot;

    private void Awake()
    {
        if (!_label) _label = GetComponentInChildren<TMP_Text>(true);

        _labelRect = _label.rectTransform;
        _baseLabelPos = _labelRect.anchoredPosition;
        _baseColor = _label.color;
        _hotColor = Color.Lerp(_baseColor, Color.white, _hotColorBlend);

        // instancia del material para que el glow de un botón no afecte al resto de los textos
        _mat = _label.fontMaterial;
        _mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
        _mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);
        _mat.SetFloat(ShaderUtilities.ID_GlowInner, 0.15f);
        _mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.6f);
        _mat.SetFloat(ShaderUtilities.ID_GlowPower, 0.8f);
        _label.UpdateMeshPadding();
    }

    private void OnEnable()
    {
        ResetState();
    }

    private void OnDisable()
    {
        ResetState();
    }

    private void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }

    // Se asigna al onClick del Button (Enter/Submit y click del mouse pasan por acá)
    public void Click()
    {
        if (_confirming || SiblingConfirming()) return;

        _confirming = true;
        _confirmed = false;
        _confirmTime = 0f;

        if (_playClickSound && AudioManager.instance) AudioManager.instance.Play(SoundNames.UiButton);

        SetSiblingsDimmed(true);
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
    public void OnPointerExit(PointerEventData eventData) => _hovered = false;

    // Solo navegación por teclado/joystick: la selección que deja el click del mouse se ignora
    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        _selected = true;
    }

    public void OnDeselect(BaseEventData eventData) => _selected = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        bool lit = (_hovered || _selected) && !_dimmed;
        _hover = Mathf.MoveTowards(_hover, lit ? 1f : 0f, dt * _hoverSpeed);
        _alpha = Mathf.MoveTowards(_alpha, _dimmed ? _dimAlpha : 1f, dt * 6f);

        float punch = 0f, flash = 0f, confirmShake = 0f;
        if (_confirming)
        {
            _confirmTime += dt;
            float u = Mathf.Clamp01(_confirmTime / _confirmDuration);
            float pulse = Mathf.Sin(u * Mathf.PI); // sube y baja: 0 -> 1 -> 0
            punch = pulse * _confirmPunch;
            flash = pulse;
            confirmShake = 1f - u;

            if (u >= 1f && !_confirmed)
            {
                _confirmed = true;
                _onConfirmed.Invoke();
                return; // el evento puede desactivar este botón / la pantalla
            }
        }

        Apply(punch, flash, confirmShake);
    }

    private void Apply(float punch, float flash, float confirmShake)
    {
        // Vibración (paso discreto para que se sienta como temblor y no como ondulación)
        float amp = _clickShake * confirmShake;
        float ang = _clickShakeAngle * confirmShake;
        if (Time.unscaledTime >= _nextShake)
        {
            _nextShake = Time.unscaledTime + 1f / _shakeRate;
            _shakeDir = Random.insideUnitCircle;
            _shakeRot = Random.Range(-1f, 1f);
        }

        _labelRect.anchoredPosition = _baseLabelPos + _shakeDir * amp;
        _labelRect.localRotation = Quaternion.Euler(0f, 0f, _shakeRot * ang);
        _labelRect.localScale = Vector3.one * (1f + _hoverScale * _hover + punch);

        // Emisivo
        float emission = Mathf.Clamp01(_idleEmission + (1f - _idleEmission) * _hover + 0.5f * flash) * _alpha;

        Color c = Color.Lerp(_baseColor, _hotColor, Mathf.Clamp01(_hover + flash));
        c.a = _baseColor.a * _alpha;
        _label.color = c;

        Color glow = _glowColor;
        glow.a = emission;
        _mat.SetColor(ShaderUtilities.ID_GlowColor, glow);

        if (_halo)
        {
            Color h = _glowColor;
            h.a = _haloMaxAlpha * emission * emission;
            _halo.color = h;
            _halo.rectTransform.localScale = Vector3.one * (1f + 0.04f * _hover * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }

    private void ResetState()
    {
        _hovered = _selected = _dimmed = _confirming = _confirmed = false;
        _confirmTime = 0f;
        _hover = 0f;
        _alpha = 1f;
        _shakeDir = Vector2.zero;
        _shakeRot = 0f;

        if (_label && _mat) Apply(0f, 0f, 0f);
    }

    private bool SiblingConfirming()
    {
        Transform parent = transform.parent;
        if (!parent) return false;

        for (int i = 0; i < parent.childCount; i++)
        {
            var other = parent.GetChild(i).GetComponent<MenuButtonFX>();
            if (other && other != this && other._confirming) return true;
        }
        return false;
    }

    private void SetSiblingsDimmed(bool dimmed)
    {
        Transform parent = transform.parent;
        if (!parent) return;

        for (int i = 0; i < parent.childCount; i++)
        {
            var other = parent.GetChild(i).GetComponent<MenuButtonFX>();
            if (other && other != this) other._dimmed = dimmed;
        }
    }
}
