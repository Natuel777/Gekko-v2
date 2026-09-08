using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gekko.CloudsSandbox.EditorTools
{
    /// <summary>
    /// Atajos de setup. El material se crea por codigo en vez de commitear un .mat
    /// a mano: asi los valores por defecto quedan documentados aca y no hay que
    /// escribir YAML de Unity con GUIDs a mano.
    /// </summary>
    public static class CloudSetupMenu
    {
        private const string ShaderName = "Gekko/Stylized Cloud";
        private const string MaterialFolder = "Assets/_CloudsSandbox/Materials";
        private const string MaterialPath = MaterialFolder + "/M_StylizedCloud.mat";

        [MenuItem("Tools/Gekko/Clouds/Crear material de nube", priority = 20)]
        public static void CreateCloudMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Clouds] No se encontro el shader '{ShaderName}'. Revisa que compile sin errores.");
                return;
            }

            if (!Directory.Exists(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            var material = new Material(shader) { name = "M_StylizedCloud" };

            // Preset de partida: cielo diurno, luz calida desde arriba.
            material.SetColor("_TopColor", new Color(1f, 0.99f, 0.96f));
            material.SetColor("_ShadeColor", new Color(0.66f, 0.72f, 0.87f));
            material.SetColor("_BottomColor", new Color(0.47f, 0.55f, 0.75f));
            material.SetColor("_RimColor", new Color(1f, 0.93f, 0.8f));

            material.SetFloat("_NoiseScale", 1.6f);
            material.SetVector("_RollDirection", new Vector4(1f, 0.15f, 0.3f, 0f));
            material.SetFloat("_RollSpeed", 0.08f);
            material.SetFloat("_Displacement", 0.18f);
            material.SetFloat("_DetailScale", 2.5f);

            material.SetFloat("_Solidity", 1.15f);
            material.SetFloat("_NoiseInfluence", 0.75f);
            material.SetFloat("_Cutoff", 0.35f);
            material.SetFloat("_EdgeSoftness", 0.14f);

            material.SetFloat("_LightWrap", 0.65f);
            material.SetFloat("_ShadeThreshold", 0.5f);
            material.SetFloat("_ShadeSmooth", 0.18f);
            material.SetFloat("_NoiseShading", 0.35f);
            material.SetFloat("_HeightScale", 1f);
            material.SetFloat("_HeightOffset", 0.45f);
            material.SetFloat("_RimPower", 3f);
            material.SetFloat("_RimStrength", 0.55f);
            material.SetFloat("_LightTint", 0.5f);

            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            string path = AssetDatabase.GenerateUniqueAssetPath(MaterialPath);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[Clouds] Material creado en {path}.");
        }

        [MenuItem("Tools/Gekko/Clouds/Crear campo de nubes en la escena", priority = 21)]
        public static void CreateCloudFieldInScene()
        {
            var go = new GameObject("CloudField");
            go.transform.position = new Vector3(0f, 25f, 0f);
            go.AddComponent<CloudField>();

            Undo.RegisterCreatedObjectUndo(go, "Crear campo de nubes");
            Selection.activeGameObject = go;

            Debug.Log("[Clouds] CloudField creado. Asignale las mallas y el material, y usa Rebuild en el menu contextual del componente.");
        }
    }
}
