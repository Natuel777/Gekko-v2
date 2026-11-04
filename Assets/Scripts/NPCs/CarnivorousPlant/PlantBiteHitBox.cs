using UnityEngine;

// Zona de daño del mordisco: el GameObject "HitBox" de la planta (hijo del hueso de la cabeza, con un BoxCollider trigger).
//
// Está apagado salvo durante la embestida: CarnivorousPlant lo prende cuando el mordisco sale de verdad (no durante la
// preparación) y lo apaga al terminar. Mientras está prendido, si el Gekko está dentro le avisa a la planta, que aplica el
// daño una sola vez por mordisco.
//
// Qué dispara el ataque no es esta caja sino el biteRange de la planta: estar en rango hace que ataque, estar dentro del HitBox
// hace que pegue.
[RequireComponent(typeof(BoxCollider))]
public class PlantBiteHitBox : MonoBehaviour
{
    private CarnivorousPlant _plant;

    // La planta lo llama en su Awake. No se puede usar el Awake de acá: el HitBox arranca apagado.
    public void Initialize(CarnivorousPlant plant) { _plant = plant; }

    // Enter y Stay: si el hitbox se prende con el Gekko ya adentro o éste entra a mitad de la embestida, alguno de los dos lo agarra.
    private void OnTriggerEnter(Collider other) { TryHit(other); }
    private void OnTriggerStay(Collider other) { TryHit(other); }

    private void TryHit(Collider other)
    {
        if(_plant == null) return;

        Player player = other.GetComponentInParent<Player>();
        if(player != null) _plant.OnBiteHitBoxContact(player);
    }

    #if UNITY_EDITOR
    public void DrawGizmo(Color color)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if(box == null) return;

        Gizmos.color = color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
    #endif
}
