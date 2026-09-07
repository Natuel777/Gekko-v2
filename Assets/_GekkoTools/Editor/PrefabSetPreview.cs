using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gekko.Tools.EditorTools
{
    /// <summary>
    /// Preview 3D de un <see cref="PrefabSet"/>.
    ///
    /// No instancia nada en la escena: usa PreviewRenderUtility, que renderiza en una
    /// escena aislada y descartable. Por eso se pueden separar, aislar y reordenar los
    /// prefabs sin tocar ni el prefab original ni la escena abierta.
    ///
    /// La separacion no es una distancia absoluta sino un multiplicador del tamano real
    /// de las piezas: con separacion 1 quedan pegadas sin superponerse, y de ahi para
    /// arriba se abren. Asi el control se comporta igual con un helecho de 20 cm que con
    /// un arbol de 8 m.
    /// </summary>
    public class PrefabSetPreview
    {
        public enum Layout
        {
            Fila,
            Grilla,
            Circulo,
        }

        private struct Part
        {
            public Mesh Mesh;
            public Material[] Materials;
            public Matrix4x4 LocalMatrix;
        }

        private class Item
        {
            public GameObject Prefab;
            public readonly List<Part> Parts = new List<Part>();
            public Bounds LocalBounds;
            public int VertexCount;
            public int MaterialCount;
        }

        private PreviewRenderUtility _preview;
        private readonly List<Item> _items = new List<Item>();

        private PrefabSet _cachedSet;
        private int _cachedRevision = -1;

        public Layout LayoutMode = Layout.Fila;
        public float Separation = 1.6f;
        public int IsolatedIndex = -1;

        private Vector2 _orbit = new Vector2(25f, -20f);
        private float _zoom = 1f;
        private Vector3 _panOffset;

        public int ItemCount => _items.Count;

        public int TotalVertices
        {
            get
            {
                int total = 0;
                foreach (Item item in _items)
                {
                    total += item.VertexCount;
                }
                return total;
            }
        }

        public string NameOf(int index)
        {
            return index >= 0 && index < _items.Count && _items[index].Prefab != null
                ? _items[index].Prefab.name
                : string.Empty;
        }

        public string StatsFor(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return string.Empty;
            }

            Item item = _items[index];
            Vector3 size = item.LocalBounds.size;
            return $"{item.VertexCount:N0} vertices - {item.MaterialCount} material(es) - " +
                   $"{size.x:0.00} x {size.y:0.00} x {size.z:0.00} m";
        }

        /// <summary>Fuerza a releer el set en el proximo dibujado.</summary>
        public void Invalidate()
        {
            _cachedRevision = -1;
        }

        public void Cleanup()
        {
            if (_preview != null)
            {
                _preview.Cleanup();
                _preview = null;
            }
        }

        public void ResetView()
        {
            _orbit = new Vector2(25f, -20f);
            _zoom = 1f;
            _panOffset = Vector3.zero;
        }

        public void Draw(Rect rect, PrefabSet set)
        {
            EnsurePreview();
            Rebuild(set);
            HandleInput(rect);

            if (_items.Count == 0)
            {
                EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));
                var centered = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
                GUI.Label(rect, "Sin prefabs con malla para previsualizar.\nAgregalos en la lista de abajo.", centered);
                return;
            }

            // IMGUI dibuja en dos pasadas. En la de Layout, GUILayoutUtility todavia no
            // sabe el ancho real y devuelve un rect de 0x0; pasarselo a BeginPreview hace
            // que PreviewRenderUtility intente crear una RenderTexture de ancho 0 y tire
            // "Texture must have width greater than 0" en cada repaint. El render 3D solo
            // tiene sentido en la pasada de Repaint.
            if (Event.current.type != EventType.Repaint || rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            List<int> visible = BuildVisibleList();
            List<Vector3> positions = BuildLayout(visible, out Bounds total);

            _preview.BeginPreview(rect, GUIStyle.none);
            FrameCamera(total, rect.width / Mathf.Max(rect.height, 1f));

            for (int i = 0; i < visible.Count; i++)
            {
                Item item = _items[visible[i]];
                Matrix4x4 placement = Matrix4x4.TRS(positions[i], Quaternion.identity, Vector3.one);

                foreach (Part part in item.Parts)
                {
                    for (int submesh = 0; submesh < part.Mesh.subMeshCount; submesh++)
                    {
                        Material material = submesh < part.Materials.Length
                            ? part.Materials[submesh]
                            : part.Materials[part.Materials.Length - 1];

                        if (material == null)
                        {
                            continue;
                        }

                        _preview.DrawMesh(part.Mesh, placement * part.LocalMatrix, material, submesh);
                    }
                }
            }

            _preview.camera.Render();
            Texture result = _preview.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            DrawOverlay(rect, visible);
        }

        private void DrawOverlay(Rect rect, List<int> visible)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            string label = IsolatedIndex >= 0
                ? $"Aislado: {NameOf(IsolatedIndex)}   |   {StatsFor(IsolatedIndex)}"
                : $"{visible.Count} prefabs   |   {TotalVertices:N0} vertices en total";

            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 18f, rect.width - 12f, 16f), label, style);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 16f),
                "arrastrar = orbitar   |   rueda = zoom   |   click medio = desplazar", style);
        }

        private void HandleInput(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _orbit.x += e.delta.x * 0.5f;
                _orbit.y = Mathf.Clamp(_orbit.y + e.delta.y * 0.5f, -85f, 85f);
                e.Use();
                GUI.changed = true;
            }
            else if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _panOffset += new Vector3(-e.delta.x, e.delta.y, 0f) * 0.01f * _zoom;
                e.Use();
                GUI.changed = true;
            }
            else if (e.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom * (1f + e.delta.y * 0.05f), 0.2f, 6f);
                e.Use();
                GUI.changed = true;
            }
        }

        private List<int> BuildVisibleList()
        {
            var visible = new List<int>();

            if (IsolatedIndex >= 0 && IsolatedIndex < _items.Count)
            {
                visible.Add(IsolatedIndex);
                return visible;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                visible.Add(i);
            }

            return visible;
        }

        /// <summary>
        /// Coloca cada pieza segun el modo elegido. El paso entre piezas sale del ancho
        /// real de la mas grande, asi la separacion se siente igual con objetos chicos y
        /// con objetos grandes.
        /// </summary>
        private List<Vector3> BuildLayout(List<int> visible, out Bounds total)
        {
            var positions = new List<Vector3>(visible.Count);
            total = new Bounds(Vector3.zero, Vector3.one * 0.01f);

            if (visible.Count == 0)
            {
                return positions;
            }

            float widest = 0.01f;
            foreach (int index in visible)
            {
                Vector3 size = _items[index].LocalBounds.size;
                widest = Mathf.Max(widest, Mathf.Max(size.x, size.z));
            }

            float step = widest * Separation;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(visible.Count)));
            int rows = Mathf.CeilToInt(visible.Count / (float)columns);

            // En fila el espaciado se calcula PAR A PAR, no con el ancho de la pieza mas
            // grande. Con un conjunto mixto (un tronco de 30 m y un hongo de 20 cm) un
            // paso global separaria todo 30 m y las piezas chicas quedarian invisibles.
            float[] lineOffsets = null;
            if (LayoutMode == Layout.Fila)
            {
                lineOffsets = new float[visible.Count];
                float cursor = 0f;
                for (int i = 0; i < visible.Count; i++)
                {
                    float half = Mathf.Max(_items[visible[i]].LocalBounds.size.x, 0.01f) * 0.5f;
                    if (i > 0)
                    {
                        float previousHalf = Mathf.Max(_items[visible[i - 1]].LocalBounds.size.x, 0.01f) * 0.5f;
                        cursor += (previousHalf + half) * Separation;
                    }
                    lineOffsets[i] = cursor;
                }

                // Se centra la fila entera en el origen.
                float center = lineOffsets[visible.Count - 1] * 0.5f;
                for (int i = 0; i < visible.Count; i++)
                {
                    lineOffsets[i] -= center;
                }
            }

            for (int i = 0; i < visible.Count; i++)
            {
                Vector3 position;

                if (LayoutMode == Layout.Grilla)
                {
                    int cx = i % columns;
                    int cz = i / columns;
                    position = new Vector3(
                        (cx - (columns - 1) * 0.5f) * step,
                        0f,
                        (cz - (rows - 1) * 0.5f) * step);
                }
                else if (LayoutMode == Layout.Circulo && visible.Count > 1)
                {
                    // El radio sale del perimetro que hace falta para que las piezas no
                    // se toquen, no de un numero fijo.
                    float radius = step * visible.Count / (2f * Mathf.PI);
                    float angle = i / (float)visible.Count * Mathf.PI * 2f;
                    position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                }
                else
                {
                    position = new Vector3(lineOffsets != null ? lineOffsets[i] : (i - (visible.Count - 1) * 0.5f) * step, 0f, 0f);
                }

                // Cada pieza se apoya sobre el piso de la preview.
                Bounds b = _items[visible[i]].LocalBounds;
                position.y -= b.min.y;
                positions.Add(position);

                var itemBounds = new Bounds(position + b.center - new Vector3(0f, b.min.y, 0f), b.size);
                if (i == 0)
                {
                    total = itemBounds;
                }
                else
                {
                    total.Encapsulate(itemBounds);
                }
            }

            return positions;
        }

        private void FrameCamera(Bounds target, float aspect)
        {
            float radius = Mathf.Max(target.extents.magnitude, 0.25f);

            // Se encuadra contra el eje mas exigente del rect. Con una preview ancha
            // manda el fov vertical; con una angosta y alta, el horizontal. Sin esto el
            // sujeto queda chico en la mitad de las proporciones de ventana.
            float verticalFov = _preview.camera.fieldOfView * Mathf.Deg2Rad;
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * Mathf.Max(aspect, 0.01f));

            float distanceVertical = radius / Mathf.Tan(verticalFov * 0.5f);
            float distanceHorizontal = radius / Mathf.Tan(horizontalFov * 0.5f);
            float distance = Mathf.Max(distanceVertical, distanceHorizontal) * 1.15f * _zoom;

            Quaternion rotation = Quaternion.Euler(-_orbit.y, _orbit.x, 0f);
            Vector3 focus = target.center + _panOffset * radius;

            _preview.camera.transform.position = focus + rotation * (Vector3.back * distance);
            _preview.camera.transform.rotation = rotation;
            _preview.camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
            _preview.camera.farClipPlane = distance * 10f;
        }

        private void EnsurePreview()
        {
            if (_preview != null)
            {
                return;
            }

            _preview = new PreviewRenderUtility();
            _preview.camera.fieldOfView = 30f;
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = new Color(0.17f, 0.18f, 0.20f);

            _preview.lights[0].intensity = 1.3f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            _preview.lights[0].color = new Color(1f, 0.97f, 0.9f);

            if (_preview.lights.Length > 1)
            {
                _preview.lights[1].intensity = 0.55f;
                _preview.lights[1].transform.rotation = Quaternion.Euler(20f, -120f, 0f);
                _preview.lights[1].color = new Color(0.7f, 0.8f, 1f);
            }

            _preview.ambientColor = new Color(0.28f, 0.30f, 0.34f);
        }

        /// <summary>
        /// Relee el set solo cuando cambio: recolectar las mallas de toda la jerarquia de
        /// cada prefab en cada repaint seria carisimo.
        /// </summary>
        private void Rebuild(PrefabSet set)
        {
            int revision = ComputeRevision(set);
            if (set == _cachedSet && revision == _cachedRevision)
            {
                return;
            }

            _cachedSet = set;
            _cachedRevision = revision;
            _items.Clear();

            if (set == null)
            {
                return;
            }

            foreach (PrefabSetEntry entry in set.Entries)
            {
                if (entry == null || entry.Prefab == null || !entry.Enabled)
                {
                    continue;
                }

                var item = new Item { Prefab = entry.Prefab };
                Transform root = entry.Prefab.transform;
                bool hasBounds = false;
                var materials = new HashSet<Material>();

                foreach (MeshFilter filter in entry.Prefab.GetComponentsInChildren<MeshFilter>(false))
                {
                    Mesh mesh = filter.sharedMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (mesh == null || renderer == null || renderer.sharedMaterials.Length == 0)
                    {
                        continue;
                    }

                    // El multiplicador de escala del set se hornea en la matriz de la
                    // pieza: el transform del prefab no se toca nunca.
                    Matrix4x4 local = Matrix4x4.Scale(Vector3.one * entry.ScaleMultiplier)
                                      * root.worldToLocalMatrix
                                      * filter.transform.localToWorldMatrix;

                    item.Parts.Add(new Part
                    {
                        Mesh = mesh,
                        Materials = renderer.sharedMaterials,
                        LocalMatrix = local,
                    });

                    item.VertexCount += mesh.vertexCount;

                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }

                    Bounds meshBounds = TransformBounds(mesh.bounds, local);
                    if (!hasBounds)
                    {
                        item.LocalBounds = meshBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        item.LocalBounds.Encapsulate(meshBounds);
                    }
                }

                if (!hasBounds)
                {
                    continue;
                }

                item.MaterialCount = materials.Count;
                _items.Add(item);
            }

            if (IsolatedIndex >= _items.Count)
            {
                IsolatedIndex = -1;
            }
        }

        /// <summary>
        /// Transforma una caja por una matriz sin leer los vertices de la malla, que
        /// exigiria que sea readable y ademas seria mucho mas caro.
        /// </summary>
        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;

            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));

            var size = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)) * 2f;

            return new Bounds(center, size);
        }

        /// <summary>Hash barato del contenido del set, para detectar cambios.</summary>
        private static int ComputeRevision(PrefabSet set)
        {
            if (set == null)
            {
                return 0;
            }

            int hash = 17;
            foreach (PrefabSetEntry entry in set.Entries)
            {
                hash = hash * 31 + (entry == null || entry.Prefab == null ? 0 : entry.Prefab.GetInstanceID());
                hash = hash * 31 + (entry != null && entry.Enabled ? 1 : 0);
                hash = hash * 31 + (entry == null ? 0 : entry.ScaleMultiplier.GetHashCode());
            }

            return hash;
        }
    }
}
