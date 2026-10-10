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

    // Saca de la lista las posiciones indicadas (ej. las de un PurificationCore roto), abre el loop y redibuja la línea.
    // Se sacan ANTES de que se destruyan los Transforms, así UpdateLRPositions nunca lee uno destruido.
    public void RemovePositions(Transform[] positions)
    {
        if(positions != null)
            foreach(Transform position in positions)
                _positions.Remove(position);

        OpenLoop();
        UpdateLRPositions();
    }

    // Un núcleo roto corta el anillo: si la línea estaba en loop pasa a false; si ya estaba en false se deja como está.
    private void OpenLoop()
    {
        if(_lineRenderer != null && _lineRenderer.loop)
            _lineRenderer.loop = false;
    }
}
