using TMPro;
using UnityEngine;

// Muestra la corriente en un punto usando SplineRiver.Sample: una flecha sobre la superficie
// (largo y color segun la velocidad) y opcionalmente un texto con los m/s. Ejemplo de uso de la API.
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
[AddComponentMenu("Gekko/River/Sandbox/River Probe")]
public class RiverProbe : MonoBehaviour
{
    [SerializeField] SplineRiver _river;
    [SerializeField, Min(0.05f)] float _arrowScale = 0.6f;
    [SerializeField] float _maxSpeed = 5f;
    [SerializeField] Color _slowColor = new Color(0.3f, 0.9f, 1f);
    [SerializeField] Color _fastColor = new Color(1f, 0.3f, 0.2f);
    [SerializeField] TextMeshPro _label;

    LineRenderer _line;

    void OnEnable()
    {
        _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 5;
    }

    void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if(_line == null)
            OnEnable();

        if(_river == null || !_river.Sample(transform.position, out SplineRiver.RiverSample sample) || !sample.Inside)
        {
            _line.enabled = false;
            if(_label != null)
                _label.text = "";
            return;
        }

        _line.enabled = true;
        Vector3 dir = sample.FlowDirection;
        float length = Mathf.Max(0.25f, Mathf.Abs(sample.FlowSpeed) * _arrowScale);
        Vector3 start = sample.SurfacePoint + Vector3.up * 0.12f - dir * length * 0.5f;
        Vector3 tip = start + dir * length;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * Mathf.Min(0.35f, length * 0.3f);
        Vector3 back = tip - dir * Mathf.Min(0.45f, length * 0.35f);

        _line.SetPosition(0, start);
        _line.SetPosition(1, tip);
        _line.SetPosition(2, back + side);
        _line.SetPosition(3, tip);
        _line.SetPosition(4, back - side);

        Color color = Color.Lerp(_slowColor, _fastColor, Mathf.Clamp01(sample.FlowSpeed / _maxSpeed));
        _line.startColor = color;
        _line.endColor = color;

        if(_label != null)
        {
            _label.text = sample.FlowSpeed.ToString("0.0") + " m/s";
            _label.color = color;
            _label.transform.position = sample.SurfacePoint + Vector3.up * 1.1f;
        }
    }
}
