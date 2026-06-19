using UnityEngine;

public class TeeterTotterPlatform : MonoBehaviour
{
    [SerializeField] protected Transform _platformMesh;
    [SerializeField] protected float _maxAngle = 20f;
    [SerializeField] protected float _rotationSpeed = 3f;
    [SerializeField] protected float _maxDistance = 3f;

    protected Transform _playerOnPlatform;
    protected float _currentAngle = 0f;

    public Transform PlayerOnPlatform { set { _playerOnPlatform = value; } }
    protected virtual void Start()
    {
        _platformMesh.GetComponent<TeeterTotterCollider>().SetParent(this);
    }
    protected virtual void FixedUpdate()
    {
        CalculateAngle();
    }
    protected virtual void CalculateAngle()
    {
        float targetAngle = 0f;

        if (_playerOnPlatform != null)
        {
            Vector3 localPos = transform.InverseTransformPoint(_playerOnPlatform.position);

            float normalizedPos = Mathf.Clamp(localPos.x / _maxDistance, -1f, 1f);

            targetAngle = normalizedPos * _maxAngle;
        }

        _currentAngle = Mathf.MoveTowards(_currentAngle, targetAngle, _rotationSpeed * Time.deltaTime);
        if (Mathf.Abs(_currentAngle - targetAngle) < 0.01f)
            _currentAngle = targetAngle;
        _platformMesh.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position - new Vector3(_maxDistance,-0.25f,0), transform.position + new Vector3(_maxDistance, 0.25f, 0));
    }
}
