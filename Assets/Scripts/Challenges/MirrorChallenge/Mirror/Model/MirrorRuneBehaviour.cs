using UnityEngine;
using UnityEngine.Rendering.Universal;

// Runa del espejo (el mismo decal que la placa de presión, sobre la piedra principal del marco): se enciende con un fade
// suave recién cuando el espejo terminó de levantarse, y se apaga apenas empieza a acostarse. Solo dibuja: cuándo está
// parado lo dice MirrorRaiseBehaviour. Sin decal no hace nada.
public class MirrorRuneBehaviour
{
    private readonly DecalProjector _rune;
    private readonly float _seconds;
    private readonly float _startScale;
    private readonly Vector3 _size;

    // 0 = apagada, 1 = encendida.
    private float _fade;

    public MirrorRuneBehaviour(DecalProjector rune, float seconds, float startScale)
    {
        _rune = rune;
        _seconds = Mathf.Max(0.01f, seconds);
        _startScale = startScale;

        if(_rune == null) return;

        _size = _rune.size;
    }

    // Sin animación: al arrancar, un espejo que nace parado ya tiene la runa prendida.
    public void Snap(bool lit)
    {
        _fade = lit ? 1f : 0f;
        Apply();
    }

    public void ArtificialUpdate(bool lit)
    {
        if(_rune == null) return;

        float target = lit ? 1f : 0f;
        if(_fade == target) return;

        _fade = Mathf.MoveTowards(_fade, target, Time.deltaTime / _seconds);
        Apply();
    }

    private void Apply()
    {
        if(_rune == null) return;

        _rune.enabled = _fade > 0f;
        _rune.fadeFactor = Mathf.SmoothStep(0f, 1f, _fade);

        // Al encenderse "se asienta" (crece hasta su tamaño), como la runa de la placa.
        float scale = Mathf.Lerp(_startScale, 1f, 1f - Mathf.Pow(1f - _fade, 3f));
        _rune.size = new Vector3(_size.x * scale, _size.y * scale, _size.z);
    }
}
