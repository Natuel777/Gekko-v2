using UnityEngine;
using UnityEngine.EventSystems;

// Se agrega solo (desde OptionRowInfo) en la raíz de cada fila de Opciones (ej. Lighting_Row).
// PointerEnter sube por toda la jerarquía, así que pasar el mouse por cualquier parte de la fila
// (fondo, label, flechas, slider) llega acá y se reenvía al OptionRowInfo de esa fila.
public class OptionRowHover : MonoBehaviour, IPointerEnterHandler
{
    public OptionRowInfo Owner { get; set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Owner) Owner.OnRowHovered(eventData);
    }
}
