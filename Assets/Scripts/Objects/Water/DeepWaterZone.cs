using System.Collections.Generic;
using UnityEngine;

// Marca un volumen de AGUA PROFUNDA: adentro Gekko puede bucear (ver GekkoSwimming). En el resto del agua solo nada en la
// superficie.
//
// Es una caja definida por el transform (posición, rotación en Y y escala) y _size, sin collider: no molesta a la lengua, a
// los raycasts ni a los triggers de Gekko. La cara de ARRIBA de la caja es la superficie del agua y la de abajo el fondo
// hasta donde se puede bajar. Hay que alinear la cara de arriba con el agua que se ve (gizmo celeste al seleccionarlo).
//
// Si el agua de arriba tiene un collider sólido en la layer Water (lakeCol, RiverSolidSurface...), no hace falta tocarlo:
// mientras nada, Gekko deja de chocar con la layer Water.
[DisallowMultipleComponent]
public class DeepWaterZone : MonoBehaviour
{
    private static readonly List<DeepWaterZone> Zones = new List<DeepWaterZone>();

    [Tooltip("Tamaño de la caja en metros (antes de la escala del transform). La cara de arriba es la superficie del agua.")]
    [SerializeField] private Vector3 _size = new Vector3(10f, 4f, 10f);

    [Tooltip("Corriente dentro de la zona, en m/s y en espacio local del objeto. 0 = agua quieta.")]
    [SerializeField] private Vector3 _current = Vector3.zero;

    // La zona que contiene worldPoint (la primera que encuentra). La altura cuenta hasta _reachAbove metros por encima de la
    // superficie, para detectar a Gekko justo cuando toca el agua.
    public static bool TryFind(Vector3 worldPoint, float reachAbove, out DeepWaterZone zone)
    {
        for(int i = 0; i < Zones.Count; i++)
        {
            if(Zones[i].Contains(worldPoint, reachAbove))
            {
                zone = Zones[i];
                return true;
            }
        }

        zone = null;
        return false;
    }

    public float SurfaceY => transform.TransformPoint(new Vector3(0f, _size.y * 0.5f, 0f)).y;
    public float BottomY => transform.TransformPoint(new Vector3(0f, -_size.y * 0.5f, 0f)).y;
    public Vector3 Current => transform.TransformDirection(_current);

    private void OnEnable() => Zones.Add(this);
    private void OnDisable() => Zones.Remove(this);

    private bool Contains(Vector3 worldPoint, float reachAbove)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 half = _size * 0.5f;

        if(Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z) return false;

        float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        return local.y >= -half.y && local.y <= half.y + reachAbove / scaleY;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.45f, 0.9f, 0.12f);
        Gizmos.DrawCube(Vector3.zero, _size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, _size);
        // La superficie, más marcada.
        Gizmos.DrawWireCube(new Vector3(0f, _size.y * 0.5f, 0f), new Vector3(_size.x, 0f, _size.z));
    }
}
