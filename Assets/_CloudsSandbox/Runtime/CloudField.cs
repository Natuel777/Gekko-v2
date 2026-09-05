using System.Collections.Generic;
using UnityEngine;

namespace Gekko.CloudsSandbox
{
    /// <summary>
    /// Reparte nubes para formar un mar de nubes. Pensado para usarse en el Editor:
    /// se aprieta "Rebuild" y quedan los hijos generados en la escena, no se instancia
    /// nada en runtime.
    ///
    /// Hay dos formas de pedir lo mismo, y cual conviene depende de que tengas fijo:
    ///
    ///  - <see cref="DistributionMode.PorCantidad"/>: elegis CUANTAS nubes y que AREA
    ///    cubrir; la separacion sale de esos dos. Es el modo por defecto.
    ///  - <see cref="DistributionMode.PorSeparacion"/>: elegis la SEPARACION y el
    ///    tamano de la grilla; la cantidad sale de esos dos.
    ///
    /// En los dos casos por debajo hay una grilla de celdas. Eso importa por dos
    /// motivos: la separacion es un parametro real y no un promedio estadistico (con
    /// posiciones random puras salen huecos y encimadas por azar), y la grilla es
    /// periodica, que es lo que permite que <see cref="CloudSeaScroll"/> haga wrap
    /// toroidal sin que se note la costura.
    /// </summary>
    [ExecuteAlways]
    public class CloudField : MonoBehaviour
    {
        private const string GeneratedChildName = "Cloud_";

        /// <summary>Arriba de esto la seleccion por dispersion se vuelve lenta y se cae al metodo barato.</summary>
        private const int MaxCellsForSpreadSelection = 20000;

        public enum DistributionMode
        {
            PorCantidad,
            PorSeparacion,
        }

        [Header("Fuentes")]
        [SerializeField] private Mesh[] _cloudMeshes;
        [SerializeField] private Material _cloudMaterial;

        [Header("Distribucion")]
        [SerializeField] private DistributionMode _mode = DistributionMode.PorCantidad;
        [SerializeField] private int _seed = 7;

        [Tooltip("Cantidad exacta de nubes en escena.")]
        [Min(1)]
        [SerializeField] private int _count = 40;
        [Tooltip("Superficie a cubrir en X y Z, en unidades.")]
        [SerializeField] private Vector2 _area = new Vector2(60f, 48f);

        [Tooltip("Celdas en X y en Z.")]
        [SerializeField] private Vector2Int _gridSize = new Vector2Int(10, 8);
        [Tooltip("Distancia entre nubes vecinas. Para que se lea como mar continuo tiene " +
                 "que ser MENOR que el diametro promedio de la nube.")]
        [Min(0.01f)]
        [SerializeField] private float _spacing = 6f;
        [Tooltip("Proporcion de celdas que se llenan.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _fillRatio = 1f;

        [Tooltip("Cuanto se corre cada nube dentro de su celda. 0 = grilla perfecta, 1 = puede tocar la celda vecina.")]
        [Range(0f, 1f)]
        [SerializeField] private float _jitter = 0.6f;
        [Tooltip("Variacion de altura, en unidades.")]
        [SerializeField] private float _heightVariation = 3f;

        [Header("Tamano")]
        [SerializeField] private Vector2 _scaleRange = new Vector2(7f, 13f);
        [Tooltip("Achatamiento vertical. Las nubes de un mar son mucho mas anchas que altas.")]
        [SerializeField] private float _verticalSquash = 0.5f;

        [Header("Movimiento")]
        [Tooltip("Agrega el CloudSeaScroll al padre y lo configura con la medida del campo.")]
        [SerializeField] private bool _addScroller = true;
        [Tooltip("Direccion de deriva. Solo XZ: un mar de nubes se mueve en horizontal.")]
        [SerializeField] private Vector3 _scrollDirection = new Vector3(1f, 0f, 0f);
        [SerializeField] private float _scrollSpeed = 0.6f;
        [SerializeField] private float _bobAmplitude = 0.35f;
        [SerializeField] private float _bobFrequency = 0.3f;

        [Header("Render")]
        [SerializeField] private bool _castShadows;

        /// <summary>Resultado de resolver el modo elegido: grilla, separacion y cantidad.</summary>
        public readonly struct Layout
        {
            public readonly int Columns;
            public readonly int Rows;
            public readonly float SpacingX;
            public readonly float SpacingZ;
            public readonly int Count;

            public Layout(int columns, int rows, float spacingX, float spacingZ, int count)
            {
                Columns = columns;
                Rows = rows;
                SpacingX = spacingX;
                SpacingZ = spacingZ;
                Count = count;
            }

            public Vector2 Extent => new Vector2(Columns * SpacingX, Rows * SpacingZ);
            public int TotalCells => Columns * Rows;
        }

        public DistributionMode Mode => _mode;

        /// <summary>Medida total del campo en X y Z. Es el periodo del wrap.</summary>
        public Vector2 FieldExtent => ResolveLayout().Extent;

        /// <summary>Diametro promedio de una nube, para comparar contra la separacion.</summary>
        public float AverageDiameter => _scaleRange.x + _scaleRange.y;

        /// <summary>
        /// Traduce el modo elegido a una grilla concreta. Es la unica parte que
        /// distingue un modo del otro: de aca para abajo el pipeline es identico.
        /// </summary>
        public Layout ResolveLayout()
        {
            if (_mode == DistributionMode.PorSeparacion)
            {
                int columns = Mathf.Max(1, _gridSize.x);
                int rows = Mathf.Max(1, _gridSize.y);
                float spacing = Mathf.Max(0.01f, _spacing);
                int count = Mathf.Max(1, Mathf.RoundToInt(columns * rows * _fillRatio));
                return new Layout(columns, rows, spacing, spacing, count);
            }

            // Por cantidad: se busca la grilla cuyas celdas queden lo mas cuadradas
            // posible dentro del area pedida, y que tenga al menos tantas celdas como
            // nubes. Celdas cuadradas => separacion pareja en X y en Z.
            int desired = Mathf.Max(1, _count);
            float areaX = Mathf.Max(0.01f, _area.x);
            float areaZ = Mathf.Max(0.01f, _area.y);

            float aspect = areaX / areaZ;
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(desired * aspect)));
            int rws = Mathf.Max(1, Mathf.CeilToInt(desired / (float)cols));

            // El redondeo puede dejar la grilla corta; se ensancha hasta que entre.
            while (cols * rws < desired)
            {
                cols++;
                rws = Mathf.Max(1, Mathf.CeilToInt(desired / (float)cols));
            }

            return new Layout(cols, rws, areaX / cols, areaZ / rws, desired);
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();

            if (_cloudMeshes == null || _cloudMeshes.Length == 0)
            {
                Debug.LogWarning("[CloudField] No hay mallas asignadas. Generalas con Tools > Gekko > Clouds > Cloud Mesh Builder.", this);
                return;
            }

            Layout layout = ResolveLayout();

            Random.State previousState = Random.state;
            Random.InitState(_seed);

            List<int> cells = SelectCells(layout, _seed);

            Vector2 extent = layout.Extent;
            float maxJitterX = layout.SpacingX * 0.5f * _jitter;
            float maxJitterZ = layout.SpacingZ * 0.5f * _jitter;

            int index = 0;
            foreach (int cell in cells)
            {
                Mesh mesh = _cloudMeshes[Random.Range(0, _cloudMeshes.Length)];
                if (mesh == null)
                {
                    continue;
                }

                int gx = cell / layout.Rows;
                int gz = cell % layout.Rows;

                // Centro de celda, con la grilla centrada en el origen del padre. El
                // +0.5 deja las celdas dentro de [-extent/2, +extent/2], que es
                // exactamente el rango sobre el que despues se hace el wrap.
                float cellX = (gx + 0.5f) * layout.SpacingX - extent.x * 0.5f;
                float cellZ = (gz + 0.5f) * layout.SpacingZ - extent.y * 0.5f;

                var cloud = new GameObject(GeneratedChildName + index.ToString("000"));
                cloud.transform.SetParent(transform, false);

                cloud.transform.SetLocalPositionAndRotation(
                    new Vector3(
                        cellX + Random.Range(-maxJitterX, maxJitterX),
                        Random.Range(-0.5f, 0.5f) * _heightVariation,
                        cellZ + Random.Range(-maxJitterZ, maxJitterZ)),
                    // Solo yaw: inclinar una nube delata la forma del blob y rompe el
                    // degrade vertical del shader, que asume que "arriba" es +Y local.
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

                float scale = Random.Range(_scaleRange.x, _scaleRange.y);
                cloud.transform.localScale = new Vector3(scale, scale * _verticalSquash, scale);

                cloud.AddComponent<MeshFilter>().sharedMesh = mesh;

                var renderer = cloud.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _cloudMaterial;
                renderer.shadowCastingMode = _castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                index++;
            }

            Random.state = previousState;

            ConfigureScroller(layout);

            Debug.Log($"[CloudField] {index} nubes en {extent.x:0} x {extent.y:0} unidades " +
                      $"(separacion {layout.SpacingX:0.0} x {layout.SpacingZ:0.0}).", this);
        }

        /// <summary>
        /// Elige que celdas se ocupan. Cuando sobran celdas no alcanza con saltear al
        /// azar: el azar hace grumos y claros grandes, justo lo que no se quiere en un
        /// mar. Se usa insercion por punto mas lejano (farthest-point): cada nube nueva
        /// va a la celda que este mas lejos de todas las ya elegidas, lo que da un
        /// reparto parejo tipo blue noise para cualquier cantidad.
        ///
        /// Las distancias son toroidales, en linea con el wrap de CloudSeaScroll: si no,
        /// al dar la vuelta el borde quedaria mas denso o mas ralo que el resto.
        /// </summary>
        private static List<int> SelectCells(Layout layout, int seed)
        {
            int total = layout.TotalCells;
            int target = Mathf.Clamp(layout.Count, 1, total);
            var selected = new List<int>(target);

            if (target == total)
            {
                for (int i = 0; i < total; i++)
                {
                    selected.Add(i);
                }
                return selected;
            }

            if (total > MaxCellsForSpreadSelection)
            {
                // Demasiadas celdas para el O(n*m): se cae al salteo al azar para no
                // colgar el Editor. Queda menos parejo, pero a esta escala no se nota.
                Random.InitState(seed);
                for (int i = 0; i < total && selected.Count < target; i++)
                {
                    int remainingCells = total - i;
                    int remainingPicks = target - selected.Count;
                    if (Random.value < remainingPicks / (float)remainingCells)
                    {
                        selected.Add(i);
                    }
                }
                return selected;
            }

            var minSqrDistance = new float[total];
            for (int i = 0; i < total; i++)
            {
                minSqrDistance[i] = float.MaxValue;
            }

            Random.InitState(seed);
            int current = Random.Range(0, total);

            for (int picked = 0; picked < target; picked++)
            {
                selected.Add(current);
                minSqrDistance[current] = -1f;

                int currentX = current / layout.Rows;
                int currentZ = current % layout.Rows;

                int best = -1;
                float bestDistance = -1f;

                for (int i = 0; i < total; i++)
                {
                    if (minSqrDistance[i] < 0f)
                    {
                        continue;
                    }

                    int dxCells = Mathf.Abs(i / layout.Rows - currentX);
                    dxCells = Mathf.Min(dxCells, layout.Columns - dxCells);

                    int dzCells = Mathf.Abs(i % layout.Rows - currentZ);
                    dzCells = Mathf.Min(dzCells, layout.Rows - dzCells);

                    float dx = dxCells * layout.SpacingX;
                    float dz = dzCells * layout.SpacingZ;
                    float sqrDistance = dx * dx + dz * dz;

                    if (sqrDistance < minSqrDistance[i])
                    {
                        minSqrDistance[i] = sqrDistance;
                    }

                    if (minSqrDistance[i] > bestDistance)
                    {
                        bestDistance = minSqrDistance[i];
                        best = i;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                current = best;
            }

            return selected;
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            var doomed = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(GeneratedChildName))
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject child in doomed)
            {
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void ConfigureScroller(Layout layout)
        {
            var scroller = GetComponent<CloudSeaScroll>();

            if (!_addScroller)
            {
                if (scroller != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(scroller);
                    }
                    else
                    {
                        DestroyImmediate(scroller);
                    }
                }
                return;
            }

            if (scroller == null)
            {
                scroller = gameObject.AddComponent<CloudSeaScroll>();
            }

            scroller.Configure(_scrollDirection, _scrollSpeed, layout.Extent, _bobAmplitude, _bobFrequency);
        }

        private void OnDrawGizmosSelected()
        {
            Layout layout = ResolveLayout();
            Vector2 extent = layout.Extent;

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.5f, 0.75f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(extent.x, Mathf.Max(_heightVariation, 0.1f), extent.y));

            // Una celda, para ver de un vistazo cuanta separacion quedo.
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(
                new Vector3(layout.SpacingX * 0.5f - extent.x * 0.5f, 0f, layout.SpacingZ * 0.5f - extent.y * 0.5f),
                new Vector3(layout.SpacingX, 0.1f, layout.SpacingZ));
        }
    }
}
