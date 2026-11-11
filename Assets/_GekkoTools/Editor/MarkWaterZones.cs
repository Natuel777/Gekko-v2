using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gekko.Tools.EditorTools
{
    /// <summary>
    /// Le pone un WaterZone a toda el agua visual de la escena abierta, para que Gekko cuente como "en el agua" (y no pueda
    /// usar el swing ni el grapple) cuando está metido en ella.
    ///
    /// Por que existe: el agua de algunas escenas (LvlTwo: los CascadeWater2, Water, Initial Water) son planos con malla y
    /// SIN collider activo, y no estan en la layer Water. Ninguna consulta de fisica los ve, asi que GekkoWater no los
    /// detecta. WaterZone define la zona con la caja del propio renderer.
    ///
    /// Que marca: todo MeshRenderer de la escena cuyo material este en Assets/Art/Materials/Water (M_Water, Waterfall...).
    /// Se saltea lo que ya esta en la layer Water (ese lo cubre el raycast de GekkoWater) y lo que ya tiene un WaterZone,
    /// asi que es segura de correr dos veces.
    ///
    /// Ojo con los rios con spline: su caja es la de toda la malla, que queda mucho mas grande que el rio en las curvas. Para
    /// esos conviene sacar el WaterZone (o no correr la herramienta en esa escena) y revisar la caja celeste en la Scene View.
    ///
    /// Se hace desde el Editor y no editando el archivo de la escena para no pisar cambios sin guardar. Se puede deshacer
    /// con Ctrl+Z. Despues de usarla hay que guardar la escena (Ctrl+S).
    /// </summary>
    public static class MarkWaterZones
    {
        private const string MenuPath = "Tools/Gekko/Marcar agua (WaterZone) en la escena abierta";

        // Carpeta de los materiales de agua del proyecto.
        private const string WaterMaterialsFolder = "Assets/Art/Materials/Water/";

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if(EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Marcar agua", "Salí de Play Mode primero: los cambios hechos jugando se pierden al salir.", "OK");
                return;
            }

            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Marcar agua (WaterZone)");
            int undoGroup = Undo.GetCurrentGroup();

            StringBuilder marked = new StringBuilder();
            int markedCount = 0, alreadyCount = 0, layerCount = 0;

            foreach(MeshRenderer renderer in renderers)
            {
                GameObject go = renderer.gameObject;

                // Solo objetos de escena (no assets de prefab).
                if(EditorUtility.IsPersistent(go)) continue;

                if(!UsesWaterMaterial(renderer)) continue;

                // Ya en la layer Water: lo cubre el raycast de GekkoWater.
                if(go.layer == GekkoWater.WaterLayer)
                {
                    layerCount++;
                    continue;
                }

                if(go.GetComponent<WaterZone>() != null)
                {
                    alreadyCount++;
                    continue;
                }

                Undo.AddComponent<WaterZone>(go);
                EditorSceneManager.MarkSceneDirty(go.scene);
                markedCount++;
                marked.Append("\n- ").Append(PathOf(go.transform));
            }

            Undo.CollapseUndoOperations(undoGroup);

            string summary = markedCount > 0
                ? $"WaterZone agregado a {markedCount} objeto(s) de agua. Falta guardar la escena (Ctrl+S)."
                : "No se agregó ningún WaterZone.";

            summary += $"\nYa tenían WaterZone: {alreadyCount}. En la layer Water (los cubre el raycast): {layerCount}.";

            Debug.Log($"[MarkWaterZones] {summary}{marked}");
            EditorUtility.DisplayDialog("Marcar agua", summary + marked, "OK");
        }

        // true si alguno de los materiales del renderer es de la carpeta de materiales de agua.
        private static bool UsesWaterMaterial(Renderer renderer)
        {
            foreach(Material material in renderer.sharedMaterials)
            {
                if(material == null) continue;

                if(AssetDatabase.GetAssetPath(material).StartsWith(WaterMaterialsFolder)) return true;
            }

            return false;
        }

        // Ruta del objeto en la Hierarchy (Padre/Hijo/Nieto), para el reporte.
        private static string PathOf(Transform t)
        {
            string path = t.name;

            while(t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
