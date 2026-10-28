using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class GraphicsSettingsPanel : MonoBehaviour
{
    [SerializeField] private ArrowValueSelector _resolutionSelector;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Toggle _shakeToggle;

    // Slider pensado en rango 0-100 (como el resto de la UI de Opciones); 50 = exposición neutra.
    [SerializeField] private Slider _brightnessSlider;
    [SerializeField, Min(0f)] private float _maxExposureEV = 2f;

    private const float BrightnessSliderDefault = 50f;

    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();

    // Aplica la resolucion guardada al iniciar el juego (solo en builds; en el editor la Game View manda).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedOnStart()
    {
        if (Application.isEditor) return;
        if (!PlayerPrefs.HasKey(PlayerPrefsKeys.resolutionWidthKey) || !PlayerPrefs.HasKey(PlayerPrefsKeys.resolutionHeightKey)) return;

        int w = PlayerPrefs.GetInt(PlayerPrefsKeys.resolutionWidthKey);
        int h = PlayerPrefs.GetInt(PlayerPrefsKeys.resolutionHeightKey);
        bool fullscreen = PlayerPrefs.GetInt(PlayerPrefsKeys.fullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        Screen.SetResolution(w, h, fullscreen);
    }

    private void OnEnable()
    {
        InitialiceValues();
    }

    private void InitialiceValues()
    {
        if (_resolutionSelector)
        {
            BuildResolutionList();
            var labels = new List<string>();
            foreach (var r in _resolutions) labels.Add(r.x + " x " + r.y);

            _resolutionSelector.SetOptions(labels, FindStartIndex());
            _resolutionSelector.OnIndexChanged.RemoveListener(SetResolution);
            _resolutionSelector.OnIndexChanged.AddListener(SetResolution);
        }

        if (_fullscreenToggle)
        {
            bool savedFullscreen = PlayerPrefs.GetInt(PlayerPrefsKeys.fullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            _fullscreenToggle.SetIsOnWithoutNotify(savedFullscreen);
            Screen.fullScreen = savedFullscreen;

            _fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            _fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (_shakeToggle)
        {
            var cameraFollow = FindFirstObjectByType<CameraFollow>();
            bool shakeEnabled = cameraFollow ? cameraFollow.ShakeEnabled : PlayerPrefs.GetInt(PlayerPrefsKeys.cameraShakeKey, 1) == 1;
            _shakeToggle.SetIsOnWithoutNotify(shakeEnabled);
            _shakeToggle.onValueChanged.RemoveListener(SetShakeEnabled);
            _shakeToggle.onValueChanged.AddListener(SetShakeEnabled);
        }

        if (_brightnessSlider)
        {
            float savedValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.brightnessKey, BrightnessSliderDefault);
            _brightnessSlider.SetValueWithoutNotify(savedValue);
            _brightnessSlider.onValueChanged.RemoveListener(SetBrightness);
            _brightnessSlider.onValueChanged.AddListener(SetBrightness);
        }
    }

    private void SetShakeEnabled(bool value)
    {
        var cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow) cameraFollow.SetShakeEnabled(value);
        else
        {
            // Sin cámara en escena (ej. probando el menú solo): guardar igual para que se aplique al entrar a jugar.
            PlayerPrefs.SetInt(PlayerPrefsKeys.cameraShakeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private void SetBrightness(float sliderValue)
    {
        PlayerPrefs.SetFloat(PlayerPrefsKeys.brightnessKey, sliderValue);
        PlayerPrefs.Save();
        ApplyBrightness(sliderValue, _maxExposureEV);
    }

    // Aplica el brillo guardado al Volume global de la escena actual (si hay uno con Color Adjustments habilitado).
    // Se llama tanto desde este panel como al cargar cada escena de juego, para que afecte aunque no se abra Opciones.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedBrightnessOnStart()
    {
        float savedValue = PlayerPrefs.GetFloat(PlayerPrefsKeys.brightnessKey, BrightnessSliderDefault);
        ApplyBrightness(savedValue, 2f);
    }

    private static void ApplyBrightness(float sliderValue, float maxExposureEV)
    {
        var volume = FindFirstObjectByType<Volume>();
        if (!volume || !volume.profile) return;
        if (!volume.profile.TryGet(out ColorAdjustments colorAdjustments)) return;

        float exposure = (sliderValue - BrightnessSliderDefault) / BrightnessSliderDefault * maxExposureEV;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = exposure;
    }

    // Screen.resolutions solo lista lo que el monitor soporta: la mas grande es la nativa, no se puede pasar de ahi.
    private void BuildResolutionList()
    {
        _resolutions.Clear();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!_resolutions.Contains(v)) _resolutions.Add(v);
        }

        var current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        if (!_resolutions.Contains(current)) _resolutions.Add(current);

        _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
    }

    private int FindStartIndex()
    {
        Vector2Int target = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        if (PlayerPrefs.HasKey(PlayerPrefsKeys.resolutionWidthKey) && PlayerPrefs.HasKey(PlayerPrefsKeys.resolutionHeightKey))
            target = new Vector2Int(PlayerPrefs.GetInt(PlayerPrefsKeys.resolutionWidthKey), PlayerPrefs.GetInt(PlayerPrefsKeys.resolutionHeightKey));

        int index = _resolutions.IndexOf(target);
        return index >= 0 ? index : _resolutions.Count - 1;
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= _resolutions.Count) return;
        var v = _resolutions[index];
        Screen.SetResolution(v.x, v.y, Screen.fullScreen);
        PlayerPrefs.SetInt(PlayerPrefsKeys.resolutionWidthKey, v.x);
        PlayerPrefs.SetInt(PlayerPrefsKeys.resolutionHeightKey, v.y);
        PlayerPrefs.Save();
    }

    private void SetFullscreen(bool value)
    {
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(PlayerPrefsKeys.fullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
