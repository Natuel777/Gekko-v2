using UnityEngine;

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

    public bool SnapToCenter => _snapToCenter;
    public float AssistRadius => _assistRadius;
    public Vector3 AnchorPosition => transform.position;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, _assistRadius);
    }
}
