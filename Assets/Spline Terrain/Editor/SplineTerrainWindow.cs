using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// Central window of the tool: creates new terrains (choosing mode and start shape)
    /// and lists the terrains already present in the scene.
    /// </summary>
    public class SplineTerrainWindow : EditorWindow
    {
        private const string MaterialsFolder = "Assets/Spline Terrain/Materials";

        private SplineTerrainMode _mode = SplineTerrainMode.Internal;
        private SplineShapeFactory.StartShape _shape = SplineShapeFactory.StartShape.Circle;
        private float _size = 3f;
        private int _circleKnots = 12;
        private string _newName = "";
        private Vector2 _scroll;

        // Cached editor of the selected terrain, to edit its settings from the window.
        private SplineTerrain _selected;
        private SerializedObject _selectedSO;

        // --- Tab system ---
        // To add a new feature: add an entry in BuildTabs() with its title
        // and the method that draws it. Nothing else.
        private int _tab;
        private (string title, System.Action draw)[] _tabs;
        private string[] _tabTitles;

        private void BuildTabs()
        {
            if (_tabs != null) return;
            _tabs = new (string, System.Action)[]
            {
                ("Edit", DrawEditTab),
                ("Create", DrawCreateTab),
                ("Scene", DrawSceneTab),
                // ("Textures", DrawTexturesTab),   <- example of a future tab
            };
            _tabTitles = new string[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++) _tabTitles[i] = _tabs[i].title;
        }

        [MenuItem("Tools/Spline Terrain/Manager")]
        public static void Open()
        {
            var window = GetWindow<SplineTerrainWindow>("Spline Terrain");
            window.minSize = new Vector2(320, 360);
        }

        private void OnSelectionChange()
        {
            RefreshSelection();
            Repaint();
        }

        private void OnEnable() => RefreshSelection();

        private void RefreshSelection()
        {
            var go = Selection.activeGameObject;
            SplineTerrain found = go != null ? go.GetComponentInParent<SplineTerrain>() : null;
            if (found != _selected)
            {
                _selected = found;
                _selectedSO = found != null ? new SerializedObject(found) : null;
            }
        }

        private void OnGUI()
        {
            BuildTabs();

            _tab = GUILayout.Toolbar(_tab, _tabTitles, EditorStyles.toolbarButton);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _tabs[Mathf.Clamp(_tab, 0, _tabs.Length - 1)].draw();
            EditorGUILayout.EndScrollView();
        }

        // --- Tabs ---

        private void DrawEditTab()
        {
            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a terrain (or its top transform) to edit its parameters here.", MessageType.Info);
                return;
            }
            if (_selectedSO == null) _selectedSO = new SerializedObject(_selected);

            EditorGUILayout.LabelField($"Editing: {_selected.name}", EditorStyles.boldLabel);
            SplineTerrainGUI.Draw(_selectedSO, _selected, showReferences: false);
        }

        private void DrawCreateTab()
        {
            _newName = EditorGUILayout.TextField(new GUIContent("Name", "Empty = numbered default name."), _newName);
            _mode = (SplineTerrainMode)EditorGUILayout.EnumPopup("Mode", _mode);
            _shape = (SplineShapeFactory.StartShape)EditorGUILayout.EnumPopup("Start shape", _shape);
            _size = EditorGUILayout.FloatField("Size / Radius", Mathf.Max(0.1f, _size));
            if (_shape == SplineShapeFactory.StartShape.Circle)
                _circleKnots = EditorGUILayout.IntSlider("Circle knots", _circleKnots, 4, 32);

            if (_mode == SplineTerrainMode.Road && _shape != SplineShapeFactory.StartShape.Line)
                EditorGUILayout.HelpBox("Road mode is usually used with the Line shape (open spline).", MessageType.Info);
            if ((_mode == SplineTerrainMode.Internal || _mode == SplineTerrainMode.External) && _shape == SplineShapeFactory.StartShape.Line)
                EditorGUILayout.HelpBox("Internal/External need a closed spline; the line will be closed automatically.", MessageType.Warning);

            if (GUILayout.Button("Create new terrain", GUILayout.Height(28)))
                CreateTerrain();
        }

        private void CreateTerrain()
        {
            string name = string.IsNullOrWhiteSpace(_newName) ? GenerateDefaultName() : _newName.Trim();
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Spline Terrain");

            var container = Undo.AddComponent<SplineContainer>(go);
            SplineShapeFactory.Build(container.Spline, _shape, _size, _circleKnots);

            var terrain = Undo.AddComponent<SplineTerrain>(go);
            terrain.splineContainer = container;
            terrain.Settings.mode = _mode;

            // Default materials (if they exist in the folder).
            terrain.FloorMaterial = LoadMaterial("M_Floor");
            terrain.WallMaterial = LoadMaterial("M_Wall");
            terrain.EdgeMaterial = LoadMaterial("M_Edge");

            terrain.EnsureTopTransform();
            terrain.Regenerate();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.FrameLastActiveSceneView();
        }

        /// <summary>Numbered default name based on how many terrains exist in the scene.</summary>
        private static string GenerateDefaultName()
        {
            int count = Object.FindObjectsByType<SplineTerrain>(FindObjectsSortMode.None).Length;
            return $"Spline Terrain {count + 1}";
        }

        private void DrawSceneTab()
        {
            EditorGUILayout.LabelField("Terrains in the scene", EditorStyles.boldLabel);
            var terrains = Object.FindObjectsByType<SplineTerrain>(FindObjectsSortMode.None);
            if (terrains.Length == 0)
            {
                EditorGUILayout.HelpBox("There are no terrains in the scene yet.", MessageType.None);
                return;
            }

            foreach (var t in terrains)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // Editable name (rename with Undo).
                    EditorGUI.BeginChangeCheck();
                    string newName = EditorGUILayout.DelayedTextField(t.name);
                    if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(newName))
                    {
                        Undo.RecordObject(t.gameObject, "Rename terrain");
                        t.gameObject.name = newName;
                    }
                    EditorGUILayout.LabelField($"({t.Settings.mode})", GUILayout.Width(70));
                    if (GUILayout.Button("Sel", GUILayout.Width(40)))
                    {
                        Selection.activeGameObject = t.gameObject;
                        EditorGUIUtility.PingObject(t.gameObject);
                    }
                    if (GUILayout.Button("Regen", GUILayout.Width(55)))
                    {
                        t.Regenerate();
                    }
                }
            }
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");
        }
    }
}
