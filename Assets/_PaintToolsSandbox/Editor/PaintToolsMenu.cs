using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gekko.PaintTools.EditorTools
{
    /// <summary>
    /// Atajos de setup para las dos herramientas.
    /// </summary>
    public static class PaintToolsMenu
    {
        private const string ShaderName = "Gekko/Path Blend";
        private const string MaterialFolder = "Assets/_PaintToolsSandbox/Materials";

        [MenuItem("Tools/Gekko/Paint Tools/Crear material de piso con camino", priority = 20)]
        public static void CreatePathMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[PaintTools] No se encontro el shader '{ShaderName}'. Revisa que compile sin errores.");
                return;
            }

            if (!Directory.Exists(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            var material = new Material(shader) { name = "M_GroundPath" };

            material.SetColor("_BaseColor", new Color(0.33f, 0.54f, 0.27f));
            material.SetFloat("_BaseTiling", 0.25f);
            material.SetColor("_PathColor", new Color(0.87f, 0.81f, 0.56f));
            material.SetFloat("_PathTiling", 0.25f);
            material.SetFloat("_TintStrength", 1f);

            material.SetFloat("_EdgeSharpness", 0.72f);
            material.SetFloat("_EdgeNoiseScale", 2.5f);
            material.SetFloat("_EdgeNoiseStrength", 0.35f);

            material.SetColor("_ShadowTint", new Color(0.34f, 0.42f, 0.55f));
            material.SetFloat("_LightWrap", 0.4f);
            material.SetFloat("_BandSmooth", 0.25f);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/M_GroundPath.mat");
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[PaintTools] Material creado en {path}. Asignale tus texturas de base y de camino.");
        }

        [MenuItem("Tools/Gekko/Paint Tools/Crear zona de camino", priority = 21)]
        public static void CreatePathCanvas()
        {
            var go = new GameObject("PathCanvas");
            go.AddComponent<PathCanvas>();

            Undo.RegisterCreatedObjectUndo(go, "Crear zona de camino");
            Selection.activeGameObject = go;

            Debug.Log("[PaintTools] PathCanvas creado. Ponelo sobre la zona, ajustale el tamaño, " +
                      "asignale los renderers del piso y creá la máscara.");
        }

        [MenuItem("Tools/Gekko/Paint Tools/Crear campo de props", priority = 22)]
        public static void CreateScatterField()
        {
            var go = new GameObject("ScatterField");
            go.AddComponent<ScatterField>();

            Undo.RegisterCreatedObjectUndo(go, "Crear campo de props");
            Selection.activeGameObject = go;

            Debug.Log("[PaintTools] ScatterField creado. Activá el modo pintura y cargale prefabs al asset de datos.");
        }
    }
}
