using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gekko.GrassSandbox
{
    /// <summary>
    /// Convierte las briznas pintadas de un <see cref="GrassData"/> en mallas listas
    /// para dibujar, agrupadas en chunks espaciales.
    ///
    /// Por que chunks: una sola malla gigante es un unico objeto para Unity, asi que o
    /// se dibuja entera o nada — no hay culling posible y se paga todo el campo en cada
    /// frame. Partido en chunks, el frustum culling normal de Unity descarta los que no
    /// se ven, que en un plataformero con camara cerrada es la enorme mayoria. El costo
    /// es un draw call por chunk visible, y por eso el tamano de chunk es un balance:
    /// muy chico = muchos draw calls, muy grande = culling grueso.
    ///
    /// Las mallas de chunk NO se guardan: se reconstruyen en OnEnable desde el asset de
    /// datos, y los GameObjects generados van con HideFlags.DontSave. Asi la escena no
    /// engorda y no hay assets de malla intermedios que mantener.
    /// </summary>
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        private const string ChunkName = "GrassChunk_";

        [Header("Datos")]
        [SerializeField] private GrassData _data;
        [SerializeField] private Material _grassMaterial;

        [Header("Chunks")]
        [Tooltip("Lado del chunk en unidades. Mas chico = mejor culling pero mas draw calls.")]
        [Min(1f)]
        [SerializeField] private float _chunkSize = 8f;

        [Header("Geometria de la brizna")]
        [Tooltip("Segmentos verticales. 1 = la brizna solo se inclina; 2 o 3 = se curva. " +
                 "Cada segmento suma 2 vertices y 2 triangulos por brizna.")]
        [Range(1, 3)]
        [SerializeField] private int _segments = 2;
        [Tooltip("Cuanto se enderezan las briznas hacia el mundo en vez de seguir la normal del suelo. " +
                 "En pendientes, 0 las deja perpendiculares al piso y 1 las para verticales.")]
        [Range(0f, 1f)]
        [SerializeField] private float _uprightBias = 0.55f;
        [Tooltip("Curvatura de la brizna hacia adelante.")]
        [SerializeField] private float _curve = 0.25f;

        [Header("Render")]
        [Tooltip("Las sombras de pasto son caras y aportan poco: por defecto van apagadas.")]
        [SerializeField] private bool _castShadows;

        private readonly List<GameObject> _chunks = new List<GameObject>();

        public GrassData Data => _data;
        public int BladeCount => _data != null ? _data.Count : 0;
        public int ChunkCount => _chunks.Count;

        /// <summary>Vertices por brizna, segun los segmentos configurados.</summary>
        public int VerticesPerBlade => _segments * 2 + 1;

        /// <summary>Triangulos por brizna, segun los segmentos configurados.</summary>
        public int TrianglesPerBlade => _segments * 2 - 1;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            ClearChunks();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            ClearChunks();

            if (_data == null || _data.Count == 0)
            {
                return;
            }

            if (_grassMaterial == null)
            {
                Debug.LogWarning("[GrassField] Falta el material. Crealo con Tools > Gekko > Grass > Crear material de pasto.", this);
                return;
            }

            Dictionary<Vector3Int, List<GrassBladeData>> buckets = BucketByChunk(_data.Blades, _chunkSize);

            foreach (KeyValuePair<Vector3Int, List<GrassBladeData>> bucket in buckets)
            {
                // El origen del chunk es su esquina: construir la geometria relativa a
                // el mantiene los numeros chicos y evita perder precision de float en
                // niveles alejados del origen del mundo.
                Vector3 chunkOrigin = new Vector3(
                    bucket.Key.x * _chunkSize,
                    bucket.Key.y * _chunkSize,
                    bucket.Key.z * _chunkSize);

                Mesh mesh = BuildChunkMesh(bucket.Value, chunkOrigin);
                if (mesh == null)
                {
                    continue;
                }

                var chunk = new GameObject($"{ChunkName}{bucket.Key.x}_{bucket.Key.y}_{bucket.Key.z}")
                {
                    // No se guardan en la escena: se rehacen solos en OnEnable.
                    hideFlags = HideFlags.DontSave
                };

                chunk.transform.SetParent(transform, false);
                chunk.transform.localPosition = chunkOrigin;

                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = chunk.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _grassMaterial;
                renderer.shadowCastingMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;

                _chunks.Add(chunk);
            }
        }

        public void ClearChunks()
        {
            foreach (GameObject chunk in _chunks)
            {
                if (chunk == null)
                {
                    continue;
                }

                var filter = chunk.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    DestroyObject(filter.sharedMesh);
                }

                DestroyObject(chunk);
            }

            _chunks.Clear();

            // Barrido de seguridad: si hubo un domain reload, la lista se vacia pero los
            // GameObjects pueden seguir colgando del transform.
            var leftovers = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(ChunkName))
                {
                    leftovers.Add(child.gameObject);
                }
            }

            foreach (GameObject leftover in leftovers)
            {
                DestroyObject(leftover);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Dictionary<Vector3Int, List<GrassBladeData>> BucketByChunk(
            List<GrassBladeData> blades, float chunkSize)
        {
            var buckets = new Dictionary<Vector3Int, List<GrassBladeData>>();

            foreach (GrassBladeData blade in blades)
            {
                var key = new Vector3Int(
                    Mathf.FloorToInt(blade.Position.x / chunkSize),
                    Mathf.FloorToInt(blade.Position.y / chunkSize),
                    Mathf.FloorToInt(blade.Position.z / chunkSize));

                if (!buckets.TryGetValue(key, out List<GrassBladeData> list))
                {
                    list = new List<GrassBladeData>();
                    buckets[key] = list;
                }

                list.Add(blade);
            }

            return buckets;
        }

        private Mesh BuildChunkMesh(List<GrassBladeData> blades, Vector3 chunkOrigin)
        {
            int bladeCount = blades.Count;
            if (bladeCount == 0)
            {
                return null;
            }

            int vertsPerBlade = VerticesPerBlade;
            int trisPerBlade = TrianglesPerBlade;

            var vertices = new List<Vector3>(bladeCount * vertsPerBlade);
            var normals = new List<Vector3>(bladeCount * vertsPerBlade);
            var uvs = new List<Vector2>(bladeCount * vertsPerBlade);
            var colors = new List<Color32>(bladeCount * vertsPerBlade);
            var triangles = new List<int>(bladeCount * trisPerBlade * 3);

            foreach (GrassBladeData blade in blades)
            {
                AppendBlade(blade, chunkOrigin, vertices, normals, uvs, colors, triangles);
            }

            var mesh = new Mesh
            {
                name = "GrassChunk",
                // Un chunk pasa los 65k vertices facil con densidad alta.
                indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                hideFlags = HideFlags.DontSave
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            // Sin tangentes ni normales recalculadas: las normales son las del suelo,
            // ya las escribimos a mano, y el shader no usa normal map.
            mesh.UploadMeshData(false);
            return mesh;
        }

        private void AppendBlade(
            GrassBladeData blade,
            Vector3 chunkOrigin,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> triangles)
        {
            // Las briznas crecen entre la normal del suelo y la vertical del mundo. En
            // pendientes fuertes, seguir la normal al pie las deja acostadas; enderezarlas
            // del todo las despega del piso. El bias deja elegir el punto medio.
            Vector3 up = Vector3.Slerp(blade.Normal.normalized, Vector3.up, _uprightBias).normalized;

            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.95f ? Vector3.right : Vector3.forward;
            Vector3 side = Vector3.Normalize(Vector3.Cross(up, reference));
            side = Quaternion.AngleAxis(blade.Yaw, up) * side;
            Vector3 forward = Vector3.Cross(side, up);

            Vector3 basePosition = blade.Position - chunkOrigin;
            int firstVertex = vertices.Count;

            byte variation = (byte)Mathf.Clamp(Mathf.RoundToInt(blade.Variation * 255f), 0, 255);
            var color = new Color32(variation, 255, 0, 255);

            for (int segment = 0; segment < _segments; segment++)
            {
                float t = segment / (float)_segments;

                // La brizna se afina hacia la punta y se curva hacia adelante. La curva
                // va con t^2 para que arranque recta desde el suelo.
                float halfWidth = blade.Width * 0.5f * (1f - t);
                Vector3 center = basePosition + up * (blade.Height * t) + forward * (_curve * blade.Height * t * t);

                vertices.Add(center - side * halfWidth);
                vertices.Add(center + side * halfWidth);

                normals.Add(blade.Normal);
                normals.Add(blade.Normal);

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(1f, t));

                colors.Add(color);
                colors.Add(color);
            }

            // Punta: un solo vertice.
            Vector3 tip = basePosition + up * blade.Height + forward * (_curve * blade.Height);
            vertices.Add(tip);
            normals.Add(blade.Normal);
            uvs.Add(new Vector2(0.5f, 1f));
            colors.Add(color);

            // Quads entre niveles consecutivos.
            for (int segment = 0; segment < _segments - 1; segment++)
            {
                int bottom = firstVertex + segment * 2;
                int top = bottom + 2;

                triangles.Add(bottom);
                triangles.Add(top);
                triangles.Add(bottom + 1);

                triangles.Add(bottom + 1);
                triangles.Add(top);
                triangles.Add(top + 1);
            }

            // Ultimo tramo: los dos vertices de arriba cierran contra la punta.
            int lastBottom = firstVertex + (_segments - 1) * 2;
            int tipIndex = firstVertex + _segments * 2;

            triangles.Add(lastBottom);
            triangles.Add(tipIndex);
            triangles.Add(lastBottom + 1);
        }
    }
}
