using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Estilo de botón de los menús (pestañas de Opciones y botones de Pausa/Opciones):
// en reposo sprite normal + texto semitransparente; con el mouse encima, con foco de teclado/joystick o
// mientras está seleccionada (pestañas) usa el sprite con reborde y el texto opaco.
public class OptionsTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject _underline;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _unselectedColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Sprite del botón (opcional)")]
    [SerializeField] private UnityEngine.UI.Image _image;
    [SerializeField] private Sprite _normalSprite;
    [Tooltip("Sprite con reborde: se usa con el mouse encima, con foco de teclado/joystick y mientras la pestaña está seleccionada.")]
    [SerializeField] private Sprite _highlightSprite;

    [Header("Sonido (opcional)")]
    [Tooltip("Sonido al pasar el mouse / mover el foco. None = sin sonido.")]
    [SerializeField] private SoundNames _hoverSound = SoundNames.None;

    private bool _selected;
    private bool _hovered;
    private bool _focused;

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (_underline) _underline.SetActive(selected);
        RefreshLabel();
    }

    // El texto queda opaco mientras el mouse está encima, y se mantiene así si la pestaña quedó seleccionada.
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        PlayHoverSound();
        RefreshLabel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RefreshLabel();
    }

    // Foco de teclado/joystick. La selección que deja el click del mouse se ignora (si no, el botón quedaría
    // resaltado después de sacar el mouse), igual que en MenuButtonFX.
    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        _focused = true;
        if (!_hovered) PlayHoverSound();
        RefreshLabel();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _focused = false;
        RefreshLabel();
    }

    private void OnEnable()
    {
        RefreshLabel();
    }

    private void OnDisable()
    {
        _hovered = false;
        _focused = false;
    }

    private void PlayHoverSound()
    {
        if (_hoverSound != SoundNames.None && AudioManager.instance) AudioManager.instance.Play(_hoverSound);
    }

    private void RefreshLabel()
    {
        bool on = _selected || _hovered || _focused;
        if (_label) _label.color = on ? _selectedColor : _unselectedColor;
        if (_image && _normalSprite && _highlightSprite) _image.sprite = on ? _highlightSprite : _normalSprite;
    }
}
