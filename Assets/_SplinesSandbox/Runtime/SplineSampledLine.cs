using UnityEngine;
using UnityEngine.Splines;

// Demo: camino de integracion de bajo riesgo para reemplazar LineController (nucleo <-> enredadera,
// hoy segmentos rectos entre Transforms) por una curva. Mismo patron que
// LineController.UpdateLRPositions(), pero samplea puntos de una SplineContainer en vez de leer
// Transforms sueltos.
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class SplineSampledLine : MonoBehaviour
{
    [SerializeField] private SplineContainer _spline;
    [SerializeField, Min(2)] private int _resolution = 30;

    private LineRenderer _lineRenderer;

    private void OnEnable()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        UpdateLine();
    }

    // Solo en Editor (no Play): para ver el cambio en vivo si mueven los knots a mano.
    private void Update()
    {
        if(!Application.isPlaying) UpdateLine();
    }

    private void UpdateLine()
    {
        if(_spline == null || _lineRenderer == null) return;

        _lineRenderer.positionCount = _resolution;

        for(int i = 0; i < _resolution; i++)
        {
            float t = i / (float)(_resolution - 1);
            Vector3 localPos = _spline.Spline.EvaluatePosition(t);
            _lineRenderer.SetPosition(i, _spline.transform.TransformPoint(localPos));
        }
    }
}
