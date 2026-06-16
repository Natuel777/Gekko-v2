using UnityEditor;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using SplineTerrainTool.Util;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="SplineTerrain"/>: uses the shared GUI and draws handles
    /// on the floor according to the active tool (Move / Rotate / Scale), and also regenerates
    /// in real time while editing the spline.
    /// </summary>
    [CustomEditor(typeof(SplineTerrain))]
    public class SplineTerrainInspector : UnityEditor.Editor
    {
        private SplineTerrain _terrain;

        private void OnEnable()
        {
            _terrain = (SplineTerrain)target;
            EditorSplineUtility.AfterSplineWasModified += OnSplineModified;
        }

        private void OnDisable()
        {
            EditorSplineUtility.AfterSplineWasModified -= OnSplineModified;
        }

        private void OnSplineModified(Spline spline)
        {
            if (_terrain == null || _terrain.splineContainer == null) return;
            if (spline != _terrain.splineContainer.Spline) return;
            _terrain.Regenerate();
            SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            SplineTerrainGUI.Draw(serializedObject, _terrain, showReferences: true);
        }

        private void OnSceneGUI()
        {
            if (_terrain == null) return;
            DrawOutlines();

            Transform top = _terrain.topTransform;
            if (top == null) return;

            // Handle according to Unity's active tool (W = Move, E = Rotate, R = Scale).
            switch (Tools.current)
            {
                case Tool.Move: MoveHandle(top); break;
                case Tool.Rotate: RotateHandle(top); break;
                case Tool.Scale: ScaleHandle(top); break;
                default: MoveHandle(top); break;
            }
        }

        private void MoveHandle(Transform top)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(top.position, top.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                RecordTopEdit(top, "Move floor");
                top.position = newPos;
                ApplyTopEdit();
            }
        }

        private void RotateHandle(Transform top)
        {
            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(top.rotation, top.position);
            if (EditorGUI.EndChangeCheck())
            {
                RecordTopEdit(top, "Rotate floor");
                top.rotation = newRot;
                ApplyTopEdit();
            }
        }

        private void ScaleHandle(Transform top)
        {
            EditorGUI.BeginChangeCheck();
            float size = HandleUtility.GetHandleSize(top.position);
            Vector3 newScale = Handles.ScaleHandle(top.localScale, top.position, top.rotation, size);
            if (EditorGUI.EndChangeCheck())
            {
                RecordTopEdit(top, "Scale floor");
                float uniform = Mathf.Max(0f, (newScale.x + newScale.z) * 0.5f);
                top.localScale = new Vector3(uniform, 1f, uniform);
                ApplyTopEdit();
            }
        }

        private void RecordTopEdit(Transform top, string label)
        {
            Undo.RecordObject(top, label);
            Undo.RecordObject(_terrain, label);
        }

        private void ApplyTopEdit()
        {
            _terrain.SyncFromTopTransform();
            _terrain.Regenerate();
        }

        /// <summary>Draws the base outline (green) and the top outline (cyan) as feedback.</summary>
        private void DrawOutlines()
        {
            var container = _terrain.splineContainer;
            if (container == null || container.Spline == null || container.Spline.Count < 2) return;

            int count = Mathf.Clamp(_terrain.Settings.segmentsPerSpline, 8, 96);
            SampledOutline outline = SplineSampler.SampleEvenly(container.Spline, count);
            Transform t = _terrain.transform;

            // Base.
            Handles.color = Color.green;
            DrawLoop(outline, t);

            // Top (only in modes with a raised floor): we apply the same matrix as the generator.
            if (_terrain.Settings.mode != SplineTerrainMode.Road)
            {
                Vector3 centroid = SplineSampler.Centroid(outline.Points);
                Matrix4x4 m = _terrain.Settings.BuildTopMatrix(centroid);
                Handles.color = Color.cyan;
                int n = outline.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = m.MultiplyPoint3x4(outline.Points[i]);
                    Vector3 b = m.MultiplyPoint3x4(outline.Points[(i + 1) % n]);
                    Handles.DrawLine(t.TransformPoint(a), t.TransformPoint(b));
                }
            }
        }

        private void DrawLoop(SampledOutline outline, Transform t)
        {
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = outline.Points[i];
                Vector3 b = outline.Points[(i + 1) % n];
                Handles.DrawLine(t.TransformPoint(a), t.TransformPoint(b));
            }
        }
    }
}
