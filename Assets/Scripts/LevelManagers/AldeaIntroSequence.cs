using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Gekko.Purification;

// Intro de la escena Aldea. La aldea no es jugable: es una vitrina que se va purificando de a poco, una zona cada
// vez que el jugador supera un nivel y vuelve acá.
//  - Las zonas ya purificadas en visitas anteriores quedan sanas desde el primer frame (sin animación).
//  - La secuencia muestra la aldea entera, la cámara viaja hasta la zona que toca, se purifica con la onda real
//    (PurifyRegion), se guarda el progreso y cierra con la pantalla de victoria (EventManager "OnLevelFinished", lo
//    mismo que dispara FinishLevelTrigger en los niveles normales).
//  - Con las 5 zonas purificadas solo muestra la aldea sana y cierra.
// El progreso se guarda en PlayerPrefs (PlayerPrefsKeys.aldeaZonasPurificadasKey).
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

    [Header("Zonas (en orden: la primera se purifica en la primera visita)")]
    [SerializeField] private Zona[] _zonas = Array.Empty<Zona>();

    [Header("Pruebas")]
    [Tooltip("-1 = usar el progreso guardado. 0..5 = arrancar como si ya hubiera esas zonas purificadas (no se guarda).")]
    [SerializeField] private int _forzarZonasPurificadas = -1;

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

    private int _yaPurificadas;
    private bool _forzado;

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
        _yaPurificadas = Mathf.Clamp(_forzado ? _forzarZonasPurificadas : ZonasPurificadasGuardadas, 0, _zonas.Length);

        for(int i = 0; i < _yaPurificadas; i++)
            if(_zonas[i].region != null) _zonas[i].region.SetPurifiedInstant();

        ActivarCamaraIntroDeEntrada();
    }

    // La cámara de la intro tiene que ser la única activa desde el primer frame: si la de Gekko llega a estar activa
    // aunque sea un frame, el CinemachineBrain hace un blend de una a la otra al cambiar.
    private void ActivarCamaraIntroDeEntrada()
    {
        if(_introCamera == null || _wideShotPoint == null) return;

        _introCamera.transform.SetPositionAndRotation(_wideShotPoint.position, _wideShotPoint.rotation);
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

        bool hayCamara = _introCamera != null && _wideShotPoint != null;
        if(hayCamara)
        {
            _introCamera.transform.SetPositionAndRotation(_wideShotPoint.position, _wideShotPoint.rotation);
            EventManager.Trigger(_introCameraTag);
        }

        // Plano general con un acercamiento lento (sin quedar quieta).
        if(hayCamara) yield return Acercar(_wideShotPoint.position, _wideShotPoint.rotation, _wideShotDrift, _wideShotDuration);
        else yield return new WaitForSeconds(_wideShotDuration);

        if(_yaPurificadas < _zonas.Length)
        {
            Zona zona = _zonas[_yaPurificadas];

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

            if(!_forzado)
            {
                PlayerPrefs.SetInt(PlayerPrefsKeys.aldeaZonasPurificadasKey, _yaPurificadas + 1);
                PlayerPrefs.Save();
            }

            Debug.Log($"[Aldea] Zona {_yaPurificadas + 1}/{_zonas.Length} purificada ({zona.nombre}).");
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
        Debug.Log("[Aldea] Progreso reseteado: la próxima visita arranca con las 5 zonas corruptas.");
    }

    [ContextMenu("Sumar una zona purificada")]
    private void SumarZonaMenu()
    {
        PlayerPrefs.SetInt(PlayerPrefsKeys.aldeaZonasPurificadasKey, Mathf.Min(_zonas.Length, ZonasPurificadasGuardadas + 1));
        PlayerPrefs.Save();
        Debug.Log($"[Aldea] Zonas purificadas guardadas: {ZonasPurificadasGuardadas}.");
    }
}
