using System.Collections.Generic;
using UnityEngine;

// Marca dónde se puede NADAR en un río que tiene "Nadar solo en zonas" prendido (RiverSolidSurface): pozones, remansos,
// piletas al pie de una cascada. Fuera de estas zonas ese río se comporta como antes del nado: Gekko camina y resbala sobre
// la superficie y la corriente lo arrastra. Así el río no sirve de atajo para cruzar el nivel nadando.
//
// Es solo una caja (transform + _size, sin collider). A diferencia de DeepWaterZone, NO define la superficie ni la corriente:
// las sigue poniendo el río, así que la caja no tiene que coincidir exacto con el agua. Solo tiene que cubrir el pozón con
// margen: desde un poco por encima de la superficie (para detectar a Gekko cuando cae o llega resbalando) hasta el fondo.
//
// Si Gekko sale de la zona nadando o buceando, se lo deja arriba del agua y el río lo sigue llevando (ver GekkoSwimming).
[DisallowMultipleComponent]
public class SwimZone : MonoBehaviour
{
    private static readonly List<SwimZone> Zones = new List<SwimZone>();

    [Tooltip("Tamaño de la caja en metros (antes de la escala del transform). Que cubra el pozón entero, de la superficie (con margen arriba) al fondo.")]
    [SerializeField] private Vector3 _size = new Vector3(15f, 5f, 15f);

    [Tooltip("Nadando en la zona, qué parte de la corriente del río arrastra a Gekko. Un pozón es agua mansa: con poco, Gekko puede quedarse nadando sin que el río se lo lleve. Negativo = la del río (RiverSolidSurface).")]
    [Range(-1f, 1.5f)]
    [SerializeField] private float _currentFactor = 0.25f;

    public float CurrentFactor => _currentFactor;

    public static bool Contains(Vector3 worldPoint) => TryFind(worldPoint, out _);

    public static bool TryFind(Vector3 worldPoint, out SwimZone zone)
    {
        for(int i = 0; i < Zones.Count; i++)
        {
            if(Zones[i].ContainsPoint(worldPoint))
            {
                zone = Zones[i];
                return true;
            }
        }
        zone = null;
        return false;
    }

    private void OnEnable() => Zones.Add(this);
    private void OnDisable() => Zones.Remove(this);

    private bool ContainsPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 half = _size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.1f, 0.9f, 0.6f, 0.12f);
        Gizmos.DrawCube(Vector3.zero, _size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.7f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, _size);
    }
}
