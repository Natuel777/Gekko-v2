using UnityEngine;

public class OscillateRotation : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _duration = 1f;
    [SerializeField] private float _speed = 1f;

    [Header("Rotation")]
    [SerializeField] private float _maxAngle = 35f;

    private float _timer;

    private void OnEnable()
    {
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime * _speed;

        if (_timer >= _duration)
        {
            transform.localEulerAngles = Vector3.zero;
            gameObject.SetActive(false);
            return;
        }

        float angle = _maxAngle * Mathf.Sin(_timer * 2f * Mathf.PI / _duration);
        transform.localEulerAngles = new Vector3(0f, 0f, angle);
    }
}
