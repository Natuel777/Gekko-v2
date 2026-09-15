using UnityEngine;

/// <summary>
/// Un arándano levantable por CONTACTO (igual que en el juego real: Gekko lo toca y ya
/// está, no hace falta la lengua). Se pone en una instancia de PF_CollectableBlueberry SIN
/// su componente Blueberry original (ese depende de GameManager, que no existe en esta
/// escena de prueba) — este es el reemplazo autónomo para el sandbox.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GeckoBlueberryPickup : MonoBehaviour
{
    [Tooltip("Si está, se destruye al agarrarlo. Si no, solo se desactiva.")]
    [SerializeField] private bool _destroyOnPickup = true;

    private void OnTriggerEnter(Collider other)
    {
        var combo = other.GetComponentInParent<GeckoBlueberryCombo>();
        if (combo == null) return;

        combo.OnCollect();
        if (_destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
