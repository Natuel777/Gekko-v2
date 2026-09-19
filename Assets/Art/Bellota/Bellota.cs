using UnityEngine;

public class Bellota : MonoBehaviour
{
    private Transform _transform;

    public float amplitude = 0.1f;
    public float speed = 1f;
    public float rotationSpeed = 50f;
    public Transform topPart;
    float startY;

    private void Start()
    {
        _transform = transform;
        startY = transform.position.y;
    }

    private void Update()
    {
       
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        Vector3 pos = _transform.position;
        pos.y = startY + offset;
        _transform.position = pos;

        topPart.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
