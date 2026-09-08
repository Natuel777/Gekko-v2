using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gekko.CloudsSandbox.EditorTools
{
    /// <summary>
    /// Genera mallas de nube "puffy" y las guarda como asset.
    ///
    /// El shader de nubes necesita una malla con normales suaves y continuas (si la
    /// malla es facetada, el degrade de luz se corta y la deformacion por vertice se
    /// nota como picos). Por eso no se combinan esferas sueltas: se parte de una
    /// icosfera de topologia uniforme y se empuja cada vertice hacia afuera hasta la
    /// isosuperficie de un campo de metaballs. El resultado es un unico blob cerrado,
    /// sin costuras internas y con vertices compartidos, asi RecalculateNormals da
    /// normales suaves de una.
    ///
    /// Limitacion conocida: al proyectar radialmente desde el centro, la forma debe
    /// ser "star-shaped" respecto del origen. Metaballs muy alejados del centro
    /// generan bultos que el rayo no alcanza. Mantener Spread por debajo de ~1.5.
    /// </summary>
    public class CloudMeshBuilder : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/_CloudsSandbox/Meshes";

        [SerializeField] private string _meshName = "Cloud_A";
        [SerializeField] private int _seed = 12345;
        [SerializeField] private int _blobCount = 7;
        [SerializeField] private int _subdivisions = 3;
        [SerializeField] private Vector3 _spread = new Vector3(1.1f, 0.35f, 0.7f);
        [SerializeField] private float _blobRadiusMin = 0.45f;
        [SerializeField] private float _blobRadiusMax = 0.85f;
        [SerializeField] private float _threshold = 1.0f;
        [SerializeField] private float _flattenBottom = 0.35f;
        [SerializeField] private string _outputFolder = DefaultOutputFolder;

        [MenuItem("Tools/Gekko/Clouds/Cloud Mesh Builder")]
        private static void Open()
        {
            GetWindow<CloudMeshBuilder>(true, "Cloud Mesh Builder", true).minSize = new Vector2(360f, 420f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Malla", EditorStyles.boldLabel);
            _meshName = EditorGUILayout.TextField("Nombre", _meshName);
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _subdivisions = EditorGUILayout.IntSlider("Subdivisiones", _subdivisions, 1, 4);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Forma", EditorStyles.boldLabel);
            _blobCount = EditorGUILayout.IntSlider("Cantidad de blobs", _blobCount, 1, 24);
            _spread = EditorGUILayout.Vector3Field("Dispersion", _spread);
            _blobRadiusMin = EditorGUILayout.Slider("Radio minimo", _blobRadiusMin, 0.1f, 1.5f);
            _blobRadiusMax = EditorGUILayout.Slider("Radio maximo", _blobRadiusMax, 0.1f, 2f);
            _threshold = EditorGUILayout.Slider("Umbral de la isosuperficie", _threshold, 0.25f, 3f);
            _flattenBottom = EditorGUILayout.Slider("Aplanar la base", _flattenBottom, 0f, 1f);

            EditorGUILayout.Space();
            _outputFolder = EditorGUILayout.TextField("Carpeta destino", _outputFolder);

            int vertexEstimate = 10 * (1 << (2 * _subdivisions)) + 2;
            EditorGUILayout.HelpBox($"~{vertexEstimate} vertices por malla.", MessageType.None);

            EditorGUILayout.Space();
            if (GUILayout.Button("Generar malla", GUILayout.Height(30f)))
            {
                CreateAndSave(_meshName, _seed);
            }

            if (GUILayout.Button("Generar set de 5 variantes", GUILayout.Height(24f)))
            {
                for (int i = 0; i < 5; i++)
                {
                    CreateAndSave($"{_meshName}_{i:00}", _seed + i * 7919);
                }
            }
        }

        private void CreateAndSave(string meshName, int seed)
        {
            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
                AssetDatabase.Refresh();
            }

            Mesh mesh = Build(meshName, seed);
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(_outputFolder, meshName + ".asset").Replace('\\', '/'));

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(mesh);
            Debug.Log($"[CloudMeshBuilder] Malla generada en {path} ({mesh.vertexCount} vertices).");
        }

        private Mesh Build(string meshName, int seed)
        {
            IcoSphere.Generate(_subdivisions, out List<Vector3> vertices, out List<int> triangles);

            Random.State previousState = Random.state;
            Random.InitState(seed);

            // El primer metaball va al centro para garantizar que el campo sea denso
            // en el origen y todos los rayos encuentren superficie.
            var centers = new Vector3[_blobCount];
            var radii = new float[_blobCount];
            centers[0] = Vector3.zero;
            radii[0] = Mathf.Max(_blobRadiusMin, _blobRadiusMax);

            for (int i = 1; i < _blobCount; i++)
            {
                Vector3 unit = Random.insideUnitSphere;
                centers[i] = Vector3.Scale(unit, _spread);
                radii[i] = Random.Range(_blobRadiusMin, _blobRadiusMax);
            }

            Random.state = previousState;

            float maxRadius = _spread.magnitude + _blobRadiusMax + 0.5f;

            var uvs = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 direction = vertices[i].normalized;
                float radius = FindSurfaceRadius(direction, centers, radii, maxRadius);
                Vector3 position = direction * radius;

                // Las nubes de referencia tienen la panza chata: se comprime solo la
                // mitad inferior, dejando la parte de arriba con todo su volumen.
                if (_flattenBottom > 0f && position.y < 0f)
                {
                    position.y *= 1f - _flattenBottom;
                }

                vertices[i] = position;
                uvs[i] = new Vector2(
                    0.5f + Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI),
                    0.5f + Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI);
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.Optimize();
            return mesh;
        }

        /// <summary>
        /// Campo escalar de metaballs: cae con el cuadrado de la distancia, asi que
        /// los blobs cercanos se funden en vez de intersectarse.
        /// </summary>
        private static float SampleField(Vector3 point, Vector3[] centers, float[] radii)
        {
            float sum = 0f;
            for (int i = 0; i < centers.Length; i++)
            {
                float sqrDistance = (point - centers[i]).sqrMagnitude + 1e-4f;
                sum += (radii[i] * radii[i]) / sqrDistance;
            }
            return sum;
        }

        /// <summary>
        /// Busca sobre el rayo (origen -> direccion) el radio donde el campo cruza el
        /// umbral: barrido grueso hacia adentro para encontrar el cruce mas externo, y
        /// biseccion para refinarlo.
        /// </summary>
        private float FindSurfaceRadius(Vector3 direction, Vector3[] centers, float[] radii, float maxRadius)
        {
            const int CoarseSteps = 48;
            const int RefineSteps = 24;

            float step = maxRadius / CoarseSteps;
            float outside = maxRadius;

            for (int i = CoarseSteps; i >= 0; i--)
            {
                float r = i * step;
                if (SampleField(direction * r, centers, radii) >= _threshold)
                {
                    float inside = r;
                    for (int j = 0; j < RefineSteps; j++)
                    {
                        float mid = 0.5f * (inside + outside);
                        if (SampleField(direction * mid, centers, radii) >= _threshold)
                        {
                            inside = mid;
                        }
                        else
                        {
                            outside = mid;
                        }
                    }
                    return 0.5f * (inside + outside);
                }
                outside = r;
            }

            // Sin cruce (umbral demasiado alto): se devuelve un radio minimo para no
            // colapsar la malla en un punto.
            return 0.05f;
        }

        /// <summary>
        /// Icosfera por subdivision con cache de puntos medios, de modo que los
        /// vertices quedan compartidos entre triangulos y las normales salen suaves.
        /// </summary>
        private static class IcoSphere
        {
            public static void Generate(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
            {
                float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

                vertices = new List<Vector3>
                {
                    new Vector3(-1f,  t, 0f).normalized,
                    new Vector3( 1f,  t, 0f).normalized,
                    new Vector3(-1f, -t, 0f).normalized,
                    new Vector3( 1f, -t, 0f).normalized,
                    new Vector3(0f, -1f,  t).normalized,
                    new Vector3(0f,  1f,  t).normalized,
                    new Vector3(0f, -1f, -t).normalized,
                    new Vector3(0f,  1f, -t).normalized,
                    new Vector3( t, 0f, -1f).normalized,
                    new Vector3( t, 0f,  1f).normalized,
                    new Vector3(-t, 0f, -1f).normalized,
                    new Vector3(-t, 0f,  1f).normalized,
                };

                var faces = new List<int>
                {
                    0, 11, 5,  0, 5, 1,   0, 1, 7,   0, 7, 10,  0, 10, 11,
                    1, 5, 9,   5, 11, 4,  11, 10, 2, 10, 7, 6,  7, 1, 8,
                    3, 9, 4,   3, 4, 2,   3, 2, 6,   3, 6, 8,   3, 8, 9,
                    4, 9, 5,   2, 4, 11,  6, 2, 10,  8, 6, 7,   9, 8, 1,
                };

                var midpointCache = new Dictionary<long, int>();

                for (int s = 0; s < subdivisions; s++)
                {
                    var next = new List<int>(faces.Count * 4);
                    for (int i = 0; i < faces.Count; i += 3)
                    {
                        int a = faces[i];
                        int b = faces[i + 1];
                        int c = faces[i + 2];

                        int ab = Midpoint(a, b, vertices, midpointCache);
                        int bc = Midpoint(b, c, vertices, midpointCache);
                        int ca = Midpoint(c, a, vertices, midpointCache);

                        next.Add(a); next.Add(ab); next.Add(ca);
                        next.Add(b); next.Add(bc); next.Add(ab);
                        next.Add(c); next.Add(ca); next.Add(bc);
                        next.Add(ab); next.Add(bc); next.Add(ca);
                    }
                    faces = next;
                }

                triangles = faces;
            }

            private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (cache.TryGetValue(key, out int existing))
                {
                    return existing;
                }

                Vector3 middle = ((vertices[a] + vertices[b]) * 0.5f).normalized;
                vertices.Add(middle);

                int index = vertices.Count - 1;
                cache[key] = index;
                return index;
            }
        }
    }
}
