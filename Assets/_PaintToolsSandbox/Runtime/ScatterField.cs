using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gekko.PaintTools
{
    /// <summary>
    /// Dibuja los props de un <see cref="ScatterData"/> con GPU instancing, sin crear
    /// ni un solo GameObject.
    ///
    /// Como funciona: al construir se aplanan todas las instancias en lotes ya listos
    /// (malla + submalla + material + array de matrices), agrupados por chunk espacial.
    /// El trabajo por frame se reduce a recorrer chunks, descartar los que no entran en
    /// el frustum y disparar un DrawMeshInstanced por lote visible. No hay Transforms
    /// que Unity tenga que actualizar ni jerarquia que recorrer.
    ///
    /// El culling se hace por camara, enganchado a beginCameraRendering, para que la
    /// vista de escena y la del juego descarten cada una lo suyo.
    /// </summary>
    [ExecuteAlways]
    public class ScatterField : MonoBehaviour
    {
        /// <summary>Tope de Graphics.DrawMeshInstanced.</summary>
        private const int MaxInstancesPerBatch = 1023;

        private struct DrawBatch
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public Matrix4x4[] Matrices;
            public int Count;
        }

        private struct Chunk
        {
            public Bounds Bounds;
            public List<DrawBatch> Batches;
        }

        [Header("Datos")]
        [SerializeField] private ScatterData _data;

        [Header("Chunks")]
        [Tooltip("Lado del chunk en unidades. Es la granularidad del culling.")]
        [Min(1f)]
        [SerializeField] private float _chunkSize = 20f;

        [Header("Render")]
        [SerializeField] private ShadowCastingMode _shadowCasting = ShadowCastingMode.On;
        [SerializeField] private bool _receiveShadows = true;
        [Tooltip("Distancia maxima de dibujo. 0 = sin limite.")]
        [Min(0f)]
        [SerializeField] private float _maxDrawDistance;

        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private Matrix4x4 _builtWithMatrix;
        private bool _built;

        public ScatterData Data => _data;
        public int InstanceCount => _data != null ? _data.Count : 0;
        public int ChunkCount => _chunks.Count;

        public int BatchCount
        {
            get
            {
                int total = 0;
                foreach (Chunk chunk in _chunks)
                {
                    total += chunk.Batches.Count;
                }
                return total;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Rebuild();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _chunks.Clear();
            _built = false;
        }

        private void Update()
        {
            // Las matrices se hornean en espacio de mundo, asi que si movés el field hay
            // que rehacerlas. Comparar la matriz es mas barato que reconstruir siempre.
            if (_built && transform.localToWorldMatrix != _builtWithMatrix)
            {
                Rebuild();
            }
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            _chunks.Clear();
            _built = false;

            if (_data == null || _data.Count == 0 || _data.Prototypes.Count == 0)
            {
                return;
            }

            List<List<MeshPart>> prototypeParts = CollectPrototypeParts(_data.Prototypes);

            // 1) Agrupar instancias por chunk.
            var buckets = new Dictionary<Vector3Int, List<ScatterInstance>>();
            foreach (ScatterInstance instance in _data.Instances)
            {
                var key = new Vector3Int(
                    Mathf.FloorToInt(instance.Position.x / _chunkSize),
                    Mathf.FloorToInt(instance.Position.y / _chunkSize),
                    Mathf.FloorToInt(instance.Position.z / _chunkSize));

                if (!buckets.TryGetValue(key, out List<ScatterInstance> list))
                {
                    list = new List<ScatterInstance>();
                    buckets[key] = list;
                }

                list.Add(instance);
            }

            Matrix4x4 fieldMatrix = transform.localToWorldMatrix;

            // 2) Aplanar cada chunk en lotes listos para dibujar.
            foreach (KeyValuePair<Vector3Int, List<ScatterInstance>> bucket in buckets)
            {
                var byPrototype = new Dictionary<int, List<Matrix4x4>>();
                Bounds bounds = default;
                bool boundsInitialized = false;

                foreach (ScatterInstance instance in bucket.Value)
                {
                    if (instance.PrototypeIndex < 0 || instance.PrototypeIndex >= prototypeParts.Count)
                    {
                        continue;
                    }

                    Matrix4x4 world = fieldMatrix * Matrix4x4.TRS(instance.Position, instance.Rotation, instance.Scale);

                    if (!byPrototype.TryGetValue(instance.PrototypeIndex, out List<Matrix4x4> matrices))
                    {
                        matrices = new List<Matrix4x4>();
                        byPrototype[instance.PrototypeIndex] = matrices;
                    }

                    matrices.Add(world);

                    Vector3 worldPosition = world.GetColumn(3);
                    if (!boundsInitialized)
                    {
                        bounds = new Bounds(worldPosition, Vector3.zero);
                        boundsInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(worldPosition);
                    }
                }

                if (!boundsInitialized)
                {
                    continue;
                }

                var batches = new List<DrawBatch>();

                foreach (KeyValuePair<int, List<Matrix4x4>> entry in byPrototype)
                {
                    List<MeshPart> parts = prototypeParts[entry.Key];
                    List<Matrix4x4> instanceMatrices = entry.Value;

                    foreach (MeshPart part in parts)
                    {
                        // La transformada local de la pieza dentro del prefab se
                        // premultiplica aca, no en cada frame.
                        var partMatrices = new Matrix4x4[instanceMatrices.Count];
                        for (int i = 0; i < instanceMatrices.Count; i++)
                        {
                            partMatrices[i] = instanceMatrices[i] * part.LocalMatrix;
                        }

                        for (int submesh = 0; submesh < part.Mesh.subMeshCount; submesh++)
                        {
                            Material material = submesh < part.Materials.Length
                                ? part.Materials[submesh]
                                : part.Materials[part.Materials.Length - 1];

                            if (material == null)
                            {
                                continue;
                            }

                            // DrawMeshInstanced no acepta mas de 1023 por llamada.
                            for (int offset = 0; offset < partMatrices.Length; offset += MaxInstancesPerBatch)
                            {
                                int count = Mathf.Min(MaxInstancesPerBatch, partMatrices.Length - offset);
                                var slice = new Matrix4x4[count];
                                System.Array.Copy(partMatrices, offset, slice, 0, count);

                                batches.Add(new DrawBatch
                                {
                                    Mesh = part.Mesh,
                                    SubMesh = submesh,
                                    Material = material,
                                    Matrices = slice,
                                    Count = count,
                                });
                            }
                        }
                    }
                }

                // Se agranda la caja con el tamano del prop mas grande: las bounds se
                // calcularon con los origenes de las instancias, no con su volumen, asi
                // que sin este margen los props del borde parpadean al salir de camara.
                bounds.Expand(_chunkSize * 0.5f);

                _chunks.Add(new Chunk { Bounds = bounds, Batches = batches });
            }

            _builtWithMatrix = fieldMatrix;
            _built = true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_built || camera == null || camera.cameraType == CameraType.Preview)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            Vector3 viewer = camera.transform.position;
            float maxDistanceSqr = _maxDrawDistance > 0f ? _maxDrawDistance * _maxDrawDistance : float.MaxValue;
            int layer = gameObject.layer;

            foreach (Chunk chunk in _chunks)
            {
                if ((chunk.Bounds.center - viewer).sqrMagnitude > maxDistanceSqr)
                {
                    continue;
                }

                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, chunk.Bounds))
                {
                    continue;
                }

                foreach (DrawBatch batch in chunk.Batches)
                {
                    Graphics.DrawMeshInstanced(
                        batch.Mesh,
                        batch.SubMesh,
                        batch.Material,
                        batch.Matrices,
                        batch.Count,
                        null,
                        _shadowCasting,
                        _receiveShadows,
                        layer,
                        camera);
                }
            }
        }

        private struct MeshPart
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Matrix4x4 LocalMatrix;
        }

        /// <summary>
        /// Saca las mallas de cada prefab, incluyendo las de sus hijos, con la
        /// transformada relativa a la raiz. Asi un prefab de varias piezas se dibuja
        /// entero y no solo su primer renderer.
        /// </summary>
        private static List<List<MeshPart>> CollectPrototypeParts(List<GameObject> prototypes)
        {
            var result = new List<List<MeshPart>>(prototypes.Count);

            foreach (GameObject prototype in prototypes)
            {
                var parts = new List<MeshPart>();

                if (prototype != null)
                {
                    Transform root = prototype.transform;

                    foreach (MeshFilter filter in prototype.GetComponentsInChildren<MeshFilter>(false))
                    {
                        Mesh mesh = filter.sharedMesh;
                        var renderer = filter.GetComponent<MeshRenderer>();

                        if (mesh == null || renderer == null || renderer.sharedMaterials.Length == 0)
                        {
                            continue;
                        }

                        parts.Add(new MeshPart
                        {
                            Mesh = mesh,
                            Materials = renderer.sharedMaterials,
                            LocalMatrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                        });
                    }
                }

                result.Add(parts);
            }

            return result;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            foreach (Chunk chunk in _chunks)
            {
                Gizmos.DrawWireCube(chunk.Bounds.center, chunk.Bounds.size);
            }
        }
    }
}
