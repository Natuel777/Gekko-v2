using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Placa de presion con runa: cuando Gekko la pisa, la piedra central baja, la runa (decal) se enciende con un fade suave y
// la placa alterna el estado de los espejos asignados (el que esta parado se acuesta y el acostado se levanta).
//
// Dos modos (Un Solo Uso):
//  - Tildado: la piedra queda hundida y la runa encendida, y la placa se activa una sola vez.
//  - Destildado: es una placa de presion de verdad. Cada vez que Gekko la pisa alterna los espejos; la piedra queda abajo
//    mientras Gekko esta encima y sube cuando se baja.
public class PressurePlateRuna : MonoBehaviour
{
    [Header("Piedra")]
    [SerializeField] Transform _piedra;
    [SerializeField] float _hundimiento = -0.07f;     // offset en Y local de la piedra
    [SerializeField] float _duracionBajada = 0.25f;   // lo que tarda en bajar (y en subir, si la placa es reutilizable)

    [Header("Runa")]
    [SerializeField] DecalProjector _runa;
    [SerializeField] float _delayRuna = 0.1f;          // espera desde que empieza a bajar la piedra
    [SerializeField] float _duracionRuna = 0.8f;
    [SerializeField, Range(0.5f, 1f)] float _escalaInicialRuna = 0.85f;

    [Header("Espejos")]
    [Tooltip("Espejos que alterna la placa cada vez que Gekko la pisa: el que esta parado se acuesta y el acostado se levanta. Una placa puede alterar varios espejos y un mismo espejo puede estar en varias placas: asi se arma el puzzle.")]
    [SerializeField] Mirror[] _espejos;

    [Header("Uso")]
    [Tooltip("Tildado: la placa se activa una sola vez y la piedra queda hundida. Destildado: se puede pisar todas las veces que se quiera; alterna los espejos cada vez que Gekko entra y la piedra sube cuando se baja.")]
    [SerializeField] bool _unSoloUso = false;

    [Header("Activable (opcional)")]
    [Tooltip("Cualquier objeto con IActivable: se le llama a Activated() cada vez que se pisa la placa. Para los espejos usar la lista de arriba, que alterna.")]
    [SerializeField] GameObject _objetoActivable;

    // Los colliders de Gekko que estan encima ahora. Gekko tiene mas de uno y cada uno avisa su entrada: sin llevar la cuenta
    // se alternaria una vez por collider (y dos cambios dejan el espejo como estaba).
    readonly HashSet<Collider> _jugadorDentro = new HashSet<Collider>();
    bool _activada;
    Coroutine _rutinaPiedra, _rutinaRuna;
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

    void OnDisable()
    {
        // Si la apagan con Gekko encima no va a llegar el OnTriggerExit.
        _jugadorDentro.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Player>() == null) return;

        // La placa se pisa una sola vez, cuando entra el primer collider de Gekko.
        DescartarColliders();
        bool estabaLibre = _jugadorDentro.Count == 0;
        _jugadorDentro.Add(other);

        if (!estabaLibre || (_unSoloUso && _activada)) return;

        Presionar();
    }

    void OnTriggerExit(Collider other)
    {
        if (!_jugadorDentro.Remove(other)) return;

        DescartarColliders();

        if (_jugadorDentro.Count == 0 && !_unSoloUso) Soltar();
    }

    // Un collider destruido o apagado con Gekko encima no avisa con OnTriggerExit: se lo descarta para no dar la placa por ocupada.
    void DescartarColliders()
    {
        _jugadorDentro.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
    }

    void Presionar()
    {
        _activada = true;

        Iniciar(ref _rutinaPiedra, MoverPiedra(true));
        Iniciar(ref _rutinaRuna, FundirRuna(true));
        StartCoroutine(ActuarTrasBajar());
    }

    void Soltar()
    {
        Iniciar(ref _rutinaPiedra, MoverPiedra(false));
        Iniciar(ref _rutinaRuna, FundirRuna(false));
    }

    // Corta la animacion anterior (si sigue corriendo) y arranca la nueva: pisar y salir rapido no deja dos animaciones
    // peleando por la piedra o la runa.
    void Iniciar(ref Coroutine rutina, IEnumerator nueva)
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(nueva);
    }

    // La piedra termina de bajar y recien ahi actua la placa. Va aparte de la animacion de la piedra: si Gekko sale antes de que
    // termine, esa animacion se corta pero la pisada ya cuenta.
    IEnumerator ActuarTrasBajar()
    {
        yield return new WaitForSeconds(_duracionBajada);

        AlternarEspejos();

        if (_objetoActivable != null && _objetoActivable.TryGetComponent(out IActivable activable))
            activable.Activated();
    }

    void AlternarEspejos()
    {
        if (_espejos == null) return;

        foreach (Mirror espejo in _espejos)
        {
            if (espejo != null) espejo.Toggle();
        }
    }

    // Baja la piedra (o la sube) desde donde este, con el rebote del final.
    IEnumerator MoverPiedra(bool hundir)
    {
        Vector3 desde = _piedra.localPosition;
        Vector3 destino = hundir ? _posInicialPiedra + Vector3.up * _hundimiento : _posInicialPiedra;

        for (float t = 0f; t < 1f; t += Time.deltaTime / _duracionBajada)
        {
            _piedra.localPosition = Vector3.LerpUnclamped(desde, destino, EaseOutBack(t));
            yield return null;
        }
        _piedra.localPosition = destino;
    }

    // Enciende la runa (con su delay y su escala) o la apaga con un fade. Parte del fade que tenga en ese momento, asi cortar
    // una animacion a medias no pega saltos.
    IEnumerator FundirRuna(bool encender)
    {
        if (encender)
        {
            yield return new WaitForSeconds(_delayRuna);
            _runa.enabled = true;
        }

        float fadeDesde = _runa.fadeFactor;
        float fadeHasta = encender ? 1f : 0f;
        float escalaDesde = Mathf.Lerp(_escalaInicialRuna, 1f, fadeDesde);

        for (float t = 0f; t < 1f; t += Time.deltaTime / _duracionRuna)
        {
            _runa.fadeFactor = Mathf.Lerp(fadeDesde, fadeHasta, Mathf.SmoothStep(0f, 1f, t));

            // Al encenderse la runa "se asienta" (crece hasta su tamano); al apagarse solo se desvanece.
            if (encender)
            {
                float escala = Mathf.Lerp(escalaDesde, 1f, EaseOutCubic(t));
                _runa.size = new Vector3(_tamanoRuna.x * escala, _tamanoRuna.y * escala, _tamanoRuna.z);
            }

            yield return null;
        }

        _runa.fadeFactor = fadeHasta;

        if (encender) _runa.size = _tamanoRuna;
        else _runa.enabled = false;
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
