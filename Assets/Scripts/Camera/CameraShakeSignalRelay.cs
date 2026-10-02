using UnityEngine;

public class CameraShakeSignalRelay : MonoBehaviour
{
    [SerializeField] private float _force = 1.5f;

    public void Shake() => EventManager.Trigger<float>("OnCameraShake", _force);
}
