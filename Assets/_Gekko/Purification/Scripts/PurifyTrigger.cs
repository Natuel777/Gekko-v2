using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Gekko.Purification
{
    /// <summary>
    /// Dispara la purificación de UNA región cuando pasa algo del juego, sin tocar los scripts existentes:
    ///  - un núcleo (<see cref="PurificationCore"/>) se rompe,
    ///  - un desafío (<see cref="PurificationChallenge"/>) se completa (la planta quedó purificada),
    ///  - o cualquier UnityEvent llama a <see cref="Disparar"/> (fin de un puzzle, un trigger, etc.).
    /// Si hay varias fuentes asignadas, dispara con la primera que ocurra.
    ///
    /// Al disparar: hitstop corto, temblor de cámara (el mismo evento "OnCameraShake" que ya usa el juego),
    /// evento <see cref="OnDisparado"/> para VFX/sonido, y la onda de la región.
    /// Los toques necesarios los sigue manejando el propio núcleo o la planta (_hitsToBreak / hitsToPurify).
    /// </summary>
    public class PurifyTrigger : MonoBehaviour
    {
        [Header("Región")]
        [Tooltip("Región que se purifica. Si se deja vacía, usa la PurifyRegion de este mismo objeto.")]
        [SerializeField] private PurifyRegion _region;

        [Header("Fuentes (opcionales)")]
        [Tooltip("Núcleo que dispara la región al romperse.")]
        [SerializeField] private PurificationCore _nucleo;
        [Tooltip("Desafío que dispara la región al completarse (planta purificada).")]
        [SerializeField] private PurificationChallenge _desafio;

        [Header("Impacto")]
        [Tooltip("Congelar el juego un instante al disparar (hitstop).")]
        [SerializeField] private bool _hitstop = true;
        [Tooltip("Escala de tiempo durante el hitstop.")]
        [Range(0f, 1f)]
        [SerializeField] private float _hitstopEscala = 0.05f;
        [Tooltip("Duración del hitstop en segundos reales.")]
        [Min(0f)]
        [SerializeField] private float _hitstopSegundos = 0.08f;
        [Tooltip("Fuerza del temblor de cámara (0 = sin temblor). Mismo evento que CameraShakeSignalRelay (1.5 por defecto ahí).")]
        [Min(0f)]
        [SerializeField] private float _fuerzaTemblor = 1.5f;

        [Header("Eventos")]
        [Tooltip("Al disparar, antes de la onda (VFX de astillas, flash, sonido).")]
        public UnityEvent OnDisparado = new();

        // Hitstop compartido entre todos los disparadores: si varios disparan juntos (o coincide con el hitstop de
        // DamageFeedback), solo el último en terminar restaura, y siempre a la escala que había antes del primero.
        private const float SegundosControl = 0.2f;
        private static int _hitstopsActivos;
        private static float _escalaOriginal = 1f;

        private bool _disparado;
        private bool _enHitstop;

        /// <summary>La región asignada, o la del mismo objeto si no hay ninguna (también en Edit Mode).</summary>
        public PurifyRegion Region => _region != null ? _region : GetComponent<PurifyRegion>();
        /// <summary>Si tiene algún núcleo o desafío asignado (si no, solo dispara por UnityEvent).</summary>
        public bool TieneFuente => _nucleo != null || _desafio != null;

        // Con "Enter Play Mode Options" sin recarga de dominio los estáticos sobreviven entre sesiones de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetearEstaticos()
        {
            _hitstopsActivos = 0;
            _escalaOriginal = 1f;
        }

        private void Awake()
        {
            if(_region == null) _region = GetComponent<PurifyRegion>();
            if(_region == null) Debug.LogWarning($"[{name}] PurifyTrigger sin región asignada.", this);
        }

        private void OnEnable()
        {
            if(_nucleo != null) _nucleo.Broken += OnNucleoRoto;
            if(_desafio != null) _desafio.Completed += Disparar;
        }

        private void OnDisable()
        {
            if(_nucleo != null) _nucleo.Broken -= OnNucleoRoto;
            if(_desafio != null) _desafio.Completed -= Disparar;

            TerminarHitstop();
        }

        // Si la fuente ya había ocurrido antes de arrancar (núcleo roto o desafío completo), la región queda
        // purificada directo, sin animación ni impacto.
        private void Start()
        {
            bool yaOcurrio = (_nucleo != null && _nucleo.IsBroken) ||
                             (_desafio != null && _desafio.State == PurificationChallenge.ChallengeState.Completed);

            if(!yaOcurrio || _region == null) return;

            _disparado = true;
            _region.SetPurifiedInstant();
        }

        private void OnNucleoRoto(PurificationCore nucleo) => Disparar();

        /// <summary>Purifica la región con impacto. Público para llamarlo desde UnityEvents. Solo dispara una vez.</summary>
        public void Disparar()
        {
            if(_disparado || _region == null) return;
            _disparado = true;

            OnDisparado.Invoke();

            if(_fuerzaTemblor > 0f) EventManager.Trigger<float>("OnCameraShake", _fuerzaTemblor);
            if(_hitstop && isActiveAndEnabled) StartCoroutine(Hitstop());

            _region.Purify();
        }

        private IEnumerator Hitstop()
        {
            // Pausado o congelado por otro hitstop (timeScale 0): no se hace nada.
            if(_enHitstop || Time.timeScale <= 0f) yield break;

            if(_hitstopsActivos == 0) _escalaOriginal = Time.timeScale;
            _hitstopsActivos++;
            _enHitstop = true;
            Time.timeScale = Mathf.Min(Time.timeScale, _hitstopEscala);

            yield return new WaitForSecondsRealtime(_hitstopSegundos);

            TerminarHitstop();

            // Control: DamageFeedback guarda la escala que encuentra y la restaura al terminar. Si arrancó durante
            // este hitstop, puede devolver 0.05 después de que este ya restauró. Exactamente esa escala es la marca
            // de este hitstop: si quedó así y nadie más está en hitstop, se vuelve a la normal.
            yield return new WaitForSecondsRealtime(SegundosControl);

            if(_hitstopsActivos == 0 && Mathf.Approximately(Time.timeScale, _hitstopEscala))
                Time.timeScale = _escalaOriginal;
        }

        // Idempotente: también desde OnDisable, para que nunca quede el juego en cámara lenta.
        private void TerminarHitstop()
        {
            if(!_enHitstop) return;
            _enHitstop = false;
            _hitstopsActivos = Mathf.Max(0, _hitstopsActivos - 1);

            // Si alguien pausó o congeló (timeScale 0) no se le pisa; el control de arriba corrige lo que quede.
            if(_hitstopsActivos == 0 && Time.timeScale > 0f) Time.timeScale = _escalaOriginal > 0f ? _escalaOriginal : 1f;
        }
    }
}
