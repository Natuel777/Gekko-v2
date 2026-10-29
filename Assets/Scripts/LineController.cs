using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
public class LineController : MonoBehaviour
{
    [SerializeField] private List<Transform> _positions = new List<Transform>();

    [Header("Spline Container (editable en la Scene view)")]
    [Tooltip("Si se asigna, la línea sigue este spline en vez de los puntos de arriba. Tiene prioridad sobre 'Use Spline'. Se puede editar arrastrando los knots en la Scene view, incluso fuera de Play Mode.")]
    [SerializeField] private SplineContainer _splineContainer;
    [Tooltip("Qué curva usar dentro del Spline Container (un mismo container puede tener varias: Spline (0), Spline (1), etc.). Varios LineController pueden apuntar al mismo índice para compartir una sola curva.")]
    [SerializeField] private int _splineIndex;
    [Tooltip("Piso mínimo de segmentos a lo largo de todo el spline, por si 'Spline Sample Spacing' da un número más chico (curvas muy cortas).")]
    [Min(2)]
    [SerializeField] private int _splineSamples = 60;
    [Tooltip("Distancia en unidades de mundo entre cada segmento de la curva. El número real de segmentos se recalcula según el largo del spline (largo / spacing), así una curva corta y una larga quedan igual de lisas sin tener que ajustar 'Spline Samples' a mano en cada núcleo. 0 = ignorar y usar 'Spline Samples' fijo de arriba.")]
    [Min(0f)]
    [SerializeField] private float _splineSampleSpacing = 0.3f;

    [Header("Catmull-Rom (si no hay Spline Container)")]
    [Tooltip("Si está activo, la línea se curva suavemente entre los puntos de arriba (Catmull-Rom) en vez de ir recta de uno a otro.")]
    [SerializeField] private bool _useSpline;
    [Tooltip("Segmentos dibujados entre cada par de puntos consecutivos. Más = curva más suave.")]
    [Min(2)]
    [SerializeField] private int _splineResolution = 10;

    private LineRenderer _lineRenderer;

    // La curva que este LineController sigue dentro del Spline Container. Varios LineController pueden
    // compartir el mismo Spline Container apuntando al mismo _splineIndex, y van a dibujar la misma forma.
    private Spline CurrentSpline =>
        (_splineContainer != null && _splineIndex >= 0 && _splineIndex < _splineContainer.Splines.Count)
            ? _splineContainer.Splines[_splineIndex]
            : null;

    //Por ahora no se necesita repetir el for loop en Update
    private void Start()
    {
        TryGetComponent(out _lineRenderer);
        UpdateLRPositions();
    }

    // ExecuteAlways + esto: si hay un Spline Container asignado, redibuja todo el tiempo fuera de Play Mode
    // para que mover los knots en la Scene view se vea reflejado al instante, sin tener que apretar Play.
    private void Update()
    {
        if(_splineContainer != null && !Application.isPlaying)
            UpdateLRPositions();
    }

    public void UpdateLRPositions()
    {
        if(_lineRenderer == null && !TryGetComponent(out _lineRenderer)) return;

        Spline spline = CurrentSpline;

        if(spline != null && spline.Count >= 2)
        {
            DrawFromSpline(spline);
            return;
        }

        if(_positions.Count < 0) return;

        if(_useSpline && _positions.Count >= 2)
        {
            List<Vector3> splinePositions = BuildCatmullRomPositions();
            _lineRenderer.positionCount = splinePositions.Count;
            _lineRenderer.SetPositions(splinePositions.ToArray());
            return;
        }

        _lineRenderer.positionCount = _positions.Count;

        for(int i = 0; i < _positions.Count; i++)
            _lineRenderer.SetPosition(i, _positions[i].position);
    }

    private void DrawFromSpline(Spline spline)
    {
        int samples = _splineSamples;

        if(_splineSampleSpacing > 0f)
        {
            float length = SplineUtility.CalculateLength(spline, _splineContainer.transform.localToWorldMatrix);
            samples = Mathf.Max(_splineSamples, Mathf.CeilToInt(length / _splineSampleSpacing));
        }

        _lineRenderer.positionCount = samples + 1;

        for(int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            spline.Evaluate(t, out float3 position, out _, out _);
            _lineRenderer.SetPosition(i, _splineContainer.transform.TransformPoint((Vector3)position));
        }
    }

    // Catmull-Rom a través de los mismos puntos que ya existían: no agrega control nuevo, solo redondea el
    // trazo recto. En las puntas repite el primer/último punto como vecino faltante, así la curva arranca y
    // termina justo en _positions[0] y _positions[^1] en vez de desviarse antes de llegar.
    private List<Vector3> BuildCatmullRomPositions()
    {
        int count = _positions.Count;
        List<Vector3> points = new List<Vector3>((count - 1) * _splineResolution + 1);

        for(int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = _positions[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = _positions[i].position;
            Vector3 p2 = _positions[i + 1].position;
            Vector3 p3 = _positions[Mathf.Min(i + 2, count - 1)].position;

            for(int s = 0; s < _splineResolution; s++)
                points.Add(CatmullRom(p0, p1, p2, p3, (float)s / _splineResolution));
        }

        points.Add(_positions[count - 1].position);
        return points;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    // Apaga el LineRenderer entero. Pensado para engancharse a un evento de "esto se destruyó/purificó"
    // (ej. LittleBarricade.OnPurified) cuando la línea no tiene puntos propios que recortar de a poco
    // (como pasa con un Spline Container compartido) y lo que corresponde es que desaparezca del todo.
    public void Hide()
    {
        if(_lineRenderer == null && !TryGetComponent(out _lineRenderer)) return;

        _lineRenderer.enabled = false;
    }

    public void RemovePositions(Transform positionsGroup)
    {
        if(positionsGroup == null) return;

        RemoveTrackedPosition(positionsGroup);

        for(int i = 0; i < positionsGroup.childCount; i++)
            RemoveTrackedPosition(positionsGroup.GetChild(i));

        UpdateLRPositions();
    }

    // Saca el punto de _positions y, si este LineController sigue un Spline Container armado 1 a 1 con esos
    // mismos puntos (ver SeedSplineFromPositions en el setup inicial), borra también el knot en el mismo
    // índice para que el spline se acorte igual que antes al romperse la barricada.
    private void RemoveTrackedPosition(Transform point)
    {
        int index = _positions.IndexOf(point);
        if(index < 0) return;

        _positions.RemoveAt(index);

        Spline spline = CurrentSpline;
        if(spline != null && index < spline.Count)
            spline.RemoveAt(index);
    }

    //private void OpenLoop()
    //{
    //    if(_lineRenderer != null && _lineRenderer.loop)
    //        _lineRenderer.loop = false;
    //}
}
