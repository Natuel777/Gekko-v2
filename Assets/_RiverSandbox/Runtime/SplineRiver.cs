using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Rio generado sobre una SplineContainer.
//
// Genera en una sola malla (dos submallas):
//   0) la superficie del agua: una cinta plana con ancho por knot. Las UV van "en espacio rio"
//      (uv0 = metros lateral / metros a lo largo) para que el shader Gekko/River Stylized haga
//      correr el agua en la direccion de la curva.
//   1) opcionalmente el cauce (lecho + orillas + margen plano), con MeshCollider, para poder
//      experimentar sin terreno. Si el rio va sobre terreno propio, apagar "Generar cauce".
//
// Interaccion (solo en Play):
//   - Crea volumenes trigger a lo largo del rio (capa Ignore Raycast, para no confundir los
//     raycasts del juego). Todo Rigidbody que entra queda registrado.
//   - Ondas: al cruzar la superficie (salpicadura) y al moverse dentro del agua (estela). Las
//     ondas se mandan al shader como arreglos (MaxRipples), sin texturas ni camaras extra.
//   - Corriente: empuja los Rigidbody a la velocidad del rio. El jugador NO se empuja salvo que
//     se active "Afectar al jugador". Los objetos con RiverFloater ademas flotan.
//
// Consultas: Sample(posicionMundo, out RiverSample) devuelve altura de la superficie, direccion
// y velocidad de la corriente en cualquier punto (para flotadores, particulas, audio, etc).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Gekko/River/Spline River")]
public class SplineRiver : MonoBehaviour
{
    public const int MaxRipples = 16;
    // Metros extra fuera del borde donde el agua todavia "agarra" objetos (flotacion, corriente).
    public const float EdgeTolerance = 0.8f;

    [Serializable]
    public struct RippleSettings
    {
        [Min(0f), Tooltip("Intensidad del anillo (espuma + distorsion).")]
        public float Strength;
        [Min(0.01f), Tooltip("Velocidad de expansion en m/s.")]
        public float Speed;
        [Min(0.05f), Tooltip("Duracion en segundos.")]
        public float Lifetime;
        [Min(0.01f), Tooltip("Grosor del anillo en metros.")]
        public float Width;

        public RippleSettings(float strength, float speed, float lifetime, float width)
        {
            Strength = strength;
            Speed = speed;
            Lifetime = lifetime;
            Width = width;
        }

        public RippleSettings Scaled(float factor)
        {
            return new RippleSettings(Strength * factor, Speed, Lifetime, Width);
        }
    }

    public struct RiverSample
    {
        public bool Inside;
        public float SurfaceY;
        public Vector3 SurfacePoint;
        public Vector3 FlowDirection;
        public float FlowSpeed;
        public Vector3 FlowVelocity => FlowDirection * FlowSpeed;
        public float Lateral01;
        public float HalfWidth;
        public float DistanceAlong;
        public float LateralOffset; // metros desde el centro (+ = derecha)
        public Vector3 Right;
        // Dentro del agua con una tolerancia extra en los bordes (para flotacion / corriente).
        public bool InsideWithMargin(float margin) => Mathf.Abs(LateralOffset) <= HalfWidth + margin;
    }

    struct Section
    {
        public Vector3 Position;
        public Vector3 Right;
        public Vector3 Forward;
        public Vector3 Up;
        public float HalfWidth;
        public float Distance;
        public float SpeedMultiplier;
        public float TurnRadius; // radio de curvatura (infinito en recta)
        public float TurnSign;   // +1 gira a la derecha (lado interior = +Right), -1 a la izquierda

        // Comprime los desplazamientos laterales del lado interior de una curva para que la malla
        // no se pliegue sobre si misma cuando el cauce es mas ancho que el radio de la curva.
        public float CompressOffset(float x)
        {
            if(TurnSign == 0f || Mathf.Sign(x) != TurnSign || float.IsInfinity(TurnRadius))
                return x;

            float limit = TurnRadius * 0.95f;
            float knee = limit * 0.5f;
            float ax = Mathf.Abs(x);
            if(ax <= knee)
                return x;

            float range = limit - knee;
            return Mathf.Sign(x) * (knee + range * (1f - Mathf.Exp(-(ax - knee) / range)));
        }

        public static Section Lerp(Section a, Section b, float t)
        {
            return new Section
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Right = Vector3.Lerp(a.Right, b.Right, t).normalized,
                Forward = Vector3.Lerp(a.Forward, b.Forward, t).normalized,
                Up = Vector3.Lerp(a.Up, b.Up, t).normalized,
                HalfWidth = Mathf.Lerp(a.HalfWidth, b.HalfWidth, t),
                Distance = Mathf.Lerp(a.Distance, b.Distance, t),
                SpeedMultiplier = Mathf.Lerp(a.SpeedMultiplier, b.SpeedMultiplier, t),
                TurnRadius = t < 0.5f ? a.TurnRadius : b.TurnRadius,
                TurnSign = t < 0.5f ? a.TurnSign : b.TurnSign
            };
        }
    }

    struct Ripple
    {
        public Vector3 Position;
        public float Age;
        public RippleSettings Settings;
        public bool Active;
    }

    class TrackedBody
    {
        public int Contacts;
        public Collider Collider;
        public RiverFloater Floater;
        public bool IsPlayer;
        public bool WasSubmerged;
        public float NextWakeTime;
    }

    [Header("Spline")]
    [SerializeField, Tooltip("La spline que define el recorrido. Puede estar en este mismo objeto.")]
    SplineContainer _container;

    [SerializeField, Min(0.05f), Tooltip("Ancho base del agua en metros.")]
    float _width = 4f;

    [SerializeField, Tooltip("Multiplicador de ancho por knot (se sincroniza solo con la cantidad de knots).")]
    List<float> _knotWidthMultipliers = new List<float>();

    [SerializeField, Min(0.1f), Tooltip("Secciones de malla por metro de rio.")]
    float _segmentsPerUnit = 1.5f;

    [SerializeField, Range(2, 32), Tooltip("Subdivisiones a lo ancho del agua.")]
    int _crossSegments = 8;

    [SerializeField, Tooltip("Mantener la superficie horizontal (ignora el giro/roll de la spline).")]
    bool _flatSurface = true;

    [Header("Materiales")]
    [SerializeField] Material _waterMaterial;
    [SerializeField] Material _bedMaterial;

    [Header("Corriente")]
    [SerializeField, Tooltip("Velocidad de la corriente en m/s (visual y fisica).")]
    float _flowSpeed = 1.5f;

    [SerializeField, Tooltip("Donde el rio se angosta, el agua corre mas rapido.")]
    bool _narrowIsFaster = true;

    [SerializeField, Tooltip("Invertir el sentido de la corriente respecto de la spline.")]
    bool _reverseFlow;

    [Header("Cauce")]
    [SerializeField, Tooltip("Generar lecho + orillas (para probar sin terreno).")]
    bool _generateBed = true;

    [SerializeField, Min(0.05f), Tooltip("Profundidad del centro del rio.")]
    float _depth = 1.6f;

    [SerializeField, Min(0f), Tooltip("Ancho de la orilla en pendiente.")]
    float _bankWidth = 2.5f;

    [SerializeField, Min(0f), Tooltip("Altura de la orilla sobre el agua.")]
    float _bankHeight = 0.6f;

    [SerializeField, Min(0f), Tooltip("Margen plano extra a cada lado, a la altura de la orilla.")]
    float _margin = 6f;

    [SerializeField, Tooltip("Agregar MeshCollider al cauce.")]
    bool _bedCollider = true;

    [Header("Interaccion (Play)")]
    [SerializeField] bool _generateTriggers = true;

    [SerializeField, Min(0.5f), Tooltip("Largo de cada volumen trigger.")]
    float _triggerChunkLength = 3f;

    [SerializeField, Min(0f), Tooltip("Altura del volumen por encima del agua.")]
    float _triggerHeightAbove = 0.4f;

    [SerializeField, Tooltip("La corriente arrastra a los Rigidbody que entran.")]
    bool _pushRigidbodies = true;

    [SerializeField, Tooltip("La corriente tambien arrastra al jugador (tag Player).")]
    bool _affectPlayer;

    [SerializeField, Min(0f), Tooltip("Que tan rapido un objeto iguala la velocidad de la corriente.")]
    float _currentForce = 1.5f;

    [SerializeField, Min(0f), Tooltip("Velocidad minima para que entrar/salir del agua salpique.")]
    float _splashMinSpeed = 1.2f;

    [SerializeField, Min(0f), Tooltip("Velocidad relativa al agua minima para dejar estela.")]
    float _wakeMinSpeed = 0.6f;

    [SerializeField, Min(0.02f), Tooltip("Segundos entre ondas de estela.")]
    float _wakeInterval = 0.22f;

    [SerializeField] RippleSettings _splashRipple = new RippleSettings(1.2f, 1.6f, 1.6f, 0.3f);
    [SerializeField] RippleSettings _wakeRipple = new RippleSettings(0.8f, 1.1f, 1.2f, 0.2f);

    // Se dispara en cada salpicadura: posicion en la superficie y fuerza (0..1.5 aprox).
    public event Action<Vector3, float> Splashed;

    static readonly int FlowSpeedId = Shader.PropertyToID("_RiverFlowSpeed");
    static readonly int RipplePosId = Shader.PropertyToID("_RipplePos");
    static readonly int RippleParamsId = Shader.PropertyToID("_RippleParams");
    static readonly int LoopLengthId = Shader.PropertyToID("_RiverLoopLength");
    static readonly List<SplineRiver> s_All = new List<SplineRiver>();

    public static IReadOnlyList<SplineRiver> All => s_All;

    public float FlowSpeed
    {
        get => _flowSpeed;
        set => _flowSpeed = value;
    }

    public float TotalLength => _sections.Count > 0 ? _sections[_sections.Count - 1].Distance : 0f;
    public SplineContainer Container => _container;

    readonly List<Section> _sections = new List<Section>();
    readonly Ripple[] _ripples = new Ripple[MaxRipples];
    readonly Vector4[] _ripplePos = new Vector4[MaxRipples];
    readonly Vector4[] _rippleParams = new Vector4[MaxRipples];
    readonly Dictionary<Rigidbody, TrackedBody> _bodies = new Dictionary<Rigidbody, TrackedBody>();
    readonly List<Rigidbody> _bodyScratch = new List<Rigidbody>();

    Mesh _mesh;
    Mesh _colliderMesh;
    MaterialPropertyBlock _mpb;
    MeshRenderer _renderer;
    GameObject _volumeRoot;
    bool _rebuildRequested;

    Spline MainSpline => _container != null ? _container.Spline : null;

    // ------------------------------------------------------------------ ciclo de vida

    void OnEnable()
    {
        s_All.Add(this);
        _renderer = GetComponent<MeshRenderer>();
        Spline.Changed += OnSplineChanged;
        SyncKnotWidths();
        Rebuild();

        if(Application.isPlaying)
            BuildTriggerVolumes();
    }

    void OnDisable()
    {
        s_All.Remove(this);
        Spline.Changed -= OnSplineChanged;

        DestroyTriggerVolumes();
        CleanupMeshes();
    }

    // Al destruir los volumenes Unity no manda OnTriggerExit, asi que se sueltan a mano.
    void ReleaseBodies()
    {
        foreach(var pair in _bodies)
        {
            if(pair.Key != null && pair.Value.Floater != null)
                pair.Value.Floater.ExitRiver(this);
        }
        _bodies.Clear();
    }

    void OnValidate()
    {
        SyncKnotWidths();
        _rebuildRequested = true;
    }

    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
    {
        if(ReferenceEquals(spline, MainSpline))
        {
            SyncKnotWidths();
            _rebuildRequested = true;
        }
    }

    void Update()
    {
        if(_rebuildRequested)
        {
            Rebuild();
            if(Application.isPlaying)
                BuildTriggerVolumes();
        }

        if(Application.isPlaying)
            AgeRipples(Time.deltaTime);

        PushShaderData();
    }

    void FixedUpdate()
    {
        if(!Application.isPlaying || _bodies.Count == 0)
            return;

        _bodyScratch.Clear();
        _bodyScratch.AddRange(_bodies.Keys);

        foreach(Rigidbody rb in _bodyScratch)
        {
            TrackedBody body = _bodies[rb];
            if(rb == null || body.Collider == null || !body.Collider.enabled || !rb.gameObject.activeInHierarchy)
            {
                if(body.Floater != null)
                    body.Floater.ExitRiver(this);
                _bodies.Remove(rb);
                continue;
            }

            ProcessBody(rb, body);
        }
    }

    // ------------------------------------------------------------------ API publica

    [ContextMenu("Regenerar")]
    public void Rebuild()
    {
        _rebuildRequested = false;
        EnsureMeshes();
        BuildSections();
        BuildMesh();
        ApplyMaterials();
        PushShaderData();
    }

    // Consulta del agua en un punto del mundo. Devuelve false si el rio no tiene malla todavia.
    public bool Sample(Vector3 worldPosition, out RiverSample sample)
    {
        sample = default;
        if(_sections.Count < 2)
            return false;

        Vector3 local = transform.InverseTransformPoint(worldPosition);

        int best = 0;
        float bestDistance = float.MaxValue;
        for(int i = 0; i < _sections.Count; i++)
        {
            Vector3 d = _sections[i].Position - local;
            float distance = d.x * d.x + d.z * d.z + d.y * d.y * 0.25f;
            if(distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        Section section = ProjectOnSegment(local, best);
        float lateral = Vector3.Dot(local - section.Position, section.Right);
        Vector3 surfaceLocal = section.Position + section.Right * lateral;

        sample.HalfWidth = section.HalfWidth * transform.lossyScale.x;
        sample.Inside = Mathf.Abs(lateral) <= section.HalfWidth;
        sample.Lateral01 = section.HalfWidth > 0f ? Mathf.Clamp01(lateral / section.HalfWidth * 0.5f + 0.5f) : 0.5f;
        sample.SurfacePoint = transform.TransformPoint(surfaceLocal);
        sample.SurfaceY = sample.SurfacePoint.y;
        sample.FlowDirection = transform.TransformDirection(section.Forward * FlowSign).normalized;
        sample.FlowSpeed = _flowSpeed * section.SpeedMultiplier;
        sample.DistanceAlong = section.Distance;
        sample.LateralOffset = lateral * transform.lossyScale.x;
        sample.Right = transform.TransformDirection(section.Right).normalized;
        return true;
    }

    // Aceleracion horizontal que devuelve hacia el centro a lo que se acerca al borde (o se paso un
    // poco). Evita que en las curvas los objetos se "escapen" del agua por inercia.
    public static Vector3 CenteringAcceleration(in RiverSample sample, float strength = 4f)
    {
        float margin = Mathf.Min(1f, sample.HalfWidth * 0.35f);
        float edge = Mathf.Abs(sample.LateralOffset) - (sample.HalfWidth - margin);
        if(edge <= 0f)
            return Vector3.zero;

        Vector3 inward = -sample.Right * Mathf.Sign(sample.LateralOffset);
        inward.y = 0f;
        return inward * (edge / Mathf.Max(margin, 0.01f)) * strength;
    }

    // Punto de la superficie a cierta distancia del inicio (metros) y posicion lateral (0 = borde
    // izquierdo, 1 = borde derecho), en mundo.
    public Vector3 PointAt(float distance, float lateral01 = 0.5f)
    {
        if(_sections.Count == 0)
            return transform.position;

        distance = Mathf.Clamp(distance, 0f, TotalLength);
        int index = 1;
        while(index < _sections.Count - 1 && _sections[index].Distance < distance)
            index++;

        Section a = _sections[index - 1];
        Section b = _sections[Mathf.Min(index, _sections.Count - 1)];
        float span = b.Distance - a.Distance;
        Section s = Section.Lerp(a, b, span > 1e-5f ? (distance - a.Distance) / span : 0f);
        float lateral = (Mathf.Clamp01(lateral01) * 2f - 1f) * s.HalfWidth;
        return transform.TransformPoint(s.Position + s.Right * lateral);
    }

    // Agrega una onda en la superficie (posicion en mundo). Reusa la onda mas vieja si no hay lugar.
    public void AddRipple(Vector3 worldPosition, RippleSettings settings)
    {
        int slot = -1;
        float oldest = -1f;
        for(int i = 0; i < MaxRipples; i++)
        {
            if(!_ripples[i].Active)
            {
                slot = i;
                break;
            }

            if(_ripples[i].Age > oldest)
            {
                oldest = _ripples[i].Age;
                slot = i;
            }
        }

        _ripples[slot] = new Ripple
        {
            Position = worldPosition,
            Age = 0f,
            Settings = settings,
            Active = true
        };
    }

    public void AddSplash(Vector3 worldPosition, float strength = 1f)
    {
        AddRipple(worldPosition, _splashRipple.Scaled(strength));
        Splashed?.Invoke(worldPosition, strength);
    }

    // ------------------------------------------------------------------ geometria

    float FlowSign => _reverseFlow ? -1f : 1f;

    void SyncKnotWidths()
    {
        Spline spline = MainSpline;
        if(spline == null)
            return;

        while(_knotWidthMultipliers.Count < spline.Count)
            _knotWidthMultipliers.Add(1f);
        while(_knotWidthMultipliers.Count > spline.Count)
            _knotWidthMultipliers.RemoveAt(_knotWidthMultipliers.Count - 1);
    }

    float WidthMultiplierAt(Spline spline, float t)
    {
        int count = _knotWidthMultipliers.Count;
        if(count == 0)
            return 1f;

        float knot = spline.ConvertIndexUnit(t, PathIndexUnit.Knot);
        // En una spline cerrada t = 1 es el knot 0 otra vez (no el ultimo): sin esto el ancho
        // pegaba un salto justo en la union del loop.
        if(spline.Closed)
            knot = Mathf.Repeat(knot, count);
        int a = Mathf.Clamp(Mathf.FloorToInt(knot), 0, count - 1);
        int b = spline.Closed ? (a + 1) % count : Mathf.Min(a + 1, count - 1);
        float f = Mathf.SmoothStep(0f, 1f, knot - Mathf.Floor(knot));
        return Mathf.Max(0.05f, Mathf.Lerp(_knotWidthMultipliers[a], _knotWidthMultipliers[b], f));
    }

    void BuildSections()
    {
        _sections.Clear();
        Spline spline = MainSpline;
        if(spline == null || spline.Count < 2)
            return;

        Transform splineSpace = _container.transform;
        float length = spline.GetLength();
        int segments = Mathf.Max(2, Mathf.CeilToInt(length * _segmentsPerUnit));

        Vector3 previousForward = Vector3.forward;
        Vector3 previousPosition = Vector3.zero;
        float distance = 0f;

        for(int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            spline.Evaluate(t, out float3 p, out float3 tan, out float3 up);

            Vector3 position = transform.InverseTransformPoint(splineSpace.TransformPoint(p));
            Vector3 forward = transform.InverseTransformDirection(splineSpace.TransformDirection(tan));
            Vector3 upVector = _flatSurface
                ? Vector3.up
                : transform.InverseTransformDirection(splineSpace.TransformDirection(up)).normalized;

            if(_flatSurface)
                forward.y = 0f;
            if(forward.sqrMagnitude < 1e-6f)
                forward = previousForward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(upVector, forward).normalized;
            if(!_flatSurface)
                upVector = Vector3.Cross(forward, right).normalized;

            if(i > 0)
                distance += Vector3.Distance(position, previousPosition);

            float widthMultiplier = WidthMultiplierAt(spline, t);
            _sections.Add(new Section
            {
                Position = position,
                Right = right,
                Forward = forward,
                Up = upVector,
                HalfWidth = _width * 0.5f * widthMultiplier,
                Distance = distance,
                SpeedMultiplier = _narrowIsFaster ? Mathf.Clamp(1f / widthMultiplier, 0.35f, 3f) : 1f
            });

            previousForward = forward;
            previousPosition = position;
        }

        ComputeCurvature();
    }

    // Radio de curvatura por seccion (minimo en una ventana de +-2 secciones, para no subestimar
    // curvas cerradas por ruido de muestreo).
    void ComputeCurvature()
    {
        int count = _sections.Count;
        var radius = new float[count];
        var sign = new float[count];
        for(int i = 0; i < count; i++)
        {
            int a = Mathf.Max(0, i - 1);
            int b = Mathf.Min(count - 1, i + 1);
            float angle = Vector3.SignedAngle(_sections[a].Forward, _sections[b].Forward, _sections[i].Up) * Mathf.Deg2Rad;
            float length = _sections[b].Distance - _sections[a].Distance;
            radius[i] = Mathf.Abs(angle) > 1e-4f && length > 1e-4f ? length / Mathf.Abs(angle) : float.PositiveInfinity;
            sign[i] = Mathf.Abs(angle) > 1e-4f ? Mathf.Sign(angle) : 0f;
        }

        for(int i = 0; i < count; i++)
        {
            float best = radius[i];
            for(int k = Mathf.Max(0, i - 2); k <= Mathf.Min(count - 1, i + 2); k++)
            {
                if(sign[k] == sign[i])
                    best = Mathf.Min(best, radius[k]);
            }

            Section s = _sections[i];
            s.TurnRadius = best;
            s.TurnSign = sign[i];
            _sections[i] = s;
        }
    }

    // Perfil del cauce: u en [-3,3]. |u|<=1 lecho (x = u*mitadAncho), 1..2 orilla, 2..3 margen.
    List<float> BuildBedProfile()
    {
        var profile = new List<float>();
        int channelSteps = _crossSegments;
        const int bankSteps = 4;
        bool hasMargin = _margin > 0.001f;

        if(hasMargin)
            profile.Add(-3f);
        for(int i = 0; i < bankSteps; i++)
            profile.Add(-2f + i / (float)bankSteps);
        for(int i = 0; i < channelSteps * 2; i++)
            profile.Add(-1f + i / (float)channelSteps);
        for(int i = 0; i <= bankSteps; i++)
            profile.Add(1f + i / (float)bankSteps);
        if(hasMargin)
            profile.Add(3f);

        return profile;
    }

    void EvaluateBed(float u, float halfWidth, out float x, out float y, out float gradient)
    {
        float au = Mathf.Abs(u);
        float sign = Mathf.Sign(u);
        float lip = Mathf.Min(0.12f, _bankHeight * 0.5f);

        if(au <= 1f)
        {
            x = au * halfWidth;
            y = -_depth + (_depth + lip) * Mathf.Pow(au, 2.2f);
        }
        else if(au <= 2f)
        {
            x = halfWidth + (au - 1f) * _bankWidth;
            y = lip + (_bankHeight - lip) * Mathf.SmoothStep(0f, 1f, au - 1f);
        }
        else
        {
            x = halfWidth + _bankWidth + (au - 2f) * _margin;
            y = _bankHeight;
        }

        x *= sign;

        // v del gradiente del material del cauce: 0 = fondo, 0.5 = linea de agua, 1 = orilla alta.
        gradient = y < 0f
            ? 0.5f * (1f + y / _depth)
            : 0.5f + 0.5f * (_bankHeight > 0.001f ? Mathf.Clamp01(y / _bankHeight) : 1f);
    }

    void BuildMesh()
    {
        _mesh.Clear();
        if(_colliderMesh != null)
            _colliderMesh.Clear();

        if(_sections.Count < 2)
            return;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        var waterTriangles = new List<int>();
        var bedTriangles = new List<int>();

        // --- agua
        int columns = _crossSegments + 1;
        for(int i = 0; i < _sections.Count; i++)
        {
            Section s = _sections[i];
            for(int j = 0; j < columns; j++)
            {
                float a = j / (float)_crossSegments;
                float offset = (a * 2f - 1f) * s.HalfWidth;
                vertices.Add(s.Position + s.Right * s.CompressOffset(offset));
                normals.Add(s.Up);
                tangents.Add(new Vector4(s.Forward.x, s.Forward.y, s.Forward.z, 1f));
                uv0.Add(new Vector2(offset, s.Distance));
                uv1.Add(new Vector2(a, s.SpeedMultiplier));
            }
        }
        AddGridTriangles(waterTriangles, 0, _sections.Count, columns);

        // --- cauce
        int bedStart = vertices.Count;
        int bedColumns = 0;
        if(_generateBed)
        {
            List<float> profile = BuildBedProfile();
            bedColumns = profile.Count;
            for(int i = 0; i < _sections.Count; i++)
            {
                Section s = _sections[i];
                foreach(float u in profile)
                {
                    EvaluateBed(u, s.HalfWidth, out float x, out float y, out float gradient);
                    vertices.Add(s.Position + s.Right * s.CompressOffset(x) + s.Up * y);
                    normals.Add(s.Up);
                    tangents.Add(new Vector4(s.Forward.x, s.Forward.y, s.Forward.z, 1f));
                    uv0.Add(new Vector2(0.5f, gradient));
                    uv1.Add(new Vector2(0f, 1f));
                }
            }
            AddGridTriangles(bedTriangles, bedStart, _sections.Count, bedColumns);

            // Tapas en los extremos (rio abierto): un abanico desde un punto bajo el lecho, para que
            // no se vea el corte del cauce ni "vacio" debajo del agua en las puntas.
            if(MainSpline != null && !MainSpline.Closed)
            {
                for(int end = 0; end < 2; end++)
                {
                    Section s = end == 0 ? _sections[0] : _sections[_sections.Count - 1];
                    Vector3 facing = end == 0 ? -s.Forward : s.Forward;
                    int capStart = vertices.Count;

                    vertices.Add(s.Position + s.Up * (-_depth - 0.6f));
                    foreach(float u in profile)
                    {
                        EvaluateBed(u, s.HalfWidth, out float x, out float y, out _);
                        vertices.Add(s.Position + s.Right * s.CompressOffset(x) + s.Up * y);
                    }

                    for(int k = capStart; k < vertices.Count; k++)
                    {
                        normals.Add(facing);
                        tangents.Add(new Vector4(s.Right.x, s.Right.y, s.Right.z, 1f));
                        uv0.Add(new Vector2(0.5f, 0.25f));
                        uv1.Add(new Vector2(0f, 1f));
                    }

                    for(int k = 0; k < profile.Count - 1; k++)
                    {
                        int a = capStart + 1 + k;
                        int b = a + 1;
                        bedTriangles.Add(capStart);
                        bedTriangles.Add(end == 0 ? a : b);
                        bedTriangles.Add(end == 0 ? b : a);
                    }
                }
            }
        }

        _mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(vertices);
        _mesh.SetNormals(normals);
        _mesh.SetTangents(tangents);
        _mesh.SetUVs(0, uv0);
        _mesh.SetUVs(1, uv1);
        _mesh.subMeshCount = _generateBed ? 2 : 1;
        _mesh.SetTriangles(waterTriangles, 0);
        if(_generateBed)
        {
            _mesh.SetTriangles(bedTriangles, 1);
            RecalculateBedNormals(vertices, normals, bedStart, bedTriangles);
            _mesh.SetNormals(normals);
        }
        _mesh.RecalculateBounds();

        // --- collider del cauce
        var meshCollider = GetComponent<MeshCollider>();
        if(_generateBed && _bedCollider)
        {
            if(meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();

            _colliderMesh.indexFormat = _mesh.indexFormat;
            _colliderMesh.SetVertices(vertices);
            _colliderMesh.SetTriangles(bedTriangles, 0);
            _colliderMesh.RecalculateBounds();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = _colliderMesh;
        }
        else if(meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }
    }

    // Normales solo del cauce (las del agua quedan en Up, que es lo que necesita el shader).
    static void RecalculateBedNormals(List<Vector3> positions, List<Vector3> normals, int start, List<int> triangles)
    {
        var accumulated = new Vector3[normals.Count - start];

        for(int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            Vector3 n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            accumulated[a - start] += n;
            accumulated[b - start] += n;
            accumulated[c - start] += n;
        }

        for(int i = 0; i < accumulated.Length; i++)
        {
            if(accumulated[i].sqrMagnitude > 1e-10f)
                normals[start + i] = accumulated[i].normalized;
        }
    }

    static void AddGridTriangles(List<int> triangles, int start, int rows, int columns)
    {
        for(int i = 0; i < rows - 1; i++)
        {
            for(int j = 0; j < columns - 1; j++)
            {
                int v00 = start + i * columns + j;
                int v01 = v00 + 1;
                int v10 = v00 + columns;
                int v11 = v10 + 1;
                triangles.Add(v00);
                triangles.Add(v10);
                triangles.Add(v01);
                triangles.Add(v01);
                triangles.Add(v10);
                triangles.Add(v11);
            }
        }
    }

    Section ProjectOnSegment(Vector3 local, int index)
    {
        Section best = _sections[index];
        float bestDistance = float.MaxValue;

        for(int k = -1; k <= 0; k++)
        {
            int a = index + k;
            int b = a + 1;
            if(a < 0 || b >= _sections.Count)
                continue;

            Vector3 pa = _sections[a].Position;
            Vector3 ab = _sections[b].Position - pa;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(local - pa, ab) / lengthSq) : 0f;
            Section candidate = Section.Lerp(_sections[a], _sections[b], t);
            Vector3 along = Vector3.Project(local - candidate.Position, candidate.Forward);
            float distance = along.sqrMagnitude;
            if(distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    void EnsureMeshes()
    {
        if(_mesh == null)
        {
            _mesh = new Mesh { name = "<Spline River>" };
            _mesh.hideFlags = HideFlags.HideAndDontSave;
            _mesh.MarkDynamic();
        }

        if(_colliderMesh == null)
        {
            _colliderMesh = new Mesh { name = "<Spline River Bed Collider>" };
            _colliderMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        var filter = GetComponent<MeshFilter>();
        filter.hideFlags = HideFlags.NotEditable;
        filter.sharedMesh = _mesh;
    }

    void CleanupMeshes()
    {
        var filter = GetComponent<MeshFilter>();
        if(filter != null)
        {
            filter.hideFlags = HideFlags.None;
            filter.sharedMesh = null;
        }

        var meshCollider = GetComponent<MeshCollider>();
        if(meshCollider != null)
            meshCollider.sharedMesh = null;

        if(_mesh != null)
            DestroyImmediate(_mesh);
        if(_colliderMesh != null)
            DestroyImmediate(_colliderMesh);
        _mesh = null;
        _colliderMesh = null;
    }

    void ApplyMaterials()
    {
        if(_renderer == null)
            _renderer = GetComponent<MeshRenderer>();

        Material[] materials = _generateBed
            ? new[] { _waterMaterial, _bedMaterial }
            : new[] { _waterMaterial };

        Material[] current = _renderer.sharedMaterials;
        bool same = current.Length == materials.Length;
        for(int i = 0; same && i < materials.Length; i++)
            same = current[i] == materials[i];

        if(!same)
            _renderer.sharedMaterials = materials;
    }

    // ------------------------------------------------------------------ ondas

    void AgeRipples(float deltaTime)
    {
        for(int i = 0; i < MaxRipples; i++)
        {
            if(!_ripples[i].Active)
                continue;

            _ripples[i].Age += deltaTime;
            if(_ripples[i].Age >= _ripples[i].Settings.Lifetime)
                _ripples[i].Active = false;
        }
    }

    void PushShaderData()
    {
        if(_renderer == null)
            _renderer = GetComponent<MeshRenderer>();
        if(_mpb == null)
            _mpb = new MaterialPropertyBlock();

        for(int i = 0; i < MaxRipples; i++)
        {
            Ripple r = _ripples[i];
            if(r.Active)
            {
                _ripplePos[i] = new Vector4(r.Position.x, r.Position.y, r.Position.z, r.Age);
                _rippleParams[i] = new Vector4(r.Settings.Strength, r.Settings.Speed, r.Settings.Lifetime, r.Settings.Width);
            }
            else
            {
                _ripplePos[i] = Vector4.zero;
                _rippleParams[i] = new Vector4(0f, 1f, 1f, 1f);
            }
        }

        _renderer.GetPropertyBlock(_mpb, 0);
        _mpb.SetFloat(FlowSpeedId, _flowSpeed * FlowSign);
        // Largo del loop (0 si el rio es abierto): el shader hace el ruido periodico para que no se
        // vea costura donde la distancia vuelve de TotalLength a 0.
        _mpb.SetFloat(LoopLengthId, MainSpline != null && MainSpline.Closed ? TotalLength : 0f);
        _mpb.SetVectorArray(RipplePosId, _ripplePos);
        _mpb.SetVectorArray(RippleParamsId, _rippleParams);
        _renderer.SetPropertyBlock(_mpb, 0);
    }

    // ------------------------------------------------------------------ interaccion

    void BuildTriggerVolumes()
    {
        DestroyTriggerVolumes();
        if(!_generateTriggers || _sections.Count < 2)
            return;

        _volumeRoot = new GameObject("RiverVolume (runtime)");
        _volumeRoot.layer = 2; // Ignore Raycast: no interfiere con los raycasts del juego
        _volumeRoot.transform.SetParent(transform, false);

        var body = _volumeRoot.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var relay = _volumeRoot.AddComponent<RiverVolume>();
        relay.River = this;

        float bottom = _generateBed ? _depth : 2f;
        int start = 0;
        for(int i = 1; i < _sections.Count; i++)
        {
            bool last = i == _sections.Count - 1;
            if(_sections[i].Distance - _sections[start].Distance < _triggerChunkLength && !last)
                continue;

            Section a = _sections[start];
            Section b = _sections[i];
            Vector3 along = b.Position - a.Position;
            float length = along.magnitude;
            if(length < 0.01f)
                continue;

            float halfWidth = 0f;
            for(int k = start; k <= i; k++)
                halfWidth = Mathf.Max(halfWidth, _sections[k].HalfWidth);

            var chunk = new GameObject("Chunk " + start);
            chunk.layer = 2;
            chunk.transform.SetParent(_volumeRoot.transform, false);
            Vector3 flatAlong = along;
            if(_flatSurface)
                flatAlong.y = 0f;
            chunk.transform.localPosition = (a.Position + b.Position) * 0.5f;
            chunk.transform.localRotation = Quaternion.LookRotation(flatAlong.sqrMagnitude > 1e-6f ? flatAlong : a.Forward, a.Up);

            var box = chunk.AddComponent<BoxCollider>();
            box.isTrigger = true;
            float height = bottom + _triggerHeightAbove + Mathf.Abs(b.Position.y - a.Position.y);
            box.size = new Vector3((halfWidth + EdgeTolerance) * 2f, height, length + 0.6f);
            box.center = new Vector3(0f, (_triggerHeightAbove - bottom) * 0.5f, 0f);

            start = i;
        }
    }

    void DestroyTriggerVolumes()
    {
        ReleaseBodies();
        if(_volumeRoot == null)
            return;

        if(Application.isPlaying)
            Destroy(_volumeRoot);
        else
            DestroyImmediate(_volumeRoot);
        _volumeRoot = null;
    }

    internal void HandleVolumeEnter(Collider other)
    {
        if(other.isTrigger)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if(rb == null)
            return;

        if(!_bodies.TryGetValue(rb, out TrackedBody body))
        {
            body = new TrackedBody
            {
                Collider = other,
                Floater = rb.GetComponent<RiverFloater>(),
                IsPlayer = rb.CompareTag("Player") || rb.GetComponentInParent<Player>() != null
            };
            _bodies.Add(rb, body);

            if(body.Floater != null)
                body.Floater.EnterRiver(this);
        }

        body.Contacts++;
    }

    internal void HandleVolumeExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if(rb == null || other.isTrigger || !_bodies.TryGetValue(rb, out TrackedBody body))
            return;

        body.Contacts--;
        if(body.Contacts > 0)
            return;

        if(body.Floater != null)
            body.Floater.ExitRiver(this);
        _bodies.Remove(rb);
    }

    void ProcessBody(Rigidbody rb, TrackedBody body)
    {
        if(!Sample(rb.worldCenterOfMass, out RiverSample sample) || !sample.InsideWithMargin(EdgeTolerance))
        {
            body.WasSubmerged = false;
            return;
        }

        Bounds bounds = body.Collider.bounds;
        bool submerged = bounds.min.y < sample.SurfaceY;
        bool touchesSurface = submerged && bounds.max.y > sample.SurfaceY - 0.25f;
        Vector3 velocity = rb.linearVelocity;
        Vector3 surfacePoint = new Vector3(rb.worldCenterOfMass.x, sample.SurfaceY, rb.worldCenterOfMass.z);

        // Salpicadura al entrar / salir del agua.
        if(submerged != body.WasSubmerged)
        {
            float verticalSpeed = Mathf.Abs(velocity.y);
            if(verticalSpeed >= _splashMinSpeed)
            {
                float strength = Mathf.Clamp(verticalSpeed / 6f, 0.35f, 1.5f) * (submerged ? 1f : 0.6f);
                AddSplash(surfacePoint, strength);
                body.NextWakeTime = Time.time + _wakeInterval;
            }
            body.WasSubmerged = submerged;
        }

        if(!submerged)
            return;

        // Estela: velocidad relativa al agua, solo si el objeto corta la superficie.
        Vector3 relative = velocity - sample.FlowVelocity;
        relative.y = 0f;
        float relativeSpeed = relative.magnitude;
        if(touchesSurface && relativeSpeed >= _wakeMinSpeed && Time.time >= body.NextWakeTime)
        {
            float strength = Mathf.Clamp(relativeSpeed / 4f, 0.3f, 1.2f);
            AddRipple(surfacePoint, _wakeRipple.Scaled(strength));
            body.NextWakeTime = Time.time + _wakeInterval;
        }

        // Corriente (los RiverFloater se manejan solos).
        if(_pushRigidbodies && body.Floater == null && !rb.isKinematic && (!body.IsPlayer || _affectPlayer))
        {
            Vector3 push = sample.FlowVelocity - velocity;
            push.y = 0f;
            rb.AddForce(push * _currentForce + CenteringAcceleration(sample), ForceMode.Acceleration);
        }
    }

    // ------------------------------------------------------------------ gizmos

    void OnDrawGizmosSelected()
    {
        if(_sections.Count < 2)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        int step = Mathf.Max(1, Mathf.RoundToInt(_segmentsPerUnit * 3f));
        for(int i = 0; i < _sections.Count; i += step)
        {
            Section s = _sections[i];
            Vector3 dir = s.Forward * FlowSign * Mathf.Max(0.5f, s.SpeedMultiplier);
            Vector3 tip = s.Position + Vector3.up * 0.05f + dir;
            Gizmos.DrawLine(s.Position + Vector3.up * 0.05f, tip);
            Gizmos.DrawLine(tip, tip - dir * 0.3f + s.Right * 0.2f);
            Gizmos.DrawLine(tip, tip - dir * 0.3f - s.Right * 0.2f);
        }
    }
}
