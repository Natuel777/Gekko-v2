using System.Collections;
using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Base de todo objeto que reacciona cuando la onda de SU región lo alcanza. Pertenece a una sola región:
    /// la que se le asigna a mano o, si no, la que lo contiene por posición al arrancar. La onda de otra región
    /// nunca lo dispara.
    ///
    /// Solo trabaja mientras su región anima: chequea cada <see cref="IntervaloChequeo"/> segundos si la onda llegó,
    /// llama <see cref="OnReached"/> una sola vez y se deshabilita. Pensado para decenas de objetos (plantas,
    /// barreras, partículas); lo masivo (pasto, hojas, flores dispersas) va por shader.
    /// </summary>
    public abstract class PurifyReactive : MonoBehaviour
    {
        private const float IntervaloChequeo = 0.1f;
        private static readonly WaitForSeconds EsperaChequeo = new(IntervaloChequeo);

        [Header("Región")]
        [Tooltip("Región a la que pertenece. Si se deja vacía, se busca por posición al arrancar (la región que lo contiene).")]
        [SerializeField] private PurifyRegion _regionOverride;
        [Tooltip("Espera al azar (segundos) entre que llega la onda y la reacción, para que no reaccione todo en el mismo frame.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float _retrasoAleatorio = 0.15f;

        private PurifyRegion _region;
        private Coroutine _espera;
        private bool _disparado;

        public PurifyRegion Region => _region;
        public PurifyRegion RegionOverride => _regionOverride;
        public bool Disparado => _disparado;

        protected virtual void Start()
        {
            _region = _regionOverride != null ? _regionOverride : PurifyRegion.Buscar(transform.position);

            if(_region == null)
            {
                Debug.LogWarning($"[{name}] {GetType().Name} fuera de toda región de purificación: no va a reaccionar.", this);
                enabled = false;
                return;
            }

            if(_region.Purificada)
            {
                ApplyPurifiedInstant();
                return;
            }

            _region.OnPurifyStarted.AddListener(OnRegionStarted);
            _region.OnPurifyCompleted.AddListener(OnRegionCompleted);

            // La región pudo haber arrancado antes que este Start.
            if(_region.Animando) OnRegionStarted();
        }

        protected virtual void OnDestroy()
        {
            if(_region == null) return;

            _region.OnPurifyStarted.RemoveListener(OnRegionStarted);
            _region.OnPurifyCompleted.RemoveListener(OnRegionCompleted);
        }

        /// <summary>Deja el objeto en su estado purificado final, sin animación (cargar partidas, región ya purificada).</summary>
        public void ApplyPurifiedInstant()
        {
            if(_disparado) return;
            _disparado = true;

            DetenerEspera();
            OnPurifiedInstant();
            enabled = false;
        }

        /// <summary>La onda llegó a este objeto. Se llama una sola vez.</summary>
        protected abstract void OnReached();

        /// <summary>Estado final sin animación.</summary>
        protected abstract void OnPurifiedInstant();

        private void OnRegionStarted()
        {
            if(_disparado || _espera != null || !isActiveAndEnabled) return;

            _espera = StartCoroutine(EsperarOnda());
        }

        // Por si la onda termina entre dos chequeos (región chica u objeto en el borde).
        private void OnRegionCompleted()
        {
            if(_disparado) return;

            DetenerEspera();
            Disparar();
        }

        private IEnumerator EsperarOnda()
        {
            while(!_region.OndaAlcanzo(transform.position))
                yield return EsperaChequeo;

            if(_retrasoAleatorio > 0f) yield return new WaitForSeconds(Random.Range(0f, _retrasoAleatorio));

            _espera = null;
            Disparar();
        }

        private void Disparar()
        {
            if(_disparado) return;
            _disparado = true;

            OnReached();
            enabled = false;
        }

        private void DetenerEspera()
        {
            if(_espera != null) StopCoroutine(_espera);
            _espera = null;
        }
    }
}
