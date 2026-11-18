using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Va en el Selectable de cada fila (o pestaña) del menú de Opciones.
// Al seleccionarse: prende el resaltado de su fila y escribe título + descripción en el panel derecho.
// Los textos son claves del Localizer. El último que escribió en el panel es el "dueño" del texto y lo vuelve a
// escribir si cambia el idioma, aunque ya no esté seleccionado (ej. al cambiar el idioma con las flechas, la que
// queda seleccionada es la flecha y no la fila/pestaña que había escrito la descripción).
public class OptionRowInfo : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private string _titleKey;
    [SerializeField] private string _descriptionKey;

    [Tooltip("Fondo de la fila que se prende solo mientras está seleccionada (opcional).")]
    [SerializeField] private Graphic _highlight;

    [Header("Panel de descripción")]
    [SerializeField] private TMP_Text _titleTarget;
    [SerializeField] private TMP_Text _descriptionTarget;

    // Último que escribió en el panel de descripción (uno solo a la vez).
    private static OptionRowInfo s_shown;

    private void OnEnable()
    {
        // La fila puede volver a activarse (cambio de pestaña) sin recibir OnDeselect: arranca apagada.
        if (_highlight) _highlight.enabled = false;
        Localizer.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        Localizer.OnLanguageChanged -= OnLanguageChanged;
        if (_highlight) _highlight.enabled = false;
    }

    private void OnDestroy()
    {
        if (s_shown == this) s_shown = null;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (_highlight) _highlight.enabled = true;
        s_shown = this;
        Refresh();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (_highlight) _highlight.enabled = false;
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
