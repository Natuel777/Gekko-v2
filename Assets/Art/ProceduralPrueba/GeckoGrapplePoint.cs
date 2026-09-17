using UnityEngine;
using System.Collections;

/// <summary>
/// Marca un punto/objeto al que la lengua-soga se puede enganchar. Opcional: si
/// GeckoTongue tiene <c>_requireGrapplePoint = false</c>, cualquier superficie de la
/// máscara sirve y este componente solo se usa para forzar un ancla exacta o mover
/// el enganche con una plataforma.
/// </summary>
public class GeckoGrapplePoint : MonoBehaviour
{
    [Tooltip("Si está, la soga se ancla exactamente acá en vez de en el punto del raycast.")]
    [SerializeField] private bool _snapToCenter = true;

    [Tooltip("Radio para el auto-aim: si el rayo pasa a menos de esto, engancha igual.")]
    [SerializeField] private float _assistRadius = 0.6f;

    [Header("Feedback visual (destello, sin partículas nuevas)")]
    [Tooltip("Color al enganchar la lengua acá. Mismo tono que el gizmo de edición.")]
    [SerializeField] private Color _attachFlashColor = new Color(0.3f, 1f, 0.5f, 1f);
    [Tooltip("Color al soltarse desde este punto.")]
    [SerializeField] private Color _releaseFlashColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private float _flashDuration = 0.25f;

    public bool SnapToCenter => _snapToCenter;
    public float AssistRadius => _assistRadius;
    public Vector3 AnchorPosition => transform.position;

    private Renderer _renderer;
    private Color _baseColor;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null) _baseColor = _renderer.material.color;
    }

    /// <summary> Lo llama GeckoTongue al enganchar la lengua acá. </summary>
    public void FlashAttach() => Flash(_attachFlashColor);

    /// <summary> Lo llama GeckoTongue al soltarse (Detach/LaunchOff) desde este punto. </summary>
    public void FlashRelease() => Flash(_releaseFlashColor);

    private void Flash(Color color)
    {
        if (_renderer == null) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(color));
    }

    private IEnumerator FlashRoutine(Color color)
    {
        float t = 0f;
        while (t < _flashDuration)
        {
            t += Time.deltaTime;
            _renderer.material.color = Color.Lerp(color, _baseColor, t / _flashDuration);
            yield return null;
        }
        _renderer.material.color = _baseColor;
        _flashRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _assistRadius);
    }
}
