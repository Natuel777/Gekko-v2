using System.Collections.Generic;
using UnityEngine;

public class LineController : MonoBehaviour
{
    [SerializeField] private List<Transform> _positions = new List<Transform>();
    private LineRenderer _lineRenderer;

    //Por ahora no se necesita repetir el for loop en Update
    private void Start()
    {
        TryGetComponent(out _lineRenderer);
        UpdateLRPositions();
    }

    public void UpdateLRPositions()
    {
        if(_positions.Count < 0 || _lineRenderer == null) return;

        _lineRenderer.positionCount = _positions.Count;

        for(int i = 0; i < _positions.Count; i++)
            _lineRenderer.SetPosition(i, _positions[i].position);
    }

    public void RemovePositions(Transform[] positions)
    {
        if(positions == null) return;

        foreach(Transform position in positions)
        {
            if(!_positions.Contains(position)) continue;

            _positions.Remove(position);
        }

        OpenLoop();
        UpdateLRPositions();
    }

    private void OpenLoop()
    {
        if(_lineRenderer != null && _lineRenderer.loop)
            _lineRenderer.loop = false;
    }
}
