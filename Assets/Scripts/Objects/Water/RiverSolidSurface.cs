using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Superficie sólida de un SplineRiver, para que Gekko flote sobre el agua.
//
// El agua que dibuja SplineRiver es solo visual, no tiene física. En LvlOne Gekko "flota" porque hay colliders sólidos en la
// layer Water puestos a mano debajo (lakeCol, pushingWater1/2/3: ver GekkoWater). Esos colliders no siguen al río: cuando el río
// cambia de forma o de altura (en LvlTwo el agua vieja se reemplazó por SplineRivers) quedan desalineados y Gekko flota por
// encima del agua, hundido en ella, o directamente no flota.
//
// Este componente arma el collider a partir del propio río, así que siempre coincide con lo que se ve:
//   - Una cinta con el ancho y la altura reales del agua (se muestrea con SplineRiver.PointAt), un poco por debajo de la
//     superficie visible (_surfaceOffset), con espesor (_thickness) para que no se atraviese al caer rápido. Acompaña también
//     las cascadas.
//   - En la layer Water, como los demás colliders de agua: Gekko pierde fricción, suena el chapuzón y no puede engancharse ni
//     hacer swing (ver GekkoWater).
//   - Con RiverSurfacePlatform: resbala (SlipperyPlatform) y la corriente lo arrastra en el sentido real del río.
// Va en el mismo objeto que el SplineRiver. Se arma al empezar el Play (en el Editor no crea nada). Los colliders de agua
// puestos a mano debajo del mismo río hay que apagarlos: Gekko se apoyaría en el más alto.
[DisallowMultipleComponent]
[RequireComponent(typeof(SplineRiver))]
public class RiverSolidSurface : MonoBehaviour
{
    [Tooltip("Metros, respecto de la superficie visible, a los que queda la cara de arriba del collider (negativo = más abajo). Gekko apoya los pies ahí.")]
    [SerializeField] private float _surfaceOffset = -0.1f;

    [Tooltip("Espesor del collider hacia abajo, en metros. Con más espesor es más difícil atravesarlo al caer a mucha velocidad.")]
    [Min(0.1f)]
    [SerializeField] private float _thickness = 1f;

    [Tooltip("Cada cuántos metros del río se toma una sección de la cinta. Más chico sigue mejor las curvas y los labios de cascada, con más triángulos.")]
    [Min(0.25f)]
    [SerializeField] private float _sectionLength = 1.5f;

    [Tooltip("Aceleración (m/s²) con la que la corriente arrastra a Gekko sobre el agua, en el sentido del río. 0 = solo resbala, sin arrastrarlo. Los pushingWater de LvlOne usan 150-200.")]
    [Min(0f)]
    [SerializeField] private float _currentForce = 150f;

    private SplineRiver _river;
    private GameObject _surface;
    private Mesh _mesh;

    private IEnumerator Start()
    {
        _river = GetComponent<SplineRiver>();

        // El río arma sus secciones en su OnEnable. Si todavía no las tiene (la spline estaba sin knots o se está regenerando)
        // se espera unos frames antes de rendirse.
        for(int i = 0; i < 5 && _river.TotalLength <= 0f; i++)
            yield return null;

        Build();
    }

    private void OnDestroy()
    {
        if(_mesh != null) Destroy(_mesh);
    }

    [ContextMenu("Reconstruir (Play Mode)")]
    private void Build()
    {
        if(!Application.isPlaying || _river == null) return;

        float length = _river.TotalLength;

        if(length <= 0f)
        {
            Debug.LogWarning($"[{name}] RiverSolidSurface: el río no tiene largo, no se arma la superficie.", this);
            return;
        }

        // Dos puntos por sección, uno en cada borde del agua.
        int sections = Mathf.Max(2, Mathf.CeilToInt(length / _sectionLength) + 1);
        var left = new Vector3[sections];
        var right = new Vector3[sections];

        for(int i = 0; i < sections; i++)
        {
            float distance = length * i / (sections - 1);
            left[i] = _river.PointAt(distance, 0f);
            right[i] = _river.PointAt(distance, 1f);
        }

        BuildSlab(left, right, _surfaceOffset, _thickness, out Vector3[] vertices, out int[] triangles);

        // Los vértices salen en mundo; el collider va de hijo del río, así que se pasan a su espacio local.
        for(int i = 0; i < vertices.Length; i++)
            vertices[i] = transform.InverseTransformPoint(vertices[i]);

        if(_surface != null) Destroy(_surface);
        if(_mesh != null) Destroy(_mesh);

        _mesh = new Mesh { name = name + " (superficie sólida)" };
        if(vertices.Length > 65000) _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.vertices = vertices;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();

        _surface = new GameObject(name + " (superficie sólida)") { layer = GekkoWater.WaterLayer };
        _surface.transform.SetParent(transform, false);

        var meshCollider = _surface.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = _mesh;

        _surface.AddComponent<RiverSurfacePlatform>().Init(_river, _currentForce);
    }

    // Arma una cinta cerrada (con espesor) a partir de los bordes izquierdo y derecho de cada sección del río, de la entrada a
    // la salida. offset mueve la cara de arriba a lo largo de la normal de la superficie y thickness la lleva hacia abajo.
    // Es estática y sin dependencias de escena para poder probar la geometría sin Unity.
    // Vértices por sección: 0 = arriba izquierda, 1 = arriba derecha, 2 = abajo izquierda, 3 = abajo derecha.
    public static void BuildSlab(IList<Vector3> left, IList<Vector3> right, float offset, float thickness,
                                 out Vector3[] vertices, out int[] triangles)
    {
        int n = left.Count;
        vertices = new Vector3[n * 4];
        var normals = new Vector3[n];
        float upwards = 0f;

        // Normal de la superficie en cada sección: cruz entre el avance del río y su ancho. Con el borde izquierdo a la izquierda
        // mirando río abajo, apunta hacia arriba.
        for(int i = 0; i < n; i++)
        {
            int prev = Mathf.Max(i - 1, 0);
            int next = Mathf.Min(i + 1, n - 1);
            Vector3 forward = (left[next] + right[next]) * 0.5f - (left[prev] + right[prev]) * 0.5f;
            Vector3 normal = Vector3.Cross(forward, right[i] - left[i]);
            normals[i] = normal.sqrMagnitude > 1e-10f ? normal.normalized : Vector3.up;
            upwards += normals[i].y;
        }

        // Si el río quedó espejado (escala negativa, bordes invertidos) todas las normales miran hacia abajo: se dan vuelta juntas,
        // nunca una por una, para no retorcer la cinta en las cascadas, donde la normal es casi horizontal.
        float side = upwards < 0f ? -1f : 1f;

        for(int i = 0; i < n; i++)
        {
            Vector3 normal = normals[i] * side;
            vertices[i * 4 + 0] = left[i] + normal * offset;
            vertices[i * 4 + 1] = right[i] + normal * offset;
            vertices[i * 4 + 2] = vertices[i * 4 + 0] - normal * thickness;
            vertices[i * 4 + 3] = vertices[i * 4 + 1] - normal * thickness;
        }

        var tris = new List<int>((n - 1) * 24 + 12);

        for(int i = 0; i < n - 1; i++)
        {
            int a = i * 4;
            int b = a + 4;

            if(side > 0f) AddQuads(tris, a, b);
            else AddQuadsFlipped(tris, a, b);
        }

        // Tapas de la entrada y de la salida.
        int last = (n - 1) * 4;

        if(side > 0f)
        {
            tris.AddRange(new[] { 0, 1, 2, 1, 3, 2 });
            tris.AddRange(new[] { last + 0, last + 2, last + 1, last + 1, last + 2, last + 3 });
        }
        else
        {
            tris.AddRange(new[] { 0, 2, 1, 1, 2, 3 });
            tris.AddRange(new[] { last + 0, last + 1, last + 2, last + 1, last + 3, last + 2 });
        }

        triangles = tris.ToArray();
    }

    // Una tira entre la sección a y la sección b: arriba, abajo y los dos costados.
    private static void AddQuads(List<int> t, int a, int b)
    {
        // arriba (mira hacia la normal)
        t.AddRange(new[] { a + 0, b + 0, a + 1, a + 1, b + 0, b + 1 });
        // abajo
        t.AddRange(new[] { a + 2, a + 3, b + 2, a + 3, b + 3, b + 2 });
        // costado izquierdo (mira hacia afuera, a la izquierda)
        t.AddRange(new[] { a + 0, a + 2, b + 0, b + 0, a + 2, b + 2 });
        // costado derecho
        t.AddRange(new[] { a + 1, b + 1, a + 3, b + 1, b + 3, a + 3 });
    }

    // Lo mismo con el sentido de los triángulos invertido, para el río espejado.
    private static void AddQuadsFlipped(List<int> t, int a, int b)
    {
        t.AddRange(new[] { a + 0, a + 1, b + 0, a + 1, b + 1, b + 0 });
        t.AddRange(new[] { a + 2, b + 2, a + 3, a + 3, b + 2, b + 3 });
        t.AddRange(new[] { a + 0, b + 0, a + 2, b + 0, b + 2, a + 2 });
        t.AddRange(new[] { a + 1, a + 3, b + 1, b + 1, a + 3, b + 3 });
    }

    // La superficie sólida armada, para ver dónde está parado Gekko (solo en Play, con el río seleccionado).
    private void OnDrawGizmosSelected()
    {
        if(_mesh == null || _surface == null) return;

        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
        Gizmos.matrix = _surface.transform.localToWorldMatrix;
        Gizmos.DrawWireMesh(_mesh);
    }
}
