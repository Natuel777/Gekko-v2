using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _offsetX = 0, _offsetZ = 0, _offsetY = 0;

    private void Update()
    {
        transform.position = _target.position;
        transform.rotation = _target.rotation;
    }
}
