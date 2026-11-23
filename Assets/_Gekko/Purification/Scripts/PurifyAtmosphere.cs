using UnityEngine;
using UnityEngine.Rendering;

namespace Gekko.Purification
{
    /// <summary>
    /// Atmósfera global gradual: lee <see cref="PurifyManager.NivelPurificacionGlobal"/> (regiones purificadas /
    /// total) y lleva suave, sin saltos, hacia ese nivel:
    ///  - el peso del Volume global "Corrupto" (1 - nivel),
    ///  - la niebla (color y densidad, o distancia si la niebla es lineal),
    ///  - la intensidad y el color de la luz direccional,
    ///  - el skybox (mezcla entre el de la escena y uno corrupto con el mismo shader) y la luz ambiente.
    /// Con 1 de 4 regiones purificadas, todo se ve un 25 % menos corrupto.
    ///
    /// El estado "sano" son los valores que tiene la escena al arrancar (lo que armó el equipo); acá solo se
    /// configura el estado corrupto. Al apagarse deja la escena como estaba.
    /// La luz de los niveles es realtime: si algún día se hornea, cambiar la luz no va a mover las sombras
    /// horneadas y el cambio se tiene que vender con el Volume y la niebla.
    /// </summary>
    public class PurifyAtmosphere : MonoBehaviour
    {
        [Header("Transición")]
        [Tooltip("Segundos que tarda la atmósfera en recorrer de punta a punta (0 a 100 %). Purificar 1 de 4 regiones tarda un cuarto de esto.")]
        [Min(0.01f)]
        [SerializeField] private float _segundosTransicion = 6f;

        [Header("Volume")]
        [Tooltip("Volume global con el look corrupto (desaturado, oscuro, violáceo). Su peso pasa de 1 (todo corrupto) a 0 (todo sano).")]
        [SerializeField] private Volume _volumeCorrupto;

        [Header("Niebla")]
        [Tooltip("Controlar la niebla. Si la escena la tiene apagada, se prende mientras haya corrupción y se apaga al purificar todo.")]
        [SerializeField] private bool _controlarNiebla = true;
        [Tooltip("Color de la niebla con todo corrupto.")]
        [SerializeField] private Color _nieblaColorCorrupta = new(0.26f, 0.19f, 0.33f, 1f);
        [Tooltip("Densidad de la niebla con todo corrupto (modos exponenciales).")]
        [Min(0f)]
        [SerializeField] private float _nieblaDensidadCorrupta = 0.012f;
        [Tooltip("Distancia final de la niebla con todo corrupto (modo lineal).")]
        [Min(1f)]
        [SerializeField] private float _nieblaFinCorrupta = 60f;

        [Header("Luz")]
        [Tooltip("Luz direccional. Si se deja vacía, usa RenderSettings.sun o la primera direccional de la escena.")]
        [SerializeField] private Light _luz;
        [Tooltip("Multiplicador de la intensidad de la luz con todo corrupto.")]
        [Range(0f, 1f)]
        [SerializeField] private float _luzIntensidadCorrupta = 0.8f;
        [Tooltip("Color de la luz con todo corrupto.")]
        [SerializeField] private Color _luzColorCorrupta = new(0.72f, 0.66f, 0.88f, 1f);

        [Header("Cielo")]
        [Tooltip("Skybox con el look corrupto. Tiene que usar el mismo shader que el skybox de la escena (Lighting > Environment): se mezcla con Material.Lerp sobre una instancia. Vacío = no toca el cielo.")]
        [SerializeField] private Material _skyboxCorrupto;
        [Tooltip("Cómo se aclara el cielo con la purificación: 1 = parejo con el resto; más bajo = el cielo sigue oscuro hasta que queda poca corrupción (con 0.5, a 3 de 5 zonas purificadas todavía está 63 % corrupto).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _cieloRespuesta = 0.5f;
        [Tooltip("Controlar la luz ambiente. En modo Trilight/Color mezcla hacia los colores de abajo; en modo Skybox la recalcula desde el cielo mezclado.")]
        [SerializeField] private bool _controlarAmbiente = true;
        [Tooltip("Ambiente con todo corrupto (modo Trilight: cielo / ecuador / suelo; modo Color usa solo el primero).")]
        [SerializeField] private Color _ambienteCieloCorrupto = new(0.42f, 0.32f, 0.55f, 1f);
        [SerializeField] private Color _ambienteEcuadorCorrupto = new(0.46f, 0.3f, 0.46f, 1f);
        [SerializeField] private Color _ambienteSueloCorrupto = new(0.18f, 0.12f, 0.2f, 1f);

        // Estado sano (lo que tenía la escena).
        private bool _nieblaSanaActiva;
        private FogMode _nieblaSanaModo;
        private Color _nieblaSanaColor;
        private float _nieblaSanaDensidad, _nieblaSanaFin;
        private float _luzSanaIntensidad;
        private Color _luzSanaColor;
        private Material _skyboxSano, _skyboxInstancia;
        private Color _ambSanoCielo, _ambSanoEcuador, _ambSanoSuelo;
        private float _proximoAmbiente;
        private bool _capturado;

        private float _nivelActual = -1f;

        /// <summary>Nivel que se está mostrando ahora (0 = corrupto, 1 = sano), ya suavizado.</summary>
        public float NivelActual => Mathf.Max(0f, _nivelActual);

        // En Start y no en OnEnable: para entonces todas las regiones ya se registraron.
        private void Start()
        {
            Capturar();
            // Arranca directo en el nivel actual: entrar a la escena no anima nada.
            _nivelActual = NivelObjetivo();
            Aplicar(_nivelActual);
        }

        // Si se reactiva después de arrancar, retoma desde el nivel que estaba mostrando.
        private void OnEnable()
        {
            if(_capturado) Aplicar(NivelActual);
        }

        private void OnDisable()
        {
            Restaurar();
        }

        private void Update()
        {
            if(!_capturado) return;

            float objetivo = NivelObjetivo();
            if(Mathf.Approximately(_nivelActual, objetivo)) return;

            _nivelActual = Mathf.MoveTowards(_nivelActual, objetivo, Time.deltaTime / _segundosTransicion);
            Aplicar(_nivelActual);
        }

        private static float NivelObjetivo() => PurifyManager.Instance != null ? PurifyManager.Instance.NivelPurificacionGlobal : 1f;

        private void Capturar()
        {
            if(_capturado) return;
            _capturado = true;

            if(_luz == null) _luz = RenderSettings.sun;
            if(_luz == null)
                foreach(Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(l.type == LightType.Directional) { _luz = l; break; }

            _nieblaSanaActiva = RenderSettings.fog;
            _nieblaSanaModo = RenderSettings.fogMode;
            _nieblaSanaColor = RenderSettings.fogColor;
            _nieblaSanaDensidad = RenderSettings.fogDensity;
            _nieblaSanaFin = RenderSettings.fogEndDistance;

            if(_luz != null)
            {
                _luzSanaIntensidad = _luz.intensity;
                _luzSanaColor = _luz.color;
            }

            _ambSanoCielo = RenderSettings.ambientSkyColor;
            _ambSanoEcuador = RenderSettings.ambientEquatorColor;
            _ambSanoSuelo = RenderSettings.ambientGroundColor;

            _skyboxSano = RenderSettings.skybox;
            if(_skyboxCorrupto != null && _skyboxSano != null)
            {
                if(_skyboxSano.shader != _skyboxCorrupto.shader)
                    Debug.LogWarning($"[{name}] El skybox corrupto usa otro shader que el de la escena: la mezcla no va a funcionar bien.", this);
                // Instancia propia: nunca se toca el material del proyecto.
                _skyboxInstancia = new Material(_skyboxSano) { name = _skyboxSano.name + " (Purify)" };
            }
        }

        private void Aplicar(float nivel)
        {
            float corrupcion = 1f - nivel;

            if(_volumeCorrupto != null) _volumeCorrupto.weight = corrupcion;

            if(_controlarNiebla)
            {
                // Sin niebla en la escena, el estado sano es "sin niebla": densidad 0 / distancia muy lejos.
                float densidadSana = _nieblaSanaActiva ? _nieblaSanaDensidad : 0f;
                Color colorSano = _nieblaSanaActiva ? _nieblaSanaColor : _nieblaColorCorrupta;

                RenderSettings.fog = _nieblaSanaActiva || corrupcion > 0.001f;
                RenderSettings.fogColor = Color.Lerp(colorSano, _nieblaColorCorrupta, corrupcion);

                if(_nieblaSanaModo == FogMode.Linear && _nieblaSanaActiva)
                    RenderSettings.fogEndDistance = Mathf.Lerp(_nieblaSanaFin, _nieblaFinCorrupta, corrupcion);
                else
                    RenderSettings.fogDensity = Mathf.Lerp(densidadSana, _nieblaDensidadCorrupta, corrupcion);
            }

            if(_luz != null)
            {
                _luz.intensity = Mathf.Lerp(_luzSanaIntensidad, _luzSanaIntensidad * _luzIntensidadCorrupta, corrupcion);
                _luz.color = Color.Lerp(_luzSanaColor, _luzColorCorrupta, corrupcion);
            }

            if(_skyboxInstancia != null)
            {
                _skyboxInstancia.Lerp(_skyboxSano, _skyboxCorrupto, Mathf.Pow(corrupcion, _cieloRespuesta));
                if(RenderSettings.skybox != _skyboxInstancia) RenderSettings.skybox = _skyboxInstancia;
            }

            if(_controlarAmbiente)
            {
                if(RenderSettings.ambientMode == AmbientMode.Skybox)
                {
                    // Recalcular el ambiente desde el cielo es caro: como mucho 5 veces por segundo durante la transición.
                    if(_skyboxInstancia != null && Time.unscaledTime >= _proximoAmbiente)
                    {
                        _proximoAmbiente = Time.unscaledTime + 0.2f;
                        DynamicGI.UpdateEnvironment();
                    }
                }
                else
                {
                    RenderSettings.ambientSkyColor = Color.Lerp(_ambSanoCielo, _ambienteCieloCorrupto, corrupcion);
                    RenderSettings.ambientEquatorColor = Color.Lerp(_ambSanoEcuador, _ambienteEcuadorCorrupto, corrupcion);
                    RenderSettings.ambientGroundColor = Color.Lerp(_ambSanoSuelo, _ambienteSueloCorrupto, corrupcion);
                }
            }
        }

        private void Restaurar()
        {
            if(!_capturado) return;

            if(_volumeCorrupto != null) _volumeCorrupto.weight = 1f;

            if(_controlarNiebla)
            {
                RenderSettings.fog = _nieblaSanaActiva;
                RenderSettings.fogMode = _nieblaSanaModo;
                RenderSettings.fogColor = _nieblaSanaColor;
                RenderSettings.fogDensity = _nieblaSanaDensidad;
                RenderSettings.fogEndDistance = _nieblaSanaFin;
            }

            if(_luz != null)
            {
                _luz.intensity = _luzSanaIntensidad;
                _luz.color = _luzSanaColor;
            }

            if(_skyboxInstancia != null)
            {
                RenderSettings.skybox = _skyboxSano;
                if(_controlarAmbiente && RenderSettings.ambientMode == AmbientMode.Skybox) DynamicGI.UpdateEnvironment();
            }

            if(_controlarAmbiente)
            {
                RenderSettings.ambientSkyColor = _ambSanoCielo;
                RenderSettings.ambientEquatorColor = _ambSanoEcuador;
                RenderSettings.ambientGroundColor = _ambSanoSuelo;
            }
        }

        private void OnDestroy()
        {
            if(_skyboxInstancia != null) Destroy(_skyboxInstancia);
        }
    }
}
