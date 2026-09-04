using UnityEngine;

public class PulseScaleRotate : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _duration = 1.5f;
    [SerializeField] private float _speed = 1f;

    [Header("Scale")]
    [SerializeField] private bool _animateScale = true;
    [SerializeField] private float _pulseScale = 0.15f;

    [Header("Rotation")]
    [SerializeField] private bool _animateRotation = true;
    [SerializeField] private float _maxAngle = 35f;

    private Vector3 _baseScale;
    private float _timer;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime * _speed;

        if (_timer >= _duration)
        {
            transform.localScale = _baseScale;
            transform.localEulerAngles = Vector3.zero;
            gameObject.SetActive(false);
            return;
        }

        if (_animateScale)
        {
            float smoothT = OscillationMath.PulseT(_timer, _duration);
            transform.localScale = _baseScale + Vector3.one * (smoothT * _pulseScale);
        }

        if (_animateRotation)
        {
            float angle = OscillationMath.Swing(_timer, _duration, _maxAngle);
            transform.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }
}
