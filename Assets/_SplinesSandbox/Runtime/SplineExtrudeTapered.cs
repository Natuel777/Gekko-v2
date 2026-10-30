using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Copia de UnityEngine.Splines.SplineExtrude (com.unity.splines) pensada para la demo de
// enredaderas (Demo_MallaEnredadera / Demo_MallaEnredadera2 en SplinesTest.unity), con dos
// agregados que el componente original no tiene:
//
// 1) Radio por knot: m_KnotRadiusMultipliers tiene un multiplicador de radio por cada knot de
//    la spline principal. Agrandar un knot solo reinterpola el tramo de malla hacia el knot
//    vecino; el radio propio del knot siguiente no cambia. La lista se mantiene sincronizada
//    con la cantidad de knots automaticamente (ver SyncKnotRadii).
// 2) Punta en pico: m_TaperEnds controla en que extremo(s) el radio baja a 0 (gradiente suave,
//    ver TaperedKnotCircleShape). Por defecto aplica solo en "Final" (un solo lado); para
//    aplicarlo en ambos extremos hay que elegir "Ambos" en el Inspector.
//
// El pipeline de mallas de bajo nivel de SplineMesh (ExtrudeRing, WindTris, etc.) es interno al
// paquete, asi que esto usa unicamente la API publica: un IExtrudeShape custom
// (TaperedKnotCircleShape) + SplineMesh.Extrude por spline, combinados con Mesh.CombineMeshes.
// A cambio de eso, se dejaron afuera del original cosas que esta demo no necesita: asignar un
// Mesh externo (targetMesh) y la sincronizacion automatica de colliders.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Splines/Spline Extrude (Tapered)")]
[ExecuteAlways]
public class SplineExtrudeTapered : MonoBehaviour
{
    public enum TaperEnds
    {
        Ninguno,
        Inicio,
        Final,
        Ambos
    }

    [SerializeField, Tooltip("La Spline a extruir.")]
    SplineContainer m_Container;

    [SerializeField, Tooltip("Regenerar la malla cuando la spline cambia.")]
    bool m_RebuildOnSplineChange = true;

    [SerializeField, Tooltip("Cantidad maxima de rebuilds por segundo.")]
    int m_RebuildFrequency = 30;

    [SerializeField, Min(3), Tooltip("Cantidad de lados del circulo que forma el tubo.")]
    int m_Sides = 8;

    [SerializeField, Min(0.0001f), Tooltip("Cantidad de anillos de malla por unidad de longitud.")]
    float m_SegmentsPerUnit = 4;

    [SerializeField, Tooltip("Tapar los extremos del tubo con una cara plana (se ignora donde el radio termina en punta).")]
    bool m_Capped = true;

    [SerializeField, Min(0.00001f), Tooltip("Radio base del tubo. Los multiplicadores por knot se aplican sobre este valor.")]
    float m_Radius = .25f;

    [SerializeField, Tooltip("Seccion de la spline a extruir (0-1).")]
    Vector2 m_Range = new Vector2(0f, 1f);

    [SerializeField, Tooltip("Invertir el sentido de las normales.")]
    bool m_FlipNormals;

    [Header("Radio por knot")]
    [SerializeField, Tooltip("Multiplicador de radio por cada knot de la spline principal (1 = radio base). " +
         "Se sincroniza solo con la cantidad de knots; agrandar un knot no afecta al siguiente.")]
    List<float> m_KnotRadiusMultipliers = new List<float>();

    [Header("Punta en pico")]
    [SerializeField, Tooltip("En que extremo(s) la malla termina en punta.")]
    TaperEnds m_TaperEnds = TaperEnds.Final;

    [SerializeField, Range(0.001f, 0.5f), Tooltip("Fraccion de la spline (0-1) sobre la que se angosta hasta terminar en punta.")]
    float m_TaperLength = 0.12f;

    Mesh m_Mesh;
    float m_NextScheduledRebuild;
    bool m_RebuildRequested;

    readonly TaperedKnotCircleShape m_Shape = new TaperedKnotCircleShape();
    readonly List<CombineInstance> m_CombineBuffer = new List<CombineInstance>();
    readonly List<Mesh> m_TempMeshBuffer = new List<Mesh>();

    public SplineContainer Container
    {
        get => m_Container;
        set => m_Container = value;
    }

    public float Radius
    {
        get => m_Radius;
        set => m_Radius = Mathf.Max(value, .00001f);
    }

    /// <summary>Multiplicador de radio por knot de la spline principal. Ver SyncKnotRadii.</summary>
    public IList<float> KnotRadiusMultipliers => m_KnotRadiusMultipliers;

    public TaperEnds TaperedEnds
    {
        get => m_TaperEnds;
        set => m_TaperEnds = value;
    }

    Spline MainSpline => m_Container != null ? m_Container.Spline : null;

    void Reset()
    {
        TryGetComponent(out m_Container);

        if(TryGetComponent<MeshRenderer>(out var renderer) && renderer.sharedMaterial == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = cube.GetComponent<MeshRenderer>().sharedMaterial;
            DestroyImmediate(cube);
            renderer.sharedMaterial = mat;
        }

        SyncKnotRadii();
        Rebuild();
    }

    internal static readonly string k_EmptyContainerError = "Spline Extrude Tapered no tiene un SplineContainer valido asignado.";
    bool IsNullOrEmptyContainer()
    {
        return m_Container == null || m_Container.Spline == null || m_Container.Splines.Count == 0;
    }

    void OnEnable()
    {
        EnsureMeshExists();
        Spline.Changed += OnSplineChanged;

        if(!IsNullOrEmptyContainer())
        {
            SyncKnotRadii();
            Rebuild();
        }
    }

    void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
        CleanupMesh();
    }

    void OnSplineChanged(Spline spline, int knotIndex, SplineModification modificationType)
    {
        if(!m_RebuildOnSplineChange)
            return;

        if(m_Container != null && IsInMainContainer(spline))
            m_RebuildRequested = true;
    }

    void Update()
    {
        if(m_RebuildRequested && Time.time >= m_NextScheduledRebuild)
            Rebuild();
    }

    bool IsInMainContainer(Spline spline)
    {
        var splines = m_Container.Splines;
        for(int i = 0; i < splines.Count; i++)
        {
            if(ReferenceEquals(splines[i], spline))
                return true;
        }
        return false;
    }

    void EnsureMeshExists()
    {
        if(m_Mesh == null)
        {
            m_Mesh = new Mesh { name = "<Spline Extruded Mesh (Tapered)>" };
            m_Mesh.hideFlags = HideFlags.HideAndDontSave;
        }

        if(TryGetComponent<MeshFilter>(out var filter))
        {
            filter.hideFlags = HideFlags.NotEditable;
            filter.sharedMesh = m_Mesh;
        }
    }

    void CleanupMesh()
    {
        if(TryGetComponent<MeshFilter>(out var filter))
        {
            filter.hideFlags = HideFlags.None;
            filter.sharedMesh = null;
        }

        if(m_Mesh != null)
        {
            DestroyImmediate(m_Mesh);
            m_Mesh = null;
        }
    }

    // Mantiene m_KnotRadiusMultipliers del mismo largo que la cantidad de knots de la spline
    // principal. Los knots nuevos entran con multiplicador 1 (sin cambio de radio); los valores
    // ya tocados a mano se conservan mientras no cambie el orden/cantidad de knots.
    void SyncKnotRadii()
    {
        if(MainSpline == null)
            return;

        int knotCount = MainSpline.Count;

        while(m_KnotRadiusMultipliers.Count < knotCount)
            m_KnotRadiusMultipliers.Add(1f);

        if(m_KnotRadiusMultipliers.Count > knotCount)
            m_KnotRadiusMultipliers.RemoveRange(knotCount, m_KnotRadiusMultipliers.Count - knotCount);
    }

    /// <summary>Reconstruye la malla extruida de todas las splines del contenedor.</summary>
    public void Rebuild()
    {
        if(m_Mesh == null)
            return;

        if(IsNullOrEmptyContainer())
        {
            if(Application.isPlaying)
                Debug.LogError(k_EmptyContainerError, this);
            return;
        }

        SyncKnotRadii();

        m_Mesh.Clear();

        if(m_Range.x == m_Range.y)
            return;

        m_Shape.SideCount = m_Sides;
        float span = Mathf.Abs(m_Range.y - m_Range.x);

        bool taperStart = m_TaperEnds == TaperEnds.Inicio || m_TaperEnds == TaperEnds.Ambos;
        bool taperEnd = m_TaperEnds == TaperEnds.Final || m_TaperEnds == TaperEnds.Ambos;

        m_CombineBuffer.Clear();
        m_TempMeshBuffer.Clear();

        foreach(var spline in m_Container.Splines)
        {
            if(spline.Count < 2)
                continue;

            bool isMainSpline = ReferenceEquals(spline, MainSpline);

            // El radio por knot y la punta en pico solo se calcularon para la spline principal;
            // cualquier spline extra del contenedor se extruye con radio uniforme.
            m_Shape.Configure(
                isMainSpline ? m_KnotRadiusMultipliers : null,
                isMainSpline && taperStart,
                isMainSpline && taperEnd,
                m_TaperLength);

            int segmentCount = Mathf.Max(Mathf.CeilToInt(spline.GetLength() * span * m_SegmentsPerUnit), 2);

            var settings = new ExtrudeSettings<TaperedKnotCircleShape>(m_Shape)
            {
                Radius = m_Radius,
                CapEnds = m_Capped,
                Range = new float2(m_Range.x, m_Range.y),
                SegmentCount = segmentCount,
                FlipNormals = m_FlipNormals
            };

            var subMesh = new Mesh();
            if(SplineMesh.Extrude(spline, subMesh, settings))
            {
                m_CombineBuffer.Add(new CombineInstance { mesh = subMesh, transform = Matrix4x4.identity });
                m_TempMeshBuffer.Add(subMesh);
            }
            else
            {
                DestroyImmediate(subMesh);
            }
        }

        if(m_CombineBuffer.Count > 0)
            m_Mesh.CombineMeshes(m_CombineBuffer.ToArray(), true, true);

        foreach(var tempMesh in m_TempMeshBuffer)
            DestroyImmediate(tempMesh);

        m_Mesh.RecalculateBounds();
        m_NextScheduledRebuild = Time.time + 1f / Mathf.Max(m_RebuildFrequency, 1);
        m_RebuildRequested = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if(!UnityEditor.EditorApplication.isPlaying)
            Rebuild();
    }
#endif
}
