using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Insecto / collectible de prueba para la lengua del gecko. Ponelo en un objeto con
/// Collider. Cuando el gecko lo come, se dispara <see cref="onEaten"/> y el objeto se
/// destruye (o se desactiva, según <see cref="_destroyOnEat"/>).
///
/// Para bichos "de verdad" (Bug, Collectible del juego principal) hacé un componente
/// puente que implemente <see cref="IGeckoEdible"/> y llame a la lógica que ya tengan.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GeckoEdible : MonoBehaviour, IGeckoEdible
{
    [Tooltip("Si está, se destruye al comerlo. Si no, solo se desactiva.")]
    [SerializeField] private bool _destroyOnEat = true;
    [SerializeField] private UnityEvent onEaten;

    private bool _hooked;

    public Transform Transform => transform;
    public bool CanBeEaten => !_hooked;

    public void OnHooked()
    {
        _hooked = true;
        if (TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        if (TryGetComponent(out Collider col)) col.enabled = false;
    }

    public void Eat()
    {
        onEaten?.Invoke();
        if (_destroyOnEat) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
