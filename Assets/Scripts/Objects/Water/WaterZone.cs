using System.Collections.Generic;
using UnityEngine;

// Marca un objeto como AGUA para Gekko (ver GekkoWater), para el agua que NO tiene un collider sólido en la layer Water.
// Es el caso del agua "visual": planos y cascadas (Water, Waterfall...) por los que Gekko camina sin ningún collider
// activo, así que el raycast hacia abajo de GekkoWater no los ve.
//
// La zona sale del Renderer del mismo objeto, no hace falta ningún collider:
//   - La huella es la caja de la malla sobre el plano XZ del objeto. Se calcula en el espacio local del objeto, así que
//     sirve con cualquier rotación y escala.
//   - La altura se mide desde la superficie (la parte más alta de la malla): Gekko está en el agua si está hasta _depth
//     metros por debajo de ella (el fondo del lago, por ejemplo) o hasta _heightAbove metros por encima (metido hasta
//     los pies). Más abajo de _depth ya no cuenta: así un nivel de más abajo no queda "en el agua" por tener un lago
//     encima.
//
// Se ve en la Scene View al seleccionar el objeto (caja celeste). Para marcar toda el agua de una escena de una vez está
// la herramienta Tools > Gekko > Marcar agua (WaterZone) en la escena abierta.
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class WaterZone : MonoBehaviour
{
    // Las zonas que están prendidas ahora mismo. Se llevan a mano (OnEnable/OnDisable) para que preguntar si Gekko está en
    // el agua no tenga que buscar objetos en la escena: son unas pocas zonas y la consulta corre en cada frame.
    private static readonly List<WaterZone> Zones = new List<WaterZone>();

    [Tooltip("Metros por debajo de la superficie que siguen contando como agua (el fondo). Más abajo, ya no.")]
    [SerializeField, Min(0f)] private float _depth = 2f;

    [Tooltip("Metros por encima de la superficie que todavía cuentan como agua (Gekko metido hasta los pies).")]
    [SerializeField, Min(0f)] private float _heightAbove = 0.5f;

    private Renderer _renderer;

    // true si worldPoint está dentro de alguna zona de agua.
    public static bool Contains(Vector3 worldPoint)
    {
        for(int i = 0; i < Zones.Count; i++)
        {
            if(Zones[i].IsInside(worldPoint)) return true;
        }

        return false;
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        Zones.Add(this);
    }

    private void OnDisable()
    {
        Zones.Remove(this);
    }

    private bool IsInside(Vector3 worldPoint)
    {
        if(_renderer == null) return false;

        Bounds bounds = _renderer.localBounds;
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        // Huella: dentro de la caja de la malla, en el plano XZ del objeto.
        if(local.x < bounds.min.x || local.x > bounds.max.x || local.z < bounds.min.z || local.z > bounds.max.z) return false;

        // Altura sobre la superficie, en metros (la escala del objeto estira la malla): positiva por encima del agua,
        // negativa por debajo.
        float heightOverSurface = (local.y - bounds.max.y) * transform.lossyScale.y;

        return heightOverSurface <= _heightAbove && heightOverSurface >= -_depth;
    }

    // La zona en la Scene View: la huella de la malla, desde _depth por debajo de la superficie hasta _heightAbove por
    // encima. Sirve para ajustar los dos valores.
    private void OnDrawGizmosSelected()
    {
        Renderer r = _renderer != null ? _renderer : GetComponent<Renderer>();

        if(r == null) return;

        Bounds bounds = r.localBounds;
        float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float top = bounds.max.y + _heightAbove / scaleY;
        float bottom = bounds.max.y - _depth / scaleY;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(bounds.center.x, (top + bottom) * 0.5f, bounds.center.z), new Vector3(bounds.size.x, top - bottom, bounds.size.z));
    }
}
