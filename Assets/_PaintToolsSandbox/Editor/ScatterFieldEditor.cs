using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Pincel de dispersion de props, equivalente al "scatter prefabs on meshes" de
    /// Polybrush pero guardando datos en vez de GameObjects.
    /// </summary>
    [CustomEditor(typeof(ScatterField))]
    public class ScatterFieldEditor : UnityEditor.Editor
    {
        private const string DataFolder = "Assets/_PaintToolsSandbox/Data";
        private const int MaxCountForSpacingCheck = 20000;

        private static readonly SceneBrush Brush = new SceneBrush { Radius = 4f };

        private static float _density = 0.4f;
        private static Vector2 _scaleRange = new Vector2(0.8f, 1.4f);
        private static float _minSpacing = 0.6f;
        private static float _maxSlope = 45f;
        private static bool _alignToNormal = true;
        private static float _normalAlign = 0.5f;
        private static float _surfaceOffset;
        private static readonly List<bool> ActivePrototypes = new List<bool>();
        private static int _selectedPrototype;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var field = (ScatterField)target;

            EditorGUILayout.Space();
            DrawSummary(field);

            EditorGUILayout.Space();
            Brush.DrawToggleButton("Modo pintura: ACTIVO (Esc para salir)", "Activar modo pintura");

            if (Brush.Enabled)
            {
                EditorGUILayout.HelpBox(
                    "Click y arrastrar: dispersar.\nShift + click: borrar.\nCtrl + rueda: radio.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            DrawBrushSettings();

            EditorGUILayout.Space();
            DrawPrototypePalette(field);

            EditorGUILayout.Space();
            DrawButtons(field);
        }

        private static void DrawSummary(ScatterField field)
        {
            EditorGUILayout.HelpBox(
                $"{field.InstanceCount:N0} props en {field.ChunkCount} chunks\n" +
                $"{field.BatchCount} lotes instanciados (máximo de draw calls, menos con culling)\n" +
                $"~{field.InstanceCount * 40 / 1024f:N0} KB de datos",
                MessageType.Info);
        }

        private static void DrawBrushSettings()
        {
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);

            Brush.DrawCommonSettings();
            _density = EditorGUILayout.Slider("Densidad (props/m2)", _density, 0.01f, 10f);
            _minSpacing = EditorGUILayout.Slider("Separación mínima", _minSpacing, 0f, 10f);
            _maxSlope = EditorGUILayout.Slider("Pendiente máxima", _maxSlope, 0f, 90f);

            EditorGUILayout.MinMaxSlider(
                new GUIContent("Escala"), ref _scaleRange.x, ref _scaleRange.y, 0.05f, 5f);
            EditorGUILayout.LabelField(" ", $"{_scaleRange.x:0.00}x .. {_scaleRange.y:0.00}x");

            _alignToNormal = EditorGUILayout.Toggle("Alinear a la superficie", _alignToNormal);
            if (_alignToNormal)
            {
                _normalAlign = EditorGUILayout.Slider(
                    new GUIContent("Cuánto se alinea", "0 = siempre vertical, 1 = perpendicular al piso."),
                    _normalAlign, 0f, 1f);
            }

            _surfaceOffset = EditorGUILayout.FloatField(
                new GUIContent("Offset vertical", "Para hundir un poco la base y que no flote."),
                _surfaceOffset);
        }

        private void DrawPrototypePalette(ScatterField field)
        {
            EditorGUILayout.LabelField("Prefabs a pintar", EditorStyles.boldLabel);

            ScatterData data = field.Data;
            if (data == null)
            {
                EditorGUILayout.HelpBox("Se crea el asset de datos la primera vez que pintes.", MessageType.None);
                return;
            }

            if (data.Prototypes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No hay prefabs cargados. Agregalos en la lista 'Prototypes' del asset de datos.",
                    MessageType.Warning);

                if (GUILayout.Button("Seleccionar el asset de datos"))
                {
                    Selection.activeObject = data;
                }
                return;
            }

            SyncActiveList(data.Prototypes.Count);

            for (int i = 0; i < data.Prototypes.Count; i++)
            {
                GameObject prototype = data.Prototypes[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    ActivePrototypes[i] = EditorGUILayout.Toggle(ActivePrototypes[i], GUILayout.Width(18f));

                    bool isSelected = i == _selectedPrototype;
                    if (GUILayout.Toggle(isSelected, prototype != null ? prototype.name : "(vacío)",
                            EditorStyles.miniButton) && !isSelected)
                    {
                        _selectedPrototype = i;
                    }
                }
            }

            DrawInstancingWarning(data);
        }

        private static void DrawInstancingWarning(ScatterData data)
        {
            var offenders = new List<string>();

            foreach (GameObject prototype in data.Prototypes)
            {
                if (prototype == null)
                {
                    continue;
                }

                foreach (MeshRenderer renderer in prototype.GetComponentsInChildren<MeshRenderer>(false))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && !material.enableInstancing && !offenders.Contains(material.name))
                        {
                            offenders.Add(material.name);
                        }
                    }
                }
            }

            if (offenders.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Estos materiales no tienen GPU Instancing activado, así que cada prop va a ser " +
                "su propio draw call y se pierde toda la ventaja:\n" + string.Join(", ", offenders),
                MessageType.Warning);

            if (GUILayout.Button("Activar GPU Instancing en esos materiales"))
            {
                foreach (GameObject prototype in data.Prototypes)
                {
                    if (prototype == null)
                    {
                        continue;
                    }

                    foreach (MeshRenderer renderer in prototype.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (material != null && !material.enableInstancing)
                            {
                                material.enableInstancing = true;
                                EditorUtility.SetDirty(material);
                            }
                        }
                    }
                }

                AssetDatabase.SaveAssets();
            }
        }

        private void DrawButtons(ScatterField field)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild", GUILayout.Height(24f)))
                {
                    field.Rebuild();
                }

                using (new EditorGUI.DisabledScope(field.Data == null || field.Data.Count == 0))
                {
                    if (GUILayout.Button("Borrar todo", GUILayout.Height(24f)))
                    {
                        if (EditorUtility.DisplayDialog("Borrar todo",
                                $"Se borran los {field.InstanceCount:N0} props dispersos.", "Borrar", "Cancelar"))
                        {
                            Undo.RecordObject(field.Data, "Borrar props");
                            field.Data.Clear();
                            EditorUtility.SetDirty(field.Data);
                            field.Rebuild();
                        }
                    }
                }
            }

            using (new EditorGUI.DisabledScope(field.Data == null || field.Data.Count == 0))
            {
                if (GUILayout.Button("Materializar el prefab seleccionado (para colliders)"))
                {
                    Materialize(field, _selectedPrototype);
                }
            }

            EditorGUILayout.HelpBox(
                "Materializar convierte esos props en GameObjects reales, con sus colliders y scripts. " +
                "Se sacan de los datos para no dibujarlos dos veces. Usalo solo con los que lo necesiten: " +
                "es exactamente el peso que este sistema evita.",
                MessageType.None);
        }

        private static void SyncActiveList(int count)
        {
            while (ActivePrototypes.Count < count)
            {
                ActivePrototypes.Add(true);
            }
            while (ActivePrototypes.Count > count)
            {
                ActivePrototypes.RemoveAt(ActivePrototypes.Count - 1);
            }
        }

        private void OnSceneGUI()
        {
            var field = (ScatterField)target;
            Event e = Event.current;

            SceneBrush.Action action = Brush.Update(e, out Vector3 point, out Vector3 normal, out bool cursorValid);

            if (Brush.Enabled && cursorValid)
            {
                Brush.DrawCursor(point, normal, e.shift);
                SceneView.RepaintAll();
            }

            switch (action)
            {
                case SceneBrush.Action.Paint:
                    Scatter(field, point);
                    break;

                case SceneBrush.Action.Erase:
                    Erase(field, point);
                    break;

                case SceneBrush.Action.StrokeEnded:
                    field.Rebuild();
                    Repaint();
                    break;
            }
        }

        private void Scatter(ScatterField field, Vector3 center)
        {
            ScatterData data = EnsureData(field);
            if (data == null || data.Prototypes.Count == 0)
            {
                return;
            }

            SyncActiveList(data.Prototypes.Count);

            var candidates = new List<int>();
            for (int i = 0; i < data.Prototypes.Count; i++)
            {
                if (ActivePrototypes[i] && data.Prototypes[i] != null)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            // Cantidad proporcional al area barrida, para que la densidad no dependa de
            // que tan rapido arrastres el mouse.
            float sweptArea = 2f * Brush.Radius * Brush.StepDistance;
            int attempts = Mathf.Max(1, Mathf.RoundToInt(_density * sweptArea));

            Undo.RecordObject(data, "Dispersar props");

            Transform fieldTransform = field.transform;
            float cosLimit = Mathf.Cos(_maxSlope * Mathf.Deg2Rad);
            float sqrSpacing = _minSpacing * _minSpacing;
            bool checkSpacing = _minSpacing > 0f && data.Count < MaxCountForSpacingCheck;
            int placed = 0;

            for (int i = 0; i < attempts; i++)
            {
                Vector2 disc = Random.insideUnitCircle * Brush.Radius;
                Vector3 origin = center + new Vector3(disc.x, 0f, disc.y) + Vector3.up * (Brush.Radius + 1f);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                        Brush.Radius * 2f + 10f, Brush.Layers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if ((hit.point - center).sqrMagnitude > Brush.Radius * Brush.Radius)
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal, Vector3.up) < cosLimit)
                {
                    continue;
                }

                if (checkSpacing && IsTooClose(data, fieldTransform, hit.point, sqrSpacing))
                {
                    continue;
                }

                Vector3 up = _alignToNormal
                    ? Vector3.Slerp(Vector3.up, hit.normal, _normalAlign).normalized
                    : Vector3.up;

                Quaternion rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), up)
                                      * Quaternion.FromToRotation(Vector3.up, up);

                float scale = Random.Range(_scaleRange.x, _scaleRange.y);
                Vector3 worldPosition = hit.point + up * _surfaceOffset;

                data.Instances.Add(new ScatterInstance
                {
                    PrototypeIndex = candidates[Random.Range(0, candidates.Count)],
                    Position = fieldTransform.InverseTransformPoint(worldPosition),
                    Rotation = Quaternion.Inverse(fieldTransform.rotation) * rotation,
                    Scale = Vector3.one * scale,
                });

                placed++;
            }

            if (placed > 0)
            {
                EditorUtility.SetDirty(data);
            }
        }

        private static bool IsTooClose(ScatterData data, Transform fieldTransform, Vector3 worldPoint, float sqrSpacing)
        {
            foreach (ScatterInstance instance in data.Instances)
            {
                Vector3 existing = fieldTransform.TransformPoint(instance.Position);
                if ((existing - worldPoint).sqrMagnitude < sqrSpacing)
                {
                    return true;
                }
            }
            return false;
        }

        private void Erase(ScatterField field, Vector3 center)
        {
            ScatterData data = field.Data;
            if (data == null || data.Count == 0)
            {
                return;
            }

            Undo.RecordObject(data, "Borrar props");

            Transform fieldTransform = field.transform;
            float sqrRadius = Brush.Radius * Brush.Radius;
            int removed = 0;

            for (int i = data.Instances.Count - 1; i >= 0; i--)
            {
                Vector3 worldPosition = fieldTransform.TransformPoint(data.Instances[i].Position);
                if ((worldPosition - center).sqrMagnitude <= sqrRadius)
                {
                    data.Instances.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(data);
            }
        }

        private static void Materialize(ScatterField field, int prototypeIndex)
        {
            ScatterData data = field.Data;
            if (data == null || prototypeIndex < 0 || prototypeIndex >= data.Prototypes.Count)
            {
                return;
            }

            GameObject prototype = data.Prototypes[prototypeIndex];
            if (prototype == null)
            {
                return;
            }

            var toMaterialize = new List<ScatterInstance>();
            foreach (ScatterInstance instance in data.Instances)
            {
                if (instance.PrototypeIndex == prototypeIndex)
                {
                    toMaterialize.Add(instance);
                }
            }

            if (toMaterialize.Count == 0)
            {
                Debug.Log("[ScatterField] No hay props de ese prefab para materializar.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Materializar",
                    $"Se van a crear {toMaterialize.Count:N0} GameObjects reales de '{prototype.name}' " +
                    "y se sacan de los datos.\n\nEsto suma peso a la escena: hacelo solo con los que necesiten collider.",
                    "Materializar", "Cancelar"))
            {
                return;
            }

            var parent = new GameObject($"Materialized_{prototype.name}");
            parent.transform.SetParent(field.transform, false);
            Undo.RegisterCreatedObjectUndo(parent, "Materializar props");

            foreach (ScatterInstance instance in toMaterialize)
            {
                var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prototype, parent.transform);
                spawned.transform.localPosition = instance.Position;
                spawned.transform.localRotation = instance.Rotation;
                spawned.transform.localScale = instance.Scale;
            }

            Undo.RecordObject(data, "Materializar props");
            data.Instances.RemoveAll(instance => instance.PrototypeIndex == prototypeIndex);
            EditorUtility.SetDirty(data);

            field.Rebuild();
            Debug.Log($"[ScatterField] {toMaterialize.Count:N0} props materializados.");
        }

        private static ScatterData EnsureData(ScatterField field)
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
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{sceneName}_{field.name}_Scatter.asset");

            var data = ScriptableObject.CreateInstance<ScatterData>();
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            var serialized = new SerializedObject(field);
            serialized.FindProperty("_data").objectReferenceValue = data;
            serialized.ApplyModifiedProperties();

            Debug.Log($"[ScatterField] Asset de datos creado en {path}. Cargale los prefabs.", data);
            return data;
        }
    }
}
