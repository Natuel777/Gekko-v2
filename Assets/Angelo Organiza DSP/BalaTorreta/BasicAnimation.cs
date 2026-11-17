using UnityEngine;

public class BasicAnimation : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private bool floating = true;
    [SerializeField] private float floatHeight = 0.5f;
    [SerializeField] private float floatSpeed = 1f;

    [Header("Rotation")]
    [SerializeField] private bool rotating = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 45, 0);

    [Header("Rotation Sway")]
    [SerializeField] private bool rotationSway = false;
    [SerializeField] private float swayAngle = 15f;
    [SerializeField] private float swaySpeed = 1f;

    [Header("Randomness")]
    [SerializeField] private bool randomOffset = true;

    private Vector3 currentRotation;
    private float currentSway;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float randomTimeOffset;

    private Vector3 additionalPositionOffset;
    private Vector3 additionalRotationOffset;

    public Vector3 RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    public bool IsRotating
    {
        get => rotating;
        set => rotating = value;
    }

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        randomTimeOffset = randomOffset ? Random.Range(0f, 100f) : 0f;
    }

    private void Update()
    {
        if (floating)
        {
            Float();
        }

        if (rotating)
        {
            Rotate();
        }
        if (rotationSway)
        {
            Sway();
        }
    }

    private void LateUpdate()
    {
        transform.rotation = startRotation * Quaternion.Euler(currentRotation) * Quaternion.Euler(currentSway, 0, 0);

        transform.position += additionalPositionOffset;
        transform.rotation *= Quaternion.Euler(additionalRotationOffset);

        additionalPositionOffset = Vector3.zero;
        additionalRotationOffset = Vector3.zero;
    }

    private void Float()
    {
        float newY = startPosition.y + Mathf.Sin((Time.time + randomTimeOffset) * floatSpeed) * floatHeight;

        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    private void Rotate()
    {
        currentRotation += rotationSpeed * Time.deltaTime;
    }

    private void Sway()
    {
        currentSway = Mathf.Sin((Time.time + randomTimeOffset) * swaySpeed) * swayAngle;
    }

    public void AddPositionOffset(Vector3 offset)
    {
        additionalPositionOffset += offset;
    }

    public void AddRotationOffset(Vector3 offset)
    {
        additionalRotationOffset += offset;
    }
}
