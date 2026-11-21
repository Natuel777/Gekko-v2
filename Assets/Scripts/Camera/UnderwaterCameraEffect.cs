using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Efecto de cámara bajo el agua: cuando la cámara queda debajo de la superficie (GekkoWater.TryGetSurface) entra de a poco un
// Volume global de URP (tinte azul verdoso, menos saturación, viñeta) y niebla exponencial del color del agua. Al salir se
// va de a poco y la niebla vuelve exactamente a como estaba en la escena.
//
// Va en la cámara que renderiza (Main Camera). Player lo agrega solo si no está (EnsureOn), con estos valores por defecto;
// si se quiere ajustar, se agrega a mano a la Main Camera de la escena y se tocan desde el inspector. El Volume y su perfil
// se arman en código: no hay asset que mantener.
[DisallowMultipleComponent]
public class UnderwaterCameraEffect : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color _tint = new Color(0.55f, 0.85f, 0.9f);
    [SerializeField, Range(-100f, 0f)] private float _saturation = -20f;
    [SerializeField, Range(0f, 1f)] private float _vignette = 0.35f;
    [SerializeField] private Color _vignetteColor = new Color(0.02f, 0.12f, 0.18f);

    [Header("Niebla")]
    [SerializeField] private Color _fogColor = new Color(0.12f, 0.38f, 0.45f);
    [SerializeField, Min(0f)] private float _fogDensity = 0.08f;

    [Header("Transición")]
    [Tooltip("Velocidad de entrada y salida del efecto (1 / segundos).")]
    [SerializeField, Min(0.1f)] private float _fadeSpeed = 8f;

    private Camera _camera;
    private Volume _volume;
    private float _weight;

    // Niebla original de la escena, para devolverla tal cual.
    private bool _fogSaved;
    private bool _origFog;
    private FogMode _origFogMode;
    private Color _origFogColor;
    private float _origFogDensity;

    public static void EnsureOn(Camera camera)
    {
        if(camera != null && !camera.TryGetComponent(out UnderwaterCameraEffect _))
            camera.gameObject.AddComponent<UnderwaterCameraEffect>();
    }

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        var go = new GameObject("Underwater Volume (runtime)");
        go.transform.SetParent(transform, false);
        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f;
        _volume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Underwater (runtime)";

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.colorFilter.Override(_tint);
        color.saturation.Override(_saturation);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(_vignette);
        vignette.color.Override(_vignetteColor);
        vignette.smoothness.Override(0.6f);

        _volume.sharedProfile = profile;
    }

    private void LateUpdate()
    {
        bool under = false;
        if(_camera != null)
        {
            Vector3 p = _camera.transform.position;
            // La cámara cuenta como bajo el agua solo si está debajo de la superficie (reachAbove 0).
            under = GekkoWater.TryGetSurface(p, 0f, 20f, out GekkoWater.WaterInfo water) && p.y < water.SurfaceY - 0.02f;
        }

        // El volumen de los ríos (SplineRiver > Volumen) solo se dibuja con la cámara bajo el agua.
        SplineRiver.SetVolumeVisible(under);

        // unscaledDeltaTime: en la pausa (timeScale 0) se mantiene como estaba, sin trabarse a mitad de camino al despausar.
        _weight = Mathf.MoveTowards(_weight, under ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
        _volume.weight = _weight;
        ApplyFog(_weight);
    }

    private void ApplyFog(float w)
    {
        if(w <= 0f)
        {
            RestoreFog();
            return;
        }

        if(!_fogSaved)
        {
            _origFog = RenderSettings.fog;
            _origFogMode = RenderSettings.fogMode;
            _origFogColor = RenderSettings.fogColor;
            _origFogDensity = RenderSettings.fogDensity;
            _fogSaved = true;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(_origFog ? _origFogColor : _fogColor, _fogColor, w);
        RenderSettings.fogDensity = Mathf.Lerp(_origFog ? _origFogDensity : 0f, _fogDensity, w);
    }

    private void RestoreFog()
    {
        if(!_fogSaved) return;

        RenderSettings.fog = _origFog;
        RenderSettings.fogMode = _origFogMode;
        RenderSettings.fogColor = _origFogColor;
        RenderSettings.fogDensity = _origFogDensity;
        _fogSaved = false;
    }

    private void OnDisable()
    {
        SplineRiver.SetVolumeVisible(false);
        RestoreFog();
        _weight = 0f;
        if(_volume != null) _volume.weight = 0f;
    }

    private void OnDestroy()
    {
        if(_volume != null && _volume.sharedProfile != null) Destroy(_volume.sharedProfile);
    }
}
