using UnityEngine;

public class OrbitPivot : MonoBehaviour
{
    private Vector3 rotationAxis = Vector3.forward;
    private float rotationSpeed = 360f;

    private void Update()
    {
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
