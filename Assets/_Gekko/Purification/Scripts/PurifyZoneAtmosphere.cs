using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gekko.Purification
{
    /// <summary>
    /// Atmósfera corrupta SOLO mientras el jugador está dentro de una región corrupta (para niveles con zonas sanas,
    /// como LvlOne; en BossLvlOne todo es corrupto y se usa <see cref="PurifyAtmosphere"/>, que depende de cuántas
    /// regiones hay purificadas). Al entrar, el skybox pasa suave del sano al corrupto, el Volume corrupto sube su
    /// peso y aparecen luces y partículas; al salir, o cuando la onda de esa región alcanza al jugador (se rompió su
    /// núcleo), vuelve suave a sano. Las regiones purificadas ya no lo activan.
    ///
    /// El color y el brillo del look corrupto se ajustan acá (sección "Color de la zona"), también en Play: se
    /// aplican sobre una copia del perfil del Volume, así que el asset (compartido con BossLvlOne) no se toca.
    ///
    /// El skybox se mezcla con <see cref="Material.Lerp"/> sobre una instancia del skybox de la escena, así que el
    /// skybox corrupto tiene que usar EL MISMO shader que el sano (lo más fácil: duplicar el material y cambiarle
    /// colores). Al apagarse deja el skybox original y el Volume en peso 0.
    /// </summary>
    public class PurifyZoneAtmosphere : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Transición")]
        [Tooltip("Segundos que tarda en pasar de sano a corrupto (o al revés) al entrar o salir de una región.")]
        [Min(0.01f)]
        [SerializeField] private float _segundosTransicion = 2.5f;
        [Tooltip("Cuánto se acerca al look corrupto dentro de una región (1 = corrupto del todo).")]
        [Range(0f, 1f)]
        [SerializeField] private float _intensidadMaxima = 1f;

        [Header("Color de la zona")]
        [Tooltip("Color de la corrupción: tiñe la pantalla, el borde de la viñeta, las esporas y las luces corruptas.")]
        [SerializeField] private Color _colorCorrupto = new(0.72f, 0.48f, 1f, 1f);
        [Tooltip("Cuánto tiñe la pantalla el color corrupto (0 = nada, 1 = filtro completo).")]
        [Range(0f, 1f)]
        [SerializeField] private float _fuerzaTinte = 0.3f;
        [Tooltip("Brillo de la pantalla dentro de la zona (Post Exposure). Positivo = más claro.")]
        [Range(-2f, 2f)]
        [SerializeField] private float _brillo = 0.25f;
        [Tooltip("Contraste dentro de la zona. Más alto = sombras más oscuras.")]
        [Range(-50f, 50f)]
        [SerializeField] private float _contraste = 8f;
        [Tooltip("Saturación dentro de la zona. Negativo = más gris.")]
        [Range(-100f, 50f)]
        [SerializeField] private float _saturacion = -12f;
        [Tooltip("Oscurecido de los bordes de la pantalla (viñeta).")]
        [Range(0f, 1f)]
        [SerializeField] private float _vineta = 0.18f;

        [Header("Skybox")]
        [Tooltip("Skybox con el look corrupto. Tiene que usar el mismo shader que el skybox de la escena (Lighting > Environment). Sus colores se editan en el material. Vacío = no toca el cielo.")]
        [SerializeField] private Material _skyboxCorrupto;
        [Tooltip("Recalcular la luz ambiente del skybox mientras cambia (la escena usa Ambient Source = Skybox). Si Gekko se ve muy oscuro en la zona, aclarar el skybox corrupto o apagar esto.")]
        [SerializeField] private bool _actualizarAmbiente = true;
        [Tooltip("Segundos entre recálculos de la luz ambiente durante la transición.")]
        [Min(0.05f)]
        [SerializeField] private float _intervaloAmbiente = 0.2f;

        [Header("Volume")]
        [Tooltip("Volume global con el look corrupto. Su peso va de 0 (afuera) a 1 (dentro de una región corrupta). Vacío = no toca post-proceso.")]
        [SerializeField] private Volume _volumeCorrupto;

        [Header("Luces y partículas")]
        [Tooltip("Luces que se prenden con la corrupción: su intensidad va de 0 a la que tienen en la escena. Toman el color corrupto.")]
        [SerializeField] private Light[] _lucesCorruptas = new Light[0];
        [Tooltip("Partículas que aparecen con la corrupción y se desvanecen al salir. Toman el color corrupto.")]
        [SerializeField] private ParticleSystem[] _particulasCorruptas = new ParticleSystem[0];
        [Tooltip("Cuánto brillan las partículas (más de 1 = brillan con el Bloom).")]
        [Min(0f)]
        [SerializeField] private float _brilloParticulas = 7f;
        [Tooltip("Objeto que acompaña al jugador (ej. esporas y luz alrededor de Gekko). Se mueve a su posición cada frame. Vacío = no sigue a nadie.")]
        [SerializeField] private Transform _seguirAlJugador;

        private float[] _lucesIntensidadBase;
        private float[] _particulasEmisionBase;
        private Material[] _particulasMaterial;

        private ColorAdjustments _ajustes;
        private Vignette _vignette;

        private Material _skyboxSano;
        private Material _instancia;
        private float _corrupcionActual;
        private float _proximoAmbiente;

        /// <summary>Cuánto del look corrupto se está mostrando ahora (0 = sano, 1 = corrupto), ya suavizado.</summary>
        public float CorrupcionActual => _corrupcionActual;

        // Lo que tienen en la escena es el valor "corrupto del todo"; se capturan una sola vez.
        private void Awake()
        {
            _lucesIntensidadBase = new float[_lucesCorruptas.Length];
            for(int i = 0; i < _lucesCorruptas.Length; i++)
                if(_lucesCorruptas[i] != null) _lucesIntensidadBase[i] = _lucesCorruptas[i].intensity;

            // Instancia de material por sistema para poder desvanecer también las partículas que ya están en el aire.
            _particulasEmisionBase = new float[_particulasCorruptas.Length];
            _particulasMaterial = new Material[_particulasCorruptas.Length];
            for(int i = 0; i < _particulasCorruptas.Length; i++)
            {
                ParticleSystem ps = _particulasCorruptas[i];
                if(ps == null) continue;

                _particulasEmisionBase[i] = ps.emission.rateOverTimeMultiplier;
                if(ps.TryGetComponent(out ParticleSystemRenderer rend)) _particulasMaterial[i] = rend.material;

                // Blancas: el color lo pone el material con el color corrupto, para controlarlo desde un solo lugar.
                ParticleSystem.MainModule main = ps.main;
                main.startColor = Color.white;
            }

            // .profile crea una copia en runtime: los ajustes de color no pisan el asset compartido.
            if(_volumeCorrupto != null && _volumeCorrupto.sharedProfile != null)
            {
                VolumeProfile perfil = _volumeCorrupto.profile;
                if(!perfil.TryGet(out _ajustes)) _ajustes = perfil.Add<ColorAdjustments>(true);
                if(!perfil.TryGet(out _vignette)) _vignette = perfil.Add<Vignette>(true);
            }
        }

        private void OnEnable()
        {
            _skyboxSano = RenderSettings.skybox;

            if(_skyboxCorrupto != null && _skyboxSano != null)
            {
                if(_skyboxSano.shader != _skyboxCorrupto.shader)
                    Debug.LogWarning($"[{name}] El skybox corrupto usa otro shader que el de la escena: la mezcla no va a funcionar bien.", this);

                _instancia = new Material(_skyboxSano) { name = _skyboxSano.name + " (Purify)" };
                RenderSettings.skybox = _instancia;
            }

            // Arranca directo en el estado actual: entrar a la escena dentro de una región no anima nada.
            _corrupcionActual = CorrupcionObjetivo();
            Aplicar(true);
        }

        private void OnDisable()
        {
            if(_volumeCorrupto != null) _volumeCorrupto.weight = 0f;
            AplicarLucesYParticulas(0f);

            if(_instancia == null) return;

            RenderSettings.skybox = _skyboxSano;
            if(_actualizarAmbiente) DynamicGI.UpdateEnvironment();

            Destroy(_instancia);
            _instancia = null;
        }

        private void OnDestroy()
        {
            if(_particulasMaterial == null) return;

            foreach(Material mat in _particulasMaterial)
                if(mat != null) Destroy(mat);
        }

        private void Update()
        {
            if(_seguirAlJugador != null && GameManager.Instance != null && GameManager.Instance.Pj != null)
                _seguirAlJugador.position = GameManager.Instance.Pj.transform.position;

            // Todos los frames (es barato) para que los cambios de color en el Inspector se vean en Play.
            AplicarColor();

            float objetivo = CorrupcionObjetivo();
            if(Mathf.Approximately(_corrupcionActual, objetivo)) return;

            _corrupcionActual = Mathf.MoveTowards(_corrupcionActual, objetivo, Time.deltaTime / _segundosTransicion);
            Aplicar(Mathf.Approximately(_corrupcionActual, objetivo));
        }

        // Intensidad máxima si el jugador está en una región a la que todavía no le llegó la onda; 0 en zona sana o
        // ya purificada.
        private float CorrupcionObjetivo()
        {
            if(GameManager.Instance == null || GameManager.Instance.Pj == null) return 0f;

            Vector3 pos = GameManager.Instance.Pj.transform.position;
            PurifyRegion region = PurifyRegion.Buscar(pos);

            if(region == null || region.OndaAlcanzo(pos)) return 0f;
            return _intensidadMaxima;
        }

        // Suavizado en los extremos para que la entrada y la salida no se noten lineales.
        private float Peso => Mathf.SmoothStep(0f, 1f, _corrupcionActual);

        private void Aplicar(bool forzarAmbiente)
        {
            float t = Peso;

            if(_volumeCorrupto != null) _volumeCorrupto.weight = t;
            AplicarColor();
            AplicarLucesYParticulas(t);

            if(_instancia == null) return;

            _instancia.Lerp(_skyboxSano, _skyboxCorrupto, t);

            if(!_actualizarAmbiente) return;
            if(!forzarAmbiente && Time.unscaledTime < _proximoAmbiente) return;

            _proximoAmbiente = Time.unscaledTime + _intervaloAmbiente;
            DynamicGI.UpdateEnvironment();
        }

        // Valores "corrupto del todo"; el peso del Volume los mezcla con la escena.
        private void AplicarColor()
        {
            if(_ajustes != null)
            {
                _ajustes.postExposure.Override(_brillo);
                _ajustes.contrast.Override(_contraste);
                _ajustes.saturation.Override(_saturacion);
                _ajustes.colorFilter.Override(Color.Lerp(Color.white, _colorCorrupto, _fuerzaTinte));
            }

            if(_vignette != null)
            {
                _vignette.intensity.Override(_vineta);
                _vignette.color.Override(_colorCorrupto * 0.6f);
            }

            float t = Peso;

            for(int i = 0; i < _lucesCorruptas.Length; i++)
                if(_lucesCorruptas[i] != null) _lucesCorruptas[i].color = _colorCorrupto;

            if(_particulasMaterial == null) return;

            // El alpha baja con el peso: las esporas que ya estaban en el aire se desvanecen junto con la zona.
            Color colorParticulas = _colorCorrupto * _brilloParticulas;
            colorParticulas.a = t;

            foreach(Material mat in _particulasMaterial)
                if(mat != null) mat.SetColor(BaseColorId, colorParticulas);
        }

        private void AplicarLucesYParticulas(float t)
        {
            if(_lucesIntensidadBase == null) return;

            bool activo = t > 0.001f;

            for(int i = 0; i < _lucesCorruptas.Length; i++)
            {
                Light luz = _lucesCorruptas[i];
                if(luz == null) continue;

                luz.intensity = _lucesIntensidadBase[i] * t;
                luz.enabled = activo;
            }

            for(int i = 0; i < _particulasCorruptas.Length; i++)
            {
                ParticleSystem ps = _particulasCorruptas[i];
                if(ps == null) continue;

                ParticleSystem.EmissionModule emision = ps.emission;
                emision.rateOverTimeMultiplier = _particulasEmisionBase[i] * t;

                // Ya invisibles (alpha 0): se borran para que no queden flotando fuera de la zona.
                if(activo && !ps.isEmitting) ps.Play(true);
                else if(!activo && (ps.isEmitting || ps.particleCount > 0)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
