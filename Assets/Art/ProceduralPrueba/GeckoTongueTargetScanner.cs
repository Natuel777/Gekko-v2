using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un collider ya interpretado como objetivo de la lengua: qué es, hasta dónde llega la punta
/// y qué hay que hacer al llegar. Es un struct de solo datos: escanear no genera basura.
/// </summary>
public struct GeckoTongueCandidate
{
    public Collider Collider;
    /// <summary> Objeto que se sigue si se mueve (y que se arrastra a la boca al comer). </summary>
    public Transform Transform;
    /// <summary> Dónde termina la punta de la lengua (mundo). </summary>
    public Vector3 Point;
    public GeckoTongueInteraction Kind;

    public IGeckoEdible Edible;
    public Collectible Collectible;
    public bool EdibleByLayer;
    public GeckoGrapplePoint Grapple;
    public IDamageable Damageable;
    public IGeckoTongueReachable Reachable;

    public float Priority;
    public float Distance;
    public float Score;

    public bool IsValid => Collider != null;
    public string Label => Transform != null ? Transform.name : (Collider != null ? Collider.name : "-");
}

/// <summary>
/// Busca el objetivo interactuable más cercano alrededor de la boca. Clase plana (sin Update
/// propio) que GeckoTongue posee y consulta, igual que el resto de los sistemas del proyecto.
///
/// Por qué OverlapSphere + Linecast y no un SphereCast: la lengua sale en cualquier
/// dirección del cono (parkour: paredes, techos, coleccionables al costado), un SphereCast
/// solo ve UNA dirección. El overlap encuentra todo lo que hay en alcance, el filtro por
/// componentes descarta lo que no es interactuable, y el Linecast confirma que la lengua no
/// tendría que atravesar geometría para llegar.
/// </summary>
public class GeckoTongueTargetScanner
{
    public enum Status { Valid, OutOfCone, Obstructed }

    public struct Entry
    {
        public GeckoTongueCandidate Candidate;
        public Status Status;
        public Vector3 BlockPoint;
    }

    public Transform Self;
    public LayerMask ScanMask = ~0;
    public LayerMask EdibleMask;
    public LayerMask ObstacleMask;
    public string[] Tags;
    public bool AcceptDamageables = true;
    /// <summary> Ángulo total del cono de detección alrededor de forward (360 = esfera completa). </summary>
    public float ConeAngle = 360f;
    public float PriorityWeight = 0.5f;
    /// <summary> Ventaja (metros) del objetivo actual, para que la selección no parpadee entre dos casi iguales. </summary>
    public float Stickiness = 0.15f;
    /// <summary> Si está activo, cada Scan deja el detalle en <see cref="Entries"/> (solo para depuración). </summary>
    public bool Record;

    public readonly List<Entry> Entries = new List<Entry>(32);

    private readonly Collider[] _overlaps = new Collider[256];
    private readonly RaycastHit[] _hits = new RaycastHit[16];
    private readonly List<Transform> _seen = new List<Transform>(32);

    /// <summary> El mejor objetivo válido dentro del alcance, o un candidato inválido si no hay ninguno. </summary>
    public GeckoTongueCandidate Scan(Vector3 origin, Vector3 forward, float range, Transform previousTarget)
    {
        Entries.Clear();

        GeckoTongueCandidate best = default;
        float bestScore = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(origin, range, _overlaps,
            ScanMask | EdibleMask, QueryTriggerInteraction.Collide);

        _seen.Clear();

        for (int i = 0; i < count; i++)
        {
            if (!TryResolve(_overlaps[i], origin, null, out GeckoTongueCandidate c)) continue;

            // Un objeto con varios colliders (trigger + físico, por ejemplo) cuenta una sola vez.
            if (_seen.Contains(c.Transform)) continue;
            _seen.Add(c.Transform);

            Vector3 to = c.Point - origin;
            float distance = to.magnitude;
            if (distance > range) continue;

            c.Distance = distance;
            c.Score = distance - c.Priority * PriorityWeight
                      - (previousTarget != null && c.Transform == previousTarget ? Stickiness : 0f);

            Status status = Status.Valid;
            Vector3 blockPoint = default;

            if (ConeAngle < 359f && distance > 0.02f && Vector3.Angle(forward, to) > ConeAngle * 0.5f)
                status = Status.OutOfCone;
            else if (IsPathBlocked(origin, c.Point, c.Collider, c.Transform, out blockPoint))
                status = Status.Obstructed;

            if (Record)
                Entries.Add(new Entry { Candidate = c, Status = status, BlockPoint = blockPoint });

            if (status == Status.Valid && c.Score < bestScore)
            {
                best = c;
                bestScore = c.Score;
            }
        }

        return best;
    }

    /// <summary>
    /// Convierte un collider en objetivo, o devuelve false si no es interactuable. Orden de
    /// resolución: componente propio (IGeckoTongueTarget) -> IGeckoEdible / Collectible /
    /// capa de comida -> GeckoGrapplePoint -> IDamageable -> Reach por tag o por componente.
    /// <paramref name="hitPoint"/> es el punto de un raycast (apuntado con mouse); null = usar
    /// el punto más cercano del collider.
    /// </summary>
    public bool TryResolve(Collider col, Vector3 origin, Vector3? hitPoint, out GeckoTongueCandidate c)
    {
        c = default;
        if (col == null) return false;
        if (Self != null && col.transform.IsChildOf(Self)) return false;

        IGeckoTongueTarget authored = col.GetComponentInParent<IGeckoTongueTarget>();
        if (authored != null && !authored.IsTargetable) return false;

        GeckoTongueInteraction requested = authored != null ? authored.Interaction : GeckoTongueInteraction.Auto;
        bool auto = requested == GeckoTongueInteraction.Auto;

        IGeckoEdible edible = null;
        Collectible collectible = null;
        bool edibleByLayer = false;
        GeckoGrapplePoint grapple = null;
        IDamageable damageable = null;

        if (auto || requested == GeckoTongueInteraction.Collect)
        {
            edible = col.GetComponentInParent<IGeckoEdible>();
            // Ya enganchado o comiéndose: no reinterpretarlo como otra cosa.
            if (edible != null && !edible.CanBeEaten) return false;

            if (edible == null) collectible = col.GetComponentInParent<Collectible>();
            if (edible == null && collectible == null)
                edibleByLayer = (EdibleMask.value & (1 << col.gameObject.layer)) != 0;
        }

        if (auto || requested == GeckoTongueInteraction.Grapple)
            grapple = col.GetComponentInParent<GeckoGrapplePoint>();

        if ((auto || requested == GeckoTongueInteraction.Damage) && AcceptDamageables)
            damageable = col.GetComponentInParent<IDamageable>();

        IGeckoTongueReachable reachable = col.GetComponentInParent<IGeckoTongueReachable>();

        bool isCollect = edible != null || collectible != null || edibleByLayer;
        GeckoTongueInteraction kind;

        if (auto)
        {
            if (isCollect) kind = GeckoTongueInteraction.Collect;
            else if (grapple != null) kind = GeckoTongueInteraction.Grapple;
            else if (damageable != null) kind = GeckoTongueInteraction.Damage;
            else if (authored != null || reachable != null || HasTag(col)) kind = GeckoTongueInteraction.Reach;
            else return false;
        }
        else
        {
            kind = requested;
            if (kind == GeckoTongueInteraction.Collect && !isCollect) return false;
            if (kind == GeckoTongueInteraction.Damage && damageable == null) return false;
        }

        Transform tracked;
        if (edible != null) tracked = edible.Transform;
        else if (collectible != null) tracked = collectible.transform;
        else if (col.attachedRigidbody != null) tracked = col.attachedRigidbody.transform;
        else tracked = grapple != null ? grapple.transform : col.transform;

        Vector3 point;
        if (authored != null && authored.TryGetTonguePoint(out Vector3 authoredPoint))
            point = authoredPoint;
        else if (kind == GeckoTongueInteraction.Collect)
            point = tracked.position;
        else if (kind == GeckoTongueInteraction.Grapple && grapple != null && grapple.SnapToCenter)
            point = grapple.AnchorPosition;
        else
            point = hitPoint ?? ClosestPoint(col, origin);

        c = new GeckoTongueCandidate
        {
            Collider = col,
            Transform = tracked,
            Point = point,
            Kind = kind,
            Edible = edible,
            Collectible = collectible,
            EdibleByLayer = edibleByLayer,
            Grapple = grapple,
            Damageable = damageable,
            Reachable = reachable,
            Priority = authored != null ? authored.Priority : 0f,
            Distance = Vector3.Distance(origin, point),
        };
        return true;
    }

    /// <summary>
    /// ¿Hay geometría entre <paramref name="origin"/> y <paramref name="end"/>? Ignora al propio
    /// gecko y al objetivo. Sirve tanto para validar la elección como para frenar la punta en
    /// pleno vuelo si algo se le cruza.
    /// </summary>
    public bool IsPathBlocked(Vector3 origin, Vector3 end, Collider targetCollider, Transform targetTransform,
                              out Vector3 blockPoint)
    {
        blockPoint = default;

        Vector3 to = end - origin;
        float distance = to.magnitude - 0.05f;
        if (distance <= 0.02f) return false;

        int count = Physics.RaycastNonAlloc(origin, to.normalized, _hits, distance,
            ObstacleMask, QueryTriggerInteraction.Ignore);

        bool blocked = false;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider h = _hits[i].collider;
            if (h == targetCollider) continue;
            if (Self != null && h.transform.IsChildOf(Self)) continue;
            if (targetTransform != null && (h.transform.IsChildOf(targetTransform) || targetTransform.IsChildOf(h.transform))) continue;

            if (_hits[i].distance < nearest)
            {
                nearest = _hits[i].distance;
                blockPoint = _hits[i].point;
                blocked = true;
            }
        }
        return blocked;
    }

    private bool HasTag(Collider col)
    {
        if (Tags == null || Tags.Length == 0) return false;
        // Comparación por string a propósito: CompareTag() loguea un error si el tag no existe
        // en el proyecto, y acá el tag viene del Inspector.
        string tag = col.gameObject.tag;
        for (int i = 0; i < Tags.Length; i++)
            if (!string.IsNullOrEmpty(Tags[i]) && Tags[i] == tag) return true;
        return false;
    }

    private static Vector3 ClosestPoint(Collider col, Vector3 p)
    {
        // Collider.ClosestPoint solo admite colliders convexos; con un MeshCollider cóncavo o un
        // Terrain tira error, así que se cae al bounds.
        if ((col is MeshCollider mesh && !mesh.convex) || col is TerrainCollider)
            return col.bounds.ClosestPoint(p);
        return col.ClosestPoint(p);
    }
}
