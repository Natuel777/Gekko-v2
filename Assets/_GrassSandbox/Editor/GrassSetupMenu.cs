using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gekko.GrassSandbox.EditorTools
{
    /// <summary>
    /// Atajos de setup. El material se crea por codigo en vez de commitear un .mat a
    /// mano: los valores por defecto quedan documentados aca y no hay que escribir YAML
    /// de Unity con GUIDs.
    /// </summary>
    public static class GrassSetupMenu
    {
        private const string ShaderName = "Gekko/Grass";
        private const string MaterialFolder = "Assets/_GrassSandbox/Materials";
        private const string MaterialPath = MaterialFolder + "/M_Grass.mat";

        [MenuItem("Tools/Gekko/Grass/Crear material de pasto", priority = 20)]
        public static void CreateGrassMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Grass] No se encontro el shader '{ShaderName}'. Revisa que compile sin errores.");
                return;
            }

            if (!Directory.Exists(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            var material = new Material(shader) { name = "M_Grass" };

            // Preset de partida: verde de base oscuro a punta clara, con manchones
            // turquesa como los de la referencia.
            material.SetColor("_BottomColor", new Color(0.09f, 0.26f, 0.16f));
            material.SetColor("_TopColor", new Color(0.58f, 0.88f, 0.47f));
            material.SetFloat("_AmbientOcclusion", 0.45f);

            material.SetColor("_VariationColor", new Color(0.33f, 0.79f, 0.63f));
            material.SetFloat("_VariationScale", 0.04f);
            material.SetFloat("_VariationStrength", 0.45f);
            material.SetFloat("_TintRandom", 0.15f);

            material.SetVector("_WindDirection", new Vector4(1f, 0f, 0.35f, 0f));
            material.SetFloat("_WindScale", 0.12f);
            material.SetFloat("_WindSpeed", 0.6f);
            material.SetFloat("_WindStrength", 0.25f);
            material.SetFloat("_SwaySpeed", 2f);
            material.SetFloat("_SwayStrength", 0.04f);

            material.SetFloat("_PushStrength", 1f);
            material.SetFloat("_PushDown", 0.5f);

            material.SetColor("_ShadowTint", new Color(0.35f, 0.45f, 0.55f));
            material.SetFloat("_LightWrap", 0.5f);
            material.SetFloat("_Translucency", 0.6f);

            string path = AssetDatabase.GenerateUniqueAssetPath(MaterialPath);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[Grass] Material creado en {path}.");
        }

        [MenuItem("Tools/Gekko/Grass/Crear campo de pasto en la escena", priority = 21)]
        public static void CreateGrassFieldInScene()
        {
            var go = new GameObject("GrassField");
            go.AddComponent<GrassField>();

            Undo.RegisterCreatedObjectUndo(go, "Crear campo de pasto");
            Selection.activeGameObject = go;

            Debug.Log("[Grass] GrassField creado. Asignale el material y usa 'Activar modo pintura' para pintar.");
        }

        [MenuItem("Tools/Gekko/Grass/Agregar interactor a la seleccion", priority = 22)]
        public static void AddInteractorToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                Debug.LogWarning("[Grass] No hay nada seleccionado.");
                return;
            }

            int added = 0;
            foreach (GameObject go in selection)
            {
                if (go.GetComponent<GrassInteractor>() != null)
                {
                    continue;
                }

                Undo.AddComponent<GrassInteractor>(go);
                added++;
            }

            Debug.Log($"[Grass] {added} interactor(es) agregados.");
        }

        [MenuItem("Tools/Gekko/Grass/Agregar interactor a la seleccion", validate = true)]
        private static bool ValidateAddInteractor()
        {
            return Selection.gameObjects.Length > 0;
        }
    }
}
