using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gekko.CloudsSandbox.EditorTools
{
    /// <summary>
    /// Inspector de <see cref="CloudField"/>.
    ///
    /// Hace dos cosas que el inspector por defecto no puede: oculta los campos del
    /// modo que no esta activo (si no, se ven a la vez "Cantidad" y "Separacion" y no
    /// queda claro cual manda), y muestra el resultado de resolver el modo — cantidad,
    /// area y separacion — ANTES de apretar Rebuild.
    /// </summary>
    [CustomEditor(typeof(CloudField))]
    public class CloudFieldEditor : UnityEditor.Editor
    {
        private static readonly string[] CountModeOnly = { "_count", "_area" };
        private static readonly string[] SpacingModeOnly = { "_gridSize", "_spacing", "_fillRatio" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var field = (CloudField)target;
            bool byCount = field.Mode == CloudField.DistributionMode.PorCantidad;

            var hidden = new HashSet<string>(byCount ? SpacingModeOnly : CountModeOnly);

            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                if (hidden.Contains(property.propertyPath))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawSummary(field);
            EditorGUILayout.Space();
            DrawButtons(field);
        }

        private static void DrawSummary(CloudField field)
        {
            CloudField.Layout layout = field.ResolveLayout();
            Vector2 extent = layout.Extent;
            int count = Mathf.Min(layout.Count, layout.TotalCells);

            EditorGUILayout.HelpBox(
                $"{count} nubes\n" +
                $"Area cubierta: {extent.x:0} x {extent.y:0} unidades\n" +
                $"Separacion: {layout.SpacingX:0.0} x {layout.SpacingZ:0.0}  " +
                $"(diametro promedio de nube: {field.AverageDiameter:0.0})",
                MessageType.Info);

            float minSpacing = Mathf.Min(layout.SpacingX, layout.SpacingZ);
            if (minSpacing > field.AverageDiameter)
            {
                string fix = field.Mode == CloudField.DistributionMode.PorCantidad
                    ? "Subí la cantidad, achicá el área, o subí la escala de las nubes."
                    : "Bajá la separación o subí la escala de las nubes.";

                EditorGUILayout.HelpBox(
                    "Las nubes quedan mas separadas que su propio diametro: van a leerse " +
                    "sueltas en vez de formar un mar continuo. " + fix,
                    MessageType.Warning);
            }
        }

        private static void DrawButtons(CloudField field)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(field.gameObject, "Rebuild campo de nubes");
                    field.Rebuild();
                }

                if (GUILayout.Button("Clear", GUILayout.Height(26f)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(field.gameObject, "Limpiar campo de nubes");
                    field.Clear();
                }
            }
        }
    }
}
