using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Placa de presion con runa: cuando Gekko la pisa, la piedra central baja y queda hundida,
// y la runa (decal) se enciende con un fade suave. Se activa una sola vez.
public class PressurePlateRuna : MonoBehaviour
{
    [Header("Piedra")]
    [SerializeField] Transform _piedra;
    [SerializeField] float _hundimiento = -0.07f;     // offset en Y local de la piedra
    [SerializeField] float _duracionBajada = 0.25f;

    [Header("Runa")]
    [SerializeField] DecalProjector _runa;
    [SerializeField] float _delayRuna = 0.1f;          // espera desde que empieza a bajar la piedra
    [SerializeField] float _duracionRuna = 0.8f;
    [SerializeField, Range(0.5f, 1f)] float _escalaInicialRuna = 0.85f;

    [Header("Activable (opcional)")]
    [SerializeField] GameObject _objetoActivable;

    bool _activada;
    Vector3 _posInicialPiedra;
    Vector3 _tamanoRuna;

    void Awake()
    {
        _posInicialPiedra = _piedra.localPosition;
        _tamanoRuna = _runa.size;

        // La runa arranca apagada hasta que Gekko pise la placa
        _runa.fadeFactor = 0f;
        _runa.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_activada || other.GetComponentInParent<Player>() == null) return;

        _activada = true;
        StartCoroutine(BajarPiedra());
        StartCoroutine(EncenderRuna());
    }

    IEnumerator BajarPiedra()
    {
        Vector3 destino = _posInicialPiedra + Vector3.up * _hundimiento;

        for (float t = 0f; t < 1f; t += Time.deltaTime / _duracionBajada)
        {
            _piedra.localPosition = Vector3.LerpUnclamped(_posInicialPiedra, destino, EaseOutBack(t));
            yield return null;
        }
        _piedra.localPosition = destino;

        if (_objetoActivable != null && _objetoActivable.TryGetComponent(out IActivable activable))
            activable.Activated();
    }

    IEnumerator EncenderRuna()
    {
        yield return new WaitForSeconds(_delayRuna);

        _runa.enabled = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / _duracionRuna)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            _runa.fadeFactor = k;
            float escala = Mathf.Lerp(_escalaInicialRuna, 1f, EaseOutCubic(t));
            _runa.size = new Vector3(_tamanoRuna.x * escala, _tamanoRuna.y * escala, _tamanoRuna.z);
            yield return null;
        }
        _runa.fadeFactor = 1f;
        _runa.size = _tamanoRuna;
    }

    // Pequeño rebote al final para que la piedra "haga click"
    static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    static float EaseOutCubic(float t)
    {
        t = 1f - t;
        return 1f - t * t * t;
    }
}
