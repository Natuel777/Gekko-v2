using UnityEngine;

// Qué hay delante de la cámara para engancharse con la lengua. Tira un SphereCast desde la cámara hacia donde mira y
// clasifica lo que toca según su layer:
//   - SwingAttach (los ganchos de swing; se eligen en Player > Grappable Layers)  -> Swing.
//   - GrapplePoint (GekkoGrapple.GrapplePointLayer)                              -> Grapple.
// Con el click derecho, GekkoSwinging mira ese resultado: Swing arranca el swing (GekkoSwinging) y Grapple arranca el
// grapple (GekkoGrapple). Antes esto era CheckForSwingPoints, dentro de GekkoSwinging.
//
// También mueve el indicador (_predictionPoint): una partícula que se queda sobre el ancla que se está apuntando.
public sealed class GekkoDetection
{
    public enum TargetType { None, Swing, Grapple }

    // Nombre del hijo de cada gancho que marca el punto exacto al que se engancha la lengua (el ancla). Los prefabs
    // Gancho y GrapplePoint lo tienen.
    private const string AnchorName = "GrapplePoint";

    private readonly Transform _camera, _predictionPoint;
    private readonly LayerMask _mask;
    private readonly float _sphereRadius, _maxDistance;

    private Vector3 _previousAnchorPosition;

    // Buscar un hijo por nombre (Find) cuesta, y apuntando al mismo gancho el resultado es siempre el mismo: se recuerda
    // el último gancho tocado y su ancla.
    private Transform _lastHitObject, _cachedAnchor;

    // Lo que se apunta ahora mismo. Se actualiza en Detect, que corre una vez por frame (ArtificialUpdate) y al hacer click.
    public TargetType Type { get; private set; }

    // Dónde está el ancla de lo apuntado. Solo vale si Type no es None.
    public Vector3 Point { get; private set; }

    public GekkoDetection(Transform camera, LayerMask swingLayers, Transform predictionPoint, float sphereRadius, float maxDistance)
    {
        _camera = camera;
        _mask = BuildMask(swingLayers);
        _predictionPoint = predictionPoint;
        _sphereRadius = sphereRadius;
        _maxDistance = maxDistance;
    }

    // Las capas que mira la detección: los ganchos de swing (las del Inspector de Player) más la de los GrapplePoint, que
    // no se elige en el Inspector sino que se suma acá. Es público y estático para que el gizmo de Player use la misma.
    public static LayerMask BuildMask(LayerMask swingLayers) => swingLayers.value | (1 << GekkoGrapple.GrapplePointLayer);

    // El SphereCast de la detección. También es público y estático por el gizmo de Player. Usa Collide porque los ganchos
    // son triggers: así se detectan aunque Physics.queriesHitTriggers esté apagado.
    public static bool SphereCast(Transform camera, float radius, float distance, LayerMask mask, out RaycastHit hit)
    {
        return Physics.SphereCast(camera.position, radius, camera.forward, out hit, distance, mask, QueryTriggerInteraction.Collide);
    }

    // Lo llama GekkoSwinging.ArtificialUpdate en cada frame: detecta y mueve el indicador.
    public void ArtificialUpdate()
    {
        Detect();
        UpdatePredictionParticle();
    }

    // Tira el SphereCast ahora y guarda el resultado en Type y Point. Devuelve el tipo, por comodidad. GekkoSwinging lo
    // vuelve a llamar al hacer click, para no usar lo del último frame: la cámara pudo moverse desde entonces.
    public TargetType Detect()
    {
        Type = TargetType.None;

        if(!SphereCast(_camera, _sphereRadius, _maxDistance, _mask, out RaycastHit hit)) return Type;

        // Un objeto sin el hijo ancla no se puede enganchar.
        Transform anchor = GetAnchor(hit.transform);

        if(anchor == null) return Type;

        Point = anchor.position;

        // La layer que cuenta es la del collider golpeado. Todo lo que no sea GrapplePoint es swing: la máscara de Player
        // solo tiene los ganchos de swing.
        Type = hit.collider.gameObject.layer == GekkoGrapple.GrapplePointLayer ? TargetType.Grapple : TargetType.Swing;
        return Type;
    }

    // El indicador: la partícula prendida y sobre el ancla mientras se apunta a algo (swing o grapple), apagada si no.
    private void UpdatePredictionParticle()
    {
        if(_predictionPoint == null) return;

        ParticleSystem ps = _predictionPoint.GetComponentInChildren<ParticleSystem>();

        if(ps == null) return;

        if(Type == TargetType.None)
        {
            HidePredictionParticle(ps);
            return;
        }

        ps.Play();

        if(Point != _previousAnchorPosition)
            _predictionPoint.position = Point;

        _previousAnchorPosition = Point;
    }

    // Ps_Selected tiene una única partícula con startLifetime = 99999 y sin loop. Con un Stop() común (StopEmitting) solo
    // se deja de emitir: la partícula ya viva sigue ahí, congelada en la última posición. Por eso hay que pedir
    // StopEmittingAndClear, que además la elimina. El próximo Play() la vuelve a emitir.
    private void HidePredictionParticle(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Transform GetAnchor(Transform hitObject)
    {
        if(hitObject == _lastHitObject) return _cachedAnchor;

        _lastHitObject = hitObject;
        _cachedAnchor = hitObject.Find(AnchorName);
        return _cachedAnchor;
    }
}
