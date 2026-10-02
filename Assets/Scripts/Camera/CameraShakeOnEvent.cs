using System.Collections;
using UnityEngine;

// Shake para cámaras sin Cinemachine (no reciben el CinemachineImpulseSource que ya usa CameraFollow).
public class CameraShakeOnEvent : MonoBehaviour
{
    [SerializeField] private float _duration = 0.3f;
    [SerializeField] private float _magnitude = 0.15f;

    private Coroutine _shaking;
    private Vector3 _basePosition;

    private void OnEnable()
    {
        _basePosition = transform.localPosition;
        EventManager.Subscribe<float>("OnCameraShake", OnShake);
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe<float>("OnCameraShake", OnShake);

        if(_shaking != null) StopCoroutine(_shaking);
        transform.localPosition = _basePosition;
    }

    private void OnShake(float force)
    {
        if(_shaking != null) StopCoroutine(_shaking);
        _shaking = StartCoroutine(ShakeRoutine(force));
    }

    private IEnumerator ShakeRoutine(float force)
    {
        float elapsed = 0f;

        while(elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - (elapsed / _duration);
            transform.localPosition = _basePosition + Random.insideUnitSphere * _magnitude * force * damper;
            yield return null;
        }

        transform.localPosition = _basePosition;
    }
}
