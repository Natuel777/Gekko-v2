using UnityEngine;
using UnityEngine.UI;

// La cámara (CameraFollow) es parte de la escena de juego, no del Canvas_Options, así que se busca en vez de
// asignarse por inspector (igual que ScreenOptions llega al jugador vía GameManager.Instance.Pj).
public class CameraSensitivityPanel : MonoBehaviour
{
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private Toggle _invertYToggle;

    private CameraFollow _cameraFollow;

    private void OnEnable()
    {
        _cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (!_cameraFollow) return;

        if (_sensitivitySlider)
        {
            _sensitivitySlider.SetValueWithoutNotify(_cameraFollow.CameraSensitivity);
            _sensitivitySlider.onValueChanged.RemoveListener(SetSensitivity);
            _sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        }

        if (_invertYToggle)
        {
            _invertYToggle.SetIsOnWithoutNotify(_cameraFollow.InvertY);
            _invertYToggle.onValueChanged.RemoveListener(SetInvertY);
            _invertYToggle.onValueChanged.AddListener(SetInvertY);
        }
    }

    private void SetSensitivity(float value) => _cameraFollow?.SetCameraSensitivity(value);
    private void SetInvertY(bool value) => _cameraFollow?.SetInvertY(value);
}
