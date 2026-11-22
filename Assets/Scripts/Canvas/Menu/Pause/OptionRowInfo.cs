using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Va en el Selectable de cada fila (o pestaña) del menú de Opciones.
// Al seleccionarse: escribe título + descripción en el panel derecho.
// Si tiene resaltado (_highlight), además:
//  - el fondo queda prendido mientras la selección esté en la fila o en cualquiera de sus controles (flechas, slider);
//  - pasar el mouse por la fila la selecciona (igual que moverse con teclado/joystick).
// Las pestañas no tienen resaltado, así que el hover no las selecciona (no cambian de pestaña con solo pasar el mouse).
// Los textos son claves del Localizer. El último que escribió en el panel es el "dueño" del texto y lo vuelve a
// escribir si cambia el idioma, aunque ya no esté seleccionado (ej. al cambiar el idioma con las flechas, la que
// queda seleccionada es la flecha y no la fila/pestaña que había escrito la descripción).
public class OptionRowInfo : MonoBehaviour, ISelectHandler
{
    [SerializeField] private string _titleKey;
    [SerializeField] private string _descriptionKey;

    [Tooltip("Fondo de la fila: se prende mientras la fila (o un control suyo) está seleccionada, y el hover la selecciona (opcional).")]
    [SerializeField] private Graphic _highlight;

    [Header("Panel de descripción")]
    [SerializeField] private TMP_Text _titleTarget;
    [SerializeField] private TMP_Text _descriptionTarget;

    // Último que escribió en el panel de descripción (uno solo a la vez).
    private static OptionRowInfo s_shown;

    private Selectable _selectable;
    private Transform _rowRoot;
    private bool _lit;

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        if (!_highlight) return;

        // La fila es el padre del Highlight (Row/{Highlight, Label, Slot}).
        _rowRoot = _highlight.transform.parent;

        // El Highlight queda siempre habilitado y con raycast para que toda la fila detecte el mouse
        // (incluso el espacio vacío); se muestra/oculta con el alpha del CanvasRenderer.
        _highlight.enabled = true;
        _highlight.raycastTarget = true;

        if (_rowRoot)
        {
            var hover = _rowRoot.GetComponent<OptionRowHover>();
            if (!hover) hover = _rowRoot.gameObject.AddComponent<OptionRowHover>();
            hover.Owner = this;
        }
    }

    private void OnEnable()
    {
        // La fila puede volver a activarse (cambio de pestaña) sin recibir OnDeselect: arranca apagada.
        SetLit(false, true);
        Localizer.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= OnLanguageChanged;
        SetLit(false, true);
    }

    private void OnDestroy()
    {
        if (s_shown == this) s_shown = null;
    }

    private void LateUpdate()
    {
        if (!_highlight) return;
        SetLit(IsSelectionInRow(), false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        s_shown = this;
        Refresh();
    }

    // Llamado por OptionRowHover cuando el mouse entra en cualquier parte de la fila.
    public void OnRowHovered(PointerEventData eventData)
    {
        var es = EventSystem.current;
        if (!es) return;
        // Arrastrando el slider de otra fila: no robarle la selección a mitad del drag.
        if (eventData.dragging || eventData.pointerPress) return;
        // Esperando una tecla en un rebind (RebindRow apaga la navegación mientras tanto).
        if (!es.sendNavigationEvents) return;
        if (_selectable && !_selectable.IsInteractable()) return;
        // Ya está seleccionada la fila o una flecha/slider suyo: no pisar esa selección.
        if (IsSelectionInRow()) return;

        es.SetSelectedGameObject(gameObject);
    }

    private bool IsSelectionInRow()
    {
        var es = EventSystem.current;
        var selected = es ? es.currentSelectedGameObject : null;
        if (!selected) return false;
        return _rowRoot ? selected.transform.IsChildOf(_rowRoot) : selected == gameObject;
    }

    private void SetLit(bool on, bool force)
    {
        if (!_highlight || (!force && on == _lit)) return;
        _lit = on;
        _highlight.canvasRenderer.SetAlpha(on ? 1f : 0f);
    }

    private void OnLanguageChanged()
    {
        if (s_shown == this) Refresh();
    }

    private void Refresh()
    {
        if (_titleTarget && !string.IsNullOrEmpty(_titleKey)) _titleTarget.text = Localizer.Get(_titleKey);
        if (_descriptionTarget) _descriptionTarget.text = string.IsNullOrEmpty(_descriptionKey) ? "" : Localizer.Get(_descriptionKey);
    }
}
