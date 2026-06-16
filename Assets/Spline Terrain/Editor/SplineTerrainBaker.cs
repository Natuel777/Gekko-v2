using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineTerrainTool.EditorTools
{
    /// <summary>
    /// "Bake" logic: saves a <see cref="SplineTerrainData"/> with all the parameters and the
    /// spline shape, plus the visual mesh and the low-poly collider mesh as .asset assets.
    /// </summary>
    public static class SplineTerrainBaker
    {
        private const string DefaultFolder = "Assets/Spline Terrain/Baked";

        public static SplineTerrainData Bake(SplineTerrain terrain)
        {
            if (terrain == null) return null;
            var container = terrain.splineContainer != null
                ? terrain.splineContainer
                : terrain.GetComponent<SplineContainer>();
            if (container == null)
            {
                Debug.LogWarning("[SplineTerrain] There is no SplineContainer to bake.");
                return null;
            }

            // Configurable folders (meshes and SO can be different or the same).
            string meshFolder = EnsureFolder(terrain.meshSaveFolder);
            string dataFolder = EnsureFolder(terrain.dataSaveFolder);
            string safeName = MakeSafeName(terrain.gameObject.name);

            // 1) Build independent meshes. The collider is optional.
            Mesh visual = terrain.BuildVisualMesh();
            if (visual == null)
            {
                Debug.LogWarning("[SplineTerrain] The visual mesh came out empty; not baking.");
                return null;
            }
            Mesh collider = terrain.bakeCollider ? terrain.BuildColliderMesh() : null;

            string visualPath = AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Visual.asset");
            string colliderPath = AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{safeName}_Collider.asset");
            string dataPath = AssetDatabase.GenerateUniqueAssetPath($"{dataFolder}/{safeName}_Data.asset");

            AssetDatabase.CreateAsset(visual, visualPath);
            if (collider != null) AssetDatabase.CreateAsset(collider, colliderPath);

            // 2) Create the SO with settings + spline snapshot + mesh refs.
            var data = ScriptableObject.CreateInstance<SplineTerrainData>();
            data.CaptureFrom(new SplineTerrainComponentSnapshot
            {
                Settings = terrain.Settings,
                Spline = container.Spline,
                Floor = terrain.FloorMaterial,
                Wall = terrain.WallMaterial,
                Edge = terrain.EdgeMaterial
            });
            data.visualMesh = visual;
            data.colliderMesh = collider;

            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();

            // 3) Apply the baked mesh and the low-poly collider to the object in the scene.
            Undo.RecordObject(terrain, "Bake Spline Terrain");
            var mf = terrain.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = visual;

            var mc = terrain.GetComponent<MeshCollider>();
            if (collider != null)
            {
                if (mc == null) mc = Undo.AddComponent<MeshCollider>(terrain.gameObject);
                mc.sharedMesh = collider;
            }

            terrain.bakedData = data;
            EditorUtility.SetDirty(terrain);

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            Debug.Log($"[SplineTerrain] Bake complete: {dataPath}");
            return data;
        }

        /// <summary>Reloads settings + spline shape from an SO onto the terrain.</summary>
        public static void LoadInto(SplineTerrain terrain, SplineTerrainData data)
        {
            if (terrain == null || data == null) return;
            var container = terrain.splineContainer != null
                ? terrain.splineContainer
                : terrain.GetComponent<SplineContainer>();
            if (container == null) return;

            Undo.RecordObject(terrain, "Load Spline Terrain Data");
            data.ApplyToSpline(container.Spline);
            terrain.ApplySettings(data.settings);
            if (data.floorMaterial != null) terrain.FloorMaterial = data.floorMaterial;
            if (data.wallMaterial != null) terrain.WallMaterial = data.wallMaterial;
            if (data.edgeMaterial != null) terrain.EdgeMaterial = data.edgeMaterial;
            terrain.bakedData = data;
            terrain.Regenerate();
            EditorUtility.SetDirty(terrain);
        }

        /// <summary>Ensures the folder exists (creating it recursively) and returns a valid path.</summary>
        private static string EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) path = DefaultFolder;
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets")) path = DefaultFolder;
            if (AssetDatabase.IsValidFolder(path)) return path;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return path;
        }

        private static string MakeSafeName(string raw)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c, '_');
            return raw.Replace(' ', '_');
        }
    }
}
