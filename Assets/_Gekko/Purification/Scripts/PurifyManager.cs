using UnityEngine;

namespace Gekko.Purification
{
    /// <summary>
    /// Único en la escena. Junta los nodos de todas las <see cref="PurifyRegion"/> habilitadas y los sube al shader
    /// como globales, solo en los frames en que alguna cambió. Wrapper fino sobre <see cref="PurifyShaderGlobals"/>,
    /// siguiendo el patrón del proyecto: el MonoBehaviour solo reenvía el ciclo de vida de Unity.
    ///
    /// Sin PurifyManager (o con el manager apagado) los shaders ven cero nodos y todo se dibuja sano: los materiales
    /// compartidos no cambian en escenas que no usan el sistema.
    /// </summary>
    public class PurifyManager : MonoBehaviour
    {
        /// <summary>Tiene que coincidir con PURIFY_MAX_SPHERES en PurifyMask.hlsl.</summary>
        public const int MaxEsferas = 32;

        private const float MargenPorDefecto = 4f;

        public static PurifyManager Instance { get; private set; }

        /// <summary>Metros que la onda sigue avanzando después del radio máximo, para cubrir el ruido del borde.</summary>
        public static float MargenOnda => Instance != null ? Instance._margenOnda : MargenPorDefecto;

        [Header("Onda")]
        [Tooltip("Ancho del frente brillante de la onda, en metros.")]
        [Min(0.01f)]
        [SerializeField] private float _anchoBorde = 1.5f;
        [Tooltip("Ancho del fundido entre lo corrupto y lo sano en el límite de cada región, en metros.")]
        [Min(0.01f)]
        [SerializeField] private float _fundidoArea = 3f;
        [Tooltip("Metros extra que avanza la onda más allá del radio máximo. Tiene que ser mayor que la irregularidad del borde de los materiales (NoiseAmount ≈ 2.5) más el ancho del borde, o quedan manchas corruptas en el límite.")]
        [Min(0f)]
        [SerializeField] private float _margenOnda = MargenPorDefecto;

        private PurifyShaderGlobals _globales;

        /// <summary>Regiones purificadas / regiones totales (1 si no hay regiones). Lo usa la atmósfera.
        /// Se calcula en el momento (son pocas regiones) para no depender del orden de arranque.</summary>
        public float NivelPurificacionGlobal
        {
            get
            {
                var regiones = PurifyRegion.Activas;
                if(regiones.Count == 0) return 1f;

                int purificadas = 0;
                for(int i = 0; i < regiones.Count; i++)
                    if(regiones[i].Purificada) purificadas++;

                return (float)purificadas / regiones.Count;
            }
        }

        private void Awake()
        {
            if(Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{name}] Ya hay un PurifyManager en la escena ({Instance.name}); este se apaga.", this);
                enabled = false;
                return;
            }

            Instance = this;
            _globales = new PurifyShaderGlobals(this);
        }

        // Al arrancar, cada región sube su estado actual (corrupta, salvo que alguien la haya purificado antes).
        private void OnEnable()
        {
            if(_globales == null) return;

            _globales.SubirParametros(_anchoBorde, _fundidoArea);
            _globales.Subir(PurifyRegion.Activas);
        }

        private void LateUpdate()
        {
            _globales.ArtificialLateUpdate(PurifyRegion.Activas);
        }

        private void OnDisable()
        {
            if(_globales != null) _globales.Limpiar();
        }

        private void OnDestroy()
        {
            if(Instance == this) Instance = null;
        }

        // Para ajustar el borde en Play Mode y verlo al instante.
        private void OnValidate()
        {
            if(Application.isPlaying && isActiveAndEnabled && _globales != null)
                _globales.SubirParametros(_anchoBorde, _fundidoArea);
        }
    }
}
