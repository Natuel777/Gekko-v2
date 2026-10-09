using UnityEngine;

public class LineController : MonoBehaviour
{
    [SerializeField] private Transform[] _positions;
    private LineRenderer _lineRenderer;

    //Por ahora no se necesita repetir el for loop en Update
    private void Start()
    {
        TryGetComponent(out _lineRenderer);

        if(_positions.Length < 0) return;

        _lineRenderer.positionCount = _positions.Length;

        for(int i = 0; i < _positions.Length; i++)
            _lineRenderer.SetPosition(i, _positions[i].position);
    }
}
