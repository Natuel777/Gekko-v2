using UnityEngine;

// Movimiento del espejo: gira el pivot entre "acostado" (la rotación que tiene el pivot en el prefab) y "parado" (esa misma
// rotación girada raisedAngle grados sobre su eje X local). Solo mueve: qué refleja y cuándo lo decide
// MirrorReflectBehaviour. Sin pivot (espejo fijo) no hace nada.
public class MirrorRaiseBehaviour
{
    private readonly Transform _pivot;
    private readonly Quaternion _lyingRotation, _raisedRotation;
    private readonly float _seconds;

    // 0 = acostado, 1 = parado. _target es hacia dónde va.
    private float _progress, _target;

    public MirrorRaiseBehaviour(Transform pivot, float raisedAngle, float seconds)
    {
        _pivot = pivot;
        _seconds = Mathf.Max(0.01f, seconds);

        if(_pivot == null) return;

        _lyingRotation = _pivot.localRotation;
        _raisedRotation = _lyingRotation * Quaternion.Euler(raisedAngle, 0f, 0f);
    }

    // Lo llaman los estados: true = hacia parado, false = hacia acostado. Se mueve de a poco, en ArtificialUpdate.
    public void Activated(bool raised) => _target = raised ? 1f : 0f;

    // Sin animación: se usa al arrancar, para que un espejo que nace activo no se levante a la vista.
    public void Snap(bool raised)
    {
        _target = raised ? 1f : 0f;
        _progress = _target;
        Apply();
    }

    public void ArtificialUpdate()
    {
        if(_pivot == null || _progress == _target) return;

        _progress = Mathf.MoveTowards(_progress, _target, Time.deltaTime / _seconds);
        Apply();
    }

    private void Apply()
    {
        if(_pivot == null) return;

        _pivot.localRotation = Quaternion.Slerp(_lyingRotation, _raisedRotation, Mathf.SmoothStep(0f, 1f, _progress));
    }
}
