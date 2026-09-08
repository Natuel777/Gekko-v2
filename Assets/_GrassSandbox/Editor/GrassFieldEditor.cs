using System.IO;
using Gekko.GrassSandbox;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gekko.GrassSandbox.EditorTools
{
    /// <summary>
    /// Pincel de pasto para la vista de escena.
    ///
    /// El raycast va contra COLLIDERS (Physics.Raycast), no contra la geometria visible.
    /// Es a proposito: los chunks de pasto no tienen collider, asi que el pincel nunca
    /// puede plantar pasto arriba del pasto ya pintado — que es el problema clasico de
    /// usar HandleUtility.PlaceObject. A cambio, la superficie sobre la que pintes
    /// necesita tener collider (el Spline Terrain los hornea, asi que ya estan).
    /// </summary>
    [CustomEditor(typeof(GrassField))]
    public class GrassFieldEditor : UnityEditor.Editor
    {
        private const string DataFolder = "Assets/_GrassSandbox/Data";

        private static bool _paintMode;

        // Ajustes del pincel: estaticos para que sobrevivan al cambio de seleccion.
        private static float _brushRadius = 2.5f;
        private static float _density = 25f;
        private static Vector2 _heightRange = new Vector2(0.35f, 0.7f);
        private static Vector2 _widthRange = new Vector2(0.05f, 0.09f);
        private static float _maxSlope = 40f;
        private static LayerMask _paintLayers = ~0;

        private Vector3 _lastPaintPoint;
        private bool _strokeActive;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var field = (GrassField)target;

            EditorGUILayout.Space();
            DrawSummary(field);

            EditorGUILayout.Space();
            DrawBrushSettings();

            EditorGUILayout.Space();
            DrawButtons(field);
        }

        private static void DrawSummary(GrassField field)
        {
            int blades = field.BladeCount;
            long vertices = (long)blades * field.VerticesPerBlade;
            long triangles = (long)blades * field.TrianglesPerBlade;

            EditorGUILayout.HelpBox(
                $"{blades:N0} briznas en {field.ChunkCount} chunks\n" +
                $"{vertices:N0} vertices / {triangles:N0} triangulos\n" +
                $"{field.ChunkCount} draw calls como maximo (menos con culling)",
                MessageType.Info);

            if (field.Data == null)
            {
                EditorGUILayout.HelpBox(
                    "No hay asset de datos. Se crea solo la primera vez que pintes.",
                    MessageType.None);
            }
        }

        private void DrawBrushSettings()
        {
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(false))
            {
                GUI.backgroundColor = _paintMode ? new Color(0.5f, 1f, 0.5f) : Color.white;
                if (GUILayout.Button(_paintMode ? "Modo pintura: ACTIVO (Esc para salir)" : "Activar modo pintura",
                        GUILayout.Height(28f)))
                {
                    _paintMode = !_paintMode;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
            }

            if (_paintMode)
            {
                EditorGUILayout.HelpBox(
                    "Click y arrastrar: pintar.\n" +
                    "Shift + click: borrar.\n" +
                    "Ctrl + rueda: cambiar el radio.",
                    MessageType.None);
            }

            _brushRadius = EditorGUILayout.Slider("Radio", _brushRadius, 0.25f, 20f);
            _density = EditorGUILayout.Slider("Densidad (briznas/m2)", _density, 1f, 300f);
            _maxSlope = EditorGUILayout.Slider("Pendiente maxima", _maxSlope, 0f, 90f);

            EditorGUILayout.MinMaxSlider(
                new GUIContent("Alto de la brizna"),
                ref _heightRange.x, ref _heightRange.y, 0.05f, 2f);
            EditorGUILayout.LabelField(" ", $"{_heightRange.x:0.00} .. {_heightRange.y:0.00} m");

            EditorGUILayout.MinMaxSlider(
                new GUIContent("Ancho de la brizna"),
                ref _widthRange.x, ref _widthRange.y, 0.01f, 0.3f);
            EditorGUILayout.LabelField(" ", $"{_widthRange.x:0.00} .. {_widthRange.y:0.00} m");

            // MaskField trabaja con indices de la lista de capas mostradas, no con el
            // bitmask real: hay que convertir en los dos sentidos o las capas quedan
            // corridas respecto de lo que se ve marcado.
            int displayed = UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(_paintLayers);
            displayed = EditorGUILayout.MaskField(
                "Capas pintables",
                displayed,
                UnityEditorInternal.InternalEditorUtility.layers);
            _paintLayers = UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(displayed);
        }

        private void DrawButtons(GrassField field)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild", GUILayout.Height(26f)))
                {
                    field.Rebuild();
                }

                using (new EditorGUI.DisabledScope(field.Data == null || field.Data.Count == 0))
                {
                    if (GUILayout.Button("Borrar todo", GUILayout.Height(26f)))
                    {
                        if (EditorUtility.DisplayDialog(
                                "Borrar todo el pasto",
                                $"Se van a borrar las {field.BladeCount:N0} briznas pintadas. No se puede deshacer con Ctrl+Z despues de guardar.",
                                "Borrar", "Cancelar"))
                        {
                            Undo.RecordObject(field.Data, "Borrar pasto");
                            field.Data.Clear();
                            EditorUtility.SetDirty(field.Data);
                            field.Rebuild();
                        }
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            if (!_paintMode)
            {
                return;
            }

            var field = (GrassField)target;
            Event e = Event.current;

            // Se toma el control por defecto para que el click no seleccione objetos.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _paintMode = false;
                Repaint();
                e.Use();
                return;
            }

            if (e.type == EventType.ScrollWheel && e.control)
            {
                _brushRadius = Mathf.Clamp(_brushRadius - e.delta.y * 0.1f, 0.25f, 20f);
                Repaint();
                e.Use();
                return;
            }

            if (!TryRaycast(e.mousePosition, out Vector3 point, out Vector3 normal))
            {
                return;
            }

            DrawBrushGizmo(point, normal, e.shift);

            bool isPaintEvent = (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                                && e.button == 0
                                && !e.alt;

            if (isPaintEvent)
            {
                // Se pinta cada cierto avance del cursor, no en cada evento del mouse:
                // si no, arrastrar despacio apila cientos de briznas en el mismo punto.
                float step = _brushRadius * 0.35f;
                bool farEnough = !_strokeActive || (point - _lastPaintPoint).sqrMagnitude > step * step;

                if (e.type == EventType.MouseDown)
                {
                    _strokeActive = true;
                    farEnough = true;
                }

                if (farEnough)
                {
                    if (e.shift)
                    {
                        Erase(field, point);
                    }
                    else
                    {
                        Paint(field, point, step);
                    }

                    _lastPaintPoint = point;
                }

                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _strokeActive = false;
                // El rebuild se hace al soltar, no en cada paso: reconstruir todos los
                // chunks en cada movimiento del mouse haria el pincel inusable.
                field.Rebuild();
                Repaint();
            }

            SceneView.RepaintAll();
        }

        private static bool TryRaycast(Vector2 guiPosition, out Vector3 point, out Vector3 normal)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, _paintLayers, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            point = Vector3.zero;
            normal = Vector3.up;
            return false;
        }

        private void DrawBrushGizmo(Vector3 point, Vector3 normal, bool erasing)
        {
            Handles.color = erasing
                ? new Color(1f, 0.4f, 0.35f, 1f)
                : new Color(0.45f, 1f, 0.5f, 1f);

            Handles.DrawWireDisc(point, normal, _brushRadius);
            Handles.DrawWireDisc(point, normal, _brushRadius * 0.5f);
        }

        private void Paint(GrassField field, Vector3 center, float step)
        {
            GrassData data = EnsureData(field);
            if (data == null)
            {
                return;
            }

            // Cantidad proporcional al area barrida por el pincel en este paso, asi la
            // densidad resultante no depende de que tan rapido arrastres el mouse.
            float sweptArea = 2f * _brushRadius * step;
            int count = Mathf.Max(1, Mathf.RoundToInt(_density * sweptArea));

            Undo.RecordObject(data, "Pintar pasto");

            Transform fieldTransform = field.transform;
            float cosLimit = Mathf.Cos(_maxSlope * Mathf.Deg2Rad);
            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                Vector2 disc = Random.insideUnitCircle * _brushRadius;
                Vector3 probeOrigin = center + new Vector3(disc.x, 0f, disc.y) + Vector3.up * (_brushRadius + 1f);

                // Se vuelve a tirar un rayo por brizna: apoyarla en el plano del primer
                // impacto la dejaria flotando o enterrada en cuanto el piso no sea plano.
                if (!Physics.Raycast(probeOrigin, Vector3.down, out RaycastHit hit,
                        _brushRadius * 2f + 10f, _paintLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if ((hit.point - center).sqrMagnitude > _brushRadius * _brushRadius)
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal, Vector3.up) < cosLimit)
                {
                    continue;
                }

                data.Blades.Add(new GrassBladeData
                {
                    Position = fieldTransform.InverseTransformPoint(hit.point),
                    Normal = fieldTransform.InverseTransformDirection(hit.normal),
                    Height = Random.Range(_heightRange.x, _heightRange.y),
                    Width = Random.Range(_widthRange.x, _widthRange.y),
                    Yaw = Random.Range(0f, 360f),
                    Variation = Random.value,
                });

                placed++;
            }

            if (placed > 0)
            {
                EditorUtility.SetDirty(data);
            }
        }

        private void Erase(GrassField field, Vector3 center)
        {
            GrassData data = field.Data;
            if (data == null || data.Count == 0)
            {
                return;
            }

            Undo.RecordObject(data, "Borrar pasto");

            Transform fieldTransform = field.transform;
            float sqrRadius = _brushRadius * _brushRadius;
            int removed = 0;

            for (int i = data.Blades.Count - 1; i >= 0; i--)
            {
                Vector3 worldPosition = fieldTransform.TransformPoint(data.Blades[i].Position);
                if ((worldPosition - center).sqrMagnitude <= sqrRadius)
                {
                    data.Blades.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>
        /// Devuelve el asset de datos del campo, creandolo la primera vez. Se crea solo
        /// para que pintar no exija un paso previo de setup.
        /// </summary>
        private static GrassData EnsureData(GrassField field)
        {
            if (field.Data != null)
            {
                return field.Data;
            }

            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
                AssetDatabase.Refresh();
            }

            Scene scene = field.gameObject.scene;
            string sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{sceneName}_{field.name}_Grass.asset");

            var data = ScriptableObject.CreateInstance<GrassData>();
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(field);
            serialized.FindProperty("_data").objectReferenceValue = data;
            serialized.ApplyModifiedProperties();

            Debug.Log($"[GrassField] Asset de datos creado en {path}.", data);
            return data;
        }
    }
}
