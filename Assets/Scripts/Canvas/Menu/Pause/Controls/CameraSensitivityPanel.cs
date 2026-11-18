using UnityEngine;
using UnityEngine.UI;

// La cámara (CameraFollow) es parte de la escena de juego, no del Canvas_Options, así que se busca en vez de
// asignarse por inspector (igual que ScreenOptions llega al jugador vía GameManager.Instance.Pj).
// Si no hay cámara (ej. Opciones desde el menú principal) igual se guarda en PlayerPrefs: CameraFollow lo lee en Start.
public class CameraSensitivityPanel : MonoBehaviour
{
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private Toggle _invertYToggle;

    private CameraFollow _cameraFollow;

    private void OnEnable()
    {
        _cameraFollow = FindFirstObjectByType<CameraFollow>();

        if (_sensitivitySlider)
        {
            float value = _cameraFollow ? _cameraFollow.CameraSensitivity : PlayerPrefs.GetFloat(PlayerPrefsKeys.cameraSensitivityKey, 1f);
            _sensitivitySlider.SetValueWithoutNotify(value);
            _sensitivitySlider.onValueChanged.RemoveListener(SetSensitivity);
            _sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        }

        if (_invertYToggle)
        {
            bool value = _cameraFollow ? _cameraFollow.InvertY : PlayerPrefs.GetInt(PlayerPrefsKeys.invertYKey, 0) == 1;
            _invertYToggle.SetIsOnWithoutNotify(value);
            _invertYToggle.onValueChanged.RemoveListener(SetInvertY);
            _invertYToggle.onValueChanged.AddListener(SetInvertY);
        }
    }

    private void SetSensitivity(float value)
    {
        if (_cameraFollow) { _cameraFollow.SetCameraSensitivity(value); return; }
        PlayerPrefs.SetFloat(PlayerPrefsKeys.cameraSensitivityKey, value);
        PlayerPrefs.Save();
    }

    private void SetInvertY(bool value)
    {
        if (_cameraFollow) { _cameraFollow.SetInvertY(value); return; }
        PlayerPrefs.SetInt(PlayerPrefsKeys.invertYKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
