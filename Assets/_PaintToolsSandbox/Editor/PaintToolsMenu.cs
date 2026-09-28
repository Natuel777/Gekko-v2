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
            material.SetColor("_Tex1Color", new Color(0.87f, 0.81f, 0.56f));
            material.SetFloat("_Tex1Tiling", 0.25f);
            material.SetFloat("_TintStrength", 1f);

            material.SetFloat("_EdgeSharpness", 0.72f);
            material.SetFloat("_EdgeNoiseScale", 2.5f);
            material.SetFloat("_EdgeNoiseStrength", 0.35f);

            material.SetColor("_ShadowTint", new Color(0.34f, 0.42f, 0.55f));
            material.SetFloat("_LightWrap", 0.4f);
            material.SetFloat("_BandSmooth", 0.25f);

            // Triplanar prendido por defecto: casi ningun piso del juego es un plano
            // horizontal perfecto (SplineTerrain, rampas, paredes curvas), y con la
            // proyeccion cenital sola el tileado se estira en rayas apenas hay pendiente.
            material.SetFloat("_Triplanar", 1f);
            material.EnableKeyword("_TRIPLANAR_ON");

            string path = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/M_GroundPath.mat");
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[PaintTools] Material creado en {path}. Asignale tu Textura base y tu Textura 1. " +
                      "Las texturas 2/3/4 son opcionales: activalas con su toggle si querés pintar más de una.");
        }

        [MenuItem("Tools/Gekko/Paint Tools/Clonar variante de camino (con nuevas texturas)", priority = 20)]
        public static void ClonePathMaterialVariant()
        {
            // Pensado para "varias texturas de camino distintas": cada PathCanvas ya
            // usa su propio material, asi que la forma barata de tener una variante
            // nueva (piedra, arena, etc.) es clonar un material que ya este bien
            // ajustado y solo cambiarle las dos texturas, en vez de recrear a mano el
            // tiling/borde/luz desde cero.
            var source = Selection.activeObject as Material;
            if (source == null || source.shader == null || source.shader.name != ShaderName)
            {
                Debug.LogError(
                    $"[PaintTools] Seleccioná en el Project un material que use el shader '{ShaderName}' " +
                    "(por ejemplo uno ya creado con 'Crear material de piso con camino'). Ese es el que se " +
                    "clona como punto de partida de la variante nueva.");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(sourcePath);

            if (!AssetDatabase.CopyAsset(sourcePath, newPath))
            {
                Debug.LogError($"[PaintTools] No se pudo clonar '{sourcePath}'.");
                return;
            }

            AssetDatabase.SaveAssets();
            var clone = AssetDatabase.LoadAssetAtPath<Material>(newPath);

            Selection.activeObject = clone;
            EditorGUIUtility.PingObject(clone);
            Debug.Log(
                $"[PaintTools] Variante creada en {newPath}, con los mismos valores que '{source.name}' " +
                "(tiling, borde, luz, triplanar). Asignale ahora SU textura base y SU textura de camino " +
                "arriba en el inspector, y usala en un PathCanvas nuevo (una zona por variante).");
        }

        [MenuItem("Tools/Gekko/Paint Tools/Migrar materiales de camino viejos", priority = 23)]
        public static void MigrateLegacyPathMaterials()
        {
            // El shader paso de "base + camino" (_PathTex/_PathColor/_PathTiling) a base +
            // Textura 1..4. Unity no renombra propiedades: los materiales creados antes
            // conservan sus valores bajo el nombre viejo y el slot nuevo queda vacio, que
            // es lo que se veia como "la capa pinta de blanco".
            int migrated = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != ShaderName)
                {
                    continue;
                }

                if (MigrateLegacyMaterial(material))
                {
                    migrated++;
                    Debug.Log($"[PaintTools] Material migrado a Textura 1: {path}", material);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PaintTools] Migracion terminada: {migrated} material(es) actualizados.");
        }

        private static bool MigrateLegacyMaterial(Material material)
        {
            var so = new SerializedObject(material);
            SerializedProperty saved = so.FindProperty("m_SavedProperties");

            Texture legacyTex = null;
            bool hasTex = TryFindEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathTex", out int texIndex);
            if (hasTex)
            {
                legacyTex = saved.FindPropertyRelative("m_TexEnvs").GetArrayElementAtIndex(texIndex)
                    .FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }

            bool hasNormal = TryFindEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathNormalMap", out int normalIndex);
            Texture legacyNormal = hasNormal
                ? saved.FindPropertyRelative("m_TexEnvs").GetArrayElementAtIndex(normalIndex)
                    .FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture
                : null;

            bool hasTiling = TryFindEntry(saved.FindPropertyRelative("m_Floats"), "_PathTiling", out int tilingIndex);
            float legacyTiling = hasTiling
                ? saved.FindPropertyRelative("m_Floats").GetArrayElementAtIndex(tilingIndex).FindPropertyRelative("second").floatValue
                : 0f;

            bool hasStrength = TryFindEntry(saved.FindPropertyRelative("m_Floats"), "_PathNormalStrength", out int strengthIndex);
            float legacyStrength = hasStrength
                ? saved.FindPropertyRelative("m_Floats").GetArrayElementAtIndex(strengthIndex).FindPropertyRelative("second").floatValue
                : 0f;

            bool hasColor = TryFindEntry(saved.FindPropertyRelative("m_Colors"), "_PathColor", out int colorIndex);
            Color legacyColor = hasColor
                ? saved.FindPropertyRelative("m_Colors").GetArrayElementAtIndex(colorIndex).FindPropertyRelative("second").colorValue
                : Color.white;

            bool hasMask = TryFindEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathMask", out _);

            if (!hasTex && !hasNormal && !hasTiling && !hasStrength && !hasColor && !hasMask)
            {
                return false;
            }

            // Se copia al slot nuevo solo si esta vacio: nunca pisa algo ya asignado a mano.
            if (legacyTex != null && material.GetTexture("_Tex1") == null)
            {
                material.SetTexture("_Tex1", legacyTex);
            }
            if (legacyNormal != null && material.GetTexture("_Tex1NormalMap") == null)
            {
                material.SetTexture("_Tex1NormalMap", legacyNormal);
            }
            if (hasTiling)
            {
                material.SetFloat("_Tex1Tiling", legacyTiling);
            }
            if (hasStrength)
            {
                material.SetFloat("_Tex1NormalStrength", legacyStrength);
            }
            if (hasColor)
            {
                material.SetColor("_Tex1Color", legacyColor);
            }

            // Se borran las entradas viejas: deja el .mat limpio y hace la migracion
            // idempotente (una segunda corrida no encuentra nada que copiar).
            so.Update();
            saved = so.FindProperty("m_SavedProperties");
            RemoveEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathTex");
            RemoveEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathNormalMap");
            RemoveEntry(saved.FindPropertyRelative("m_TexEnvs"), "_PathMask");
            RemoveEntry(saved.FindPropertyRelative("m_Floats"), "_PathTiling");
            RemoveEntry(saved.FindPropertyRelative("m_Floats"), "_PathNormalStrength");
            RemoveEntry(saved.FindPropertyRelative("m_Colors"), "_PathColor");
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool TryFindEntry(SerializedProperty array, string name, out int index)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue == name)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static void RemoveEntry(SerializedProperty array, string name)
        {
            if (TryFindEntry(array, name, out int index))
            {
                array.DeleteArrayElementAtIndex(index);
            }
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
