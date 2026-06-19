using UnityEditor;
using UnityEngine;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// Shared drawing of the terrain parameters + bake section.
    /// Both the custom inspector and the tool window use it, so that most
    /// of the modifications can be made from either place.
    /// </summary>
    public static class SplineTerrainGUI
    {
        /// <summary>Draws all settings + materials + bake. Applies changes and regenerates.</summary>
        public static void Draw(SerializedObject so, SplineTerrain terrain, bool showReferences)
        {
            so.Update();
            SerializedProperty settings = so.FindProperty("settings");

            if (showReferences)
            {
                EditorGUILayout.PropertyField(so.FindProperty("splineContainer"));
                EditorGUILayout.PropertyField(so.FindProperty("topTransform"));
                EditorGUILayout.Space();
            }

            DrawProfileSection(so, terrain);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            SerializedProperty mode = settings.FindPropertyRelative("mode");
            EditorGUILayout.PropertyField(mode, new GUIContent("Mode"));

            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            Field(settings, "segmentsPerSpline", "Resolution (segments)");
            Field(settings, "colliderMatchesVisual", "Collider = visual shape");
            if (!settings.FindPropertyRelative("colliderMatchesVisual").boolValue)
                Field(settings, "colliderSimplify", "Collider extra simplify (perimeter)");
            Field(settings, "colliderSplit", "Collider split");
            Field(settings, "splineSmoothing", "Outline smoothing");
            if (settings.FindPropertyRelative("splineSmoothing").floatValue > 0f)
                Field(settings, "splineSmoothingIterations", "Smoothing iterations");

            var modeValue = (SplineTerrainMode)mode.enumValueIndex;
            switch (modeValue)
            {
                case SplineTerrainMode.Internal:
                    EditorGUILayout.LabelField("Floor", EditorStyles.boldLabel);
                    Field(settings, "topOffset", "Offset (XZ) / Height (Y)");
                    Field(settings, "topEuler", "Rotation (tilt)");
                    Field(settings, "topScale", "Floor scale (XZ)");
                    DrawWallSection(settings);
                    break;
                case SplineTerrainMode.Road:
                    EditorGUILayout.LabelField("Road", EditorStyles.boldLabel);
                    Field(settings, "roadWidth", "Width");
                    Field(settings, "topOffset", "Height (Y) / Offset");
                    DrawWallSection(settings);
                    break;
                case SplineTerrainMode.External:
                    EditorGUILayout.LabelField("Island", EditorStyles.boldLabel);
                    Field(settings, "topOffset", "Offset (XZ) / Height (Y)");
                    Field(settings, "topEuler", "Rotation (tilt)");
                    Field(settings, "topScale", "Scale (XZ)");
                    Field(settings, "outwardDistance", "Outward distance");
                    Field(settings, "noiseAmplitude", "Noise amplitude");
                    Field(settings, "noiseFrequency", "Noise frequency");
                    Field(settings, "noiseSeed", "Seed");
                    Field(settings, "externalInnerWall", "Inner wall (rim)");
                    DrawWallSection(settings);
                    break;
            }

            EditorGUILayout.LabelField("Visual mesh layout", EditorStyles.boldLabel);
            Field(settings, "floorGrid", "Dense floor grid (Polybrush)");
            if (settings.FindPropertyRelative("floorGrid").boolValue)
            {
                Field(settings, "floorGridStyle", "Grid style (Internal)");
                Field(settings, "floorCellSize", "Grid cell size");
            }
            Field(settings, "separateVisualMeshes", "Separate floor / walls meshes");

            EditorGUILayout.LabelField("UVs / Tiling", EditorStyles.boldLabel);
            Field(settings, "floorUvScale", "Floor UV scale");
            Field(settings, "wallUvScale", "Wall UV scale");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("floorMaterial"), new GUIContent("Floor"));
            EditorGUILayout.PropertyField(so.FindProperty("wallMaterial"), new GUIContent("Wall"));
            EditorGUILayout.PropertyField(so.FindProperty("edgeMaterial"), new GUIContent("Edge"));

            bool changed = EditorGUI.EndChangeCheck();
            so.ApplyModifiedProperties();

            if (changed && terrain != null)
            {
                terrain.Settings.Validate();
                terrain.ApplyToTopTransform();
                terrain.Regenerate();
            }

            EditorGUILayout.Space();
            DrawBakeSection(so, terrain);
        }

        private static void DrawWallSection(SerializedProperty settings)
        {
            EditorGUILayout.LabelField("Walls / Smoothing", EditorStyles.boldLabel);
            Field(settings, "wallHeightSegments", "Vertical subdivisions");
            Field(settings, "wallCurvature", "Curvature (bulge)");
            Field(settings, "smoothWalls", "Smooth shading");

            Field(settings, "bevelFloorEdges", "Wall-floor bevel");
            if (settings.FindPropertyRelative("bevelFloorEdges").boolValue)
            {
                Field(settings, "bevelSize", "Bevel size");
                Field(settings, "bevelSegments", "Bevel subdivisions");
                Field(settings, "bevelCurvature", "Curvature (+ outward / - inward)");
            }
        }

        /// <summary>Profiles section: assign, apply, save and export reusable profiles.</summary>
        private static void DrawProfileSection(SerializedObject so, SplineTerrain terrain)
        {
            EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("profile"), new GUIContent("Profile"));
            so.ApplyModifiedProperties();

            if (terrain == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(terrain.profile == null))
                {
                    if (GUILayout.Button("Apply profile"))
                    {
                        Undo.RecordObject(terrain, "Apply profile");
                        terrain.ApplyProfile();
                        EditorUtility.SetDirty(terrain);
                        so.Update();
                    }
                    if (GUILayout.Button("Save into profile"))
                        SplineTerrainProfileIO.SaveInto(terrain.profile, terrain);
                }
                if (GUILayout.Button("Export new"))
                {
                    SplineTerrainProfileIO.ExportNew(terrain);
                    so.Update();
                }
            }
        }

        private static void DrawBakeSection(SerializedObject so, SplineTerrain terrain)
        {
            if (terrain == null) return;

            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("bakedData"), new GUIContent("Baked data"));
            EditorGUILayout.PropertyField(so.FindProperty("bakeCollider"), new GUIContent("Include collider"));

            FolderField(so.FindProperty("meshSaveFolder"), "Meshes folder");
            FolderField(so.FindProperty("dataSaveFolder"), "SO folder (data)");
            so.ApplyModifiedProperties();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake"))
                    SplineTerrainBaker.Bake(terrain);

                using (new EditorGUI.DisabledScope(terrain.bakedData == null))
                {
                    if (GUILayout.Button("Load from SO"))
                        SplineTerrainBaker.LoadInto(terrain, terrain.bakedData);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate"))
                    terrain.Regenerate();

                if (GUILayout.Button("Rebuild colliders (preview)"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(terrain.gameObject, "Rebuild colliders");
                    terrain.RebuildCollidersPreview();
                }
            }
        }

        private static void Field(SerializedProperty settings, string relativeName, string label)
        {
            SerializedProperty p = settings.FindPropertyRelative(relativeName);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
        }

        /// <summary>Folder field (string) with a button to pick it and save it relative to Assets.</summary>
        private static void FolderField(SerializedProperty prop, string label)
        {
            if (prop == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(prop, new GUIContent(label));
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string abs = EditorUtility.OpenFolderPanel(label, Application.dataPath, "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        string rel = ToProjectRelative(abs);
                        if (rel != null) prop.stringValue = rel;
                        else EditorUtility.DisplayDialog("Invalid folder", "Pick a folder inside the project (Assets/...).", "Ok");
                    }
                }
            }
        }

        private static string ToProjectRelative(string absolutePath)
        {
            absolutePath = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (absolutePath == dataPath) return "Assets";
            if (absolutePath.StartsWith(dataPath + "/")) return "Assets" + absolutePath.Substring(dataPath.Length);
            return null;
        }
    }
}
