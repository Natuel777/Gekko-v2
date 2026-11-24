using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Rune pressure plate: when Gekko steps on it, the central stone sinks, the rune (decal) lights up with a soft fade and
// the plate toggles the state of the assigned mirrors (a standing one lies down and a lying one stands up).
//
// Two modes (Un Solo Uso field):
//  - Checked: the stone stays sunk and the rune stays lit, and the plate activates only once.
//  - Unchecked: it is a real pressure plate. Every time Gekko steps on it, it toggles the mirrors; the stone stays down
//    while Gekko is on top and goes back up when Gekko steps off.
public class PressurePlateRuna : MonoBehaviour
{
    [Header("Stone")]
    [SerializeField] Transform _piedra;
    [SerializeField] float _hundimiento = -0.07f;     // local Y offset of the stone
    [SerializeField] float _duracionBajada = 0.25f;   // how long it takes to go down (and up, if the plate is reusable)

    [Header("Rune")]
    [SerializeField] DecalProjector _runa;
    [SerializeField] float _delayRuna = 0.1f;          // wait from when the stone starts sinking
    [SerializeField] float _duracionRuna = 0.8f;
    [SerializeField, Range(0.5f, 1f)] float _escalaInicialRuna = 0.85f;

    [Header("Mirrors")]
    [Tooltip("Espejos que alterna la placa cada vez que Gekko la pisa: el que esta parado se acuesta y el acostado se levanta. Una placa puede alterar varios espejos y un mismo espejo puede estar en varias placas: asi se arma el puzzle.")]
    [SerializeField] Mirror[] _espejos;

    [Header("Usage")]
    [Tooltip("Tildado: la placa se activa una sola vez y la piedra queda hundida. Destildado: se puede pisar todas las veces que se quiera; alterna los espejos cada vez que Gekko entra y la piedra sube cuando se baja.")]
    [SerializeField] bool _unSoloUso = false;

    [Header("Activable (optional)")]
    [Tooltip("Cualquier objeto con IActivable: se le llama a Activated() cada vez que se pisa la placa. Para los espejos usar la lista de arriba, que alterna.")]
    [SerializeField] GameObject _objetoActivable;

    // Gekko's colliders that are on top right now. Gekko has more than one and each reports its own entry: without keeping count,
    // it would toggle once per collider (and two toggles leave the mirror as it was).
    readonly HashSet<Collider> _jugadorDentro = new HashSet<Collider>();
    bool _activada;
    Coroutine _rutinaPiedra, _rutinaRuna;
    Vector3 _posInicialPiedra;
    Vector3 _tamanoRuna;

    void Awake()
    {
        _posInicialPiedra = _piedra.localPosition;
        _tamanoRuna = _runa.size;

        // The rune starts off until Gekko steps on the plate
        _runa.fadeFactor = 0f;
        _runa.enabled = false;
    }

    void OnDisable()
    {
        // If it is turned off with Gekko on top, OnTriggerExit will never arrive.
        _jugadorDentro.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Player>() == null) return;

        // The plate is stepped on only once, when Gekko's first collider enters.
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

    // A collider destroyed or disabled with Gekko on top doesn't notify with OnTriggerExit: it is discarded so the plate isn't considered occupied.
    void DescartarColliders()
    {
        _jugadorDentro.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
    }

    void Presionar()
    {
        _activada = true;

        // Plays as soon as Gekko steps on it, together with the stone sinking (it doesn't wait for it to finish going down). Once per step:
        // OnTriggerEnter already discards Gekko's other colliders and, with Un Solo Uso, every step after the first.
        if (AudioManager.instance) AudioManager.instance.Play(SoundNames.PlankPlaced);

        Iniciar(ref _rutinaPiedra, MoverPiedra(true));
        Iniciar(ref _rutinaRuna, FundirRuna(true));
        StartCoroutine(ActuarTrasBajar());
    }

    void Soltar()
    {
        Iniciar(ref _rutinaPiedra, MoverPiedra(false));
        Iniciar(ref _rutinaRuna, FundirRuna(false));
    }

    // Stops the previous animation (if it is still running) and starts the new one: stepping on and quickly leaving doesn't leave two animations
    // fighting over the stone or the rune.
    void Iniciar(ref Coroutine rutina, IEnumerator nueva)
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(nueva);
    }

    // The stone finishes going down and only then does the plate act. It is separate from the stone's animation: if Gekko leaves before it
    // finishes, that animation is cut but the step already counts.
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

    // Lowers the stone (or raises it) from wherever it is, with the bounce at the end.
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

    // Lights the rune (with its delay and scale) or turns it off with a fade. It starts from whatever fade it has at that moment, so cutting
    // an animation midway doesn't cause jumps.
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

            // When the rune lights up it "settles" (grows to its size); when it turns off it just fades out.
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

    // Small bounce at the end so the stone "clicks"
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
