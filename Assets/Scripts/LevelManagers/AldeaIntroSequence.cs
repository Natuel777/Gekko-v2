using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Gekko.Purification;

// Intro de la escena Aldea. La aldea no es jugable: es una vitrina que se va purificando de a poco. Cada nivel
// tiene su zona fija: al ganar el nivel 1 se purifica la zona 1, al ganar el nivel 2 la zona 2 (y así).
//  - El nivel ganado sale de LevelStats.Instance.LevelNumber (el LevelStats del nivel sigue vivo al llegar acá).
//  - La primera vez que se gana un nivel, su zona arranca corrupta y se purifica con la onda real (PurifyRegion).
//    Si ese nivel ya se había ganado, la zona ya está sana y la cámara solo muestra la casa de su gekko rescatado.
//  - Las zonas de OTROS niveles ya ganados quedan sanas desde el primer frame (sin animación).
//  - La secuencia muestra la aldea entera, la cámara viaja hasta la zona, se purifica, se guarda el progreso y cierra
//    con la pantalla de victoria (EventManager "OnLevelFinished", lo mismo que dispara FinishLevelTrigger).
//  - Casas: mismo criterio, el gekko de la casa del nivel ganado aparece; los de otros niveles ya ganados están ahí.
// El progreso (hasta qué nivel se ganó) se guarda en PlayerPrefs (PlayerPrefsKeys.aldeaZonasPurificadasKey).
public class AldeaIntroSequence : MonoBehaviour
{
    [Serializable]
    public class Zona
    {
        public string nombre = "Zona";
        [Tooltip("Región que se purifica cuando le toca a esta zona.")]
        public PurifyRegion region;
        [Tooltip("De dónde se ve esta zona mientras se purifica (posición y rotación).")]
        public Transform plano;
    }

    [Serializable]
    public class Casa
    {
        public string nombre = "Casa";
        [Tooltip("Gekko que vive en esta casa. Arranca escondido y aparece en la visita que le toca.")]
        public AldeaHabitante gekko;
        [Tooltip("Opcional: de dónde se ve la casa cuando aparece el gekko. Vacío = la cámara se queda donde está.")]
        public Transform plano;
    }

    [Header("Zonas (en orden: la primera se purifica en la primera visita)")]
    [SerializeField] private Zona[] _zonas = Array.Empty<Zona>();

    [Header("Casas (en orden: el primer gekko aparece en la primera visita, uno por nivel completado)")]
    [SerializeField] private Casa[] _casas = Array.Empty<Casa>();
    [SerializeField] private float _moveToCasaDuration = 1.5f;
    [SerializeField] private float _holdAfterGekko = 1.5f;

    [Header("Pruebas")]
    [Tooltip("-1 = usar el progreso guardado. 0..N = arrancar como si ya se hubieran completado esos niveles (no se guarda).")]
    [SerializeField] private int _forzarZonasPurificadas = -1;
    [Tooltip("Nivel que se toma como ganado si se abre la Aldea directo (sin venir de un nivel). 0 = no purifica nada.")]
    [Min(0)]
    [SerializeField] private int _nivelSiSeAbreDirecto = 1;

    [Header("Cámara")]
    [Tooltip("Cámara propia de esta cinemática, registrada en CameraStateManager.")]
    [SerializeField] private CinemachineCamera _introCamera;
    [SerializeField] private string _introCameraTag = "AldeaIntro";
    [Tooltip("De dónde se ve toda la aldea.")]
    [SerializeField] private Transform _wideShotPoint;

    [Header("Tiempos")]
    [SerializeField] private float _wideShotDuration = 3f;
    [Tooltip("Cuánto se acerca la cámara durante el plano general (metros), para que no quede quieta.")]
    [SerializeField] private float _wideShotDrift = 3f;
    [SerializeField] private float _moveToZoneDuration = 2.5f;
    [Tooltip("Cuánto se acerca la cámara a la zona mientras pasa la onda (metros).")]
    [SerializeField] private float _purifyPushIn = 2.5f;
    [SerializeField] private float _holdAfterPurify = 2f;

    // Niveles ganados antes de esta visita (zonas/casas 0.._yaPurificadas-1 ya sanas) y el índice de la zona/casa del
    // nivel que se acaba de ganar (-1 = ninguno).
    private int _yaPurificadas;
    private int _actual = -1;
    private bool _forzado;
    // El nivel ganado ya se había ganado antes: sin purificación, la cámara va directo a la casa de su gekko.
    private bool _repeticion;

    private Transform PlanoInicial
    {
        get
        {
            if(_repeticion)
            {
                if(_actual < _casas.Length && _casas[_actual].plano != null) return _casas[_actual].plano;
                if(_actual < _zonas.Length && _zonas[_actual].plano != null) return _zonas[_actual].plano;
            }
            return _wideShotPoint;
        }
    }

    private int TotalVisitas => Mathf.Max(_zonas.Length, _casas.Length);

    public static int ZonasPurificadasGuardadas => PlayerPrefs.GetInt(PlayerPrefsKeys.aldeaZonasPurificadasKey, 0);

    public static void ResetearProgreso()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKeys.aldeaZonasPurificadasKey);
        PlayerPrefs.Save();
    }

    // En Awake, antes que los Start de PurifyReactive/PurifyTrigger: así las plantas, partículas y luces de las
    // zonas ya purificadas arrancan directo en su estado sano.
    private void Awake()
    {
        _forzado = _forzarZonasPurificadas >= 0;
        _yaPurificadas = Mathf.Clamp(_forzado ? _forzarZonasPurificadas : ZonasPurificadasGuardadas, 0, TotalVisitas);

        int nivel = LevelStats.Instance != null ? LevelStats.Instance.LevelNumber : 0;
        if(nivel <= 0) nivel = _nivelSiSeAbreDirecto;
        _actual = nivel >= 1 && nivel <= TotalVisitas ? nivel - 1 : -1;
        _repeticion = _actual >= 0 && _actual < _yaPurificadas;

        // Primera vez que se gana el nivel: su zona queda corrupta para purificarla en la secuencia. Si es una
        // repetición, ya está sana desde el primer frame (no se repite la purificación). Las de otros niveles ganados, sanas.
        for(int i = 0; i < Mathf.Min(_yaPurificadas, _zonas.Length); i++)
            if((i != _actual || _repeticion) && _zonas[i].region != null) _zonas[i].region.SetPurifiedInstant();

        for(int i = 0; i < _casas.Length; i++)
        {
            if(_casas[i].gekko == null) continue;
            if(i < _yaPurificadas && (i != _actual || _repeticion)) _casas[i].gekko.MostrarInstantaneo();
            else _casas[i].gekko.Esconder();
        }

        ActivarCamaraIntroDeEntrada();
    }

    // La cámara de la intro tiene que ser la única activa desde el primer frame: si la de Gekko llega a estar activa
    // aunque sea un frame, el CinemachineBrain hace un blend de una a la otra al cambiar.
    private void ActivarCamaraIntroDeEntrada()
    {
        Transform plano = PlanoInicial;
        if(_introCamera == null || plano == null) return;

        _introCamera.transform.SetPositionAndRotation(plano.position, plano.rotation);
        foreach(CinemachineCamera cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            cam.enabled = cam == _introCamera;
    }

    private void Start()
    {
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if(GameManager.Instance != null && GameManager.Instance.Pj != null)
            GameManager.Instance.Pj.Inputs(false);

        Transform planoInicial = PlanoInicial;
        bool hayCamara = _introCamera != null && planoInicial != null;
        if(hayCamara)
        {
            _introCamera.transform.SetPositionAndRotation(planoInicial.position, planoInicial.rotation);
            EventManager.Trigger(_introCameraTag);
        }

        // Repetición de un nivel ya ganado: solo la casa del gekko rescatado (ya está ahí), con un acercamiento lento.
        if(_repeticion)
        {
            if(hayCamara) yield return Acercar(planoInicial.position, planoInicial.rotation, _wideShotDrift, _wideShotDuration);
            else yield return new WaitForSeconds(_wideShotDuration);

            yield return new WaitForSeconds(_holdAfterPurify);
            FinishSequence();
            yield break;
        }

        // Plano general con un acercamiento lento (sin quedar quieta).
        if(hayCamara) yield return Acercar(_wideShotPoint.position, _wideShotPoint.rotation, _wideShotDrift, _wideShotDuration);
        else yield return new WaitForSeconds(_wideShotDuration);

        if(_actual >= 0 && _actual < _zonas.Length)
        {
            Zona zona = _zonas[_actual];

            if(hayCamara && zona.plano != null)
                yield return MoveCamera(_introCamera.transform.position, _introCamera.transform.rotation, zona.plano, _moveToZoneDuration);

            if(zona.region != null)
            {
                zona.region.Purify();
                Vector3 desde = _introCamera != null ? _introCamera.transform.position : Vector3.zero;
                Quaternion rot = _introCamera != null ? _introCamera.transform.rotation : Quaternion.identity;
                float t = 0f;
                while(!zona.region.Purificada)
                {
                    t += Time.deltaTime;
                    if(hayCamara)
                    {
                        float k = 1f - Mathf.Exp(-t * 0.35f);
                        _introCamera.transform.position = desde + rot * Vector3.forward * (_purifyPushIn * k);
                    }
                    yield return null;
                }
            }

            Debug.Log($"[Aldea] Zona {_actual + 1}/{_zonas.Length} purificada ({zona.nombre}).");
        }

        if(_actual >= 0 && _actual < _casas.Length && _casas[_actual].gekko != null)
        {
            Casa casa = _casas[_actual];

            if(hayCamara && casa.plano != null)
                yield return MoveCamera(_introCamera.transform.position, _introCamera.transform.rotation, casa.plano, _moveToCasaDuration);

            yield return casa.gekko.Aparecer();
            yield return new WaitForSeconds(_holdAfterGekko);

            Debug.Log($"[Aldea] Gekko {_actual + 1}/{_casas.Length} llegó a su casa ({casa.nombre}).");
        }

        // Guarda hasta qué nivel se ganó (nunca baja: rejugar el nivel 1 no "despurifica" la zona del 2).
        if(!_forzado && _actual >= 0 && _actual + 1 > _yaPurificadas)
        {
            PlayerPrefs.SetInt(PlayerPrefsKeys.aldeaZonasPurificadasKey, _actual + 1);
            PlayerPrefs.Save();
        }

        yield return new WaitForSeconds(_holdAfterPurify);

        FinishSequence();
    }

    // Avanza la cámara hacia adelante con ease-out, para que el plano general no se sienta congelado.
    private IEnumerator Acercar(Vector3 desde, Quaternion rot, float metros, float duration)
    {
        float t = 0f;
        while(t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
            _introCamera.transform.SetPositionAndRotation(desde + rot * Vector3.forward * (metros * Mathf.Sin(t * Mathf.PI * 0.5f)), rot);
            yield return null;
        }
    }

    private IEnumerator MoveCamera(Vector3 fromPos, Quaternion fromRot, Transform to, float duration)
    {
        float t = 0f;
        while(t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / Mathf.Max(0.01f, duration));
            float k = Mathf.SmoothStep(0f, 1f, t);
            _introCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, to.position, k),
                Quaternion.Slerp(fromRot, to.rotation, k));
            yield return null;
        }
    }

    private void FinishSequence()
    {
        if(ScreenFader.Instance == null)
        {
            EventManager.Trigger("OnLevelFinished");
            return;
        }

        ScreenFader.Instance.OnFadeMiddle += OnScreenBlack;
        ScreenFader.Instance.StartFade();
    }

    private void OnScreenBlack()
    {
        if(ScreenFader.Instance != null) ScreenFader.Instance.OnFadeMiddle -= OnScreenBlack;
        EventManager.Trigger("OnLevelFinished");
    }

    [ContextMenu("Resetear progreso de la Aldea")]
    private void ResetearProgresoMenu()
    {
        ResetearProgreso();
        Debug.Log("[Aldea] Progreso reseteado: la próxima visita arranca con las zonas corruptas y las casas vacías.");
    }

    [ContextMenu("Sumar una zona purificada")]
    private void SumarZonaMenu()
    {
        PlayerPrefs.SetInt(PlayerPrefsKeys.aldeaZonasPurificadasKey, Mathf.Min(TotalVisitas, ZonasPurificadasGuardadas + 1));
        PlayerPrefs.Save();
        Debug.Log($"[Aldea] Zonas purificadas guardadas: {ZonasPurificadasGuardadas}.");
    }
}
