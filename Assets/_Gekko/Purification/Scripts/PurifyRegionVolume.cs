using UnityEngine;
using UnityEngine.Rendering;

namespace Gekko.Purification
{
    /// <summary>
    /// Atmósfera local de una región (opcional): un Volume local (Is Global apagado, con collider y Blend Distance)
    /// con su propia corrupción, que baja a peso 0 a medida que avanza la onda de SU región. Así, si el jugador está
    /// dentro de una región ya purificada se ve sana aunque el resto siga oscuro.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PurifyRegionVolume : MonoBehaviour
    {
        [Tooltip("Región que controla este Volume. Si se deja vacía, la que contiene a este objeto.")]
        [SerializeField] private PurifyRegion _region;
        [Tooltip("Peso del Volume mientras la región está corrupta.")]
        [Range(0f, 1f)]
        [SerializeField] private float _pesoCorrupto = 1f;

        private Volume _volume;

        private void Awake()
        {
            _volume = GetComponent<Volume>();
        }

        private void Start()
        {
            if(_region == null) _region = PurifyRegion.Buscar(transform.position);

            if(_region == null)
            {
                Debug.LogWarning($"[{name}] PurifyRegionVolume sin región: el Volume queda como está.", this);
                enabled = false;
                return;
            }

            _volume.weight = _region.Purificada ? 0f : _pesoCorrupto * (1f - _region.Progreso);
            _region.OnProgress.AddListener(OnProgress);
            _region.OnPurifyCompleted.AddListener(OnCompleted);
        }

        private void OnDestroy()
        {
            if(_region == null) return;

            _region.OnProgress.RemoveListener(OnProgress);
            _region.OnPurifyCompleted.RemoveListener(OnCompleted);
        }

        private void OnProgress(float progreso) => _volume.weight = _pesoCorrupto * (1f - progreso);

        private void OnCompleted() => _volume.weight = 0f;
    }
}
